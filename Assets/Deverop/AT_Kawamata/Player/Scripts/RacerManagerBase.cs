using System;
using UnityEngine;

// レーサー（Player／CPU）に関する機能の窓口の共通部分。入力を1回だけ読み、Boost/Shieldの入力をハンドリングし、
// 移動の入力（IRaceInput.Move）とFlightTuningProfileをPlayerFlightControllerへ渡す。
// 入力をどこから受け取るかだけを派生クラスが決める（PlayerManager：RaceInputProvider、CPUManager：CPUの入力）。
// レースの参加者（IRacer）として、Initialize/PlaceAt/TickはRaceManagerから呼び出される。
// ゴール後は入力を無視し、RaceSettingsの秒数が経ったらその場で停止させる（Tickは呼ばれ続ける）。
// ブースト接触は、当てる側（IBoostAttacker → BoostHitDetector）と当てられる側（IBoostHitReceiver）の両方を持つ。
// shild_01の当たり判定は、ブースト中は前方（FrontShell。BoostHitDetectorが付いていて、相手に当てる）、
// シールド中は後方（BackShell。ブースト中の相手から当てられると防ぐ）だけを有効にする。
// 通常の接触（ブーストなし）の食い込みは、RacerContactResolverからIRacerContactBodyとして押し戻される。
public abstract class RacerManagerBase : MonoBehaviour, IRacer, IBoostAttacker, IBoostHitReceiver, IRacerContactBody
{
    [SerializeField] private PlayerFlightController _flightController;
    [SerializeField] private BoostController _boost;
    [SerializeField] private ShieldController _shield;

    [Header("慣性設定")]
    [SerializeField] private FlightTuningProfile _defaultProfile;

    [Header("ブースト接触")]
    [Tooltip("ブーストを当てられたときの効果（スタン・横ずれ・無敵など）。PlayerとCPUで同じアセットを使う")]
    [SerializeField] private BoostHitProfile _boostHitProfile;

    [Tooltip("自分のブーストが相手に当たったことを受け取るBoostHitDetector（前方のshild_01の当たり判定に付ける）。未設定なら、自分か子から探す")]
    [SerializeField] private BoostHitDetector _boostHitDetector;

    [Header("shild_01")]
    [Tooltip("前方のshild_01の当たり判定。ブースト中だけ有効にする")]
    [SerializeField] private ShieldShellHitbox _frontShell;

    [Tooltip("後方のshild_01の当たり判定。シールド中だけ有効にする")]
    [SerializeField] private ShieldShellHitbox _backShell;

    [Header("通常の接触")]
    [Tooltip("他のレーサーとの食い込みの計算に使う体のCollider。未設定なら、このGameObjectのColliderを使う")]
    [SerializeField] private Collider _bodyCollider;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private readonly StunController _stun = new StunController();

    private bool _isInitialized;
    private bool _isFinished;
    private bool _hasCapturedFinishSpeed;
    private float _speedAtFinish;

    // 移動の入力を受け付けない残りの秒数（シールドで防がれてブーストを解除されたとき。検証用）。
    private float _steeringLockTimer;

    // 直近に通知したブースト・シールド・スタンの状態。変化したときだけイベントを出すために持つ。
    private bool _wasBoosting;
    private bool _wasShielding;
    private bool _wasStunned;

    // 以下は演出（BoostPresentation / ShieldPresentation）向けの通知。レーサーは演出を直接触らない。
    public event Action BoostStarted;

    public event Action BoostEnded;

    public event Action ShieldStarted;

    public event Action ShieldEnded;

    // シールドでブースト接触を防いだ瞬間（この後、シールドが消えるのでShieldEndedも出る）。
    public event Action ShieldBlocked;

    // シールドで防いで、前へ押し出し始めた瞬間（ShieldBlockedと同じタイミング）。
    // 引数は押し出しの初速（m/s）と、押し出す秒数。演出の強さ・長さを押し出しに合わせるのに使う。
    public event Action<float, float> ShieldBlockPushStarted;

