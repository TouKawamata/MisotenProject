using System.Collections.Generic;
using UnityEngine;

// コース全体のSplineを管理するファサードクラス。
// Point管理・Catmull-Rom評価・距離テーブル・最近傍検索・ICourseGuide提供までをまとめて担当する。
// 向き（Up/Right）とトンネル半径はベイクしたサンプルからだけ取る。上方向は、前のサンプルの上方向を進行方向の変化分だけ
// 回して引き継ぐ（ねじれない）。勾配が緩い区間はワールドの真上に合わせ、最後に制御点のBank Angleの分だけ進行方向を軸に回す。
[ExecuteAlways]
public class CourseSpline : MonoBehaviour, ICourseGuide
{
    [Tooltip("制御点の親オブジェクト。未設定の場合はこのGameObject自身の子を制御点として扱う")]
    [SerializeField] private Transform pointsContainer;

    [Tooltip("ループコースにするかどうか（始点と終点は水平な区間にすること。上方向のずれが残らないように）")]
    [SerializeField] private bool isLoop = false;

    [Tooltip("制御点間ごとの距離テーブルサンプル数（最低4）")]
    [SerializeField] private int samplesPerSegment = 20;

    [Tooltip("Editorの「Add Point」ボタンで延長する際の既定の間隔")]
    [SerializeField] private float defaultAddPointSpacing = 20f;

    [Header("上方向の作り方（勾配に応じてワールドの真上に合わせる）")]
    [Tooltip("勾配（度）がこれ以下の区間は、上方向をワールドの真上に合わせる（水平な区間は従来と同じ向きになる）")]
    [SerializeField] private float worldUpBlendStartSlope = 45f;

    [Tooltip("勾配（度）がこれ以上の区間は、ワールドの真上には合わせず、前の上方向をそのまま引き継ぐ。Startとの間はなめらかに切り替える")]
    [SerializeField] private float worldUpBlendEndSlope = 75f;

    [Header("トンネル半径（コースの飛行可能範囲。円形境界）")]
    [Tooltip("標準の半径。制御点のRadius Scaleを掛けた値がその場所の半径になる")]
    [SerializeField] private float tunnelRadius = 15f;
    [SerializeField] private bool drawTunnelGizmos = false;
    [SerializeField] private int tunnelGizmoSegments = 16;
    [SerializeField] private float tunnelGizmoInterval = 20f;

    private readonly List<CourseSplineSample> _samples = new List<CourseSplineSample>();

    // サンプルごとの、バンクをかける前の上方向。エディタでRotationをBank Angleへ変換するときの基準に使う。
    private readonly List<Vector3> _baseUps = new List<Vector3>();

    private readonly List<CourseSplinePoint> _pointsBuffer = new List<CourseSplinePoint>();
    private int _lastDirtyHash;

    public float DefaultAddPointSpacing => defaultAddPointSpacing;

    public bool IsLoop => isLoop;

    // 標準の半径（Radius Scaleを掛ける前）。その場所の半径はGetTunnelRadiusByDistance／GetTunnelRadiusで取る。
    public float TunnelRadius => tunnelRadius;

    private Transform PointsRoot => pointsContainer != null ? pointsContainer : transform;

    public int PointCount => GetPoints().Count;

    public float TotalLength { get; private set; }

    private void Awake()
    {
        RebuildSamplesAndCacheHash();
    }

    private void OnValidate()
    {
        samplesPerSegment = Mathf.Max(4, samplesPerSegment);
        worldUpBlendStartSlope = Mathf.Clamp(worldUpBlendStartSlope, 0f, 88f);
        worldUpBlendEndSlope = Mathf.Clamp(worldUpBlendEndSlope, worldUpBlendStartSlope + 1f, 89f);
        RebuildSamplesAndCacheHash();
    }

    private void Update()
    {
        // ランタイム中はPointが動かない前提のため、Editモードでのみ変更検知を行う。
        if (Application.isPlaying)
        {
            return;
        }

        RebuildIfDirty();
    }

    // ----------------------------------------------------
    // 正規化t評価（ライブの子Transformから直接計算する）
    // ----------------------------------------------------

