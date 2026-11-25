using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 車・電車・背景オブジェクト用の簡易走行パス。
///
/// 子Transformを制御点としてCatmull-Rom曲線を作り、
/// 距離ベースで位置と進行方向を取得できる。
///
/// レーサー用CourseSplineとは完全に独立している。
/// </summary>
[ExecuteAlways]
public sealed class VehiclePath : MonoBehaviour
{
    [Header("パス")]

    [Tooltip("ループする経路にする")]
    [SerializeField]
    private bool _isLoop;

    [Tooltip("制御点間のサンプル数。通常20～40程度")]
    [SerializeField, Min(4)]
    private int _samplesPerSegment = 30;

    [Header("Scene表示")]

    [SerializeField]
    private bool _drawPath = true;

    [SerializeField]
    private Color _pathColor =
        new Color(0.2f, 1f, 0.3f, 1f);

    [SerializeField, Min(0.01f)]
    private float _pointRadius = 0.5f;

    private readonly List<Transform> _points =
        new List<Transform>();

    private readonly List<Vector3> _positions =
        new List<Vector3>();

    private readonly List<float> _distances =
        new List<float>();

    private float _totalLength;

    private int _lastHash;

    public float TotalLength => _totalLength;

    public bool IsLoop => _isLoop;

    public int PointCount => _points.Count;

    private void Awake()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
        {
            RebuildIfDirty();
        }
    }

    private void OnValidate()
    {
        _samplesPerSegment =
            Mathf.Max(4, _samplesPerSegment);

        Rebuild();
    }