    // 自分のブーストを相手に当てて、効果があった瞬間（はじいた／シールドで防がれた／スタンさせた）。
    // タックル成功はEBoostHitResult.Stunned。バックミラーの表示条件・当てたときの演出に使う。
    public event Action<EBoostHitResult> BoostHitLanded;

    // ブーストを当てられてスタンした瞬間・スタンが解けた瞬間（入力を受け付けない間の始まりと終わり）。
    public event Action StunStarted;

    public event Action StunEnded;

    // 直近のTickでPlayerFlightControllerへ渡したProfile。Initialize前はnull。
    public FlightTuningProfile CurrentProfile { get; private set; }

    public Transform Transform => transform;

    public bool IsBoosting => _boost != null && _boost.IsBoosting;

    public bool IsShielding => _shield != null && _shield.IsActive;

    public bool IsStunned => _stun.IsStunned;

    // スタンが解けるまでの残り秒数。スタン中でなければ0。
    public float StunRemainingTime => _stun.StunRemainingTime;

    public Collider BodyCollider => _bodyCollider;

    public Vector3 Velocity => _flightController.Velocity;

    // ブースト中・横ずれ中は、通常の接触を処理しない（ブースト接触の処理を優先する）。
    public bool IgnoresContact => !_isInitialized || IsBoosting || _flightController.IsBeingPushed;

    // 以下は、CPUの入力（判断）・UIなどが読むための値。読むだけで、状態は変えない。

    // コースへの問い合わせ口（中心・左右・上下・トンネル半径）。Initialize前はnull。
    public ICourseGuide CourseGuide => _flightController.Guide;

    // 機首の向きの速さ（m/s。最高速度で制限している成分）。
    public float ForwardSpeed => _flightController.ForwardSpeed;

    // 直近のTickで、トンネルの境界（壁）に触れていたか。
    public bool IsTouchingWall => _flightController.IsClampedToTunnel;

    // ブーストのゲージ（0～1）。
    public float BoostGaugeNormalized => _boost.GaugeNormalized;

    // 今ブーストを発動できるか（ゲージ・待ち時間に加え、シールド中は発動できないことも含める）。
    public bool CanActivateBoost => _boost.CanActivate && !_shield.IsActive;

    // 今シールドを張れるか（防いだ後の使えない間・ブースト中は張れないことも含める）。
    public bool CanActivateShield => _shield.CanActivate && !_boost.IsBoosting;

    // 防いだ後、シールドが再び使えるようになるまでの残り秒数。使えない間でなければ0。
    public float ShieldBrokenRemainingTime => _shield.BrokenRemainingTime;

    // 入力元の初期化（Initializeの中で呼ぶ）。
    protected abstract void InitializeInput();

