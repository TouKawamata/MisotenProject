using UnityEngine;

// プレイヤーの飛行制御。自由飛行（Velocityベース）で移動しつつ、姿勢（ロール）はSplineの傾き
// （バンク）に自然に追従する。CourseSplineへは基本的に ICourseGuide 経由で問い合わせる。
// Splineを中心軸とした円筒（CourseSpline.TunnelRadius）の外には出られず、境界にぶつかっても
// 摩擦0で滑るだけ（前進方向の速度は失わず、外側へ押し出す成分だけを消す）。
[RequireComponent(typeof(BoostController))]
[RequireComponent(typeof(RaceInputProvider))]
public class PlayerFlightController : MonoBehaviour, IShortcutZoneReceiver
{
    [SerializeField] private CourseSpline courseSpline;
    [SerializeField] private RaceInputProvider inputProvider;
    [SerializeField] private BoostController boost;

    [Header("速度の追従（慣性の強さ）")]
    [SerializeField] private float headingTurnRate = 1.5f;
    [SerializeField] private float velocityTurnRate = 2.0f;

    [Header("コース中央への補正（左右）")]
    [SerializeField] private float centerCorrectionGain = 0.5f;
    [SerializeField] private float maxCorrectionAccel = 20f;

    [Header("コース中央への補正（上下）")]
    [SerializeField] private float verticalCorrectionGain = 0.5f;
    [SerializeField] private float maxVerticalCorrectionAccel = 20f;

    [Header("ロール（バンク）")]
    [SerializeField] private float rollMaxAngle = 25f;
    [SerializeField] private float rollSmoothTime = 0.25f;

    [Header("カメラ用先読み")]
    [SerializeField] private float lookAheadDistance = 40f;

    [Header("ショートカット判定（Splineへの吸着）")]
    [Tooltip("ShortcutZoneの発動エリア内でショートカット入力があると、上下補正がこの強さに切り替わりSplineの高さへ強く吸着する")]
    [SerializeField] private float heightSnapCorrectionGain = 20f;
    [SerializeField] private float maxHeightSnapCorrectionAccel = 200f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _currentRoll;
    private float _rollVelocity;
    private ShortcutZone _currentShortcutZone;
    private bool _heightSnapActive;
    private bool _wasHorizontalAboveThreshold;

    public Vector3 Velocity => _velocity;

    public float CurrentRoll => _currentRoll;

    public Vector3 LookAheadWorldPosition { get; private set; }

    // ShortcutZone（発動エリア）からの通知。エリアを離れたら吸着も強制的に解除する。
    public void SetInShortcutZone(ShortcutZone zone)
    {
        _currentShortcutZone = zone;
        if (zone == null)
        {
            _heightSnapActive = false;
        }
    }

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

        // シールドは今回デバッグログ出力のみ。状態管理は一切持たない。
        if (input.ShieldPressed)
        {
            if (showDebugLogs)
            {
                Debug.Log("シールド発動した");
            }
        }

        // ショートカットキー入力自体は常にログを出す（デバッグ用。トラッキング入力側の動作確認にも使う）。
        if (input.ShortcutPressed && showDebugLogs)
        {
            Debug.Log("ショートカットした");
        }

        // 実際の吸着発動は「発動エリア内であること」＋「そのエリアが指定するトリガー方式で入力があったこと」が条件。
        // どちらを使うか（Eキー／Horizontal閾値）はエリアごとにShortcutZone側で設定する。
        if (_currentShortcutZone != null)
        {
            bool zoneTriggered;
            if (_currentShortcutZone.TriggerMethod == EShortcutTriggerMethod.HorizontalThreshold)
            {
                bool isAboveThreshold = Mathf.Abs(horizontal) >= _currentShortcutZone.HorizontalThreshold;
                zoneTriggered = isAboveThreshold && !_wasHorizontalAboveThreshold;
                _wasHorizontalAboveThreshold = isAboveThreshold;
            }
            else
            {
                zoneTriggered = input.ShortcutPressed;
            }

            if (zoneTriggered)
            {
                _heightSnapActive = true;
            }
        }

