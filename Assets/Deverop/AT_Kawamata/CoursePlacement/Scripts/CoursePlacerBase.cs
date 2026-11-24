using System.Collections.Generic;
using UnityEngine;

// コースに沿って物を置く配置ツールの共通部分（看板・ギミック）。
// 置く場所を計算するだけで、生成はエディタのボタン（CoursePlacementGenerator）で行う。実行時は何もしない。
// 生成したものはContainer Nameの子にまとめて置き、作り直すたびに中身を消す。手で直したいものはコンテナの外へ移しておく。
// Course Networkを入れると本線と分岐すべてに置く。範囲（距離）は本線の距離で指定し、分岐は本線の距離に直して比べる（CourseTarget）。
public abstract class CoursePlacerBase : MonoBehaviour
{
    [Header("コース")]
    [Tooltip("本線と分岐をまとめて扱うときに入れる。入れると、本線と分岐すべてに置く（Course Splineは使わない）")]
    [SerializeField] protected CourseNetwork _courseNetwork;

    [Tooltip("Course Networkを使わないときの、置く基準のコース（1本だけ）")]
    [SerializeField] protected CourseSpline _courseSpline;

    [Header("乱数・出力")]
    [Tooltip("同じSeedなら毎回同じ配置になる")]
    [SerializeField] protected int _seed = 0;

    [Tooltip("生成したものをまとめる子オブジェクトの名前。作り直すとこの中身は消える")]
    [SerializeField] protected string _containerName = "_Generated";

    [Tooltip("選択中、置く予定の位置をSceneビューに表示する")]
    [SerializeField] protected bool _drawPreview = true;

    public CourseSpline CourseSpline => _courseSpline;

    public CourseNetwork CourseNetwork => _courseNetwork;

    public string ContainerName => string.IsNullOrEmpty(_containerName) ? "_Generated" : _containerName;

    // 範囲（距離）の基準にする本線。Course Networkがあればその本線、Course Splineが分岐ならその本線、それ以外はCourse Spline。
    protected CourseSpline MainSpline
    {
        get
        {
            if (_courseNetwork != null && _courseNetwork.MainSpline != null)
            {
                return _courseNetwork.MainSpline;
            }

            if (_courseSpline != null && _courseSpline.TryGetComponent(out CourseBranch branch) && branch.MainSpline != null)
            {
                return branch.MainSpline;
            }

            return _courseSpline;
        }
    }

    // 置く対象のコース（本線・分岐）。
    protected List<CourseTarget> GetTargets(bool includeMain = true, bool includeBranches = true)
    {
        return CourseTarget.Collect(_courseSpline, _courseNetwork, includeMain, includeBranches);
    }

    // 置くものの一覧を計算する（ワールド座標）。
    public abstract List<CoursePlacementItem> BuildPlacements();

    // 生成できる状態か。できないときはreasonに理由を入れる（エディタが警告に出す）。
    public virtual bool CanBuild(out string reason)
    {
        if (_courseNetwork != null)
        {
            if (_courseNetwork.MainSpline == null)
            {
                reason = "Course Network の Main Spline が未設定です";
                return false;
            }
        }
        else if (_courseSpline == null)
        {
            reason = "Course Network か Course Spline のどちらかを入れてください";
            return false;
        }

        if (GetTargets().Count == 0)
        {
            reason = "使えるコースがありません（Splineの長さが0か、分岐の分岐点・合流点を求められません）";
            return false;
        }

        reason = null;
        return true;
    }

    // 分岐のうち、分岐点・合流点を求められず使えないものの名前（エディタの警告用）。
    public List<string> GetInvalidBranchNames()
    {
        List<string> names = new List<string>();
        if (_courseNetwork == null || _courseNetwork.ConfiguredBranches == null)
        {
            return names;
        }

        foreach (CourseBranch branch in _courseNetwork.ConfiguredBranches)
        {
            if (branch == null)
            {
                continue;
            }

            if (branch.MainSpline != _courseNetwork.MainSpline)
            {
                names.Add($"{branch.name}（Main Spline が Course Network の本線と違う）");
                continue;
            }

            branch.Rebuild();
            if (!branch.IsValid)
            {
                names.Add($"{branch.name}（分岐点・合流点を求められない）");
            }
        }

        return names;
    }

#if UNITY_EDITOR
    protected virtual Color PreviewColor => new Color(1f, 0.8f, 0.2f, 0.9f);

    // 予定位置に描く枠の大きさ（ローカル）。
    protected virtual Vector3 PreviewSize => new Vector3(2f, 2f, 0.2f);

    private void OnDrawGizmosSelected()
    {
        if (!_drawPreview || !CanBuild(out _))
        {
            return;
        }

        foreach (CoursePlacementItem item in BuildPlacements())
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(item.Position, item.Rotation, Vector3.one * Mathf.Max(0.01f, item.Scale));

            Gizmos.color = PreviewColor;
            Gizmos.DrawWireCube(Vector3.zero, PreviewSize);

            // 青：プレハブの+Z（前）。赤：矢印の向き（看板のみ）。
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 2f);

            if (item.HasArrow)
            {
                float rad = item.ArrowAngle * Mathf.Deg2Rad;
                float length = Mathf.Max(0.5f, Mathf.Min(PreviewSize.x, PreviewSize.y) * 0.4f);
                Vector3 arrow = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * length;
                Vector3 side = new Vector3(-arrow.y, arrow.x, 0f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(-arrow, arrow);
                Gizmos.DrawLine(arrow, arrow * 0.5f + side * 0.4f);
                Gizmos.DrawLine(arrow, arrow * 0.5f - side * 0.4f);
            }

            Gizmos.matrix = previousMatrix;
        }
    }
#endif
}
