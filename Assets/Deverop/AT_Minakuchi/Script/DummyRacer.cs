using UnityEngine;

public class DummyRacer : MonoBehaviour, IBoostHitReceiver, IRacer, IBoostAttacker, IRacerContactBody
{
    private enum StartPositionMode
    {
        [InspectorName("絶対距離")]
        AbsoluteDistance,

        [InspectorName("Playerからの相対距離")]
        RelativeToPlayer
    }

    [Header("コース設定")]
    [InspectorName("コーススプライン")]
    [SerializeField] private CourseSpline _courseSpline;

    [Header("移動設定")]
    [InspectorName("速度")]
    [SerializeField] private float _speed = 50f;

    [Header("開始位置設定")]
    [InspectorName("開始位置モード")]
    [SerializeField]
    private StartPositionMode _startPositionMode
        = StartPositionMode.AbsoluteDistance;

    [InspectorName("開始距離")]
    [SerializeField] private float _startDistance = 0f;

    [InspectorName("Player")]
    [SerializeField] private Transform _player;

    [Header("位置オフセット")]
    [InspectorName("左右オフセット")]
    [SerializeField] private float _lateralOffset = 0f;

    [InspectorName("上下オフセット")]
    [SerializeField] private float _verticalOffset = 0f;

    [InspectorName("Playerと同じ左右位置を走る")]
    [Tooltip("ON：Playerと同じ左右位置を走る。OFF：左右オフセットの位置のまま走る（RaceManagerに登録されている場合は、配置されたグリッドの左右位置）")]
    [SerializeField]
    private bool _followPlayerLateralPosition = true;

    [Header("ブースト接触設定")]
    [Tooltip("ブーストを当てられたときの効果（スタン・横ずれ・無敵など）。Playerと同じアセットを使う")]
    [SerializeField] private BoostHitProfile _boostHitProfile;

    [Tooltip("押し出し切ってから、元の左右位置に戻るまでのおおよその秒数（押し出す距離・秒数はBoostHitProfileで設定）")]
    [SerializeField] private float _knockbackReturnTime = 1f;

    [Header("通常の接触")]
    [Tooltip("他のレーサーとの食い込みの計算に使う体のCollider。未設定なら、このGameObjectのColliderを使う（RaceManagerに登録されている場合だけ押し戻される）")]
    [SerializeField] private Collider _bodyCollider;

    [Header("デバッグ用ブースト")]
    [Tooltip("ON：一定間隔でブーストする（Playerがブーストを当てられる処理・シールドの確認用）。ブースト中に重なった相手へBoostHitDetectorが当てる")]
    [SerializeField] private bool _enableDebugBoost = false;

    [Tooltip("ブーストが終わってから、次のブーストまでの秒数")]
    [SerializeField] private float _debugBoostInterval = 5f;

    [Tooltip("1回のブーストの秒数")]
    [SerializeField] private float _debugBoostDuration = 2f;

    [Tooltip("ブースト中の速度の倍率")]
    [SerializeField] private float _debugBoostSpeedMultiplier = 1.5f;

    [Header("デバッグ設定")]
    [InspectorName("ログを出力する")]
    [SerializeField] private bool _enableLog = true;

    private float _distance;
    private float _resolvedStartDistance;

    private readonly StunController _stun = new StunController();

    // ブースト接触による横ずれ。左右オフセットとは別に持ち、押し出し切ったら時間で0（元の左右位置）に戻す
    private readonly LateralPush _lateralPush = new LateralPush();
    private float _knockbackOffset;
    private float _knockbackVelocity;

    private bool _isBoosting;
    private float _debugBoostTimer;

    private Vector3 _estimatedVelocity;
    private Vector3 _previousPosition;

    private Vector3 _playerEstimatedVelocity;
    private Vector3 _previousPlayerPosition;

    private bool _isTouching;