    public Vector3 EvaluatePosition(float normalizedT)
    {
        EvaluateAtNormalizedT(GetPoints(), normalizedT, out Vector3 position, out _, out _, out _);
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

    // その距離でのトンネル半径（標準の半径×制御点のRadius Scaleを補間した倍率）。
    public float GetTunnelRadiusByDistance(float distance)
    {
        if (_samples.Count == 0)
        {
            return tunnelRadius;
        }

        FindBracketingSamples(WrapOrClampDistance(distance), out int lower, out int upper, out float t);
        return tunnelRadius * Mathf.Lerp(_samples[lower].RadiusScale, _samples[upper].RadiusScale, t);
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
        // 中心線上の先読み点をそのまま返すと、worldPositionが中心からズレているときに
        // カメラが常に中心方向へ引っ張られる向きになってしまう。
        // worldPositionの「中心からのズレ」を先読み点にも同じだけ足すことで、
        // 中心線と平行にオフセットした先読み点（＝実際の進行方向に近い狙い先）にする。
        float distance = FindNearestDistance(worldPosition);
        Vector3 offsetFromCenter = worldPosition - EvaluatePositionByDistance(distance);
        return EvaluateLookAhead(distance, lookAheadDistance) + offsetFromCenter;
    }

    public float GetTunnelRadius(Vector3 worldPosition)
    {
        return GetTunnelRadiusByDistance(FindNearestDistance(worldPosition));
    }

    // ----------------------------------------------------
    // エディタ用（RotationをBank Angleへ変換する）
    // ----------------------------------------------------

    public CourseSplinePoint GetControlPoint(int index)
    {
        return GetPoints()[index];
    }

    // 制御点の位置での、進行方向とバンクをかける前の上方向。
    public bool TryGetBaseFrameAtPoint(int pointIndex, out Vector3 forward, out Vector3 baseUp)
    {
        RebuildIfDirty();

        int sampleIndex = pointIndex * Mathf.Max(4, samplesPerSegment);
        if (pointIndex < 0 || sampleIndex >= _samples.Count)
        {
            forward = Vector3.forward;
            baseUp = Vector3.up;
            return false;
        }

        forward = _samples[sampleIndex].Forward;
        baseUp = _baseUps[sampleIndex];
        return true;
    }

    // ----------------------------------------------------
    // 内部処理
    // ----------------------------------------------------

    private void RebuildSamplesAndCacheHash()
    {
        RebuildSamples();
        _lastDirtyHash = ComputeDirtyHash();
    }

    private void RebuildIfDirty()
    {
        int hash = ComputeDirtyHash();
        if (hash != _lastDirtyHash)
        {
            _lastDirtyHash = hash;
            RebuildSamples();
        }
    }

    private void RebuildSamples()
    {
        _samples.Clear();
        _baseUps.Clear();
        TotalLength = 0f;

        List<CourseSplinePoint> points = GetPoints();
        int count = points.Count;
        if (count < 2)
        {
            return;
        }

        int clampedSamplesPerSegment = Mathf.Max(4, samplesPerSegment);
        int numSections = isLoop ? count : count - 1;
        int totalSteps = numSections * clampedSamplesPerSegment;

        Vector3 previousPosition = default;
        Vector3 previousForward = transform.forward;
        Vector3 previousBaseUp = default;
        float accumulatedDistance = 0f;

        for (int step = 0; step <= totalSteps; step++)
        {
            float normalizedT = (float)step / totalSteps;
            EvaluateAtNormalizedT(points, normalizedT, out Vector3 position, out Vector3 tangent, out float bankAngle, out float radiusScale);

            Vector3 forward = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : previousForward;

            Vector3 baseUp;
            if (step == 0)
            {
                baseUp = OrthonormalizeUp(Vector3.up, forward, Vector3.forward);
            }
            else
            {
                accumulatedDistance += Vector3.Distance(previousPosition, position);
                baseUp = ComputeNextBaseUp(previousForward, previousBaseUp, forward);
            }

            // ワールドの真上に合わせた後でバンクをかけるので、バンク180°で逆さにしても真上へ戻されない。
            Vector3 up = Quaternion.AngleAxis(bankAngle, forward) * baseUp;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;

            _samples.Add(new CourseSplineSample(accumulatedDistance, normalizedT, position, forward, up, right, radiusScale));
            _baseUps.Add(baseUp);

            previousPosition = position;
            previousForward = forward;
            previousBaseUp = baseUp;
        }

        TotalLength = accumulatedDistance;
    }

    // 前のサンプルの上方向を、進行方向の変化分だけ回して引き継ぐ（ねじれない）。
    // 勾配が緩いほどワールドの真上に寄せる（Start以下で完全に真上、End以上で引き継いだまま）。
    private Vector3 ComputeNextBaseUp(Vector3 previousForward, Vector3 previousBaseUp, Vector3 forward)
    {
        Vector3 transported = Quaternion.FromToRotation(previousForward, forward) * previousBaseUp;
        transported = OrthonormalizeUp(transported, forward, previousBaseUp);

        float slope = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(forward.y))) * Mathf.Rad2Deg;
        float keepWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(worldUpBlendStartSlope, worldUpBlendEndSlope, slope));
        if (keepWeight >= 1f)
        {
            return transported;
        }

        Vector3 worldAligned = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (worldAligned.sqrMagnitude < 0.000001f)
        {
            return transported;
        }

        return OrthonormalizeUp(Vector3.Slerp(worldAligned.normalized, transported, keepWeight), forward, transported);
    }

    // candidateをforwardに直交する単位ベクトルにする。forwardとほぼ平行な縮退ケースではfallback→ワールドForwardの順に使う。
    private static Vector3 OrthonormalizeUp(Vector3 candidate, Vector3 forward, Vector3 fallback)
    {
        Vector3 up = Vector3.ProjectOnPlane(candidate, forward);
        if (up.sqrMagnitude < 0.000001f)
        {
            up = Vector3.ProjectOnPlane(fallback, forward);
        }

        if (up.sqrMagnitude < 0.000001f)
        {
            up = Vector3.ProjectOnPlane(Vector3.forward, forward);
        }

        return up.normalized;
    }

    // 位置・接線（未正規化）と、制御点のBank Angle／Radius Scaleを補間した値を求める。
    // Bank Angleは角度をそのまま線形補間（0→270なら4分の3回転）、Radius Scaleはなめらかに補間する。
    private void EvaluateAtNormalizedT(List<CourseSplinePoint> points, float normalizedT, out Vector3 position, out Vector3 tangent, out float bankAngle, out float radiusScale)
    {
        int count = points.Count;
        if (count < 2)
        {
            position = transform.position;
            tangent = transform.forward;
            bankAngle = 0f;
            radiusScale = 1f;
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
        tangent = CourseSplineEvaluator.EvaluateTangent(p0, p1, p2, p3, localT);

        CourseSplinePoint point1 = GetPoint(points, i);
        CourseSplinePoint point2 = GetPoint(points, i + 1);
        bankAngle = Mathf.Lerp(point1.BankAngle, point2.BankAngle, localT);
        radiusScale = Mathf.SmoothStep(point1.RadiusScale, point2.RadiusScale, localT);
    }

    private Vector3 GetPointPosition(List<CourseSplinePoint> points, int index)
    {
        return GetPoint(points, index).transform.position;
    }

    private CourseSplinePoint GetPoint(List<CourseSplinePoint> points, int index)
    {
        int count = points.Count;

        if (isLoop)
        {
            int wrapped = ((index % count) + count) % count;
            return points[wrapped];
        }

        int clamped = Mathf.Clamp(index, 0, count - 1);
        return points[clamped];
    }

    private List<CourseSplinePoint> GetPoints()
    {
        _pointsBuffer.Clear();

        Transform root = PointsRoot;
        if (root == null)
        {
            return _pointsBuffer;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).TryGetComponent(out CourseSplinePoint point))
            {
                _pointsBuffer.Add(point);
            }
        }

        return _pointsBuffer;
    }

    private int ComputeDirtyHash()
    {
        List<CourseSplinePoint> points = GetPoints();
        int hash = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            hash = unchecked(hash * 31 + points[i].transform.position.GetHashCode());
            hash = unchecked(hash * 31 + points[i].BankAngle.GetHashCode());
            hash = unchecked(hash * 31 + points[i].RadiusScale.GetHashCode());
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
    // Scene View可視化（ゲーム側と同じく、ベイクしたサンプルから描画する）
    // ----------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            RebuildIfDirty();
        }

        if (_samples.Count < 2)
        {
            return;
        }

        // 制御点：赤い球と、その位置でのコースの上方向（緑）
        List<CourseSplinePoint> points = GetPoints();
        int clampedSamplesPerSegment = Mathf.Max(4, samplesPerSegment);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 pointPosition = points[i].transform.position;
            Vector3 pointUp = _samples[Mathf.Min(i * clampedSamplesPerSegment, _samples.Count - 1)].Up;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pointPosition, 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(pointPosition, pointPosition + pointUp * 3f);
            Gizmos.DrawSphere(pointPosition + pointUp * 3f, 0.3f);
        }

        float nextTunnelSectionDistance = 0f;
        Vector3[] previousTunnelSection = null;

        for (int i = 0; i < _samples.Count; i++)
        {
            CourseSplineSample sample = _samples[i];

            if (i > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_samples[i - 1].Position, sample.Position);
            }

            if (drawTunnelGizmos && tunnelRadius > 0f && sample.Distance >= nextTunnelSectionDistance)
            {
                Vector3[] section = BuildTunnelSection(sample.Position, sample.Right, sample.Up, tunnelRadius * sample.RadiusScale);
                DrawTunnelSection(section, previousTunnelSection);
                previousTunnelSection = section;
                nextTunnelSectionDistance += tunnelGizmoInterval;
            }

            if (i % 5 == 0)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(sample.Position, sample.Position + sample.Up * 1.5f);
                Gizmos.DrawSphere(sample.Position + sample.Up * 1.5f, 0.1f);
            }
        }
    }

    private Vector3[] BuildTunnelSection(Vector3 center, Vector3 right, Vector3 up, float radius)
    {
        int segments = Mathf.Max(3, tunnelGizmoSegments);
        Vector3[] section = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            section[i] = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
        }

        return section;
    }

    private static void DrawTunnelSection(Vector3[] section, Vector3[] previousSection)
    {
        Gizmos.color = Color.magenta;

        for (int i = 0; i < section.Length; i++)
        {
            int next = (i + 1) % section.Length;
            Gizmos.DrawLine(section[i], section[next]);

            if (previousSection != null)
            {
                Gizmos.DrawLine(previousSection[i], section[i]);
            }
        }
    }
}
