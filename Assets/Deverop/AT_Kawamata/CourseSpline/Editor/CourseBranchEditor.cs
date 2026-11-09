using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseBranch))]
public class CourseBranchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CourseBranch branch = (CourseBranch)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("始点・終点を本線の中心線に合わせる"))
        {
            SnapEndsToMain(branch);
        }

        EditorGUILayout.Space();
        branch.Rebuild();
        if (!branch.IsValid)
        {
            EditorGUILayout.HelpBox(
                "分岐点・合流点を求められません。Main Spline を設定し、分岐のSplineの始点を分岐点、終点を合流点（分岐点より先）に置いてください。",
                MessageType.Warning);
            return;
        }

        // 計算した値は表示だけ（Splineの形から毎回計算し直す）。
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("分岐点（本線の距離 m）", branch.ForkDistance);
            EditorGUILayout.FloatField("合流点（本線の距離 m）", branch.MergeDistance);
            EditorGUILayout.FloatField("分岐の長さ（m）", branch.Spline.TotalLength);
            EditorGUILayout.FloatField("分岐の領域の終わり（分岐の距離 m）", branch.ForkZoneEnd);
            EditorGUILayout.FloatField("合流の領域の始まり（分岐の距離 m）", branch.MergeZoneStart);
        }

        if (branch.ForkZoneEnd >= branch.Spline.TotalLength)
        {
            EditorGUILayout.HelpBox("最後まで本線のトンネルと重なっていて、分岐の領域を抜けられません。分岐を本線から十分に離してください。", MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "分岐の領域（分岐点〜黄色の球）では、本線と分岐のうち、より深く入っている方のトンネルを走り、領域を抜けたところでルートを確定します。\n" +
            "合流の領域（黄色の球〜合流点）で本線の方へ深く入ると、本線に戻ります。\n" +
            "分岐のSplineは、分岐点では本線と同じ向きから、だんだん離していくと入りやすくなります。",
            MessageType.Info);
    }

    // 分岐のSplineの最初と最後の制御点を、本線の中心線の最寄り点へ動かす。
    private static void SnapEndsToMain(CourseBranch branch)
    {
        CourseSpline main = branch.MainSpline;
        CourseSpline spline = branch.Spline;
        if (main == null || spline == null || spline.PointCount < 2)
        {
            Debug.LogWarning($"{branch.name}: Main Spline が未設定か、分岐の制御点が2つ未満のため、合わせられません", branch);
            return;
        }

        Undo.SetCurrentGroupName("Snap Course Branch Ends To Main");
        int undoGroup = Undo.GetCurrentGroup();

        SnapPointToMain(main, spline.GetControlPoint(0));
        SnapPointToMain(main, spline.GetControlPoint(spline.PointCount - 1));

        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void SnapPointToMain(CourseSpline main, CourseSplinePoint point)
    {
        Transform pointTransform = point.transform;
        Vector3 snapped = main.EvaluatePositionByDistance(main.FindNearestDistance(pointTransform.position));
        Undo.RecordObject(pointTransform, "Snap Course Branch Ends To Main");
        pointTransform.position = snapped;
    }
}
