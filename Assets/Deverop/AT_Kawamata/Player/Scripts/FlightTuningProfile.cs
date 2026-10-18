using UnityEngine;
using UnityEngine.Serialization;

// 飛行の慣性・横移動・補正・壁系のチューニング値の塊。PlayerFlightControllerはこの値を引数で受け取って計算するだけで、
// どのProfileを使うか（通常／ショートカット吸着など）はPlayerManagerが判断して差し替える。
[CreateAssetMenu(fileName = "FlightTuningProfile", menuName = "Player/FlightTuningProfile")]
public class FlightTuningProfile : ScriptableObject
{
    [Header("速度の追従（慣性の強さ）")]
    [SerializeField] private float _headingTurnRate = 1.5f;
    [SerializeField] private float _velocityTurnRate = 2.0f;

    [Header("横移動")]
    [Tooltip("左右の入力を離した・弱めた・逆へ切り返したときに、横の速さを落とす加速度（m/s²）。大きいほどすぐ止まる。速くするときの加速度はBoostProfileのSteering Power")]
    [SerializeField] private float _lateralStopDeceleration = 200f;

    [Header("コース中央への補正（左右）")]
    [Tooltip("中央からのずれ1mあたり、中央へ戻る横の速さ（m/s）。左右の入力が小さいほど強くかかる")]
    [SerializeField] private float _centerCorrectionGain = 0.5f;
    [Tooltip("中央へ戻る横の速さの上限（m/s）")]
    [FormerlySerializedAs("_maxCorrectionAccel")]
    [SerializeField] private float _maxCorrectionSpeed = 20f;

    [Header("コース中央への補正（上下）")]
    [SerializeField] private float _verticalCorrectionGain = 0.5f;
    [SerializeField] private float _maxVerticalCorrectionAccel = 20f;

    [Header("カメラ用先読み")]
    [SerializeField] private float _lookAheadDistance = 40f;

    [Header("最高速度")]
    [Tooltip("横の速さのうち、最高速度に含める割合。前進の上限 ＝ 最高速度 − この値 × 横の速さ。0なら横に動いても前進の速さは落ちない")]
    [Range(0f, 1f)]
    [SerializeField] private float _lateralSpeedLimitRate = 0.25f;

    [Header("壁（トンネルの境界）")]
    [Tooltip("壁に当たったときの減速の強さ。減らす割合 ＝ この値 × 当たった角度のsin（正面から当たると1、かすめると0に近い）。0なら減速せずに滑る、1なら正面から当たると止まる")]
    [Range(0f, 1f)]
    [SerializeField] private float _wallHitSpeedLossRate = 0.5f;

    public float HeadingTurnRate => _headingTurnRate;

    public float VelocityTurnRate => _velocityTurnRate;

    public float LateralStopDeceleration => Mathf.Max(0f, _lateralStopDeceleration);

    public float CenterCorrectionGain => _centerCorrectionGain;

    public float MaxCorrectionSpeed => Mathf.Max(0f, _maxCorrectionSpeed);

    public float VerticalCorrectionGain => _verticalCorrectionGain;

    public float MaxVerticalCorrectionAccel => _maxVerticalCorrectionAccel;

    public float LookAheadDistance => _lookAheadDistance;

    public float LateralSpeedLimitRate => _lateralSpeedLimitRate;

    public float WallHitSpeedLossRate => _wallHitSpeedLossRate;
}
