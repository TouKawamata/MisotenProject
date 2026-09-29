using UnityEngine;

public class DummyRacer : MonoBehaviour, IBoostHitReceiver, IRacer
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
    [InspectorName("ブースト接触時の減速量")]
    [SerializeField] private float _boostSlowAmount = 20f;

    [InspectorName("減速時間")]
    [SerializeField] private float _boostSlowDuration = 1f;

    [InspectorName("ブースト接触時の横ずれ量")]
    [SerializeField] private float _boostSideMove = 1f;

    [Header("デバッグ設定")]
    [InspectorName("ログを出力する")]
    [SerializeField] private bool _enableLog = true;

    private float _distance;
    private float _resolvedStartDistance;

    private float _slowTimer;

    private Vector3 _estimatedVelocity;
    private Vector3 _previousPosition;

    private Vector3 _playerEstimatedVelocity;
    private Vector3 _previousPlayerPosition;

    private bool _isTouching;

    // RaceManagerから動かされているか（PlaceAtが呼ばれたらtrue）。trueの間はStart/Updateで動かない。
    private bool _isDrivenByRace;
    private bool _hasCapturedFinishSpeed;
    private float _speedAtFinish;

    public Transform Transform => transform;

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

        Move(Time.deltaTime, GetCurrentSpeed(Time.deltaTime));
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
        _hasCapturedFinishSpeed = false;
        _slowTimer = 0f;

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

        float currentSpeed = GetCurrentSpeed(deltaTime);

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

    // ブースト接触による減速を反映した、現在の速度
    private float GetCurrentSpeed(float deltaTime)
    {
        float currentSpeed = _speed;

        if (_slowTimer > 0f)
        {
            _slowTimer -= deltaTime;

            currentSpeed -= _boostSlowAmount;
            currentSpeed = Mathf.Max(0f, currentSpeed);
        }

        return currentSpeed;
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

        ApplyTransform(_distance);
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
            + right * _lateralOffset
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

    public void ReceiveBoostHit(
        Vector3 hitDirection
    )
    {
        _slowTimer = _boostSlowDuration;

        Vector3 right =
            _courseSpline
                .EvaluateRightByDistance(
                    _distance
                );

        float sideDirection =
            Vector3.Dot(
                hitDirection.normalized,
                right
            );

        _lateralOffset +=
            sideDirection * _boostSideMove;

        if (_enableLog)
        {
            Debug.Log(
                $"{name}: BoostHitを受けました。" +
                $" Direction = {hitDirection}",
                this
            );
        }
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
        ReceiveBoostHit(transform.right);
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