    // RaceManagerから動かされているか（PlaceAtが呼ばれたらtrue）。trueの間はStart/Updateで動かない。
    private bool _isDrivenByRace;
    private bool _isFinished;
    private bool _hasCapturedFinishSpeed;
    private float _speedAtFinish;

    public Transform Transform => transform;

    public bool IsBoosting => _isBoosting;

    // DummyRacerはシールドを使わない
    public bool IsShielding => false;

    public bool IsStunned => _stun.IsStunned;

    public Collider BodyCollider
    {
        get
        {
            if (_bodyCollider == null)
            {
                _bodyCollider = GetComponent<Collider>();
            }

            return _bodyCollider;
        }
    }

    public Vector3 Velocity => _estimatedVelocity;

    // ブースト中・横ずれ中は、通常の接触を処理しない（ブースト接触の処理を優先する）
    public bool IgnoresContact =>
        _courseSpline == null
        || _isBoosting
        || _lateralPush.IsActive;

    private void Start()
    {
        if (_courseSpline == null)
        {
            Debug.LogError(
                $"{name}: CourseSplineが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        // RaceManagerがすでにスタートグリッドへ配置しているので、開始位置で上書きしない
        if (_isDrivenByRace)
            return;

        _resolvedStartDistance = GetStartDistance();
        _distance = _resolvedStartDistance;

        ApplyTransform(_distance);

        _previousPosition = transform.position;

        if (_player != null)
        {
            _previousPlayerPosition = _player.position;
        }
    }

    private void Update()
    {
        // RaceManagerに登録されている場合は、RaceManagerからTickで動かす
        if (_isDrivenByRace)
            return;

        if (_courseSpline == null)
            return;

        UpdateStunAndBoost(Time.deltaTime, true);

        Move(Time.deltaTime, GetCurrentSpeed());
    }

    // RaceManagerからスタートグリッドへ配置されるときに呼ばれる。以降はTickで動かされる。
    // 向きはコースに沿った向きになるため、rotationは使わない。
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (_courseSpline == null)
        {
            Debug.LogError(
                $"{name}: CourseSplineが設定されていないため、スタートグリッドに配置できません。",
                this
            );

            return;
        }

        _isDrivenByRace = true;
        _isFinished = false;
        _hasCapturedFinishSpeed = false;
        ResetStunAndBoost();

        _distance = _courseSpline.FindNearestDistance(position);
        _resolvedStartDistance = _distance;

        // 配置された位置のコース中心からのズレを、左右・上下のオフセットとして持つ
        Vector3 offset =
            position
            - _courseSpline.EvaluatePositionByDistance(_distance);

        _lateralOffset =
            Vector3.Dot(
                offset,
                _courseSpline.EvaluateRightByDistance(_distance)
            );

        _verticalOffset =
            Vector3.Dot(
                offset,
                _courseSpline.EvaluateUpByDistance(_distance)
            );

        ApplyTransform(_distance);

        _previousPosition = transform.position;

        if (_player != null)
        {
            _previousPlayerPosition = _player.position;
        }
    }

    public void Tick(float deltaTime, IReadOnlyRacerData data)
    {
        if (_courseSpline == null)
            return;

        _isFinished = data.IsFinished;

        // ゴール後はブーストしない
        UpdateStunAndBoost(deltaTime, !_isFinished);

        float currentSpeed = GetCurrentSpeed();

        // ゴール後は、ゴールした瞬間の速度にSpeedMultiplierを掛けて減速・停止する
        if (data.IsFinished)
        {
            if (!_hasCapturedFinishSpeed)
            {
                _speedAtFinish = currentSpeed;
                _hasCapturedFinishSpeed = true;
            }

            currentSpeed = _speedAtFinish * data.SpeedMultiplier;
        }

        Move(deltaTime, currentSpeed);
    }

    // デバッグ用ブーストと、ブースト接触によるスタン中の減速を反映した、現在の速度
    private float GetCurrentSpeed()
    {
        float currentSpeed = _speed;

        if (_isBoosting)
        {
            currentSpeed *= _debugBoostSpeedMultiplier;
        }

        if (_stun.IsStunned && _boostHitProfile != null)
        {
            currentSpeed *= _boostHitProfile.StunSpeedMultiplier;
        }

        return currentSpeed;
    }

    private void UpdateStunAndBoost(float deltaTime, bool canBoost)
    {
        _stun.Tick(deltaTime);

        // 無効・ゴール後・スタン中はブーストしない（次のブーストまでの間隔は最初から数え直す）
        if (!_enableDebugBoost || !canBoost || _stun.IsStunned)
        {
            _isBoosting = false;
            _debugBoostTimer = 0f;
            return;
        }

        _debugBoostTimer += deltaTime;

        if (_isBoosting)
        {
            if (_debugBoostTimer >= _debugBoostDuration)
            {
                _isBoosting = false;
                _debugBoostTimer = 0f;
            }
        }
        else if (_debugBoostTimer >= _debugBoostInterval)
        {
            _isBoosting = true;
            _debugBoostTimer = 0f;

            if (_enableLog)
            {
                Debug.Log($"{name}: デバッグ用ブースト開始", this);
            }
        }
    }

    private void ResetStunAndBoost()
    {
        _stun.Reset();
        _isBoosting = false;
        _debugBoostTimer = 0f;
        _lateralPush.Stop();
        _knockbackOffset = 0f;
        _knockbackVelocity = 0f;
    }

    private void Move(float deltaTime, float currentSpeed)
    {
        UpdateVelocity(deltaTime);

        _distance += currentSpeed * deltaTime;

        // 非ループコースなら終点で開始地点に戻る（RaceManagerから動かされている場合は終点で止まる）
        if (!_courseSpline.IsLoop)
        {
            if (_distance >= _courseSpline.TotalLength)
            {
                _distance = _isDrivenByRace
                    ? _courseSpline.TotalLength
                    : _resolvedStartDistance;
            }
        }
        else
        {
            if (_courseSpline.TotalLength > 0f)
            {
                _distance =
                    Mathf.Repeat(
                        _distance,
                        _courseSpline.TotalLength
                    );
            }
        }

        UpdatePlayerLateralOffset();

        UpdateKnockbackOffset(deltaTime);

        ApplyTransform(_distance);
    }

    // 横ずれは、BoostHitProfileの距離・秒数で押し出してから、元の左右位置へ戻る
    private void UpdateKnockbackOffset(float deltaTime)
    {
        if (_lateralPush.IsActive)
        {
            _knockbackOffset += _lateralPush.Advance(deltaTime);
            _knockbackVelocity = 0f;
        }
        else
        {
            _knockbackOffset =
                Mathf.SmoothDamp(
                    _knockbackOffset,
                    0f,
                    ref _knockbackVelocity,
                    _knockbackReturnTime,
                    Mathf.Infinity,
                    deltaTime
                );
        }

        // トンネルの外には出さない
        float tunnelRadius = _courseSpline.TunnelRadius;
        if (tunnelRadius > 0f)
        {
            float lateral =
                Mathf.Clamp(
                    _lateralOffset + _knockbackOffset,
                    -tunnelRadius,
                    tunnelRadius
                );

            _knockbackOffset = lateral - _lateralOffset;
        }
    }

    private void UpdateVelocity(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        _estimatedVelocity =
            (transform.position - _previousPosition)
            / deltaTime;

        _previousPosition = transform.position;

        if (_player != null)
        {
            _playerEstimatedVelocity =
                (_player.position - _previousPlayerPosition)
                / deltaTime;

            _previousPlayerPosition = _player.position;
        }
    }

    private void UpdatePlayerLateralOffset()
    {
        if (!_followPlayerLateralPosition)
            return;

        if (_player == null)
            return;

        float playerDistance =
            _courseSpline.FindNearestDistance(
                _player.position
            );

        Vector3 center =
            _courseSpline.EvaluatePositionByDistance(
                playerDistance
            );

        Vector3 right =
            _courseSpline.EvaluateRightByDistance(
                playerDistance
            );

        Vector3 difference =
            _player.position - center;

        _lateralOffset =
            Vector3.Dot(difference, right);
    }

    private void ApplyTransform(float distance)
    {
        Vector3 position =
            _courseSpline.EvaluatePositionByDistance(
                distance
            );

        Vector3 forward =
            _courseSpline.EvaluateForwardByDistance(
                distance
            );

        Vector3 up =
            _courseSpline.EvaluateUpByDistance(
                distance
            );

        Vector3 right =
            _courseSpline.EvaluateRightByDistance(
                distance
            );

        transform.position =
            position
            + right * (_lateralOffset + _knockbackOffset)
            + up * _verticalOffset;

        if (forward.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    forward,
                    up
                );
        }
    }

