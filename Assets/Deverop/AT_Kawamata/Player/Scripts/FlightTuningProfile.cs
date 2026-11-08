using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

// 飛行の慣性・上下左右の移動・中央への戻り・壁・カメラ用の予測のチューニング値の塊。PlayerFlightControllerはこの値を引数で受け取って計算するだけ。
// PlayerとCPUで同じアセットを使う想定（移動の方向の方式の切り替えも、ここを1か所変えれば両方に効く）。
// コースの中央へは、移動の入力がニュートラルに近いときだけ、だんだん戻る（入力中は入力を優先する）。
[CreateAssetMenu(fileName = "FlightTuningProfile", menuName = "Player/FlightTuningProfile")]
public class FlightTuningProfile : ScriptableObject
{
    // 「ほぼ（約95%）追いつく」までに、時定数（1 ÷ 1mあたりの割合）の何倍かかるか（e^-3 ≒ 5%残り）。
    private const float FollowDistanceTimeConstants = 3f;

    [Header("速度の追従（慣性の強さ）。時間ではなく、コースの中心線に沿って進んだ距離で追いつく")]
    [Tooltip("機首がコースの向き（接線）へ、ほぼ（約95%）向き直るまでに、コースの中心線に沿って進む距離（m）。大きいほどゆっくり向き直る。" +
             "コースに沿って進んだ距離で決めるので、インコース・アウトコース・速さによらず、角を抜けるときの遅れ方がそろう。0なら遅れなし。" +
             "下の「入力中の追従」がONなら、入力していないときの値")]
    [Min(0f)]
    [SerializeField] private float _headingFollowDistance = 112.5f;

    [Tooltip("速度の向きが機首の向きへ、ほぼ（約95%）向き直るまでに、コースの中心線に沿って進む距離（m）。大きいほどカーブで外へ膨らむ（滑る）。0なら遅れなし。" +
             "下の「入力中の追従」がONなら、入力していないときの値")]
    [Min(0f)]
    [SerializeField] private float _velocityFollowDistance = 67.5f;

    [Header("入力中の追従（検証用）。入力している間だけ、上の2つの距離を別の値にする")]
    [Tooltip("ONなら、移動の入力がある間は下の2つの距離を使う（操作すればコースに沿って曲がりやすく、操作しなければ外へ膨らむ）。" +
             "OFFなら、入力に関係なく上の2つの距離を使う")]
    [SerializeField] private bool _useInputFollowDistance = true;

    [Tooltip("入力中の、機首がコースの向きへほぼ向き直るまでの距離（m）。0なら遅れなし")]
    [Min(0f)]
    [SerializeField] private float _inputHeadingFollowDistance = 0f;

    [Tooltip("入力中の、速度の向きが機首の向きへほぼ向き直るまでの距離（m）。0なら遅れなし")]
    [Min(0f)]
    [SerializeField] private float _inputVelocityFollowDistance = 30f;

    [Tooltip("入力をやめてから、入力中の距離から通常の距離へ戻し切るまでの秒数（入力し始めたときは、すぐに入力中の距離にする）。0なら一瞬で戻る")]
    [Min(0f)]
    [SerializeField] private float _inputFollowReturnDuration = 0.5f;

    [Tooltip("入力中の距離から通常の距離へ戻すときのイージング")]
    [SerializeField] private Ease _inputFollowReturnEase = Ease.InOutSine;

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

    // 入力していないとき（「入力中の追従」がOFFなら常に）の、機首・速度の向きの追従の距離（m）。
    public float HeadingFollowDistance => Mathf.Max(0f, _headingFollowDistance);

    public float VelocityFollowDistance => Mathf.Max(0f, _velocityFollowDistance);

    public bool UseInputFollowDistance => _useInputFollowDistance;

    // 入力中の、機首・速度の向きの追従の距離（m）。
    public float InputHeadingFollowDistance => Mathf.Max(0f, _inputHeadingFollowDistance);

    public float InputVelocityFollowDistance => Mathf.Max(0f, _inputVelocityFollowDistance);

    public float InputFollowReturnDuration => Mathf.Max(0f, _inputFollowReturnDuration);

    public Ease InputFollowReturnEase => _inputFollowReturnEase;

    // 追従の距離（m）から、コースの中心線に沿って1m進むごとに近づく割合（1/m）を求める（3 ÷ 距離で約95%）。距離が0なら無限大（遅れなし）。
    public static float ToFollowRatePerMeter(float followDistance)
    {
        return followDistance > 0f ? FollowDistanceTimeConstants / followDistance : float.PositiveInfinity;
    }

    public EMoveDirectionMode MoveDirectionMode => _moveDirectionMode;

    public float LateralStopDeceleration => Mathf.Max(0f, _lateralStopDeceleration);

    public float CenterCorrectionGain => Mathf.Max(0f, _centerCorrectionGain);

    public float MaxCorrectionSpeed => Mathf.Max(0f, _maxCorrectionSpeed);

    public float CameraPredictionTime => Mathf.Max(0f, _cameraPredictionTime);

    public float LookAheadDistance => _lookAheadDistance;

    public float LateralSpeedLimitRate => _lateralSpeedLimitRate;

    public float WallHitSpeedLossRate => _wallHitSpeedLossRate;
}
