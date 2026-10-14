using UnityEngine;

// ブーストの性能（通常時／ブースト時の速度・加速度・旋回性能）と、ゲージの設定の塊。
// 順位で変わるのはゲージの消費速度だけで、それはBoostRankTableが持つ。
[CreateAssetMenu(fileName = "BoostProfile", menuName = "Player/BoostProfile")]
public class BoostProfile : ScriptableObject
{
    [Header("通常時")]
    [SerializeField] private float _normalMaxSpeed = 40f;
    [SerializeField] private float _normalForwardAcceleration = 20f;
    [SerializeField] private float _normalSteeringPower = 100f;

    [Header("ブースト時")]
    [SerializeField] private float _boostMaxSpeed = 100f;
    [SerializeField] private float _boostForwardAcceleration = 70f;
    [SerializeField] private float _boostSteeringPower = 70f;
    [Tooltip("ブーストが終わってから、最高速度をブースト時の値から通常の値まで下げる秒数（0なら一瞬で下がる）。速度がガクッと落ちないようにする")]
    [SerializeField] private float _boostEndEaseDuration = 0.2f;

    [Header("ゲージ")]
    [Tooltip("ゲージの最大量。レース開始時は満タン")]
    [SerializeField] private float _maxGauge = 100f;
    [Tooltip("発動に必要な最低量。これ未満では発動できない（発動と終了を細かく繰り返すのを防ぐ）")]
    [SerializeField] private float _minGaugeToActivate = 10f;
    [Tooltip("1秒あたりの回復量")]
    [SerializeField] private float _recoveryPerSecond = 20f;
    [Tooltip("ブーストを離して終了してから、回復を始めるまでの秒数（この間も、ゲージが残っていれば再発動できる）")]
    [SerializeField] private float _recoveryDelay = 1f;
    [Tooltip("ゲージを使い切ったとき（オーバーヒート）に、回復を始めるまでの秒数。この間は発動できない")]
    [SerializeField] private float _overheatDelay = 3f;

    public float NormalMaxSpeed => _normalMaxSpeed;

    public float NormalForwardAcceleration => _normalForwardAcceleration;

    public float NormalSteeringPower => _normalSteeringPower;

    public float BoostMaxSpeed => _boostMaxSpeed;

    public float BoostForwardAcceleration => _boostForwardAcceleration;

    public float BoostSteeringPower => _boostSteeringPower;

    public float BoostEndEaseDuration => Mathf.Max(0f, _boostEndEaseDuration);

    public float MaxGauge => Mathf.Max(0.0001f, _maxGauge);

    public float MinGaugeToActivate => Mathf.Clamp(_minGaugeToActivate, 0f, MaxGauge);

    public float RecoveryPerSecond => Mathf.Max(0f, _recoveryPerSecond);

    public float RecoveryDelay => Mathf.Max(0f, _recoveryDelay);

    public float OverheatDelay => Mathf.Max(0f, _overheatDelay);
}
