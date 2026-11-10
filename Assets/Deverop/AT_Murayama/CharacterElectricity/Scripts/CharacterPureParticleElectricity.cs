using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.CharacterElectricity
{
    /// <summary>
    /// 純粋パーティクル放電パターン列挙型（全8パターン ＋ スタンバイ）
    /// メッシュシェーダーを一切変更せず、100%パーティクルのみで多彩な電流・放電表現を実現します。
    /// </summary>
    public enum PureParticlePattern
    {
        [InspectorName("⚪ 停止 / スタンバイ (Off / Standby)")]
        Off = 0,

        [InspectorName("⚡ パターン1: アーク・ライトニング・リボン (Arc Lightning Ribbons)")]
        Pattern1_ArcRibbons = 1,

        [InspectorName("⚡ パターン2: スパスミック・ニードルストーム (Spasmic Needle Storm)")]
        Pattern2_SpasmicNeedles = 2,

        [InspectorName("⚡ パターン3: 高圧ストロボ・電離オーラ (High-Voltage Strobe Aura)")]
        Pattern3_StrobeAura = 3,

        [InspectorName("⚡ パターン4: 樹状分岐フォーク・カスケード (Branching Fork Arcs)")]
        Pattern4_BranchingForks = 4,

        [InspectorName("⚡ パターン5: 表面這い回りプラズマ・クリープ (Crawling Plasma Creep)")]
        Pattern5_CrawlingPlasmaCreep = 5,

        [InspectorName("⚡ パターン6: 過負荷臨界メルトダウン (Overdrive Critical Meltdown)")]
        Pattern6_OverdriveMeltdown = 6,

        [InspectorName("⚡ パターン7: 磁場旋回バイポーラ・アーク (Magnetic Vortex Bipolar)")]
        Pattern7_MagneticVortex = 7,

        [InspectorName("⚡ パターン8: 間欠短絡・接触不良スパーク (Intermittent Short-Circuit)")]
        Pattern8_IntermittentShort = 8,
    }

    /// <summary>
    /// born model などのキャラクター（SkinnedMeshRenderer）に対して、
    /// キャラクター本来のマテリアルやシェーダーを一切書き換えず、
    /// 100% パーティクル（リボン稲妻軌跡・高速スパーク・電離オーラ・樹状フォーク・表面這い回り）のみで
    /// 迫力ある電流・放電演出を実現する独立型パーティクルコントローラー。
    /// 
    /// 【特徴】
    /// 1. メッシュシェーダー完全不干渉: キャラクターの材質・テクスチャ（shiro/ao/kuro等）をそのまま保持。
    /// 2. SkinnedMeshRenderer 完全追従: ボーンアニメーション変形中のポリゴン表面から直接パーティクル＆リボン軌跡を生成。
    /// 3. 多彩な8つの純粋パーティクルパターン: リボン稲妻、ニードル放射、高圧ストロボ、樹状放電、這い回りプラズマ等。
    /// 4. 完全ゼロGC (0 Alloc): Update() 内でのヒープアロケーションは 0 バイト。
    /// 5. スタン・被弾・衝突連携API: RacerManagerBase等のゲームスクリプトから1行で呼び出し可能。
    /// 6. ワンクリック自動構築: 不足している子エミッターやマテリアルをインスペクターから即座に自動生成。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterPureParticleElectricity : MonoBehaviour
    {
        [Header("1. 連動対象レンダラー")]
        [Tooltip("電流を発生させる対象（未設定時は自身・親・子から SkinnedMeshRenderer を自動検出）")]
        [SerializeField] private Renderer _targetRenderer;

        [Header("2. 現在の放電パターン")]
        [Tooltip("純粋パーティクルによる放電パターン")]
        [SerializeField] private PureParticlePattern _currentPattern = PureParticlePattern.Pattern1_ArcRibbons;

        [Header("3. パーティクルエミッター参照（6系統）")]
        [Tooltip("1. 電撃アークリボン（Trailsモジュールによるジグザグ3D稲妻軌跡）")]
        [SerializeField] private ParticleSystem _ribbonEmitter;

        [Tooltip("2. 高速スパーク（鋭いニードル状の放射放電針）")]
        [SerializeField] private ParticleSystem _needleEmitter;

        [Tooltip("3. 電離プラズマオーラ（装甲表面を覆うソフト発光皮膜）")]
        [SerializeField] private ParticleSystem _auraEmitter;

        [Tooltip("4. 樹状分岐フォークアーク（関節や突起から走る枝分かれ瞬発アーク）")]
        [SerializeField] private ParticleSystem _forkEmitter;

        [Tooltip("5. 表面這い回りチリチリ（装甲表面を這う超高密度微小放電）")]
        [SerializeField] private ParticleSystem _crackleEmitter;

        [Tooltip("6. 短絡・被弾衝撃バースト（被弾時やスタン開始時に炸裂するバースト火花）")]
        [SerializeField] private ParticleSystem _burstEmitter;

        [Header("4. パラメーター調整")]
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

        [Tooltip("3Dリボン稲妻をワールド空間に引きずるか（ONにすると高速走行時に背後へ長く稲妻軌跡を残す）")]
        [SerializeField] private bool _ribbonWorldSpace = false;

        [Header("5. スタン状態シミュレーション")]
        [Tooltip("スタン中フラグ（外部APIからも操作可能）")]
        [SerializeField] private bool _isStunned = true;

        [Range(0f, 1f)]
        [Tooltip("現在のスタン重み（0: 無放電, 1: 最大放電）")]
        [SerializeField] private float _currentStunWeight = 1.0f;

        [Range(0.1f, 10f)]
        [Tooltip("スタン重みのフェード速度")]
        [SerializeField] private float _stunFadeSpeed = 4.0f;

        [Header("6. カラーカスタム")]
        [Tooltip("パターンのデフォルト色を上書きするかどうか")]
        [SerializeField] private bool _overrideColor = false;

        [ColorUsage(true, true)]
        [Tooltip("カスタム放電カラー (HDR)")]
        [SerializeField] private Color _customColor = new Color(0.25f, 0.85f, 2.5f, 1.0f);

        [Header("7. マテリアルアセット参照")]
        [SerializeField] private Material _matTrail;
        [SerializeField] private Material _matNeedle;
        [SerializeField] private Material _matPlasma;
        [SerializeField] private Material _matFork;
        [SerializeField] private Material _matCrackle;

        // ランタイムゼロGC用キャッシュ
        private float _periodicTimer = 0f;
        private float _staccatoTimer = 0f;
        private bool _staccatoState = true;
        private bool _isCleaningUp = false;

        // 公開プロパティ
        public PureParticlePattern CurrentPattern
        {
            get => _currentPattern;
            set
            {
                if (_currentPattern == value) return;
                _currentPattern = value;
                if (value != PureParticlePattern.Off)
                {
                    _particlesEnabled = true;
                }
                else
                {
                    _particlesEnabled = false;
                    _isStunned = false;
                    _currentStunWeight = 0.0f;
                    StopEmissionGradually();
                }
                ApplyPatternImmediate();
            }
        }

        public bool ParticlesEnabled
        {
            get => _particlesEnabled;
            set
            {
                _particlesEnabled = value;
                if (!value)
                {
                    StopEmissionGradually();
                }
            }
        }
        public float RateMultiplier { get => _rateMultiplier; set => _rateMultiplier = value; }
        public float SpeedMultiplier { get => _speedMultiplier; set => _speedMultiplier = value; }
        public float SizeMultiplier { get => _sizeMultiplier; set => _sizeMultiplier = value; }
        public bool RibbonWorldSpace
        {
            get => _ribbonWorldSpace;
            set
            {
                _ribbonWorldSpace = value;
                ApplyRibbonWorldSpace();
            }
        }
        public bool IsStunned => _isStunned;
        public float CurrentStunWeight => _currentStunWeight;
        public bool OverrideColor { get => _overrideColor; set => _overrideColor = value; }
        public Color CustomColor { get => _customColor; set => _customColor = value; }

        public ParticleSystem RibbonEmitter => _ribbonEmitter;
        public ParticleSystem NeedleEmitter => _needleEmitter;
        public ParticleSystem AuraEmitter => _auraEmitter;
        public ParticleSystem ForkEmitter => _forkEmitter;
        public ParticleSystem CrackleEmitter => _crackleEmitter;
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
            if (_isCleaningUp) return;
            EnsureEmitters();
            EnsureTargetRenderer();
            LoadMaterials();
        }

        private void Start()
        {
            if (_isCleaningUp) return;
            EnsureEmitters();
            EnsureTargetRenderer();
            LoadMaterials();
        }

        private void Reset()
        {
            ResetToOriginalAndCleanup(recordUndo: false);
            EnsureTargetRenderer();
            LoadMaterials();
        }

        private void OnDestroy()
        {
            // エディタ編集時またはコンポーネント単体削除（Remove Component）時の安全ガード
            // スクリプト解除時にキャラクターヒエラルキーに余計な子オブジェクトを残さない
            try
            {
                CleanupAllParticles(recordUndo: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterPureParticleElectricity] OnDestroy クリーンアップ警告: {ex.Message}", this);
            }
        }

        private void Update()
        {
            if (_isCleaningUp) return;

            // スタン重みのスムーズ補間（ゼロGC）
            float targetWeight = (_isStunned && _particlesEnabled) ? 1.0f : 0.0f;
            float dt = Application.isPlaying ? Time.deltaTime : 0.0333f;
            _currentStunWeight = Mathf.MoveTowards(_currentStunWeight, targetWeight, dt * _stunFadeSpeed);

            if (!_particlesEnabled) return;

            UpdateEmission(dt);
        }

        /// <summary>
        /// 子階層に既に存在するパーティクルエミッターを自動検知して再バインド
        /// </summary>
        public void EnsureEmitters()
        {
            if (_isCleaningUp || !_particlesEnabled) return;
            if (_ribbonEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_RibbonArcs");
                if (t != null) _ribbonEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_needleEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_NeedleSparks");
                if (t != null) _needleEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_auraEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_PlasmaAura");
                if (t != null) _auraEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_forkEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_ForkArcs");
                if (t != null) _forkEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_crackleEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_MicroCrackles");
                if (t != null) _crackleEmitter = t.GetComponent<ParticleSystem>();
            }
            if (_burstEmitter == null)
            {
                Transform t = transform.Find("PS_Pure_ShockBurst");
                if (t != null) _burstEmitter = t.GetComponent<ParticleSystem>();
            }
        }

        /// <summary>
        /// 対象レンダラーの自動解決（自身・親・子・ルートから SkinnedMeshRenderer を検索）
        /// </summary>
        public void EnsureTargetRenderer()
        {
            if (_isCleaningUp) return;

            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponent<SkinnedMeshRenderer>();
                if (_targetRenderer == null && transform.parent != null)
                {
                    _targetRenderer = transform.parent.GetComponentInChildren<SkinnedMeshRenderer>();
                }
                if (_targetRenderer == null)
                {
                    _targetRenderer = transform.root.GetComponentInChildren<SkinnedMeshRenderer>();
                }
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

            BindEmittersToRenderer();
        }

        /// <summary>
        /// マテリアルアセットの自動解決
        /// </summary>
        public void LoadMaterials()
        {
#if UNITY_EDITOR
            const string matDir = "Assets/Deverop/AT_Murayama/CharacterElectricity/Materials";
            if (_matTrail == null)
                _matTrail = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_Pure_Trail.mat");
            if (_matNeedle == null)
                _matNeedle = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_Pure_Needle.mat");
            if (_matPlasma == null)
                _matPlasma = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_Pure_Plasma.mat");
            if (_matFork == null)
                _matFork = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_Pure_Fork.mat");
            if (_matCrackle == null)
                _matCrackle = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/M_Electricity_Pure_Crackle.mat");
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

            BindShape(_ribbonEmitter, smr, mr, 0.025f);
            BindShape(_needleEmitter, smr, mr, 0.02f);
            BindShape(_auraEmitter, smr, mr, 0.015f);
            BindShape(_forkEmitter, smr, mr, 0.03f);
            BindShape(_crackleEmitter, smr, mr, 0.01f);
            BindShape(_burstEmitter, smr, mr, 0.02f);
        }

        private void BindShape(ParticleSystem ps, SkinnedMeshRenderer smr, MeshRenderer mr, float normalOffset)
        {
            if (ps == null) return;

            var shape = ps.shape;
            shape.enabled = true;
            shape.useMeshColors = false;

            if (smr != null)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = smr;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.normalOffset = normalOffset;
            }
            else if (mr != null)
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = mr;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.normalOffset = normalOffset;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;
            }
        }

        /// <summary>
        /// 毎フレームのパーティクル放出量とパラメーター更新（完全ゼロGC）
        /// </summary>
        private void UpdateEmission(float dt)
        {
            if (_currentStunWeight <= 0.001f || _currentPattern == PureParticlePattern.Off)
            {
                StopEmissionGradually();
                return;
            }

            EnsureEmitters();

            // パターン別ベース設定の取得
            GetPatternParameters(_currentPattern,
                out float ribbonRate, out float needleRate, out float auraRate,
                out float forkRate, out float crackleRate,
                out float baseSpeed, out float baseSize,
                out Color patternColor,
                out bool hasPeriodicBurst, out float burstInterval,
                out bool hasVortex, out bool isStaccato);

            // カラー上書き判定
            Color activeColor = _overrideColor ? _customColor : patternColor;

            // スタッカート（間欠断続放電）処理
            if (isStaccato)
            {
                _staccatoTimer += dt;
                if (_staccatoTimer >= 0.12f)
                {
                    _staccatoTimer = 0f;
                    _staccatoState = !_staccatoState;
                }
                float staccatoMul = _staccatoState ? 1.4f : 0.15f;
                ribbonRate *= staccatoMul;
                needleRate *= staccatoMul;
                forkRate *= staccatoMul;
            }

            float weight = _currentStunWeight * _rateMultiplier;
            float speedScale = _speedMultiplier * baseSpeed;
            float sizeScale = _sizeMultiplier * baseSize;

            // 各エミッター固有の基準自然スケール（メートル単位: 巨大化・白飛び膨張防止）
            const float kRibbonBaseSize = 0.12f;
            const float kNeedleBaseSize = 0.18f;
            const float kAuraBaseSize = 0.55f;
            const float kForkBaseSize = 0.28f;
            const float kCrackleBaseSize = 0.08f;

            // 1. リボンエミッター更新（3D稲妻軌跡）
            if (_ribbonEmitter != null)
            {
                UpdateEmitterRates(_ribbonEmitter, ribbonRate * weight, speedScale * 1.5f, kRibbonBaseSize * sizeScale, activeColor);
                var trails = _ribbonEmitter.trails;
                trails.worldSpace = _ribbonWorldSpace;
            }

            // 2. ニードルエミッター更新（高速放射スパーク）
            if (_needleEmitter != null)
            {
                UpdateEmitterRates(_needleEmitter, needleRate * weight, speedScale * 3.6f, kNeedleBaseSize * sizeScale, activeColor);
            }

            // 3. オーラエミッター更新（装甲を覆う柔らかな電離プラズマ皮膜）
            if (_auraEmitter != null)
            {
                Color auraColor = activeColor;
                auraColor.a = 0.42f;
                UpdateEmitterRates(_auraEmitter, auraRate * weight, speedScale * 0.25f, kAuraBaseSize * sizeScale, auraColor);
            }

            // 4. フォークエミッター更新（関節・突起からの樹状リーヒテンベルク瞬発アーク）
            if (_forkEmitter != null)
            {
                UpdateEmitterRates(_forkEmitter, forkRate * weight, speedScale * 2.2f, kForkBaseSize * sizeScale, activeColor);
            }

            // 5. チリチリエミッター更新（装甲表面に張り付く微小放電）
            if (_crackleEmitter != null)
            {
                UpdateEmitterRates(_crackleEmitter, crackleRate * weight, speedScale * 0.35f, kCrackleBaseSize * sizeScale, activeColor);
            }

            // 6. ボルテックス（磁場旋回）処理
            if (_ribbonEmitter != null)
            {
                var vol = _ribbonEmitter.velocityOverLifetime;
                if (hasVortex)
                {
                    vol.enabled = true;
                    vol.space = ParticleSystemSimulationSpace.Local;
                    vol.orbitalZ = new ParticleSystem.MinMaxCurve(3.5f * speedScale);
                }
                else
                {
                    vol.enabled = false;
                }
            }

            // 7. 周期バーストの判定
            if (hasPeriodicBurst && _currentStunWeight > 0.5f)
            {
                _periodicTimer += dt;
                if (_periodicTimer >= burstInterval)
                {
                    _periodicTimer = 0f;
                    TriggerBurst(16, speedScale, activeColor, null);
                }
            }
        }

        private void UpdateEmitterRates(ParticleSystem ps, float rate, float speed, float size, Color color)
        {
            if (rate > 0.001f)
            {
                if (!ps.isPlaying) ps.Play();
                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);

                var main = ps.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.75f, speed * 1.25f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(color);
            }
            else
            {
                var emission = ps.emission;
                emission.rateOverTime = 0f;
            }
        }

        /// <summary>
        /// 全8パターンの定数パラメーター取得（ゼロGC）
        /// </summary>
        public static void GetPatternParameters(
            PureParticlePattern pattern,
            out float ribbonRate, out float needleRate, out float auraRate,
            out float forkRate, out float crackleRate,
            out float baseSpeed, out float baseSize,
            out Color defaultColor,
            out bool hasPeriodicBurst, out float burstInterval,
            out bool hasVortex, out bool isStaccato)
        {
            ribbonRate = 0f;
            needleRate = 0f;
            auraRate = 0f;
            forkRate = 0f;
            crackleRate = 0f;
            baseSpeed = 1.0f;
            baseSize = 1.0f;
            defaultColor = new Color(0.25f, 0.85f, 2.5f, 1.0f); // 基本シアンブルー
            hasPeriodicBurst = false;
            burstInterval = 1.0f;
            hasVortex = false;
            isStaccato = false;

            switch (pattern)
            {
                case PureParticlePattern.Pattern1_ArcRibbons:
                    // ⚡ パターン1: アーク・ライトニング・リボン (Arc Lightning Ribbons)
                    // 3Dリボン軌跡が体を巡り、激しい稲妻を描く王道スタイル（リボン主役）
                    ribbonRate = 35.0f;
                    needleRate = 6.0f;
                    auraRate = 4.0f;
                    forkRate = 8.0f;
                    crackleRate = 18.0f;
                    baseSpeed = 1.6f;
                    baseSize = 1.0f;
                    defaultColor = new Color(0.25f, 0.85f, 2.5f, 1.0f); // 高圧シアン
                    break;

                case PureParticlePattern.Pattern2_SpasmicNeedles:
                    // ⚡ パターン2: スパスミック・ニードルストーム (Spasmic Needle Storm)
                    // 超高速の鋭い針状放電が全身からハリネズミ状に放射（ニードル特化）
                    ribbonRate = 2.0f;
                    needleRate = 80.0f;
                    auraRate = 4.0f;
                    forkRate = 16.0f;
                    crackleRate = 22.0f;
                    baseSpeed = 4.2f;
                    baseSize = 0.85f;
                    defaultColor = new Color(0.45f, 1.25f, 2.8f, 1.0f); // スカイブーストブルー
                    hasPeriodicBurst = true;
                    burstInterval = 0.7f;
                    break;

                case PureParticlePattern.Pattern3_StrobeAura:
                    // ⚡ パターン3: 高圧ストロボ・電離オーラ (High-Voltage Strobe Aura)
                    // 紫色に励起された高圧電離ガスが激しく脈動・ストロボ明滅（オーラ特化）
                    ribbonRate = 4.0f;
                    needleRate = 6.0f;
                    auraRate = 48.0f;
                    forkRate = 6.0f;
                    crackleRate = 24.0f;
                    baseSpeed = 0.6f;
                    baseSize = 1.3f;
                    defaultColor = new Color(1.5f, 0.35f, 2.8f, 1.0f); // ネオンバイオレット
                    hasPeriodicBurst = true;
                    burstInterval = 0.35f;
                    break;

                case PureParticlePattern.Pattern4_BranchingForks:
                    // ⚡ パターン4: 樹状分岐フォーク・カスケード (Branching Fork Arcs)
                    // 関節や装甲端からリーヒテンベルク図形のように枝分かれ瞬発放電（フォーク特化）
                    ribbonRate = 8.0f;
                    needleRate = 14.0f;
                    auraRate = 4.0f;
                    forkRate = 48.0f;
                    crackleRate = 18.0f;
                    baseSpeed = 2.4f;
                    baseSize = 1.15f;
                    defaultColor = new Color(1.1f, 1.5f, 2.8f, 1.0f); // 白熱電離青
                    hasPeriodicBurst = true;
                    burstInterval = 0.6f;
                    break;

                case PureParticlePattern.Pattern5_CrawlingPlasmaCreep:
                    // ⚡ パターン5: 表面這い回りプラズマ・クリープ (Crawling Plasma Creep)
                    // 装甲表面にまとわりつく高密度エレクトリック流体・飛散火花ゼロの純這い回り
                    ribbonRate = 0.0f;
                    needleRate = 0.0f;
                    auraRate = 14.0f;
                    forkRate = 4.0f;
                    crackleRate = 95.0f;
                    baseSpeed = 0.35f;
                    baseSize = 0.8f;
                    defaultColor = new Color(0.15f, 2.4f, 1.5f, 1.0f); // プラズマエメラルド
                    break;

                case PureParticlePattern.Pattern6_OverdriveMeltdown:
                    // ⚡ パターン6: 過負荷臨界メルトダウン (Overdrive Critical Meltdown)
                    // 金色に輝く超高圧サージ。全エミッターが全開で高密度炸裂
                    ribbonRate = 32.0f;
                    needleRate = 55.0f;
                    auraRate = 28.0f;
                    forkRate = 35.0f;
                    crackleRate = 60.0f;
                    baseSpeed = 3.0f;
                    baseSize = 1.25f;
                    defaultColor = new Color(2.8f, 1.6f, 0.25f, 1.0f); // 黄金オーバードライブ
                    hasPeriodicBurst = true;
                    burstInterval = 0.35f;
                    break;

                case PureParticlePattern.Pattern7_MagneticVortex:
                    // ⚡ パターン7: 磁場旋回バイポーラ・アーク (Magnetic Vortex Bipolar)
                    // 電磁場拘束コイルのようにキャラクター周囲を旋回するアーク流
                    ribbonRate = 36.0f;
                    needleRate = 24.0f;
                    auraRate = 10.0f;
                    forkRate = 12.0f;
                    crackleRate = 15.0f;
                    baseSpeed = 2.2f;
                    baseSize = 1.1f;
                    defaultColor = new Color(2.5f, 0.35f, 1.9f, 1.0f); // ネオマゼンタ
                    hasVortex = true;
                    break;

                case PureParticlePattern.Pattern8_IntermittentShort:
                    // ⚡ パターン8: 間欠短絡・接触不良スパーク (Intermittent Short-Circuit)
                    // バチバチッと激しく断続する接触不良・短絡ショート放電
                    ribbonRate = 20.0f;
                    needleRate = 40.0f;
                    auraRate = 6.0f;
                    forkRate = 32.0f;
                    crackleRate = 45.0f;
                    baseSpeed = 2.8f;
                    baseSize = 0.95f;
                    defaultColor = new Color(2.6f, 2.2f, 1.0f, 1.0f); // 電弧白黄
                    isStaccato = true;
                    hasPeriodicBurst = true;
                    burstInterval = 0.45f;
                    break;

                case PureParticlePattern.Off:
                default:
                    // 放電完全停止
                    break;
            }
        }

        /// <summary>
        /// パターン変更時の即時適用
        /// </summary>
        public void ApplyPatternImmediate()
        {
            _periodicTimer = 0f;
            _staccatoTimer = 0f;
            UpdateEmission(0.016f);
        }

        /// <summary>
        /// スタン開始時・被弾時・強放電時に炸裂するバースト火花
        /// </summary>
        public void TriggerBurst(int count = 25, float speedScale = 1.0f, Color? burstColor = null, Vector3? impactDirection = null)
        {
            if (_isCleaningUp || !_particlesEnabled) return;
            EnsureEmitters();
            if (_burstEmitter == null) return;

            Color col = burstColor ?? (_overrideColor ? _customColor : new Color(0.25f, 0.85f, 2.5f, 1.0f));
            var main = _burstEmitter.main;
            main.startColor = new ParticleSystem.MinMaxGradient(col);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * speedScale * _speedMultiplier, 7.5f * speedScale * _speedMultiplier);

            // 被弾方向の適用
            var vel = _burstEmitter.velocityOverLifetime;
            if (impactDirection.HasValue && impactDirection.Value.sqrMagnitude > 0.001f)
            {
                Vector3 dir = impactDirection.Value.normalized;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(dir.x * 5.0f * speedScale);
                vel.y = new ParticleSystem.MinMaxCurve(dir.y * 5.0f * speedScale);
                vel.z = new ParticleSystem.MinMaxCurve(dir.z * 5.0f * speedScale);
            }
            else
            {
                vel.enabled = false;
            }

            if (!_burstEmitter.isPlaying)
            {
                _burstEmitter.Play();
            }
            _burstEmitter.Emit(Mathf.RoundToInt(count * _rateMultiplier));
        }

        /// <summary>
        /// スタン状態の切り替えAPI（RacerManagerBase等から呼び出し可能）
        /// </summary>
        public void SetStun(bool enable, bool immediate = false, bool triggerBurst = true)
        {
            _isStunned = enable;
            if (enable && _currentPattern != PureParticlePattern.Off)
            {
                _particlesEnabled = true;
            }
            else if (!enable && immediate)
            {
                _particlesEnabled = false;
            }

            if (immediate)
            {
                _currentStunWeight = enable ? 1.0f : 0.0f;
                if (!enable)
                {
                    StopEmissionGradually();
                }
                else if (triggerBurst)
                {
                    TriggerBurst(30, 1.2f, null, null);
                }
            }
            else if (enable && triggerBurst)
            {
                TriggerBurst(20, 1.0f, null, null);
            }
        }

        /// <summary>
        /// スタン進行度（0.0〜1.0）の直接設定API
        /// </summary>
        public void SetStunNormalizedTime(float normalizedTime)
        {
            _currentStunWeight = Mathf.Clamp01(normalizedTime);
            _isStunned = _currentStunWeight > 0.001f && _currentPattern != PureParticlePattern.Off;
            if (_isStunned)
            {
                _particlesEnabled = true;
            }
            else if (_currentPattern == PureParticlePattern.Off)
            {
                _particlesEnabled = false;
            }
        }

        /// <summary>
        /// 放電強度倍率の設定API
        /// </summary>
        public void SetIntensity(float multiplier)
        {
            _rateMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// 飛散速度倍率の設定API
        /// </summary>
        public void SetSpeed(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>
        /// パターン切り替えAPI
        /// </summary>
        public void SetPattern(PureParticlePattern pattern)
        {
            CurrentPattern = pattern;
        }

        /// <summary>
        /// カスタムカラー設定API
        /// </summary>
        public void SetCustomColor(Color color)
        {
            _overrideColor = true;
            _customColor = color;
        }

        /// <summary>
        /// カスタムカラー解除API
        /// </summary>
        public void ClearCustomColor()
        {
            _overrideColor = false;
        }

        /// <summary>
        /// 放出レートを0に減衰
        /// </summary>
        public void StopEmissionGradually()
        {
            _periodicTimer = 0f;
            SetRateZero(_ribbonEmitter);
            SetRateZero(_needleEmitter);
            SetRateZero(_auraEmitter);
            SetRateZero(_forkEmitter);
            SetRateZero(_crackleEmitter);
        }

        private void SetRateZero(ParticleSystem ps)
        {
            if (ps != null)
            {
                var emission = ps.emission;
                emission.rateOverTime = 0f;
            }
        }

        /// <summary>
        /// 全パーティクルを即座に消去・停止
        /// </summary>
        public void ClearAndStop()
        {
            _periodicTimer = 0f;
            _isStunned = false;
            _currentStunWeight = 0f;
            StopAndClear(ref _ribbonEmitter);
            StopAndClear(ref _needleEmitter);
            StopAndClear(ref _auraEmitter);
            StopAndClear(ref _forkEmitter);
            StopAndClear(ref _crackleEmitter);
            StopAndClear(ref _burstEmitter);
        }

        private void StopAndClear(ref ParticleSystem ps)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// 子階層に純粋パーティクルエミッター（PS_Pure_*）が存在するかどうかを判定
        /// </summary>
        public bool HasAnyEmitters()
        {
            if (_ribbonEmitter != null || _needleEmitter != null ||
                _auraEmitter != null || _forkEmitter != null ||
                _crackleEmitter != null || _burstEmitter != null)
            {
                return true;
            }

            string[] pureChildNames = new string[]
            {
                "PS_Pure_RibbonArcs",
                "PS_Pure_NeedleSparks",
                "PS_Pure_PlasmaAura",
                "PS_Pure_ForkArcs",
                "PS_Pure_MicroCrackles",
                "PS_Pure_ShockBurst"
            };

            for (int i = 0; i < pureChildNames.Length; i++)
            {
                if (transform.Find(pureChildNames[i]) != null) return true;
            }

            return false;
        }

        /// <summary>
        /// プレイ中・エディタ中を問わず例外を出さずに安全にオブジェクトを破棄
        /// </summary>
        private static void SafeDestroyObject(UnityEngine.Object obj, bool recordUndo = true)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
#if UNITY_EDITOR
                if (recordUndo && (obj.hideFlags & HideFlags.DontSave) == 0)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
                else
                {
                    DestroyImmediate(obj);
                }
#else
                Destroy(obj);
#endif
            }
        }

        private static bool IsPureChildName(string name)
        {
            return name == "PS_Pure_RibbonArcs" ||
                   name == "PS_Pure_NeedleSparks" ||
                   name == "PS_Pure_PlasmaAura" ||
                   name == "PS_Pure_ForkArcs" ||
                   name == "PS_Pure_MicroCrackles" ||
                   name == "PS_Pure_ShockBurst";
        }

        /// <summary>
        /// 【安全クリーンアップ】生成された全6系統の子パーティクルオブジェクト（PS_Pure_*）を
        /// 安全かつ完全に破棄し、キャラクター階層を初期状態（純粋な素体状態）に復元します。
        /// スクリプトを Remove Component する前や、パーティクルを全撤去したいときに呼び出します。
        /// キャラクター本来のボーン・メッシュ・マテリアルには一切干渉せず、構成を壊しません。
        /// </summary>
        public void CleanupAllParticles(bool recordUndo = true)
        {
            if (_isCleaningUp) return;
            _isCleaningUp = true;
            try
            {
#if UNITY_EDITOR
                if (recordUndo && !Application.isPlaying && (hideFlags & HideFlags.DontSave) == 0)
                {
                    Undo.RegisterCompleteObjectUndo(this, "Cleanup All Pure Particles");
                }
#endif

                // 1. 放電・エミッターの完全停止
                ClearAndStop();
                _currentPattern = PureParticlePattern.Off;
                _isStunned = false;
                _currentStunWeight = 0.0f;
                _particlesEnabled = false;

                // 2. 登録済みエミッターのGameObjectを安全に破棄
                ParticleSystem[] emitters = new ParticleSystem[]
                {
                    _ribbonEmitter,
                    _needleEmitter,
                    _auraEmitter,
                    _forkEmitter,
                    _crackleEmitter,
                    _burstEmitter
                };

                for (int i = 0; i < emitters.Length; i++)
                {
                    ParticleSystem ps = emitters[i];
                    if (ps != null && ps.gameObject != null && ps.gameObject != gameObject)
                    {
                        SafeDestroyObject(ps.gameObject, recordUndo);
                    }
                }

                _ribbonEmitter = null;
                _needleEmitter = null;
                _auraEmitter = null;
                _forkEmitter = null;
                _crackleEmitter = null;
                _burstEmitter = null;

                // 3. 子階層を逆順に走査し、残存子オブジェクト（PS_Pure_*）を確実に安全破棄
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = transform.GetChild(i);
                    if (child != null && child.gameObject != gameObject &&
                        (child.name.StartsWith("PS_Pure_") || IsPureChildName(child.name)))
                    {
                        SafeDestroyObject(child.gameObject, recordUndo);
                    }
                }

                // 4. 万一の重複生成に対する完全フラッシュ
                string[] pureChildNames = new string[]
                {
                    "PS_Pure_RibbonArcs",
                    "PS_Pure_NeedleSparks",
                    "PS_Pure_PlasmaAura",
                    "PS_Pure_ForkArcs",
                    "PS_Pure_MicroCrackles",
                    "PS_Pure_ShockBurst"
                };

                for (int i = 0; i < pureChildNames.Length; i++)
                {
                    Transform child = transform.Find(pureChildNames[i]);
                    while (child != null && child.gameObject != gameObject)
                    {
                        SafeDestroyObject(child.gameObject, recordUndo);
                        child = transform.Find(pureChildNames[i]);
                    }
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && this != null)
                {
                    EditorUtility.SetDirty(this);
                }
#endif
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        /// <summary>
        /// CharacterElectricityPreview と統一された完全初期化・クリーンアップAPI
        /// </summary>
        public void ResetToOriginalAndCleanup(bool recordUndo = true)
        {
            CleanupAllParticles(recordUndo);
        }

        /// <summary>
        /// リボン軌跡のワールド空間化設定を即時反映
        /// </summary>
        public void ApplyRibbonWorldSpace()
        {
            if (_ribbonEmitter != null)
            {
                var trails = _ribbonEmitter.trails;
                trails.worldSpace = _ribbonWorldSpace;
            }
        }

        /// <summary>
        /// インスペクターからワンクリックで全6系統のパーティクルエミッターを自動構築
        /// </summary>
        public void SetupParticlesHierarchy()
        {
            if (_isCleaningUp) return;
            _particlesEnabled = true;
            LoadMaterials();
            EnsureTargetRenderer();

            // 1. 電撃アークリボン（Trails付き）
            _ribbonEmitter = EnsureChildParticleSystem("PS_Pure_RibbonArcs", _ribbonEmitter, _matTrail, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.maxParticles = 80;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 25.0f;

                // 激しいジグザグ稲妻ノイズ
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.1f;
                noise.frequency = 4.2f;
                noise.octaveCount = 3;
                noise.scrollSpeed = 3.5f;
                noise.damping = true;

                // Trails（3Dリボン軌跡）の有効化
                var trails = ps.trails;
                trails.enabled = true;
                trails.ratio = 1.0f;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.worldSpace = _ribbonWorldSpace;
                trails.dieWithParticles = true;
                trails.sizeAffectsWidth = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = _matNeedle != null ? _matNeedle : _matTrail; // 光る先駆スパーク核
                psr.trailMaterial = _matTrail;                                    // 3D稲妻リボン軌跡
                psr.enableGPUInstancing = true;
            });

            // 2. 高速スパーク（Stretchニードル）
            _needleEmitter = EnsureChildParticleSystem("PS_Pure_NeedleSparks", _needleEmitter, _matNeedle, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.maxParticles = 150;
                main.gravityModifier = 0.05f;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 20.0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.7f;
                noise.frequency = 3.5f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.075f;
                psr.lengthScale = 2.4f;
                psr.sharedMaterial = _matNeedle;
                psr.enableGPUInstancing = true;
            });

            // 3. 電離プラズマオーラ（柔らかな球形グロー）
            _auraEmitter = EnsureChildParticleSystem("PS_Pure_PlasmaAura", _auraEmitter, _matPlasma, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                main.maxParticles = 60;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 12.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = _matPlasma;
                psr.enableGPUInstancing = true;
            });

            // 4. 樹状分岐フォークアーク
            _forkEmitter = EnsureChildParticleSystem("PS_Pure_ForkArcs", _forkEmitter, _matFork, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.maxParticles = 80;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 15.0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.95f;
                noise.frequency = 4.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1.1f, 1f, 0f));

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = _matFork;
                psr.enableGPUInstancing = true;
            });

            // 5. 表面這い回りチリチリ放電
            _crackleEmitter = EnsureChildParticleSystem("PS_Pure_MicroCrackles", _crackleEmitter, _matCrackle, ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.maxParticles = 200;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 30.0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.4f;
                noise.frequency = 6.0f;
                noise.scrollSpeed = 5.0f;

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = _matCrackle;
                psr.enableGPUInstancing = true;
            });

            // 6. 短絡・被弾衝撃バースト（飛散ニードルスパーク）
            _burstEmitter = EnsureChildParticleSystem("PS_Pure_ShockBurst", _burstEmitter, _matNeedle, ps =>
            {
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.maxParticles = 100;
                main.gravityModifier = 0.1f;

                var emission = ps.emission;
                emission.enabled = false;
                emission.rateOverTime = 0f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.9f;
                noise.frequency = 4.5f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.08f;
                psr.lengthScale = 2.8f;
                psr.sharedMaterial = _matNeedle; // 飛散ニードルテクスチャ
                psr.enableGPUInstancing = true;
            });

            BindEmittersToRenderer();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log("[CharacterPureParticleElectricity] 純粋パーティクル放電システム（全6エミッター）のセットアップが完了しました！", this);
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
