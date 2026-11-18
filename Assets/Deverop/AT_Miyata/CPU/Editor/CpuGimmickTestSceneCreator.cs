using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// CPUの動作確認用に、背景・UIなどを省いたシーンを作る（コース・Player・エフェクト・CPU・ギミックだけ）。
// 元のシーン（CPUTest_2）をコピーしてから、コピーの方で不要なルートのオブジェクトを消し、コースに沿ってギミックを置く。
// ・消すのは下のRemoveRootNamesに挙げたものだけ。残すオブジェクトから参照されているものは消さずに残す（Consoleに出す）
// ・元のシーンは変えない。作るシーンが既にあれば何もしない（上書きしない）
public static class CpuGimmickTestSceneCreator
{
    private const string SourceScenePath = "Assets/Deverop/AT_Miyata/Scenes/CPUTest_2.unity";
    private const string NewScenePath = "Assets/Deverop/AT_Miyata/Scenes/CPUGimmickTest.unity";

    // 消すルートのオブジェクト（背景・建物・UI・音・演出）。
    private static readonly string[] RemoveRootNames =
    {
        "ocean", "Plane", "stadium", "tower", "building", "signboards", "Decal_Stadium", "Mid_Ring",
        "Minimap", "PauseMenu", "PlayerPoseStatusUIController", "DebugHud", "AudioManager",
        "RaceIntroDirection", "RaceGoalDirection",
    };

    // 参照されていても消してよいもの（参照する側が未設定を想定している）。
    // RaceIntroDirection：RaceManagerのIntroは未設定ならすぐカウントダウンを始める。
    private static readonly string[] RemoveEvenIfReferenced = { "RaceIntroDirection" };

    // コースに沿って置くギミック（この順にくり返す）。
    private static readonly string[] GimmickPrefabPaths =
    {
        "Assets/Deverop/AT_Minakuchi/Prefab/AccelerationRing.prefab",
        "Assets/Deverop/AT_Takahashi/Gimmick/Prefab/BombRoot.prefab",
        "Assets/Deverop/AT_Minakuchi/Prefab/AccelerationRing.prefab",
        "Assets/Deverop/AT_Takahashi/Gimmick/Prefab/SlowZone.prefab",
        "Assets/Deverop/AT_Minakuchi/Prefab/DecelerationRing.prefab",
    };

    // スタートからこの距離（m）より先に置く。
    private const float FirstGimmickDistance = 150f;

    // ギミックの間隔（m）。
    private const float GimmickSpacing = 100f;

    // ゴールの手前この距離（m）には置かない。
    private const float EndMargin = 80f;

    // 中心からずらす量（トンネル半径に対する割合）。0なら中央。
    private const float RingOffsetRate = 0.35f;
    private const float BombOffsetRate = 0.45f;
    private const float SlowZoneOffsetRate = 0.4f;

    private const string GimmickRootName = "------- Gimmicks（自動配置） -------";

