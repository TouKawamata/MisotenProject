using System.Collections.Generic;
using UnityEngine;

// コース全体のSplineを管理するファサードクラス。
// Point管理・Catmull-Rom評価・距離テーブル・最近傍検索・ICourseGuide提供までをまとめて担当する。
[ExecuteAlways]
public class CourseSpline : MonoBehaviour, ICourseGuide
{
    [Tooltip("制御点の親オブジェクト。未設定の場合はこのGameObject自身の子を制御点として扱う")]
    [SerializeField] private Transform pointsContainer;

    [Tooltip("ループコースにするかどうか")]
    [SerializeField] private bool isLoop = false;

    [Tooltip("制御点間ごとの距離テーブルサンプル数（最低4）")]
    [SerializeField] private int samplesPerSegment = 20;

    [Tooltip("Editorの「Add Point」ボタンで延長する際の既定の間隔")]
    [SerializeField] private float defaultAddPointSpacing = 20f;

    private readonly List<CourseSplineSample> _samples = new List<CourseSplineSample>();
    private readonly List<Transform> _pointsBuffer = new List<Transform>();
    private int _lastDirtyHash;

    public float DefaultAddPointSpacing => defaultAddPointSpacing;

    public bool IsLoop => isLoop;

    private Transform PointsRoot => pointsContainer != null ? pointsContainer : transform;

    public int PointCount => GetPointTransforms().Count;

    public float TotalLength { get; private set; }

    private void Awake()
    {
        RebuildSamplesAndCacheHash();
    }

    private void OnValidate()
    {
        samplesPerSegment = Mathf.Max(4, samplesPerSegment);
        RebuildSamplesAndCacheHash();
    }

    private void Update()
    {
        // ランタイム中はPointが動かない前提のため、Editモードでのみ変更検知を行う。
        if (Application.isPlaying)
        {
            return;
        }

        int hash = ComputeDirtyHash();
        if (hash != _lastDirtyHash)
        {
            _lastDirtyHash = hash;
            RebuildSamples();
        }
    }

    // ----------------------------------------------------
    // 正規化t評価（ライブの子Transformから直接計算する）
    // ----------------------------------------------------

    public Vector3 EvaluatePosition(float normalizedT)
    {
        EvaluateAtNormalizedT(GetPointTransforms(), normalizedT, out Vector3 position, out _);
        return position;
    }

    // ----------------------------------------------------
    // 距離ベース評価（ベイクされた距離テーブルを使用する）
    // ----------------------------------------------------

    public Vector3 EvaluatePositionByDistance(float distance)
    {
        if (_samples.Count == 0)
        {
            return transform.position;
        }

        FindBracketingSamples(WrapOrClampDistance(distance), out int lower, out int upper, out float t);
        return Vector3.Lerp(_samples[lower].Position, _samples[upper].Position, t);
    }

    public Vector3 EvaluateForwardByDistance(float distance)
    {
        if (_samples.Count == 0)
        {
            return transform.forward;
        }

        FindBracketingSamples(WrapOrClampDistance(distance), out int lower, out int upper, out float t);
        Vector3 forward = Vector3.Slerp(_samples[lower].Forward, _samples[upper].Forward, t);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : _samples[lower].Forward;
    }

    public Vector3 EvaluateRightByDistance(float distance)
    {
        if (_samples.Count == 0)
        {
            return transform.right;
        }

        FindBracketingSamples(WrapOrClampDistance(distance), out int lower, out int upper, out float t);
        Vector3 right = Vector3.Slerp(_samples[lower].Right, _samples[upper].Right, t);
        return right.sqrMagnitude > 0.0001f ? right.normalized : _samples[lower].Right;
    }

    public Vector3 EvaluateUpByDistance(float distance)
    {
        if (_samples.Count == 0)
        {
            return transform.up;
        }

        FindBracketingSamples(WrapOrClampDistance(distance), out int lower, out int upper, out float t);
        Vector3 up = Vector3.Slerp(_samples[lower].Up, _samples[upper].Up, t);
        return up.sqrMagnitude > 0.0001f ? up.normalized : _samples[lower].Up;
    }

    public Vector3 EvaluateLookAhead(float distance, float lookAheadDistance)
    {
        return EvaluatePositionByDistance(distance + lookAheadDistance);
    }

