using System.Collections.Generic;
using UnityEngine;

// 本線（CourseSpline）と、そこから分かれる分岐（CourseBranch）の組。RaceManagerの「Course Network」の欄に入れると、
// 参加者ごとにルート（CourseRoute：今どの区間を走っているか）を作って渡す。未設定なら、今まで通り本線1本だけで走る。
// 対応しているのは「本線から分かれて、本線へ戻る」分岐だけ（分岐からさらに分かれる分岐は対応していない）。
// 同じところから複数の分岐が分かれてもよい（分岐の領域の中で、一番深く入っているトンネルを走る）。
public class CourseNetwork : MonoBehaviour
{
    [Tooltip("本線。RaceManagerのCourse Splineと同じものを入れる（スタート・ゴール・進行度の基準）")]
    [SerializeField] private CourseSpline _mainSpline;

    [Tooltip("本線から分かれる分岐。順番は問わない（分岐点の距離の順に並べ直して使う）")]
    [SerializeField] private CourseBranch[] _branches = new CourseBranch[0];

    [Header("区間の切り替え")]
    [Tooltip("走る区間（本線／分岐）が切り替わったとき、前の区間の向き（前・右・上）から新しい区間の向きへ、なめらかに移す距離（m。新しい区間に沿って進んだ距離）。" +
             "0なら一瞬で切り替わる（機首・カメラの向きが、2本のSplineの向きの差の分だけ跳ぶ）。中心・トンネル半径は、壁の判定のため、すぐ新しい区間のものにする")]
    [Min(0f)]
    [SerializeField] private float _transitionDistance = 30f;

    [Header("デバッグ")]
    [Tooltip("ONなら、区間が切り替わるたびに、前後の区間の向きの差・中心のずれをConsoleに出す（つなぎ目の確認用）")]
    [SerializeField] private bool _showDebugLogs = true;

    private readonly List<CourseBranch> _validBranches = new List<CourseBranch>();
    private bool _isBuilt;

    public CourseSpline MainSpline => _mainSpline;

    public float TransitionDistance => Mathf.Max(0f, _transitionDistance);

    public bool ShowDebugLogs => _showDebugLogs;

    // 使える分岐（分岐点の距離の順）。Build前は空。
    public IReadOnlyList<CourseBranch> Branches => _validBranches;

    public CourseRoute CreateRoute()
    {
        return new CourseRoute(this);
    }

    // 分岐の分岐点・合流点を計算する（1回だけ）。全SplineのAwake（距離テーブルの作成）が終わった後に呼ぶ。
    // CourseRouteが最初に使うときに呼ぶので、外から呼ばなくてもよい。
    public void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _validBranches.Clear();
        if (_mainSpline == null)
        {
            _isBuilt = true;
            Debug.LogError("CourseNetwork: Main Spline が未設定です", this);
            return;
        }

        // 本線の距離テーブルがまだ無い（SplineのAwakeの前）なら、次に呼ばれたときに計算する。
        if (_mainSpline.TotalLength <= 0f)
        {
            return;
        }

        _isBuilt = true;

        foreach (CourseBranch branch in _branches)
        {
            if (branch == null)
            {
                continue;
            }

            if (branch.MainSpline != _mainSpline)
            {
                Debug.LogError($"CourseNetwork: {branch.name} の Main Spline が、このCourseNetworkの本線と違うため使いません", branch);
                continue;
            }

            branch.Rebuild();
            if (!branch.IsValid)
            {
                Debug.LogError($"CourseNetwork: {branch.name} の分岐点・合流点を求められないため使いません（合流点が分岐点より先にあるか、Splineの制御点を確かめてください）", branch);
                continue;
            }

            if (branch.ForkZoneEnd >= branch.Spline.TotalLength)
            {
                Debug.LogWarning($"CourseNetwork: {branch.name} は最後まで本線のトンネルと重なっていて、分岐の領域を抜けられません。本線から十分に離してください", branch);
            }

            _validBranches.Add(branch);
        }

        _validBranches.Sort((a, b) => a.ForkDistance.CompareTo(b.ForkDistance));
    }
}
