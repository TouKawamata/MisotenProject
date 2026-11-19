using UnityEngine;

namespace AT_Murayama.PlayerAnimation
{
    /// <summary>
    /// RaceManager がシーンに存在しない単体テスト環境（03.unity 等）での初期開始モード。
    /// </summary>
    public enum StandaloneInitialPhase
    {
        [InspectorName("SetUp で待機（テストボタンで GO! 発火を確認）")]
        WaitInSetUp = 0,

        [InspectorName("即座に TakeOff → Fly を再生")]
        AutoTakeOffToFly = 1,

        [InspectorName("最初から Fly（直線飛行）で開始")]
        StartInFly = 2
    }

    /// <summary>
    /// Player / CPU 共通のキャラクターアニメーション駆動コントローラー。
    /// ・既存の RacerManagerBase / PlayerFlightController / RaceManager を一切変更せず、外部から読み取り専用で監視します。
    /// ・TimelineRaceIntro がバインドする親「Model」の Animator とは分離し、子「born model」側の Animator を駆動する設計です。
    /// ・Update 内 0 GC Alloc（Animator パラメータハッシュを全事前キャッシュ）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class RacerAnimationController : MonoBehaviour
    {
        // ============================================================
        // Animator パラメータハッシュ（0 GC Alloc）
        // ============================================================
        private static readonly int HashIsRacing = Animator.StringToHash(RacerAnimationProfile.ParamIsRacing);
        private static readonly int HashStartPlay = Animator.StringToHash(RacerAnimationProfile.ParamStartPlay);
        private static readonly int HashMoveX = Animator.StringToHash(RacerAnimationProfile.ParamMoveX);
        private static readonly int HashMoveY = Animator.StringToHash(RacerAnimationProfile.ParamMoveY);
        private static readonly int HashTurnDir = Animator.StringToHash(RacerAnimationProfile.ParamTurnDir);
        private static readonly int HashVerticalDir = Animator.StringToHash(RacerAnimationProfile.ParamVerticalDir);
        private static readonly int HashIsStunned = Animator.StringToHash(RacerAnimationProfile.ParamIsStunned);
        private static readonly int HashIsBoosting = Animator.StringToHash(RacerAnimationProfile.ParamIsBoosting);
        private static readonly int HashIsShielding = Animator.StringToHash(RacerAnimationProfile.ParamIsShielding);
        private static readonly int HashFire = Animator.StringToHash(RacerAnimationProfile.ParamFire);

        private static readonly int HashStateSetUp = Animator.StringToHash(RacerAnimationProfile.StateSetUp);
        private static readonly int HashStateFly = Animator.StringToHash(RacerAnimationProfile.StateFly);
        private static readonly int HashStateFlightBlend = Animator.StringToHash(RacerAnimationProfile.StateFlightBlend);

        [Header("1. プロファイル＆アニメーター参照")]
        [Tooltip("アニメーションクリップ・しきい値・方式設定を保持するプロファイル")]
        [SerializeField] private RacerAnimationProfile _profile;

        [Tooltip("キャラクターモデル（born model）直上の Animator。親の Model（TimelineRaceIntro用）とは別にしてください")]
        [SerializeField] private Animator _characterAnimator;

        [Tooltip("起動時に Profile の GeneratedController を Animator へ自動適用するか（Animator 側が未設定のときも補完）")]
        [SerializeField] private bool _applyProfileControllerOnEnable = true;

        [Tooltip("起動時に Animator.applyRootMotion を強制 OFF にする（PlayerFlightController の移動と競合防止）")]
        [SerializeField] private bool _enforceDisableRootMotion = true;

        [Header("2. 状態監視先（未設定なら自動検索・Player/CPU両対応）")]
        [Tooltip("PlayerManager または CPUManager（基底 RacerManagerBase）。未設定時は自身・親から自動取得")]
        [SerializeField] private RacerManagerBase _racerManager;

        [Tooltip("飛行入力（MoveInput / Steering）の取得元。未設定時は自身・親・RacerManager配下から自動取得")]
        [SerializeField] private PlayerFlightController _flightController;

        [Tooltip("レース開始（Ready / Countdown / Racing）の監視元。未設定時はシーン内から1回だけ自動検索")]
        [SerializeField] private RaceManager _raceManager;

        [Header("3. 挙動オプション")]
        [Tooltip("未割り当て（null）のクリップ枠（Stun / Fire / Boost / Shield 等）にはランタイムから遷移要求を送らない（Tポーズ・中断防止）")]
        [SerializeField] private bool _ignoreUnassignedClipTriggers = true;

        [Tooltip("RaceManager がシーンに無い単体検証（03.unity 等）での開始挙動")]
        [SerializeField] private StandaloneInitialPhase _standaloneInitialPhase = StandaloneInitialPhase.WaitInSetUp;

        [Tooltip("テスト用：ONにすると外部の RacerManager / RaceManager を無視し、下記のデバッグ値で直接アニメーションを操作できます")]
        [SerializeField] private bool _useManualTestOverride = false;

        // 手動デバッグ用オーバーライド値（インスペクターやテストから操作）
        [SerializeField, HideInInspector] private bool _manualIsRacing;
        [SerializeField, HideInInspector] private Vector2 _manualMoveInput;
        [SerializeField, HideInInspector] private bool _manualIsStunned;
        [SerializeField, HideInInspector] private bool _manualIsBoosting;
        [SerializeField, HideInInspector] private bool _manualIsShielding;

        // ランタイム内部状態（0 GC Alloc）
        private bool _hasRaceStarted;
        private bool _wasStunned;
        private int _lastJammerAmmo = -1;
        private float _smoothedMoveX;
        private float _smoothedMoveY;
        private int _currentTurnDir;
        private int _currentVerticalDir;
        private bool _hasPendingFireTrigger;
        private bool _hasPendingStartPlayTrigger;

        // 自動移動（中央戻り補間・コースカーブ自動旋回）計測用キャッシュ（0 GC Alloc）
        private bool _hasPreviousMotionSample;
        private Vector2 _previousPlanarOffset;
        private Vector3 _previousForward = Vector3.forward;
        private int _previousSegmentChangeCount;
        private Vector2 _smoothedCenterReturnSpeed;
        private Vector2 _smoothedCourseCurveRate;
        private Vector2 _lastRawInputMove;
        private Vector2 _lastCenterReturnContribution;
        private Vector2 _lastCourseCurveContribution;
        private Vector2 _lastCombinedMove;

        // ============================================================
        // 外部・Editor向けプロパティ
        // ============================================================
        public RacerAnimationProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        public Animator CharacterAnimator
        {
            get => _characterAnimator;
            set => _characterAnimator = value;
        }

        public RacerManagerBase RacerManager
        {
            get => _racerManager;
            set => _racerManager = value;
        }

        public PlayerFlightController FlightController
        {
            get => _flightController;
            set => _flightController = value;
        }

        public RaceManager RaceManager
        {
            get => _raceManager;
            set => _raceManager = value;
        }

        public bool UseManualTestOverride
        {
            get => _useManualTestOverride;
            set => _useManualTestOverride = value;
        }

        public bool ManualIsRacing
        {
            get => _manualIsRacing;
            set => _manualIsRacing = value;
        }

        public Vector2 ManualMoveInput
        {
            get => _manualMoveInput;
            set => _manualMoveInput = value;
        }

        public bool ManualIsStunned
        {
            get => _manualIsStunned;
            set => _manualIsStunned = value;
        }

        public bool ManualIsBoosting
        {
            get => _manualIsBoosting;
            set => _manualIsBoosting = value;
        }

        public bool ManualIsShielding
        {
            get => _manualIsShielding;
            set => _manualIsShielding = value;
        }

        public bool HasRaceStarted => _hasRaceStarted;
        public bool IsStunnedNow => _wasStunned;
        public float CurrentSmoothedMoveX => _smoothedMoveX;
        public float CurrentSmoothedMoveY => _smoothedMoveY;
        public int CurrentTurnDir => _currentTurnDir;
        public int CurrentVerticalDir => _currentVerticalDir;
        public Vector2 LastRawInputMove => _lastRawInputMove;
        public Vector2 LastSmoothedCenterReturnSpeed => _smoothedCenterReturnSpeed;
        public Vector2 LastSmoothedCourseCurveRate => _smoothedCourseCurveRate;
        public Vector2 LastCenterReturnContribution => _lastCenterReturnContribution;
        public Vector2 LastCourseCurveContribution => _lastCourseCurveContribution;
        public Vector2 LastCombinedMove => _lastCombinedMove;

        // ============================================================
        // Unity ライフサイクル
        // ============================================================
        private void Awake()
        {
            ResolveReferencesIfNeeded(allowSceneFind: false);
        }

        private void OnEnable()
        {
            ResolveReferencesIfNeeded(allowSceneFind: true);
            ConfigureAnimatorIfReady();
            SubscribeEvents();
            InitializeRuntimeState();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (_characterAnimator == null || _characterAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            // 1. 入力・状態を読み取る（ManualOverride または 実ゲーム状態）
            Vector2 targetMove;
            bool isStunned;
            bool isBoosting;
            bool isShielding;
            bool fireTriggered = _hasPendingFireTrigger;
            _hasPendingFireTrigger = false;

            bool startPlayTriggered = _hasPendingStartPlayTrigger;
            _hasPendingStartPlayTrigger = false;

            if (_useManualTestOverride)
            {
                if (_manualIsRacing && !_hasRaceStarted)
                {
                    _hasRaceStarted = true;
                    startPlayTriggered = true;
                }
                else if (!_manualIsRacing && _hasRaceStarted)
                {
                    _hasRaceStarted = false;
                }

                ResetAutoMotionTracking();
                targetMove = _manualMoveInput;
                _lastRawInputMove = targetMove;
                _lastCenterReturnContribution = Vector2.zero;
                _lastCourseCurveContribution = Vector2.zero;
                _lastCombinedMove = targetMove;
                isStunned = _manualIsStunned;
                isBoosting = _manualIsBoosting;
                isShielding = _manualIsShielding;
            }
            else
            {
                // レース開始判定：1度でも Racing に入ったら、途中ポーズの再開カウントダウンでは SetUp へ戻さない
                if (_raceManager != null)
                {
                    ERaceState raceState = _raceManager.State;
                    if (!_hasRaceStarted && (raceState == ERaceState.Racing || raceState == ERaceState.Finished))
                    {
                        _hasRaceStarted = true;
                        startPlayTriggered = true;
                    }
                }

                Vector2 rawInputMove;
                if (_flightController != null)
                {
                    rawInputMove = _flightController.MoveInput;
                    // MoveInput.x と Steering が同期しない特殊構成へのフォールバック
                    if (Mathf.Approximately(rawInputMove.x, 0f) && !Mathf.Approximately(_flightController.Steering, 0f))
                    {
                        rawInputMove.x = _flightController.Steering;
                    }
                }
                else
                {
                    rawInputMove = Vector2.zero;
                }

                // コース自動移動（中央戻り補間・コースカーブ自動旋回）の計測とソース合成
                if (_hasRaceStarted && _flightController != null)
                {
                    SampleAutoFlightMotion(dt);
                }
                else
                {
                    ResetAutoMotionTracking();
                }

                if (_profile != null)
                {
                    targetMove = CombineFlightMotion(
                        _profile.MotionSourceMode,
                        rawInputMove,
                        _smoothedCenterReturnSpeed,
                        _smoothedCourseCurveRate,
                        _profile.InputMotionWeight,
                        _profile.CenterReturnWeight,
                        _profile.CenterReturnReferenceSpeed,
                        _profile.CenterReturnMaxContribution,
                        _profile.CourseCurveWeight,
                        _profile.CourseCurveReferenceYawRate,
                        _profile.CourseCurveMaxContribution,
                        _profile.ApplyAutoMotionToVertical,
                        out _lastCenterReturnContribution,
                        out _lastCourseCurveContribution);
                }
                else
                {
                    _lastCenterReturnContribution = Vector2.zero;
                    _lastCourseCurveContribution = Vector2.zero;
                    targetMove = new Vector2(Mathf.Clamp(rawInputMove.x, -1f, 1f), Mathf.Clamp(rawInputMove.y, -1f, 1f));
                }

                _lastRawInputMove = rawInputMove;
                _lastCombinedMove = targetMove;

                if (_racerManager != null)
                {
                    isStunned = _racerManager.IsStunned;
                    isBoosting = _racerManager.IsBoosting;
                    isShielding = _racerManager.IsShielding;

                    // 妨害弾の発射検知（残弾が減少したフレームでワンショット発火）
                    int currentAmmo = _racerManager.JammerAmmo;
                    if (_lastJammerAmmo >= 0 && currentAmmo < _lastJammerAmmo)
                    {
                        fireTriggered = true;
                    }
                    _lastJammerAmmo = currentAmmo;
                }
                else
                {
                    isStunned = false;
                    isBoosting = false;
                    isShielding = false;
                }
            }

            // 2. パラメータのスムージング＆ヒステリシス判定
            float smoothSpeed = _profile != null ? _profile.MoveParameterSmoothSpeed : 12f;
            if (smoothSpeed <= 0f || dt <= 0f)
            {
                _smoothedMoveX = Mathf.Clamp(targetMove.x, -1f, 1f);
                _smoothedMoveY = Mathf.Clamp(targetMove.y, -1f, 1f);
            }
            else
            {
                float lerpT = 1f - Mathf.Exp(-smoothSpeed * dt);
                _smoothedMoveX = Mathf.Lerp(_smoothedMoveX, Mathf.Clamp(targetMove.x, -1f, 1f), lerpT);
                _smoothedMoveY = Mathf.Lerp(_smoothedMoveY, Mathf.Clamp(targetMove.y, -1f, 1f), lerpT);
            }

            float turnEnter = _profile != null ? _profile.TurnEnterThreshold : 0.25f;
            float turnExit = _profile != null ? _profile.TurnExitThreshold : 0.12f;
            _currentTurnDir = EvaluateAxisWithHysteresis(_smoothedMoveX, _currentTurnDir, turnEnter, turnExit);

            float vertEnter = _profile != null ? _profile.VerticalEnterThreshold : 0.30f;
            float vertExit = _profile != null ? _profile.VerticalExitThreshold : 0.15f;
            _currentVerticalDir = EvaluateAxisWithHysteresis(_smoothedMoveY, _currentVerticalDir, vertEnter, vertExit);

            // 3. 未割り当てクリップの安全ガード
            bool allowStun = !_ignoreUnassignedClipTriggers || _profile == null || _profile.HasStunClip;
            bool allowFire = !_ignoreUnassignedClipTriggers || _profile == null || _profile.HasFireClip;
            bool allowBoost = !_ignoreUnassignedClipTriggers || _profile == null || _profile.HasBoostClip;
            bool allowShield = !_ignoreUnassignedClipTriggers || _profile == null || _profile.HasShieldClip;

            bool effectiveStun = allowStun && isStunned;
            bool effectiveBoost = allowBoost && isBoosting && !effectiveStun;
            bool effectiveShield = allowShield && isShielding && !effectiveStun;

            // 4. Animator へ反映（0 GC Alloc）
            _characterAnimator.SetBool(HashIsRacing, _hasRaceStarted);
            _characterAnimator.SetFloat(HashMoveX, _smoothedMoveX);
            _characterAnimator.SetFloat(HashMoveY, _smoothedMoveY);
            _characterAnimator.SetInteger(HashTurnDir, _currentTurnDir);
            _characterAnimator.SetInteger(HashVerticalDir, _currentVerticalDir);
            _characterAnimator.SetBool(HashIsStunned, effectiveStun);
            _characterAnimator.SetBool(HashIsBoosting, effectiveBoost);
            _characterAnimator.SetBool(HashIsShielding, effectiveShield);

            if (startPlayTriggered)
            {
                _characterAnimator.SetTrigger(HashStartPlay);
            }

            if (fireTriggered && allowFire && _hasRaceStarted && !effectiveStun)
            {
                _characterAnimator.SetTrigger(HashFire);
            }

            _wasStunned = isStunned;
        }

        // ============================================================
        // 公開操作 API（デバッグ・外部連携・テスト用）
        // ============================================================

        /// <summary>
        /// スタート前の SetUp 状態へ強制リセットします（リトライやテスト用）。
        /// </summary>
        public void ResetToSetUp()
        {
            _hasRaceStarted = false;
            _manualIsRacing = false;
            _smoothedMoveX = 0f;
            _smoothedMoveY = 0f;
            _currentTurnDir = 0;
            _currentVerticalDir = 0;
            _hasPendingFireTrigger = false;
            _hasPendingStartPlayTrigger = false;
            ResetAutoMotionTracking();

            if (_characterAnimator != null && _characterAnimator.runtimeAnimatorController != null)
            {
                _characterAnimator.ResetTrigger(HashStartPlay);
                _characterAnimator.ResetTrigger(HashFire);
                _characterAnimator.SetBool(HashIsRacing, false);
                _characterAnimator.SetBool(HashIsStunned, false);
                _characterAnimator.SetBool(HashIsBoosting, false);
                _characterAnimator.SetBool(HashIsShielding, false);
                _characterAnimator.SetFloat(HashMoveX, 0f);
                _characterAnimator.SetFloat(HashMoveY, 0f);
                _characterAnimator.SetInteger(HashTurnDir, 0);
                _characterAnimator.SetInteger(HashVerticalDir, 0);

                if (_characterAnimator.HasState(0, HashStateSetUp))
                {
                    _characterAnimator.Play(HashStateSetUp, 0, 0f);
                }
            }
        }

        /// <summary>
        /// GO!（SetUp → TakeOff → Fly）の開始トリガーを手動発火します。
        /// </summary>
        public void TriggerStartPlay()
        {
            _hasRaceStarted = true;
            _manualIsRacing = true;
            _hasPendingStartPlayTrigger = true;
        }

        /// <summary>
        /// 弾発射モーションのワンショットトリガーを手動発火します。
        /// </summary>
        public void TriggerFire()
        {
            _hasPendingFireTrigger = true;
        }

        /// <summary>
        /// キャラクターモデル直上の Animator（mainRig / jnt_org を直接持つ Transform）を自動検出します。
        /// 親「Model」の TimelineRaceIntro 用 Animator を誤選択しないよう厳密に優先順位を付けて探します。
        /// </summary>
        public Transform FindBestCharacterModelRoot()
        {
            // 1. 自身が既に mainRig または jnt_org を直接子に持つなら自身
            if (HasDirectRigChild(transform))
            {
                return transform;
            }

            // 2. 子孫から mainRig / jnt_org を直接子に持つ Transform を探す
            Transform[] children = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform candidate = children[i];
                if (candidate != transform && HasDirectRigChild(candidate))
                {
                    return candidate;
                }
            }

            // 3. 見つからない場合は SkinnedMeshRenderer の親（main_model1 の親）を候補にする
            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            if (smr != null && smr.transform.parent != null)
            {
                return smr.transform.parent;
            }

            return null;
        }

        /// <summary>
        /// 指定した Animator がキャラクターリグ（mainRig / jnt_org）の直上にあるか判定します。
        /// false の場合、1階層ズレ（親 Model 等）により Generic ボーンアニメが動かないリスクがあります。
        /// </summary>
        public static bool IsAnimatorOnRigRoot(Animator animator)
        {
            return animator != null && HasDirectRigChild(animator.transform);
        }

        public static bool HasDirectRigChild(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            return root.Find("mainRig") != null || root.Find("jnt_org") != null;
        }

        /// <summary>
        /// 現在の参照（Animator / RacerManager / FlightController / RaceManager）を安全に自動解決します。
        /// </summary>
        public void ResolveReferencesIfNeeded(bool allowSceneFind)
        {
            if (_characterAnimator == null)
            {
                Transform rigRoot = FindBestCharacterModelRoot();
                if (rigRoot != null)
                {
                    _characterAnimator = rigRoot.GetComponent<Animator>();
                }
            }

            if (_racerManager == null)
            {
                _racerManager = GetComponent<RacerManagerBase>();
                if (_racerManager == null)
                {
                    _racerManager = GetComponentInParent<RacerManagerBase>();
                }
            }

            if (_flightController == null)
            {
                _flightController = GetComponent<PlayerFlightController>();
                if (_flightController == null)
                {
                    _flightController = GetComponentInParent<PlayerFlightController>();
                }
                if (_flightController == null && _racerManager != null)
                {
                    _flightController = _racerManager.GetComponent<PlayerFlightController>();
                }
            }

            if (_raceManager == null && allowSceneFind)
            {
                _raceManager = FindFirstObjectByType<RaceManager>();
            }
        }

        // ============================================================
        // 内部処理
        // ============================================================
        private void ConfigureAnimatorIfReady()
        {
            if (_characterAnimator == null)
            {
                return;
            }

            if (_enforceDisableRootMotion)
            {
                _characterAnimator.applyRootMotion = false;
            }

            if (_profile != null && _profile.GeneratedController != null)
            {
                if (_applyProfileControllerOnEnable || _characterAnimator.runtimeAnimatorController == null)
                {
                    _characterAnimator.runtimeAnimatorController = _profile.GeneratedController;
                }
            }
        }

        private void SubscribeEvents()
        {
            if (_raceManager != null)
            {
                _raceManager.RaceStarted -= OnRaceStarted;
                _raceManager.RaceStarted += OnRaceStarted;
            }

            if (_flightController != null)
            {
                _flightController.StateReset -= OnFlightStateReset;
                _flightController.StateReset += OnFlightStateReset;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_raceManager != null)
            {
                _raceManager.RaceStarted -= OnRaceStarted;
            }

            if (_flightController != null)
            {
                _flightController.StateReset -= OnFlightStateReset;
            }
        }

        private void InitializeRuntimeState()
        {
            _smoothedMoveX = 0f;
            _smoothedMoveY = 0f;
            _currentTurnDir = 0;
            _currentVerticalDir = 0;
            _hasPendingFireTrigger = false;
            _hasPendingStartPlayTrigger = false;
            ResetAutoMotionTracking();
            _wasStunned = _racerManager != null && _racerManager.IsStunned;
            _lastJammerAmmo = _racerManager != null ? _racerManager.JammerAmmo : -1;

            if (_useManualTestOverride)
            {
                _hasRaceStarted = _manualIsRacing;
                return;
            }

            if (_raceManager != null)
            {
                ERaceState state = _raceManager.State;
                _hasRaceStarted = (state == ERaceState.Racing || state == ERaceState.Finished);
                return;
            }

            // RaceManager が無い単体テストシーン（03.unity 等）での初期遷移
            switch (_standaloneInitialPhase)
            {
                case StandaloneInitialPhase.WaitInSetUp:
                    _hasRaceStarted = false;
                    break;

                case StandaloneInitialPhase.AutoTakeOffToFly:
                    _hasRaceStarted = true;
                    _hasPendingStartPlayTrigger = true;
                    break;

                case StandaloneInitialPhase.StartInFly:
                    _hasRaceStarted = true;
                    if (_characterAnimator != null && _characterAnimator.runtimeAnimatorController != null)
                    {
                        _characterAnimator.SetBool(HashIsRacing, true);
                        if (_characterAnimator.HasState(0, HashStateFlightBlend))
                        {
                            _characterAnimator.Play(HashStateFlightBlend, 0, 0f);
                        }
                        else if (_characterAnimator.HasState(0, HashStateFly))
                        {
                            _characterAnimator.Play(HashStateFly, 0, 0f);
                        }
                    }
                    break;
            }
        }

        private void OnRaceStarted()
        {
            if (_useManualTestOverride)
            {
                return;
            }

            if (!_hasRaceStarted)
            {
                _hasRaceStarted = true;
                _hasPendingStartPlayTrigger = true;
            }
        }

        private void OnFlightStateReset()
        {
            if (_useManualTestOverride)
            {
                return;
            }

            // リトライやスタートグリッド再配置時（Ready 状態）のみ SetUp に戻す
            if (_raceManager == null || _raceManager.State == ERaceState.Ready)
            {
                ResetToSetUp();
            }
        }

        private void ResetAutoMotionTracking()
        {
            _hasPreviousMotionSample = false;
            _previousPlanarOffset = Vector2.zero;
            _previousForward = _flightController != null ? _flightController.transform.forward : Vector3.forward;
            _previousSegmentChangeCount = 0;
            _smoothedCenterReturnSpeed = Vector2.zero;
            _smoothedCourseCurveRate = Vector2.zero;
            _lastRawInputMove = Vector2.zero;
            _lastCenterReturnContribution = Vector2.zero;
            _lastCourseCurveContribution = Vector2.zero;
            _lastCombinedMove = Vector2.zero;
        }

        private void SampleAutoFlightMotion(float dt)
        {
            if (dt <= 0.0001f || _flightController == null)
            {
                return;
            }

            ICourseGuide guide = _flightController.Guide;
            ICourseRoute route = guide as ICourseRoute;
            int segCount = route != null ? route.SegmentChangeCount : 0;

            Transform flightTr = _flightController.transform;
            Vector3 pos = flightTr.position;
            Vector3 curForward = flightTr.forward;
            Vector3 refUp = _flightController.ReferenceUp;

            bool hasGuide = guide != null;
            Vector2 curPlanarOffset = Vector2.zero;
            if (hasGuide)
            {
                Vector3 center = guide.GetCenterPosition(pos);
                Vector3 offset = pos - center;
                curPlanarOffset = new Vector2(
                    Vector3.Dot(offset, guide.GetRight(pos)),
                    Vector3.Dot(offset, guide.GetUp(pos)));
            }

            // 初回フレームまたは分岐コースの区間切り替わりフレームは基準点ジャンプを防ぐため記録のみ行う
            if (!_hasPreviousMotionSample || segCount != _previousSegmentChangeCount)
            {
                _previousPlanarOffset = curPlanarOffset;
                _previousForward = curForward;
                _previousSegmentChangeCount = segCount;
                _hasPreviousMotionSample = true;
                return;
            }

            Vector2 rawPlanarSpeed = Vector2.zero;
            if (hasGuide)
            {
                Vector2 deltaOffset = curPlanarOffset - _previousPlanarOffset;
                // 1フレーム 5m 超のワープ移動はスパイクとみなして除外
                if (deltaOffset.sqrMagnitude <= 25f)
                {
                    rawPlanarSpeed = deltaOffset / dt;
                }
            }

            Vector2 rawCurveRate = ComputeCourseCurveRateDegPerSec(_previousForward, curForward, refUp, dt);

            float autoSmooth = _profile != null ? _profile.AutoMotionSignalSmoothSpeed : 8f;
            if (autoSmooth <= 0f)
            {
                _smoothedCenterReturnSpeed = rawPlanarSpeed;
                _smoothedCourseCurveRate = rawCurveRate;
            }
            else
            {
                float t = 1f - Mathf.Exp(-autoSmooth * dt);
                _smoothedCenterReturnSpeed = Vector2.Lerp(_smoothedCenterReturnSpeed, rawPlanarSpeed, t);
                _smoothedCourseCurveRate = Vector2.Lerp(_smoothedCourseCurveRate, rawCurveRate, t);
            }

            _previousPlanarOffset = curPlanarOffset;
            _previousForward = curForward;
            _previousSegmentChangeCount = segCount;
        }

        /// <summary>
        /// 機首の前フレーム向きと現フレーム向きから、コース基準上方向に対する左右旋回速度(X)と上下ピッチ速度(Y)[deg/s]を算出します。
        /// 右旋回・上り坂が正(+)になります。1フレームで閾値角度を超えるワープ時は0を返します。
        /// </summary>
        public static Vector2 ComputeCourseCurveRateDegPerSec(
            Vector3 prevForward,
            Vector3 currentForward,
            Vector3 referenceUp,
            float deltaTime,
            float maxValidAnglePerFrameDeg = 45f)
        {
            if (deltaTime <= 0.0001f || prevForward.sqrMagnitude < 0.0001f || currentForward.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            Vector3 up = referenceUp.sqrMagnitude > 0.0001f ? referenceUp.normalized : Vector3.up;
            Vector3 prevN = prevForward.normalized;
            Vector3 curN = currentForward.normalized;

            if (Vector3.Angle(prevN, curN) > maxValidAnglePerFrameDeg)
            {
                return Vector2.zero;
            }

            float yawDeg = 0f;
            Vector3 prevYaw = Vector3.ProjectOnPlane(prevN, up);
            Vector3 curYaw = Vector3.ProjectOnPlane(curN, up);
            if (prevYaw.sqrMagnitude > 0.0001f && curYaw.sqrMagnitude > 0.0001f)
            {
                yawDeg = Vector3.SignedAngle(prevYaw, curYaw, up);
            }

            float pitchDeg = 0f;
            Vector3 right = Vector3.Cross(up, curN);
            if (right.sqrMagnitude > 0.0001f)
            {
                right.Normalize();
                Vector3 prevPitch = Vector3.ProjectOnPlane(prevN, right);
                Vector3 curPitch = Vector3.ProjectOnPlane(curN, right);
                if (prevPitch.sqrMagnitude > 0.0001f && curPitch.sqrMagnitude > 0.0001f)
                {
                    pitchDeg = -Vector3.SignedAngle(prevPitch, curPitch, right);
                }
            }

            return new Vector2(yawDeg / deltaTime, pitchDeg / deltaTime);
        }

        /// <summary>
        /// 指定したソースモードに応じて「入力」「中央戻り補間」「コースカーブ自動旋回」を安全に合成します。
        /// 入力中は軸ごとの neutralFactor によって中央戻り寄与の二重加算を防ぎます。
        /// </summary>
        public static Vector2 CombineFlightMotion(
            FlightMotionSourceMode mode,
            Vector2 rawInput,
            Vector2 centerReturnSpeedMps,
            Vector2 courseCurveRateDps,
            float inputWeight,
            float centerReturnWeight,
            float centerReturnRefSpeed,
            float centerReturnMaxContribution,
            float courseCurveWeight,
            float courseCurveRefYawRate,
            float courseCurveMaxContribution,
            bool applyAutoMotionToVertical,
            out Vector2 centerContrib,
            out Vector2 curveContrib)
        {
            centerContrib = Vector2.zero;
            curveContrib = Vector2.zero;

            Vector2 inputPart = new Vector2(Mathf.Clamp(rawInput.x, -1f, 1f), Mathf.Clamp(rawInput.y, -1f, 1f)) * inputWeight;

            bool useCenter = mode == FlightMotionSourceMode.InputAndCenterReturn || mode == FlightMotionSourceMode.FullCombined;
            bool useCurve = mode == FlightMotionSourceMode.InputAndCourseCurve || mode == FlightMotionSourceMode.FullCombined;

            if (useCenter)
            {
                float refSpeed = Mathf.Max(0.5f, centerReturnRefSpeed);
                float neutralFactorX = 1f - Mathf.Clamp01(Mathf.Abs(rawInput.x));
                float cx = Mathf.Clamp(
                    (centerReturnSpeedMps.x / refSpeed) * centerReturnWeight,
                    -centerReturnMaxContribution,
                    centerReturnMaxContribution) * neutralFactorX;

                float cy = 0f;
                if (applyAutoMotionToVertical)
                {
                    float neutralFactorY = 1f - Mathf.Clamp01(Mathf.Abs(rawInput.y));
                    cy = Mathf.Clamp(
                        (centerReturnSpeedMps.y / refSpeed) * centerReturnWeight,
                        -centerReturnMaxContribution,
                        centerReturnMaxContribution) * neutralFactorY;
                }

                centerContrib = new Vector2(cx, cy);
            }

            if (useCurve)
            {
                float refYaw = Mathf.Max(1f, courseCurveRefYawRate);
                float kx = Mathf.Clamp(
                    (courseCurveRateDps.x / refYaw) * courseCurveWeight,
                    -courseCurveMaxContribution,
                    courseCurveMaxContribution);

                float ky = 0f;
                if (applyAutoMotionToVertical)
                {
                    ky = Mathf.Clamp(
                        (courseCurveRateDps.y / refYaw) * courseCurveWeight,
                        -courseCurveMaxContribution,
                        courseCurveMaxContribution);
                }

                curveContrib = new Vector2(kx, ky);
            }

            Vector2 total = inputPart + centerContrib + curveContrib;
            return new Vector2(Mathf.Clamp(total.x, -1f, 1f), Mathf.Clamp(total.y, -1f, 1f));
        }

        /// <summary>
        /// ヒステリシス付きの 3 値判定（-1 / 0 / +1）。
        /// 境界付近でのガタつき（チャタリング）を防ぎます。
        /// </summary>
        public static int EvaluateAxisWithHysteresis(float value, int previousDir, float enterThreshold, float exitThreshold)
        {
            if (previousDir > 0)
            {
                if (value < -enterThreshold) return -1;
                if (value >= exitThreshold) return 1;
                return 0;
            }

            if (previousDir < 0)
            {
                if (value > enterThreshold) return 1;
                if (value <= -exitThreshold) return -1;
                return 0;
            }

            if (value >= enterThreshold) return 1;
            if (value <= -enterThreshold) return -1;
            return 0;
        }
    }
}
