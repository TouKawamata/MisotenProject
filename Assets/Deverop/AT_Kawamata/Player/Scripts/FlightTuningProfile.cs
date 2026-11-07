using UnityEngine;
using UnityEngine.Serialization;

// 飛行の慣性・上下左右の移動・中央への戻り・壁・カメラ用の予測のチューニング値の塊。PlayerFlightControllerはこの値を引数で受け取って計算するだけ。
// PlayerとCPUで同じアセットを使う想定（移動の方向の方式の切り替えも、ここを1か所変えれば両方に効く）。
// コースの中央へは、移動の入力がニュートラルに近いときだけ、だんだん戻る（入力中は入力を優先する）。
[CreateAssetMenu(fileName = "FlightTuningProfile", menuName = "Player/FlightTuningProfile")]
public class FlightTuningProfile : ScriptableObject
{
    [Header("速度の追従（慣性の強さ）")]
    [SerializeField] private float _headingTurnRate = 1.5f;
    [SerializeField] private float _velocityTurnRate = 2.0f;

    [Header("上下左右の移動")]
    [Tooltip("移動の方向の決め方。EightWay：45度刻みの8方向、FreeAngle：入力の角度そのまま（比較検証用）")]
    [SerializeField] private EMoveDirectionMode _moveDirectionMode = EMoveDirectionMode.FreeAngle;

    [Tooltip("移動の入力を離した・弱めた・別の向きへ切り替えたときに、上下左右の速さを落とす加速度（m/s²）。大きいほどすぐ止まる。速くするときの加速度はBoostProfileのSteering Power")]
    [SerializeField] private float _lateralStopDeceleration = 200f;

    [Header("コース中央への戻り（ニュートラル時）")]
    [Tooltip("中央からのずれ1mあたり、中央へ戻る速さ（m/s）。上下左右とも同じ。入力が小さいほど強くかかり、入力いっぱいなら0。0なら戻らない")]
    [Min(0f)]
    [SerializeField] private float _centerCorrectionGain = 0.5f;

    [Tooltip("中央へ戻る速さの上限（m/s）")]
    [FormerlySerializedAs("_maxCorrectionAccel")]
    [SerializeField] private float _maxCorrectionSpeed = 20f;

    [Header("カメラ用の予測位置")]
    [Tooltip("今の速さ（前進・上下左右）のまま何秒進んだときの位置を、カメラの狙い先にするか（秒）")]
    [Min(0f)]
    [SerializeField] private float _cameraPredictionTime = 0.5f;

    [Tooltip("カメラの狙い先を、コースに沿って最低何m先にするか（遅いときに、狙い先が近づきすぎないように）")]
    [SerializeField] private float _lookAheadDistance = 40f;

    [Header("最高速度")]
    [Tooltip("上下左右の速さのうち、最高速度に含める割合。前進の上限 ＝ 最高速度 − この値 × 上下左右の速さ。0なら動いても前進の速さは落ちない")]
    [Range(0f, 1f)]
    [SerializeField] private float _lateralSpeedLimitRate = 0.25f;

    [Header("壁（トンネルの境界）")]
    [Tooltip("壁に当たったときの減速の強さ。減らす割合 ＝ この値 × 当たった角度のsin（正面から当たると1、かすめると0に近い）。0なら減速せずに滑る、1なら正面から当たると止まる")]
    [Range(0f, 1f)]
    [SerializeField] private float _wallHitSpeedLossRate = 0.5f;

    public float HeadingTurnRate => _headingTurnRate;

    public float VelocityTurnRate => _velocityTurnRate;

    public EMoveDirectionMode MoveDirectionMode => _moveDirectionMode;

    public float LateralStopDeceleration => Mathf.Max(0f, _lateralStopDeceleration);

    public float CenterCorrectionGain => Mathf.Max(0f, _centerCorrectionGain);

    public float MaxCorrectionSpeed => Mathf.Max(0f, _maxCorrectionSpeed);

    public float CameraPredictionTime => Mathf.Max(0f, _cameraPredictionTime);

    public float LookAheadDistance => _lookAheadDistance;

    public float LateralSpeedLimitRate => _lateralSpeedLimitRate;

    public float WallHitSpeedLossRate => _wallHitSpeedLossRate;
}
