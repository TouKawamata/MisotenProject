using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 開いているシーンにあるHUD（順位・ブースト・シールド・アンダーライン）を、
// 今の見た目（位置・大きさ・色などの調整）のままプレハブとして保存し、シーン上のものをそのプレハブのインスタンスにする。
// シーン上のオブジェクトへの参照（RaceManager・プレイヤーのBoostControllerなど）はプレハブには保存されないが、
// 各スクリプトが実行時に自動で探すので、別のシーンにプレハブを置くだけで動く。
public static class HudPrefabSaver
{
    private const string PrefabFolder = "Assets/Deverop/AT_Miyata/UI/Prefabs";

    private static readonly string[] HudNames =
    {
        "RankImageUI",
        "BoostHUD",
        "ShieldHUD",
        "UnderlineUI",
    };

    [MenuItem("Tools/UI/Save HUD Prefabs From Scene")]
    private static void SaveFromScene()
    {
        EnsureFolder();

        Scene scene = SceneManager.GetActiveScene();
        int savedCount = 0;

        foreach (string hudName in HudNames)
        {
            GameObject go = FindInScene(scene, hudName);
            if (go == null)
            {
                Debug.LogWarning($"HudPrefabSaver: シーンに {hudName} がありません（スキップ）");
                continue;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                Debug.LogWarning($"HudPrefabSaver: {hudName} は既にプレハブのインスタンスです（スキップ）");
                continue;
            }

            string path = $"{PrefabFolder}/{hudName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.LogWarning($"HudPrefabSaver: {path} は既にあります。上書きしないようスキップしました");
                continue;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
            if (prefab != null)
            {
                savedCount++;
                Debug.Log($"HudPrefabSaver: {path} を作成しました", prefab);
            }
        }

        if (savedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"HudPrefabSaver: {savedCount} 個のプレハブを作成しました（シーンを保存してください）");
        }
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }
        }

        return null;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Deverop/AT_Miyata/UI", "Prefabs");
        }
    }
}
