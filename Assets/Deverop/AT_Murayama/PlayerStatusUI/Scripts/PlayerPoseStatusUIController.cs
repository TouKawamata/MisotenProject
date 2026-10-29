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
            [InspectorName("右下 (Bottom Right)")]
            BottomRight = 0,

            [InspectorName("左下 (Bottom Left)")]
            BottomLeft = 1,

            [InspectorName("右上 (Top Right)")]
            TopRight = 2,

            [InspectorName("左上 (Top Left)")]
            TopLeft = 3
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

        // ──────────────────────────────────────────────
        // Inspector 設定
        // ──────────────────────────────────────────────

        [Header("1. プレイヤー参照（未指定時はシーン内から自動検索）")]
        [Tooltip("状態を保持する PlayerManager（IsStunned / IsBoosting / IsShielding / 各イベントの取得元）")]
        [SerializeField] private PlayerManager _playerManager;

        [Tooltip("左右移動量（Steering: -1〜+1）を保持する PlayerFlightController")]
        [SerializeField] private PlayerFlightController _flightController;

        [Header("2. 3Dキャラクターモデル ＆ ポーズプリセットデータ")]
        [Tooltip("UIに表示するボーン入りキャラクターモデルプレハブ（未指定時は Assets/Deverop/AT_Murayama/born model.fbx を自動ロード）")]
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

        [Tooltip("【通常マテリアル時】状態変化（スタン・ブースト・シールド等）に応じてモデルの色をほんのり発光・ティントさせるか")]
        [SerializeField] private bool _tintOriginalMaterialOnState = true;

        [Tooltip("【通常マテリアル時】ティントの強度（0でティントなし完全オリジナル、1でしっかり状態色を反映）")]
        [Range(0f, 1f)]
        [SerializeField] private float _originalMaterialTintIntensity = 0.35f;

        [Tooltip("【ホログラム時】キャラクターモデルに適用する3Dホログラムスタイル")]
        [SerializeField] private ModelHologramController.ModelHologramStyle _hologramStyle =
            ModelHologramController.ModelHologramStyle.ClassicScanline;

        [Tooltip("【カスタムマテリアル時】モデルの全スロットに割り当てるカスタムマテリアル")]
        [SerializeField] private Material _customMaterial;

        [Tooltip("切り替わり瞬間（BoostStarted / ShieldStarted / ShieldBlocked / Stun開始）の発光パルス強度")]
        [Range(0f, 4.0f)]
        [SerializeField] private float _eventFlashIntensity = 2.0f;

        [Header("6. UI背景・枠線・サブカメラ設定")]
        [Tooltip("UI背景の半透明バックプレートを表示するか")]
        [SerializeField] private bool _showUIBackdrop = true;

        [Tooltip("UI背景バックプレートの色と透明度（アルファを0にすると背景が完全透明になりキャラクターのみ浮きます）")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _uiBackdropColor = new Color(0.02f, 0.08f, 0.14f, 0.55f);

        [Tooltip("UI外枠（ボーダー）を表示するか")]
        [SerializeField] private bool _showUIBorderFrame = true;

        [Tooltip("UI外枠の色をプレイヤー状態の発光色と連動させるか（ON: 通常はシアン、ブースト時はオレンジ、シールド時は緑、スタン時は赤等に発光）")]
        [SerializeField] private bool _dynamicBorderColor = true;

        [Tooltip("【動的枠色OFF時】UI外枠の固定カラー")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _uiBorderStaticColor = new Color(0.1f, 0.6f, 0.8f, 0.6f);

        [Tooltip("UI外枠の透明度（アルファ倍率）")]
        [Range(0f, 1f)]
        [SerializeField] private float _uiBorderAlpha = 0.65f;

        [Tooltip("サブカメラの背景クリア色（通常は完全透明 (0,0,0,0) を推奨。単色やグラデーションにしたい場合に変更可能）")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _cameraBackgroundColor = new Color(0f, 0f, 0f, 0f);

        [Header("7. UI表示方式・レイアウト・カメラアングル設定")]
        [Tooltip("方式A（RenderTexture＋Canvas UI表示）と 方式B（メインカメラ前3Dミニチュア配置）を切り替えます")]
        [SerializeField] private UIDisplayMode _displayMode = UIDisplayMode.ModeA_RenderTextureUI;

        [Tooltip("画面のどの角にステータスUIを表示するか")]
        [SerializeField] private ScreenCorner _screenCorner = ScreenCorner.BottomRight;

        [Tooltip("UIの表示サイズ（ピクセル）")]
        [SerializeField] private Vector2 _uiSize = new Vector2(260f, 300f);

        [Tooltip("画面端からの余白（ピクセル）")]
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

        private Camera _cachedMainCamera;
        private int _savedMainCameraCullingMask;
        private bool _hasModifiedMainCameraMask;

        private readonly List<Renderer> _cachedRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, Material[]> _originalMaterialsMap = new Dictionary<Renderer, Material[]>();
        private Material _runtimeHologramMaterial;
        private ModelHologramController.ModelHologramStyle _lastRuntimeHoloStyle = (ModelHologramController.ModelHologramStyle)(-1);
        private MaterialPropertyBlock _propBlock;

        private readonly Dictionary<string, Transform> _boneByPath = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _boneByName = new Dictionary<string, Transform>();
        private readonly List<Transform> _allRigBones = new List<Transform>();
        private readonly Dictionary<Transform, Quaternion> _currentSmoothedRotations = new Dictionary<Transform, Quaternion>();

        private PlayerUIState _currentState = PlayerUIState.Normal;
        private PlayerUIState _pendingNormalSubState = PlayerUIState.Normal;
        private float _lastNormalSubStateSwitchTime = -10f;

        private Color _currentMainColor = new Color(0.0f, 2.2f, 3.2f, 0.80f);
        private Color _currentRimColor = new Color(0.4f, 4.0f, 4.8f, 0.95f);
        private Color _currentAccentColor = new Color(0.2f, 4.2f, 3.8f, 1.00f);

        private Vector3 _currentModelRootRotOffset = Vector3.zero;
        private Vector3 _currentModelRootPosOffset = Vector3.zero;

        private float _eventFlashTimer = 0f;
        private Color _eventFlashColor = Color.white;
        private float _eventScalePunch = 0f;

        private bool _wasStunnedLastFrame = false;
        private Bounds _initialModelLocalBounds;
        private bool _hasValidModelBounds = false;

        private float _editorTransitionRemainingTime = 0f;

        // シェーダープロパティIDキャッシュ
        private static readonly int ShaderProp_BaseColor       = Shader.PropertyToID("_BaseColor");
        private static readonly int ShaderProp_Color           = Shader.PropertyToID("_Color");
        private static readonly int ShaderProp_EmissionColor   = Shader.PropertyToID("_EmissionColor");
        private static readonly int ShaderProp_MainColor       = Shader.PropertyToID("_MainColor");
        private static readonly int ShaderProp_RimColor        = Shader.PropertyToID("_RimColor");
        private static readonly int ShaderProp_HexWireColor    = Shader.PropertyToID("_HexWireColor");
        private static readonly int ShaderProp_GridColor       = Shader.PropertyToID("_GridColor");
        private static readonly int ShaderProp_ScanPlaneColor  = Shader.PropertyToID("_ScanPlaneColor");
        private static readonly int ShaderProp_GlitchGlowColor = Shader.PropertyToID("_GlitchGlowColor");
        private static readonly int ShaderProp_SubColor        = Shader.PropertyToID("_SubColor");
        private static readonly int ShaderProp_EmitterColor    = Shader.PropertyToID("_EmitterColor");

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

        public ModelHologramController.ModelHologramStyle HologramStyle
        {
            get => _hologramStyle;
            set
            {
                _hologramStyle = value;
                ApplyMaterialsAndColors(_currentMainColor, _currentRimColor, _currentAccentColor);
            }
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

        // ──────────────────────────────────────────────
        // ライフサイクル
        // ──────────────────────────────────────────────

        private void OnEnable()
        {
            AutoFindReferencesIfNeeded();
            SubscribePlayerEvents();
            EnsureStageAndUIReady();
        }

        private void OnDisable()
        {
            UnsubscribePlayerEvents();
            RestoreMainCameraCullingMask();
            CleanupGeneratedObjects();
        }

        private void OnValidate()
        {
            if (this == null) return;

            // インスペクター変更時に即座にUIレイアウト・カメラ・マテリアル・色を更新
            if (_stageRoot != null && _modelInstance != null)
            {
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
            }

            if (_flightController == null)
            {
                _flightController = FindAnyObjectByType<PlayerFlightController>();
            }

            if (_characterModelPrefab == null)
            {
#if UNITY_EDITOR
                string defaultPath = "Assets/Deverop/AT_Murayama/born model.fbx";
                _characterModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath);
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
                float now = Application.isPlaying ? Time.unscaledTime : 0f;
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

            // スタン中の警告点滅
            if (targetState == PlayerUIState.Stunned && _enableStunWobbleAndFlicker)
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
            if (_eventFlashTimer > 0.001f)
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
            if (_tintOriginalMaterialOnState && _originalMaterialTintIntensity > 0.001f)
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
            string shaderPath = ModelHologramController.GetShaderPath(_hologramStyle);
            Shader shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                shader = Shader.Find("Custom/Hologram/ModelHologram");
            }

            if (_runtimeHologramMaterial == null || _lastRuntimeHoloStyle != _hologramStyle || _runtimeHologramMaterial.shader != shader)
            {
                if (_runtimeHologramMaterial != null)
                {
                    DestroySafe(_runtimeHologramMaterial);
                }
                if (shader != null)
                {
                    _runtimeHologramMaterial = new Material(shader)
                    {
                        name = $"M_RuntimePoseHologram_{_hologramStyle}",
                        hideFlags = HideFlags.DontSave
                    };
                    _lastRuntimeHoloStyle = _hologramStyle;
                }
            }
        }

        private void UpdateHologramMaterialProperties(Material mat, Color mainCol, Color rimCol, Color accentCol)
        {
            if (mat == null) return;
            if (mat.HasProperty(ShaderProp_MainColor)) mat.SetColor(ShaderProp_MainColor, mainCol);
            if (mat.HasProperty(ShaderProp_RimColor)) mat.SetColor(ShaderProp_RimColor, rimCol);
            if (mat.HasProperty(ShaderProp_HexWireColor)) mat.SetColor(ShaderProp_HexWireColor, accentCol);
            if (mat.HasProperty(ShaderProp_GridColor)) mat.SetColor(ShaderProp_GridColor, accentCol);
            if (mat.HasProperty(ShaderProp_ScanPlaneColor)) mat.SetColor(ShaderProp_ScanPlaneColor, accentCol);
            if (mat.HasProperty(ShaderProp_GlitchGlowColor)) mat.SetColor(ShaderProp_GlitchGlowColor, accentCol);
            if (mat.HasProperty(ShaderProp_SubColor)) mat.SetColor(ShaderProp_SubColor, accentCol * 0.35f);
            if (mat.HasProperty(ShaderProp_EmitterColor)) mat.SetColor(ShaderProp_EmitterColor, accentCol);
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
            RectTransform rawRect = _uiRawImage.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = new Vector2(4f, 24f);
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

                    float signX = (_screenCorner == ScreenCorner.BottomRight || _screenCorner == ScreenCorner.TopRight) ? 1f : -1f;
                    float signY = (_screenCorner == ScreenCorner.TopRight || _screenCorner == ScreenCorner.TopLeft) ? 1f : -1f;

                    float offsetX = signX * (vWidth * 0.34f);
                    float offsetY = signY * (vHeight * 0.28f) - (_initialModelLocalBounds.center.y * _modeBMiniatureScale);

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
                default: // BottomRight
                    anchor = new Vector2(1f, 0f);
                    pivot = new Vector2(1f, 0f);
                    pos = new Vector2(-_uiMargin.x, _uiMargin.y);
                    break;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = _uiSize;
        }

        private void ApplyMainCameraCullingMaskExclusion()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            if (_cachedMainCamera != mainCam)
            {
                RestoreMainCameraCullingMask();
                _cachedMainCamera = mainCam;
                _savedMainCameraCullingMask = mainCam.cullingMask;
                _hasModifiedMainCameraMask = true;
            }

            int layerBit = 1 << _uiModelLayer;
            if ((mainCam.cullingMask & layerBit) != 0)
            {
                mainCam.cullingMask &= ~layerBit;
            }
        }

        private void RestoreMainCameraCullingMask()
        {
            if (_hasModifiedMainCameraMask && _cachedMainCamera != null)
            {
                _cachedMainCamera.cullingMask = _savedMainCameraCullingMask;
            }
            _hasModifiedMainCameraMask = false;
            _cachedMainCamera = null;
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