    private float GetStartDistance()
    {
        float distance = _startDistance;

        if (_startPositionMode ==
            StartPositionMode.RelativeToPlayer)
        {
            if (_player == null)
            {
                Debug.LogWarning(
                    $"{name}: Playerが設定されていません。",
                    this
                );

                return 0f;
            }

            float playerDistance =
                _courseSpline.FindNearestDistance(
                    _player.position
                );

            distance =
                playerDistance + _startDistance;
        }

        if (_courseSpline.TotalLength <= 0f)
            return distance;

        if (_courseSpline.IsLoop)
        {
            distance =
                Mathf.Repeat(
                    distance,
                    _courseSpline.TotalLength
                );
        }
        else
        {
            distance =
                Mathf.Clamp(
                    distance,
                    0f,
                    _courseSpline.TotalLength
                );
        }

        return distance;
    }

    // ゴール後・無敵中 → 無視、お互いにブースト中 → 少しはじくだけ（ブーストは解除しない）、
    // それ以外 → スタン（減速）＋横ずれ（中心どうしが近いほど大きく押し出す）。DummyRacerはシールドを使わない
    public void ReceiveBoostHit(
        BoostHitInfo hit
    )
    {
        if (_boostHitProfile == null)
        {
            Debug.LogWarning(
                $"{name}: BoostHitProfileが設定されていないため、BoostHitを無視します。",
                this
            );

            return;
        }

        if (_courseSpline == null || _isFinished || _stun.IsInvincible)
            return;

        if (_isBoosting)
        {
            if (!_stun.CanBounce)
                return;

            _stun.BeginBounceInterval(
                _boostHitProfile.BounceInterval
            );

            StartPush(
                hit.PushDirection,
                _boostHitProfile.BounceDistance
            );

            if (_enableLog)
            {
                Debug.Log(
                    $"{name}: ブースト同士で接触しました（はじく）。" +
                    $" 距離 = {_boostHitProfile.BounceDistance:F1}m",
                    this
                );
            }

            return;
        }

        _stun.BeginStun(
            _boostHitProfile.StunDuration,
            _boostHitProfile.InvincibleDuration
        );

        float pushDistance =
            _boostHitProfile.EvaluatePushDistance(
                hit.Closeness
            );

        StartPush(
            hit.PushDirection,
            pushDistance
        );

        if (_enableLog)
        {
            Debug.Log(
                $"{name}: BoostHitを受けました（スタン）。" +
                $" 近さ = {hit.Closeness:F2} 横ずれ = {pushDistance:F1}m" +
                (hit.HasPushDirection ? "" : "（向き未確定→コース中央側）"),
                this
            );
        }
    }

