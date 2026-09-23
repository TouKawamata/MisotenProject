using UnityEngine;

// 飛行の慣性・補正・ロール系のチューニング値の塊。PlayerFlightControllerはこの値を引数で受け取って計算するだけで、
// どのProfileを使うか（通常／ショートカット吸着など）はPlayerManagerが判断して差し替える。
[CreateAssetMenu(fileName = "FlightTuningProfile", menuName = "Player/FlightTuningProfile")]
public class FlightTuningProfile : ScriptableObject
{
    [Header("速度の追従（慣性の強さ）")]
    [SerializeField] private float _headingTurnRate = 1.5f;
    [SerializeField] private float _velocityTurnRate = 2.0f;

    [Header("コース中央への補正（左右）")]
    [SerializeField] private float _centerCorrectionGain = 0.5f;
    [SerializeField] private float _maxCorrectionAccel = 20f;

    [Header("コース中央への補正（上下）")]
    [SerializeField] private float _verticalCorrectionGain = 0.5f;
    [SerializeField] private float _maxVerticalCorrectionAccel = 20f;

    [Header("ロール（バンク）")]
    [SerializeField] private float _rollMaxAngle = 25f;
    [SerializeField] private float _rollSmoothTime = 0.25f;

    [Header("カメラ用先読み")]
    [SerializeField] private float _lookAheadDistance = 40f;

    public float HeadingTurnRate => _headingTurnRate;

    public float VelocityTurnRate => _velocityTurnRate;

    public float CenterCorrectionGain => _centerCorrectionGain;

    public float MaxCorrectionAccel => _maxCorrectionAccel;

    public float VerticalCorrectionGain => _verticalCorrectionGain;

    public float MaxVerticalCorrectionAccel => _maxVerticalCorrectionAccel;

    public float RollMaxAngle => _rollMaxAngle;

    public float RollSmoothTime => _rollSmoothTime;

    public float LookAheadDistance => _lookAheadDistance;
}
