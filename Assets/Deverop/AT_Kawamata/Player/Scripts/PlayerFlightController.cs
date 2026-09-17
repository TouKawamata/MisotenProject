using UnityEngine;

// プレイヤーの飛行制御。SplineFollowModeで選んだパターン（A/B/C/E/F）に応じて
// 位置・向き・ロールの計算方法を切り替える検証用実装。
// CourseSplineへは基本的に ICourseGuide 経由で問い合わせるが、
// 距離ベースで進行するパターン（B/E）は courseSpline の距離ベースAPIを直接使う。
// A/B/Cはコース幅（CourseSpline.CourseWidth）の外に出られず、境界にぶつかっても
// 摩擦0で滑るだけ（前進方向の速度は失わず、外側へ押し出す成分だけを消す）。
// F（手動操作方式）はEと同じトンネル空間（CourseSpline.TunnelRadius）の中だけを移動できる
// （境界の扱いは同じ摩擦0スライド）。
[RequireComponent(typeof(BoostController))]
[RequireComponent(typeof(RaceInputProvider))]
public class PlayerFlightController : MonoBehaviour
{
    [SerializeField] private CourseSpline courseSpline;
    [SerializeField] private RaceInputProvider inputProvider;
    [SerializeField] private BoostController boost;

    [Header("検証パターン")]
    [SerializeField] private SplineFollowMode followMode = SplineFollowMode.FreeFlight;

    [Header("速度の追従（慣性の強さ）")]
    [SerializeField] private float headingTurnRate = 1.5f;
    [SerializeField] private float velocityTurnRate = 2.0f;

    [Header("コース中央への補正（パターンA/Cで使用）")]
    [SerializeField] private float centerCorrectionGain = 0.5f;
    [SerializeField] private float maxCorrectionAccel = 20f;

    [Header("ロール（バンク）")]
    [SerializeField] private float rollMaxAngle = 25f;
    [SerializeField] private float rollSmoothTime = 0.25f;

    [Header("カメラ用先読み")]
    [SerializeField] private float lookAheadDistance = 40f;

    [Header("パターンB：路面方式")]
    [SerializeField] private float roadLateralSpeed = 15f;

    [Header("パターンE：トンネル方式")]
    [SerializeField] private float tunnelLateralSpeed = 15f;
    [SerializeField] private float tunnelVerticalSpeed = 15f;

    [Header("パターンF：手動操作方式")]
    [SerializeField] private float manualTurnRate = 90f;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _currentRoll;
    private float _rollVelocity;
    private float _targetRoll;

    // パターンB/E（距離ベース進行）専用の状態。_forwardSpeedはFでも使う。
    private float _distanceAlongSpline;
    private float _forwardSpeed;
    private float _lateralOffset;
    private float _verticalOffset;

    public Vector3 Velocity => _velocity;

    public float CurrentRoll => _currentRoll;

    public Vector3 LookAheadWorldPosition { get; private set; }

    private void Awake()
    {
        if (boost == null)
        {
            boost = GetComponent<BoostController>();
        }

        if (inputProvider == null)
        {
            inputProvider = GetComponent<RaceInputProvider>();
        }

        _guide = courseSpline;
        _playerForward = transform.forward;
        LookAheadWorldPosition = transform.position + transform.forward * lookAheadDistance;

        if (courseSpline != null)
        {
            _distanceAlongSpline = courseSpline.FindNearestDistance(transform.position);
        }
    }

