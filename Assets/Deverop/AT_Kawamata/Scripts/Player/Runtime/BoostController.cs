using UnityEngine;

// ブーストの状態（Idle→Boosting→Cooldown→Idle）と、現在有効な速度／加速度／旋回性能の
// チューニング値を管理する。PlayerFlightControllerはここから現在値を読むだけで、
// ブースト状態そのものは重複して持たない。
public class BoostController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Boosting,
        Cooldown
    }

    [SerializeField] private float normalMaxSpeed = 40f;
    [SerializeField] private float normalForwardAcceleration = 20f;
    [SerializeField] private float normalSteeringPower = 18f;

    [SerializeField] private float boostMaxSpeed = 70f;
    [SerializeField] private float boostForwardAcceleration = 45f;
    [SerializeField] private float boostSteeringPower = 10f;

    [SerializeField] private float boostDuration = 3f;
    [SerializeField] private float cooldownDuration = 5f;

    private State _state = State.Idle;
    private float _stateTimer;

    public float CurrentMaxSpeed { get; private set; }
    public float CurrentForwardAcceleration { get; private set; }
    public float CurrentSteeringPower { get; private set; }

    public bool IsBoosting => _state == State.Boosting;
    public bool IsOnCooldown => _state == State.Cooldown;

    private void Awake()
    {
        ApplyNormalTuning();
    }

    // Idle状態のときだけ発動に成功する。
    public bool TryActivate()
    {
        if (_state != State.Idle)
        {
            return false;
        }

        _state = State.Boosting;
        _stateTimer = boostDuration;
        ApplyBoostTuning();
        return true;
    }

    private void Update()
    {
        if (_state == State.Idle)
        {
            return;
        }

        _stateTimer -= Time.deltaTime;
        if (_stateTimer > 0f)
        {
            return;
        }

        if (_state == State.Boosting)
        {
            _state = State.Cooldown;
            _stateTimer = cooldownDuration;
            ApplyNormalTuning();
        }
        else if (_state == State.Cooldown)
        {
            _state = State.Idle;
        }
    }

    private void ApplyNormalTuning()
    {
        CurrentMaxSpeed = normalMaxSpeed;
        CurrentForwardAcceleration = normalForwardAcceleration;
        CurrentSteeringPower = normalSteeringPower;
    }

    private void ApplyBoostTuning()
    {
        CurrentMaxSpeed = boostMaxSpeed;
        CurrentForwardAcceleration = boostForwardAcceleration;
        CurrentSteeringPower = boostSteeringPower;
    }
}
