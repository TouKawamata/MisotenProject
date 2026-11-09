using UnityEngine;

// 本線（CourseSpline）から分かれて、また本線へ合流する分岐の1本。分岐のCourseSplineと同じGameObjectに付ける。
// 分岐のSplineの始点（分岐点）と終点（合流点）は、本線の中心線の上に置く（エディタの「始点・終点を本線の中心線に合わせる」）。
// 分岐点・合流点の本線上の距離と、本線のトンネルと重なっている区間（分岐の領域・合流の領域）は、Splineの形から計算する。
// ・分岐の領域：分岐点から、本線と分岐のトンネルが離れるまで。この中では、より深く入っている方のトンネルを走る（CourseRoute）。
// ・合流の領域：本線と分岐のトンネルが再び重なり始めてから、合流点まで。本線の方へ深く入ったら本線に戻る。
// 進行度（順位）は、分岐の上の距離を本線の距離に直して比べる（MainEquivalentDistance）。
[RequireComponent(typeof(CourseSpline))]
public class CourseBranch : MonoBehaviour
{
    // 重なりを調べる間隔（m）。
    private const float OverlapScanStep = 1f;

    [Tooltip("分かれる元の本線。分岐のSplineの始点・終点は、この中心線の上に置く")]
    [SerializeField] private CourseSpline _mainSpline;

    // 以下はSplineの形から計算する（Rebuild）。Inspectorには、エディタ（CourseBranchEditor）が表示だけする。
    private float _forkDistance;
    private float _mergeDistance;
    private float _forkZoneEnd;
    private float _mergeZoneStart;

    private CourseSpline _spline;

    public CourseSpline MainSpline => _mainSpline;

    public CourseSpline Spline => _spline != null ? _spline : _spline = GetComponent<CourseSpline>();

    // 分岐点・合流点の本線上の距離（m）。
    public float ForkDistance => _forkDistance;

    public float MergeDistance => _mergeDistance;

    // 分岐の領域の終わり（分岐の上の距離、m）と、そこに対応する本線の距離（m）。
    public float ForkZoneEnd => _forkZoneEnd;

    public float ForkZoneEndOnMain { get; private set; }

    // 合流の領域の始まり（分岐の上の距離、m）と、そこに対応する本線の距離（m）。
    public float MergeZoneStart => _mergeZoneStart;

    public float MergeZoneStartOnMain { get; private set; }

    // 計算した結果が使えるか（本線・分岐のSplineがあり、合流点が分岐点より先にある）。
    public bool IsValid { get; private set; }

    // 分岐の上の距離（m）を、本線の距離（m）に直す（分岐点〜合流点の本線の長さに、分岐の長さを比例で合わせる）。
    public float MainEquivalentDistance(float branchDistance)
    {
        float branchLength = Spline.TotalLength;
        if (branchLength <= 0f)
        {
            return _forkDistance;
        }

        float rate = Mathf.Clamp01(branchDistance / branchLength);
        return Mathf.Lerp(_forkDistance, _mergeDistance, rate);
    }

    // 分岐点・合流点と、トンネルが重なっている区間を計算し直す。本線・分岐のSplineの距離テーブルができてから呼ぶ。
    public void Rebuild()
    {
        IsValid = false;
        CourseSpline branch = Spline;
        if (_mainSpline == null || branch == null || _mainSpline == branch || _mainSpline.TotalLength <= 0f || branch.TotalLength <= 0f)
        {
            return;
        }

        float branchLength = branch.TotalLength;
        _forkDistance = _mainSpline.FindNearestDistance(branch.EvaluatePositionByDistance(0f));
        _mergeDistance = _mainSpline.FindNearestDistance(branch.EvaluatePositionByDistance(branchLength));
        if (_mergeDistance <= _forkDistance)
        {
            return;
        }

        // 分岐点から先へ、トンネルが離れるところを探す。
        _forkZoneEnd = branchLength;
        for (float distance = 0f; distance <= branchLength; distance += OverlapScanStep)
        {
            if (!IsOverlappingMain(distance))
            {
                _forkZoneEnd = distance;
                break;
            }
        }

        // 合流点から手前へ、トンネルが離れているところを探す。
        _mergeZoneStart = _forkZoneEnd;
        for (float distance = branchLength; distance >= _forkZoneEnd; distance -= OverlapScanStep)
        {
            if (!IsOverlappingMain(distance))
            {
                _mergeZoneStart = Mathf.Min(distance + OverlapScanStep, branchLength);
                break;
            }
        }

        ForkZoneEndOnMain = _mainSpline.FindNearestDistance(branch.EvaluatePositionByDistance(_forkZoneEnd));
        MergeZoneStartOnMain = _mainSpline.FindNearestDistance(branch.EvaluatePositionByDistance(_mergeZoneStart));
        IsValid = true;
    }

    // 分岐の上の距離distanceの地点で、本線のトンネルと重なっているか（中心どうしの距離 < 半径の和）。
    private bool IsOverlappingMain(float distance)
    {
        CourseSpline branch = Spline;
        Vector3 branchCenter = branch.EvaluatePositionByDistance(distance);
        float mainDistance = _mainSpline.FindNearestDistance(branchCenter);
        Vector3 mainCenter = _mainSpline.EvaluatePositionByDistance(mainDistance);
        float radiusSum = branch.GetTunnelRadiusByDistance(distance) + _mainSpline.GetTunnelRadiusByDistance(mainDistance);
        return (branchCenter - mainCenter).sqrMagnitude < radiusSum * radiusSum;
    }

    private void OnDrawGizmosSelected()
    {
        Rebuild();
        if (!IsValid)
        {
            return;
        }

        CourseSpline branch = Spline;

        // 分岐点・合流点（本線の上）：水色。分岐の領域の終わり・合流の領域の始まり（分岐の上）：黄色。
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_mainSpline.EvaluatePositionByDistance(_forkDistance), 3f);
        Gizmos.DrawWireSphere(_mainSpline.EvaluatePositionByDistance(_mergeDistance), 3f);

        Gizmos.color = Color.yellow;
        Vector3 forkZoneEnd = branch.EvaluatePositionByDistance(_forkZoneEnd);
        Vector3 mergeZoneStart = branch.EvaluatePositionByDistance(_mergeZoneStart);
        Gizmos.DrawWireSphere(forkZoneEnd, 2f);
        Gizmos.DrawWireSphere(mergeZoneStart, 2f);
        Gizmos.DrawLine(forkZoneEnd, _mainSpline.EvaluatePositionByDistance(ForkZoneEndOnMain));
        Gizmos.DrawLine(mergeZoneStart, _mainSpline.EvaluatePositionByDistance(MergeZoneStartOnMain));
    }
}
