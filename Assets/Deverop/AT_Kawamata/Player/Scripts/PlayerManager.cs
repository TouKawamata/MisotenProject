using UnityEngine;

// Playerに関する機能の窓口。入力を1回だけ読み、Boost/Shield/Shortcutの入力をハンドリングし、
// 現在有効なFlightTuningProfile（通常／ショートカット吸着）を選んでPlayerFlightControllerへ渡す。
// レースの参加者（IRacer）として、Initialize/PlaceAt/TickはRaceManagerから呼び出される。
// ゴール後は入力を無視し、RaceSettingsの設定に沿って減速・停止させる（Tickは呼ばれ続ける）。
// ブースト接触は、当てる側（IBoostAttacker → BoostHitDetector）と当てられる側（IBoostHitReceiver）の両方を持つ。
// 通常の接触（ブーストなし）の食い込みは、RacerContactResolverからIRacerContactBodyとして押し戻される。
public class PlayerManager : MonoBehaviour, IShortcutZoneReceiver, IRacer, IBoostAttacker, IBoostHitReceiver, IRacerContactBody
{
    [SerializeField] private PlayerFlightController _flightController;
    [SerializeField] private BoostController _boost;
    [SerializeField] private ShieldController _shield;
    [SerializeField] private RaceInputProvider _inputProvider;

    [Header("慣性設定")]
    [SerializeField] private FlightTuningProfile _defaultProfile;
    [Tooltip("ShortcutZoneの発動エリア内でトリガーが成立したときに切り替えるProfile（Splineの高さへ強く吸着する設定）")]
    [SerializeField] private FlightTuningProfile _shortcutProfile;

    [Header("ブースト接触")]
    [Tooltip("ブーストを当てられたときの効果（スタン・横ずれ・無敵など）。DummyRacerと同じアセットを使う")]
    [SerializeField] private BoostHitProfile _boostHitProfile;

    [Header("通常の接触")]
    [Tooltip("他のレーサーとの食い込みの計算に使う体のCollider。未設定なら、このGameObjectのColliderを使う")]
    [SerializeField] private Collider _bodyCollider;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private readonly StunController _stun = new StunController();

    private ShortcutZone _currentShortcutZone;
    private bool _isShortcutProfileActive;
    private bool _wasHorizontalAboveThreshold;
    private bool _isInitialized;
    private bool _isFinished;
    private bool _hasCapturedFinishSpeed;
    private float _speedAtFinish;

    public bool IsInShortcutZone => _currentShortcutZone != null;

    public bool IsShortcutProfileActive => _isShortcutProfileActive;

    // 直近のTickでPlayerFlightControllerへ渡したProfile。Initialize前はnull。
    public FlightTuningProfile CurrentProfile { get; private set; }

    public Transform Transform => transform;

    public bool IsBoosting => _boost != null && _boost.IsBoosting;

    public bool IsShielding => _shield != null && _shield.IsActive;

    public bool IsStunned => _stun.IsStunned;

    public Collider BodyCollider => _bodyCollider;

    public Vector3 Velocity => _flightController.Velocity;

    // ブースト中・横ずれ中は、通常の接触を処理しない（ブースト接触の処理を優先する）。
    public bool IgnoresContact => !_isInitialized || IsBoosting || _flightController.IsBeingPushed;

    public void Initialize()
    {
        if (_defaultProfile == null || _shortcutProfile == null)
        {
            Debug.LogError("PlayerManager: Default Profile / Shortcut Profile が未設定です", this);
            return;
        }

        if (_shield == null || _boostHitProfile == null)
        {
            Debug.LogError("PlayerManager: Shield / Boost Hit Profile が未設定です", this);
            return;
        }

        if (!_boost.Initialize() || !_shield.Initialize())
        {
            return;
        }

        if (_bodyCollider == null)
        {
            _bodyCollider = GetComponent<Collider>();
        }

        _inputProvider.Initialize();
        _stun.Reset();
        _flightController.Initialize(_defaultProfile);
        _isInitialized = true;
    }