    // このTickで使う入力。ゴール前のTickごとに1回だけ呼ぶ。nullなら、このTickは飛行の計算をしない。
    protected abstract IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data);

    public void Initialize()
    {
        if (_defaultProfile == null)
        {
            Debug.LogError($"{GetType().Name}: Default Profile が未設定です", this);
            return;
        }

        if (_shield == null || _boostHitProfile == null)
        {
            Debug.LogError($"{GetType().Name}: Shield / Boost Hit Profile が未設定です", this);
            return;
        }

        if (!_boost.Initialize() || !_shield.Initialize())
        {
            return;
        }

        // shild_01が無くても走れるよう、止めずに知らせるだけにする（ブースト接触の当てる・防ぐが効かなくなる）。
        if (_frontShell == null || _backShell == null)
        {
            Debug.LogError($"{GetType().Name}: Front Shell / Back Shell が未設定です（ブーストを当てる・シールドで防ぐ判定が取れません）", this);
        }

        if (_bodyCollider == null)
        {
            _bodyCollider = GetComponent<Collider>();
        }

        if (_boostHitDetector == null)
        {
            _boostHitDetector = GetComponentInChildren<BoostHitDetector>();
        }

        if (_boostHitDetector != null)
        {
            // Initializeが2回呼ばれても二重に購読しないよう、外してから付ける。
            _boostHitDetector.HitLanded -= OnBoostHitLanded;
            _boostHitDetector.HitLanded += OnBoostHitLanded;
        }
        else
        {
            Debug.LogWarning($"{GetType().Name}: BoostHitDetectorが見つからないため、BoostHitLandedは出ません", this);
        }

        InitializeInput();
        _stun.Reset();
        _steeringLockTimer = 0f;
        _flightController.Initialize(_defaultProfile);
        _isInitialized = true;
    }

    // 配置してから、ブースト・シールド・スタン・飛行の内部状態をリセットする（飛行の向きはtransformから取るため、この順番）。
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (!_isInitialized)
        {
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
        _boost.Initialize();
        _shield.Initialize();
        _stun.Reset();
        _steeringLockTimer = 0f;
        _isFinished = false;
        _hasCapturedFinishSpeed = false;
        CurrentProfile = _defaultProfile;
        _flightController.ResetState(CurrentProfile);
        NotifyStateChanges();
    }

    // カウントダウン中はRaceManagerがTickを呼ばないため、ブースト・シールドは使えない。
    // 最後にブースト・シールド・スタンの状態の変化を見て、演出用のイベントを出す（どの経路で始まった・終わったかに関係なく必ず出す）。
    public void Tick(float deltaTime, IReadOnlyRacerData data)
    {
        if (!_isInitialized)
        {
            return;
        }

        TickCore(deltaTime, data);
        NotifyStateChanges();
    }

    private void TickCore(float deltaTime, IReadOnlyRacerData data)
    {
        _stun.Tick(deltaTime);
        _steeringLockTimer = Mathf.Max(0f, _steeringLockTimer - deltaTime);
        _isFinished = data.IsFinished;

        // ゴール後は入力を無視し、ブースト・シールドを解除して、通常のProfileでまっすぐ飛ばす。
        // 速度の上限はゴールした瞬間の速度のまま（途中でブーストが切れても変わらないよう、ブーストの最高速度は使わない）。
        // SpeedMultiplierが0になったら（RaceSettingsのGoalStopDuration経過）、その場でぴたっと止め、以降は飛行の計算をしない。
        if (data.IsFinished)
        {
            _boost.ForceStop();
            _shield.ForceStop();
            _boost.Tick(deltaTime, false);
            _shield.Tick(deltaTime, false);

            if (!_hasCapturedFinishSpeed)
            {
                // 最高速度は前進の成分だけにかかるので、基準も前進の速さにする。
                _speedAtFinish = Mathf.Max(0f, _flightController.ForwardSpeed);
                _hasCapturedFinishSpeed = true;
            }

            CurrentProfile = _defaultProfile;
            if (data.SpeedMultiplier <= 0f)
            {
                _flightController.Stop();
                return;
            }

            _flightController.Tick(Vector2.zero, _speedAtFinish, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, _boost.CurrentMaxLateralSpeed, CurrentProfile, deltaTime);
            return;
        }

        IRaceInput input = GetInput(deltaTime, data);
        if (input == null)
        {
            return;
        }

        // スタン中は入力を受け付けない（移動・ブースト・シールドのすべて）。
        // シールドで防がれてブーストを解除された後（検証用の切り替えがONのとき）は、移動の入力だけを受け付けない。
        // Moveは入力側で大きさ0～1に制限済み（IRaceInputの約束）なので、ここでは正規化し直さない。
        bool canControl = !_stun.IsStunned;
        Vector2 move = canControl && _steeringLockTimer <= 0f ? input.Move : Vector2.zero;

        if (canControl)
        {
            HandleBoostAndShieldInput(input, data.RankRatio);
        }

        _boost.Tick(deltaTime, canControl && input.BoostHeld);
        _shield.Tick(deltaTime, canControl && input.ShieldHeld);

        CurrentProfile = _defaultProfile;
        float maxSpeed = _boost.CurrentMaxSpeed * _shield.MaxSpeedMultiplier * GetStunSpeedMultiplier();
        _flightController.Tick(move, maxSpeed, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, _boost.CurrentMaxLateralSpeed, CurrentProfile, deltaTime);
    }

    // ブースト中に他のレーサーと重なったとき、BoostHitDetectorから呼ばれる。
    // ゴール後・無敵中 → 無視、お互いにブースト中 → 少しはじくだけ（ブーストは解除しない）、
    // シールド中 → 後方のshild_01に当たったら防いでシールドが消え、前へ押し出される（加速。初速はBoostHitProfileの設定で、一定かぶつかってきた速さ次第）。体に当たっただけなら無視
    // （シールド中はshild_01同士で判定する）、それ以外 → スタン＋横ずれ（中心どうしが近いほど大きく押し出す）。
    // BoostHitDetector専用の入口なので、IBoostHitReceiverとしてだけ呼べるよう明示的に実装する。
    EBoostHitResult IBoostHitReceiver.ReceiveBoostHit(BoostHitInfo hit)
    {
        if (!_isInitialized || _isFinished || _stun.IsInvincible)
        {
            return EBoostHitResult.Ignored;
        }

        if (_boost.IsBoosting)
        {
            if (!_stun.CanBounce)
            {
                return EBoostHitResult.Ignored;
            }

            _stun.BeginBounceInterval(_boostHitProfile.BounceInterval);
            _flightController.ApplyLateralPush(hit.PushDirection, _boostHitProfile.BounceDistance, _boostHitProfile.PushDuration);
            Log($"ブースト同士で接触（はじく） 距離 {_boostHitProfile.BounceDistance:F1}m");
            return EBoostHitResult.Bounced;
        }

        if (_shield.IsActive)
        {
            if (!hit.HitShell || !_shield.TryBlock())
            {
                return EBoostHitResult.Ignored;
            }

            // 押し出しを始める前の速さで差を取る（ビリヤード方式のとき、ぶつかってきた速さで初速が変わる）。
            float relativeSpeed = hit.AttackerForwardSpeed - _flightController.ForwardSpeed;
            float blockPushSpeed = _boostHitProfile.EvaluateBlockPushSpeed(relativeSpeed);
            float blockPushDuration = _boostHitProfile.BlockPushDuration;
            float blockPushDistance = LateralPush.GetDistanceForInitialSpeed(blockPushSpeed, blockPushDuration);
            _stun.BeginInvincible(_boostHitProfile.InvincibleDuration);
            _flightController.ApplyForwardPush(blockPushDistance, blockPushDuration);
            ShieldBlocked?.Invoke();
            ShieldBlockPushStarted?.Invoke(blockPushSpeed, blockPushDuration);
            Log($"シールドでブースト接触を防いだ 初速 {blockPushSpeed:F1}m/s 前へ {blockPushDistance:F1}m（ぶつかってきた速さ {relativeSpeed:F1}m/s）");
            return EBoostHitResult.Blocked;
        }

        _boost.ForceStop();
        _stun.BeginStun(_boostHitProfile.StunDuration, _boostHitProfile.InvincibleDuration);
        float pushDistance = _boostHitProfile.EvaluatePushDistance(hit.Closeness);
        _flightController.ApplyLateralPush(hit.PushDirection, pushDistance, _boostHitProfile.PushDuration);
        Log($"ブーストを当てられた（スタン） 近さ {hit.Closeness:F2} 横ずれ {pushDistance:F1}m{(hit.HasPushDirection ? "" : "（向き未確定→コース中央側）")}");
        return EBoostHitResult.Stunned;
    }

    // 自分のブーストが相手に効果を与えたとき（BoostHitDetectorから）。演出・UI向けにそのまま通知する。
    // シールドで防がれたときは、BoostHitProfileの設定がONならブーストを解除する（押し続けていても、しばらく再発動しない）。
    private void OnBoostHitLanded(EBoostHitResult result)
    {
        Log($"ブーストを当てた（{result}）");

        if (result == EBoostHitResult.Blocked && _boostHitProfile.CancelAttackerBoostOnBlock)
        {
            _boost.ForceStop(_boostHitProfile.AttackerBoostLockDuration);
            Log($"シールドで防がれたため、ブーストを解除した（{_boostHitProfile.AttackerBoostLockDuration:F1}秒は再発動しない）");

            if (_boostHitProfile.LockAttackerSteeringOnCancel)
            {
                _steeringLockTimer = Mathf.Max(_steeringLockTimer, _boostHitProfile.AttackerBoostLockDuration);
                Log($"シールドで防がれたため、{_boostHitProfile.AttackerBoostLockDuration:F1}秒は移動の入力を受け付けない");
            }
        }

        BoostHitLanded?.Invoke(result);
    }

    // 通常の接触で食い込んだとき、RacerContactResolverから呼ばれる。
    // RacerContactResolver専用の入口なので、IRacerContactBodyとしてだけ呼べるよう明示的に実装する。
    void IRacerContactBody.ApplyContactCorrection(Vector3 offset, Vector3 blockNormal)
    {
        _flightController.ApplyContactCorrection(offset, blockNormal);
    }

    // ブーストとシールドは同時に使えない。片方を使っている間は、もう片方の「押した」入力を受け付けない（先に押した方を優先）。
    // ブーストは押しっぱなしのままでも、GO・オーバーヒート明け・スタン明け・シールドの解除の後に、ゲージが十分（CanRestartWhileHeld）なら再発動する。
    // シールドは引き続き「押した瞬間」が必要。
    private void HandleBoostAndShieldInput(IRaceInput input, float rankRatio)
    {
        bool wantsBoost = input.BoostPressed || (input.BoostHeld && _boost.CanRestartWhileHeld);
        if (wantsBoost && !_shield.IsActive && _boost.TryActivate(rankRatio))
        {
            Log($"ブースト発動 順位の割合 {rankRatio:F2} 消費 {_boost.ConsumptionPerSecond:F1}/秒 ゲージ {_boost.Gauge:F1}");
        }

        if (input.ShieldPressed && !_boost.IsBoosting && _shield.TryActivate())
        {
            Log("シールド発動");
        }
    }

    // 離した・使い切った・スタン・ゴール・置き直しなど、どの経路で変わっても、前回の通知から変わっていればイベントを出す。
    private void NotifyStateChanges()
    {
        bool isBoosting = IsBoosting;
        if (isBoosting != _wasBoosting)
        {
            _wasBoosting = isBoosting;
            if (isBoosting)
            {
                BoostStarted?.Invoke();
            }
            else
            {
                BoostEnded?.Invoke();
            }
        }

        bool isShielding = IsShielding;
        if (isShielding != _wasShielding)
        {
            _wasShielding = isShielding;
            if (isShielding)
            {
                ShieldStarted?.Invoke();
            }
            else
            {
                ShieldEnded?.Invoke();
            }
        }

        bool isStunned = IsStunned;
        if (isStunned != _wasStunned)
        {
            _wasStunned = isStunned;
            if (isStunned)
            {
                StunStarted?.Invoke();
            }
            else
            {
                StunEnded?.Invoke();
            }
        }

        UpdateShellHitboxes(isBoosting, isShielding);
    }

    // shild_01の当たり判定を、ブースト中は前方・シールド中は後方だけ有効にする。状態の通知と同じく、どの経路で変わっても毎回そろえる。
    private void UpdateShellHitboxes(bool isBoosting, bool isShielding)
    {
        if (_frontShell != null)
        {
            _frontShell.SetActive(isBoosting);
        }

        if (_backShell != null)
        {
            _backShell.SetActive(isShielding);
        }
    }

    private float GetStunSpeedMultiplier()
    {
        return _stun.IsStunned ? _boostHitProfile.StunSpeedMultiplier : 1f;
    }

    protected void Log(string message)
    {
        if (_showDebugLogs)
        {
            Debug.Log($"{GetType().Name}: {message}", this);
        }
    }
}
