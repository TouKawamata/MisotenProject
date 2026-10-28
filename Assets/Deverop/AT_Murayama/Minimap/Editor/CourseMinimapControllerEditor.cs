using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseMinimapController))]
public class CourseMinimapControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CourseMinimapController controller = (CourseMinimapController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("① 表示方式の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isModeA = controller.GetDisplayMode() == CourseMinimapController.MinimapDisplayMode.ModeA_RenderTexture;
            GUI.backgroundColor = isModeA ? new Color(0.3f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button("方式A：サブカメラ ＋ UI (RenderTexture)", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap to Mode A");
                controller.SetDisplayMode(CourseMinimapController.MinimapDisplayMode.ModeA_RenderTexture);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = !isModeA ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button("方式B：カメラ前 3Dミニチュアホログラム", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap to Mode B");
                controller.SetDisplayMode(CourseMinimapController.MinimapDisplayMode.ModeB_3DMiniature);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 表示範囲（ズーム ⇄ 全体）の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isAround = controller.GetScopeMode() == CourseMinimapController.MinimapScopeMode.AroundPlayer;
            GUI.backgroundColor = isAround ? new Color(1f, 0.85f, 0.35f) : Color.white;
            if (GUILayout.Button("🔍 周辺ズーム（自機中心・周囲切り抜き）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Scope to AroundPlayer");
                controller.SetScopeMode(CourseMinimapController.MinimapScopeMode.AroundPlayer);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = !isAround ? new Color(0.95f, 0.55f, 1f) : Color.white;
            if (GUILayout.Button("🗺️ コース全体マップ（全域俯瞰）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Scope to WholeCourse");
                controller.SetScopeMode(CourseMinimapController.MinimapScopeMode.WholeCourse);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("③ コース形状（帯リボン ⇄ 円柱トンネル）の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isCylinder = controller.GetMeshShape() == CourseMinimapController.MinimapMeshShape.CylinderTube;
            GUI.backgroundColor = !isCylinder ? new Color(0.5f, 0.9f, 0.9f) : Color.white;
            if (GUILayout.Button("🛣️ 平らな帯（フラットリボン）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Shape to FlatRibbon");
                controller.SetMeshShape(CourseMinimapController.MinimapMeshShape.FlatRibbon);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = isCylinder ? new Color(1f, 0.65f, 0.3f) : Color.white;
            if (GUILayout.Button("⭕ 円柱トンネル（CourseSplineギズモ風チューブ）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Shape to CylinderTube");
                controller.SetMeshShape(CourseMinimapController.MinimapMeshShape.CylinderTube);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6f);
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Rebuild（ミニマップ＆アイコン再生成）", GUILayout.Height(32f)))
        {
            foreach (Object targetObject in targets)
            {
                CourseMinimapController c = (CourseMinimapController)targetObject;
                Undo.RecordObject(c, "Rebuild CourseMinimap");
                c.Rebuild();
                EditorUtility.SetDirty(c);
            }
        }
    }
}
