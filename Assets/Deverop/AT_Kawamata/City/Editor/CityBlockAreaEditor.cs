using UnityEditor;
using UnityEngine;

/// <summary>
/// CityBlockAreaのInspectorに「生成」「クリア」ボタンを追加する。
/// このファイルはEditorフォルダに置くこと。
/// </summary>
[CustomEditor(typeof(CityBlockArea)), CanEditMultipleObjects]
public sealed class CityBlockAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        foreach (var t in targets)
        {
            var area = (CityBlockArea)t;
            if (area.transform.lossyScale != Vector3.one)
            {
                EditorGUILayout.HelpBox(
                    $"{area.name}: Transformのスケールが1ではありません。建物にもスケールがかかるので、区画の大きさはBoxColliderのSizeで指定してください。",
                    MessageType.Warning);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("生成", GUILayout.Height(28)))
            {
                foreach (var t in targets) Generate((CityBlockArea)t);
            }
            if (GUILayout.Button("クリア", GUILayout.Height(28)))
            {
                foreach (var t in targets) ClearWithUndo((CityBlockArea)t);
            }
        }

        if (GUILayout.Button("Seedを変えて生成"))
        {
            foreach (var t in targets)
            {
                var so = new SerializedObject(t);
                so.FindProperty("_seed").intValue = Random.Range(int.MinValue, int.MaxValue);
                so.ApplyModifiedProperties();
                Generate((CityBlockArea)t);
            }
        }
    }

    [MenuItem("Tools/City/シーン内の区画をすべて生成")]
    private static void GenerateAll()
    {
        foreach (var area in Object.FindObjectsByType<CityBlockArea>(FindObjectsSortMode.None))
        {
            Generate(area);
        }
    }

    public static void Generate(CityBlockArea area)
    {
        if (PrefabUtility.IsPartOfPrefabAsset(area)) return;

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("区画に建物を生成");

        Clear(area);

        // 中身を全部作ってからまとめてUndo登録する
        var container = new GameObject(area.ContainerName);
        container.transform.SetParent(area.transform, false);

        foreach (var p in area.BuildPlacements())
        {
            // Prefabとのリンクを保ったまま生成する
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p.Prefab, container.transform);
            go.transform.localPosition = p.LocalPosition;
            go.transform.localRotation = Quaternion.Euler(0f, p.LocalYaw, 0f);
        }

        Undo.RegisterCreatedObjectUndo(container, "区画に建物を生成");
        Undo.CollapseUndoOperations(group);
    }

    private static void ClearWithUndo(CityBlockArea area)
    {
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("区画の建物を削除");
        Clear(area);
        Undo.CollapseUndoOperations(group);
    }

    private static void Clear(CityBlockArea area)
    {
        Transform t;
        while ((t = area.transform.Find(area.ContainerName)) != null)
        {
            Undo.DestroyObjectImmediate(t.gameObject);
        }
    }
}