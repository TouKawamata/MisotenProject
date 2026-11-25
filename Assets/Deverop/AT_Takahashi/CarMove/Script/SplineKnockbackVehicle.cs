using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CourseSpline上を距離ベースで移動し、接触したレーサーを吹き飛ばす車両ギミック。
///
/// 外部のUnity Splinesパッケージには依存しない。
///
/// 推奨構成:
///
/// CourseSplineKnockbackVehicle
/// ├─ Rigidbody
/// ├─ CourseSplineKnockbackVehicle.cs
/// ├─ Hitbox
/// │   └─ BoxCollider（Is Trigger = ON）
/// └─ VisualRoot
///     └─ 任意の車・電車メッシュ
///
/// Rigidbody:
///     Is Kinematic = ON
///     Use Gravity = OFF
///     Interpolate = Interpolate
///
/// Collider:
///     Is Trigger = ON
///
/// Racer:
///     Layer = Racer
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CourseSplineKnockbackVehicle : MonoBehaviour
{
    private const string RacerLayerName = "Racer";
    private const float MinDirectionSqrMagnitude = 0.0001f;

    public enum EndBehaviour
    {
        [InspectorName("コース設定に従う")]
        FollowCourseSetting,

        [InspectorName("ループ")]
        Loop,

        [InspectorName("往復")]
        PingPong,

        [InspectorName("終点で停止")]
        Stop
    }

    public enum KnockbackMode
    {
        [InspectorName("車両の進行方向")]
        ForwardOnly,

        [InspectorName("車体の外側")]
        PlanarOnly,

        [InspectorName("進行方向＋車体の外側")]
        ForwardAndPlanar
    }

    [Header("コース")]

    [Tooltip("移動に使用するCourseSpline")]
    [SerializeField]
    private CourseSpline _courseSpline;

    [Tooltip("開始地点。0が始点、1が終点")]
    [SerializeField, Range(0f, 1f)]
    private float _startNormalizedPosition;

    [Tooltip("コース端に到達したときの処理")]
    [SerializeField]
    private EndBehaviour _endBehaviour =
        EndBehaviour.FollowCourseSetting;

    [Header("移動")]

    [Tooltip("移動速度[m/s]")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 25f;

    [Tooltip("コースの向きへ車両を回転させる")]
    [SerializeField]
    private bool _alignToCourse = true;

    [Tooltip("回転の追従速度。0以下なら即座に向きを合わせる")]
    [SerializeField, Min(0f)]
    private float _rotationSharpness = 12f;

    [Tooltip("進行方向計算用の先読み距離[m]。カーブでの向きを滑らかにする")]
    [SerializeField, Min(0.01f)]
    private float _lookAheadDistance = 2f;

    [Tooltip("モデルのローカル回転補正。モデルの正面が+Zでない場合に使用する")]
    [SerializeField]
    private Vector3 _rotationOffset;

    [Tooltip("コース中心から右方向へのオフセット[m]")]
    [SerializeField]
    private float _rightOffset;

    [Tooltip("コース中心から上方向へのオフセット[m]")]
    [SerializeField]
    private float _upOffset;

    [Header("見た目")]

    [Tooltip("任意の車両メッシュを配置するルート。移動ロジックはこの中のメッシュに依存しない")]
    [SerializeField]
    private Transform _visualRoot;

    [Header("ノックバック")]

    [Tooltip("接触時の吹き飛ばし方向")]
    [SerializeField]
    private KnockbackMode _knockbackMode =
        KnockbackMode.ForwardAndPlanar;

    [Tooltip("車両の進行方向へ吹き飛ばす距離[m]")]
    [SerializeField, Min(0f)]
    private float _forwardPushDistance = 12f;

    [Tooltip("コースの断面方向へ吹き飛ばす距離[m]")]
    [SerializeField, Min(0f)]
    private float _planarPushDistance = 6f;

    [Tooltip("吹き飛ばしにかける時間[秒]")]
    [SerializeField, Min(0.01f)]
    private float _pushDuration = 0.45f;

    [Tooltip("行動不能にする時間[秒]")]
    [SerializeField, Min(0f)]
    private float _stunDuration = 0.3f;

    [Header("減速")]

    [Tooltip("接触後の最高速度倍率")]
    [SerializeField, Range(0f, 1f)]
    private float _maxSpeedMultiplier = 0.35f;

    [Tooltip("減速の持続時間[秒]")]
    [SerializeField, Min(0f)]
    private float _slowDuration = 1f;

    [Tooltip("減速開始・終了時のフェード時間[秒]")]
    [SerializeField, Min(0f)]
    private float _slowFadeDuration = 0.1f;

    [Header("防御")]

    [Tooltip("ONの場合、シールドで接触効果を防げる")]
    [SerializeField]
    private bool _canBeBlockedByShield = true;

    [Tooltip("同じレーサーへ再度ヒットできるまでの時間[秒]")]
    [SerializeField, Min(0f)]
    private float _sameRacerCooldown = 0.5f;

    [Header("衝突判定")]

    [Tooltip("車両の接触判定。未設定の場合は子を含むColliderから自動取得する")]
    [SerializeField]
    private Collider _vehicleCollider;

    [Header("演出")]

    [Tooltip("レーサーへ効果を与えたときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onHitRacer;

    [Tooltip("シールドで防がれたときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onBlockedByShield;

    [Tooltip("コース端へ到達したときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onReachedEnd;

    [Header("デバッグ")]

    [SerializeField]
    private bool _showDebugLines = true;

    [SerializeField, Min(0.1f)]
    private float _debugLineDuration = 3f;

    private readonly Dictionary<IGimmickRacer, float> _lastHitTimes =
        new Dictionary<IGimmickRacer, float>();

    private readonly List<IGimmickRacer> _removalBuffer =
        new List<IGimmickRacer>();

    private Rigidbody _rigidbody;
    private int _racerLayer;

    // CourseSpline上の現在距離。
    private float _currentDistance;

    // PingPongで逆走中か。
    private bool _isMovingBackward;

    // Stopモードで停止したか。
    private bool _hasStopped;

    // コース終端イベントの連続呼び出し防止。
    private bool _hasInvokedEnd;

    // ノックバックに使う実際の車両進行方向。
    private Vector3 _movementDirection = Vector3.forward;

    public Transform VisualRoot => _visualRoot;

    public float CurrentDistance => _currentDistance;

    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        _rigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        if (_vehicleCollider == null)
        {
            _vehicleCollider =
                GetComponentInChildren<Collider>();
        }

        if (_vehicleCollider == null)
        {
            Debug.LogWarning(
                $"{nameof(CourseSplineKnockbackVehicle)}: " +
                "車両のColliderが設定されていません。",
                this);
        }
        else if (!_vehicleCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{nameof(CourseSplineKnockbackVehicle)}: " +
                $"{_vehicleCollider.name} のIs TriggerがOFFです。",
                _vehicleCollider);
        }

        _racerLayer = LayerMask.NameToLayer(
            RacerLayerName);

        if (_racerLayer < 0)
        {
            Debug.LogWarning(
                $"{nameof(CourseSplineKnockbackVehicle)}: " +
                $"{RacerLayerName}レイヤーが見つかりません。",
                this);
        }
    }

    private void Start()
    {
        if (!ValidateCourse())
        {
            enabled = false;
            return;
        }

        _currentDistance =
            _courseSpline.TotalLength *
            _startNormalizedPosition;

        EvaluatePose(
            _currentDistance,
            out Vector3 position,
            out Quaternion rotation);

        _rigidbody.position = position;
        _rigidbody.rotation = rotation;

        transform.SetPositionAndRotation(
            position,
            rotation);
    }

    private void FixedUpdate()
    {
        RemoveExpiredCooldowns();

        if (!ValidateCourse() ||
            _moveSpeed <= 0f ||
            _hasStopped)
        {
            return;
        }

        AdvanceDistance(
            _moveSpeed * Time.fixedDeltaTime);

        EvaluatePose(
            _currentDistance,
            out Vector3 targetPosition,
            out Quaternion targetRotation);

        Vector3 movement =
            targetPosition - _rigidbody.position;

        if (movement.sqrMagnitude >
            MinDirectionSqrMagnitude)
        {
            _movementDirection =
                movement.normalized;
        }
        else
        {
            _movementDirection =
                GetCourseMovementDirection(
                    _currentDistance);
        }

        _rigidbody.MovePosition(targetPosition);

        if (_alignToCourse)
        {
            Quaternion rotation =
                CalculateSmoothedRotation(
                    targetRotation);

            _rigidbody.MoveRotation(rotation);
        }
    }

    private bool ValidateCourse()
    {
        return
            _courseSpline != null &&
            _courseSpline.TotalLength > 0.001f;
    }

    /// <summary>
    /// 現在距離から車両の位置と姿勢を求める。
    /// </summary>
    private void EvaluatePose(
        float distance,
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 center =
            _courseSpline.EvaluatePositionByDistance(
                distance);

        Vector3 right =
            _courseSpline.EvaluateRightByDistance(
                distance);

        Vector3 up =
            _courseSpline.EvaluateUpByDistance(
                distance);

        Vector3 forward =
            GetCourseMovementDirection(distance);

        position =
            center +
            right * _rightOffset +
            up * _upOffset;

        if (forward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            forward = transform.forward;
        }

        if (up.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            up = Vector3.up;
        }

        // forwardとupが平行に近い場合のLookRotation対策。
        if (Mathf.Abs(
                Vector3.Dot(
                    forward.normalized,
                    up.normalized)) > 0.999f)
        {
            up = Vector3.up;

            if (Mathf.Abs(
                    Vector3.Dot(
                        forward.normalized,
                        up)) > 0.999f)
            {
                up = Vector3.right;
            }
        }

        rotation =
            Quaternion.LookRotation(
                forward.normalized,
                up.normalized) *
            Quaternion.Euler(_rotationOffset);
    }

    /// <summary>
    /// 先読み位置を使い、カーブ上の向きを滑らかに求める。
    /// </summary>
    private Vector3 GetCourseMovementDirection(
        float distance)
    {
        float directionSign =
            _isMovingBackward ? -1f : 1f;

        float aheadDistance =
            distance +
            _lookAheadDistance *
            directionSign;

        Vector3 currentPosition =
            _courseSpline.EvaluatePositionByDistance(
                distance);

        Vector3 aheadPosition =
            _courseSpline.EvaluatePositionByDistance(
                aheadDistance);

        Vector3 direction =
            aheadPosition - currentPosition;

        if (direction.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            direction =
                _courseSpline
                    .EvaluateForwardByDistance(
                        distance) *
                directionSign;
        }

        return direction.sqrMagnitude >
               MinDirectionSqrMagnitude
            ? direction.normalized
            : transform.forward;
    }

    private Quaternion CalculateSmoothedRotation(
        Quaternion targetRotation)
    {
        if (_rotationSharpness <= 0f)
        {
            return targetRotation;
        }

        // フレームレートの影響を受けにくい指数補間。
        float t =
            1f -
            Mathf.Exp(
                -_rotationSharpness *
                Time.fixedDeltaTime);

        return Quaternion.Slerp(
            _rigidbody.rotation,
            targetRotation,
            t);
    }

    private void AdvanceDistance(
        float movementDistance)
    {
        float totalLength =
            _courseSpline.TotalLength;

        if (totalLength <= 0f)
        {
            return;
        }

        EndBehaviour behaviour =
            ResolveEndBehaviour();

        switch (behaviour)
        {
            case EndBehaviour.Loop:
                {
                    _currentDistance += movementDistance;

                    if (_currentDistance >= totalLength)
                    {
                        InvokeReachedEnd();
                    }

                    _currentDistance =
                        RepeatDistance(
                            _currentDistance,
                            totalLength);

                    break;
                }

            case EndBehaviour.PingPong:
                {
                    float direction =
                        _isMovingBackward ? -1f : 1f;

                    _currentDistance +=
                        movementDistance * direction;

                    while (_currentDistance < 0f ||
                           _currentDistance > totalLength)
                    {
                        if (_currentDistance >
                            totalLength)
                        {
                            _currentDistance =
                                totalLength -
                                (_currentDistance -
                                 totalLength);

                            _isMovingBackward = true;
                            InvokeReachedEnd();
                        }
                        else
                        {
                            _currentDistance =
                                -_currentDistance;

                            _isMovingBackward = false;
                            InvokeReachedEnd();
                        }
                    }

                    // 往復では次に別の端へ到達した際にも
                    // イベントを呼べるようにする。
                    _hasInvokedEnd = false;
                    break;
                }

            case EndBehaviour.Stop:
                {
                    _currentDistance += movementDistance;

                    if (_currentDistance >= totalLength)
                    {
                        _currentDistance = totalLength;
                        _hasStopped = true;
                        InvokeReachedEnd();
                    }

                    break;
                }
        }
    }

    private EndBehaviour ResolveEndBehaviour()
    {
        if (_endBehaviour !=
            EndBehaviour.FollowCourseSetting)
        {
            return _endBehaviour;
        }

        return _courseSpline.IsLoop
            ? EndBehaviour.Loop
            : EndBehaviour.Stop;
    }

    private static float RepeatDistance(
        float distance,
        float length)
    {
        float result = distance % length;

        return result < 0f
            ? result + length
            : result;
    }

    private void InvokeReachedEnd()
    {
        if (_hasInvokedEnd)
        {
            return;
        }

        _hasInvokedEnd = true;
        _onReachedEnd?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_racerLayer >= 0 &&
            other.gameObject.layer != _racerLayer)
        {
            return;
        }

        IGimmickRacer racer =
            other.GetComponentInParent<IGimmickRacer>();

        if (racer == null)
        {
            return;
        }

        TryHitRacer(racer);
    }

    private void TryHitRacer(
        IGimmickRacer racer)
    {
        if (_lastHitTimes.TryGetValue(
                racer,
                out float lastHitTime) &&
            Time.time - lastHitTime <
            _sameRacerCooldown)
        {
            return;
        }

        // 無敵中でもTrigger内で多段判定されないよう、
        // 判定した時点でクールダウンを記録する。
        _lastHitTimes[racer] = Time.time;

        if (racer.IsInvincible)
        {
            return;
        }

        if (_canBeBlockedByShield &&
            racer.TryBlockWithShield())
        {
            _onBlockedByShield?.Invoke();
            return;
        }

        ApplyKnockback(racer);

        _onHitRacer?.Invoke();
    }

    private void ApplyKnockback(
        IGimmickRacer racer)
    {
        if (racer.Transform == null)
        {
            return;
        }

        Vector3 racerPosition =
            racer.Transform.position;

        ICourseGuide guide =
            racer.CourseGuide;

        Vector3 courseForward =
            guide != null
                ? guide.GetForward(racerPosition)
                : _courseSpline
                    .EvaluateForwardByDistance(
                        _currentDistance);

        if (courseForward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            courseForward =
                _movementDirection;
        }

        courseForward.Normalize();

        Vector3 vehicleForward =
            _movementDirection;

        if (vehicleForward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            vehicleForward =
                transform.forward;
        }

        vehicleForward.Normalize();

        Vector3 closestPoint =
            _vehicleCollider != null
                ? _vehicleCollider
                    .ClosestPoint(racerPosition)
                : transform.position;

        Vector3 awayFromVehicle =
            racerPosition - closestPoint;

        // レーサーのコース進行方向を除去し、
        // コース断面上の押し出し方向を作る。
        Vector3 planarOffset =
            Vector3.ProjectOnPlane(
                awayFromVehicle,
                courseForward);

        // Collider内部ではClosestPointがレーサー位置と
        // 同じになる場合があるため、車両中心から再計算する。
        if (planarOffset.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            planarOffset =
                Vector3.ProjectOnPlane(
                    racerPosition -
                    transform.position,
                    courseForward);
        }

        // 車両の中心と完全に重なった場合。
        if (planarOffset.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            planarOffset =
                guide != null
                    ? guide.GetRight(
                        racerPosition)
                    : transform.right;
        }

        racer.Stun(_stunDuration);

        racer.Effects.SetMaxSpeedMultiplier(
            this,
            _maxSpeedMultiplier,
            _slowDuration,
            _slowFadeDuration);

        bool applyForward =
            _knockbackMode ==
                KnockbackMode.ForwardOnly ||
            _knockbackMode ==
                KnockbackMode.ForwardAndPlanar;

        bool applyPlanar =
            _knockbackMode ==
                KnockbackMode.PlanarOnly ||
            _knockbackMode ==
                KnockbackMode.ForwardAndPlanar;

        if (applyPlanar &&
            _planarPushDistance > 0f)
        {
            racer.ApplyPlanarPush(
                planarOffset,
                _planarPushDistance,
                _pushDuration);
        }

        if (applyForward &&
            _forwardPushDistance > 0f)
        {
            // ApplyForwardPushはレーサーのコース方向を
            // 正方向とするため、車両の移動方向から符号を決める。
            float sign =
                Vector3.Dot(
                    vehicleForward,
                    courseForward) >= 0f
                    ? 1f
                    : -1f;

            racer.ApplyForwardPush(
                _forwardPushDistance * sign,
                _pushDuration);
        }

        Debug.Log(
            $"{nameof(CourseSplineKnockbackVehicle)}: " +
            $"{racer.Transform.name} に接触 " +
            $"進行方向押し出し " +
            $"{(applyForward ? _forwardPushDistance : 0f):F1}m " +
            $"断面押し出し " +
            $"{(applyPlanar ? _planarPushDistance : 0f):F1}m " +
            $"スタン {_stunDuration:F2}秒 " +
            $"減速 ×{_maxSpeedMultiplier:F2} " +
            $"{_slowDuration:F1}秒",
            this);

        DrawHitResult(
            racerPosition,
            vehicleForward,
            planarOffset,
            applyForward,
            applyPlanar);
    }

    private void RemoveExpiredCooldowns()
    {
        if (_lastHitTimes.Count == 0)
        {
            return;
        }

        _removalBuffer.Clear();

        foreach (
            KeyValuePair<IGimmickRacer, float> pair
            in _lastHitTimes)
        {
            if (pair.Key == null ||
                Time.time - pair.Value >=
                _sameRacerCooldown)
            {
                _removalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0;
             i < _removalBuffer.Count;
             i++)
        {
            _lastHitTimes.Remove(
                _removalBuffer[i]);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void DrawHitResult(
        Vector3 racerPosition,
        Vector3 vehicleForward,
        Vector3 planarOffset,
        bool applyForward,
        bool applyPlanar)
    {
        if (!_showDebugLines)
        {
            return;
        }

        if (applyForward)
        {
            Debug.DrawRay(
                racerPosition,
                vehicleForward.normalized *
                _forwardPushDistance,
                Color.red,
                _debugLineDuration);
        }

        if (applyPlanar &&
            planarOffset.sqrMagnitude >
            MinDirectionSqrMagnitude)
        {
            Debug.DrawRay(
                racerPosition,
                planarOffset.normalized *
                _planarPushDistance,
                Color.yellow,
                _debugLineDuration);
        }
    }

    /// <summary>
    /// 外部から車両を指定距離へ移動する。
    /// </summary>
    public void SetDistance(float distance)
    {
        if (!ValidateCourse())
        {
            return;
        }

        _currentDistance =
            _courseSpline.IsLoop
                ? RepeatDistance(
                    distance,
                    _courseSpline.TotalLength)
                : Mathf.Clamp(
                    distance,
                    0f,
                    _courseSpline.TotalLength);

        _hasStopped = false;
        _hasInvokedEnd = false;

        EvaluatePose(
            _currentDistance,
            out Vector3 position,
            out Quaternion rotation);

        _rigidbody.position = position;
        _rigidbody.rotation = rotation;

        transform.SetPositionAndRotation(
            position,
            rotation);
    }

    public void Restart()
    {
        _isMovingBackward = false;
        _hasStopped = false;
        _hasInvokedEnd = false;

        SetDistance(
            _courseSpline.TotalLength *
            _startNormalizedPosition);
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        _moveSpeed =
            Mathf.Max(0f, _moveSpeed);

        _rotationSharpness =
            Mathf.Max(0f, _rotationSharpness);

        _lookAheadDistance =
            Mathf.Max(
                0.01f,
                _lookAheadDistance);

        _forwardPushDistance =
            Mathf.Max(
                0f,
                _forwardPushDistance);

        _planarPushDistance =
            Mathf.Max(
                0f,
                _planarPushDistance);

        _pushDuration =
            Mathf.Max(
                0.01f,
                _pushDuration);

        _stunDuration =
            Mathf.Max(
                0f,
                _stunDuration);

        _maxSpeedMultiplier =
            Mathf.Clamp01(
                _maxSpeedMultiplier);

        _slowDuration =
            Mathf.Max(
                0f,
                _slowDuration);

        _slowFadeDuration =
            Mathf.Max(
                0f,
                _slowFadeDuration);

        _sameRacerCooldown =
            Mathf.Max(
                0f,
                _sameRacerCooldown);

        _debugLineDuration =
            Mathf.Max(
                0.1f,
                _debugLineDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (_courseSpline == null)
        {
            return;
        }

        float distance = Application.isPlaying
            ? _currentDistance
            : _courseSpline.TotalLength *
              _startNormalizedPosition;

        Vector3 center =
            _courseSpline
                .EvaluatePositionByDistance(
                    distance);

        Vector3 right =
            _courseSpline
                .EvaluateRightByDistance(
                    distance);

        Vector3 up =
            _courseSpline
                .EvaluateUpByDistance(
                    distance);

        Vector3 position =
            center +
            right * _rightOffset +
            up * _upOffset;

        Vector3 forward =
            _courseSpline
                .EvaluateForwardByDistance(
                    distance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(position, 0.4f);
        Gizmos.DrawLine(
            position,
            position +
            forward.normalized * 5f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            position,
            position +
            forward.normalized *
            _forwardPushDistance);
    }

#endif
}