using UnityEngine;
using UnityEngine.Serialization;

// プレイヤーの飛行制御（純粋な飛行計算のみ）。自由飛行（Velocityベース）で移動しつつ、姿勢（ロール）はSplineの傾き
// （バンク）に自然に追従する。CourseSplineへは基本的に ICourseGuide 経由で問い合わせる。
// Splineを中心軸とした円筒（CourseSpline.TunnelRadius）の外には出られず、境界にぶつかっても
// 摩擦0で滑るだけ（前進方向の速度は失わず、外側へ押し出す成分だけを消す）。
// 入力・Boostのチューニング値・FlightTuningProfileはPlayerManagerからTickの引数で受け取る。
public class PlayerFlightController : MonoBehaviour
{
    [FormerlySerializedAs("courseSpline")]
    [SerializeField] private CourseSpline _courseSpline;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _currentRoll;
    private float _rollVelocity;

    public Vector3 Velocity => _velocity;

    public float CurrentRoll => _currentRoll;

    public Vector3 LookAheadWorldPosition { get; private set; }

    // 直近のTickで、トンネル境界の外に出ようとして位置を戻したか。
    public bool IsClampedToTunnel { get; private set; }

    public void Initialize(FlightTuningProfile initialProfile)
    {
        _guide = _courseSpline;
        _playerForward = transform.forward;
        LookAheadWorldPosition = transform.position + transform.forward * initialProfile.LookAheadDistance;
    }

    public void Tick(float horizontal, float maxSpeed, float forwardAcceleration, float steeringPower, FlightTuningProfile profile, float deltaTime)
    {
        if (_guide == null || profile == null)
        {
            return;
        }

        // 1. 現在位置でのSpline基準値
        Vector3 position = transform.position;
        Vector3 splineForward = _guide.GetForward(position);
        Vector3 splineRight = _guide.GetRight(position);
        Vector3 splineUp = _guide.GetUp(position);
        Vector3 centerPosition = _guide.GetCenterPosition(position);

        // 2. 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-profile.HeadingTurnRate * deltaTime));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 3. 加速度計算（コース中央への補正は左右・上下で別々の強さを持てるように分けて計算する）
        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 lateralAccel = splineRight * (horizontal * steeringPower);
        Vector3 centerOffset = centerPosition - position;
        float lateralCenterOffset = Vector3.Dot(centerOffset, splineRight);
        float verticalCenterOffset = Vector3.Dot(centerOffset, splineUp);
        Vector3 lateralCorrectionAccel = splineRight * Mathf.Clamp(lateralCenterOffset * profile.CenterCorrectionGain, -profile.MaxCorrectionAccel, profile.MaxCorrectionAccel);
        Vector3 verticalCorrectionAccel = splineUp * Mathf.Clamp(verticalCenterOffset * profile.VerticalCorrectionGain, -profile.MaxVerticalCorrectionAccel, profile.MaxVerticalCorrectionAccel);
        Vector3 correctionAccel = lateralCorrectionAccel + verticalCorrectionAccel;

        // 4. 速度積分
        _velocity += (forwardAccel + lateralAccel + correctionAccel) * deltaTime;
        _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);

        // 5. 速度方向の遅延追従（第2段階。カーブで外側へ膨らんでから戻る「滑る」感）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, 1f - Mathf.Exp(-profile.VelocityTurnRate * deltaTime));
            _velocity = velocityDirection.normalized * speed;
        }

        // 6. 位置積分（円筒境界の外には出られず、境界では摩擦0で滑る）
        Vector3 desiredPosition = position + _velocity * deltaTime;
        transform.position = ClampVelocityAndPositionToTunnelRadius(desiredPosition, centerPosition, splineRight, splineUp, ref _velocity);

        // 7. 回転：ForwardはPlayerForwardから、Rollは「Splineのバンクへの追従」＋「操作によるロール」の合算
        Vector3 bankUp = _guide.GetUp(transform.position);
        float bankFromCourse = Vector3.SignedAngle(Vector3.up, bankUp, _playerForward);
        float steeringRoll = -horizontal * profile.RollMaxAngle;
        float targetRoll = Mathf.Clamp(bankFromCourse + steeringRoll, -profile.RollMaxAngle, profile.RollMaxAngle);
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, profile.RollSmoothTime, Mathf.Infinity, deltaTime);
        transform.rotation = Quaternion.LookRotation(_playerForward, Vector3.up) * Quaternion.AngleAxis(_currentRoll, Vector3.forward);

        // 8. カメラへ公開する先読み位置
        LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, profile.LookAheadDistance);
    }

    // Splineを中心軸とした円筒（TunnelRadius）の外に出られないようにする（frictionless slide:
    // 境界の外側へ押し出す速度成分だけを消す。前進方向などの接線成分は失わない）。
    private Vector3 ClampVelocityAndPositionToTunnelRadius(Vector3 desiredPosition, Vector3 center, Vector3 right, Vector3 up, ref Vector3 velocity)
    {
        IsClampedToTunnel = false;
        if (_courseSpline == null || _courseSpline.TunnelRadius <= 0f)
        {
            return desiredPosition;
        }

        float radius = _courseSpline.TunnelRadius;
        Vector3 offset = desiredPosition - center;
        float lateral = Vector3.Dot(offset, right);
        float vertical = Vector3.Dot(offset, up);
        float distance = Mathf.Sqrt(lateral * lateral + vertical * vertical);

        if (distance <= radius || distance < 0.0001f)
        {
            return desiredPosition;
        }

        float scale = radius / distance;
        Vector3 correctedPosition = desiredPosition + right * (lateral * scale - lateral) + up * (vertical * scale - vertical);

        Vector3 radialDir = (right * lateral + up * vertical) / distance;
        float velocityAlongRadial = Vector3.Dot(velocity, radialDir);
        if (velocityAlongRadial > 0f)
        {
            velocity -= radialDir * velocityAlongRadial;
        }

        IsClampedToTunnel = true;
        return correctedPosition;
    }
}