    private void Update()
    {
        if (_guide == null || inputProvider == null || boost == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        IRaceInput input = inputProvider.Current;
        if (input == null)
        {
            return;
        }

        // 1. 入力取得
        float horizontal = input.Horizontal;
        bool boostPressed = input.BoostPressed;

        // 2. ブースト発動
        if (boostPressed)
        {
            boost.TryActivate();
        }

        // シールドは今回デバッグログ出力のみ。状態管理は一切持たない。
        if (input.ShieldPressed)
        {
            Debug.Log("[Shield] Pressed (debug only, no state yet)");
        }

        // 3. 現在のチューニング値（Boost状態に応じてBoostControllerが持つ）
        float maxSpeed = boost.CurrentMaxSpeed;
        float forwardAcceleration = boost.CurrentForwardAcceleration;
        float steeringPower = boost.CurrentSteeringPower;

        switch (followMode)
        {
            case SplineFollowMode.RoadSurface:
                UpdatePatternB_RoadSurface(dt, horizontal, maxSpeed, forwardAcceleration);
                break;
            case SplineFollowMode.FreeFlightWithRoadAttitude:
                UpdatePatternC_FreeFlightWithRoadAttitude(dt, horizontal, maxSpeed, forwardAcceleration, steeringPower);
                break;
            case SplineFollowMode.Tunnel:
                UpdatePatternE_Tunnel(dt, horizontal, input.Vertical, maxSpeed, forwardAcceleration);
                break;
            case SplineFollowMode.ManualDrive:
                UpdatePatternF_ManualDrive(dt, horizontal, input.Vertical, maxSpeed, forwardAcceleration);
                break;
            default:
                UpdatePatternA_FreeFlight(dt, horizontal, maxSpeed, forwardAcceleration, steeringPower);
                break;
        }

        // 全パターン共通：ロールはSmoothDampで滑らかに追従させ、Forward+ロールで最終的な回転を組み立てる。
        _currentRoll = Mathf.SmoothDamp(_currentRoll, _targetRoll, ref _rollVelocity, rollSmoothTime);
        transform.rotation = Quaternion.LookRotation(_playerForward, Vector3.up) * Quaternion.AngleAxis(_currentRoll, Vector3.forward);

        UpdateLookAhead();
    }

    // A: 現状方式。Splineへは固定せず、Velocityベースで自由に飛行する。
    private void UpdatePatternA_FreeFlight(float dt, float horizontal, float maxSpeed, float forwardAcceleration, float steeringPower)
    {
        Vector3 position = transform.position;
        Vector3 splineForward = _guide.GetForward(position);
        Vector3 splineRight = _guide.GetRight(position);
        Vector3 centerPosition = _guide.GetCenterPosition(position);

        // 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-headingTurnRate * dt));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 lateralAccel = splineRight * (horizontal * steeringPower);
        Vector3 centerOffset = centerPosition - position;
        Vector3 correctionAccel = Vector3.ClampMagnitude(centerOffset * centerCorrectionGain, maxCorrectionAccel);

        _velocity += (forwardAccel + lateralAccel + correctionAccel) * dt;
        _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);

        // 速度方向の遅延追従（第2段階。カーブで外側へ膨らんでから戻る「滑る」感）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, 1f - Mathf.Exp(-velocityTurnRate * dt));
            _velocity = velocityDirection.normalized * speed;
        }

        // (将来の慣性オフセット・バネ系を追加する場合はこの直後にオフセットを加算する)
        Vector3 desiredPosition = position + _velocity * dt;
        transform.position = ClampVelocityAndPositionToCourseWidth(desiredPosition, centerPosition, splineRight, ref _velocity);

