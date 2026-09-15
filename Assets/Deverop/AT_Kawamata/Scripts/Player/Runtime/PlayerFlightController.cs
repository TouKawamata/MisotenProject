using UnityEngine;

// プレイヤーの飛行制御。Splineへは固定せず、Velocityベースで自由に飛行する。
// CourseSplineへは ICourseGuide 経由でのみ問い合わせる（具象型に依存しない）。
[RequireComponent(typeof(BoostController))]
[RequireComponent(typeof(RaceInputProvider))]
public class PlayerFlightController : MonoBehaviour
{
    [SerializeField] private CourseSpline courseSpline;
    [SerializeField] private RaceInputProvider inputProvider;
    [SerializeField] private BoostController boost;

    [Header("見出し・速度の追従（慣性の強さ）")]
    [SerializeField] private float headingTurnRate = 1.5f;
    [SerializeField] private float velocityTurnRate = 2.0f;

    [Header("コース中央への補正")]
    [SerializeField] private float centerCorrectionGain = 0.5f;
    [SerializeField] private float maxCorrectionAccel = 20f;

    [Header("ロール（バンク）")]
    [SerializeField] private float rollMaxAngle = 25f;
    [SerializeField] private float rollSmoothTime = 0.25f;

    [Header("カメラ用先読み")]
    [SerializeField] private float lookAheadDistance = 40f;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _currentRoll;
    private float _rollVelocity;

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

        // 3. 現在のチューニング値（Boost状態に応じてBoostControllerが持つ）
        float maxSpeed = boost.CurrentMaxSpeed;
        float forwardAcceleration = boost.CurrentForwardAcceleration;
        float steeringPower = boost.CurrentSteeringPower;

        // 4. 現在位置でのSpline基準値
        Vector3 position = transform.position;
        Vector3 splineForward = _guide.GetForward(position);
        Vector3 splineRight = _guide.GetRight(position);
        Vector3 centerPosition = _guide.GetCenterPosition(position);

        // 5. 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-headingTurnRate * dt));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 6. 加速度計算
        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 lateralAccel = splineRight * (horizontal * steeringPower);
        Vector3 centerOffset = centerPosition - position;
        Vector3 correctionAccel = Vector3.ClampMagnitude(centerOffset * centerCorrectionGain, maxCorrectionAccel);

        // 7. 速度積分
        _velocity += (forwardAccel + lateralAccel + correctionAccel) * dt;
        _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);

        // 8. 速度方向の遅延追従（第2段階。カーブで外側へ膨らんでから戻る「滑る」感）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, 1f - Mathf.Exp(-velocityTurnRate * dt));
            _velocity = velocityDirection.normalized * speed;
        }

        // 9. 位置積分
        // (将来の慣性オフセット・バネ系を追加する場合はこの直後にオフセットを加算する)
        transform.position = position + _velocity * dt;

        // 10. 回転（Yaw/PitchはplayerForwardから、Rollは横入力から別途計算して合成）
        Quaternion headingRotation = Quaternion.LookRotation(_playerForward, Vector3.up);
        float targetRoll = -horizontal * rollMaxAngle;
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, rollSmoothTime);
        transform.rotation = headingRotation * Quaternion.AngleAxis(_currentRoll, Vector3.forward);

        // 11. カメラへ公開する先読み位置
        LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, lookAheadDistance);
    }
}