        // 3. 現在のチューニング値（Boost状態に応じてBoostControllerが持つ）
        float maxSpeed = boost.CurrentMaxSpeed;
        float forwardAcceleration = boost.CurrentForwardAcceleration;
        float steeringPower = boost.CurrentSteeringPower;

        // 4. 現在位置でのSpline基準値
        Vector3 position = transform.position;
        Vector3 splineForward = _guide.GetForward(position);
        Vector3 splineRight = _guide.GetRight(position);
        Vector3 splineUp = _guide.GetUp(position);
        Vector3 centerPosition = _guide.GetCenterPosition(position);

        // 5. 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-headingTurnRate * dt));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 6. 加速度計算（コース中央への補正は左右・上下で別々の強さを持てるように分けて計算する）
        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 lateralAccel = splineRight * (horizontal * steeringPower);
        Vector3 centerOffset = centerPosition - position;
        float lateralCenterOffset = Vector3.Dot(centerOffset, splineRight);
        float verticalCenterOffset = Vector3.Dot(centerOffset, splineUp);
        float effectiveVerticalGain = _heightSnapActive ? heightSnapCorrectionGain : verticalCorrectionGain;
        float effectiveMaxVerticalAccel = _heightSnapActive ? maxHeightSnapCorrectionAccel : maxVerticalCorrectionAccel;
        Vector3 lateralCorrectionAccel = splineRight * Mathf.Clamp(lateralCenterOffset * centerCorrectionGain, -maxCorrectionAccel, maxCorrectionAccel);
        Vector3 verticalCorrectionAccel = splineUp * Mathf.Clamp(verticalCenterOffset * effectiveVerticalGain, -effectiveMaxVerticalAccel, effectiveMaxVerticalAccel);
        Vector3 correctionAccel = lateralCorrectionAccel + verticalCorrectionAccel;

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

        // 9. 位置積分（円筒境界の外には出られず、境界では摩擦0で滑る）
        Vector3 desiredPosition = position + _velocity * dt;
        transform.position = ClampVelocityAndPositionToTunnelRadius(desiredPosition, centerPosition, splineRight, splineUp, ref _velocity);

        // 10. 回転：ForwardはPlayerForwardから、Rollは「Splineのバンクへの追従」＋「操作によるロール」の合算
        Vector3 bankUp = _guide.GetUp(transform.position);
        float bankFromCourse = Vector3.SignedAngle(Vector3.up, bankUp, _playerForward);
        float steeringRoll = -horizontal * rollMaxAngle;
        float targetRoll = Mathf.Clamp(bankFromCourse + steeringRoll, -rollMaxAngle, rollMaxAngle);
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, rollSmoothTime);
        transform.rotation = Quaternion.LookRotation(_playerForward, Vector3.up) * Quaternion.AngleAxis(_currentRoll, Vector3.forward);

        // 11. カメラへ公開する先読み位置
        LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, lookAheadDistance);
    }

    // Splineを中心軸とした円筒（TunnelRadius）の外に出られないようにする（frictionless slide:
    // 境界の外側へ押し出す速度成分だけを消す。前進方向などの接線成分は失わない）。
    private Vector3 ClampVelocityAndPositionToTunnelRadius(Vector3 desiredPosition, Vector3 center, Vector3 right, Vector3 up, ref Vector3 velocity)
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
        Vector3 correctedPosition = desiredPosition + right * (lateral * scale - lateral) + up * (vertical * scale - vertical);

        Vector3 radialDir = (right * lateral + up * vertical) / distance;
        float velocityAlongRadial = Vector3.Dot(velocity, radialDir);
        if (velocityAlongRadial > 0f)
        {
            velocity -= radialDir * velocityAlongRadial;
        }

        return correctedPosition;
    }
}