    public float FindNearestDistance(Vector3 worldPosition)
    {
        if (_samples.Count == 0)
        {
            return 0f;
        }

        int nearestIndex = 0;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < _samples.Count; i++)
        {
            float sqrDistance = (_samples[i].Position - worldPosition).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestIndex = i;
            }
        }

        float bestDistance = _samples[nearestIndex].Distance;
        float bestSqrDistance = nearestSqrDistance;

        if (nearestIndex > 0)
        {
            RefineAgainstSegment(_samples[nearestIndex - 1], _samples[nearestIndex], worldPosition, ref bestDistance, ref bestSqrDistance);
        }

        if (nearestIndex < _samples.Count - 1)
        {
            RefineAgainstSegment(_samples[nearestIndex], _samples[nearestIndex + 1], worldPosition, ref bestDistance, ref bestSqrDistance);
        }

        return bestDistance;
    }

    // ----------------------------------------------------
    // ICourseGuide 実装
    // ----------------------------------------------------

    public Vector3 GetCenterPosition(Vector3 worldPosition)
    {
        return EvaluatePositionByDistance(FindNearestDistance(worldPosition));
    }

    public Vector3 GetForward(Vector3 worldPosition)
    {
        return EvaluateForwardByDistance(FindNearestDistance(worldPosition));
    }

    public Vector3 GetRight(Vector3 worldPosition)
    {
        return EvaluateRightByDistance(FindNearestDistance(worldPosition));
    }

    public Vector3 GetUp(Vector3 worldPosition)
    {
        return EvaluateUpByDistance(FindNearestDistance(worldPosition));
    }

    public Vector3 GetLookAheadPosition(Vector3 worldPosition, float lookAheadDistance)
    {
        return EvaluateLookAhead(FindNearestDistance(worldPosition), lookAheadDistance);
    }

    // ----------------------------------------------------
    // 内部処理
    // ----------------------------------------------------

    private void RebuildSamplesAndCacheHash()
    {
        RebuildSamples();
        _lastDirtyHash = ComputeDirtyHash();
    }

    private void RebuildSamples()
    {
        _samples.Clear();
        TotalLength = 0f;

        List<Transform> points = GetPointTransforms();
        int count = points.Count;
        if (count < 2)
        {
            return;
        }

        int clampedSamplesPerSegment = Mathf.Max(4, samplesPerSegment);
        int numSections = isLoop ? count : count - 1;
        int totalSteps = numSections * clampedSamplesPerSegment;

        Vector3 previousPosition = default;
        float accumulatedDistance = 0f;

        for (int step = 0; step <= totalSteps; step++)
        {
            float normalizedT = (float)step / totalSteps;
            EvaluateAtNormalizedT(points, normalizedT, out Vector3 position, out Vector3 forward);

            if (step > 0)
            {
                accumulatedDistance += Vector3.Distance(previousPosition, position);
            }

            Vector3 right = ComputeRight(forward);
            Vector3 up = Vector3.Cross(forward, right).normalized;

            _samples.Add(new CourseSplineSample(accumulatedDistance, normalizedT, position, forward, up, right));

            previousPosition = position;
        }

        TotalLength = accumulatedDistance;
    }

    private static Vector3 ComputeRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            // Forwardがワールド上方向とほぼ平行（垂直に近いコース）な場合のフォールバック。
            right = Vector3.Cross(Vector3.forward, forward);
        }

        return right.normalized;
    }

    private void EvaluateAtNormalizedT(List<Transform> points, float normalizedT, out Vector3 position, out Vector3 forward)
    {
        int count = points.Count;
        if (count < 2)
        {
            position = transform.position;
            forward = transform.forward;
            return;
        }

        int numSections = isLoop ? count : count - 1;
        float scaledT = Mathf.Clamp01(normalizedT) * numSections;
        int i = Mathf.FloorToInt(scaledT);
        float localT = scaledT - i;

        if (i >= numSections)
        {
            i = numSections - 1;
            localT = 1f;
        }

        Vector3 p0 = GetPointPosition(points, i - 1);
        Vector3 p1 = GetPointPosition(points, i);
        Vector3 p2 = GetPointPosition(points, i + 1);
        Vector3 p3 = GetPointPosition(points, i + 2);

        position = CourseSplineEvaluator.EvaluatePosition(p0, p1, p2, p3, localT);

        Vector3 tangent = CourseSplineEvaluator.EvaluateTangent(p0, p1, p2, p3, localT);
        forward = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
    }

    private Vector3 GetPointPosition(List<Transform> points, int index)
    {
        int count = points.Count;

        if (isLoop)
        {
            int wrapped = ((index % count) + count) % count;
            return points[wrapped].position;
        }

        int clamped = Mathf.Clamp(index, 0, count - 1);
        return points[clamped].position;
    }

    private List<Transform> GetPointTransforms()
    {
        _pointsBuffer.Clear();

        Transform root = PointsRoot;
        if (root == null)
        {
            return _pointsBuffer;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.TryGetComponent<CourseSplinePoint>(out _))
            {
                _pointsBuffer.Add(child);
            }
        }

        return _pointsBuffer;
    }

    private int ComputeDirtyHash()
    {
        List<Transform> points = GetPointTransforms();
        int hash = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            hash = unchecked(hash * 31 + points[i].position.GetHashCode());
        }

        hash = unchecked(hash * 31 + isLoop.GetHashCode());
        hash = unchecked(hash * 31 + samplesPerSegment.GetHashCode());

        return hash;
    }

    private static void RefineAgainstSegment(CourseSplineSample a, CourseSplineSample b, Vector3 worldPosition, ref float bestDistance, ref float bestSqrDistance)
    {
        Vector3 segment = b.Position - a.Position;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr < 0.0001f)
        {
            return;
        }

        float t = Mathf.Clamp01(Vector3.Dot(worldPosition - a.Position, segment) / segmentLengthSqr);
        Vector3 projected = a.Position + segment * t;
        float sqrDistance = (projected - worldPosition).sqrMagnitude;

        if (sqrDistance < bestSqrDistance)
        {
            bestSqrDistance = sqrDistance;
            bestDistance = Mathf.Lerp(a.Distance, b.Distance, t);
        }
    }

    private void FindBracketingSamples(float distance, out int lowerIndex, out int upperIndex, out float t)
    {
        int count = _samples.Count;

        if (count == 1 || distance <= _samples[0].Distance)
        {
            lowerIndex = 0;
            upperIndex = 0;
            t = 0f;
            return;
        }

        if (distance >= _samples[count - 1].Distance)
        {
            lowerIndex = count - 1;
            upperIndex = count - 1;
            t = 0f;
            return;
        }

        int low = 0;
        int high = count - 1;

        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (_samples[mid].Distance <= distance)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        lowerIndex = low;
        upperIndex = high;

        float lowerDistance = _samples[low].Distance;
        float upperDistance = _samples[high].Distance;
        float span = upperDistance - lowerDistance;
        t = span > 0.0001f ? (distance - lowerDistance) / span : 0f;
    }

    private float WrapOrClampDistance(float distance)
    {
        if (TotalLength <= 0f)
        {
            return 0f;
        }

        if (isLoop)
        {
            float wrapped = distance % TotalLength;
            return wrapped < 0f ? wrapped + TotalLength : wrapped;
        }

        return Mathf.Clamp(distance, 0f, TotalLength);
    }

    // ----------------------------------------------------
    // Scene View可視化（常にライブの子Transformから直接描画する）
    // ----------------------------------------------------

    private void OnDrawGizmos()
    {
        List<Transform> points = GetPointTransforms();
        int count = points.Count;
        if (count < 2)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Transform point = points[i];

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point.position, 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(point.position, point.position + point.up * 3f);
            Gizmos.DrawSphere(point.position + point.up * 3f, 0.3f);
        }

        int numSections = isLoop ? count : count - 1;
        int resolution = numSections * 20;

        Gizmos.color = Color.yellow;
        Vector3 previousPosition = default;

        for (int step = 0; step <= resolution; step++)
        {
            float normalizedT = (float)step / resolution;
            EvaluateAtNormalizedT(points, normalizedT, out Vector3 position, out Vector3 forward);

            if (step > 0)
            {
                Gizmos.DrawLine(previousPosition, position);
            }

            previousPosition = position;

            if (step % 5 == 0)
            {
                Vector3 right = ComputeRight(forward);
                Vector3 up = Vector3.Cross(forward, right).normalized;

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(position, position + up * 1.5f);
                Gizmos.DrawSphere(position + up * 1.5f, 0.1f);
                Gizmos.color = Color.yellow;
            }
        }
    }
}