        _targetRoll = -horizontal * rollMaxAngle;
    }

    // B: 路面方式。Splineを道路の中心として扱い、左右移動はRight、高さはUpに追従させる。
    private void UpdatePatternB_RoadSurface(float dt, float horizontal, float maxSpeed, float forwardAcceleration)
    {
        _forwardSpeed = Mathf.MoveTowards(_forwardSpeed, maxSpeed, forwardAcceleration * dt);
        _distanceAlongSpline += _forwardSpeed * dt;
        _lateralOffset += horizontal * roadLateralSpeed * dt;
        _lateralOffset = ClampLateralOffsetToCourseWidth(_lateralOffset);

        Vector3 center = courseSpline.EvaluatePositionByDistance(_distanceAlongSpline);
        Vector3 forward = courseSpline.EvaluateForwardByDistance(_distanceAlongSpline);
        Vector3 right = courseSpline.EvaluateRightByDistance(_distanceAlongSpline);
        Vector3 up = courseSpline.EvaluateUpByDistance(_distanceAlongSpline);

        transform.position = center + right * _lateralOffset;

        _playerForward = Vector3.Slerp(_playerForward, forward, 1f - Mathf.Exp(-headingTurnRate * dt));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = forward;
        }

        // 現状のCourseSplineはRight/UpをForwardとワールドUpから導出しているため、
        // バンク角は常にほぼ0にしかならない（制御点にロール情報を持たせる拡張は別スコープ）。
        // Splineが将来バンクデータを持つようになれば、ここは無改修で追従する。
        _targetRoll = Vector3.SignedAngle(Vector3.up, up, _playerForward);
    }

    // C: 自由飛行＋路面姿勢。位置・速度はAと同じ自由飛行だが、姿勢はSplineの傾きに追従させる。
    private void UpdatePatternC_FreeFlightWithRoadAttitude(float dt, float horizontal, float maxSpeed, float forwardAcceleration, float steeringPower)
    {
        UpdatePatternA_FreeFlight(dt, horizontal, maxSpeed, forwardAcceleration, steeringPower);

        Vector3 splineUp = _guide.GetUp(transform.position);
        float bankFromCourse = Vector3.SignedAngle(Vector3.up, splineUp, _playerForward);
        float steeringRoll = -horizontal * rollMaxAngle;
        _targetRoll = Mathf.Clamp(bankFromCourse + steeringRoll, -rollMaxAngle, rollMaxAngle);
    }

    // E: トンネル方式。Splineを中心軸として、その周囲を上下左右に自由移動する。
    private void UpdatePatternE_Tunnel(float dt, float horizontal, float vertical, float maxSpeed, float forwardAcceleration)
    {
        _forwardSpeed = Mathf.MoveTowards(_forwardSpeed, maxSpeed, forwardAcceleration * dt);
        _distanceAlongSpline += _forwardSpeed * dt;
        _lateralOffset += horizontal * tunnelLateralSpeed * dt;
        _verticalOffset += vertical * tunnelVerticalSpeed * dt;

        Vector3 center = courseSpline.EvaluatePositionByDistance(_distanceAlongSpline);
        Vector3 forward = courseSpline.EvaluateForwardByDistance(_distanceAlongSpline);
        Vector3 right = courseSpline.EvaluateRightByDistance(_distanceAlongSpline);
        Vector3 up = courseSpline.EvaluateUpByDistance(_distanceAlongSpline);

        // 半径Clamp（courseSpline.TunnelRadius）は今回未実装。Gizmosでの目視確認のみ行う。
        transform.position = center + right * _lateralOffset + up * _verticalOffset;
        _playerForward = forward;
        _targetRoll = -horizontal * rollMaxAngle;
    }

    // F: 手動操作方式。Eと同じトンネル空間の中を、操作した向き（ヨー＋ピッチ）へそのまま前進する。
    private void UpdatePatternF_ManualDrive(float dt, float horizontal, float vertical, float maxSpeed, float forwardAcceleration)
    {
        Vector3 localRight = Vector3.Cross(Vector3.up, _playerForward);
        if (localRight.sqrMagnitude < 0.0001f)
        {
            localRight = transform.right;
        }
        localRight.Normalize();

        Quaternion yaw = Quaternion.AngleAxis(horizontal * manualTurnRate * dt, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(-vertical * manualTurnRate * dt, localRight);
        _playerForward = pitch * yaw * _playerForward;
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = transform.forward;
        }
        _playerForward.Normalize();

        _forwardSpeed = Mathf.MoveTowards(_forwardSpeed, maxSpeed, forwardAcceleration * dt);
        Vector3 desiredPosition = transform.position + _playerForward * _forwardSpeed * dt;

        Vector3 center = _guide.GetCenterPosition(desiredPosition);
        Vector3 right = _guide.GetRight(desiredPosition);
        Vector3 up = _guide.GetUp(desiredPosition);
        transform.position = ClampPositionToTunnelRadius(desiredPosition, center, right, up);

        _targetRoll = -horizontal * rollMaxAngle;
    }

    // コース幅の外に出られないようにする（frictionless slide: 境界の外側へ押し出す速度成分だけを消す）。
    private Vector3 ClampVelocityAndPositionToCourseWidth(Vector3 desiredPosition, Vector3 center, Vector3 right, ref Vector3 velocity)
    {
        if (courseSpline == null || courseSpline.CourseWidth <= 0f)
        {
            return desiredPosition;
        }

        float halfWidth = courseSpline.CourseWidth * 0.5f;
        float lateral = Vector3.Dot(desiredPosition - center, right);

        if (Mathf.Abs(lateral) <= halfWidth)
        {
            return desiredPosition;
        }

        float clampedLateral = Mathf.Clamp(lateral, -halfWidth, halfWidth);
        Vector3 correctedPosition = desiredPosition + right * (clampedLateral - lateral);

        float velocityAlongRight = Vector3.Dot(velocity, right);
        bool pushingOutward = (lateral > halfWidth && velocityAlongRight > 0f) || (lateral < -halfWidth && velocityAlongRight < 0f);
        if (pushingOutward)
        {
            velocity -= right * velocityAlongRight;
        }

        return correctedPosition;
    }

    // パターンF用：Right/Up平面上の円（TunnelRadius）の外に出られないようにする（frictionless slide）。
    // Fは持続的なVelocityを持たないため、位置を境界の内側へ戻すだけで径方向成分が消え、
    // 接線方向（前進方向を含む）の移動量はそのまま残る。
    private Vector3 ClampPositionToTunnelRadius(Vector3 desiredPosition, Vector3 center, Vector3 right, Vector3 up)
    {
        if (courseSpline == null || courseSpline.TunnelRadius <= 0f)
        {
            return desiredPosition;
        }

        float radius = courseSpline.TunnelRadius;
        Vector3 offset = desiredPosition - center;
        float lateral = Vector3.Dot(offset, right);
        float vertical = Vector3.Dot(offset, up);
        float distance = Mathf.Sqrt(lateral * lateral + vertical * vertical);

        if (distance <= radius || distance < 0.0001f)
        {
            return desiredPosition;
        }

        float scale = radius / distance;
        return desiredPosition + right * (lateral * scale - lateral) + up * (vertical * scale - vertical);
    }

    // パターンB用：スカラーのオフセットをコース幅の範囲にClampするだけ（前進距離とは独立しているため、
    // このClampだけで自動的にfrictionless slideになる）。
    private float ClampLateralOffsetToCourseWidth(float lateralOffset)
    {
        if (courseSpline == null || courseSpline.CourseWidth <= 0f)
        {
            return lateralOffset;
        }

        float halfWidth = courseSpline.CourseWidth * 0.5f;
        return Mathf.Clamp(lateralOffset, -halfWidth, halfWidth);
    }

    private void UpdateLookAhead()
    {
        if (followMode == SplineFollowMode.FreeFlight || followMode == SplineFollowMode.FreeFlightWithRoadAttitude || followMode == SplineFollowMode.ManualDrive)
        {
            LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, lookAheadDistance);
            return;
        }

        // 距離ベースで進行するパターンは、最近傍探索（ヘアピンでのジャンプの可能性）を避けて
        // 自前で管理している距離をそのまま使う。
        Vector3 offsetFromCenter = transform.position - courseSpline.EvaluatePositionByDistance(_distanceAlongSpline);
        LookAheadWorldPosition = courseSpline.EvaluateLookAhead(_distanceAlongSpline, lookAheadDistance) + offsetFromCenter;
    }
}
