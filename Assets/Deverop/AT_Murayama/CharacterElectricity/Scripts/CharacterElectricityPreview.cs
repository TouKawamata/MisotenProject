using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.CharacterElectricity
{
    /// <summary>
    /// 放電演出の動作モード列挙型
    /// 【シェーダーのみ / パーティクルのみ / ハイブリッド / リッチハイブリッド】の各方式を自在に切り替え可能。
    /// 最終的に採用する方式以外のアセット（シェーダーやパーティクル素材）をプロジェクトから削除しても安全に動作を継続できる設計です。
    /// </summary>
    public enum CharacterDischargeMode
    {
        [InspectorName("⚡ シェーダーのみ (Shader Only)")]
        ShaderOnly = 0,

        [InspectorName("✨ パーティクルのみ (Particle Only)")]
        ParticleOnly = 1,

        [InspectorName("🔥 ハイブリッド (Shader + Particle)")]
        Hybrid = 2,

        [InspectorName("🌟 リッチハイブリッド (Shader + Pure Particles)")]
        RichHybrid = 3
    }

    /// <summary>
    /// ハイブリッド放電演出時のパーティクル連携方式
    /// 【リッチ純粋パーティクル (P1~P8) / 軽量放電スパーク / 両方併用】
    /// </summary>
    public enum HybridParticleSource
    {
        [InspectorName("🌟 リッチ純粋パーティクル (Pure Particles P1~P8) [推奨]")]
        PureParticles = 0,

        [InspectorName("⚡ 軽量放電スパーク (Lightweight Particles)")]
        Lightweight = 1,

        [InspectorName("💥 両方併用 (Both Systems: Pure + Lightweight)")]
        Both = 2
    }

    /// <summary>
    /// シェーダー S_CharacterElectricity の _PatternMode (Float 0〜11) に1:1対応する列挙型。
    /// ShaderLab の [Enum(AT_Murayama.CharacterElectricity.CharacterElectricityMode)] でインスペクター表示されます。
    /// </summary>
    public enum CharacterElectricityMode
    {
        [InspectorName("⚡ パターン1: 高密度アーク放電 (Surface Arc)")]
        SurfaceArc = 0,

        [InspectorName("⚡ パターン2: 脈動オーバードライブ (Pulse Overcharge)")]
        PulseOvercharge = 1,

        [InspectorName("⚡ パターン3: 纏わりプラズマ膜 (Plasma Cling)")]
        PlasmaCling = 2,

        [InspectorName("⚡ パターン4: 超帯電ゴールド (Golden Overload)")]
        GoldenOverload = 3,

        [InspectorName("⚡ パターン5: クリーン・低密度ストリーク (Clean Streak)")]
        CleanStreak = 4,

        [InspectorName("⚡ パターン6: フレネル・エッジ放電 (Fresnel Rim)")]
        FresnelRim = 5,

        [InspectorName("⚡ パターン7: 間欠的ショートスパーク (Short Sparks)")]
        ShortSparks = 6,

        [InspectorName("⚡ パターン8: 電子サーキット・グリッド (Cyber Circuit)")]
        CyberCircuit = 7,

        [InspectorName("⚡ パターン9: 多点連鎖クラスタースパーク (Multi-Point Chain)")]
        MultiChainSparks = 8,

        [InspectorName("⚡ パターン10: 微小チリチリスパーク (Micro Crackle)")]
        MicroCrackle = 9,

        [InspectorName("⚡ パターン11: 高圧アーク放電バースト (Heavy Arc Burst)")]
        HeavyArcBurst = 10,

        [InspectorName("⚡ パターン12: 余熱赤熱スパーク (Thermal Afterglow)")]
        ThermalAfterglow = 11,

        [InspectorName("⚡ パターン13: 3D骨格階層・解剖学的接地アーク (Anatomical Grounding)")]
        AnatomicalGrounding = 12,

        [InspectorName("⚡ パターン14: リーヒテンベルク樹状分岐カスケード (Lichtenberg Branch)")]
        LichtenbergBranch = 13,

        [InspectorName("⚡ パターン15: 絶縁破壊ステップトリーダー・スタッカート (Stepped Leader)")]
        SteppedLeader = 14,

        [InspectorName("⚡ パターン16: 被弾点・タックル衝突ベクトル飛散放電 (Impact Vector)")]
        ImpactVector = 15,

        [InspectorName("⚡ パターン17: 衝撃波法線ディストーション＆爆圧スパーク (Shockwave Distortion)")]
        ShockwaveDistortion = 16,

        [InspectorName("⚡ パターン18: 双極マグネティック・アークウィップ (Magnetic Whip)")]
        MagneticWhip = 17,

        [InspectorName("⚡ パターン19: 装甲シーム（継ぎ目・エッジ）沿い短絡奔流 (Armor Seam Short)")]
        ArmorSeamShort = 18,

        [InspectorName("⚡ パターン20: 臨界メルトダウン・ランダムストロボ連鎖 (Critical Strobe Cascade)")]
        CriticalStrobe = 19
    }

    /// <summary>
    /// born model などのキャラクターメッシュに対して、
    /// キャラクターの色バリエーション（shiro / ao / kuro 等）やデザイナー制作マテリアルを保護しながら、
    /// 全20種類の帯電・放電パターンを安全に切り替えてプレビュー・制御できるコントローラー。
    /// 
    /// 【特徴】
    /// 1. デザイナー制作マテリアル保護: 現在アタッチされているマテリアルを動的バックアップし、100%安全に元通り復元可能。
    /// 2. 色バリエーション追従: ao (青), kuro (黒), shiro (白) などの地色・テクスチャを自動判別し、放電中もキャラクター本来のカラーを維持。
    /// 3. 完全ゼロGC (0 Alloc): Update() 内の sharedMaterials 呼び出しを完全撤廃し、キャッシュと MaterialPropertyBlock による完全ゼロGC動作を実現。
    /// 4. プレイヤーシステム連携API: RacerManagerBase等のプレイヤー制御スクリプトから1行で呼び出せる安全な公開メソッド群を提供。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterElectricityPreview : MonoBehaviour
    {
        public enum ElectricityPattern
        {
            [InspectorName("⚪ 元のマテリアル (Original / Restore)")]
            Original = 0,

            [InspectorName("⚡ パターン1: 高密度アーク放電 (Surface Arc)")]
            Pattern1_Arc = 1,

            [InspectorName("⚡ パターン2: 脈動オーバードライブ (Pulse Overcharge)")]
            Pattern2_Pulse = 2,

            [InspectorName("⚡ パターン3: 纏わりプラズマ膜 (Plasma Cling)")]
            Pattern3_Plasma = 3,

            [InspectorName("⚡ パターン4: 超帯電ゴールド (Golden Overload)")]
            Pattern4_Gold = 4,

            [InspectorName("⚡ パターン5: クリーン・低密度ストリーク (Clean Streak)")]
            Pattern5_CleanStreak = 5,

            [InspectorName("⚡ パターン6: フレネル・エッジ放電 (Fresnel Rim)")]
            Pattern6_FresnelRim = 6,

            [InspectorName("⚡ パターン7: 間欠的ショートスパーク (Short Sparks)")]
            Pattern7_ShortSparks = 7,

            [InspectorName("⚡ パターン8: 電子サーキット・グリッド (Cyber Circuit)")]
            Pattern8_CyberCircuit = 8,

            [InspectorName("⚡ パターン9: 多点連鎖クラスタースパーク (Multi-Point Chain)")]
            Pattern9_MultiChain = 9,

            [InspectorName("⚡ パターン10: 微小・高頻度チリチリスパーク (Micro Spark Jitter)")]
            Pattern10_MicroCrackle = 10,

            [InspectorName("⚡ パターン11: 高圧アーク放電バースト (Heavy Arc Burst)")]
            Pattern11_HeavyArcBurst = 11,

            [InspectorName("⚡ パターン12: 余熱赤熱スパーク (Thermal Afterglow)")]
            Pattern12_ThermalAfterglow = 12,

            [InspectorName("⚡ パターン13: 3D骨格階層・解剖学的接地アーク (Anatomical Grounding)")]
            Pattern13_AnatomicalGrounding = 13,

            [InspectorName("⚡ パターン14: リーヒテンベルク樹状分岐カスケード (Lichtenberg Branch)")]
            Pattern14_LichtenbergBranch = 14,

            [InspectorName("⚡ パターン15: 絶縁破壊ステップトリーダー・スタッカート (Stepped Leader)")]
            Pattern15_SteppedLeader = 15,

            [InspectorName("⚡ パターン16: 被弾点・タックル衝突ベクトル飛散放電 (Impact Vector)")]
            Pattern16_ImpactVector = 16,

            [InspectorName("⚡ パターン17: 衝撃波法線ディストーション＆爆圧スパーク (Shockwave Distortion)")]
            Pattern17_ShockwaveDistortion = 17,

            [InspectorName("⚡ パターン18: 双極マグネティック・アークウィップ (Magnetic Whip)")]
            Pattern18_MagneticWhip = 18,

            [InspectorName("⚡ パターン19: 装甲シーム（継ぎ目・エッジ）沿い短絡奔流 (Armor Seam Short)")]
            Pattern19_ArmorSeamShort = 19,

            [InspectorName("⚡ パターン20: 臨界メルトダウン・ランダムストロボ連鎖 (Critical Strobe Cascade)")]
            Pattern20_CriticalStrobe = 20
        }

        [Header("0. 放電演出 動作モード (Discharge Mode)")]
        [Tooltip("放電演出の動作モード【シェーダーのみ / パーティクルのみ / ハイブリッド / リッチハイブリッド】\n将来どの方式を採用しても、不要なアセットを安全に削除できる柔軟な構成です。")]
        [SerializeField] private CharacterDischargeMode _dischargeMode = CharacterDischargeMode.ShaderOnly;

        [Tooltip("ハイブリッド放電時のパーティクル連動方式【リッチ純粋パーティクル (P1~P8) / 軽量放電スパーク / 両方併用】")]
        [SerializeField] private HybridParticleSource _hybridParticleSource = HybridParticleSource.Lightweight;

        [Header("0B. 純粋パーティクル放電設定 (Particle Only Mode)")]
        [Tooltip("純粋パーティクルによる放電パターン（全8パターン）")]
        [SerializeField] private PureParticlePattern _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;

        [Tooltip("純粋パーティクル放電コントローラー（未設定時は自動取得または自動生成）")]
        [SerializeField] private CharacterPureParticleElectricity _pureParticles;

        [Range(0f, 5f)]
        [Tooltip("純粋パーティクル放出量倍率")]
        [SerializeField] private float _pureRateMultiplier = 1.0f;

        [Range(0.2f, 3f)]
        [Tooltip("純粋パーティクル速度倍率")]
        [SerializeField] private float _pureSpeedMultiplier = 1.0f;

        [Range(0.2f, 3f)]
        [Tooltip("純粋パーティクルサイズ倍率")]
        [SerializeField] private float _pureSizeMultiplier = 1.0f;

        [Tooltip("純粋パーティクルの3Dリボン稲妻をワールド空間に引きずるか")]
        [SerializeField] private bool _pureRibbonWorldSpace = false;

        [Header("1. 対象レンダラー & スロット設定")]
        [Tooltip("放電を適用するレンダラー（未設定時は自身または子から自動取得）")]
        [SerializeField] private Renderer _targetRenderer;

        [Tooltip("放電を適用するマテリアルのスロット番号（-1 で 'shiro'/'ao'/'kuro' などのキャラ衣装スロットを自動検索）")]
        [SerializeField] private int _targetMaterialSlot = -1;

        [Header("2. 現在の放電パターン (Shader / Hybrid Mode)")]
        [SerializeField] private ElectricityPattern _currentPattern = ElectricityPattern.Original;

        [Header("3. リアルタイム調整パラメーター")]
        [Range(0f, 5f)]
        [SerializeField] private float _intensityMultiplier = 1.0f;

        [Range(0.2f, 4f)]
        [SerializeField] private float _speedMultiplier = 1.0f;

        [Header("4. 電流カラーカスタム（汎用カラー調整）")]
        [Tooltip("チェックを入れると、各パターンの規定カラーを下記のカスタムカラーで上書きします")]
        [SerializeField] private bool _overrideColor = false;

        [ColorUsage(true, true)]
        [Tooltip("放電のHDRカラー（発光強度を含めて自由に調整可能）")]
        [SerializeField] private Color _customElectricityColor = new Color(0.25f, 0.85f, 2.5f, 1.0f);

        [Header("5. スタン状態シミュレーション")]
        [Tooltip("スタン発生フラグ（ONで放電開始、OFFで即座またはフェードで放電停止）")]
        [SerializeField] private bool _isStunned = true;

        [Tooltip("スタン発生/解除時の放電フェード速度（0で即時切替）")]
        [Range(0f, 20f)]
        [SerializeField] private float _stunTransitionSpeed = 10f;

        [Tooltip("スタン発生時に自動適用される放電パターン")]
        [SerializeField] private ElectricityPattern _stunElectricityPattern = ElectricityPattern.Pattern1_Arc;

        [Tooltip("スタン終了時（放電ウェイトが0到達時）に自動で元のデザイナー制作マテリアルへ復帰するかどうか")]
        [SerializeField] private bool _autoRestoreOnStunEnd = true;

        [Header("5B. 被弾点・タックル衝突ベクトル設定 (Pattern 16)")]
        [Tooltip("被弾起点のUV座標 (0.0〜1.0)")]
        [SerializeField] private Vector2 _impactCenterUV = new Vector2(0.5f, 0.55f);

        [Tooltip("衝撃・吹っ飛び方向のUVベクトル")]
        [SerializeField] private Vector2 _impactDirectionUV = new Vector2(0.28f, -0.18f);

        [Header("6. プリセットマテリアル参照 (全20種)")]
        [SerializeField] private Material _matPattern1Arc;
        [SerializeField] private Material _matPattern2Pulse;
        [SerializeField] private Material _matPattern3Plasma;
        [SerializeField] private Material _matPattern4Gold;
        [SerializeField] private Material _matPattern5CleanStreak;
        [SerializeField] private Material _matPattern6FresnelRim;
        [SerializeField] private Material _matPattern7ShortSparks;
        [SerializeField] private Material _matPattern8CyberCircuit;
        [SerializeField] private Material _matPattern9MultiChain;
        [SerializeField] private Material _matPattern10MicroCrackle;
        [SerializeField] private Material _matPattern11HeavyArcBurst;
        [SerializeField] private Material _matPattern12ThermalAfterglow;
        [SerializeField] private Material _matPattern13Anatomical;
        [SerializeField] private Material _matPattern14Lichtenberg;
        [SerializeField] private Material _matPattern15SteppedLeader;
        [SerializeField] private Material _matPattern16ImpactVector;
        [SerializeField] private Material _matPattern17Shockwave;
        [SerializeField] private Material _matPattern18MagneticWhip;
        [SerializeField] private Material _matPattern19ArmorSeam;
        [SerializeField] private Material _matPattern20CriticalStrobe;

        [Header("7. 復元用バックアップ（自動保存・デザイナー保護）")]
        [Tooltip("対象スロットにアタッチされていた元のマテリアル（色バリエーションやデザイナー制作物）の退避先")]
        [SerializeField] private Material _originalMaterialBackup;

        [Header("8. パーティクル演出連動 (Discharge Particles)")]
        [Tooltip("放電パーティクル演出の有効 / 無効")]
        [SerializeField] private bool _enableParticles = true;

        [Tooltip("キャラクター放電パーティクルコントローラー（未設定時は自動取得または自動生成）")]
        [SerializeField] private CharacterElectricityParticles _particles;

        [Range(0f, 3f)]
        [Tooltip("パーティクル発生量の全体倍率")]
        [SerializeField] private float _particleRateMultiplier = 1.0f;

        // ゼロGC・プロパティIDキャッシュ
        private static readonly int _propBaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int _propBaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int _propBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int _propBaseColorMapAlt = Shader.PropertyToID("_BASE_COLOR_MAP");
        private static readonly int _propBaseColorAlt = Shader.PropertyToID("_BASE_COLOR");
        private static readonly int _propColorAlt = Shader.PropertyToID("_Color");
        private static readonly int _propElectricityIntensity = Shader.PropertyToID("_ElectricityIntensity");
        private static readonly int _propElectricitySpeed = Shader.PropertyToID("_ElectricitySpeed");
        private static readonly int _propElectricityColor = Shader.PropertyToID("_ElectricityColor");
        private static readonly int _propMeshBoundsMin = Shader.PropertyToID("_MeshBoundsMin");
        private static readonly int _propMeshBoundsInvSize = Shader.PropertyToID("_MeshBoundsInvSize");
        private static readonly int _propImpactConfig = Shader.PropertyToID("_ImpactConfig");

        // ランタイムキャッシュ（Update内のGCアロケーションゼロ化）
        private MaterialPropertyBlock _mpb;
        private float _currentStunWeight = 1.0f;
        private Material _cachedAppliedMaterial;
        private float _cachedBaseIntensity = 1.8f;
        private float _cachedBaseSpeed = 2.5f;
        private Color _cachedBaseMatElectricityColor = new Color(0.25f, 0.85f, 2.5f, 1.0f);
        private Texture _cachedOriginalBaseMap;
        private Vector4 _cachedOriginalBaseMapST = new Vector4(1f, 1f, 0f, 0f);
        private Color _cachedOriginalBaseColor = Color.white;
        private Vector4 _cachedBoundsMin = new Vector4(-0.8f, 0.0f, -0.5f, 0.0f);
        private Vector4 _cachedBoundsInvSize = new Vector4(0.625f, 0.5f, 1.0f, 0.0f);
        private bool _hasOriginalPropsCached = false;

        // 公開プロパティ
        public CharacterDischargeMode DischargeMode
        {
            get => _dischargeMode;
            set => SetDischargeMode(value);
        }

        public HybridParticleSource HybridSource
        {
            get => _hybridParticleSource;
            set => SetHybridParticleSource(value);
        }

        public bool HasShaderEffect => _dischargeMode == CharacterDischargeMode.ShaderOnly ||
                                       _dischargeMode == CharacterDischargeMode.Hybrid ||
                                       _dischargeMode == CharacterDischargeMode.RichHybrid;

        public bool UsesPureParticles => _dischargeMode == CharacterDischargeMode.ParticleOnly ||
                                         _dischargeMode == CharacterDischargeMode.RichHybrid ||
                                         (_dischargeMode == CharacterDischargeMode.Hybrid && (_hybridParticleSource == HybridParticleSource.PureParticles || _hybridParticleSource == HybridParticleSource.Both));

        public bool UsesLightweightParticles => (_dischargeMode == CharacterDischargeMode.Hybrid && (_hybridParticleSource == HybridParticleSource.Lightweight || _hybridParticleSource == HybridParticleSource.Both));

        public ElectricityPattern CurrentPattern => _currentPattern;

        public PureParticlePattern PurePattern
        {
            get => _pureParticlePattern;
            set => SwitchPureParticlePattern(value);
        }

        public Renderer TargetRenderer => _targetRenderer;
        public int TargetMaterialSlot => _targetMaterialSlot;
        public bool IsStunned => _isStunned;
        public float CurrentStunWeight => _currentStunWeight;
        public float StunNormalizedTime => _currentStunWeight;
        public float IntensityMultiplier => _intensityMultiplier;
        public Material OriginalMaterialBackup => _originalMaterialBackup;
        public Color CustomColor { get => _customElectricityColor; set => SetElectricityColor(value, true); }
        public bool OverrideColor { get => _overrideColor; set => _overrideColor = value; }
        public bool AutoRestoreOnStunEnd { get => _autoRestoreOnStunEnd; set => _autoRestoreOnStunEnd = value; }
        public ElectricityPattern StunElectricityPattern { get => _stunElectricityPattern; set => _stunElectricityPattern = value; }
        public bool EnableParticles { get => _enableParticles; set => _enableParticles = value; }
        public CharacterElectricityParticles Particles => _particles;
        public CharacterPureParticleElectricity PureParticles => _pureParticles;
        public float ParticleRateMultiplier { get => _particleRateMultiplier; set => _particleRateMultiplier = value; }
        public float PureRateMultiplier { get => _pureRateMultiplier; set { _pureRateMultiplier = value; if (_pureParticles != null) _pureParticles.RateMultiplier = value; } }
        public float PureSpeedMultiplier { get => _pureSpeedMultiplier; set { _pureSpeedMultiplier = value; if (_pureParticles != null) _pureParticles.SpeedMultiplier = value; } }
        public float PureSizeMultiplier { get => _pureSizeMultiplier; set { _pureSizeMultiplier = value; if (_pureParticles != null) _pureParticles.SizeMultiplier = value; } }
        public bool PureRibbonWorldSpace { get => _pureRibbonWorldSpace; set { _pureRibbonWorldSpace = value; if (_pureParticles != null) _pureParticles.RibbonWorldSpace = value; } }

        private void OnEnable()
        {
            EnsureRendererAndSlot();
            LoadMaterialPresets();
            CacheOriginalMaterialProps(_originalMaterialBackup);
            ApplyCurrentPattern();

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
                if (_pureParticles == null)
                {
                    _pureParticles = GetComponentInChildren<CharacterPureParticleElectricity>(true);
                    if (_pureParticles == null)
                    {
                        EnsurePureParticles(recordUndo: false);
                    }
                }
                SyncPureParticlesImmediate();
            }
            else if (_dischargeMode == CharacterDischargeMode.RichHybrid || _dischargeMode == CharacterDischargeMode.Hybrid)
            {
                if (UsesPureParticles)
                {
                    if (_pureParticles == null)
                    {
                        _pureParticles = GetComponentInChildren<CharacterPureParticleElectricity>(true);
                        if (_pureParticles == null)
                        {
                            EnsurePureParticles(recordUndo: false);
                        }
                    }
                    SyncPureParticlesImmediate();
                }
                else if (_pureParticles != null)
                {
                    _pureParticles.ParticlesEnabled = false;
                    _pureParticles.StopEmissionGradually();
                }

                if (UsesLightweightParticles && _enableParticles)
                {
                    if (_particles == null)
                    {
                        _particles = GetComponentInChildren<CharacterElectricityParticles>(true);
                        if (_particles == null)
                        {
                            SetupParticles();
                        }
                    }
                    SyncParticlesImmediate();
                }
                else if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
            }
            else
            {
                if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
                if (_pureParticles != null)
                {
                    _pureParticles.ParticlesEnabled = false;
                    _pureParticles.StopEmissionGradually();
                }
            }
        }

        private void Start()
        {
            EnsureRendererAndSlot();
            LoadMaterialPresets();
            CacheOriginalMaterialProps(_originalMaterialBackup);

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
                if (_pureParticles == null)
                {
                    _pureParticles = GetComponentInChildren<CharacterPureParticleElectricity>(true);
                    if (_pureParticles == null)
                    {
                        EnsurePureParticles(recordUndo: false);
                    }
                }
                SyncPureParticlesImmediate();
            }
            else if (_dischargeMode == CharacterDischargeMode.RichHybrid || _dischargeMode == CharacterDischargeMode.Hybrid)
            {
                if (UsesPureParticles)
                {
                    if (_pureParticles == null)
                    {
                        _pureParticles = GetComponentInChildren<CharacterPureParticleElectricity>(true);
                        if (_pureParticles == null)
                        {
                            EnsurePureParticles(recordUndo: false);
                        }
                    }
                    SyncPureParticlesImmediate();
                }
                else if (_pureParticles != null)
                {
                    _pureParticles.ParticlesEnabled = false;
                    _pureParticles.StopEmissionGradually();
                }

                if (UsesLightweightParticles && _enableParticles)
                {
                    if (_particles == null)
                    {
                        _particles = GetComponentInChildren<CharacterElectricityParticles>(true);
                        if (_particles == null)
                        {
                            SetupParticles();
                        }
                    }
                    SyncParticlesImmediate();
                }
                else if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
            }
            else
            {
                if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
                if (_pureParticles != null)
                {
                    _pureParticles.ParticlesEnabled = false;
                    _pureParticles.StopEmissionGradually();
                }
            }
        }

        private void OnValidate()
        {
            EnsureRendererAndSlot();
            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                ApplyCurrentPattern();
                SyncPureParticlesImmediate();
            }
            else
            {
                if (_currentPattern != ElectricityPattern.Original)
                {
                    _isStunned = true;
                    _currentStunWeight = 1.0f;
                }
                ApplyCurrentPattern();

                if (UsesPureParticles)
                {
                    SyncPureParticlesImmediate();
                }
                else if (_pureParticles != null)
                {
                    _pureParticles.ParticlesEnabled = false;
                    _pureParticles.StopEmissionGradually();
                }

                if (UsesLightweightParticles)
                {
                    if (!_enableParticles && _particles != null)
                    {
                        _particles.ParticlesEnabled = false;
                        _particles.StopEmissionGradually();
                    }
                    SyncParticlesImmediate();
                }
                else if (_particles != null)
                {
                    _particles.ParticlesEnabled = false;
                    _particles.StopEmissionGradually();
                }
            }
        }

        private void OnDestroy()
        {
            // エディタ編集時（非プレイ中）またはコンポーネント単体削除（Remove Component）時の安全ガード
            try
            {
                // 1. マテリアルの安全自動復帰（放電マテリアル固定化防止）
                if (_targetRenderer == null)
                {
                    EnsureRendererAndSlot();
                }

                if (_targetRenderer != null)
                {
                    Material[] mats = _targetRenderer.sharedMaterials;
                    if (mats != null && _targetMaterialSlot >= 0 && _targetMaterialSlot < mats.Length)
                    {
                        Material cur = mats[_targetMaterialSlot];
                        if (_currentPattern != ElectricityPattern.Original || (cur != null && IsElectricityMaterial(cur)))
                        {
                            Material restoreMat = _originalMaterialBackup != null ? _originalMaterialBackup : FindFallbackMaterial();
                            if (restoreMat != null)
                            {
                                mats[_targetMaterialSlot] = restoreMat;
                                _targetRenderer.sharedMaterials = mats;
                            }
                        }
                        _targetRenderer.SetPropertyBlock(null, _targetMaterialSlot);
                    }
                }

                // 2. 生成された子パーティクルオブジェクト（ハイブリッド・純粋パーティクルの両方）の自動クリーンアップ
                // スクリプト解除時にキャラクターヒエラルキーに余計な子オブジェクトを残さない
                DeleteHybridParticles(recordUndo: false);
                DeletePureParticles(recordUndo: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterElectricityPreview] OnDestroy クリーンアップ警告: {ex.Message}", this);
            }
        }

        private void Reset()
        {
            // 1. レンダラーとスロットを解決
            EnsureRendererAndSlot();
            LoadMaterialPresets();

            // 2. レンダラーに放電マテリアルが残っていれば安全に元デザイナーマテリアルへ復帰
            if (_targetRenderer != null && _targetMaterialSlot >= 0)
            {
                Material[] mats = _targetRenderer.sharedMaterials;
                if (mats != null && _targetMaterialSlot < mats.Length)
                {
                    Material cur = mats[_targetMaterialSlot];
                    if (cur != null && IsElectricityMaterial(cur))
                    {
                        Material fallback = _originalMaterialBackup != null ? _originalMaterialBackup : FindFallbackMaterial();
                        if (fallback != null)
                        {
                            mats[_targetMaterialSlot] = fallback;
                            _targetRenderer.sharedMaterials = mats;
                            _originalMaterialBackup = fallback;
                        }
                    }
                }
                _targetRenderer.SetPropertyBlock(null, _targetMaterialSlot);
            }

            // 3. 子パーティクルオブジェクト（ハイブリッドおよび純粋パーティクルの両方）を完全破棄
            DeleteHybridParticles(recordUndo: false);
            DeletePureParticles(recordUndo: false);

            // 4. ステート初期化
            _dischargeMode = CharacterDischargeMode.ShaderOnly;
            _hybridParticleSource = HybridParticleSource.Lightweight;
            _currentPattern = ElectricityPattern.Original;
            _pureParticlePattern = PureParticlePattern.Off;
            _isStunned = false;
            _currentStunWeight = 0.0f;
            _enableParticles = false;
        }

        private void Update()
        {
            // スタン遷移ウェイトの更新（プレイ中は滑らかなフェード、エディタ編集時は即応）
            float targetWeight = _isStunned ? 1.0f : 0.0f;
            if (Application.isPlaying && _stunTransitionSpeed > 0.001f)
            {
                _currentStunWeight = Mathf.MoveTowards(_currentStunWeight, targetWeight, Time.deltaTime * _stunTransitionSpeed);
            }
            else
            {
                _currentStunWeight = targetWeight;
            }

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                // 【パーティクル単体モード】
                // メッシュマテリアルやMPBは一切触らず、純粋パーティクル側のみを制御（完全 0 GC）
                if (_pureParticles != null)
                {
                    bool isPureActive = _pureParticlePattern != PureParticlePattern.Off;
                    bool shouldEmit = (_isStunned || _currentStunWeight > 0.001f) && isPureActive;
                    _pureParticles.ParticlesEnabled = shouldEmit;
                    _pureParticles.RateMultiplier = _pureRateMultiplier;
                    _pureParticles.SpeedMultiplier = _pureSpeedMultiplier;
                    _pureParticles.SizeMultiplier = _pureSizeMultiplier;
                    _pureParticles.RibbonWorldSpace = _pureRibbonWorldSpace;
                    _pureParticles.OverrideColor = _overrideColor;
                    if (_overrideColor)
                    {
                        _pureParticles.CustomColor = _customElectricityColor;
                    }
                    _pureParticles.SetStunNormalizedTime(isPureActive ? _currentStunWeight : 0.0f);
                }
                return;
            }

            // スタン終了時（ウェイトが 0 に到達）かつ自動復帰有効時、元のデザイナー制作マテリアルへ安全に完全復帰
            if (!_isStunned && _currentStunWeight <= 0.001f)
            {
                if (_autoRestoreOnStunEnd && _currentPattern != ElectricityPattern.Original)
                {
                    RestoreOriginalMaterial();
                    return;
                }
            }

            // 【完全ゼロGC】Update() 内では sharedMaterials を一切呼ばず、放電中のみキャッシュ値で MPB を更新
            if (_currentPattern != ElectricityPattern.Original && _targetRenderer != null && _targetMaterialSlot >= 0)
            {
                UpdatePropertyBlockFast();
            }

            // 純粋パーティクル演出の同期（RichHybrid または Hybrid(Pure/Both) 時、完全 0 GC）
            if (UsesPureParticles && _pureParticles != null)
            {
                bool isPureActive = _pureParticlePattern != PureParticlePattern.Off;
                bool shouldEmit = (_isStunned || _currentStunWeight > 0.001f) && isPureActive;
                _pureParticles.ParticlesEnabled = shouldEmit;
                _pureParticles.RateMultiplier = _pureRateMultiplier;
                _pureParticles.SpeedMultiplier = _pureSpeedMultiplier;
                _pureParticles.SizeMultiplier = _pureSizeMultiplier;
                _pureParticles.RibbonWorldSpace = _pureRibbonWorldSpace;
                _pureParticles.OverrideColor = _overrideColor;
                if (_overrideColor)
                {
                    _pureParticles.CustomColor = _customElectricityColor;
                }
                _pureParticles.SetStunNormalizedTime(isPureActive ? _currentStunWeight : 0.0f);
            }

            // 軽量パーティクル演出の同期（0 GC）
            if (UsesLightweightParticles && _particles != null)
            {
                if (_currentPattern != ElectricityPattern.Original && _isStunned && _currentStunWeight > 0.001f)
                {
                    Color effectiveColor = _overrideColor ? _customElectricityColor : _cachedBaseMatElectricityColor;
                    _particles.ParticlesEnabled = _enableParticles;
                    _particles.RateMultiplier = _particleRateMultiplier;
                    _particles.SynchronizeWithElectricity(
                        _intensityMultiplier,
                        _speedMultiplier,
                        effectiveColor,
                        _currentStunWeight,
                        _currentPattern,
                        _impactDirectionUV);
                }
                else
                {
                    _particles.StopEmissionGradually();
                }
            }
        }

        /// <summary>
        /// レンダラーと対象マテリアルスロットの自動検出、およびデザイナー元マテリアルの動的バックアップ
        /// </summary>
        public void EnsureRendererAndSlot()
        {
            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (_targetRenderer == null)
                {
                    _targetRenderer = GetComponentInChildren<MeshRenderer>();
                }
            }

            if (_enableParticles)
            {
                if (_particles == null)
                {
                    _particles = GetComponentInChildren<CharacterElectricityParticles>(true);
                    if (_particles == null)
                    {
                        _particles = GetComponent<CharacterElectricityParticles>();
                    }
                }

                if (_particles != null && _targetRenderer != null && _particles.TargetRenderer == null)
                {
                    _particles.TargetRenderer = _targetRenderer;
                }
            }
            else
            {
                _particles = null;
            }

            if (_targetRenderer == null) return;

            // 3Dローカルバウンディングボックスをキャッシュ（Pattern 13 の3D解剖学的接地用）
            Bounds localB = _targetRenderer.localBounds;
            Vector3 bSize = localB.size;
            if (bSize.sqrMagnitude > 0.0001f)
            {
                _cachedBoundsMin = new Vector4(localB.min.x, localB.min.y, localB.min.z, 0f);
                _cachedBoundsInvSize = new Vector4(
                    bSize.x > 0.0001f ? 1f / bSize.x : 1f,
                    bSize.y > 0.0001f ? 1f / bSize.y : 1f,
                    bSize.z > 0.0001f ? 1f / bSize.z : 1f,
                    0f);
            }

            Material[] sharedMats = _targetRenderer.sharedMaterials;
            if (sharedMats == null || sharedMats.Length == 0) return;

            // スロット未設定(-1)または範囲外の場合、衣装・素体マテリアル（shiro/ao/kuro等）を自動検索
            if (_targetMaterialSlot < 0 || _targetMaterialSlot >= sharedMats.Length)
            {
                _targetMaterialSlot = FindCharacterBodySlot(sharedMats);
            }

            // 元のマテリアルの動的バックアップ（デザイナー制作マテリアルや色バリエーションを保持）
            if (_targetMaterialSlot >= 0 && _targetMaterialSlot < sharedMats.Length)
            {
                Material currentMat = sharedMats[_targetMaterialSlot];
                if (currentMat != null && !IsElectricityMaterial(currentMat))
                {
                    // 現在アタッチされているのが放電マテリアルでない場合、それを本来のデザイナーマテリアルとして退避
                    _originalMaterialBackup = currentMat;
                    CacheOriginalMaterialProps(_originalMaterialBackup);
                }
            }
        }

        /// <summary>
        /// キャラクターの衣装・色バリエーションスロットを柔軟に検索（shiro固定の依存を排除）
        /// </summary>
        private int FindCharacterBodySlot(Material[] sharedMats)
        {
            if (sharedMats == null || sharedMats.Length == 0) return 0;

            // 1. 一般的なキャラ素体・色バリエーション名キーワードを優先検索
            string[] priorityKeywords = new[] { "shiro", "ao", "kuro", "body", "character", "chara", "main", "costume", "skin" };
            for (int k = 0; k < priorityKeywords.Length; k++)
            {
                string kw = priorityKeywords[k];
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] != null && sharedMats[i].name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }

            // 2. キーワードに一致しない場合、放電マテリアル以外の最初の非nullスロットを選択
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] != null && !IsElectricityMaterial(sharedMats[i]))
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// 指定マテリアルが放電エフェクト用マテリアルかどうかを判定
        /// </summary>
        public bool IsElectricityMaterial(Material mat)
        {
            if (mat == null) return false;
            if (mat == _matPattern1Arc || mat == _matPattern2Pulse ||
                mat == _matPattern3Plasma || mat == _matPattern4Gold ||
                mat == _matPattern5CleanStreak || mat == _matPattern6FresnelRim ||
                mat == _matPattern7ShortSparks || mat == _matPattern8CyberCircuit ||
                mat == _matPattern9MultiChain || mat == _matPattern10MicroCrackle ||
                mat == _matPattern11HeavyArcBurst || mat == _matPattern12ThermalAfterglow ||
                mat == _matPattern13Anatomical || mat == _matPattern14Lichtenberg ||
                mat == _matPattern15SteppedLeader || mat == _matPattern16ImpactVector ||
                mat == _matPattern17Shockwave || mat == _matPattern18MagneticWhip ||
                mat == _matPattern19ArmorSeam || mat == _matPattern20CriticalStrobe)
            {
                return true;
            }
            if (mat.shader != null && mat.shader.name == "Custom/CharacterElectricity/S_CharacterElectricity")
            {
                return true;
            }
            string name = mat.name.ToLowerInvariant();
            return name.StartsWith("m_shiro_pattern") || name.Contains("electricity");
        }

        /// <summary>
        /// 元マテリアルの地色・ベーステクスチャ・タイリングオフセットをキャッシュ（色バリエーション保持用）
        /// </summary>
        private void CacheOriginalMaterialProps(Material originalMat)
        {
            if (originalMat == null) return;

            // テクスチャおよび UV ST の取得
            string texturePropName = null;
            if (originalMat.HasProperty(_propBaseMap) && originalMat.GetTexture(_propBaseMap) != null)
            {
                _cachedOriginalBaseMap = originalMat.GetTexture(_propBaseMap);
                texturePropName = "_BaseMap";
            }
            else if (originalMat.HasProperty(_propBaseColorMapAlt) && originalMat.GetTexture(_propBaseColorMapAlt) != null)
            {
                _cachedOriginalBaseMap = originalMat.GetTexture(_propBaseColorMapAlt);
                texturePropName = "_BASE_COLOR_MAP";
            }
            else if (originalMat.mainTexture != null)
            {
                _cachedOriginalBaseMap = originalMat.mainTexture;
                _cachedOriginalBaseMapST = new Vector4(originalMat.mainTextureScale.x, originalMat.mainTextureScale.y, originalMat.mainTextureOffset.x, originalMat.mainTextureOffset.y);
            }

            if (texturePropName != null)
            {
                Vector2 scale = originalMat.GetTextureScale(texturePropName);
                Vector2 offset = originalMat.GetTextureOffset(texturePropName);
                _cachedOriginalBaseMapST = new Vector4(scale.x, scale.y, offset.x, offset.y);
            }

            // ベースカラーの取得
            if (originalMat.HasProperty(_propBaseColor))
            {
                _cachedOriginalBaseColor = originalMat.GetColor(_propBaseColor);
            }
            else if (originalMat.HasProperty(_propBaseColorAlt))
            {
                _cachedOriginalBaseColor = originalMat.GetColor(_propBaseColorAlt);
            }
            else if (originalMat.HasProperty(_propColorAlt))
            {
                _cachedOriginalBaseColor = originalMat.GetColor(_propColorAlt);
            }

            _hasOriginalPropsCached = true;
        }

        /// <summary>
        /// 色バリエーション（ao, kuro, shiro 等）に応じた安全なフォールバックマテリアルを検出
        /// </summary>
        private Material FindFallbackMaterial()
        {
#if UNITY_EDITOR
            // 1. オブジェクト名や親の名前から色バリエーションキーワードを検索 (ao, kuro, shiro 等)
            string targetName = gameObject.name.ToLowerInvariant();
            if (transform.parent != null) targetName += " " + transform.parent.name.ToLowerInvariant();
            if (_targetRenderer != null) targetName += " " + _targetRenderer.name.ToLowerInvariant();

            if (targetName.Contains("ao") || targetName.Contains("blue"))
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/ao.mat");
                if (m != null) return m;
            }
            if (targetName.Contains("kuro") || targetName.Contains("black"))
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/kuro.mat");
                if (m != null) return m;
            }

            // 2. 正規パスの shiro.mat
            Material shiro = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
            if (shiro != null) return shiro;
