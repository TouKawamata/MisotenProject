using UnityEditor;
using UnityEngine;

// 配置ツール（看板・ギミック）のInspectorに「生成」「クリア」「Seedを変えて生成」ボタンと、設定の警告を出す。
// CoursePlacerBaseを継承したもの（CourseSignboardPlacer・CourseGimmickPlacer）すべてに使う。
[CustomEditor(typeof(CoursePlacerBase), true), CanEditMultipleObjects]
public sealed class CoursePlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        foreach (Object targetObject in targets)
        {
            CoursePlacerBase placer = (CoursePlacerBase)targetObject;
            if (!placer.CanBuild(out string reason))
            {
                EditorGUILayout.HelpBox($"{placer.name}: {reason}", MessageType.Warning);
            }

            foreach (string branchName in placer.GetInvalidBranchNames())
            {
                EditorGUILayout.HelpBox($"分岐 {branchName} には置けません", MessageType.Warning);
            }

            if (placer is CourseGimmickPlacer gimmickPlacer)
            {
                DrawGimmickWarnings(gimmickPlacer);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("生成", GUILayout.Height(28)))
            {
                foreach (Object targetObject in targets)
                {
                    CoursePlacementGenerator.Generate((CoursePlacerBase)targetObject);
                }
            }

            if (GUILayout.Button("クリア", GUILayout.Height(28)))
            {
                foreach (Object targetObject in targets)
                {
                    CoursePlacementGenerator.ClearWithUndo((CoursePlacerBase)targetObject);
                }
            }
        }

        if (GUILayout.Button("Seedを変えて生成"))
        {
            foreach (Object targetObject in targets)
            {
                SerializedObject serialized = new SerializedObject(targetObject);
                serialized.FindProperty("_seed").intValue = Random.Range(int.MinValue, int.MaxValue);
                serialized.ApplyModifiedProperties();
                CoursePlacementGenerator.Generate((CoursePlacerBase)targetObject);
            }
        }

        EditorGUILayout.HelpBox("生成し直すと、コンテナの中身は消えて作り直されます。手で直したいものはコンテナの外へ移してください。", MessageType.None);
    }

    private static void DrawGimmickWarnings(CourseGimmickPlacer placer)
    {
        foreach (CourseGimmickPlacer.GimmickEntry entry in placer.Entries)
        {
            if (entry == null || !entry.Enabled)
            {
                continue;
            }

            if (entry.Prefab == null)
            {
                EditorGUILayout.HelpBox($"{entry.Name}: Prefab が未設定です（置かれません）", MessageType.Warning);
            }
            else if (!entry.HasGimmickComponent)
            {
                EditorGUILayout.HelpBox($"{entry.Name}: {entry.Prefab.name} に CourseGimmickBase を継承したコンポーネントがありません", MessageType.Warning);
            }
        }
    }
}
