using UnityEngine;
using UnityEngine.Serialization;

// ブーストの状態（Idle→Boosting→Cooldown→Idle）と、現在有効な速度／加速度／旋回性能の
// チューニング値を管理する。PlayerManagerがここから現在値を読んでPlayerFlightControllerへ渡すため、
// ブースト状態そのものは重複して持たない。Initialize/TickはPlayerManagerから呼び出される。
public class BoostController : MonoBehaviour
{
    private enum EState
    {
        Idle,
        Boosting,
        Cooldown
    }

    [FormerlySerializedAs("normalMaxSpeed")]
    [SerializeField] private float _normalMaxSpeed = 40f;
    [FormerlySerializedAs("normalForwardAcceleration")]
    [SerializeField] private float _normalForwardAcceleration = 20f;
    [FormerlySerializedAs("normalSteeringPower")]
    [SerializeField] private float _normalSteeringPower = 18f;

    [FormerlySerializedAs("boostMaxSpeed")]
    [SerializeField] private float _boostMaxSpeed = 70f;
    [FormerlySerializedAs("boostForwardAcceleration")]
    [SerializeField] private float _boostForwardAcceleration = 45f;
    [FormerlySerializedAs("boostSteeringPower")]
    [SerializeField] private float _boostSteeringPower = 10f;

    [FormerlySerializedAs("boostDuration")]
    [SerializeField] private float _boostDuration = 3f;
    [FormerlySerializedAs("cooldownDuration")]
    [SerializeField] private float _cooldownDuration = 5f;

    private EState _state = EState.Idle;
    private float _stateTimer;

    public float CurrentMaxSpeed { get; private set; }
    public float CurrentForwardAcceleration { get; private set; }
    public float CurrentSteeringPower { get; private set; }

    public bool IsBoosting => _state == EState.Boosting;
    public bool IsOnCooldown => _state == EState.Cooldown;

    // ブースト中でなければ0。
    public float BoostRemainingTime => _state == EState.Boosting ? _stateTimer : 0f;

    // クールダウン中でなければ0。
    public float CooldownRemainingTime => _state == EState.Cooldown ? _stateTimer : 0f;

    public void Initialize()
    {
        _state = EState.Idle;
        _stateTimer = 0f;
        ApplyNormalTuning();
    }

    // Idle状態のときだけ発動に成功する。
    public bool TryActivate()
    {
        if (_state != EState.Idle)
        {
            return false;
        }

        _state = EState.Boosting;
        _stateTimer = _boostDuration;
        ApplyBoostTuning();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (_state == EState.Idle)
        {
            return;
        }

        _stateTimer -= deltaTime;
        if (_stateTimer > 0f)
        {
            return;
        }

        if (_state == EState.Boosting)
        {
            _state = EState.Cooldown;
            _stateTimer = _cooldownDuration;
            ApplyNormalTuning();
        }
        else if (_state == EState.Cooldown)
        {
            _state = EState.Idle;
        }
    }

    private void ApplyNormalTuning()
    {
        CurrentMaxSpeed = _normalMaxSpeed;
        CurrentForwardAcceleration = _normalForwardAcceleration;
        CurrentSteeringPower = _normalSteeringPower;
    }

    private void ApplyBoostTuning()
    {
        CurrentMaxSpeed = _boostMaxSpeed;
        CurrentForwardAcceleration = _boostForwardAcceleration;
        CurrentSteeringPower = _boostSteeringPower;
    }
}
