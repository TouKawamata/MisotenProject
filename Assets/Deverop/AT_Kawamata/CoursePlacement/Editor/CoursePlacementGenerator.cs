using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 配置ツール（CoursePlacerBase）が計算した配置を、シーンに生成する・消す。Undoでまとめて戻せる。
public static class CoursePlacementGenerator
{
    [MenuItem("Tools/Course/シーン内の配置をすべて生成")]
    private static void GenerateAll()
    {
        int group = Undo.GetCurrentGroup();
        foreach (CoursePlacerBase placer in Object.FindObjectsByType<CoursePlacerBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Generate(placer);
        }

        Undo.CollapseUndoOperations(group);
    }

    public static void Generate(CoursePlacerBase placer)
    {
        if (placer == null || PrefabUtility.IsPartOfPrefabAsset(placer))
        {
            return;
        }

        if (!placer.CanBuild(out string reason))
        {
            Debug.LogWarning($"{placer.GetType().Name}: {placer.name} を生成できません。{reason}", placer);
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"{placer.name} を生成");

        Clear(placer);

        GameObject container = new GameObject(placer.ContainerName);
        container.transform.SetParent(placer.transform, false);
        Dictionary<string, Transform> groups = new Dictionary<string, Transform>();

        List<CoursePlacementItem> items = placer.BuildPlacements();
        foreach (CoursePlacementItem item in items)
        {
            if (item.Prefab == null)
            {
                continue;
            }

            Transform parent = GetGroup(container.transform, item.Group, groups);

            // プレハブとのリンクを保ったまま生成する。
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.Prefab, parent);
            instance.transform.SetPositionAndRotation(item.Position, item.Rotation);
            instance.transform.localScale = item.Prefab.transform.localScale * item.Scale;

            if (item.HasArrow)
            {
                ApplyArrow(instance, item.ArrowAngle);
            }
        }

        // 生成物を全部作ってから、まとめてUndoに登録する。
        Undo.RegisterCreatedObjectUndo(container, $"{placer.name} を生成");
        Undo.CollapseUndoOperations(group);

        Debug.Log($"{placer.GetType().Name}: {placer.name} に {items.Count} 個を生成しました", placer);
    }

    public static void ClearWithUndo(CoursePlacerBase placer)
    {
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"{placer.name} の生成物を削除");
        Clear(placer);
        Undo.CollapseUndoOperations(group);
    }

    private static void Clear(CoursePlacerBase placer)
    {
        Transform container;
        while ((container = placer.transform.Find(placer.ContainerName)) != null)
        {
            Undo.DestroyObjectImmediate(container.gameObject);
        }
    }

    private static Transform GetGroup(Transform container, string groupName, Dictionary<string, Transform> groups)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            return container;
        }

        if (!groups.TryGetValue(groupName, out Transform group))
        {
            group = new GameObject(groupName).transform;
            group.SetParent(container, false);
            groups.Add(groupName, group);
        }

        return group;
    }

    // SmartArrowSignageControllerがあればArrow Angleに入れる。無ければ前方向を軸に回し、プレハブの+Xを矢印の向きにする。
    private static void ApplyArrow(GameObject instance, float arrowAngle)
    {
        SmartArrowSignageController controller = instance.GetComponentInChildren<SmartArrowSignageController>(true);
        if (controller == null)
        {
            instance.transform.rotation = instance.transform.rotation * Quaternion.AngleAxis(arrowAngle, Vector3.forward);
            return;
        }

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty angle = serialized.FindProperty("_arrowAngle");
        if (angle == null)
        {
            Debug.LogWarning("SmartArrowSignageController に _arrowAngle が見つかりません。矢印の向きを設定できませんでした", controller);
            return;
        }

        angle.floatValue = Mathf.DeltaAngle(0f, arrowAngle);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        controller.UpdateSignboard();
    }
}