#endif
            return null;
        }

        /// <summary>
        /// プリセットマテリアルおよび復元用デフォルトパスのロード
        /// </summary>
        public void LoadMaterialPresets()
        {
#if UNITY_EDITOR
            // パーティクル単体モード時はシェーダー用マテリアルのロードをスキップ（将来シェーダー素材を削除しても安全に動作）
            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                if (_originalMaterialBackup == null)
                {
                    _originalMaterialBackup = FindFallbackMaterial();
                    if (_originalMaterialBackup != null)
                    {
                        CacheOriginalMaterialProps(_originalMaterialBackup);
                    }
                }
                return;
            }

            string basePath = "Assets/Deverop/AT_Murayama/CharacterElectricity/Materials";
            if (_matPattern1Arc == null)
                _matPattern1Arc = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern1_Arc.mat");
            if (_matPattern2Pulse == null)
                _matPattern2Pulse = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern2_Pulse.mat");
            if (_matPattern3Plasma == null)
                _matPattern3Plasma = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern3_Plasma.mat");
            if (_matPattern4Gold == null)
                _matPattern4Gold = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern4_Gold.mat");
            if (_matPattern5CleanStreak == null)
                _matPattern5CleanStreak = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern5_CleanStreak.mat");
            if (_matPattern6FresnelRim == null)
                _matPattern6FresnelRim = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern6_FresnelRim.mat");
            if (_matPattern7ShortSparks == null)
                _matPattern7ShortSparks = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern7_ShortSparks.mat");
            if (_matPattern8CyberCircuit == null)
                _matPattern8CyberCircuit = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern8_CyberCircuit.mat");
            if (_matPattern9MultiChain == null)
                _matPattern9MultiChain = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern9_MultiChain.mat");
            if (_matPattern10MicroCrackle == null)
                _matPattern10MicroCrackle = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern10_MicroCrackle.mat");
            if (_matPattern11HeavyArcBurst == null)
                _matPattern11HeavyArcBurst = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern11_HeavyArcBurst.mat");
            if (_matPattern12ThermalAfterglow == null)
                _matPattern12ThermalAfterglow = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern12_ThermalAfterglow.mat");
            if (_matPattern13Anatomical == null)
                _matPattern13Anatomical = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern13_AnatomicalGrounding.mat");
            if (_matPattern14Lichtenberg == null)
                _matPattern14Lichtenberg = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern14_LichtenbergBranch.mat");
            if (_matPattern15SteppedLeader == null)
                _matPattern15SteppedLeader = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern15_SteppedLeader.mat");
            if (_matPattern16ImpactVector == null)
                _matPattern16ImpactVector = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern16_ImpactVector.mat");
            if (_matPattern17Shockwave == null)
                _matPattern17Shockwave = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern17_ShockwaveDistortion.mat");
            if (_matPattern18MagneticWhip == null)
                _matPattern18MagneticWhip = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern18_MagneticWhip.mat");
            if (_matPattern19ArmorSeam == null)
                _matPattern19ArmorSeam = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern19_ArmorSeamShort.mat");
            if (_matPattern20CriticalStrobe == null)
                _matPattern20CriticalStrobe = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/M_Shiro_Pattern20_CriticalStrobe.mat");

            // パス修正: Art/Models/shiro.mat から 正しい Art/Materials/shiro.mat へ変更（色バリエーション判定付き）
            if (_originalMaterialBackup == null)
            {
                _originalMaterialBackup = FindFallbackMaterial();
                if (_originalMaterialBackup != null)
                {
                    CacheOriginalMaterialProps(_originalMaterialBackup);
                }
            }