    // 通常の接触で食い込んだとき、RaceManager（RacerContactResolver）から呼ばれる。
    // 左右の分は横ずれのオフセットへ、前後の分はコース上の距離へ足す（横ずれと同じく、時間で元の左右位置へ戻る）。
    // DummyRacerは速度を持たないので、blockNormalは使わない
    public void ApplyContactCorrection(
        Vector3 offset,
        Vector3 blockNormal
    )
    {
        if (_courseSpline == null)
            return;

        Vector3 right =
            _courseSpline
                .EvaluateRightByDistance(
                    _distance
                );

        Vector3 forward =
            _courseSpline
                .EvaluateForwardByDistance(
                    _distance
                );

        _knockbackOffset +=
            Vector3.Dot(offset, right);

        _distance =
            Mathf.Max(
                0f,
                _distance + Vector3.Dot(offset, forward)
            );

        ApplyTransform(_distance);
    }

    // 押し出す向きは左右どちらかだけを使う。向きが決まらないときは、コースの中央側へ押し出す
    private void StartPush(
        Vector3 pushDirection,
        float distance
    )
    {
        Vector3 right =
            _courseSpline
                .EvaluateRightByDistance(
                    _distance
                );

        float dot =
            Vector3.Dot(pushDirection, right);

        float side;
        if (Mathf.Abs(dot) > 0.0001f)
        {
            side = Mathf.Sign(dot);
        }
        else
        {
            side =
                _lateralOffset + _knockbackOffset > 0f
                    ? -1f
                    : 1f;
        }

        _lateralPush.Start(
            side * distance,
            _boostHitProfile.PushDuration
        );
    }