    // 配置してから、ブースト・シールド・スタン・ショートカット・飛行の内部状態をリセットする（飛行の向きはtransformから取るため、この順番）。
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
        // 発動エリアに居るかどうか（_currentShortcutZone）はShortcutZone側のトリガー通知に任せ、吸着状態だけ解除する。
        _isShortcutProfileActive = false;
        _wasHorizontalAboveThreshold = false;
        _isFinished = false;
        _hasCapturedFinishSpeed = false;
        CurrentProfile = _defaultProfile;
        _flightController.ResetState(CurrentProfile);
    }

    // カウントダウン中はRaceManagerがTickを呼ばないため、ブースト・シールドは使えない。
    public void Tick(float deltaTime, IReadOnlyRacerData data)
    {
        if (!_isInitialized)
        {
            return;
        }

        _stun.Tick(deltaTime);
        _isFinished = data.IsFinished;

        // ゴール後は入力を無視し、ブースト・シールドを解除して、通常のProfileでまっすぐ飛ばしながら停止させる。
        // 速度の上限を「ゴールした瞬間の速度 × SpeedMultiplier」にすることで、RaceSettingsのカーブ通りに減速する
        // （途中でブーストが切れても落ち方が変わらないよう、ブーストの最高速度ではなくゴール時の速度を基準にする）。
        if (data.IsFinished)
        {
            _boost.ForceStop();
            _shield.ForceStop();
            _boost.Tick(deltaTime, false);
            _shield.Tick(deltaTime, false);

            if (!_hasCapturedFinishSpeed)
            {
                _speedAtFinish = _flightController.Velocity.magnitude;
                _hasCapturedFinishSpeed = true;
            }

            CurrentProfile = _defaultProfile;
            float finishMaxSpeed = _speedAtFinish * data.SpeedMultiplier;
            _flightController.Tick(0f, finishMaxSpeed, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, CurrentProfile, deltaTime);
            return;
        }

        IRaceInput input = _inputProvider.Current;
        if (input == null)
        {
            return;
        }

        // スタン中は入力を受け付けない（左右・ブースト・シールド・ショートカットのすべて）。
        bool canControl = !_stun.IsStunned;
        float horizontal = canControl ? input.Horizontal : 0f;

        if (canControl)
        {
            HandleBoostAndShieldInput(input, data.RankRatio);
        }

        _boost.Tick(deltaTime, canControl && input.BoostHeld);
        _shield.Tick(deltaTime, canControl && input.ShieldHeld);

        // ショートカットキー入力自体は常にログを出す（デバッグ用。トラッキング入力側の動作確認にも使う）。
        if (input.ShortcutPressed && _showDebugLogs)
        {
            Debug.Log("ショートカットした");
        }

        if (canControl)
        {
            UpdateShortcutTrigger(input, horizontal);
        }

        CurrentProfile = _isShortcutProfileActive ? _shortcutProfile : _defaultProfile;
        float maxSpeed = _boost.CurrentMaxSpeed * _shield.MaxSpeedMultiplier * GetStunSpeedMultiplier();
        _flightController.Tick(horizontal, maxSpeed, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, CurrentProfile, deltaTime);
    }

    // ブースト中に他のレーサーと重なったとき、BoostHitDetectorから呼ばれる。
    // ゴール後・無敵中 → 無視、お互いにブースト中 → 少しはじくだけ（ブーストは解除しない）、
    // シールド中 → 防いでシールドが消える、それ以外 → スタン＋横ずれ（中心どうしが近いほど大きく押し出す）。
    public void ReceiveBoostHit(BoostHitInfo hit)
    {
        if (!_isInitialized || _isFinished || _stun.IsInvincible)
        {
            return;
        }

        if (_boost.IsBoosting)
        {
            if (!_stun.CanBounce)
            {
                return;
            }

            _stun.BeginBounceInterval(_boostHitProfile.BounceInterval);
            _flightController.ApplyLateralPush(hit.PushDirection, _boostHitProfile.BounceDistance, _boostHitProfile.PushDuration);
            Log($"ブースト同士で接触（はじく） 距離 {_boostHitProfile.BounceDistance:F1}m");
            return;
        }

        if (_shield.TryBlock())
        {
            _stun.BeginInvincible(_boostHitProfile.InvincibleDuration);
            Log("シールドでブースト接触を防いだ");
            return;
        }

        _boost.ForceStop();
        _stun.BeginStun(_boostHitProfile.StunDuration, _boostHitProfile.InvincibleDuration);
        float pushDistance = _boostHitProfile.EvaluatePushDistance(hit.Closeness);
        _flightController.ApplyLateralPush(hit.PushDirection, pushDistance, _boostHitProfile.PushDuration);
        Log($"ブーストを当てられた（スタン） 近さ {hit.Closeness:F2} 横ずれ {pushDistance:F1}m{(hit.HasPushDirection ? "" : "（向き未確定→コース中央側）")}");
    }

    // 通常の接触で食い込んだとき、RacerContactResolverから呼ばれる。
    public void ApplyContactCorrection(Vector3 offset, Vector3 blockNormal)
    {
        _flightController.ApplyContactCorrection(offset, blockNormal);
    }

    // ShortcutZone（発動エリア）からの通知。エリアを離れたら吸着も強制的に解除する。
    public void SetInShortcutZone(ShortcutZone zone)
    {
        _currentShortcutZone = zone;
        if (zone == null)
        {
            _isShortcutProfileActive = false;
        }
    }

    // ブーストとシールドは同時に使えない。片方を使っている間は、もう片方の「押した」入力を受け付けない（先に押した方を優先）。
    // 開始には「押した瞬間」が必要なため、押しっぱなしのままGO・スタン明け・シールドの消失を迎えても自動では始まらない。
    private void HandleBoostAndShieldInput(IRaceInput input, float rankRatio)
    {
        if (input.BoostPressed && !_shield.IsActive && _boost.TryActivate(rankRatio))
        {
            Log($"ブースト発動 順位の割合 {rankRatio:F2} 消費 {_boost.ConsumptionPerSecond:F1}/秒 ゲージ {_boost.Gauge:F1}");
        }

        if (input.ShieldPressed && !_boost.IsBoosting && _shield.TryActivate())
        {
            Log("シールド発動");
        }
    }

    private float GetStunSpeedMultiplier()
    {
        return _stun.IsStunned ? _boostHitProfile.StunSpeedMultiplier : 1f;
    }

    // 実際の吸着発動は「発動エリア内であること」＋「そのエリアが指定するトリガー方式で入力があったこと」が条件。
    // どちらを使うか（Eキー／Horizontal閾値）はエリアごとにShortcutZone側で設定する。
    private void UpdateShortcutTrigger(IRaceInput input, float horizontal)
    {
        if (_currentShortcutZone == null)
        {
            return;
        }

        bool zoneTriggered;
        if (_currentShortcutZone.TriggerMethod == EShortcutTriggerMethod.HorizontalThreshold)
        {
            bool isAboveThreshold = Mathf.Abs(horizontal) >= _currentShortcutZone.HorizontalThreshold;
            zoneTriggered = isAboveThreshold && !_wasHorizontalAboveThreshold;
            _wasHorizontalAboveThreshold = isAboveThreshold;
        }
        else
        {
            zoneTriggered = input.ShortcutPressed;
        }

        if (zoneTriggered)
        {
            _isShortcutProfileActive = true;
        }
    }

    private void Log(string message)
    {
        if (_showDebugLogs)
        {
            Debug.Log($"PlayerManager: {message}", this);
        }
    }
}
