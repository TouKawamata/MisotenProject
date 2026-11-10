using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.CharacterElectricity
{
    /// <summary>
    /// born model などのキャラクター（SkinnedMeshRenderer）に追従し、
    /// 放電シェーダー（S_CharacterElectricity）とリアルタイムに同期して
    /// 3D空間へのアーク飛散スパーク・プラズマ電離グロー・衝撃バーストを発生させるパーティクルコントローラー。
    /// 
    /// 【特徴】
    /// 1. SkinnedMeshRenderer 完全追従: ボーン変形・走行アニメーション中のメッシュ表面から直接火花を射出。
    /// 2. 放電シェーダー・色・スタン完全同期: スタン状態、発光強度、放電色、被弾ベクトルと自動連動。
    /// 3. パターン別演出変調: P1(アーク), P4(ゴールド), P7(間欠), P10(チリチリ), P11(重放電), P16(被弾飛散) 等の挙動変化に対応。
    /// 4. 完全ゼロGC (0 Alloc): Update() 内でのアロケーションゼロ、高速な MinMaxCurve / MinMaxGradient 更新。
    /// 5. ワンクリック自動構築: 不足している ParticleSystem オブジェクトやマテリアル参照をインスペクターから1秒で自動生成。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterElectricityParticles : MonoBehaviour
    {
        [Header("1. 連動対象レンダラー")]
        [Tooltip("火花を発生させる対象（未設定時は親または自身から SkinnedMeshRenderer を自動検出）")]
        [SerializeField] private Renderer _targetRenderer;

        [Header("2. パーティクルエミッター参照")]
        [Tooltip("高速アーク飛散スパーク（伸びるニードルスパーク・ワールド空間飛散）")]
        [SerializeField] private ParticleSystem _sparkEmitter;

        [Tooltip("プラズマ電離グローフラッシュ（装甲周囲の柔らかな電離発光）")]
        [SerializeField] private ParticleSystem _plasmaGlowEmitter;

        [Tooltip("短絡・被弾衝撃バースト（スタン発生時や強放電時に炸裂するバースト火花）")]
        [SerializeField] private ParticleSystem _burstEmitter;

        [Header("3. パラメーター調整")]
        [Tooltip("パーティクル全体の有効 / 無効切り替え")]
        [SerializeField] private bool _particlesEnabled = true;

        [Range(0f, 5f)]
        [Tooltip("パーティクル放出量の全体倍率")]
        [SerializeField] private float _rateMultiplier = 1.0f;

        [Range(0.2f, 3f)]
        [Tooltip("火花の飛散速度倍率")]
        [SerializeField] private float _speedMultiplier = 1.0f;

        [Range(0.2f, 3f)]
        [Tooltip("火花のサイズ倍率")]
        [SerializeField] private float _sizeMultiplier = 1.0f;

        [Header("4. マテリアルプリセット参照")]
        [SerializeField] private Material _matNeedleSpark;
        [SerializeField] private Material _matPlasmaGlow;
        [SerializeField] private Material _matArcFork;

        // ランタイム制御用キャッシュ（ゼロGC）
        private float _lastBurstTimer = 0f;
        private float _nextPeriodicBurstInterval = 1.0f;
        private ParticleSystem.Burst[] _burstCache = new ParticleSystem.Burst[1];

        // 公開プロパティ
        public bool ParticlesEnabled { get => _particlesEnabled; set => _particlesEnabled = value; }
        public float RateMultiplier { get => _rateMultiplier; set => _rateMultiplier = value; }
        public float SpeedMultiplier { get => _speedMultiplier; set => _speedMultiplier = value; }
        public float SizeMultiplier { get => _sizeMultiplier; set => _sizeMultiplier = value; }
        public ParticleSystem SparkEmitter => _sparkEmitter;
        public ParticleSystem PlasmaGlowEmitter => _plasmaGlowEmitter;
        public ParticleSystem BurstEmitter => _burstEmitter;
        public Renderer TargetRenderer
        {
            get => _targetRenderer;
            set
            {
                _targetRenderer = value;
                BindEmittersToRenderer();
            }
        }

        private void OnEnable()
        {
            EnsureEmitters();
            EnsureTargetRenderer();
            LoadMaterials();
        }

        /// <summary>
        /// 子階層に既に存在するパーティクルエミッターを自動検知して再バインド（ドメインリロード・シーンロード復帰用）
        /// </summary>
        public void EnsureEmitters()
        {
            if (_sparkEmitter == null)
            {
                Transform t = transform.Find("PS_NeedleSparks");
                if (t != null) _sparkEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_plasmaGlowEmitter == null)
            {
                Transform t = transform.Find("PS_PlasmaGlow");
                if (t != null) _plasmaGlowEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_burstEmitter == null)
            {
                Transform t = transform.Find("PS_ShockBurst");
                if (t != null) _burstEmitter = t.GetComponent<ParticleSystem>();
            }
        }

        /// <summary>
        /// 対象レンダラーの自動解決（親・子・ルート階層から SkinnedMeshRenderer を確実に取得）
        /// </summary>
        public void EnsureTargetRenderer()
        {
            if (_targetRenderer == null)
            {
                // 1. 親階層の CharacterElectricityPreview から対象レンダラーを引き継ぎ
                var preview = GetComponentInParent<CharacterElectricityPreview>();
                if (preview != null && preview.TargetRenderer != null)
                {
                    _targetRenderer = preview.TargetRenderer;
                }

                // 2. 自身・親・子・ルート階層から SkinnedMeshRenderer を検索
                if (_targetRenderer == null)
                {
                    _targetRenderer = GetComponent<SkinnedMeshRenderer>();
                }
                if (_targetRenderer == null && transform.parent != null)
                {
                    _targetRenderer = transform.parent.GetComponentInChildren<SkinnedMeshRenderer>();
                }
                if (_targetRenderer == null)
                {
                    _targetRenderer = transform.root.GetComponentInChildren<SkinnedMeshRenderer>();
                }

                // 3. MeshRenderer のフォールバック検索
                if (_targetRenderer == null)
                {
                    _targetRenderer = GetComponent<MeshRenderer>();
                }
                if (_targetRenderer == null && transform.parent != null)
                {
                    _targetRenderer = transform.parent.GetComponentInChildren<MeshRenderer>();
                }
                if (_targetRenderer == null)
                {
                    _targetRenderer = transform.root.GetComponentInChildren<MeshRenderer>();
                }
            }

            // エミッターの Shape にレンダラーを割り当て
            BindEmittersToRenderer();
        }

        /// <summary>
        /// マテリアルアセットの自動解決
        /// </summary>
        public void LoadMaterials()
        {
#if UNITY_EDITOR
            const string matDir = "Assets/Deverop/AT_Murayama/CharacterElectricity/Materials";
            if (_matNeedleSpark == null)
                _matNeedleSpark = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_NeedleSpark.mat");
            if (_matPlasmaGlow == null)
                _matPlasmaGlow = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_PlasmaGlow.mat");
            if (_matArcFork == null)
                _matArcFork = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_ArcFork.mat");
#endif
        }

        /// <summary>
        /// 各エミッターの ShapeModule に対象レンダラーをバインド
        /// </summary>
        public void BindEmittersToRenderer()
        {
            EnsureEmitters();
            if (_targetRenderer == null) return;

            SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
            MeshRenderer mr = _targetRenderer as MeshRenderer;

            BindShape(_sparkEmitter, smr, mr);
            BindShape(_plasmaGlowEmitter, smr, mr);
            BindShape(_burstEmitter, smr, mr);
        }

        private void BindShape(ParticleSystem ps, SkinnedMeshRenderer smr, MeshRenderer mr)
        {
            if (ps == null) return;

            var shape = ps.shape;
            shape.enabled = true;
            shape.useMeshColors = false; // メッシュ頂点カラーによる意図しない黒つぶれを防止

            if (smr != null)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = smr;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.normalOffset = 0.02f;
            }
            else if (mr != null)
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = mr;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.normalOffset = 0.02f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;
            }
        }

        /// <summary>
        /// CharacterElectricityPreview から毎フレーム呼ばれるゼロGC高速更新メソッド
        /// 全20パターンの演出変調に完全対応
        /// </summary>
        public void SynchronizeWithElectricity(
            float intensityMultiplier,
            float speedMultiplier,
            Color electricColor,
            float stunWeight,
            CharacterElectricityPreview.ElectricityPattern pattern,
            Vector2 impactDirectionUV)
        {
            if (!_particlesEnabled || stunWeight <= 0.001f || pattern == CharacterElectricityPreview.ElectricityPattern.Original)
            {
                // 放電停止・非スタン時は放出をスムーズに停止（既存の飛散粒子は自然消滅）
                StopEmissionGradually();
                return;
            }

            EnsureEmitters();
            if (_targetRenderer == null)
            {
                EnsureTargetRenderer();
            }

            float totalIntensity = intensityMultiplier * stunWeight * _rateMultiplier;
            float totalSpeed = speedMultiplier * _speedMultiplier;

            // 全20パターン別の放出係数＆挙動チューニング
            float sparkRateBase = 22.0f;
            float glowRateBase = 10.0f;
            float sparkSpeedBase = 2.2f;
            float sparkSizeBase = 0.22f * _sizeMultiplier;
            bool enablePeriodicBurst = false;

            switch (pattern)
            {
                case CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc:
                    sparkRateBase = 28.0f;
                    sparkSpeedBase = 2.6f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern2_Pulse:
                    // 脈動に合わせてサイン波で放出レートが激しく波打つ
                    float pulseWave = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.0f * speedMultiplier);
                    sparkRateBase = Mathf.Lerp(12.0f, 42.0f, pulseWave);
                    glowRateBase = Mathf.Lerp(6.0f, 22.0f, pulseWave);
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern3_Plasma:
                    // 纏わりプラズマ：グロー多め、スパークは短く密着
                    sparkRateBase = 14.0f;
                    glowRateBase = 26.0f;
                    sparkSpeedBase = 1.1f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold:
                    // 超帯電ゴールド：高密度・高エネルギー
                    sparkRateBase = 44.0f;
                    glowRateBase = 20.0f;
                    sparkSpeedBase = 3.4f;
                    sparkSizeBase *= 1.3f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern5_CleanStreak:
                    // クリーンストリーク：低密度で明瞭
                    sparkRateBase = 10.0f;
                    glowRateBase = 4.0f;
                    sparkSpeedBase = 2.8f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern6_FresnelRim:
                    // リム放電：輪郭グロー強調
                    sparkRateBase = 12.0f;
                    glowRateBase = 16.0f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern7_ShortSparks:
                    // 間欠ショート：通常時は微小放出、周期的にバチッと小バースト
                    sparkRateBase = 7.0f;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.8f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern8_CyberCircuit:
                    sparkRateBase = 22.0f;
                    sparkSpeedBase = 3.6f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern9_MultiChain:
                    // 多点連鎖：小バーストが連続発生
                    sparkRateBase = 14.0f;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.42f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern10_MicroCrackle:
                    // 微小チリチリ：超高頻度・微小サイズ
                    sparkRateBase = 58.0f;
                    sparkSizeBase *= 0.5f;
                    sparkSpeedBase = 1.2f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern11_HeavyArcBurst:
                    // 高圧重バースト：普段は静かでたまにドカンと破裂
                    sparkRateBase = 8.0f;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 1.35f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern12_ThermalAfterglow:
                    // 余熱スパーク：燃える火の粉のようにゆっくり漂う
                    sparkRateBase = 22.0f;
                    sparkSpeedBase = 0.9f;
                    sparkSizeBase *= 1.15f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern13_AnatomicalGrounding:
                    // 3D解剖学的接地アーク：足元・地面方向への接地放出
                    sparkRateBase = 30.0f;
                    sparkSpeedBase = 3.2f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern14_LichtenbergBranch:
                    // リーヒテンベルク樹状分岐：フラクタル分岐バースト
                    sparkRateBase = 24.0f;
                    sparkSpeedBase = 2.8f;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.55f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern15_SteppedLeader:
                    // 絶縁破壊ステップトリーダー：スタッカート断続放出
                    float staccato = Mathf.Sin(Time.time * 28.0f) > 0.1f ? 1.5f : 0.35f;
                    sparkRateBase = 22.0f * staccato;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.75f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern16_ImpactVector:
                    // 被弾ベクトル：被弾方向へ勢いよく飛散
                    sparkRateBase = 32.0f;
                    sparkSpeedBase = 4.2f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern17_ShockwaveDistortion:
                    // 衝撃波法線ディストーション＆爆圧スパーク
                    sparkRateBase = 26.0f;
                    sparkSpeedBase = 3.8f;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.9f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern18_MagneticWhip:
                    // 双極マグネティック・アークウィップ：乱流しなりスパーク
                    sparkRateBase = 28.0f;
                    sparkSpeedBase = 3.4f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern19_ArmorSeamShort:
                    // 装甲シーム沿い短絡奔流：隙間からの高速ショート
                    sparkRateBase = 38.0f;
                    sparkSizeBase *= 0.7f;
                    sparkSpeedBase = 1.8f;
                    break;

                case CharacterElectricityPreview.ElectricityPattern.Pattern20_CriticalStrobe:
                    // 臨界ストロボ：不規則な乱数ストロボ放出
                    float strobe = Mathf.Sin(Time.time * 24.0f) > 0.3f ? 1.8f : 0.2f;
                    sparkRateBase = 22.0f * strobe;
                    enablePeriodicBurst = true;
                    _nextPeriodicBurstInterval = 0.28f;
                    break;
            }

            // 1. スパークエミッターの更新
            if (_sparkEmitter != null)
            {
                if (!_sparkEmitter.isPlaying) _sparkEmitter.Play();

                var emission = _sparkEmitter.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(sparkRateBase * totalIntensity);

                var main = _sparkEmitter.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeedBase * 0.7f * totalSpeed, sparkSpeedBase * 1.3f * totalSpeed);
                main.startSize = new ParticleSystem.MinMaxCurve(sparkSizeBase * 0.8f, sparkSizeBase * 1.25f);
                main.startColor = new ParticleSystem.MinMaxGradient(electricColor);
            }

            // 2. プラズマ電離グローエミッターの更新
            if (_plasmaGlowEmitter != null)
            {
                if (!_plasmaGlowEmitter.isPlaying) _plasmaGlowEmitter.Play();

                var emission = _plasmaGlowEmitter.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(glowRateBase * totalIntensity);

                var main = _plasmaGlowEmitter.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(electricColor.r, electricColor.g, electricColor.b, 0.45f));
            }

            // 3. 周期バーストの判定・トリガー
            if (enablePeriodicBurst)
            {
                _lastBurstTimer += Time.deltaTime;
                if (_lastBurstTimer >= _nextPeriodicBurstInterval)
                {
                    _lastBurstTimer = 0f;
                    TriggerBurst(12, totalSpeed, electricColor, impactDirectionUV);
                }
            }
        }

        /// <summary>
        /// スタン開始時・被弾時・強放電時に炸裂するワンショットバースト火花
        /// </summary>
        public void TriggerBurst(int count = 20, float speedScale = 1.0f, Color? burstColor = null, Vector2? directionUV = null)
        {
            EnsureEmitters();
            if (_burstEmitter == null) return;

            Color col = burstColor ?? Color.white;
            var main = _burstEmitter.main;
            main.startColor = new ParticleSystem.MinMaxGradient(col);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f * speedScale * _speedMultiplier, 6.5f * speedScale * _speedMultiplier);

            // 被弾ベクトルが指定されている場合（Pattern 16連動）、噴射速度を設定
            var vel = _burstEmitter.velocityOverLifetime;
            if (directionUV.HasValue && directionUV.Value.sqrMagnitude > 0.001f)
            {
                Vector2 dir = directionUV.Value;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(dir.x * 4.0f * speedScale);
                vel.y = new ParticleSystem.MinMaxCurve(dir.y * 4.0f * speedScale);
                vel.z = new ParticleSystem.MinMaxCurve(-2.0f * speedScale); // 後方へ吹き飛び
            }
            else
            {
                // 【残留汚染防止】ベクトル未指定時は velocityOverLifetime を確実に無効化
                vel.enabled = false;
            }

            if (!_burstEmitter.isPlaying)
            {
                _burstEmitter.Play();
            }
            _burstEmitter.Emit(Mathf.RoundToInt(count * _rateMultiplier));
        }

        /// <summary>
        /// 放電停止時のスムーズな減衰停止
        /// </summary>
        public void StopEmissionGradually()
        {
            _lastBurstTimer = 0f;
            if (_sparkEmitter != null)
            {
                var emission = _sparkEmitter.emission;
                emission.rateOverTime = 0f;
            }
            if (_plasmaGlowEmitter != null)
            {
                var emission = _plasmaGlowEmitter.emission;
                emission.rateOverTime = 0f;
            }
        }

        /// <summary>
        /// 全パーティクルを即座に消去・停止
        /// </summary>
        public void ClearAndStop()
        {
            _lastBurstTimer = 0f;
            if (_sparkEmitter != null)
            {
                _sparkEmitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_plasmaGlowEmitter != null)
            {
                _plasmaGlowEmitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_burstEmitter != null)
            {
                _burstEmitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// インスペクターから呼び出せるパーティクルシステム自動生成・再構築メソッド
        /// </summary>
        public void SetupParticlesHierarchy()
        {
            LoadMaterials();
            EnsureTargetRenderer();

            // 1. スパークエミッターの生成 / 再構成
            _sparkEmitter = EnsureChildParticleSystem("PS_NeedleSparks", _sparkEmitter, _matNeedleSpark, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.30f);
                main.maxParticles = 120;
                main.gravityModifier = 0.05f;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 25.0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.65f;
                noise.frequency = 3.5f;
                noise.octaveCount = 2;
                noise.scrollSpeed = 2.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                var col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(g);

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.065f;
                psr.lengthScale = 2.2f;
                psr.sharedMaterial = _matNeedleSpark;
                psr.enableGPUInstancing = true;
            });

            // 2. プラズマ電離グローエミッターの生成 / 再構成
            _plasmaGlowEmitter = EnsureChildParticleSystem("PS_PlasmaGlow", _plasmaGlowEmitter, _matPlasmaGlow, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
                main.maxParticles = 40;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 12.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = _matPlasmaGlow;
                psr.enableGPUInstancing = true;
            });

            // 3. 短絡・被弾衝撃バーストエミッターの生成 / 再構成
            _burstEmitter = EnsureChildParticleSystem("PS_ShockBurst", _burstEmitter, _matArcFork, ps =>
            {
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 7.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // ランダム回転による自然な飛散演出
                main.maxParticles = 80;
                main.gravityModifier = 0.1f;

                var emission = ps.emission;
                emission.enabled = false;
                emission.rateOverTime = 0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.85f;
                noise.frequency = 4.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.085f;
                psr.lengthScale = 2.5f;
                psr.sharedMaterial = _matArcFork;
                psr.enableGPUInstancing = true;
            });

            BindEmittersToRenderer();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log("[CharacterElectricityParticles] パーティクルシステムのセットアップが完了しました！", this);
        }

        private ParticleSystem EnsureChildParticleSystem(string childName, ParticleSystem existingPs, Material defaultMat, Action<ParticleSystem> configureAction)
        {
            ParticleSystem ps = existingPs;
            if (ps == null)
            {
                Transform child = transform.Find(childName);
                if (child != null)
                {
                    ps = child.GetComponent<ParticleSystem>();
                }
            }

            if (ps == null)
            {
                GameObject childGo = new GameObject(childName);
                if (gameObject.hideFlags.HasFlag(HideFlags.DontSave))
                {
                    childGo.hideFlags |= HideFlags.DontSave;
                }
#if UNITY_EDITOR
                if (!Application.isPlaying && !EditorApplication.isUpdating && !EditorApplication.isCompiling && (childGo.hideFlags & HideFlags.DontSave) == 0)
                {
                    try { Undo.RegisterCreatedObjectUndo(childGo, $"Create {childName}"); } catch {}
                }
#endif
                childGo.transform.SetParent(transform, false);
                childGo.transform.localPosition = Vector3.zero;
                childGo.transform.localRotation = Quaternion.identity;
                childGo.transform.localScale = Vector3.one;

                ps = childGo.AddComponent<ParticleSystem>();
            }

            configureAction(ps);
            return ps;
        }
    }
}