    [ContextMenu("開始位置に戻す")]
    private void ResetToStartPosition()
    {
        if (_courseSpline == null)
            return;

        _resolvedStartDistance =
            GetStartDistance();

        _distance =
            _resolvedStartDistance;

        ApplyTransform(_distance);

        if (_enableLog)
        {
            Debug.Log(
                $"{name}: 開始位置に戻しました。",
                this
            );
        }
    }

    [ContextMenu("テスト/BoostHitを手動実行")]
    private void TestBoostHit()
    {
        // 右から、中心どうしが一致した（最大の横ずれ）扱いで当てる
        ReceiveBoostHit(
            new BoostHitInfo(transform.right, 1f)
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        // Inspectorで指定したPlayer以外は無視
        if (!IsPlayer(other))
            return;

        _isTouching = true;

        if (!_enableLog)
            return;

        float relativeSpeed = GetRelativeSpeed(other);

        Debug.Log(
            $"{name}: Playerと接触開始 " +
            $"RelativeSpeed = {relativeSpeed:F2} m/s",
            this
        );
    }

    private void OnTriggerExit(Collider other)
    {
        // Inspectorで指定したPlayer以外は無視
        if (!IsPlayer(other))
            return;

        _isTouching = false;

        if (!_enableLog)
            return;

        Debug.Log(
            $"{name}: Playerと接触終了",
            this
        );
    }

    private bool IsPlayer(Collider other)
    {
        if (_player == null)
            return false;

        // Player本体にColliderがある場合
        if (other.transform == _player)
            return true;

        // Playerの子オブジェクトにColliderがある場合
        if (other.transform.IsChildOf(_player))
            return true;

        return false;
    }

    private float GetRelativeSpeed(
        Collider other
    )
    {
        if (_player != null)
        {
            Transform otherTransform =
                other.transform;

            if (otherTransform == _player ||
                otherTransform.IsChildOf(_player))
            {
                return (
                    _estimatedVelocity
                    - _playerEstimatedVelocity
                ).magnitude;
            }
        }

        Rigidbody otherRb =
            other.attachedRigidbody;

        if (otherRb != null)
        {
            return (
                _estimatedVelocity
                - otherRb.linearVelocity
            ).magnitude;
        }

        return _estimatedVelocity.magnitude;
    }

    private void OnDrawGizmos()
    {
        if (_isTouching)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                2f
            );
        }
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (_courseSpline == null)
            return;

        if (_courseSpline.TotalLength <= 0f)
            return;

        float previewDistance =
            GetStartDistance();

        ApplyTransform(previewDistance);
    }

#endif
}