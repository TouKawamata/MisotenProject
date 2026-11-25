using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// VehiclePath上を移動し、接触したレーサーをノックバックする車両ギミック。
///
/// 車両の走行経路にはVehiclePathを使用する。
/// レーサーの吹き飛ばし方向には、レーサー側のICourseGuideを使用する。
///
/// 推奨構成:
///
/// MovingVehicle
/// ├─ Rigidbody
/// ├─ BoxCollider または子Hitbox
/// ├─ CourseSplineKnockbackVehicle
/// └─ VisualRoot
///     └─ 任意の車・電車メッシュ
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CourseSplineKnockbackVehicle : MonoBehaviour
{
    private const string RacerLayerName = "Racer";
    private const float MinDirectionSqrMagnitude = 0.0001f;

    public enum EndBehaviour
    {
        [InspectorName("パス設定に従う")]
        FollowPathSetting,

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

    [Header("車両用パス")]

    [Tooltip("車両専用の移動経路")]
    [SerializeField]
    private VehiclePath _vehiclePath;

    [Tooltip("開始地点。0が始点、1が終点")]
    [SerializeField, Range(0f, 1f)]
    private float _startNormalizedPosition;

    [Tooltip("パスの端へ到達したときの処理")]
    [SerializeField]
    private EndBehaviour _endBehaviour =
        EndBehaviour.FollowPathSetting;

    [Header("移動")]

    [Tooltip("移動速度[m/s]")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 25f;

    [Tooltip("パスの進行方向へ車両を回転させる")]
    [SerializeField]
    private bool _alignToPath = true;

    [Tooltip("回転の追従速度。0なら即座に向きを合わせる")]
    [SerializeField, Min(0f)]
    private float _rotationSharpness = 12f;

    [Tooltip("進行方向を求めるために先を見る距離[m]")]
    [SerializeField, Min(0.01f)]
    private float _lookAheadDistance = 2f;

    [Tooltip("モデルのローカル回転補正。モデルの正面が+Zでない場合に使用する")]
    [SerializeField]
    private Vector3 _rotationOffset;

    [Tooltip("パス位置から車両のローカル右方向へずらす距離[m]")]
    [SerializeField]
    private float _rightOffset;

    [Tooltip("パス位置からワールド上方向へずらす距離[m]")]
    [SerializeField]
    private float _upOffset;

    [Header("見た目")]

    [Tooltip("任意の車両メッシュを配置するルート")]
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

    [Tooltip("レーサーのコース断面方向へ吹き飛ばす距離[m]")]
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

    [Tooltip("減速がかかるときと戻るときのフェード時間[秒]")]
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

    [Tooltip("車両の当たり判定。未設定なら子を含むColliderから取得する")]
    [SerializeField]
    private Collider _vehicleCollider;

    [Header("演出")]

    [Tooltip("レーサーへ効果を与えたときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onHitRacer;

    [Tooltip("シールドで防がれたときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onBlockedByShield;

    [Tooltip("パスの端へ到達したときに呼ばれる")]
    [SerializeField]
    private UnityEvent _onReachedEnd;

    [Header("デバッグ")]

    [Tooltip("接触時のノックバック方向を表示する")]
    [SerializeField]
    private bool _showDebugLines = true;

    [Tooltip("デバッグ線を表示する時間[秒]")]
    [SerializeField, Min(0.1f)]
    private float _debugLineDuration = 3f;

    private readonly Dictionary<IGimmickRacer, float> _lastHitTimes =
        new Dictionary<IGimmickRacer, float>();

    private readonly List<IGimmickRacer> _removalBuffer =
        new List<IGimmickRacer>();

    private Rigidbody _rigidbody;
    private int _racerLayer;

    private float _currentDistance;
    private bool _isMovingBackward;
    private bool _hasStopped;

    // 最後に車両が実際に移動した方向。
    private Vector3 _movementDirection = Vector3.forward;

    public Transform VisualRoot => _visualRoot;

    public float CurrentDistance => _currentDistance;

    public Vector3 MovementDirection => _movementDirection;

    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Mathf.Max(0f, value);
    }

    private void Reset()
    {
        Rigidbody body = GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        _vehicleCollider =
            GetComponentInChildren<Collider>();
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
                $"{_vehicleCollider.name}のIs TriggerがOFFです。",
                _vehicleCollider);
        }

        _racerLayer =
            LayerMask.NameToLayer(RacerLayerName);

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
        if (!ValidatePath())
        {
            Debug.LogError(
                $"{nameof(CourseSplineKnockbackVehicle)}: " +
                "有効なVehiclePathが設定されていません。",
                this);

            enabled = false;
            return;
        }

        _currentDistance =
            _vehiclePath.TotalLength *
            _startNormalizedPosition;

        SnapToCurrentPosition();
    }

    private void FixedUpdate()
    {
        RemoveExpiredCooldowns();

        if (!ValidatePath() ||
            _moveSpeed <= 0f ||
            _hasStopped)
        {
            return;
        }

        Vector3 previousPosition =
            _rigidbody.position;

        AdvanceDistance(
            _moveSpeed * Time.fixedDeltaTime);

        EvaluatePose(
            _currentDistance,
            out Vector3 targetPosition,
            out Quaternion targetRotation);

        Vector3 movement =
            targetPosition - previousPosition;

        /*
         * ループ境界を跨ぐと、
         * 最終地点から開始地点への大きな差が出ることがある。
         * ノックバック方向へその差を使用せず、
         * パスの評価方向を使用する。
         */
        float expectedMovement =
            _moveSpeed * Time.fixedDeltaTime;

        bool movementLooksValid =
            movement.sqrMagnitude >
                MinDirectionSqrMagnitude &&
            movement.magnitude <=
                expectedMovement * 3f + 1f;

        if (movementLooksValid)
        {
            _movementDirection =
                movement.normalized;
        }
        else
        {
            _movementDirection =
                GetPathMovementDirection(
                    _currentDistance);
        }

        _rigidbody.MovePosition(targetPosition);

        if (_alignToPath)
        {
            Quaternion rotation =
                CalculateSmoothedRotation(
                    targetRotation);

            _rigidbody.MoveRotation(rotation);
        }
    }

    private bool ValidatePath()
    {
        return
            _vehiclePath != null &&
            _vehiclePath.TotalLength > 0.001f;
    }

    /// <summary>
    /// 現在距離の位置へ即座に配置する。
    /// </summary>
    private void SnapToCurrentPosition()
    {
        EvaluatePose(
            _currentDistance,
            out Vector3 position,
            out Quaternion rotation);

        _rigidbody.position = position;
        _rigidbody.rotation = rotation;

        transform.SetPositionAndRotation(
            position,
            rotation);

        _movementDirection =
            GetPathMovementDirection(
                _currentDistance);
    }

    /// <summary>
    /// VehiclePath上の位置と姿勢を求める。
    /// </summary>
    private void EvaluatePose(
        float distance,
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 pathPosition =
            _vehiclePath.EvaluatePosition(
                distance);

        Vector3 forward =
            GetPathMovementDirection(distance);

        Vector3 up = Vector3.up;

        if (forward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            forward = transform.forward;
        }

        forward.Normalize();

        // 上下に近い経路でもLookRotationが破綻しないようにする。
        if (Mathf.Abs(
                Vector3.Dot(
                    forward,
                    up)) > 0.999f)
        {
            up = transform.up;

            if (Mathf.Abs(
                    Vector3.Dot(
                        forward,
                        up.normalized)) > 0.999f)
            {
                up = Vector3.forward;
            }
        }

        // ローカル右方向。
        Vector3 right =
            Vector3.Cross(up, forward);

        if (right.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            right = transform.right;
        }

        right.Normalize();

        // 直交するUpを再計算する。
        up =
            Vector3.Cross(
                forward,
                right).normalized;

        position =
            pathPosition +
            right * _rightOffset +
            Vector3.up * _upOffset;

        rotation =
            Quaternion.LookRotation(
                forward,
                up) *
            Quaternion.Euler(
                _rotationOffset);
    }

    /// <summary>
    /// VehiclePathから現在の移動方向を取得する。
    /// PingPongで戻っている場合は方向を反転する。
    /// </summary>
    private Vector3 GetPathMovementDirection(
        float distance)
    {
        Vector3 direction =
            _vehiclePath.EvaluateForward(
                distance,
                _lookAheadDistance);

        if (_isMovingBackward)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            return transform.forward;
        }

        return direction.normalized;
    }

    private Quaternion CalculateSmoothedRotation(
        Quaternion targetRotation)
    {
        if (_rotationSharpness <= 0f)
        {
            return targetRotation;
        }

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
            _vehiclePath.TotalLength;

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
                    float before =
                        _currentDistance;

                    _currentDistance +=
                        movementDistance;

                    if (before < totalLength &&
                        _currentDistance >= totalLength)
                    {
                        _onReachedEnd?.Invoke();
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
                        _isMovingBackward
                            ? -1f
                            : 1f;

                    _currentDistance +=
                        movementDistance *
                        direction;

                    /*
                     * 速度が非常に大きく、1フレームで端を複数回越えても
                     * 範囲へ戻せるようwhileで処理する。
                     */
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
                            _onReachedEnd?.Invoke();
                        }
                        else
                        {
                            _currentDistance =
                                -_currentDistance;

                            _isMovingBackward = false;
                            _onReachedEnd?.Invoke();
                        }
                    }

                    break;
                }

            case EndBehaviour.Stop:
                {
                    _currentDistance +=
                        movementDistance;

                    if (_currentDistance >=
                        totalLength)
                    {
                        _currentDistance =
                            totalLength;

                        _hasStopped = true;
                        _onReachedEnd?.Invoke();
                    }

                    break;
                }
        }
    }

    private EndBehaviour ResolveEndBehaviour()
    {
        if (_endBehaviour !=
            EndBehaviour.FollowPathSetting)
        {
            return _endBehaviour;
        }

        return _vehiclePath.IsLoop
            ? EndBehaviour.Loop
            : EndBehaviour.Stop;
    }

    private static float RepeatDistance(
        float distance,
        float length)
    {
        if (length <= 0f)
        {
            return 0f;
        }

        float result =
            distance % length;

        return result < 0f
            ? result + length
            : result;
    }

    private void OnTriggerEnter(
        Collider other)
    {
        ProcessTriggerEnter(other);
    }

    /// <summary>
    /// 子Hitboxの中継スクリプトからも呼び出せる接触窓口。
    /// </summary>
    public void ProcessTriggerEnter(
        Collider other)
    {
        if (other == null)
        {
            return;
        }

        /*
         * RacerのColliderが子オブジェクトにあり、
         * Layerが親にだけ設定されている構成も考慮し、
         * Layerだけで確定せずIGimmickRacerも検索する。
         */
        IGimmickRacer racer =
            other.GetComponentInParent<
                IGimmickRacer>();

        if (racer == null)
        {
            return;
        }

        if (_racerLayer >= 0)
        {
            GameObject racerObject =
                racer.Transform != null
                    ? racer.Transform.gameObject
                    : other.gameObject;

            bool colliderIsRacerLayer =
                other.gameObject.layer ==
                _racerLayer;

            bool rootIsRacerLayer =
                racerObject.layer ==
                _racerLayer;

            if (!colliderIsRacerLayer &&
                !rootIsRacerLayer)
            {
                return;
            }
        }

        TryHitRacer(racer);
    }

    private void TryHitRacer(
        IGimmickRacer racer)
    {
        if (racer == null)
        {
            return;
        }

        if (_lastHitTimes.TryGetValue(
                racer,
                out float lastHitTime) &&
            Time.time - lastHitTime <
                _sameRacerCooldown)
        {
            return;
        }

        /*
         * 複数Colliderから同時に接触しても多段ヒットしないよう、
         * 無敵・シールド判定より先に時刻を記録する。
         */
        _lastHitTimes[racer] =
            Time.time;

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
        Transform racerTransform =
            racer.Transform;

        if (racerTransform == null)
        {
            return;
        }

        Vector3 racerPosition =
            racerTransform.position;

        /*
         * ここではVehiclePathではなく、
         * レーサー自身が使っているCourseGuideを参照する。
         *
         * ApplyPlanarPushとApplyForwardPushはレーサーの
         * コース座標系で処理されるため。
         */
        ICourseGuide guide =
            racer.CourseGuide;

        Vector3 courseForward =
            guide != null
                ? guide.GetForward(
                    racerPosition)
                : _movementDirection;

        if (courseForward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            courseForward =
                transform.forward;
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
                ? _vehicleCollider.ClosestPoint(
                    racerPosition)
                : transform.position;

        Vector3 awayFromVehicle =
            racerPosition -
            closestPoint;

        /*
         * レーサーのコース進行方向成分を除去し、
         * レーサーのコース断面に沿う押し出し方向を作る。
         */
        Vector3 planarOffset =
            Vector3.ProjectOnPlane(
                awayFromVehicle,
                courseForward);

        /*
         * RacerがTrigger内部にいる場合、
         * ClosestPointはRacer自身の位置を返すため、
         * 車両中心からの方向を使用する。
         */
        if (planarOffset.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            planarOffset =
                Vector3.ProjectOnPlane(
                    racerPosition -
                    transform.position,
                    courseForward);
        }

        /*
         * 車両中心と完全に重なった場合。
         * CourseGuideの右方向を優先する。
         */
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
            /*
             * ApplyForwardPushの正方向は、
             * VehiclePathではなくレーサー側コースの正方向。
             *
             * 車両がレーサーの進行方向と逆向きなら
             * 負の距離を渡す。
             */
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
            $"{racerTransform.name}に接触。 " +
            $"車両方向 {vehicleForward}、" +
            $"前後押し出し " +
            $"{(applyForward ? _forwardPushDistance : 0f):F1}m、" +
            $"断面押し出し " +
            $"{(applyPlanar ? _planarPushDistance : 0f):F1}m、" +
            $"スタン {_stunDuration:F2}秒、" +
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
                _removalBuffer.Add(
                    pair.Key);
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

    [System.Diagnostics.Conditional(
        "UNITY_EDITOR")]
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

        if (applyForward &&
            vehicleForward.sqrMagnitude >
                MinDirectionSqrMagnitude)
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
    /// VehiclePath上の指定距離へ即座に移動する。
    /// </summary>
    public void SetDistance(
        float distance)
    {
        if (!ValidatePath())
        {
            return;
        }

        _currentDistance =
            _vehiclePath.IsLoop
                ? RepeatDistance(
                    distance,
                    _vehiclePath.TotalLength)
                : Mathf.Clamp(
                    distance,
                    0f,
                    _vehiclePath.TotalLength);

        _hasStopped = false;

        SnapToCurrentPosition();
    }

    /// <summary>
    /// Inspectorで指定した開始地点へ戻す。
    /// </summary>
    public void Restart()
    {
        if (!ValidatePath())
        {
            return;
        }

        _isMovingBackward = false;
        _hasStopped = false;

        SetDistance(
            _vehiclePath.TotalLength *
            _startNormalizedPosition);
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        _moveSpeed =
            Mathf.Max(
                0f,
                _moveSpeed);

        _rotationSharpness =
            Mathf.Max(
                0f,
                _rotationSharpness);

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
        if (_vehiclePath == null ||
            _vehiclePath.TotalLength <= 0f)
        {
            return;
        }

        float distance =
            Application.isPlaying
                ? _currentDistance
                : _vehiclePath.TotalLength *
                  _startNormalizedPosition;

        Vector3 position =
            _vehiclePath.EvaluatePosition(
                distance);

        Vector3 forward =
            _vehiclePath.EvaluateForward(
                distance,
                _lookAheadDistance);

        if (_isMovingBackward)
        {
            forward = -forward;
        }

        if (forward.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            forward = transform.forward;
        }

        forward.Normalize();

        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                forward);

        if (right.sqrMagnitude <
            MinDirectionSqrMagnitude)
        {
            right = transform.right;
        }

        position +=
            right.normalized *
            _rightOffset;

        position +=
            Vector3.up *
            _upOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(
            position,
            0.4f);

        Gizmos.DrawLine(
            position,
            position +
            forward * 5f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            position,
            position +
            forward *
            _forwardPushDistance);
    }

#endif
}