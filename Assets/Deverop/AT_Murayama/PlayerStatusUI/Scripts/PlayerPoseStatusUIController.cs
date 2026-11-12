using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AT_Murayama.PlayerStatusUI
{
    /// <summary>
    /// プレイヤーの現在の状態（Stunned / Boosting / Shielding / Normal ＋ 左右移動 Steering）を受け取り、
    /// 画面前面（RenderTexture UI または カメラ前3Dミニチュア）に表示した3Dキャラクターモデル（born model.fbx等）を
    /// 事前保存された各状態のポーズ＆発光色へ滑らかに補間遷移させるコントローラー。
    /// ・通常マテリアル／ホログラムシェーダー／カスタムマテリアルの切り替えに対応。
    /// ・複数Rendererや複数マテリアルスロットを持つモデルでも全スロットへ確実に適用・復元します。
    /// ・UI背景色・アルファ透明度・外枠色・サブカメラ背景クリア色をインスペクターから自由に変更可能です。
    /// ・トラッキングや PlayerManager / PlayerFlightController 側の既存コードを一切変更せず、外部から読み取るだけで安全に動作します。
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class PlayerPoseStatusUIController : MonoBehaviour
    {
        public enum UIDisplayMode
        {
            [InspectorName("方式A：専用サブカメラ ＋ RenderTexture (Canvas UI前面表示・推奨)")]
            ModeA_RenderTextureUI = 0,

            [InspectorName("方式B：メインカメラ前 3Dミニチュア直接配置")]
            ModeB_CameraForeground3D = 1
        }

        public enum ScreenCorner
        {
            [InspectorName("右下 (Bottom Right - 従来標準)")]
            BottomRight = 0,

            [InspectorName("左下 (Bottom Left)")]
            BottomLeft = 1,

            [InspectorName("右上 (Top Right)")]
            TopRight = 2,

            [InspectorName("左上 (Top Left)")]
            TopLeft = 3,

            [InspectorName("画面下中央 (Bottom Center - 推奨候補)")]
            BottomCenter = 4,

            [InspectorName("画面上中央 (Top Center)")]
            TopCenter = 5,

            [InspectorName("画面中央 (Center)")]
            Center = 6,

            [InspectorName("カスタム自由配置 (Custom)")]
            Custom = 7
        }

        public enum SteeringPoseMode
        {
            [InspectorName("しきい値で『左移動ポーズ』『右移動ポーズ』へ状態遷移")]
            DiscreteStateSwitch = 0,

            [InspectorName("Steering値 (-1〜+1) に応じて通常ポーズと左右ポーズを連続ブレンド")]
            ContinuousBlend = 1
        }

        public enum MaterialDisplayMode
        {
            [InspectorName("① モデル本来の通常マテリアルを使用 (Original Material)")]
            OriginalMaterial = 0,

            [InspectorName("② サイバーホログラムシェーダー (Hologram Shader)")]
            HologramShader = 1,

            [InspectorName("③ ユーザー指定カスタムマテリアル (Custom Material)")]
            CustomMaterial = 2
        }

        /// <summary>
        /// プレイヤー状態ポーズUIに適用できる全16種類のホログラムパターン
        /// （定番7種 ＋ 汎用拡張4種 ＋ 新設UI特化5種）
        /// </summary>
        public enum UIHologramPattern
        {
            [InspectorName("01. 王道スキャンライン (Classic Scanline)")]
            ClassicScanline = 0,

            [InspectorName("02. 六角形ヘックスシールド (Hex Shield)")]
            HexShield = 1,

            [InspectorName("03. 3D等高線CADワイヤーフレーム (Wireframe Grid)")]
            WireframeGrid = 2,

            [InspectorName("04. サイバーRGB色収差グリッチ (Cyber Glitch)")]
            CyberGlitch = 3,

            [InspectorName("05. サイバーLEDドットマトリクス (LED Matrix)")]
            LEDMatrix = 4,

            [InspectorName("06. 流れるサイバー矢印・ストライプ (Pattern Banner)")]
            PatternBanner = 5,

            [InspectorName("07. 台座プロジェクター照射光 (Projector Beam)")]
            ProjectorBeam = 6,

            [InspectorName("08. 最軽量フレネル輪郭・呼吸パルス (Ghost Rim)")]
            GhostRim = 7,

            [InspectorName("09. 出現消滅ディゾルブ (Dissolve Energy)")]
            Dissolve = 8,

            [InspectorName("10. 3軸均等ドット点群 (Triplanar Dots)")]
            TriplanarDots = 9,

            [InspectorName("11. 高さグラデーション・上昇エネルギー (Gradient Energy)")]
            GradientEnergy = 10,

            [InspectorName("12. ✨ UI専用：サイバーネオン・クリスタル (Cyber Neon)")]
            UI_CyberNeon = 11,

            [InspectorName("13. ✨ UI専用：クアンタム格子・デジタルスケルトン (Quantum Lattice)")]
            UI_QuantumLattice = 12,

            [InspectorName("14. ✨ UI専用：戦術データストリーム・グリッチ (Tactical Glitch)")]
            UI_TacticalGlitch = 13,

            [InspectorName("15. ✨ UI専用：オーロラプラズマ・シールド (Aurora Plasma)")]
            UI_AuroraPlasma = 14,

            [InspectorName("16. ✨ UI専用：レーダースキャン・パルスグロー (Glint Pulse Radar)")]
            UI_GlintPulseRadar = 15
        }

        // ──────────────────────────────────────────────
        // Inspector 設定
        // ──────────────────────────────────────────────

        [Header("1. プレイヤー参照（未指定時はシーン内から自動検索）")]
        [Tooltip("状態を保持する PlayerManager（IsStunned / IsBoosting / IsShielding / 各イベントの取得元）")]
        [SerializeField] private PlayerManager _playerManager;

        [Tooltip("左右移動量（Steering: -1〜+1）を保持する PlayerFlightController")]
        [SerializeField] private PlayerFlightController _flightController;

        [Header("2. 3Dキャラクターモデル ＆ ポーズプリセットデータ")]
        [Tooltip("UIに表示するボーン入りキャラクターモデルプレハブ（未指定時は Assets/TeamResources/Art/Models/born model.fbx を自動ロード）")]
        [SerializeField] private GameObject _characterModelPrefab;

        [Tooltip("実行前に保存したポーズデータを保持する ScriptableObject アセット（任意・インスペクターからワンクリック作成＆保存可能）")]
        [SerializeField] private PlayerPosePresetData _posePresetAsset;

        [Tooltip("アセット未作成時でもシーン／プレハブ内に直接ポーズを保存する内蔵リスト")]
        [SerializeField] private List<StatePoseEntry> _inlineStatePoses = new List<StatePoseEntry>();

        [Tooltip("モデルの初期バインドポーズ（Tポーズ）のバックアップ")]
        [SerializeField] private List<BoneTransformSnapshot> _bindPoseSnapshots = new List<BoneTransformSnapshot>();

        [Header("3. 状態判定・左右移動（Steering）遷移設定")]
        [Tooltip("左右移動（Steering）のポーズ反映方式")]
        [SerializeField] private SteeringPoseMode _steeringMode = SteeringPoseMode.DiscreteStateSwitch;

        [Tooltip("Steering（-1〜+1）がこの絶対値を超えたら『左移動』『右移動』ポーズへ切り替えるしきい値")]
        [Range(0.05f, 0.8f)]
        [SerializeField] private float _steeringThreshold = 0.2f;

        [Tooltip("トラッキング入力の境界で状態が1フレームごとにパタパタ切り替わる（チャタリング）のを防ぐ最小維持秒数")]
        [Range(0f, 0.25f)]
        [SerializeField] private float _stateChangeDebounceTime = 0.06f;

        [Header("4. ポーズ補間・アイドル呼吸揺れ・スタン演出")]
        [Tooltip("ポーズ遷移の全体速度倍率")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _globalTransitionSpeedMultiplier = 1.0f;

        [Tooltip("静止中も固まって見えないよう、微弱な呼吸・浮遊揺れを加えるか")]
        [SerializeField] private bool _enableIdleBreathing = true;

        [Tooltip("呼吸・浮遊揺れの強さ")]
        [Range(0f, 2.0f)]
        [SerializeField] private float _idleBreathingAmplitude = 0.6f;

        [Tooltip("スタン中（IsStunned）に上半身をフラフラ揺らし、ホログラムを激しく点滅させるか")]
        [SerializeField] private bool _enableStunWobbleAndFlicker = true;

        [Header("5. マテリアル・見た目設定")]
        [Tooltip("UIモデルの表示マテリアル方式（モデル本来の通常マテリアル／サイバーホログラムシェーダー／カスタムマテリアル）")]
        [SerializeField] private MaterialDisplayMode _materialMode = MaterialDisplayMode.OriginalMaterial;

        [Tooltip("状態（スタン、シールド/ガード、ブースト等）によるモデルの強制色変化・ティントを有効にするか。OFFにするとスタン時やガード時でも本来のマテリアル色／ホログラム配色を100%維持します")]
        [SerializeField] private bool _enableStateColorOverride = true;

        [Tooltip("【通常マテリアル時】状態変化（スタン・ブースト・シールド等）に応じてモデルの色をほんのり発光・ティントさせるか")]
        [SerializeField] private bool _tintOriginalMaterialOnState = true;

        [Tooltip("【通常マテリアル時】ティントの強度（0でティントなし完全オリジナル、1でしっかり状態色を反映）")]
        [Range(0f, 1f)]
        [SerializeField] private float _originalMaterialTintIntensity = 0.35f;

        [Tooltip("【ホログラム時】キャラクターモデルに適用する3Dホログラムパターン（全16種類）")]
        [SerializeField] private UIHologramPattern _hologramPattern = UIHologramPattern.ClassicScanline;

        [Tooltip("【ホログラム時】ホログラムマテリアルの色をプレイヤー状態（通常/ブースト/シールド/スタン）の発光色と連動させるか（OFFにするとマテリアル固有の配色を完全維持）")]
        [SerializeField] private bool _syncHologramColorsWithState = true;

        [Tooltip("GhostRim（最軽量ゴースト輪郭）選択時、呼吸パルスを無効化して常時一定強度（静止・Static）にするか")]
        [SerializeField] private bool _disableGhostRimPulse = false;

        [SerializeField, HideInInspector] private ModelHologramController.ModelHologramStyle _hologramStyle =
            ModelHologramController.ModelHologramStyle.ClassicScanline;

        [Tooltip("【カスタムマテリアル時】モデルの全スロットに割り当てるカスタムマテリアル")]
        [SerializeField] private Material _customMaterial;

        [Tooltip("切り替わり瞬間（BoostStarted / ShieldStarted / ShieldBlocked / Stun開始）の発光フラッシュ＆スケール演出を有効にするか")]
        [SerializeField] private bool _enableEventFlash = true;

        [Tooltip("切り替わり瞬間（BoostStarted / ShieldStarted / ShieldBlocked / Stun開始）の発光パルス強度")]
        [Range(0f, 4.0f)]
        [SerializeField] private float _eventFlashIntensity = 2.0f;

        [Header("6. UI背景・枠線・サブカメラ設定")]
        [Tooltip("UI下部の状態名テキスト（NORMAL / BOOST / SHIELD / STUNNED 等）を表示するか")]
        [SerializeField] private bool _showStateLabel = true;

        [Tooltip("UI背景の半透明バックプレートを表示するか")]
        [SerializeField] private bool _showUIBackdrop = true;

        [Tooltip("UI背景バックプレートの色と透明度（アルファを0にすると背景が完全透明になりキャラクターのみ浮きます。03シーン設定準拠：透過デフォルト）")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _uiBackdropColor = new Color(0.02f, 0.08f, 0.14f, 0.0f);

        [Tooltip("UI外枠（ボーダー）を表示するか（03シーン設定準拠：非表示デフォルト）")]
        [SerializeField] private bool _showUIBorderFrame = false;

        [Tooltip("UI外枠の色をプレイヤー状態の発光色と連動させるか（ON: 通常はシアン、ブースト時はオレンジ、シールド時は緑、スタン時は赤等に発光）")]
        [SerializeField] private bool _dynamicBorderColor = true;

        [Tooltip("【動的枠色OFF時】UI外枠の固定カラー")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _uiBorderStaticColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("UI外枠の透明度（アルファ倍率。03シーン設定準拠：0デフォルト）")]
        [Range(0f, 1f)]
        [SerializeField] private float _uiBorderAlpha = 0.0f;

        [Tooltip("サブカメラの背景クリア色（通常は完全透明 (0,0,0,0) を推奨。単色やグラデーションにしたい場合に変更可能）")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _cameraBackgroundColor = new Color(0f, 0f, 0f, 0f);

        [Tooltip("RenderTexture表示時にUI専用アルファブレンド（Premultiplied Alpha）を適用するか（ホログラムの加算発光・走査線が背景透過時も暗くならず鮮やかに光ります）")]
        [SerializeField] private bool _usePremultipliedUIAlpha = true;

        [Header("7. UI表示方式・レイアウト・カメラアングル設定")]
        [Tooltip("方式A（RenderTexture＋Canvas UI表示）と 方式B（メインカメラ前3Dミニチュア配置）を切り替えます")]
        [SerializeField] private UIDisplayMode _displayMode = UIDisplayMode.ModeA_RenderTextureUI;

        [Tooltip("画面のどの位置・アンカーにステータスUIを表示するか")]
        [SerializeField] private ScreenCorner _screenCorner = ScreenCorner.BottomRight;

        [Tooltip("カスタム配置時のアンカー位置 (X: 0=左〜1=右, Y: 0=下〜1=上)")]
        [SerializeField] private Vector2 _customAnchor = new Vector2(0.5f, 0f);

        [Tooltip("カスタム配置時のピボット位置 (X: 0=左〜1=右, Y: 0=下〜1=上)")]
        [SerializeField] private Vector2 _customPivot = new Vector2(0.5f, 0f);

        [Tooltip("UIの表示サイズ（ピクセル。03シーン設定準拠：390x450）")]
        [SerializeField] private Vector2 _uiSize = new Vector2(390f, 450f);

        [Tooltip("画面端・アンカー基準位置からの余白・オフセット（ピクセル）")]
        [SerializeField] private Vector2 _uiMargin = new Vector2(28f, 28f);

        [Tooltip("キャラクターを映す向き（度）。160〜200度で正面〜斜め前、0〜30度で背面〜斜め後ろから映します")]
        [Range(0f, 360f)]
        [SerializeField] private float _modelViewYaw = 165f;

        [Tooltip("カメラの上下俯瞰アングル（度）")]
        [Range(-25f, 35f)]
        [SerializeField] private float _cameraPitch = 6f;

        [Tooltip("カメラの距離倍率（小さいほど上半身に寄り、大きいほど全身が引きで映ります）")]
        [Range(0.45f, 2.0f)]
        [SerializeField] private float _cameraDistanceMultiplier = 0.95f;

        [Tooltip("カメラの高さオフセット（全身の中心に対する上下調整）")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float _cameraHeightOffset = 0.05f;

        [Tooltip("【方式A】UIモデル専用レイヤー番号（デフォルト29。ミニマップの30と被らない番号）")]
        [Range(8, 31)]
        [SerializeField] private int _uiModelLayer = 29;

        [Tooltip("【方式B】メインカメラからの前方距離（m）")]
        [Range(0.4f, 3.0f)]
        [SerializeField] private float _modeBForwardDistance = 1.1f;

        [Tooltip("【方式B】メインカメラ前のミニチュアスケール")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _modeBMiniatureScale = 0.08f;

        [Tooltip("【方式B】トンネルの壁や地形に近づいてもUIモデルが壁に埋もれないよう最前面描画（ZTest Always）を適用するか")]
        [SerializeField] private bool _alwaysOnTopInModeB = true;

        [Header("8. デバッグ・手動テストモード（トラッキング無しで確認用）")]
        [Tooltip("ONにすると PlayerManager の状態に関わらず、下の『手動テスト状態』と『手動Steering』でUIを自由にテストできます")]
        [SerializeField] private bool _useManualTestMode = false;

        [Tooltip("手動テストモード時の表示状態")]
        [SerializeField] private PlayerUIState _manualTestState = PlayerUIState.Normal;

        [Tooltip("手動テストモード時の左右移動量（-1=左, 0=中央, +1=右）")]
        [Range(-1f, 1f)]
        [SerializeField] private float _manualTestSteering = 0f;

        [Tooltip("現在インスペクター上でポーズ編集・保存対象として選択している状態")]
        [SerializeField] private PlayerUIState _selectedEditState = PlayerUIState.Normal;

        // ──────────────────────────────────────────────
        // 内部ステート・キャッシュ
        // ──────────────────────────────────────────────

        private GameObject _stageRoot;
        private GameObject _modelInstance;
        private Transform _rigRoot;
        private Camera _subCamera;
        private RenderTexture _renderTexture;
        private Canvas _uiCanvas;
        private RectTransform _uiRootRect;
        private Image _uiBackdropImage;
        private Image _uiBorderFrameImage;
        private RawImage _uiRawImage;
        private Text _uiStateLabelText;

        private Camera _modifiedMainCamera;
        private int _lastExcludedLayer = -1;
        private bool _hasExcludedLayer;

        private readonly List<Renderer> _cachedRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, Material[]> _originalMaterialsMap = new Dictionary<Renderer, Material[]>();
        private Material _runtimeHologramMaterial;
        private UIHologramPattern _lastRuntimeHoloPattern = (UIHologramPattern)(-1);
        private bool _lastRuntimeHoloPulseDisabled = false;
        private Material _runtimeUIRawImageMaterial;
        private MaterialPropertyBlock _propBlock;

        private readonly Dictionary<string, Transform> _boneByPath = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _boneByName = new Dictionary<string, Transform>();
        private readonly List<Transform> _allRigBones = new List<Transform>();
        private readonly Dictionary<Transform, Quaternion> _currentSmoothedRotations = new Dictionary<Transform, Quaternion>();

        private PlayerUIState _currentState = PlayerUIState.Normal;
        private PlayerUIState _pendingNormalSubState = PlayerUIState.Normal;
        private float _lastNormalSubStateSwitchTime = -10f;
        private float _nextAutoFindTime = 0f;

        private Color _currentMainColor = new Color(0.0f, 2.2f, 3.2f, 0.80f);
        private Color _currentRimColor = new Color(0.4f, 4.0f, 4.8f, 0.95f);
        private Color _currentAccentColor = new Color(0.2f, 4.2f, 3.8f, 1.00f);

        private Vector3 _currentModelRootRotOffset = Vector3.zero;
        private Vector3 _currentModelRootPosOffset = Vector3.zero;

        private float _eventFlashTimer = 0f;
        private Color _eventFlashColor = Color.white;
        private float _eventScalePunch = 0f;

        private bool _wasStunnedLastFrame = false;
        private bool _wasHoloColorOverridden = false;
        private Bounds _initialModelLocalBounds;
        private bool _hasValidModelBounds = false;

        private float _editorTransitionRemainingTime = 0f;

        // シェーダープロパティIDキャッシュ
        private static readonly int ShaderProp_BaseColor        = Shader.PropertyToID("_BaseColor");
        private static readonly int ShaderProp_Color            = Shader.PropertyToID("_Color");
        private static readonly int ShaderProp_EmissionColor    = Shader.PropertyToID("_EmissionColor");
        private static readonly int ShaderProp_MainColor        = Shader.PropertyToID("_MainColor");
        private static readonly int ShaderProp_RimColor         = Shader.PropertyToID("_RimColor");
        private static readonly int ShaderProp_HexWireColor     = Shader.PropertyToID("_HexWireColor");
        private static readonly int ShaderProp_GridColor        = Shader.PropertyToID("_GridColor");
        private static readonly int ShaderProp_ScanPlaneColor   = Shader.PropertyToID("_ScanPlaneColor");
        private static readonly int ShaderProp_GlitchGlowColor  = Shader.PropertyToID("_GlitchGlowColor");
        private static readonly int ShaderProp_SubColor         = Shader.PropertyToID("_SubColor");
        private static readonly int ShaderProp_EmitterColor     = Shader.PropertyToID("_EmitterColor");
        private static readonly int ShaderProp_EdgeColor        = Shader.PropertyToID("_EdgeColor");
        private static readonly int ShaderProp_DotColor         = Shader.PropertyToID("_DotColor");
        private static readonly int ShaderProp_PatternColor     = Shader.PropertyToID("_PatternColor");
        private static readonly int ShaderProp_BottomColor      = Shader.PropertyToID("_BottomColor");
        private static readonly int ShaderProp_TopColor         = Shader.PropertyToID("_TopColor");
        private static readonly int ShaderProp_FlowColor        = Shader.PropertyToID("_FlowColor");
        private static readonly int ShaderProp_ZTest            = Shader.PropertyToID("_ZTest");
        private static readonly int ShaderProp_UseWorldSpaceScan = Shader.PropertyToID("_UseWorldSpaceScan");
        private static readonly int ShaderProp_UseWorldSpace    = Shader.PropertyToID("_UseWorldSpace");
        private static readonly int ShaderProp_UseWorldSpaceHex = Shader.PropertyToID("_UseWorldSpaceHex");
        private static readonly int ShaderProp_EnableBreathePulse = Shader.PropertyToID("_EnableBreathePulse");
        private static readonly int ShaderProp_BreatheAmount    = Shader.PropertyToID("_BreatheAmount");
        private static readonly int ShaderProp_BreatheSpeed     = Shader.PropertyToID("_BreatheSpeed");

        // ──────────────────────────────────────────────
        // 公開プロパティ
        // ──────────────────────────────────────────────

        public bool HasValidSetup => _stageRoot != null && _modelInstance != null;
        public PlayerUIState CurrentState => _currentState;

        public bool UseManualTestMode
        {
            get => _useManualTestMode;
            set => _useManualTestMode = value;
        }

        public PlayerUIState ManualTestState
        {
            get => _manualTestState;
            set => _manualTestState = value;
        }

        public float ManualTestSteering
        {
            get => _manualTestSteering;
            set => _manualTestSteering = value;
        }

        public PlayerUIState SelectedEditState
        {
            get => _selectedEditState;
            set => _selectedEditState = value;
        }

        public int UIModelLayer
        {
            get => _uiModelLayer;
            set
            {
                int clamped = Mathf.Clamp(value, 0, 31);
                if (_uiModelLayer != clamped)
                {
                    _uiModelLayer = clamped;
                    UpdateUILayoutAndCamera();
                }
            }
        }

        public bool EnableEventFlash
        {
            get => _enableEventFlash;
            set => _enableEventFlash = value;
        }

        public bool ShowStateLabel
        {
            get => _showStateLabel;
            set
            {
                _showStateLabel = value;
                UpdateUILayoutAndCamera();
            }
        }

        public MaterialDisplayMode MaterialMode
        {
            get => _materialMode;
            set
            {
                _materialMode = value;
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public bool ShowUIBackdrop
        {
            get => _showUIBackdrop;
            set
            {
                _showUIBackdrop = value;
                if (_uiBackdropImage != null) _uiBackdropImage.enabled = value;
            }
        }

        public Color UIBackdropColor
        {
            get => _uiBackdropColor;
            set
            {
                _uiBackdropColor = value;
                if (_uiBackdropImage != null) _uiBackdropImage.color = value;
            }
        }

        public bool ShowUIBorderFrame
        {
            get => _showUIBorderFrame;
            set
            {
                _showUIBorderFrame = value;
                if (_uiBorderFrameImage != null) _uiBorderFrameImage.enabled = value;
            }
        }

        public bool DynamicBorderColor
        {
            get => _dynamicBorderColor;
            set => _dynamicBorderColor = value;
        }

        public Color UIBorderStaticColor
        {
            get => _uiBorderStaticColor;
            set => _uiBorderStaticColor = value;
        }

        public float UIBorderAlpha
        {
            get => _uiBorderAlpha;
            set => _uiBorderAlpha = value;
        }

        public Color CameraBackgroundColor
        {
            get => _cameraBackgroundColor;
            set
            {
                _cameraBackgroundColor = value;
                if (_subCamera != null) _subCamera.backgroundColor = value;
            }
        }

        public UIHologramPattern HologramPattern
        {
            get => _hologramPattern;
            set
            {
                _hologramPattern = value;
                _hologramStyle = (ModelHologramController.ModelHologramStyle)Mathf.Clamp((int)value, 0, 6);
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public ModelHologramController.ModelHologramStyle HologramStyle
        {
            get => (ModelHologramController.ModelHologramStyle)Mathf.Clamp((int)_hologramPattern, 0, 6);
            set => HologramPattern = (UIHologramPattern)(int)value;
        }

        public bool EnableStateColorOverride
        {
            get => _enableStateColorOverride;
            set
            {
                _enableStateColorOverride = value;
                if (!value && _materialMode == MaterialDisplayMode.HologramShader && _runtimeHologramMaterial != null)
                {
                    RestoreHologramOriginalColors(_runtimeHologramMaterial, _hologramPattern);
                    _wasHoloColorOverridden = false;
                }
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public bool TintOriginalMaterialOnState
        {
            get => _tintOriginalMaterialOnState;
            set
            {
                _tintOriginalMaterialOnState = value;
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public float OriginalMaterialTintIntensity
        {
            get => _originalMaterialTintIntensity;
            set
            {
                _originalMaterialTintIntensity = Mathf.Clamp01(value);
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public bool SyncHologramColorsWithState
        {
            get => _syncHologramColorsWithState;
            set
            {
                _syncHologramColorsWithState = value;
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        /// <summary>
        /// 状態（スタン、シールド/ガード、ブースト等）によるモデルの強制色変化・ティントのON/OFFを一括設定します。
        /// falseに設定するとスタン時やガード時でも元の色を完全に維持します。
        /// </summary>
        public void SetStateColorOverride(bool enabled)
        {
            EnableStateColorOverride = enabled;
        }

        public bool UsePremultipliedUIAlpha
        {
            get => _usePremultipliedUIAlpha;
            set
            {
                _usePremultipliedUIAlpha = value;
                UpdateUIRawImageMaterial();
            }
        }

        public bool DisableGhostRimPulse
        {
            get => _disableGhostRimPulse;
            set
            {
                if (_disableGhostRimPulse != value || _runtimeHologramMaterial == null)
                {
                    _disableGhostRimPulse = value;
                    if (_hologramPattern == UIHologramPattern.GhostRim)
                    {
                        EnsureRuntimeHologramMaterial();
                        if (_runtimeHologramMaterial != null)
                        {
                            ApplyGhostRimPulseSetting(_runtimeHologramMaterial);
                        }
                    }
                }
            }
        }

        public void ApplyGhostRimPulseSetting(Material mat)
        {
            if (mat == null) return;
            if (_disableGhostRimPulse)
            {
                if (mat.HasProperty(ShaderProp_EnableBreathePulse)) mat.SetFloat(ShaderProp_EnableBreathePulse, 0f);
                if (mat.HasProperty(ShaderProp_BreatheAmount)) mat.SetFloat(ShaderProp_BreatheAmount, 0f);
                if (mat.HasProperty(ShaderProp_BreatheSpeed)) mat.SetFloat(ShaderProp_BreatheSpeed, 0f);
            }
            else
            {
                if (mat.HasProperty(ShaderProp_EnableBreathePulse)) mat.SetFloat(ShaderProp_EnableBreathePulse, 1f);
                if (mat.HasProperty(ShaderProp_BreatheAmount)) mat.SetFloat(ShaderProp_BreatheAmount, 0.28f);
                if (mat.HasProperty(ShaderProp_BreatheSpeed)) mat.SetFloat(ShaderProp_BreatheSpeed, 1.8f);
            }
        }

        /// <summary>
        /// 指定したホログラムパターンを適用し、表示モードをホログラムシェーダーに切り替えます。
        /// </summary>
        public void ApplyHologramPattern(UIHologramPattern pattern)
        {
            _materialMode = MaterialDisplayMode.HologramShader;
            HologramPattern = pattern;
        }

        /// <summary>
        /// 任意のマテリアルアセットをモデル全スロットへ割り当て、表示モードをカスタムマテリアルに切り替えます。
        /// 割り当てられたマテリアルが既知のホログラムパターンと合致する場合はホログラム番号も自動同期します。
        /// </summary>
        public void AssignMaterialAsset(Material mat)
        {
            if (mat == null) return;
            _materialMode = MaterialDisplayMode.CustomMaterial;
            _customMaterial = mat;

            string matName = mat.name;
            for (int i = 0; i <= (int)UIHologramPattern.UI_GlintPulseRadar; i++)
            {
                UIHologramPattern p = (UIHologramPattern)i;
                string pName = p.ToString();
                string fName = GetHologramMaterialFileName(p);
                if (matName.Contains(pName) || matName.Contains(fName))
                {
                    _hologramPattern = p;
                    _hologramStyle = (ModelHologramController.ModelHologramStyle)Mathf.Clamp((int)p, 0, 6);
                    if (p == UIHologramPattern.GhostRim)
                    {
                        bool isStatic = matName.Contains("Static") || matName.Contains("NoPulse") ||
                                        (mat.HasProperty(ShaderProp_EnableBreathePulse) && mat.GetFloat(ShaderProp_EnableBreathePulse) < 0.5f) ||
                                        (mat.HasProperty(ShaderProp_BreatheAmount) && mat.GetFloat(ShaderProp_BreatheAmount) < 0.001f);
                        _disableGhostRimPulse = isStatic;
                    }
                    break;
                }
            }

            ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
        }

        /// <summary>
        /// 現在プレビュー中のホログラムパターンを本採用（確定）として永続設定します。
        /// 対応するUIマテリアルアセットをCustomMaterialに割り当て、シーン保存で確実に維持できるようにします。
        /// </summary>
        public void AdoptCurrentHologram(bool useAsCustomMaterial = false)
        {
#if UNITY_EDITOR
            Material matAsset = null;
            if (useAsCustomMaterial && _customMaterial != null)
            {
                matAsset = _customMaterial;
            }
            else
            {
                string assetPath = (_hologramPattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                    ? GetGhostRimMaterialAssetPath(true)
                    : GetHologramMaterialAssetPath(_hologramPattern);
                matAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (matAsset == null && (int)_hologramPattern < 11)
                {
                    // UI用が見つからない場合は既存モデルホログラム側を検索
                    string existingPath = (_hologramPattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                        ? GetModelGhostRimMaterialAssetPath(true)
                        : $"Assets/Deverop/AT_Murayama/Hologram/Materials/{GetExistingModelHologramFileName((int)_hologramPattern)}.mat";
                    matAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(existingPath);
                }
            }

            if (matAsset != null)
            {
                _customMaterial = matAsset;
            }

            _materialMode = useAsCustomMaterial ? MaterialDisplayMode.CustomMaterial : MaterialDisplayMode.HologramShader;

            UnityEditor.EditorUtility.SetDirty(this);
            ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            Debug.Log($"[PlayerPoseStatusUI] ホログラムパターン【 {_hologramPattern} 】（アセット: {(matAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(matAsset) : "Runtime")}）を本採用（表示モード: {_materialMode}）として確定しました。");
#else
            _materialMode = useAsCustomMaterial ? MaterialDisplayMode.CustomMaterial : MaterialDisplayMode.HologramShader;
            ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
#endif
        }

        /// <summary>
        /// コンポーネント追加時またはインスペクターのコンテキストメニュー「Reset」時に03シーン推奨設定を適用します。
        /// </summary>
        private void Reset()
        {
            Apply03SceneDefaults();
        }

        /// <summary>
        /// 03検証シーン推奨の設定（背景完全透過・枠線なし・サブカメラ透過クリア・Premultiplied Alpha ON・390x450）を一発適用します。
        /// </summary>
        public void Apply03SceneDefaults()
        {
            _showUIBackdrop = true;
            _uiBackdropColor = new Color(0.02f, 0.08f, 0.14f, 0.0f);
            _showUIBorderFrame = false;
            _uiBorderAlpha = 0.0f;
            _uiBorderStaticColor = new Color(1f, 1f, 1f, 0f);
            _cameraBackgroundColor = new Color(0f, 0f, 0f, 0f);
            _usePremultipliedUIAlpha = true;
            _uiSize = new Vector2(390f, 450f);
            if (_screenCorner == ScreenCorner.BottomCenter)
            {
                _uiMargin = new Vector2(0f, 28f);
            }
            else
            {
                _screenCorner = ScreenCorner.BottomRight;
                _uiMargin = new Vector2(28f, 28f);
            }
            _modelViewYaw = 165f;
            _cameraPitch = 6f;
            _cameraDistanceMultiplier = 0.95f;
            _cameraHeightOffset = 0.05f;

            if (_stageRoot != null && _modelInstance != null)
            {
                UpdateUIRawImageMaterial();
                UpdateUILayoutAndCamera();
            }
        }

        public ScreenCorner ScreenCornerPosition
        {
            get => _screenCorner;
            set
            {
                _screenCorner = value;
                UpdateUILayoutAndCamera();
            }
        }

        public Vector2 UISize
        {
            get => _uiSize;
            set
            {
                _uiSize = value;
                UpdateUILayoutAndCamera();
            }
        }

        public Vector2 UIMargin
        {
            get => _uiMargin;
            set
            {
                _uiMargin = value;
                UpdateUILayoutAndCamera();
            }
        }

        public Vector2 CustomAnchor
        {
            get => _customAnchor;
            set
            {
                _customAnchor = value;
                UpdateUILayoutAndCamera();
            }
        }

        public Vector2 CustomPivot
        {
            get => _customPivot;
            set
            {
                _customPivot = value;
                UpdateUILayoutAndCamera();
            }
        }

        public UIDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                UpdateUILayoutAndCamera();
            }
        }

        public float ModelViewYaw
        {
            get => _modelViewYaw;
            set
            {
                _modelViewYaw = value;
                UpdateUILayoutAndCamera();
            }
        }

        public float CameraPitch
        {
            get => _cameraPitch;
            set
            {
                _cameraPitch = value;
                UpdateUILayoutAndCamera();
            }
        }

        public float CameraDistanceMultiplier
        {
            get => _cameraDistanceMultiplier;
            set
            {
                _cameraDistanceMultiplier = value;
                UpdateUILayoutAndCamera();
            }
        }

        public float CameraHeightOffset
        {
            get => _cameraHeightOffset;
            set
            {
                _cameraHeightOffset = value;
                UpdateUILayoutAndCamera();
            }
        }

        public float ModeBForwardDistance
        {
            get => _modeBForwardDistance;
            set
            {
                _modeBForwardDistance = Mathf.Clamp(value, 0.4f, 3.0f);
                UpdateUILayoutAndCamera();
            }
        }

        public float ModeBMiniatureScale
        {
            get => _modeBMiniatureScale;
            set
            {
                _modeBMiniatureScale = Mathf.Clamp(value, 0.01f, 0.5f);
                UpdateUILayoutAndCamera();
            }
        }

        public void SetScreenCornerPreset(ScreenCorner corner, bool resetHorizontalMargin = true)
        {
            _screenCorner = corner;
            if (resetHorizontalMargin)
            {
                if (corner == ScreenCorner.BottomCenter || corner == ScreenCorner.TopCenter || corner == ScreenCorner.Center)
                {
                    _uiMargin.x = 0f;
                }
                else if (_uiMargin.x == 0f)
                {
                    _uiMargin.x = 28f;
                }
            }
            UpdateUILayoutAndCamera();
        }

        public Material CustomMaterial
        {
            get => _customMaterial;
            set
            {
                _customMaterial = value;
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        public GameObject ModelInstance => _modelInstance;

        public PlayerPosePresetData PosePresetAsset
        {
            get => _posePresetAsset;
            set => _posePresetAsset = value;
        }

        [SerializeField, HideInInspector] private string _customSaveName = "";
        public string CustomSaveName
        {
            get => _customSaveName;
            set => _customSaveName = value;
        }

        // ──────────────────────────────────────────────
        // ライフサイクル
        // ──────────────────────────────────────────────

        private void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RestoreMainCameraCullingMask;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += RestoreMainCameraCullingMask;
#endif
            _nextAutoFindTime = 0f;
            AutoFindReferencesIfNeeded();
            SubscribePlayerEvents();
            EnsureStageAndUIReady();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RestoreMainCameraCullingMask;
#endif
            UnsubscribePlayerEvents();
            RestoreMainCameraCullingMask();
            CleanupGeneratedObjects();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RestoreMainCameraCullingMask;
#endif
            UnsubscribePlayerEvents();
            RestoreMainCameraCullingMask();
            CleanupGeneratedObjects();
        }

        private void OnValidate()
        {
            if (this == null) return;

            _uiModelLayer = Mathf.Clamp(_uiModelLayer, 0, 31);
            _hologramStyle = (ModelHologramController.ModelHologramStyle)Mathf.Clamp((int)_hologramPattern, 0, 6);

            // インスペクター変更時に即座にUIレイアウト・カメラ・マテリアル・色を更新
            if (_stageRoot != null && _modelInstance != null)
            {
                UpdateUIRawImageMaterial();
                UpdateUILayoutAndCamera();
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            if (_eventFlashTimer > 0f)
            {
                _eventFlashTimer = Mathf.Max(0f, _eventFlashTimer - dt * 2.8f);
            }
            if (_eventScalePunch > 0f)
            {
                _eventScalePunch = Mathf.Max(0f, _eventScalePunch - dt * 3.5f);
            }

            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                if (IsUserEditingBoneInSceneView())
                {
                    return;
                }

                if (_editorTransitionRemainingTime > 0f)
                {
                    _editorTransitionRemainingTime -= dt;
                }
                else
                {
                    return;
                }
#else
                return;
#endif
            }

            if (_stageRoot == null || _modelInstance == null)
            {
                EnsureStageAndUIReady();
                if (_stageRoot == null) return;
            }

            // レース開始時にプレイヤー機が後から動的生成（Instantiate）された場合でも自動的に検知・接続する
            if (Application.isPlaying && !_useManualTestMode && (_playerManager == null || _flightController == null))
            {
                float now = Time.unscaledTime;
                if (now >= _nextAutoFindTime)
                {
                    _nextAutoFindTime = now + 0.5f;
                    AutoFindReferencesIfNeeded();
                }
            }

            PlayerUIState targetState;
            float steering;

            if (_useManualTestMode)
            {
                targetState = _manualTestState;
                steering = _manualTestSteering;
            }
            else
            {
                steering = EvaluateCurrentPlayerState(out targetState);
            }

            _currentState = targetState;

            UpdatePoseAndColors(targetState, steering, dt);
            UpdateUILayoutAndCamera();
        }

        private void LateUpdate()
        {
            // 方式B（メインカメラ前3Dミニチュア）のとき、CameraController（LateUpdate）の移動が完了した後に
            // ステージ位置を再同期することで、カメラ移動に対する1フレーム遅延のガタつき（ジッター）を完全に防止する
            if (_displayMode == UIDisplayMode.ModeB_CameraForeground3D && _stageRoot != null && _modelInstance != null)
            {
                UpdateUILayoutAndCamera();
            }
        }

#if UNITY_EDITOR
        private bool IsUserEditingBoneInSceneView()
        {
            GameObject selected = UnityEditor.Selection.activeGameObject;
            if (selected == null || _modelInstance == null) return false;
            return selected.transform.IsChildOf(_modelInstance.transform);
        }

        public void StartEditorTransitionPreview(float duration = 0.8f)
        {
            _editorTransitionRemainingTime = duration;
        }
#endif

        // ──────────────────────────────────────────────
        // 1. 状態判定＆イベント監視（既存ロジック・安全維持）
        // ──────────────────────────────────────────────

        private void AutoFindReferencesIfNeeded()
        {
            if (_playerManager == null)
            {
                _playerManager = FindAnyObjectByType<PlayerManager>();
                if (_playerManager != null)
                {
                    SubscribePlayerEvents();
                }
            }

            if (_flightController == null)
            {
                _flightController = FindAnyObjectByType<PlayerFlightController>();
            }

            if (_characterModelPrefab == null)
            {
#if UNITY_EDITOR
                string defaultPath = "Assets/TeamResources/Art/Models/born model.fbx";
                _characterModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath);
                if (_characterModelPrefab == null)
                {
                    _characterModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deverop/AT_Murayama/born model.fbx");
                }
#endif
            }
        }

        private void SubscribePlayerEvents()
        {
            if (_playerManager == null) return;

            UnsubscribePlayerEvents();

            _playerManager.BoostStarted += OnBoostStarted;
            _playerManager.BoostEnded   += OnBoostEnded;
            _playerManager.ShieldStarted += OnShieldStarted;
            _playerManager.ShieldEnded   += OnShieldEnded;
            _playerManager.ShieldBlocked += OnShieldBlocked;
        }

        private void UnsubscribePlayerEvents()
        {
            if (_playerManager == null) return;

            _playerManager.BoostStarted -= OnBoostStarted;
            _playerManager.BoostEnded   -= OnBoostEnded;
            _playerManager.ShieldStarted -= OnShieldStarted;
            _playerManager.ShieldEnded   -= OnShieldEnded;
            _playerManager.ShieldBlocked -= OnShieldBlocked;
        }

        private void OnBoostStarted()
        {
            TriggerEventFlash(new Color(4.5f, 2.2f, 0.5f, 1.0f), 1.0f, 0.12f);
        }

        private void OnBoostEnded()
        {
            TriggerEventFlash(new Color(0.2f, 1.8f, 2.5f, 1.0f), 0.5f, 0.05f);
        }

        private void OnShieldStarted()
        {
            TriggerEventFlash(new Color(0.5f, 4.8f, 2.5f, 1.0f), 0.9f, 0.10f);
        }

        private void OnShieldEnded()
        {
            TriggerEventFlash(new Color(0.2f, 1.8f, 2.5f, 1.0f), 0.5f, 0.05f);
        }

        private void OnShieldBlocked()
        {
            TriggerEventFlash(new Color(3.5f, 6.0f, 4.5f, 1.0f), 1.5f, 0.22f);
        }

        public void TriggerEventFlash(Color flashColor, float intensity, float punchScale)
        {
            if (!_enableEventFlash) return;
            _eventFlashColor = flashColor;
            _eventFlashTimer = Mathf.Max(_eventFlashTimer, intensity);
            _eventScalePunch = Mathf.Max(_eventScalePunch, punchScale);
#if UNITY_EDITOR
            StartEditorTransitionPreview(0.6f);
#endif
        }

        private float EvaluateCurrentPlayerState(out PlayerUIState resolvedState)
        {
            float steering = 0f;
            if (_flightController != null)
            {
                steering = Mathf.Clamp(_flightController.Steering, -1f, 1f);
            }

            if (_playerManager == null)
            {
                resolvedState = PlayerUIState.Normal;
                return steering;
            }

            bool isStunned = _playerManager.IsStunned;

            // スタンの瞬間検知（スタンのイベントは無いためポーリング比較で瞬間演出を発火）
            if (isStunned && !_wasStunnedLastFrame)
            {
                TriggerEventFlash(new Color(5.0f, 0.6f, 0.6f, 1.0f), 1.35f, 0.16f);
            }
            _wasStunnedLastFrame = isStunned;

            // 優先度1（最優先）：スタン
            if (isStunned)
            {
                resolvedState = PlayerUIState.Stunned;
                return steering;
            }

            // 優先度2：ブースト
            if (_playerManager.IsBoosting)
            {
                resolvedState = PlayerUIState.Boosting;
                return steering;
            }

            // 優先度3：シールド
            if (_playerManager.IsShielding)
            {
                resolvedState = PlayerUIState.Shielding;
                return steering;
            }

            // 優先度4：通常（Steeringに応じて左・右・中央）
            if (_steeringMode == SteeringPoseMode.DiscreteStateSwitch)
            {
                PlayerUIState desiredSubState = PlayerUIState.Normal;
                if (steering <= -_steeringThreshold)
                {
                    desiredSubState = PlayerUIState.MoveLeft;
                }
                else if (steering >= _steeringThreshold)
                {
                    desiredSubState = PlayerUIState.MoveRight;
                }

                // トラッキング境界での微細なチャタリング防止デバウンス
                float now = Application.isPlaying ? Time.unscaledTime : (float)
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.timeSinceStartup
#else
                    0f
#endif
                    ;
                if (desiredSubState != _pendingNormalSubState)
                {
                    _pendingNormalSubState = desiredSubState;
                    _lastNormalSubStateSwitchTime = now;
                }

                if (now - _lastNormalSubStateSwitchTime >= _stateChangeDebounceTime)
                {
                    resolvedState = _pendingNormalSubState;
                }
                else
                {
                    resolvedState = (_currentState == PlayerUIState.Normal ||
                                     _currentState == PlayerUIState.MoveLeft ||
                                     _currentState == PlayerUIState.MoveRight)
                        ? _currentState
                        : desiredSubState;
                }
                return steering;
            }

            resolvedState = PlayerUIState.Normal;
            return steering;
        }

        // ──────────────────────────────────────────────
        // 2. ポーズ補間（Slerp）＆ 色・マテリアル更新
        // ──────────────────────────────────────────────

        private void UpdatePoseAndColors(PlayerUIState targetState, float steering, float dt)
        {
            EnsureDefaultPosesInitialized();

            StatePoseEntry primaryEntry = GetPoseEntry(targetState);
            if (primaryEntry == null) return;

            float speed = Mathf.Max(1f, primaryEntry.transitionSpeed * _globalTransitionSpeedMultiplier);
            float tLerp = 1f - Mathf.Exp(-speed * dt);

            // ContinuousBlend モードかつ Normal 状態の場合は、Steering量に応じて MoveLeft / MoveRight と連続ブレンド
            StatePoseEntry secondaryEntry = null;
            float secondaryBlend = 0f;
            if (_steeringMode == SteeringPoseMode.ContinuousBlend && targetState == PlayerUIState.Normal && Mathf.Abs(steering) > 0.01f)
            {
                secondaryEntry = GetPoseEntry(steering < 0f ? PlayerUIState.MoveLeft : PlayerUIState.MoveRight);
                secondaryBlend = Mathf.Clamp01(Mathf.Abs(steering));
            }

            float time = Application.isPlaying ? Time.time : (float)
#if UNITY_EDITOR
                UnityEditor.EditorApplication.timeSinceStartup
#else
                0f
#endif
                ;

            // 各ボーンの回転を Slerp で滑らかに更新
            int count = primaryEntry.boneSnapshots.Count;
            for (int i = 0; i < count; i++)
            {
                BoneTransformSnapshot snap = primaryEntry.boneSnapshots[i];
                Transform bone = FindBoneTransform(snap.bonePath, snap.boneName);
                if (bone == null) continue;

                Quaternion targetRot = snap.localRotation;

                // ContinuousBlend 時の左右ブレンド
                if (secondaryEntry != null && i < secondaryEntry.boneSnapshots.Count)
                {
                    targetRot = Quaternion.Slerp(targetRot, secondaryEntry.boneSnapshots[i].localRotation, secondaryBlend);
                }

                // アイドル呼吸揺れ・スタン時の揺れ加算
                if (_enableIdleBreathing && _idleBreathingAmplitude > 0.001f)
                {
                    if (snap.boneName == "spine_C0_1_jnt" || snap.boneName == "spine_C0_2_jnt")
                    {
                        float breathPitch = Mathf.Sin(time * 2.4f) * 1.8f * _idleBreathingAmplitude;
                        targetRot = targetRot * Quaternion.Euler(breathPitch, 0f, 0f);
                    }
                }

                if (targetState == PlayerUIState.Stunned && _enableStunWobbleAndFlicker)
                {
                    if (snap.boneName == "spine_C0_2_jnt" || snap.boneName == "neck_C0_0_jnt")
                    {
                        float wobbleX = Mathf.Sin(time * 8.5f) * 7.5f;
                        float wobbleZ = Mathf.Cos(time * 6.2f) * 9.0f;
                        targetRot = targetRot * Quaternion.Euler(wobbleX, 0f, wobbleZ);
                    }
                }

                if (!_currentSmoothedRotations.TryGetValue(bone, out Quaternion currentRot))
                {
                    currentRot = bone.localRotation;
                }

                Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, tLerp);
                bone.localRotation = nextRot;
                _currentSmoothedRotations[bone] = nextRot;
            }

            // モデルルートの回転・位置オフセット補間
            Vector3 targetRootRotOffset = primaryEntry.modelRootRotationOffset;
            Vector3 targetRootPosOffset = primaryEntry.modelRootPositionOffset;
            if (secondaryEntry != null)
            {
                targetRootRotOffset = Vector3.Lerp(targetRootRotOffset, secondaryEntry.modelRootRotationOffset, secondaryBlend);
                targetRootPosOffset = Vector3.Lerp(targetRootPosOffset, secondaryEntry.modelRootPositionOffset, secondaryBlend);
            }

            _currentModelRootRotOffset = Vector3.Lerp(_currentModelRootRotOffset, targetRootRotOffset, tLerp);
            _currentModelRootPosOffset = Vector3.Lerp(_currentModelRootPosOffset, targetRootPosOffset, tLerp);

            // 発光色の補間
            Color targetMain = primaryEntry.mainHoloColor;
            Color targetRim = primaryEntry.rimGlowColor;
            Color targetAccent = primaryEntry.accentColor;

            if (secondaryEntry != null)
            {
                targetMain = Color.Lerp(targetMain, secondaryEntry.mainHoloColor, secondaryBlend);
                targetRim = Color.Lerp(targetRim, secondaryEntry.rimGlowColor, secondaryBlend);
                targetAccent = Color.Lerp(targetAccent, secondaryEntry.accentColor, secondaryBlend);
            }

            // スタン中の警告点滅（状態カラー変更が有効な場合のみ）
            bool shouldOverrideColor = _enableStateColorOverride && (_materialMode == MaterialDisplayMode.OriginalMaterial ? _tintOriginalMaterialOnState : _syncHologramColorsWithState);
            if (targetState == PlayerUIState.Stunned && _enableStunWobbleAndFlicker && shouldOverrideColor)
            {
                float blink = 0.55f + 0.45f * Mathf.Sin(time * 18.0f);
                targetMain *= blink;
                targetRim *= (0.7f + 0.5f * blink);
            }

            _currentMainColor = Color.Lerp(_currentMainColor, targetMain, tLerp);
            _currentRimColor = Color.Lerp(_currentRimColor, targetRim, tLerp);
            _currentAccentColor = Color.Lerp(_currentAccentColor, targetAccent, tLerp);

            // 切り替わり瞬間フラッシュの加算
            Color finalMain = _currentMainColor;
            Color finalRim = _currentRimColor;
            if (_enableEventFlash && _eventFlashTimer > 0.001f && shouldOverrideColor)
            {
                float flashWeight = _eventFlashTimer * _eventFlashIntensity * 0.5f;
                finalMain += _eventFlashColor * flashWeight;
                finalRim += _eventFlashColor * flashWeight;
            }

            // 全Renderer・全マテリアルスロットへ適用
            ApplyMaterialsAndColors(finalMain, finalRim, _currentAccentColor);

            // UIラベル更新
            if (_uiStateLabelText != null)
            {
                _uiStateLabelText.text = primaryEntry.displayLabel;
                Color labelCol = finalRim;
                float maxComp = Mathf.Max(1f, Mathf.Max(labelCol.r, Mathf.Max(labelCol.g, labelCol.b)));
                labelCol.r /= maxComp;
                labelCol.g /= maxComp;
                labelCol.b /= maxComp;
                labelCol.a = 0.95f;
                _uiStateLabelText.color = labelCol;
            }

            // UI外枠（ボーダー）更新
            if (_uiBorderFrameImage != null)
            {
                _uiBorderFrameImage.enabled = _showUIBorderFrame;
                Color borderCol = _dynamicBorderColor ? finalRim : _uiBorderStaticColor;
                float maxComp = Mathf.Max(1f, Mathf.Max(borderCol.r, Mathf.Max(borderCol.g, borderCol.b)));
                borderCol.r /= maxComp;
                borderCol.g /= maxComp;
                borderCol.b /= maxComp;
                borderCol.a = _showUIBorderFrame ? _uiBorderAlpha : 0f;
                _uiBorderFrameImage.color = borderCol;
            }

            // UI背景バックプレート更新
            if (_uiBackdropImage != null)
            {
                _uiBackdropImage.enabled = _showUIBackdrop;
                _uiBackdropImage.color = _uiBackdropColor;
            }
        }

        // ──────────────────────────────────────────────
        // 3. 全Renderer・全スロット対応マテリアル管理
        // ──────────────────────────────────────────────

        private void CacheModelRenderersAndOriginalMaterials()
        {
            _cachedRenderers.Clear();
            _originalMaterialsMap.Clear();

            if (_modelInstance == null) return;

            Renderer[] renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }

                _cachedRenderers.Add(r);

                Material[] mats = r.sharedMaterials;
                Material[] cloned = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    cloned[i] = mats[i];
                }
                _originalMaterialsMap[r] = cloned;
            }
        }

        private void ApplyMaterialsAndColors(Color mainCol, Color rimCol, Color accentCol)
        {
            if (_cachedRenderers.Count == 0 && _modelInstance != null)
            {
                CacheModelRenderersAndOriginalMaterials();
            }
            if (_cachedRenderers.Count == 0) return;

            switch (_materialMode)
            {
                case MaterialDisplayMode.OriginalMaterial:
                    ApplyOriginalMaterialMode(mainCol, rimCol);
                    break;

                case MaterialDisplayMode.HologramShader:
                    ApplyHologramShaderMode(mainCol, rimCol, accentCol);
                    break;

                case MaterialDisplayMode.CustomMaterial:
                    ApplyCustomMaterialMode();
                    break;
            }
        }

        private void ApplyOriginalMaterialMode(Color mainCol, Color rimCol)
        {
            // 1. 各Rendererの全スロットを本来の通常マテリアルに戻す
            foreach (Renderer rend in _cachedRenderers)
            {
                if (rend == null) continue;
                if (_originalMaterialsMap.TryGetValue(rend, out Material[] origMats) && origMats != null)
                {
                    rend.sharedMaterials = origMats;
                }
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            // 2. 状態ティントがONなら MaterialPropertyBlock で安全に色・発光をブレンド
            bool shouldTint = _enableStateColorOverride && _tintOriginalMaterialOnState && (_originalMaterialTintIntensity > 0.001f);
            if (shouldTint)
            {
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

                Color tint = Color.Lerp(Color.white, mainCol, _originalMaterialTintIntensity);
                Color emission = rimCol * (_originalMaterialTintIntensity * 0.7f);

                foreach (Renderer rend in _cachedRenderers)
                {
                    if (rend == null) continue;
                    rend.GetPropertyBlock(_propBlock);
                    _propBlock.SetColor(ShaderProp_BaseColor, tint);
                    _propBlock.SetColor(ShaderProp_Color, tint);
                    _propBlock.SetColor(ShaderProp_EmissionColor, emission);
                    rend.SetPropertyBlock(_propBlock);
                }
            }
            else
            {
                // ティントOFFならプロパティブロックを解除
                foreach (Renderer rend in _cachedRenderers)
                {
                    if (rend == null) continue;
                    rend.SetPropertyBlock(null);
                }
            }
        }

        private void ApplyHologramShaderMode(Color mainCol, Color rimCol, Color accentCol)
        {
            EnsureRuntimeHologramMaterial();
            if (_runtimeHologramMaterial == null) return;

            UpdateHologramMaterialProperties(_runtimeHologramMaterial, mainCol, rimCol, accentCol);

            foreach (Renderer rend in _cachedRenderers)
            {
                if (rend == null) continue;
                int slotCount = rend.sharedMaterials != null ? rend.sharedMaterials.Length : 1;
                if (slotCount <= 0) slotCount = 1;

                // 全スロットにホログラムマテリアルが割り当たっているか確認し、必要なら一括適用
                Material[] current = rend.sharedMaterials;
                bool allMatch = (current != null && current.Length == slotCount);
                if (allMatch)
                {
                    for (int i = 0; i < slotCount; i++)
                    {
                        if (current[i] != _runtimeHologramMaterial)
                        {
                            allMatch = false;
                            break;
                        }
                    }
                }

                if (!allMatch)
                {
                    Material[] newMats = new Material[slotCount];
                    for (int i = 0; i < slotCount; i++)
                    {
                        newMats[i] = _runtimeHologramMaterial;
                    }
                    rend.sharedMaterials = newMats;
                }

                rend.SetPropertyBlock(null);
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        private void ApplyCustomMaterialMode()
        {
            if (_customMaterial == null) return;

            foreach (Renderer rend in _cachedRenderers)
            {
                if (rend == null) continue;
                int slotCount = rend.sharedMaterials != null ? rend.sharedMaterials.Length : 1;
                if (slotCount <= 0) slotCount = 1;

                Material[] current = rend.sharedMaterials;
                bool allMatch = (current != null && current.Length == slotCount);
                if (allMatch)
                {
                    for (int i = 0; i < slotCount; i++)
                    {
                        if (current[i] != _customMaterial)
                        {
                            allMatch = false;
                            break;
                        }
                    }
                }

                if (!allMatch)
                {
                    Material[] newMats = new Material[slotCount];
                    for (int i = 0; i < slotCount; i++)
                    {
                        newMats[i] = _customMaterial;
                    }
                    rend.sharedMaterials = newMats;
                }

                rend.SetPropertyBlock(null);
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        private void EnsureRuntimeHologramMaterial()
        {
            string assetPath = (_hologramPattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                ? GetGhostRimMaterialAssetPath(true)
                : GetHologramMaterialAssetPath(_hologramPattern);
            Material sourceMat = null;
#if UNITY_EDITOR
            sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (sourceMat == null && (int)_hologramPattern < 11)
            {
                string existingPath = (_hologramPattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                    ? GetModelGhostRimMaterialAssetPath(true)
                    : $"Assets/Deverop/AT_Murayama/Hologram/Materials/{GetExistingModelHologramFileName((int)_hologramPattern)}.mat";
                sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(existingPath);
            }
#endif
            string shaderPath = GetHologramShaderPath(_hologramPattern);
            Shader shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                shader = Shader.Find("Custom/Hologram/ModelHologram");
            }

            bool pulseSettingChanged = (_hologramPattern == UIHologramPattern.GhostRim && _lastRuntimeHoloPulseDisabled != _disableGhostRimPulse);

            if (_runtimeHologramMaterial == null || _lastRuntimeHoloPattern != _hologramPattern || pulseSettingChanged || (_runtimeHologramMaterial.shader != shader && sourceMat == null))
            {
                if (_runtimeHologramMaterial != null)
                {
                    DestroySafe(_runtimeHologramMaterial);
                }

                if (sourceMat != null)
                {
                    _runtimeHologramMaterial = Instantiate(sourceMat);
                    _runtimeHologramMaterial.name = $"M_RuntimePoseHologram_{_hologramPattern}";
                    _runtimeHologramMaterial.hideFlags = HideFlags.DontSave;
                }
                else if (shader != null)
                {
                    _runtimeHologramMaterial = new Material(shader)
                    {
                        name = $"M_RuntimePoseHologram_{_hologramPattern}",
                        hideFlags = HideFlags.DontSave
                    };
                }

                if (_runtimeHologramMaterial != null)
                {
                    ConfigurePatternSpecificDefaults(_runtimeHologramMaterial, _hologramPattern, false, _disableGhostRimPulse);
                    if (_hologramPattern == UIHologramPattern.GhostRim)
                    {
                        ApplyGhostRimPulseSetting(_runtimeHologramMaterial);
                    }
                    _lastRuntimeHoloPattern = _hologramPattern;
                    _lastRuntimeHoloPulseDisabled = _disableGhostRimPulse;
                }
            }
        }

        public static string GetExistingModelHologramFileName(int index)
        {
            switch (index)
            {
                case 0: return "M_Model_01_ClassicScanline";
                case 1: return "M_Model_02_HexShield";
                case 2: return "M_Model_03_WireframeGrid";
                case 3: return "M_Model_04_CyberGlitch";
                case 4: return "M_Model_05_HoloLEDMatrix";
                case 5: return "M_Model_06_HoloPatternBanner";
                case 6: return "M_Model_07_HoloProjectorBeam";
                case 7: return "M_Model_08_GhostRim";
                case 8: return "M_Model_09_Dissolve";
                case 9: return "M_Model_10_TriplanarDots";
                case 10: return "M_Model_11_GradientEnergy";
                default: return "M_Model_01_ClassicScanline";
            }
        }

        public static string GetHologramMaterialFileName(UIHologramPattern pattern)
        {
            switch (pattern)
            {
                case UIHologramPattern.ClassicScanline: return "M_UIHolo_01_ClassicScanline";
                case UIHologramPattern.HexShield: return "M_UIHolo_02_HexShield";
                case UIHologramPattern.WireframeGrid: return "M_UIHolo_03_WireframeGrid";
                case UIHologramPattern.CyberGlitch: return "M_UIHolo_04_CyberGlitch";
                case UIHologramPattern.LEDMatrix: return "M_UIHolo_05_HoloLEDMatrix";
                case UIHologramPattern.PatternBanner: return "M_UIHolo_06_HoloPatternBanner";
                case UIHologramPattern.ProjectorBeam: return "M_UIHolo_07_HoloProjectorBeam";
                case UIHologramPattern.GhostRim: return "M_UIHolo_08_GhostRim";
                case UIHologramPattern.Dissolve: return "M_UIHolo_09_Dissolve";
                case UIHologramPattern.TriplanarDots: return "M_UIHolo_10_TriplanarDots";
                case UIHologramPattern.GradientEnergy: return "M_UIHolo_11_GradientEnergy";
                case UIHologramPattern.UI_CyberNeon: return "M_UIHolo_12_CyberNeon";
                case UIHologramPattern.UI_QuantumLattice: return "M_UIHolo_13_QuantumLattice";
                case UIHologramPattern.UI_TacticalGlitch: return "M_UIHolo_14_TacticalGlitch";
                case UIHologramPattern.UI_AuroraPlasma: return "M_UIHolo_15_AuroraPlasma";
                case UIHologramPattern.UI_GlintPulseRadar: return "M_UIHolo_16_GlintPulseRadar";
                default: return "M_UIHolo_01_ClassicScanline";
            }
        }

        public static string GetHologramMaterialAssetPath(UIHologramPattern pattern)
        {
            return $"Assets/Deverop/AT_Murayama/PlayerStatusUI/Materials/{GetHologramMaterialFileName(pattern)}.mat";
        }

        public static string GetGhostRimMaterialAssetPath(bool isStatic = false)
        {
            return isStatic
                ? "Assets/Deverop/AT_Murayama/PlayerStatusUI/Materials/M_UIHolo_08_GhostRim_Static.mat"
                : "Assets/Deverop/AT_Murayama/PlayerStatusUI/Materials/M_UIHolo_08_GhostRim.mat";
        }

        public static string GetModelGhostRimMaterialAssetPath(bool isStatic = false)
        {
            return isStatic
                ? "Assets/Deverop/AT_Murayama/Hologram/Materials/M_Model_08_GhostRim_Static.mat"
                : "Assets/Deverop/AT_Murayama/Hologram/Materials/M_Model_08_GhostRim.mat";
        }

        public static string GetHologramShaderPath(UIHologramPattern pattern)
        {
            switch (pattern)
            {
                case UIHologramPattern.ClassicScanline:
                case UIHologramPattern.UI_CyberNeon:
                    return "Custom/Hologram/ModelHologram";
                case UIHologramPattern.HexShield:
                    return "Custom/Hologram/ModelHologram_HexGrid";
                case UIHologramPattern.WireframeGrid:
                case UIHologramPattern.UI_QuantumLattice:
                    return "Custom/Hologram/ModelHologram_WireframeGrid";
                case UIHologramPattern.CyberGlitch:
                case UIHologramPattern.UI_TacticalGlitch:
                    return "Custom/Hologram/ModelHologram_CyberGlitch";
                case UIHologramPattern.LEDMatrix:
                    return "Custom/Hologram/ModelHologram_LEDMatrix";
                case UIHologramPattern.PatternBanner:
                    return "Custom/Hologram/ModelHologram_PatternBanner";
                case UIHologramPattern.ProjectorBeam:
                    return "Custom/Hologram/ModelHologram_ProjectorBeam";
                case UIHologramPattern.GhostRim:
                case UIHologramPattern.UI_GlintPulseRadar:
                    return "Custom/Hologram/ModelHologram_GhostRim";
                case UIHologramPattern.Dissolve:
                    return "Custom/Hologram/ModelHologram_Dissolve";
                case UIHologramPattern.TriplanarDots:
                    return "Custom/Hologram/ModelHologram_TriplanarDots";
                case UIHologramPattern.GradientEnergy:
                case UIHologramPattern.UI_AuroraPlasma:
                    return "Custom/Hologram/ModelHologram_GradientEnergy";
                default:
                    return "Custom/Hologram/ModelHologram";
            }
        }

        public static void ConfigurePatternSpecificDefaults(Material mat, UIHologramPattern pattern, bool setColorsIfDefault = true, bool disableGhostRimPulse = false)
        {
            if (mat == null) return;

            // ローカル座標基準（高速移動時の走査線ストロボ防止）
            if (mat.HasProperty(ShaderProp_UseWorldSpaceScan)) mat.SetFloat(ShaderProp_UseWorldSpaceScan, 0f);
            if (mat.HasProperty(ShaderProp_UseWorldSpace)) mat.SetFloat(ShaderProp_UseWorldSpace, 0f);
            if (mat.HasProperty(ShaderProp_UseWorldSpaceHex)) mat.SetFloat(ShaderProp_UseWorldSpaceHex, 0f);

            bool ShouldSetColor(int propId)
            {
                if (!mat.HasProperty(propId)) return false;
                if (setColorsIfDefault) return true;
                Color cur = mat.GetColor(propId);
                return (cur.r < 0.01f && cur.g < 0.01f && cur.b < 0.01f);
            }

            switch (pattern)
            {
                case UIHologramPattern.ClassicScanline:
                    if (mat.HasProperty("_ScanlineDensity")) mat.SetFloat("_ScanlineDensity", 30f);
                    if (mat.HasProperty("_ScanlineSpeed")) mat.SetFloat("_ScanlineSpeed", 1.6f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.6f);
                    if (mat.HasProperty("_CenterAlpha")) mat.SetFloat("_CenterAlpha", 0.22f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.HexShield:
                    if (mat.HasProperty("_HexScale")) mat.SetFloat("_HexScale", 14f);
                    if (mat.HasProperty("_HexPulseSpeed")) mat.SetFloat("_HexPulseSpeed", 2.8f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.3f);
                    if (mat.HasProperty("_CenterAlpha")) mat.SetFloat("_CenterAlpha", 0.2f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_HexWireColor)) mat.SetColor(ShaderProp_HexWireColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.WireframeGrid:
                    if (mat.HasProperty("_VerticalGridDensity")) mat.SetFloat("_VerticalGridDensity", 20f);
                    if (mat.HasProperty("_VerticalGridIntensity")) mat.SetFloat("_VerticalGridIntensity", 0.7f);
                    if (mat.HasProperty("_ContourInterval")) mat.SetFloat("_ContourInterval", 0.45f);
                    if (mat.HasProperty("_ContourLineWidth")) mat.SetFloat("_ContourLineWidth", 0.045f);
                    if (mat.HasProperty("_ScanSliceSpeed")) mat.SetFloat("_ScanSliceSpeed", 1.4f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_GridColor)) mat.SetColor(ShaderProp_GridColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_ScanPlaneColor)) mat.SetColor(ShaderProp_ScanPlaneColor, new Color(0.4f, 0.95f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.CyberGlitch:
                    if (mat.HasProperty("_ChromaticSplit")) mat.SetFloat("_ChromaticSplit", 0.03f);
                    if (mat.HasProperty("_SyncRollSpeed")) mat.SetFloat("_SyncRollSpeed", 0.85f);
                    if (mat.HasProperty("_SyncRollIntensity")) mat.SetFloat("_SyncRollIntensity", 0.45f);
                    if (mat.HasProperty("_VertexGlitchIntensity")) mat.SetFloat("_VertexGlitchIntensity", 0.08f);
                    if (mat.HasProperty("_VertexGlitchRate")) mat.SetFloat("_VertexGlitchRate", 0.25f);
                    if (mat.HasProperty("_SliceDensity")) mat.SetFloat("_SliceDensity", 14f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_GlitchGlowColor)) mat.SetColor(ShaderProp_GlitchGlowColor, new Color(0.3f, 0.9f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.LEDMatrix:
                    if (mat.HasProperty("_PatternMode")) mat.SetFloat("_PatternMode", 2f);
                    mat.EnableKeyword("_PATTERNMODE_CYBERARROWS");
                    if (mat.HasProperty("_PixelateTexToDots")) mat.SetFloat("_PixelateTexToDots", 1f);
                    mat.EnableKeyword("_PIXELATETEXTODOTS_ON");
                    if (mat.HasProperty("_DotRadius")) mat.SetFloat("_DotRadius", 0.35f);
                    if (mat.HasProperty("_SparkleSpeed")) mat.SetFloat("_SparkleSpeed", 4f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_DotColor)) mat.SetColor(ShaderProp_DotColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.PatternBanner:
                    if (mat.HasProperty("_PatternMode")) mat.SetFloat("_PatternMode", 2f);
                    mat.EnableKeyword("_PATTERNMODE_CYBERARROWS");
                    if (mat.HasProperty("_PatternScrollSpeed")) mat.SetFloat("_PatternScrollSpeed", 1.8f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_PatternColor)) mat.SetColor(ShaderProp_PatternColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.ProjectorBeam:
                    if (mat.HasProperty("_BeamDensity")) mat.SetFloat("_BeamDensity", 24f);
                    if (mat.HasProperty("_BeamIntensity")) mat.SetFloat("_BeamIntensity", 0.7f);
                    if (mat.HasProperty("_BeamSpeed")) mat.SetFloat("_BeamSpeed", 3.5f);
                    if (mat.HasProperty("_EmitterGlowHeight")) mat.SetFloat("_EmitterGlowHeight", 0.4f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_EmitterColor)) mat.SetColor(ShaderProp_EmitterColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.GhostRim:
                    if (setColorsIfDefault)
                    {
                        if (disableGhostRimPulse)
                        {
                            if (mat.HasProperty(ShaderProp_BreatheSpeed)) mat.SetFloat(ShaderProp_BreatheSpeed, 0f);
                            if (mat.HasProperty(ShaderProp_BreatheAmount)) mat.SetFloat(ShaderProp_BreatheAmount, 0f);
                            if (mat.HasProperty(ShaderProp_EnableBreathePulse)) mat.SetFloat(ShaderProp_EnableBreathePulse, 0f);
                        }
                        else
                        {
                            if (mat.HasProperty(ShaderProp_BreatheSpeed)) mat.SetFloat(ShaderProp_BreatheSpeed, 1.8f);
                            if (mat.HasProperty(ShaderProp_BreatheAmount)) mat.SetFloat(ShaderProp_BreatheAmount, 0.28f);
                            if (mat.HasProperty(ShaderProp_EnableBreathePulse)) mat.SetFloat(ShaderProp_EnableBreathePulse, 1.0f);
                        }
                        if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.4f);
                    }
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.Dissolve:
                    if (mat.HasProperty("_AutoLoopSpeed")) mat.SetFloat("_AutoLoopSpeed", 0.35f);
                    if (mat.HasProperty("_DirectionMode")) mat.SetFloat("_DirectionMode", 1f);
                    mat.EnableKeyword("_DIRECTIONMODE_BOTTOMTOTOP");
                    if (mat.HasProperty("_DirectionBlend")) mat.SetFloat("_DirectionBlend", 0.65f);
                    if (mat.HasProperty("_EdgeWidth")) mat.SetFloat("_EdgeWidth", 0.09f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_EdgeColor)) mat.SetColor(ShaderProp_EdgeColor, new Color(0.3f, 0.95f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.TriplanarDots:
                    if (mat.HasProperty("_DotScale")) mat.SetFloat("_DotScale", 8.0f);
                    if (mat.HasProperty("_DotRadius")) mat.SetFloat("_DotRadius", 0.24f);
                    if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 1.8f);
                    if (mat.HasProperty("_WaveDotBoost")) mat.SetFloat("_WaveDotBoost", 0.65f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.65f, 0.95f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_DotColor)) mat.SetColor(ShaderProp_DotColor, new Color(0.2f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.GradientEnergy:
                    if (mat.HasProperty("_FlowScale")) mat.SetFloat("_FlowScale", 3.0f);
                    if (mat.HasProperty("_FlowSpeed")) mat.SetFloat("_FlowSpeed", 0.9f);
                    if (mat.HasProperty("_FlowContrast")) mat.SetFloat("_FlowContrast", 3.2f);
                    if (mat.HasProperty("_FlowIntensity")) mat.SetFloat("_FlowIntensity", 1.0f);
                    if (ShouldSetColor(ShaderProp_BottomColor)) mat.SetColor(ShaderProp_BottomColor, new Color(0.05f, 0.5f, 0.9f, 0.8f));
                    if (ShouldSetColor(ShaderProp_TopColor)) mat.SetColor(ShaderProp_TopColor, new Color(0.2f, 0.85f, 1.0f, 0.9f));
                    if (ShouldSetColor(ShaderProp_FlowColor)) mat.SetColor(ShaderProp_FlowColor, new Color(0.3f, 0.95f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.35f, 0.85f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.UI_CyberNeon:
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 3.8f);
                    if (mat.HasProperty("_CenterAlpha")) mat.SetFloat("_CenterAlpha", 0.2f);
                    if (mat.HasProperty("_ScanlineDensity")) mat.SetFloat("_ScanlineDensity", 36f);
                    if (mat.HasProperty("_ScanlineSpeed")) mat.SetFloat("_ScanlineSpeed", 2.0f);
                    if (mat.HasProperty("_SweepIntensity")) mat.SetFloat("_SweepIntensity", 1.25f);
                    if (mat.HasProperty("_GlitchFrequency")) mat.SetFloat("_GlitchFrequency", 0.06f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.45f, 0.9f, 0.85f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.75f, 0.05f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.UI_QuantumLattice:
                    if (mat.HasProperty("_VerticalGridDensity")) mat.SetFloat("_VerticalGridDensity", 32f);
                    if (mat.HasProperty("_VerticalGridIntensity")) mat.SetFloat("_VerticalGridIntensity", 0.85f);
                    if (mat.HasProperty("_ContourInterval")) mat.SetFloat("_ContourInterval", 0.35f);
                    if (mat.HasProperty("_ContourLineWidth")) mat.SetFloat("_ContourLineWidth", 0.05f);
                    if (mat.HasProperty("_ScanSliceSpeed")) mat.SetFloat("_ScanSliceSpeed", 1.8f);
                    if (mat.HasProperty("_ScanSliceThickness")) mat.SetFloat("_ScanSliceThickness", 0.16f);
                    if (mat.HasProperty("_ScanSliceRange")) mat.SetFloat("_ScanSliceRange", 2.5f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.8f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0f, 0.6f, 0.9f, 0.75f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0f, 0.9f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_GridColor)) mat.SetColor(ShaderProp_GridColor, new Color(0f, 0.85f, 1.0f, 1.0f));
                    if (ShouldSetColor(ShaderProp_ScanPlaneColor)) mat.SetColor(ShaderProp_ScanPlaneColor, new Color(0.2f, 0.95f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.UI_TacticalGlitch:
                    if (mat.HasProperty("_ChromaticSplit")) mat.SetFloat("_ChromaticSplit", 0.035f);
                    if (mat.HasProperty("_SyncRollSpeed")) mat.SetFloat("_SyncRollSpeed", 0.75f);
                    if (mat.HasProperty("_SyncRollIntensity")) mat.SetFloat("_SyncRollIntensity", 0.35f);
                    if (mat.HasProperty("_VertexGlitchIntensity")) mat.SetFloat("_VertexGlitchIntensity", 0.07f);
                    if (mat.HasProperty("_VertexGlitchRate")) mat.SetFloat("_VertexGlitchRate", 0.2f);
                    if (mat.HasProperty("_SliceDensity")) mat.SetFloat("_SliceDensity", 16f);
                    if (mat.HasProperty("_StaticNoiseIntensity")) mat.SetFloat("_StaticNoiseIntensity", 0.25f);
                    if (mat.HasProperty("_FlickerDropRate")) mat.SetFloat("_FlickerDropRate", 0.1f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.7f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0.05f, 0.75f, 0.65f, 0.85f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.2f, 0.95f, 0.85f, 1.0f));
                    if (ShouldSetColor(ShaderProp_GlitchGlowColor)) mat.SetColor(ShaderProp_GlitchGlowColor, new Color(1.0f, 0.35f, 0.15f, 1.0f));
                    break;

                case UIHologramPattern.UI_AuroraPlasma:
                    if (mat.HasProperty("_FlowScale")) mat.SetFloat("_FlowScale", 3.6f);
                    if (mat.HasProperty("_FlowSpeed")) mat.SetFloat("_FlowSpeed", 1.2f);
                    if (mat.HasProperty("_FlowContrast")) mat.SetFloat("_FlowContrast", 3.5f);
                    if (mat.HasProperty("_FlowIntensity")) mat.SetFloat("_FlowIntensity", 1.25f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.5f);
                    if (ShouldSetColor(ShaderProp_BottomColor)) mat.SetColor(ShaderProp_BottomColor, new Color(0.0f, 0.8f, 0.8f, 0.9f));
                    if (ShouldSetColor(ShaderProp_TopColor)) mat.SetColor(ShaderProp_TopColor, new Color(0.85f, 0.1f, 0.85f, 0.9f));
                    if (ShouldSetColor(ShaderProp_FlowColor)) mat.SetColor(ShaderProp_FlowColor, new Color(0.2f, 0.95f, 0.85f, 1.0f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.7f, 0.3f, 1.0f, 1.0f));
                    break;

                case UIHologramPattern.UI_GlintPulseRadar:
                    if (mat.HasProperty("_BreatheSpeed")) mat.SetFloat("_BreatheSpeed", 2.6f);
                    if (mat.HasProperty("_BreatheAmount")) mat.SetFloat("_BreatheAmount", 0.45f);
                    if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 3.2f);
                    if (mat.HasProperty("_CenterAlpha")) mat.SetFloat("_CenterAlpha", 0.16f);
                    if (ShouldSetColor(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, new Color(0.0f, 0.75f, 0.95f, 0.8f));
                    if (ShouldSetColor(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, new Color(0.3f, 0.95f, 1.0f, 1.0f));
                    break;
            }
        }

        private void UpdateHologramMaterialProperties(Material mat, Color mainCol, Color rimCol, Color accentCol)
        {
            if (mat == null) return;

            bool shouldSyncHoloColors = _enableStateColorOverride && _syncHologramColorsWithState;

            if (shouldSyncHoloColors)
            {
                // 通常状態（Normal / MoveLeft / MoveRight）以外のイベント状態（Boosting, Shielding, Stunned）の時のみ
                // 強烈な状態カラーでマテリアル色をダイナミックに上書き
                bool isEventActiveState = (_currentState == PlayerUIState.Boosting ||
                                           _currentState == PlayerUIState.Shielding ||
                                           _currentState == PlayerUIState.Stunned);

                if (_useManualTestMode)
                {
                    isEventActiveState = (_manualTestState == PlayerUIState.Boosting ||
                                          _manualTestState == PlayerUIState.Shielding ||
                                          _manualTestState == PlayerUIState.Stunned);
                }

                if (isEventActiveState)
                {
                    if (mat.HasProperty(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, mainCol);
                    if (mat.HasProperty(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, rimCol);
                    if (mat.HasProperty(ShaderProp_HexWireColor)) mat.SetColor(ShaderProp_HexWireColor, accentCol);
                    if (mat.HasProperty(ShaderProp_GridColor)) mat.SetColor(ShaderProp_GridColor, accentCol);
                    if (mat.HasProperty(ShaderProp_ScanPlaneColor)) mat.SetColor(ShaderProp_ScanPlaneColor, accentCol);
                    if (mat.HasProperty(ShaderProp_GlitchGlowColor)) mat.SetColor(ShaderProp_GlitchGlowColor, accentCol);
                    if (mat.HasProperty(ShaderProp_SubColor)) mat.SetColor(ShaderProp_SubColor, accentCol * 0.35f);
                    if (mat.HasProperty(ShaderProp_EmitterColor)) mat.SetColor(ShaderProp_EmitterColor, accentCol);
                    if (mat.HasProperty(ShaderProp_EdgeColor)) mat.SetColor(ShaderProp_EdgeColor, accentCol);
                    if (mat.HasProperty(ShaderProp_DotColor)) mat.SetColor(ShaderProp_DotColor, accentCol);
                    if (mat.HasProperty(ShaderProp_PatternColor)) mat.SetColor(ShaderProp_PatternColor, accentCol);
                    if (mat.HasProperty(ShaderProp_BottomColor)) mat.SetColor(ShaderProp_BottomColor, mainCol);
                    if (mat.HasProperty(ShaderProp_TopColor)) mat.SetColor(ShaderProp_TopColor, accentCol);
                    if (mat.HasProperty(ShaderProp_FlowColor)) mat.SetColor(ShaderProp_FlowColor, rimCol);
                    _wasHoloColorOverridden = true;
                }
                else
                {
                    // 通常状態に戻った場合、前回イベントで上書きされていれば元のホログラム固有色に復元
                    if (_wasHoloColorOverridden)
                    {
                        RestoreHologramOriginalColors(mat, _hologramPattern);
                        _wasHoloColorOverridden = false;
                    }
                    else if (mat.HasProperty(ShaderProp_MainColor))
                    {
                        Color c = mat.GetColor(ShaderProp_MainColor);
                        if (c.r < 0.01f && c.g < 0.01f && c.b < 0.01f) mat.SetColor(ShaderProp_MainColor, mainCol);
                    }
                }
            }
            else
            {
                // 状態色連動OFFの場合：もし前回上書き状態のままなら直ちに本来のホログラム配色に復元
                if (_wasHoloColorOverridden)
                {
                    RestoreHologramOriginalColors(mat, _hologramPattern);
                    _wasHoloColorOverridden = false;
                }
            }

            // 方式Bでカメラと一緒に高速移動してもスキャンラインがストロボ点滅しないようローカル空間基準にする
            if (mat.HasProperty(ShaderProp_UseWorldSpaceScan)) mat.SetFloat(ShaderProp_UseWorldSpaceScan, 0f);
            if (mat.HasProperty(ShaderProp_UseWorldSpace)) mat.SetFloat(ShaderProp_UseWorldSpace, 0f);
            if (mat.HasProperty(ShaderProp_UseWorldSpaceHex)) mat.SetFloat(ShaderProp_UseWorldSpaceHex, 0f);

            // GhostRim選択時はパルス無効化設定を常に確実に同期
            if (_hologramPattern == UIHologramPattern.GhostRim)
            {
                ApplyGhostRimPulseSetting(mat);
            }

            // 方式Bにおける壁・地形めり込み防止用の ZTest Always 切り替え
            bool useAlwaysZ = (_displayMode == UIDisplayMode.ModeB_CameraForeground3D && _alwaysOnTopInModeB);
            if (mat.HasProperty(ShaderProp_ZTest))
            {
                mat.SetFloat(ShaderProp_ZTest, useAlwaysZ ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            }
            mat.renderQueue = useAlwaysZ ? 3990 : -1;
        }

        /// <summary>
        /// ホログラムマテリアルの配色をディスク上のアセット原本（または既定パターンカラー）から完全復元します。
        /// アーティストがマテリアルアセット上で微調整した配色を100%維持します。
        /// </summary>
        private void RestoreHologramOriginalColors(Material mat, UIHologramPattern pattern)
        {
            if (mat == null) return;
            Material sourceMat = null;
#if UNITY_EDITOR
            string assetPath = (pattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                ? GetGhostRimMaterialAssetPath(true)
                : GetHologramMaterialAssetPath(pattern);
            sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (sourceMat == null && (int)pattern < 11)
            {
                string existingPath = (pattern == UIHologramPattern.GhostRim && _disableGhostRimPulse)
                    ? GetModelGhostRimMaterialAssetPath(true)
                    : $"Assets/Deverop/AT_Murayama/Hologram/Materials/{GetExistingModelHologramFileName((int)pattern)}.mat";
                sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(existingPath);
            }
#endif
            if (sourceMat != null)
            {
                mat.CopyPropertiesFromMaterial(sourceMat);
            }
            else
            {
                ConfigurePatternSpecificDefaults(mat, pattern, true, _disableGhostRimPulse);
            }

            if (pattern == UIHologramPattern.GhostRim)
            {
                ApplyGhostRimPulseSetting(mat);
            }
        }

        // ──────────────────────────────────────────────
        // 4. ステージ・サブカメラ・RenderTexture・Canvas UI 構築
        // ──────────────────────────────────────────────

        private void EnsureStageAndUIReady()
        {
            if (_stageRoot == null || _modelInstance == null)
            {
                RebuildStageAndUI();
            }
        }

        public void RebuildStageAndUI()
        {
            CleanupGeneratedObjects();
            AutoFindReferencesIfNeeded();

            if (_characterModelPrefab == null)
            {
                return;
            }

            // 1. ステージルート生成
            _stageRoot = new GameObject("PlayerPoseUI_StageRoot (AutoGenerated)")
            {
                hideFlags = HideFlags.DontSave
            };
            _stageRoot.transform.SetParent(transform, false);
            _stageRoot.transform.position = new Vector3(0f, -550f, 0f);
            _stageRoot.transform.rotation = Quaternion.identity;

            // 2. キャラクターモデルのインスタンス化
            _modelInstance = Instantiate(_characterModelPrefab, _stageRoot.transform);
            _modelInstance.name = "PlayerPoseModel_Instance";
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.Euler(0f, _modelViewYaw, 0f);
            _modelInstance.transform.localScale = Vector3.one;

            // 不要な Animator が付いていて Transform をロックしてしまうのを防ぐため無効化
            Animator anim = _modelInstance.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
            }

            // 全Rendererおよび通常マテリアルのキャッシュ
            CacheModelRenderersAndOriginalMaterials();

            // 3. 全ボーンのキャッシュ＆初期バインドポーズ（Tポーズ）の記録
            CacheBoneHierarchy();
            MeasureModelBounds();

            if (_bindPoseSnapshots == null || _bindPoseSnapshots.Count == 0)
            {
                CaptureCurrentPoseToList(_bindPoseSnapshots);
            }
            if (_posePresetAsset != null && (_posePresetAsset.bindPoseSnapshots == null || _posePresetAsset.bindPoseSnapshots.Count == 0))
            {
                CaptureCurrentPoseToList(_posePresetAsset.bindPoseSnapshots);
            }

            // 4. 初期ポーズが未生成なら 6状態のサンプルポーズを自動生成
            EnsureDefaultPosesInitialized();

            // 5. サブカメラ＆RenderTexture＆Canvas UI の生成
            SetupCameraAndCanvasUI();
            UpdateUILayoutAndCamera();

            // 初回ポーズ・マテリアルを即時適用
            PreviewPoseImmediate(_useManualTestMode ? _manualTestState : _currentState);
        }

        private void SetupCameraAndCanvasUI()
        {
            // 1. RenderTexture 生成
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGBHalf)
                {
                    name = "RT_PlayerPoseStatusUI",
                    antiAliasing = 2,
                    hideFlags = HideFlags.DontSave
                };
                _renderTexture.Create();
            }

            // 2. サブカメラ生成
            GameObject camObj = new GameObject("PlayerPoseUI_SubCamera")
            {
                hideFlags = HideFlags.DontSave
            };
            camObj.transform.SetParent(_stageRoot.transform, false);
            _subCamera = camObj.AddComponent<Camera>();
            _subCamera.clearFlags = CameraClearFlags.SolidColor;
            _subCamera.backgroundColor = _cameraBackgroundColor;
            _subCamera.cullingMask = 1 << _uiModelLayer;
            _subCamera.fieldOfView = 30f;
            _subCamera.nearClipPlane = 0.05f;
            _subCamera.farClipPlane = 50f;
            _subCamera.depth = -10f;
            _subCamera.targetTexture = _renderTexture;

            UniversalAdditionalCameraData urpCamData = camObj.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
            {
                urpCamData = camObj.AddComponent<UniversalAdditionalCameraData>();
            }
            urpCamData.renderPostProcessing = false;
            urpCamData.renderShadows = false;
            urpCamData.requiresDepthTexture = false;
            urpCamData.requiresColorTexture = false;

            // 3. Screen Space Overlay Canvas 生成
            GameObject canvasObj = new GameObject("PlayerPoseUI_OverlayCanvas")
            {
                hideFlags = HideFlags.DontSave
            };
            canvasObj.transform.SetParent(transform, false);
            _uiCanvas = canvasObj.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 110;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 4. UIルートコンテナ
            GameObject containerObj = new GameObject("StatusMonitorContainer");
            containerObj.transform.SetParent(canvasObj.transform, false);
            _uiRootRect = containerObj.AddComponent<RectTransform>();

            // 5. 外枠ボーダー（外側・Image）
            GameObject borderObj = new GameObject("BorderFrame");
            borderObj.transform.SetParent(containerObj.transform, false);
            _uiBorderFrameImage = borderObj.AddComponent<Image>();
            _uiBorderFrameImage.raycastTarget = false;
            _uiBorderFrameImage.color = _uiBorderStaticColor;
            RectTransform borderRect = _uiBorderFrameImage.rectTransform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            // 6. 背景バックプレート（内側・Image：外枠より2px小さくしてボーダー線を表現）
            GameObject backdropObj = new GameObject("BackdropPanel");
            backdropObj.transform.SetParent(containerObj.transform, false);
            _uiBackdropImage = backdropObj.AddComponent<Image>();
            _uiBackdropImage.raycastTarget = false;
            _uiBackdropImage.color = _uiBackdropColor;
            RectTransform bgRect = _uiBackdropImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(2f, 2f);
            bgRect.offsetMax = new Vector2(-2f, -2f);

            // 7. キャラクター表示用 RawImage
            GameObject rawImgObj = new GameObject("CharacterPoseRawImage");
            rawImgObj.transform.SetParent(containerObj.transform, false);
            _uiRawImage = rawImgObj.AddComponent<RawImage>();
            _uiRawImage.texture = _renderTexture;
            _uiRawImage.raycastTarget = false;
            UpdateUIRawImageMaterial();
            RectTransform rawRect = _uiRawImage.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = new Vector2(4f, _showStateLabel ? 24f : 4f);
            rawRect.offsetMax = new Vector2(-4f, -4f);

            // 8. 状態名テキスト（NORMAL / BOOST!! / SHIELD / STUNNED!）
            GameObject labelObj = new GameObject("StateLabelText");
            labelObj.transform.SetParent(containerObj.transform, false);
            _uiStateLabelText = labelObj.AddComponent<Text>();
            _uiStateLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _uiStateLabelText.fontSize = 18;
            _uiStateLabelText.fontStyle = FontStyle.Bold;
            _uiStateLabelText.alignment = TextAnchor.MiddleCenter;
            _uiStateLabelText.text = "NORMAL";
            _uiStateLabelText.raycastTarget = false;
            labelObj.SetActive(_showStateLabel);

            RectTransform labelRect = _uiStateLabelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(0f, 24f);
        }

        public void UpdateUILayoutAndCamera()
        {
            if (_stageRoot == null || _modelInstance == null) return;

            bool isModeA = (_displayMode == UIDisplayMode.ModeA_RenderTextureUI);

            if (isModeA)
            {
                // 方式A：専用レイヤーを設定し、メインカメラのCullingMaskから除外
                SetLayerRecursively(_stageRoot, _uiModelLayer);
                ApplyMainCameraCullingMaskExclusion();

                _stageRoot.transform.position = new Vector3(0f, -550f, 0f);
                _stageRoot.transform.rotation = Quaternion.identity;
                _stageRoot.transform.localScale = Vector3.one;

                float scaleFactor = 1.0f + _eventScalePunch;
                _modelInstance.transform.localPosition = _currentModelRootPosOffset;
                _modelInstance.transform.localRotation =
                    Quaternion.Euler(0f, _modelViewYaw, 0f) * Quaternion.Euler(_currentModelRootRotOffset);
                _modelInstance.transform.localScale = Vector3.one * scaleFactor;

                // サブカメラ背景クリア色反映
                if (_subCamera != null)
                {
                    _subCamera.enabled = true;
                    _subCamera.backgroundColor = _cameraBackgroundColor;
                    _subCamera.cullingMask = 1 << _uiModelLayer;

                    float modelHeight = Mathf.Max(0.5f, _initialModelLocalBounds.size.y);
                    Vector3 centerLocal = _initialModelLocalBounds.center + new Vector3(0f, modelHeight * _cameraHeightOffset, 0f);

                    float halfFovRad = _subCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    float baseDist = (modelHeight * 0.58f) / Mathf.Tan(halfFovRad);
                    float dist = baseDist * _cameraDistanceMultiplier;

                    Quaternion camRot = Quaternion.Euler(_cameraPitch, 0f, 0f);
                    Vector3 camLocalPos = centerLocal - (camRot * Vector3.forward) * dist;

                    _subCamera.transform.localPosition = camLocalPos;
                    _subCamera.transform.localRotation = camRot;
                }

                if (_uiCanvas != null)
                {
                    _uiCanvas.gameObject.SetActive(true);
                    UpdateCornerRectTransform(_uiRootRect);
                }

                // 状態テキスト表示・非表示 ＆ RawImage レイアウト連動
                if (_uiStateLabelText != null && _uiStateLabelText.gameObject.activeSelf != _showStateLabel)
                {
                    _uiStateLabelText.gameObject.SetActive(_showStateLabel);
                }
                if (_uiRawImage != null)
                {
                    float bottomMargin = _showStateLabel ? 24f : 4f;
                    RectTransform rawRect = _uiRawImage.rectTransform;
                    if (Mathf.Abs(rawRect.offsetMin.y - bottomMargin) > 0.5f)
                    {
                        rawRect.offsetMin = new Vector2(4f, bottomMargin);
                        rawRect.offsetMax = new Vector2(-4f, -4f);
                    }
                }

                // 背景・枠線の表示・色更新
                if (_uiBackdropImage != null)
                {
                    _uiBackdropImage.enabled = _showUIBackdrop;
                    _uiBackdropImage.color = _uiBackdropColor;
                }
                if (_uiBorderFrameImage != null)
                {
                    _uiBorderFrameImage.enabled = _showUIBorderFrame;
                }
            }
            else
            {
                // 方式B：メインカメラ前方に直接3Dミニチュアとして配置
                RestoreMainCameraCullingMask();
                SetLayerRecursively(_stageRoot, 0);

                if (_subCamera != null) _subCamera.enabled = false;
                if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(false);

                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    float aspect = mainCam.aspect;
                    float vHeight = 2f * _modeBForwardDistance * Mathf.Tan(mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    float vWidth = vHeight * aspect;

                    float signX;
                    float signY;

                    switch (_screenCorner)
                    {
                        case ScreenCorner.BottomLeft:
                            signX = -1f;
                            signY = -1f;
                            break;
                        case ScreenCorner.TopRight:
                            signX = 1f;
                            signY = 1f;
                            break;
                        case ScreenCorner.TopLeft:
                            signX = -1f;
                            signY = 1f;
                            break;
                        case ScreenCorner.BottomCenter:
                            signX = 0f;
                            signY = -1f;
                            break;
                        case ScreenCorner.TopCenter:
                            signX = 0f;
                            signY = 1f;
                            break;
                        case ScreenCorner.Center:
                            signX = 0f;
                            signY = 0f;
                            break;
                        case ScreenCorner.Custom:
                            signX = (_customAnchor.x - 0.5f) * 2f;
                            signY = (_customAnchor.y - 0.5f) * 2f;
                            break;
                        default: // BottomRight
                            signX = 1f;
                            signY = -1f;
                            break;
                    }

                    float marginSignX = (signX > 0.01f) ? -1f : 1f;
                    float marginSignY = (signY > 0.01f) ? -1f : 1f;

                    float offsetX = signX * (vWidth * 0.34f) + (marginSignX * _uiMargin.x * 0.001f);
                    float offsetY = signY * (vHeight * 0.28f) + (marginSignY * _uiMargin.y * 0.001f) - (_initialModelLocalBounds.center.y * _modeBMiniatureScale);

                    _stageRoot.transform.position = mainCam.transform.TransformPoint(new Vector3(offsetX, offsetY, _modeBForwardDistance));
                    _stageRoot.transform.rotation = mainCam.transform.rotation;
                    _stageRoot.transform.localScale = Vector3.one * (_modeBMiniatureScale * (1.0f + _eventScalePunch));

                    _modelInstance.transform.localPosition = _currentModelRootPosOffset;
                    _modelInstance.transform.localRotation =
                        Quaternion.Euler(0f, _modelViewYaw, 0f) * Quaternion.Euler(_currentModelRootRotOffset);
                    _modelInstance.transform.localScale = Vector3.one;
                }
            }
        }

        private void UpdateCornerRectTransform(RectTransform rect)
        {
            if (rect == null) return;

            Vector2 anchor;
            Vector2 pivot;
            Vector2 pos;

            switch (_screenCorner)
            {
                case ScreenCorner.BottomLeft:
                    anchor = new Vector2(0f, 0f);
                    pivot = new Vector2(0f, 0f);
                    pos = new Vector2(_uiMargin.x, _uiMargin.y);
                    break;
                case ScreenCorner.TopRight:
                    anchor = new Vector2(1f, 1f);
                    pivot = new Vector2(1f, 1f);
                    pos = new Vector2(-_uiMargin.x, -_uiMargin.y);
                    break;
                case ScreenCorner.TopLeft:
                    anchor = new Vector2(0f, 1f);
                    pivot = new Vector2(0f, 1f);
                    pos = new Vector2(_uiMargin.x, -_uiMargin.y);
                    break;
                case ScreenCorner.BottomCenter:
                    anchor = new Vector2(0.5f, 0f);
                    pivot = new Vector2(0.5f, 0f);
                    pos = new Vector2(_uiMargin.x, _uiMargin.y);
                    break;
                case ScreenCorner.TopCenter:
                    anchor = new Vector2(0.5f, 1f);
                    pivot = new Vector2(0.5f, 1f);
                    pos = new Vector2(_uiMargin.x, -_uiMargin.y);
                    break;
                case ScreenCorner.Center:
                    anchor = new Vector2(0.5f, 0.5f);
                    pivot = new Vector2(0.5f, 0.5f);
                    pos = new Vector2(_uiMargin.x, _uiMargin.y);
                    break;
                case ScreenCorner.Custom:
                    anchor = _customAnchor;
                    pivot = _customPivot;
                    pos = new Vector2(_uiMargin.x, _uiMargin.y);
                    break;
                default: // BottomRight
                    anchor = new Vector2(1f, 0f);
                    pivot = new Vector2(1f, 0f);
                    pos = new Vector2(-_uiMargin.x, _uiMargin.y);
                    break;
            }

            if (rect.anchorMin != anchor) rect.anchorMin = anchor;
            if (rect.anchorMax != anchor) rect.anchorMax = anchor;
            if (rect.pivot != pivot) rect.pivot = pivot;
            if (rect.anchoredPosition != pos) rect.anchoredPosition = pos;
            if (rect.sizeDelta != _uiSize) rect.sizeDelta = _uiSize;
        }

        /// <summary>
        /// 方式AでメインカメラからポーズUIモデル専用レイヤーを除外する。
        /// 元々ONだった場合のみ自身が除外したフラグを記録し、復元時にピンポイントで戻す。
        /// </summary>
        private void ApplyMainCameraCullingMaskExclusion()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            if (_uiModelLayer < 0 || _uiModelLayer >= 32)
            {
                RestoreMainCameraCullingMask();
                return;
            }

            // カメラ参照や除外対象レイヤーが変更された場合は、以前除外していた旧カメラ/旧レイヤーを先に復元
            if (_modifiedMainCamera != mainCam || _lastExcludedLayer != _uiModelLayer)
            {
                RestoreMainCameraCullingMask();

                _modifiedMainCamera = mainCam;
                _lastExcludedLayer = _uiModelLayer;
                _hasExcludedLayer = false;
            }

            int layerBit = 1 << _uiModelLayer;
            if ((mainCam.cullingMask & layerBit) != 0)
            {
                mainCam.cullingMask &= ~layerBit;
                _hasExcludedLayer = true;
            }
        }

        /// <summary>
        /// 方式Aでメインカメラから除外したポーズUIモデル専用レイヤー（CullingMask）を安全に元通り復元する。
        /// 自身が除外したレイヤーのみピンポイントで元に戻し、他のレイヤー状態には影響を与えない。
        /// </summary>
        private void RestoreMainCameraCullingMask()
        {
            if (_hasExcludedLayer && _modifiedMainCamera != null && _lastExcludedLayer >= 0 && _lastExcludedLayer < 32)
            {
                _modifiedMainCamera.cullingMask |= (1 << _lastExcludedLayer);
            }
            _hasExcludedLayer = false;
            _lastExcludedLayer = -1;
            _modifiedMainCamera = null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            Transform tr = root.transform;
            int childCount = tr.childCount;
            for (int i = 0; i < childCount; i++)
            {
                SetLayerRecursively(tr.GetChild(i).gameObject, layer);
            }
        }

        private void UpdateUIRawImageMaterial()
        {
            if (_uiRawImage == null) return;

            if (_usePremultipliedUIAlpha)
            {
                if (_runtimeUIRawImageMaterial == null)
                {
                    Shader uiPremulShader = Shader.Find("Custom/UI/PremultipliedRawImage");
                    if (uiPremulShader != null)
                    {
                        _runtimeUIRawImageMaterial = new Material(uiPremulShader)
                        {
                            name = "M_Runtime_UIPremultipliedRawImage",
                            hideFlags = HideFlags.DontSave
                        };
                    }
                }
                _uiRawImage.material = _runtimeUIRawImageMaterial;
            }
            else
            {
                _uiRawImage.material = null;
            }
        }

        private void CleanupGeneratedObjects()
        {
            if (_renderTexture != null)
            {
                if (_subCamera != null) _subCamera.targetTexture = null;
                _renderTexture.Release();
                DestroySafe(_renderTexture);
                _renderTexture = null;
            }

            if (_stageRoot != null)
            {
                DestroySafe(_stageRoot);
                _stageRoot = null;
            }

            if (_uiCanvas != null)
            {
                DestroySafe(_uiCanvas.gameObject);
                _uiCanvas = null;
            }

            if (_runtimeHologramMaterial != null)
            {
                DestroySafe(_runtimeHologramMaterial);
                _runtimeHologramMaterial = null;
            }

            if (_runtimeUIRawImageMaterial != null)
            {
                DestroySafe(_runtimeUIRawImageMaterial);
                _runtimeUIRawImageMaterial = null;
            }

            // 念のため残骸オブジェクトをクリーンアップ
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && (child.name.StartsWith("PlayerPoseUI_") || child.name.StartsWith("PlayerPoseModel_")))
                {
                    DestroySafe(child.gameObject);
                }
            }

            _modelInstance = null;
            _rigRoot = null;
            _subCamera = null;
            _uiRootRect = null;
            _uiBackdropImage = null;
            _uiBorderFrameImage = null;
            _uiRawImage = null;
            _uiStateLabelText = null;
            _cachedRenderers.Clear();
            _originalMaterialsMap.Clear();
            _boneByPath.Clear();
            _boneByName.Clear();
            _allRigBones.Clear();
            _currentSmoothedRotations.Clear();
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        // ──────────────────────────────────────────────
        // 5. ボーン階層管理・ポーズ保存・初期サンプルポーズ自動生成
        // ──────────────────────────────────────────────

        private void CacheBoneHierarchy()
        {
            _boneByPath.Clear();
            _boneByName.Clear();
            _allRigBones.Clear();
            _currentSmoothedRotations.Clear();

            if (_modelInstance == null) return;

            Transform searchRoot = _modelInstance.transform.Find("mainRig");
            if (searchRoot == null) searchRoot = _modelInstance.transform;
            _rigRoot = searchRoot;

            Transform[] transforms = _rigRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                if (t == _modelInstance.transform) continue;
                string relPath = GetRelativePath(_modelInstance.transform, t);
                _boneByPath[relPath] = t;
                if (!_boneByName.ContainsKey(t.name))
                {
                    _boneByName[t.name] = t;
                }
                _allRigBones.Add(t);
            }
        }

        private void MeasureModelBounds()
        {
            if (_modelInstance == null || _hasValidModelBounds) return;

            Quaternion prevRot = _modelInstance.transform.localRotation;
            Vector3 prevPos = _modelInstance.transform.localPosition;
            _modelInstance.transform.localRotation = Quaternion.identity;
            _modelInstance.transform.localPosition = Vector3.zero;

            Renderer[] renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds worldBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    worldBounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 localCenter = _stageRoot.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = worldBounds.size;
                if (localSize.y > 0.1f)
                {
                    _initialModelLocalBounds = new Bounds(localCenter, localSize);
                    _hasValidModelBounds = true;
                }
            }

            _modelInstance.transform.localRotation = prevRot;
            _modelInstance.transform.localPosition = prevPos;
        }

        private Transform FindBoneTransform(string path, string boneName)
        {
            if (!string.IsNullOrEmpty(path) && _boneByPath.TryGetValue(path, out Transform byPath) && byPath != null)
            {
                return byPath;
            }
            if (!string.IsNullOrEmpty(boneName) && _boneByName.TryGetValue(boneName, out Transform byName) && byName != null)
            {
                return byName;
            }
            return null;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            List<string> parts = new List<string>();
            Transform curr = target;
            while (curr != null && curr != root)
            {
                parts.Add(curr.name);
                curr = curr.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private StatePoseEntry GetPoseEntry(PlayerUIState state)
        {
            List<StatePoseEntry> list = (_posePresetAsset != null && _posePresetAsset.statePoses.Count > 0)
                ? _posePresetAsset.statePoses
                : _inlineStatePoses;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].state == state)
                {
                    return list[i];
                }
            }
            return null;
        }

        public StatePoseEntry GetOrCreatePoseEntry(PlayerUIState state)
        {
            if (_posePresetAsset != null)
            {
                return _posePresetAsset.GetOrCreateEntry(state);
            }

            for (int i = 0; i < _inlineStatePoses.Count; i++)
            {
                if (_inlineStatePoses[i] != null && _inlineStatePoses[i].state == state)
                {
                    return _inlineStatePoses[i];
                }
            }

            StatePoseEntry created = PlayerPosePresetData.CreateDefaultMetadataEntry(state);
            _inlineStatePoses.Add(created);
            return created;
        }

        /// <summary>
        /// 現在のモデルのボーン回転をそのまま指定状態のポーズとして保存します（Sceneビューで調整したポーズの保存用）。
        /// </summary>
        public void CaptureCurrentModelPoseToState(PlayerUIState targetState)
        {
            if (_modelInstance == null || _allRigBones.Count == 0)
            {
                RebuildStageAndUI();
            }

            StatePoseEntry entry = GetOrCreatePoseEntry(targetState);
            CaptureCurrentPoseToList(entry.boneSnapshots);

            // ScriptableObject と内蔵リストの両方を同期
            SyncInlineAndAssetPoses();
        }

        private void CaptureCurrentPoseToList(List<BoneTransformSnapshot> targetList)
        {
            targetList.Clear();
            if (_modelInstance == null) return;

            for (int i = 0; i < _allRigBones.Count; i++)
            {
                Transform b = _allRigBones[i];
                if (b == null) continue;
                string path = GetRelativePath(_modelInstance.transform, b);
                targetList.Add(new BoneTransformSnapshot(path, b.name, b.localRotation, b.localPosition));
            }
        }

        /// <summary>
        /// モデルを初期バインドポーズ（Tポーズ）にリセットします。
        /// </summary>
        public void ResetModelToBindPose()
        {
            List<BoneTransformSnapshot> bindList = (_posePresetAsset != null && _posePresetAsset.bindPoseSnapshots.Count > 0)
                ? _posePresetAsset.bindPoseSnapshots
                : _bindPoseSnapshots;

            if (bindList == null || bindList.Count == 0) return;

            for (int i = 0; i < bindList.Count; i++)
            {
                BoneTransformSnapshot snap = bindList[i];
                Transform bone = FindBoneTransform(snap.bonePath, snap.boneName);
                if (bone != null)
                {
                    bone.localRotation = snap.localRotation;
                    bone.localPosition = snap.localPosition;
                    _currentSmoothedRotations[bone] = snap.localRotation;
                }
            }

            _currentModelRootRotOffset = Vector3.zero;
            _currentModelRootPosOffset = Vector3.zero;
            UpdateUILayoutAndCamera();
        }

        /// <summary>
        /// 指定した状態の保存済みポーズ＆発光色を即座にモデルへ反映します（確認・編集用）。
        /// </summary>
        public void PreviewPoseImmediate(PlayerUIState state)
        {
            EnsureDefaultPosesInitialized();

            StatePoseEntry entry = GetPoseEntry(state);
            if (entry == null) return;

            for (int i = 0; i < entry.boneSnapshots.Count; i++)
            {
                BoneTransformSnapshot snap = entry.boneSnapshots[i];
                Transform bone = FindBoneTransform(snap.bonePath, snap.boneName);
                if (bone != null)
                {
                    bone.localRotation = snap.localRotation;
                    _currentSmoothedRotations[bone] = snap.localRotation;
                }
            }

            _currentState = state;
            _currentMainColor = entry.mainHoloColor;
            _currentRimColor = entry.rimGlowColor;
            _currentAccentColor = entry.accentColor;
            _currentModelRootRotOffset = entry.modelRootRotationOffset;
            _currentModelRootPosOffset = entry.modelRootPositionOffset;

            ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            UpdateUILayoutAndCamera();
        }

        private void EnsureDefaultPosesInitialized()
        {
            bool needsInit = (_inlineStatePoses == null || _inlineStatePoses.Count < 6);
            if (!needsInit && _inlineStatePoses.Count > 0 && _inlineStatePoses[0].boneSnapshots.Count == 0)
            {
                needsInit = true;
            }

            if (needsInit && _modelInstance != null && _allRigBones.Count > 0)
            {
                GenerateDefaultSixStatePoses();
            }
        }

        /// <summary>
        /// アニメーション知識がなくてもワンクリックで6状態（通常・左・右・ブースト・シールド・スタン）の
        /// カッコいいポーズを Bind Pose（Tポーズ）から幾何学的に自動計算して構築します。
        /// </summary>
        public void GenerateDefaultSixStatePoses()
        {
            if (_modelInstance == null || _allRigBones.Count == 0)
            {
                CacheBoneHierarchy();
                if (_allRigBones.Count == 0) return;
            }

            if (_bindPoseSnapshots == null || _bindPoseSnapshots.Count == 0)
            {
                CaptureCurrentPoseToList(_bindPoseSnapshots);
            }

            Quaternion savedModelRot = _modelInstance.transform.rotation;
            _modelInstance.transform.rotation = Quaternion.identity;
            ResetModelToBindPose();

            Transform spine0 = FindBoneTransform(null, "spine_C0_0_jnt");
            Transform head   = FindBoneTransform(null, "neck_C0_head_jnt");
            Transform armL0  = FindBoneTransform(null, "arm_L0_0_jnt");
            Transform armR0  = FindBoneTransform(null, "arm_R0_0_jnt");
            Transform footL0 = FindBoneTransform(null, "foot_L0_0_jnt");
            Transform footL1 = FindBoneTransform(null, "foot_L0_1_jnt");

            Vector3 charUp = (head != null && spine0 != null)
                ? (head.position - spine0.position).normalized
                : Vector3.up;
            if (charUp.sqrMagnitude < 0.01f) charUp = Vector3.up;

            Vector3 charLeft = (armL0 != null && armR0 != null)
                ? Vector3.ProjectOnPlane(armL0.position - armR0.position, charUp).normalized
                : Vector3.right;
            if (charLeft.sqrMagnitude < 0.01f) charLeft = Vector3.right;
            Vector3 charRight = -charLeft;

            Vector3 charForward = (footL1 != null && footL0 != null)
                ? Vector3.ProjectOnPlane(footL1.position - footL0.position, charUp).normalized
                : Vector3.forward;
            if (charForward.sqrMagnitude < 0.01f) charForward = Vector3.forward;

            _inlineStatePoses.Clear();
            foreach (PlayerUIState state in Enum.GetValues(typeof(PlayerUIState)))
            {
                ResetModelToBindPose();
                StatePoseEntry entry = PlayerPosePresetData.CreateDefaultMetadataEntry(state);
                ApplyProceduralPoseInBindSpace(state, entry, charUp, charForward, charLeft, charRight);
                CaptureCurrentPoseToList(entry.boneSnapshots);
                _inlineStatePoses.Add(entry);
            }

            SyncInlineAndAssetPoses();

            _modelInstance.transform.rotation = savedModelRot;
            PreviewPoseImmediate(_selectedEditState);
        }

        private void ApplyProceduralPoseInBindSpace(
            PlayerUIState state,
            StatePoseEntry entry,
            Vector3 up,
            Vector3 fwd,
            Vector3 left,
            Vector3 right)
        {
            switch (state)
            {
                case PlayerUIState.Normal:
                    RotateBoneWorld("spine_C0_1_jnt", up, fwd, 6f);
                    RotateBoneWorld("spine_C0_3_jnt", up, fwd, 5f);
                    RotateBoneWorld("neck_C0_0_jnt", up, -fwd, 6f);

                    RotateBoneWorld("arm_L0_0_jnt", left, -up, 58f);
                    RotateBoneWorld("arm_L0_0_jnt", left, fwd, 14f);
                    RotateBoneWorld("arm_L0_4_jnt", -up, fwd, 36f);

                    RotateBoneWorld("arm_R0_0_jnt", right, -up, 58f);
                    RotateBoneWorld("arm_R0_0_jnt", right, fwd, 14f);
                    RotateBoneWorld("arm_R0_4_jnt", -up, fwd, 36f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 14f);
                    RotateBoneWorld("leg_L0_0_jnt", -up, left, 6f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 22f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 14f);
                    RotateBoneWorld("leg_R0_0_jnt", -up, right, 6f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 22f);
                    break;

                case PlayerUIState.MoveLeft:
                    entry.modelRootRotationOffset = new Vector3(0f, 0f, -12f);
                    RotateBoneWorld("spine_C0_1_jnt", up, left, 14f);
                    RotateBoneWorld("spine_C0_2_jnt", up, fwd, 10f);
                    RotateBoneWorld("spine_C0_3_jnt", fwd, left, 14f);
                    RotateBoneWorld("neck_C0_0_jnt", up, left, 10f);
                    RotateBoneWorld("neck_C0_1_jnt", fwd, left, 12f);

                    RotateBoneWorld("arm_L0_0_jnt", left, -up, 42f);
                    RotateBoneWorld("arm_L0_0_jnt", left, -fwd, 18f);
                    RotateBoneWorld("arm_L0_4_jnt", -up, fwd, 52f);

                    RotateBoneWorld("arm_R0_0_jnt", right, -up, 32f);
                    RotateBoneWorld("arm_R0_0_jnt", right, fwd, 26f);
                    RotateBoneWorld("arm_R0_4_jnt", -up, fwd, 28f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 24f);
                    RotateBoneWorld("leg_L0_0_jnt", -up, left, 12f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 38f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 10f);
                    RotateBoneWorld("leg_R0_0_jnt", -up, right, 10f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 16f);
                    break;

                case PlayerUIState.MoveRight:
                    entry.modelRootRotationOffset = new Vector3(0f, 0f, 12f);
                    RotateBoneWorld("spine_C0_1_jnt", up, right, 14f);
                    RotateBoneWorld("spine_C0_2_jnt", up, fwd, 10f);
                    RotateBoneWorld("spine_C0_3_jnt", fwd, right, 14f);
                    RotateBoneWorld("neck_C0_0_jnt", up, right, 10f);
                    RotateBoneWorld("neck_C0_1_jnt", fwd, right, 12f);

                    RotateBoneWorld("arm_R0_0_jnt", right, -up, 42f);
                    RotateBoneWorld("arm_R0_0_jnt", right, -fwd, 18f);
                    RotateBoneWorld("arm_R0_4_jnt", -up, fwd, 52f);

                    RotateBoneWorld("arm_L0_0_jnt", left, -up, 32f);
                    RotateBoneWorld("arm_L0_0_jnt", left, fwd, 26f);
                    RotateBoneWorld("arm_L0_4_jnt", -up, fwd, 28f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 24f);
                    RotateBoneWorld("leg_R0_0_jnt", -up, right, 12f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 38f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 10f);
                    RotateBoneWorld("leg_L0_0_jnt", -up, left, 10f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 16f);
                    break;

                case PlayerUIState.Boosting:
                    entry.modelRootRotationOffset = new Vector3(14f, 0f, 0f);
                    RotateBoneWorld("spine_C0_1_jnt", up, fwd, 18f);
                    RotateBoneWorld("spine_C0_2_jnt", up, fwd, 14f);
                    RotateBoneWorld("spine_C0_3_jnt", up, fwd, 12f);
                    RotateBoneWorld("neck_C0_0_jnt", up, -fwd, 24f);
                    RotateBoneWorld("neck_C0_1_jnt", up, -fwd, 14f);

                    RotateBoneWorld("arm_L0_0_jnt", left, -up, 45f);
                    RotateBoneWorld("arm_L0_0_jnt", left, -fwd, 42f);
                    RotateBoneWorld("arm_L0_4_jnt", -up, fwd, 20f);

                    RotateBoneWorld("arm_R0_0_jnt", right, -up, 45f);
                    RotateBoneWorld("arm_R0_0_jnt", right, -fwd, 42f);
                    RotateBoneWorld("arm_R0_4_jnt", -up, fwd, 20f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 32f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 55f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 32f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 55f);
                    break;

                case PlayerUIState.Shielding:
                    RotateBoneWorld("spine_C0_1_jnt", up, fwd, 10f);
                    RotateBoneWorld("spine_C0_3_jnt", up, fwd, 8f);
                    RotateBoneWorld("neck_C0_0_jnt", up, fwd, 8f);

                    RotateBoneWorld("arm_L0_0_jnt", left, fwd, 68f);
                    RotateBoneWorld("arm_L0_0_jnt", fwd, -up, 22f);
                    RotateBoneWorld("arm_L0_4_jnt", fwd, right, 75f);
                    RotateBoneWorld("arm_L0_4_jnt", right, up, 35f);

                    RotateBoneWorld("arm_R0_0_jnt", right, fwd, 68f);
                    RotateBoneWorld("arm_R0_0_jnt", fwd, -up, 22f);
                    RotateBoneWorld("arm_R0_4_jnt", fwd, left, 75f);
                    RotateBoneWorld("arm_R0_4_jnt", left, up, 35f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 26f);
                    RotateBoneWorld("leg_L0_0_jnt", -up, left, 14f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 42f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 26f);
                    RotateBoneWorld("leg_R0_0_jnt", -up, right, 14f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 42f);
                    break;

                case PlayerUIState.Stunned:
                    entry.modelRootRotationOffset = new Vector3(-10f, 0f, 7f);
                    RotateBoneWorld("spine_C0_1_jnt", up, -fwd, 14f);
                    RotateBoneWorld("spine_C0_2_jnt", up, left, 12f);
                    RotateBoneWorld("spine_C0_3_jnt", up, -fwd, 12f);
                    RotateBoneWorld("neck_C0_0_jnt", up, -fwd, 22f);
                    RotateBoneWorld("neck_C0_1_jnt", up, right, 18f);

                    RotateBoneWorld("arm_L0_0_jnt", left, -up, 64f);
                    RotateBoneWorld("arm_L0_0_jnt", left, -fwd, 18f);
                    RotateBoneWorld("arm_L0_4_jnt", -up, fwd, 18f);

                    RotateBoneWorld("arm_R0_0_jnt", right, -up, 60f);
                    RotateBoneWorld("arm_R0_0_jnt", right, -fwd, 22f);
                    RotateBoneWorld("arm_R0_4_jnt", -up, fwd, 14f);

                    RotateBoneWorld("leg_L0_0_jnt", -up, fwd, 18f);
                    RotateBoneWorld("leg_L0_4_jnt", -up, -fwd, 35f);

                    RotateBoneWorld("leg_R0_0_jnt", -up, fwd, 12f);
                    RotateBoneWorld("leg_R0_4_jnt", -up, -fwd, 28f);
                    break;
            }
        }

        private void RotateBoneWorld(string boneName, Vector3 fromDir, Vector3 toDir, float angleDegrees)
        {
            Transform bone = FindBoneTransform(null, boneName);
            if (bone == null) return;

            Quaternion fullArc = Quaternion.FromToRotation(fromDir.normalized, toDir.normalized);
            Quaternion deltaWorld = Quaternion.SlerpUnclamped(Quaternion.identity, fullArc, angleDegrees / 90f);
            bone.rotation = deltaWorld * bone.rotation;
        }

        public void SyncInlineAndAssetPoses()
        {
            if (_posePresetAsset == null) return;

            _posePresetAsset.statePoses.Clear();
            for (int i = 0; i < _inlineStatePoses.Count; i++)
            {
                StatePoseEntry src = _inlineStatePoses[i];
                var copy = new StatePoseEntry
                {
                    state = src.state,
                    displayLabel = src.displayLabel,
                    transitionSpeed = src.transitionSpeed,
                    mainHoloColor = src.mainHoloColor,
                    rimGlowColor = src.rimGlowColor,
                    accentColor = src.accentColor,
                    modelRootRotationOffset = src.modelRootRotationOffset,
                    modelRootPositionOffset = src.modelRootPositionOffset,
                    boneSnapshots = new List<BoneTransformSnapshot>(src.boneSnapshots)
                };
                _posePresetAsset.statePoses.Add(copy);
            }

            if (_bindPoseSnapshots != null && _bindPoseSnapshots.Count > 0)
            {
                _posePresetAsset.bindPoseSnapshots = new List<BoneTransformSnapshot>(_bindPoseSnapshots);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_posePresetAsset);
#endif
        }
    }
}