#endif
        }

        /// <summary>
        /// 放電パターンの切り替え
        /// </summary>
        public void SwitchPattern(ElectricityPattern newPattern, bool triggerBurst = true)
        {
            _currentPattern = newPattern;
            if (newPattern != ElectricityPattern.Original)
            {
                _isStunned = true;
                _currentStunWeight = 1.0f;
                if (triggerBurst)
                {
                    TriggerParticleBurst(18);
                }
            }
            else
            {
                // シェーダーが Original になった場合でも、純粋パーティクルが有効稼働中ならスタン状態を維持
                if (!UsesPureParticles || _pureParticlePattern == PureParticlePattern.Off)
                {
                    _isStunned = false;
                    _currentStunWeight = 0.0f;
                }

                if (_particles != null)
                {
                    _particles.StopEmissionGradually();
                }

                if (!UsesPureParticles || _pureParticlePattern == PureParticlePattern.Off)
                {
                    if (_pureParticles != null)
                    {
                        _pureParticles.SetStun(false, immediate: true);
                        _pureParticles.ClearAndStop();
                    }
                }
            }
            ApplyCurrentPattern();
            SyncParticlesImmediate();
            if (UsesPureParticles)
            {
                SyncPureParticlesImmediate();
            }
        }

        /// <summary>
        /// 現在選択されている放電パターンをレンダラーに反映
        /// バックアップ欠落時のフォールバックおよび恒久最大放電化バグを完全に防止
        /// </summary>
        public void ApplyCurrentPattern()
        {
            EnsureRendererAndSlot();
            LoadMaterialPresets();

            if (_targetRenderer == null || _targetMaterialSlot < 0) return;

            Material[] currentMats = _targetRenderer.sharedMaterials;
            if (currentMats == null || _targetMaterialSlot >= currentMats.Length) return;

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly || _currentPattern == ElectricityPattern.Original)
            {
                // ─────────────────────────────────────────────────────────────
                // 【安全な復元】元のデザイナー制作マテリアルへの復帰処理
                // ─────────────────────────────────────────────────────────────
                Material restoreMat = _originalMaterialBackup;

#if UNITY_EDITOR
                // バックアップが null の場合のフォールバック: 色バリエーションに応じたマテリアルを再ロード
                if (restoreMat == null)
                {
                    restoreMat = FindFallbackMaterial();
                    _originalMaterialBackup = restoreMat;
                }
#endif

                if (restoreMat != null)
                {
                    if (currentMats[_targetMaterialSlot] != restoreMat)
                    {
                        currentMats[_targetMaterialSlot] = restoreMat;
                        _targetRenderer.sharedMaterials = currentMats;
                    }
                    _cachedAppliedMaterial = restoreMat;
                    // MPB をクリア（通常マテリアルに戻し、SRP Batcher完全互換を回復）
                    _targetRenderer.SetPropertyBlock(null, _targetMaterialSlot);
                }
                else
                {
                    // 【恒久最大放電化バグ防止ガード】
                    // バックアップが完全に失われている場合でも、放電強度が1.8で固定化されないよう
                    // MPB で放電強度を 0.0 に強制クランプし、見た目を通常装甲に安全維持する
                    if (_mpb == null) _mpb = new MaterialPropertyBlock();
                    _mpb.Clear();
                    _mpb.SetFloat(_propElectricityIntensity, 0.0f);
                    _targetRenderer.SetPropertyBlock(_mpb, _targetMaterialSlot);
                    Debug.LogWarning("[CharacterElectricityPreview] 元マテリアルのバックアップが見つかりません。恒久放電を防ぐため放電強度を 0 に設定しました。", this);
                }
                return;
            }

            // ─────────────────────────────────────────────────────────────
            // 放電パターンの割り当て
            // ─────────────────────────────────────────────────────────────
            Material targetMat = null;
            switch (_currentPattern)
            {
                case ElectricityPattern.Pattern1_Arc:
                    targetMat = _matPattern1Arc;
                    break;
                case ElectricityPattern.Pattern2_Pulse:
                    targetMat = _matPattern2Pulse;
                    break;
                case ElectricityPattern.Pattern3_Plasma:
                    targetMat = _matPattern3Plasma;
                    break;
                case ElectricityPattern.Pattern4_Gold:
                    targetMat = _matPattern4Gold;
                    break;
                case ElectricityPattern.Pattern5_CleanStreak:
                    targetMat = _matPattern5CleanStreak;
                    break;
                case ElectricityPattern.Pattern6_FresnelRim:
                    targetMat = _matPattern6FresnelRim;
                    break;
                case ElectricityPattern.Pattern7_ShortSparks:
                    targetMat = _matPattern7ShortSparks;
                    break;
                case ElectricityPattern.Pattern8_CyberCircuit:
                    targetMat = _matPattern8CyberCircuit;
                    break;
                case ElectricityPattern.Pattern9_MultiChain:
                    targetMat = _matPattern9MultiChain;
                    break;
                case ElectricityPattern.Pattern10_MicroCrackle:
                    targetMat = _matPattern10MicroCrackle;
                    break;
                case ElectricityPattern.Pattern11_HeavyArcBurst:
                    targetMat = _matPattern11HeavyArcBurst;
                    break;
                case ElectricityPattern.Pattern12_ThermalAfterglow:
                    targetMat = _matPattern12ThermalAfterglow;
                    break;
                case ElectricityPattern.Pattern13_AnatomicalGrounding:
                    targetMat = _matPattern13Anatomical;
                    break;
                case ElectricityPattern.Pattern14_LichtenbergBranch:
                    targetMat = _matPattern14Lichtenberg;
                    break;
                case ElectricityPattern.Pattern15_SteppedLeader:
                    targetMat = _matPattern15SteppedLeader;
                    break;
                case ElectricityPattern.Pattern16_ImpactVector:
                    targetMat = _matPattern16ImpactVector;
                    break;
                case ElectricityPattern.Pattern17_ShockwaveDistortion:
                    targetMat = _matPattern17Shockwave;
                    break;
                case ElectricityPattern.Pattern18_MagneticWhip:
                    targetMat = _matPattern18MagneticWhip;
                    break;
                case ElectricityPattern.Pattern19_ArmorSeamShort:
                    targetMat = _matPattern19ArmorSeam;
                    break;
                case ElectricityPattern.Pattern20_CriticalStrobe:
                    targetMat = _matPattern20CriticalStrobe;
                    break;
            }

            if (targetMat != null)
            {
                if (currentMats[_targetMaterialSlot] != targetMat)
                {
                    currentMats[_targetMaterialSlot] = targetMat;
                    _targetRenderer.sharedMaterials = currentMats;
                }
                _cachedAppliedMaterial = targetMat;

                // マテリアルのベース値をキャッシュ（毎フレームの問い合わせ排除）
                if (targetMat.HasProperty(_propElectricityIntensity))
                {
                    _cachedBaseIntensity = targetMat.GetFloat(_propElectricityIntensity);
                }
                if (targetMat.HasProperty(_propElectricitySpeed))
                {
                    _cachedBaseSpeed = targetMat.GetFloat(_propElectricitySpeed);
                }
                if (targetMat.HasProperty(_propElectricityColor))
                {
                    _cachedBaseMatElectricityColor = targetMat.GetColor(_propElectricityColor);
                }

                // 【色バリエーション保護】切り替え時にテクスチャ・地色・ST を MPB に事前登録
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _mpb.Clear();
                if (_hasOriginalPropsCached)
                {
                    if (_cachedOriginalBaseMap != null)
                    {
                        _mpb.SetTexture(_propBaseMap, _cachedOriginalBaseMap);
                    }
                    _mpb.SetVector(_propBaseMapST, _cachedOriginalBaseMapST);
                    _mpb.SetColor(_propBaseColor, _cachedOriginalBaseColor);
                }
            }

            UpdatePropertyBlockFast();
        }

        /// <summary>
        /// 【完全ゼロGC】毎フレーム呼ばれる高速プロパティブロック更新処理
        /// sharedMaterials を一切呼ばず、GetPropertyBlock の冗長呼び出しも排除
        /// </summary>
        private void UpdatePropertyBlockFast()
        {
            if (_targetRenderer == null || _targetMaterialSlot < 0) return;

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
                if (_hasOriginalPropsCached)
                {
                    if (_cachedOriginalBaseMap != null)
                    {
                        _mpb.SetTexture(_propBaseMap, _cachedOriginalBaseMap);
                    }
                    _mpb.SetVector(_propBaseMapST, _cachedOriginalBaseMapST);
                    _mpb.SetColor(_propBaseColor, _cachedOriginalBaseColor);
                }
            }

            float finalIntensity = _cachedBaseIntensity * _intensityMultiplier * _currentStunWeight;
            float finalSpeed = _cachedBaseSpeed * _speedMultiplier;

            _mpb.SetFloat(_propElectricityIntensity, finalIntensity);
            _mpb.SetFloat(_propElectricitySpeed, finalSpeed);
            _mpb.SetVector(_propMeshBoundsMin, _cachedBoundsMin);
            _mpb.SetVector(_propMeshBoundsInvSize, _cachedBoundsInvSize);
            _mpb.SetVector(_propImpactConfig, new Vector4(_impactCenterUV.x, _impactCenterUV.y, _impactDirectionUV.x, _impactDirectionUV.y));

            // 放電カラーの反映
            if (_overrideColor)
            {
                _mpb.SetColor(_propElectricityColor, _customElectricityColor);
            }
            else
            {
                _mpb.SetColor(_propElectricityColor, _cachedBaseMatElectricityColor);
            }

            _targetRenderer.SetPropertyBlock(_mpb, _targetMaterialSlot);
        }

        // =========================================================================
        // 🎮 プレイヤーシステム連携向け 公開API群 (RacerManagerBase 等からの呼び出し口)
        // 他メンバーのスクリプトを直接編集せず、外から1行で呼べる口を提供
        // =========================================================================

        /// <summary>
        /// 【プレイヤー担当者向けAPI】スタン状態の開始 / 解除を切り替えます。
        /// <para>例: racer.StunStarted += () => electricity.SetStun(true);</para>
        /// <para>例: racer.StunEnded   += () => electricity.SetStun(false);</para>
        /// </summary>
        /// <param name="isStunned">スタン中なら true、解除時は false</param>
        /// <param name="immediate">true の場合、フェードを待たず即座に切り替え、解除時は元のマテリアルへ即時復帰します</param>
        public void SetStun(bool isStunned, bool immediate = false)
        {
            _isStunned = isStunned;
            if (immediate || !Application.isPlaying)
            {
                _currentStunWeight = isStunned ? 1.0f : 0.0f;
            }

            if (isStunned)
            {
                TriggerParticleBurst(22);

                if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
                {
                    if (_pureParticles == null)
                    {
                        EnsurePureParticles(recordUndo: false);
                    }
                    if (_pureParticles != null)
                    {
                        if (_pureParticlePattern == PureParticlePattern.Off)
                        {
                            _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                        }
                        _pureParticles.CurrentPattern = _pureParticlePattern;
                        _pureParticles.ParticlesEnabled = true;
                        _pureParticles.SetStun(true, immediate, triggerBurst: false);
                        SyncPureParticlesImmediate();
                    }
                    return;
                }

                // Original マテリアル表示中にスタン開始された場合、放電パターンへ遷移
                if (_currentPattern == ElectricityPattern.Original)
                {
                    _currentPattern = _stunElectricityPattern != ElectricityPattern.Original ? _stunElectricityPattern : ElectricityPattern.Pattern1_Arc;
                    ApplyCurrentPattern();
                }
                else
                {
                    UpdatePropertyBlockFast();
                }

                if (UsesPureParticles)
                {
                    if (_pureParticles == null)
                    {
                        EnsurePureParticles(recordUndo: false);
                    }
                    if (_pureParticles != null)
                    {
                        if (_pureParticlePattern == PureParticlePattern.Off)
                        {
                            _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                            _pureParticles.CurrentPattern = _pureParticlePattern;
                        }
                        _pureParticles.ParticlesEnabled = true;
                        _pureParticles.SetStun(true, immediate, triggerBurst: false);
                    }
                }
            }
            else
            {
                if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
                {
                    if (_pureParticles != null)
                    {
                        _pureParticles.SetStun(false, immediate, triggerBurst: false);
                        if (immediate || !Application.isPlaying)
                        {
                            _pureParticles.ClearAndStop();
                            _pureParticles.ParticlesEnabled = false;
                        }
                        SyncPureParticlesImmediate();
                    }
                    return;
                }

                if (immediate || !Application.isPlaying)
                {
                    _currentStunWeight = 0.0f;
                    if (_particles != null)
                    {
                        _particles.ClearAndStop();
                    }
                    if (_pureParticles != null)
                    {
                        _pureParticles.SetStun(false, immediate: true, triggerBurst: false);
                        _pureParticles.ClearAndStop();
                        _pureParticles.ParticlesEnabled = false;
                    }
                    if (_autoRestoreOnStunEnd && _currentPattern != ElectricityPattern.Original)
                    {
                        RestoreOriginalMaterial();
                        return;
                    }
                }
                else
                {
                    if (UsesPureParticles && _pureParticles != null)
                    {
                        _pureParticles.SetStun(false, immediate: false, triggerBurst: false);
                    }
                }

                UpdatePropertyBlockFast();
            }

            SyncParticlesImmediate();
            if (UsesPureParticles)
            {
                SyncPureParticlesImmediate();
            }
        }

        /// <summary>
        /// パーティクル演出の即時同期（エディタ編集時やモード切り替え時にフレーム遅延なく即時反映）
        /// </summary>
        public void SyncParticlesImmediate()
        {
            if (_particles == null) return;

            if (_enableParticles && _currentPattern != ElectricityPattern.Original && _isStunned && _currentStunWeight > 0.001f)
            {
                Color effectiveColor = _overrideColor ? _customElectricityColor : _cachedBaseMatElectricityColor;
                _particles.ParticlesEnabled = _enableParticles;
                _particles.RateMultiplier = _particleRateMultiplier;
                _particles.SynchronizeWithElectricity(
                    _intensityMultiplier,
                    _speedMultiplier,
                    effectiveColor,
                    _currentStunWeight,
                    _currentPattern,
                    _impactDirectionUV);
            }
            else
            {
                _particles.StopEmissionGradually();
            }
        }

        /// <summary>
        /// 【プレイヤー担当者向けAPI】スタン残時間やタイマー進行に応じた減衰表現（0.0〜1.0）を設定します。
        /// <para>例: electricity.SetStunNormalizedTime(racer.StunRemainingTime / stunDuration);</para>
        /// </summary>
        /// <param name="normalizedTime">1.0 = 最大放電（スタン開始直後）、0.0 = 放電終了（スタン回復・元マテリアル自動復帰）</param>
        public void SetStunNormalizedTime(float normalizedTime)
        {
            float clamped = Mathf.Clamp01(normalizedTime);
            _currentStunWeight = clamped;
            _isStunned = clamped > 0.001f;

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                if (_pureParticles != null)
                {
                    _pureParticles.SetStunNormalizedTime(clamped);
                }
                return;
            }

            if (_isStunned)
            {
                if (_currentPattern == ElectricityPattern.Original)
                {
                    SwitchPattern(_stunElectricityPattern != ElectricityPattern.Original ? _stunElectricityPattern : ElectricityPattern.Pattern1_Arc);
                }
                UpdatePropertyBlockFast();
                if (UsesPureParticles && _pureParticles != null)
                {
                    _pureParticles.SetStunNormalizedTime(clamped);
                }
            }
            else
            {
                if (UsesPureParticles && _pureParticles != null)
                {
                    _pureParticles.SetStunNormalizedTime(0.0f);
                }
                if (UsesLightweightParticles && _particles != null)
                {
                    _particles.StopEmissionGradually();
                }
                if (_autoRestoreOnStunEnd && _currentPattern != ElectricityPattern.Original)
                {
                    RestoreOriginalMaterial();
                }
                else
                {
                    UpdatePropertyBlockFast();
                }
            }
        }

        /// <summary>
        /// 【プレイヤー担当者向けAPI】放電の強度倍率を動的に制御します（例：大ダメージ電撃、小スパーク）。
        /// </summary>
        /// <param name="intensityMultiplier">通常は 1.0。0.0 で完全消灯、2.0 以上で激しいオーバーロード</param>
        public void SetElectricityIntensity(float intensityMultiplier)
        {
            _intensityMultiplier = Mathf.Max(0.0f, intensityMultiplier);
            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                _pureRateMultiplier = _intensityMultiplier;
                if (_pureParticles != null) _pureParticles.RateMultiplier = _pureRateMultiplier;
            }
            else
            {
                if (UsesPureParticles)
                {
                    _pureRateMultiplier = _intensityMultiplier;
                    if (_pureParticles != null) _pureParticles.RateMultiplier = _pureRateMultiplier;
                }
                UpdatePropertyBlockFast();
            }
        }

        /// <summary>
        /// 【プレイヤー担当者向けAPI】攻撃属性や状態異常の種類に応じて放電色を動的に変更します。
        /// <para>例: 紫のプラズマ毒電撃、赤の過負荷爆発、黄のサンダーボルト</para>
        /// </summary>
        /// <param name="color">放電のHDRカラー</param>
        /// <param name="enableOverride">true でカラー上書きを有効化</param>
        public void SetElectricityColor(Color color, bool enableOverride = true)
        {
            _customElectricityColor = color;
            _overrideColor = enableOverride;
            if (_pureParticles != null)
            {
                _pureParticles.OverrideColor = enableOverride;
                _pureParticles.CustomColor = color;
            }
            if (_dischargeMode != CharacterDischargeMode.ParticleOnly)
            {
                UpdatePropertyBlockFast();
            }
        }

        /// <summary>
        /// 【プレイヤー担当者向けAPI】タックル衝突や被弾時の「着弾点UV」と「吹っ飛び方向ベクトル」を設定します（Pattern 16 連動）。
        /// </summary>
        /// <param name="impactCenterUV">着弾点のUV座標 (0.0〜1.0)</param>
        /// <param name="impactDirectionUV">電撃・火花が吹き飛ぶ方向ベクトル</param>
        public void SetImpactVector(Vector2 impactCenterUV, Vector2 impactDirectionUV)
        {
            _impactCenterUV = impactCenterUV;
            _impactDirectionUV = impactDirectionUV;
            UpdatePropertyBlockFast();
        }

        /// <summary>
        /// 【プレイヤー担当者向けAPI】ワンクリックで元のデザイナー制作マテリアルへ完全復帰します。
        /// </summary>
        public void RestoreOriginalMaterial()
        {
            SwitchPattern(ElectricityPattern.Original);
        }

        /// <summary>
        /// 現在アタッチされているマテリアルからバックアップを強制再取得します。
        /// インスペクターでキャラクターの色を変更した後に呼び出します。
        /// </summary>
        public void RefreshOriginalMaterialBackup()
        {
            if (_targetRenderer != null && _targetMaterialSlot >= 0)
            {
                Material[] sharedMats = _targetRenderer.sharedMaterials;
                if (sharedMats != null && _targetMaterialSlot < sharedMats.Length)
                {
                    Material cur = sharedMats[_targetMaterialSlot];
                    if (cur != null && !IsElectricityMaterial(cur))
                    {
                        _originalMaterialBackup = cur;
                        CacheOriginalMaterialProps(_originalMaterialBackup);
                    }
                }
            }
        }

        /// <summary>
        /// 動作モード（シェーダーのみ / パーティクルのみ / ハイブリッド / リッチハイブリッド）の切り替え
        /// </summary>
        public void SetDischargeMode(CharacterDischargeMode newMode, bool recordUndo = true)
        {
            _dischargeMode = newMode;

            switch (newMode)
            {
                case CharacterDischargeMode.ShaderOnly:
                    _enableParticles = false;
                    DeletePureParticles(recordUndo);
                    DeleteHybridParticles(recordUndo);
                    if (_currentPattern == ElectricityPattern.Original)
                    {
                        _currentPattern = _stunElectricityPattern != ElectricityPattern.Original ? _stunElectricityPattern : ElectricityPattern.Pattern1_Arc;
                    }
                    _isStunned = true;
                    _currentStunWeight = 1.0f;
                    ApplyCurrentPattern();
                    break;

                case CharacterDischargeMode.ParticleOnly:
                    _enableParticles = false;
                    DeleteHybridParticles(recordUndo);
                    EnsurePureParticles(recordUndo);
                    if (_pureParticlePattern == PureParticlePattern.Off)
                    {
                        _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                    }
                    _isStunned = true;
                    _currentStunWeight = 1.0f;
                    ApplyCurrentPattern(); // 元マテリアル復元
                    SyncPureParticlesImmediate();
                    break;

                case CharacterDischargeMode.RichHybrid:
                    _enableParticles = true;
                    _hybridParticleSource = HybridParticleSource.PureParticles;
                    DeleteHybridParticles(recordUndo);
                    EnsurePureParticles(recordUndo);
                    if (_currentPattern == ElectricityPattern.Original)
                    {
                        _currentPattern = _stunElectricityPattern != ElectricityPattern.Original ? _stunElectricityPattern : ElectricityPattern.Pattern1_Arc;
                    }
                    if (_pureParticlePattern == PureParticlePattern.Off)
                    {
                        _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                    }
                    _isStunned = true;
                    _currentStunWeight = 1.0f;
                    ApplyCurrentPattern();
                    SyncPureParticlesImmediate();
                    break;

                case CharacterDischargeMode.Hybrid:
                    _enableParticles = true;
                    if (_currentPattern == ElectricityPattern.Original)
                    {
                        _currentPattern = _stunElectricityPattern != ElectricityPattern.Original ? _stunElectricityPattern : ElectricityPattern.Pattern1_Arc;
                    }
                    _isStunned = true;
                    _currentStunWeight = 1.0f;
                    if (_hybridParticleSource == HybridParticleSource.PureParticles)
                    {
                        DeleteHybridParticles(recordUndo);
                        EnsurePureParticles(recordUndo);
                        if (_pureParticlePattern == PureParticlePattern.Off)
                        {
                            _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                        }
                        ApplyCurrentPattern();
                        SyncPureParticlesImmediate();
                    }
                    else if (_hybridParticleSource == HybridParticleSource.Both)
                    {
                        EnsurePureParticles(recordUndo);
                        SetupParticles();
                        if (_pureParticlePattern == PureParticlePattern.Off)
                        {
                            _pureParticlePattern = PureParticlePattern.Pattern1_ArcRibbons;
                        }
                        ApplyCurrentPattern();
                        SyncPureParticlesImmediate();
                        SyncParticlesImmediate();
                    }
                    else // Lightweight
                    {
                        DeletePureParticles(recordUndo);
                        SetupParticles();
                        ApplyCurrentPattern();
                        SyncParticlesImmediate();
                    }
                    break;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && this != null)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// ハイブリッド放電演出時のパーティクル連携方式（純粋パーティクル / 軽量スパーク / 両方併用）を切り替え
        /// </summary>
        public void SetHybridParticleSource(HybridParticleSource source, bool recordUndo = true)
        {
            _hybridParticleSource = source;
            if (_dischargeMode == CharacterDischargeMode.Hybrid || _dischargeMode == CharacterDischargeMode.RichHybrid)
            {
                if (source == HybridParticleSource.PureParticles)
                {
                    DeleteHybridParticles(recordUndo);
                    EnsurePureParticles(recordUndo);
                    ApplyCurrentPattern();
                    SyncPureParticlesImmediate();
                }
                else if (source == HybridParticleSource.Both)
                {
                    EnsurePureParticles(recordUndo);
                    SetupParticles();
                    ApplyCurrentPattern();
                    SyncPureParticlesImmediate();
                    SyncParticlesImmediate();
                }
                else // Lightweight
                {
                    DeletePureParticles(recordUndo);
                    SetupParticles();
                    ApplyCurrentPattern();
                    SyncParticlesImmediate();
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && this != null)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 純粋パーティクル放電パターンの切り替え
        /// </summary>
        public void SwitchPureParticlePattern(PureParticlePattern newPattern, bool triggerBurst = true)
        {
            _pureParticlePattern = newPattern;
            if (newPattern != PureParticlePattern.Off)
            {
                if (_dischargeMode == CharacterDischargeMode.ShaderOnly)
                {
                    _dischargeMode = CharacterDischargeMode.RichHybrid;
                }
                _isStunned = true;
                _currentStunWeight = 1.0f;
            }
            else
            {
                if (_dischargeMode == CharacterDischargeMode.ParticleOnly || _currentPattern == ElectricityPattern.Original)
                {
                    _isStunned = false;
                    _currentStunWeight = 0.0f;
                }
            }

            if (_dischargeMode == CharacterDischargeMode.ParticleOnly || UsesPureParticles)
            {
                EnsurePureParticles();
                if (_pureParticles != null)
                {
                    _pureParticles.CurrentPattern = newPattern;
                    if (newPattern != PureParticlePattern.Off)
                    {
                        _pureParticles.ParticlesEnabled = true;
                        _pureParticles.SetStun(true, immediate: true, triggerBurst: false);
                        if (triggerBurst)
                        {
                            _pureParticles.TriggerBurst(22);
                        }
                    }
                    else
                    {
                        _pureParticles.SetStun(false, immediate: true, triggerBurst: false);
                        _pureParticles.ClearAndStop();
                        _pureParticles.ParticlesEnabled = false;
                    }
                    SyncPureParticlesImmediate();
                }
            }
        }

        /// <summary>
        /// 純粋パーティクルコントローラーの自動確保（子オブジェクト PureElectricityParticles の生成・バインド）
        /// </summary>
        public void EnsurePureParticles(bool recordUndo = true)
        {
            EnsureRendererAndSlot();
            if (_pureParticles == null)
            {
                _pureParticles = GetComponentInChildren<CharacterPureParticleElectricity>(true);
                if (_pureParticles == null)
                {
                    Transform existingChild = transform.Find("PureElectricityParticles");
                    if (existingChild != null)
                    {
                        _pureParticles = existingChild.GetComponent<CharacterPureParticleElectricity>();
                        if (_pureParticles == null)
                        {
                            _pureParticles = existingChild.gameObject.AddComponent<CharacterPureParticleElectricity>();
                        }
                    }
                }

                if (_pureParticles == null)
                {
                    GameObject pGo = new GameObject("PureElectricityParticles");
                    if (gameObject.hideFlags.HasFlag(HideFlags.DontSave))
                    {
                        pGo.hideFlags |= HideFlags.DontSave;
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying && recordUndo && !EditorApplication.isUpdating && !EditorApplication.isCompiling && (pGo.hideFlags & HideFlags.DontSave) == 0)
                    {
                        try { Undo.RegisterCreatedObjectUndo(pGo, "Create PureElectricityParticles"); } catch {}
                    }
#endif
                    pGo.transform.SetParent(transform, false);
                    pGo.transform.localPosition = Vector3.zero;
                    pGo.transform.localRotation = Quaternion.identity;
                    pGo.transform.localScale = Vector3.one;

                    _pureParticles = pGo.AddComponent<CharacterPureParticleElectricity>();
                }
            }

            if (_targetRenderer != null && _pureParticles != null)
            {
                _pureParticles.TargetRenderer = _targetRenderer;
                _pureParticles.EnsureTargetRenderer();
            }

            if (_pureParticles != null)
            {
                _pureParticles.SetupParticlesHierarchy();
                SyncPureParticlesImmediate();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && this != null)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 純粋パーティクルコントローラーおよび子オブジェクトの即時パラメータ同期
        /// </summary>
        public void SyncPureParticlesImmediate()
        {
            if (_pureParticles == null) return;

            _pureParticles.TargetRenderer = _targetRenderer;
            _pureParticles.RateMultiplier = _pureRateMultiplier;
            _pureParticles.SpeedMultiplier = _pureSpeedMultiplier;
            _pureParticles.SizeMultiplier = _pureSizeMultiplier;
            _pureParticles.RibbonWorldSpace = _pureRibbonWorldSpace;
            _pureParticles.OverrideColor = _overrideColor;
            if (_overrideColor)
            {
                _pureParticles.CustomColor = _customElectricityColor;
            }
            _pureParticles.CurrentPattern = _pureParticlePattern;

            bool isPureActive = _pureParticlePattern != PureParticlePattern.Off;
            bool shouldEmit = (_isStunned || _currentStunWeight > 0.001f) && isPureActive;
            _pureParticles.ParticlesEnabled = shouldEmit;
            _pureParticles.SetStun(_isStunned && isPureActive, immediate: true, triggerBurst: false);
            _pureParticles.SetStunNormalizedTime(isPureActive ? _currentStunWeight : 0.0f);
            if (!shouldEmit)
            {
                _pureParticles.ClearAndStop();
            }
        }

        /// <summary>
        /// 純粋パーティクルオブジェクト（PureElectricityParticles および PS_Pure_*）の完全安全消去
        /// </summary>
        public void DeletePureParticles(bool recordUndo = true)
        {
            if (_pureParticles != null)
            {
                _pureParticles.CleanupAllParticles(recordUndo);
                GameObject go = _pureParticles.gameObject;
                var pureComp = _pureParticles;
                _pureParticles = null;
                if (go != gameObject)
                {
                    SafeDestroyObject(go, recordUndo);
                }
                else
                {
                    SafeDestroyObject(pureComp, recordUndo);
                }
            }

            Transform child = transform.Find("PureElectricityParticles");
            if (child != null && child.gameObject != gameObject)
            {
                SafeDestroyObject(child.gameObject, recordUndo);
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = transform.GetChild(i);
                if (ch != null && ch.gameObject != gameObject &&
                    (ch.name.StartsWith("PS_Pure_") || ch.name == "PureElectricityParticles"))
                {
                    SafeDestroyObject(ch.gameObject, recordUndo);
                }
            }

            var allPure = GetComponentsInChildren<CharacterPureParticleElectricity>(true);
            foreach (var p in allPure)
            {
                if (p != null && p.gameObject != gameObject)
                {
                    SafeDestroyObject(p.gameObject, recordUndo);
                }
            }
        }

        /// <summary>
        /// ハイブリッド用パーティクルオブジェクト（ElectricityParticles）の完全安全消去
        /// </summary>
        public void DeleteHybridParticles(bool recordUndo = true)
        {
            if (_particles != null)
            {
                GameObject pGo = _particles.gameObject;
                var partComp = _particles;
                _particles = null;

                if (pGo != gameObject)
                {
                    SafeDestroyObject(pGo, recordUndo);
                }
                else
                {
                    SafeDestroyObject(partComp, recordUndo);
                }
            }

            Transform child = transform.Find("ElectricityParticles");
            if (child != null && child.gameObject != gameObject)
            {
                SafeDestroyObject(child.gameObject, recordUndo);
            }

            var remaining = GetComponentsInChildren<CharacterElectricityParticles>(true);
            foreach (var p in remaining)
            {
                if (p != null && p.gameObject != gameObject)
                {
                    SafeDestroyObject(p.gameObject, recordUndo);
                }
            }
        }

        /// <summary>
        /// 子階層に生成されたパーティクルオブジェクトが存在するかどうかを判定
        /// </summary>
        public bool HasAnyParticleObjects()
        {
            if (_particles != null) return true;
            if (_pureParticles != null) return true;
            if (transform.Find("ElectricityParticles") != null) return true;
            if (transform.Find("PureElectricityParticles") != null) return true;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).name.StartsWith("PS_Pure_")) return true;
            }
            var parts = GetComponentsInChildren<CharacterElectricityParticles>(true);
            if (parts != null && parts.Length > 0) return true;
            var pureParts = GetComponentsInChildren<CharacterPureParticleElectricity>(true);
            if (pureParts != null && pureParts.Length > 0) return true;
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

        /// <summary>
        /// 生成された子パーティクルオブジェクトを完全に破棄し、
        /// メッシュシェーダーのみの100%単体動作モードに切り替えます。
        /// ヒエラルキー構成を一切汚さず、余計なGameObjectを残しません。
        /// </summary>
        public void DeleteParticlesAndSetShaderOnly(bool recordUndo = true)
        {
            _dischargeMode = CharacterDischargeMode.ShaderOnly;
            _enableParticles = false;

            DeleteHybridParticles(recordUndo);
            DeletePureParticles(recordUndo);

#if UNITY_EDITOR
            if (!Application.isPlaying && this != null)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 【安全クリーンアップ】元のデザイナー制作マテリアル（shiro/ao/kuro）へ完全復帰し、
        /// 生成された全子パーティクルオブジェクト（ハイブリッド ＆ 純粋パーティクルの両方）を安全に破棄して初期状態に戻します。
        /// スクリプトを Remove Component する前や、構成を破棄する前に呼び出します。
        /// キャラクター本来のボーン・メッシュ構成は一切破壊されません。
        /// </summary>
        public void ResetToOriginalAndCleanup(bool recordUndo = true)
        {
#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying && (hideFlags & HideFlags.DontSave) == 0)
            {
                Undo.RegisterCompleteObjectUndo(this, "Reset Character Electricity");
                if (_targetRenderer != null && (_targetRenderer.hideFlags & HideFlags.DontSave) == 0)
                {
                    Undo.RegisterCompleteObjectUndo(_targetRenderer, "Reset Character Electricity Material");
                }
            }
#endif

            // 1. 元のデザイナーマテリアルへ完全復帰
            RestoreOriginalMaterial();

            // 2. MPBをクリアして SRP Batcher 最適化復帰
            if (_targetRenderer != null && _targetMaterialSlot >= 0)
            {
                _targetRenderer.SetPropertyBlock(null, _targetMaterialSlot);
            }

            // 3. 子パーティクルオブジェクト（ハイブリッド ＆ 純粋パーティクルの両方）を完全破棄
            DeleteHybridParticles(recordUndo);
            DeletePureParticles(recordUndo);

            // 4. ステート初期化
            _currentPattern = ElectricityPattern.Original;
            _pureParticlePattern = PureParticlePattern.Off;
            _isStunned = false;
            _currentStunWeight = 0.0f;

#if UNITY_EDITOR
            if (_targetRenderer != null)
            {
                EditorUtility.SetDirty(_targetRenderer);
            }
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// パーティクルシステムの自動生成・セットアップ（ワンクリック構築）
        /// </summary>
        public void SetupParticles()
        {
            _enableParticles = true;
            EnsureRendererAndSlot();
            if (_particles == null)
            {
                _particles = GetComponentInChildren<CharacterElectricityParticles>(true);
                if (_particles == null)
                {
                    Transform existingChild = transform.Find("ElectricityParticles");
                    if (existingChild != null)
                    {
                        _particles = existingChild.GetComponent<CharacterElectricityParticles>();
                        if (_particles == null)
                        {
                            _particles = existingChild.gameObject.AddComponent<CharacterElectricityParticles>();
                        }
                    }
                }

                if (_particles == null)
                {
                    GameObject pGo = new GameObject("ElectricityParticles");
                    if (gameObject.hideFlags.HasFlag(HideFlags.DontSave))
                    {
                        pGo.hideFlags |= HideFlags.DontSave;
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying && !EditorApplication.isUpdating && !EditorApplication.isCompiling && (pGo.hideFlags & HideFlags.DontSave) == 0)
                    {
                        try { Undo.RegisterCreatedObjectUndo(pGo, "Create ElectricityParticles"); } catch {}
                    }
#endif
                    pGo.transform.SetParent(transform, false);
                    pGo.transform.localPosition = Vector3.zero;
                    pGo.transform.localRotation = Quaternion.identity;
                    pGo.transform.localScale = Vector3.one;

                    _particles = pGo.AddComponent<CharacterElectricityParticles>();
                }
            }

            if (_targetRenderer != null)
            {
                _particles.TargetRenderer = _targetRenderer;
            }
            _particles.EnsureTargetRenderer();
            _particles.SetupParticlesHierarchy();

            // Original または非スタン時は初期状態で火花を放出させない
            if (_currentPattern == ElectricityPattern.Original || !_isStunned || _currentStunWeight <= 0.001f)
            {
                _particles.StopEmissionGradually();
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 強放電・被弾・衝撃バースト火花をワンショット射出（純粋・ハイブリッド両対応）
        /// </summary>
        public void TriggerParticleBurst(int count = 25)
        {
            if (_dischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                if (_pureParticles != null)
                {
                    Color col = _overrideColor ? _customElectricityColor : new Color(0.25f, 0.85f, 2.5f, 1.0f);
                    _pureParticles.TriggerBurst(count, _pureSpeedMultiplier, col);
                }
                return;
            }

            if (UsesPureParticles && _pureParticles != null)
            {
                Color col = _overrideColor ? _customElectricityColor : _cachedBaseMatElectricityColor;
                _pureParticles.TriggerBurst(count, _pureSpeedMultiplier, col);
            }

            if (UsesLightweightParticles && _particles != null && _enableParticles)
            {
                Color col = _overrideColor ? _customElectricityColor : _cachedBaseMatElectricityColor;
                _particles.TriggerBurst(count, _speedMultiplier, col, _impactDirectionUV);
            }
        }
    }

    /// <summary>
    /// CharacterElectricityPreview のクラス名エイリアス。
    /// チームメンバーやインスペクターからの視認性向上のため、
    /// 「プレビュー」だけでなく本番「コントローラー」としても同等にアタッチ・利用可能です。
    /// </summary>
    [AddComponentMenu("AT_Murayama/Character Electricity/Character Electricity Controller")]
    public class CharacterElectricityController : CharacterElectricityPreview
    {
    }
}