    [MenuItem("Tools/CPU/Create Gimmick Test Scene")]
    private static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            Debug.LogError($"CpuGimmickTestSceneCreator: 元のシーン {SourceScenePath} が見つかりません");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NewScenePath) != null)
        {
            Debug.LogWarning($"CpuGimmickTestSceneCreator: {NewScenePath} は既にあります。作り直すときは消してから実行してください");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, NewScenePath))
        {
            Debug.LogError($"CpuGimmickTestSceneCreator: {NewScenePath} にコピーできませんでした");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(NewScenePath, OpenSceneMode.Single);
        RemoveRoots(scene);
        PlaceGimmicks(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"CpuGimmickTestSceneCreator: {NewScenePath} を作りました");
    }

    private static void RemoveRoots(Scene scene)
    {
        // 見つからなかった名前（元のシーンで名前が変わったなど）を知らせる。
        HashSet<string> rootNames = new HashSet<string>(scene.GetRootGameObjects().Select(root => root.name));
        foreach (string name in RemoveRootNames)
        {
            if (!rootNames.Contains(name))
            {
                Debug.Log($"CpuGimmickTestSceneCreator: {name} はシーンにありませんでした");
            }
        }

        List<GameObject> candidates = scene.GetRootGameObjects().Where(root => RemoveRootNames.Contains(root.name)).ToList();
        List<GameObject> kept = scene.GetRootGameObjects().Where(root => !candidates.Contains(root)).ToList();

        // 残すオブジェクトから参照されている候補は残す。残した候補からの参照も見るため、変わらなくなるまでくり返す。
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (GameObject candidate in candidates.ToList())
            {
                if (RemoveEvenIfReferenced.Contains(candidate.name))
                {
                    continue;
                }

                string referrer = FindReferrer(kept, candidate);
                if (referrer != null)
                {
                    Debug.LogWarning($"CpuGimmickTestSceneCreator: {candidate.name} は {referrer} から参照されているため残しました");
                    candidates.Remove(candidate);
                    kept.Add(candidate);
                    changed = true;
                }
            }
        }

        foreach (GameObject candidate in candidates)
        {
            Debug.Log($"CpuGimmickTestSceneCreator: {candidate.name} を消しました");
            Object.DestroyImmediate(candidate);
        }
    }

    // keptの中に、target（とその子）のオブジェクト・コンポーネントを参照しているものがあれば、その名前を返す。
    private static string FindReferrer(List<GameObject> kept, GameObject target)
    {
        HashSet<Object> targetObjects = new HashSet<Object>();
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            targetObjects.Add(child.gameObject);
            foreach (Component component in child.GetComponents<Component>())
            {
                if (component != null)
                {
                    targetObjects.Add(component);
                }
            }
        }

        foreach (GameObject root in kept)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue != null
                        && targetObjects.Contains(property.objectReferenceValue))
                    {
                        return $"{component.gameObject.name}（{component.GetType().Name}.{property.propertyPath}）";
                    }
                }
            }
        }

        return null;
    }

    private static void PlaceGimmicks(Scene scene)
    {
        CourseSpline course = Object.FindFirstObjectByType<CourseSpline>();
        if (course == null || course.TotalLength <= 0f)
        {
            Debug.LogWarning("CpuGimmickTestSceneCreator: CourseSpline が見つからないため、ギミックは置きませんでした");
            return;
        }

        GameObject[] prefabs = GimmickPrefabPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
        if (prefabs.Any(prefab => prefab == null))
        {
            Debug.LogWarning("CpuGimmickTestSceneCreator: ギミックのPrefabが見つからないため、ギミックは置きませんでした");
            return;
        }

        GameObject parent = new GameObject(GimmickRootName);
        SceneManager.MoveGameObjectToScene(parent, scene);

        // 毎回同じ配置になるように、乱数の種を固定する。
        System.Random random = new System.Random(12345);
        int count = 0;
        for (float distance = FirstGimmickDistance; distance <= course.TotalLength - EndMargin; distance += GimmickSpacing)
        {
            GameObject prefab = prefabs[count % prefabs.Length];
            Vector3 center = course.EvaluatePositionByDistance(distance);
            Vector3 forward = course.EvaluateForwardByDistance(distance);
            Vector3 right = course.EvaluateRightByDistance(distance);
            Vector3 up = course.EvaluateUpByDistance(distance);
            float radius = course.GetTunnelRadiusByDistance(distance);

            // 断面の中で、でたらめな方向へずらす。
            float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
            Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * GetOffsetRate(prefab);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(forward, up));
            instance.name = $"{prefab.name}_{Mathf.RoundToInt(distance)}m";
            count++;
        }

        Debug.Log($"CpuGimmickTestSceneCreator: ギミックを{count}個置きました（{GimmickSpacing}mおき、{parent.name} の下）");
    }

    private static float GetOffsetRate(GameObject prefab)
    {
        if (prefab.GetComponentInChildren<Bomb>(true) != null)
        {
            return BombOffsetRate;
        }

        if (prefab.GetComponentInChildren<SlowZone>(true) != null)
        {
            return SlowZoneOffsetRate;
        }

        return RingOffsetRate;
    }
}
