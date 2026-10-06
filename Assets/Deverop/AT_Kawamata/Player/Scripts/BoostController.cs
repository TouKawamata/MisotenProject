using UnityEngine;

// ブーストのゲージと状態（Ready→Boosting→RecoveryDelay／Overheat→Ready）、現在有効な速度／加速度／旋回性能を管理する。
// 押した瞬間にTryActivateで開始し、押し続けている間（TickのisHeld）だけゲージを消費する。
// 消費速度は発動した瞬間の順位の割合でBoostRankTableから引き、そのブーストが終わるまで変えない。
// PlayerManagerがここから現在値を読んでPlayerFlightControllerへ渡すため、ブースト状態そのものは重複して持たない。
// ブースト終了時は、最高速度だけをBoostEndEaseDuration秒かけて通常の値まで下げる（速度がガクッと落ちないように）。
// Initialize/TryActivate/Tick/ForceStopはPlayerManagerから呼び出される。
public class BoostController : MonoBehaviour
{
    private enum EState
    {
        // ゲージが回復している（満タンなら何もしない）
        Ready,
        Boosting,
        // ブースト終了から回復を始めるまでの待ち時間。ゲージが残っていれば再発動できる
        RecoveryDelay,
        // ゲージを使い切ったときの待ち時間。発動できない
        Overheat
    }

    [SerializeField] private BoostProfile _profile;
    [SerializeField] private BoostRankTable _rankTable;

    private EState _state = EState.Ready;
    private float _stateTimer;

    // ブースト終了時に最高速度を下げている途中の残り秒数・全体の秒数と、下げ始めの値。
    private float _endEaseTimer;
    private float _endEaseDuration;
    private float _endEaseFromMaxSpeed;

    public float CurrentMaxSpeed { get; private set; }
    public float CurrentForwardAcceleration { get; private set; }
    public float CurrentSteeringPower { get; private set; }

    public float Gauge { get; private set; }

    public float MaxGauge => _profile != null ? _profile.MaxGauge : 0f;

    // 0～1。
    public float GaugeNormalized => MaxGauge > 0f ? Gauge / MaxGauge : 0f;

    // 直近に発動したときの順位の割合で確定した、1秒あたりの消費量。
    public float ConsumptionPerSecond { get; private set; }

    public bool IsBoosting => _state == EState.Boosting;

    // 回復を待っている間（終了直後の待ち時間、またはオーバーヒート）。
    public bool IsOnCooldown => _state == EState.RecoveryDelay || _state == EState.Overheat;

    public bool IsOverheated => _state == EState.Overheat;

    public bool CanActivate =>
        _profile != null && (_state == EState.Ready || _state == EState.RecoveryDelay) && Gauge >= _profile.MinGaugeToActivate;

    // 今のゲージで、あと何秒ブーストできるか。ブースト中でなければ0。
    public float BoostRemainingTime => _state == EState.Boosting && ConsumptionPerSecond > 0f ? Gauge / ConsumptionPerSecond : 0f;

    // 回復を始めるまでの残り秒数。待っていなければ0。
    public float CooldownRemainingTime => IsOnCooldown ? _stateTimer : 0f;

    public bool Initialize()
    {
        if (_profile == null || _rankTable == null)
        {
            Debug.LogError("BoostController: Boost Profile / Rank Table が未設定です", this);
            return false;
        }

        _state = EState.Ready;
        _stateTimer = 0f;
        Gauge = _profile.MaxGauge;
        ConsumptionPerSecond = 0f;
        ApplyNormalTuning();
        return true;
    }

    // rankRatio：発動した瞬間の順位の割合（0＝トップ、1＝最下位）。消費速度をここで確定する。
    public bool TryActivate(float rankRatio)
    {
        if (!CanActivate)
        {
            return false;
        }

        ConsumptionPerSecond = _rankTable.EvaluateConsumptionPerSecond(rankRatio);
        _state = EState.Boosting;
        _stateTimer = 0f;
        ApplyBoostTuning();
        return true;
    }

    // isHeld：ブーストの入力を押し続けているか。離したらブーストを終了する。
    public void Tick(float deltaTime, bool isHeld)
    {
        if (_profile == null)
        {
            return;
        }

        UpdateEndEase(deltaTime);

        switch (_state)
        {
            case EState.Boosting:
                if (!isHeld)
                {
                    EndBoost(EState.RecoveryDelay, _profile.RecoveryDelay);
                    break;
                }

                Gauge -= ConsumptionPerSecond * deltaTime;
                if (Gauge <= 0f)
                {
                    Gauge = 0f;
                    EndBoost(EState.Overheat, _profile.OverheatDelay);
                }

                break;

            case EState.RecoveryDelay:
            case EState.Overheat:
                _stateTimer -= deltaTime;
                if (_stateTimer <= 0f)
                {
                    _state = EState.Ready;
                    _stateTimer = 0f;
                }

                break;

            case EState.Ready:
                Gauge = Mathf.Min(_profile.MaxGauge, Gauge + _profile.RecoveryPerSecond * deltaTime);
                break;
        }
    }

    // 入力に関係なくブーストを終了する（ゴール時・スタン時）。使い切った扱いにはせず、通常の待ち時間に入る。
    public void ForceStop()
    {
        if (_state == EState.Boosting)
        {
            EndBoost(EState.RecoveryDelay, _profile.RecoveryDelay);
        }
    }

    // 加速度・旋回性能はすぐ通常の値に戻し、最高速度だけを下げ始める。
    private void EndBoost(EState nextState, float delay)
    {
        _state = nextState;
        _stateTimer = delay;

        float boostMaxSpeed = CurrentMaxSpeed;
        ApplyNormalTuning();

        if (_profile.BoostEndEaseDuration > 0f)
        {
            _endEaseFromMaxSpeed = boostMaxSpeed;
            _endEaseDuration = _profile.BoostEndEaseDuration;
            _endEaseTimer = _endEaseDuration;
            CurrentMaxSpeed = boostMaxSpeed;
        }
    }

    // ゆっくり動き出して、ゆっくり止まる（SmoothStep）形で、通常の最高速度まで下げる。
    private void UpdateEndEase(float deltaTime)
    {
        if (_endEaseTimer <= 0f)
        {
            return;
        }

        _endEaseTimer = Mathf.Max(0f, _endEaseTimer - deltaTime);
        float t = 1f - _endEaseTimer / _endEaseDuration;
        CurrentMaxSpeed = Mathf.Lerp(_endEaseFromMaxSpeed, _profile.NormalMaxSpeed, Mathf.SmoothStep(0f, 1f, t));
    }

    // 通常の値へ戻す（ブースト終了時の最高速度の下げ途中も打ち切る）。
    private void ApplyNormalTuning()
    {
        _endEaseTimer = 0f;
        CurrentMaxSpeed = _profile.NormalMaxSpeed;
        CurrentForwardAcceleration = _profile.NormalForwardAcceleration;
        CurrentSteeringPower = _profile.NormalSteeringPower;
    }

    // ブースト時の値にする（終了時の最高速度の下げ途中に再発動した場合も、ここで打ち切る）。
    private void ApplyBoostTuning()
    {
        _endEaseTimer = 0f;
        CurrentMaxSpeed = _profile.BoostMaxSpeed;
        CurrentForwardAcceleration = _profile.BoostForwardAcceleration;
        CurrentSteeringPower = _profile.BoostSteeringPower;
    }
}