#endif

    // --------------------------------------------------
    // 公開API
    // --------------------------------------------------

    /// <summary>
    /// 指定距離の位置を取得する。
    /// </summary>
    public Vector3 EvaluatePosition(
        float distance)
    {
        if (_positions.Count == 0)
        {
            return transform.position;
        }

        if (_positions.Count == 1)
        {
            return _positions[0];
        }

        distance = WrapOrClamp(distance);

        FindSegment(
            distance,
            out int lower,
            out int upper,
            out float t);

        return Vector3.Lerp(
            _positions[lower],
            _positions[upper],
            t);
    }

    /// <summary>
    /// 指定距離の進行方向を取得する。
    /// lookAheadDistanceを使って実際の移動方向から求める。
    /// </summary>
    public Vector3 EvaluateForward(
        float distance,
        float lookAheadDistance = 1f)
    {
        if (_positions.Count < 2)
        {
            return transform.forward;
        }

        float offset =
            Mathf.Max(0.05f, lookAheadDistance);

        Vector3 current =
            EvaluatePosition(distance);

        Vector3 ahead =
            EvaluatePosition(distance + offset);

        Vector3 forward = ahead - current;

        // 非ループ経路の終端では前を見ることができないので、
        // 後方との差から方向を求める。
        if (forward.sqrMagnitude < 0.0001f)
        {
            Vector3 behind =
                EvaluatePosition(distance - offset);

            forward = current - behind;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            return transform.forward;
        }

        return forward.normalized;
    }

    /// <summary>
    /// Pathを明示的に再構築する。
    /// </summary>
    public void Rebuild()
    {
        CollectPoints();

        _positions.Clear();
        _distances.Clear();
        _totalLength = 0f;

        int count = _points.Count;

        if (count < 2)
        {
            _lastHash = ComputeHash();
            return;
        }

        int sections =
            _isLoop
                ? count
                : count - 1;

        int steps =
            sections *
            Mathf.Max(4, _samplesPerSegment);

        Vector3 previous = default;

        for (int step = 0;
             step <= steps;
             step++)
        {
            float normalizedT =
                (float)step / steps;

            Vector3 position =
                EvaluateCatmullRom(normalizedT);

            if (step > 0)
            {
                _totalLength +=
                    Vector3.Distance(
                        previous,
                        position);
            }

            _positions.Add(position);
            _distances.Add(_totalLength);

            previous = position;
        }

        _lastHash = ComputeHash();
    }

    // --------------------------------------------------
    // Catmull-Rom
    // --------------------------------------------------

    private Vector3 EvaluateCatmullRom(
        float normalizedT)
    {
        int count = _points.Count;

        if (count < 2)
        {
            return transform.position;
        }

        int sections =
            _isLoop
                ? count
                : count - 1;

        float scaledT =
            Mathf.Clamp01(normalizedT) *
            sections;

        int index =
            Mathf.FloorToInt(scaledT);

        float localT =
            scaledT - index;

        if (index >= sections)
        {
            index = sections - 1;
            localT = 1f;
        }

        Vector3 p0 =
            GetPointPosition(index - 1);

        Vector3 p1 =
            GetPointPosition(index);

        Vector3 p2 =
            GetPointPosition(index + 1);

        Vector3 p3 =
            GetPointPosition(index + 2);

        return CatmullRom(
            p0,
            p1,
            p2,
            p3,
            localT);
    }

    private static Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f *
        (
            2f * p1 +

            (-p0 + p2) * t +

            (2f * p0 -
             5f * p1 +
             4f * p2 -
             p3) * t2 +

            (-p0 +
             3f * p1 -
             3f * p2 +
             p3) * t3
        );
    }

    // --------------------------------------------------
    // 距離検索
    // --------------------------------------------------

    private void FindSegment(
        float distance,
        out int lower,
        out int upper,
        out float t)
    {
        int count = _distances.Count;

        if (distance <= _distances[0])
        {
            lower = 0;
            upper = 0;
            t = 0f;
            return;
        }

        if (distance >=
            _distances[count - 1])
        {
            lower = count - 1;
            upper = count - 1;
            t = 0f;
            return;
        }

        int low = 0;
        int high = count - 1;

        while (low < high - 1)
        {
            int mid = (low + high) / 2;

            if (_distances[mid] <= distance)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        lower = low;
        upper = high;

        float span =
            _distances[upper] -
            _distances[lower];

        t = span > 0.0001f
            ? (distance -
               _distances[lower]) / span
            : 0f;
    }

    private float WrapOrClamp(
        float distance)
    {
        if (_totalLength <= 0f)
        {
            return 0f;
        }

        if (!_isLoop)
        {
            return Mathf.Clamp(
                distance,
                0f,
                _totalLength);
        }

        float wrapped =
            distance % _totalLength;

        return wrapped < 0f
            ? wrapped + _totalLength
            : wrapped;
    }

    // --------------------------------------------------
    // Point
    // --------------------------------------------------

    private void CollectPoints()
    {
        _points.Clear();

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            Transform child =
                transform.GetChild(i);

            // VehiclePath自身の直下の子だけを
            // 制御点として使用する。
            _points.Add(child);
        }
    }

    private Vector3 GetPointPosition(
        int index)
    {
        int count = _points.Count;

        if (_isLoop)
        {
            index =
                ((index % count) + count) %
                count;
        }
        else
        {
            index =
                Mathf.Clamp(
                    index,
                    0,
                    count - 1);
        }

        return _points[index].position;
    }

    // --------------------------------------------------
    // Editor変更検知
    // --------------------------------------------------

    private void RebuildIfDirty()
    {
        int hash = ComputeHash();

        if (hash == _lastHash)
        {
            return;
        }

        Rebuild();
    }

    private int ComputeHash()
    {
        unchecked
        {
            int hash = 17;

            hash =
                hash * 31 +
                _isLoop.GetHashCode();

            hash =
                hash * 31 +
                _samplesPerSegment.GetHashCode();

            hash =
                hash * 31 +
                transform.childCount;

            for (int i = 0;
                 i < transform.childCount;
                 i++)
            {
                Transform child =
                    transform.GetChild(i);

                hash =
                    hash * 31 +
                    child.position.GetHashCode();
            }

            return hash;
        }
    }

    // --------------------------------------------------
    // Gizmos
    // --------------------------------------------------

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        RebuildIfDirty();
#endif

        if (!_drawPath ||
            _positions.Count < 2)
        {
            return;
        }

        Gizmos.color = _pathColor;

        for (int i = 1;
             i < _positions.Count;
             i++)
        {
            Gizmos.DrawLine(
                _positions[i - 1],
                _positions[i]);
        }

        Gizmos.color = Color.yellow;

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            Transform point =
                transform.GetChild(i);

            Gizmos.DrawSphere(
                point.position,
                _pointRadius);
        }
    }
}