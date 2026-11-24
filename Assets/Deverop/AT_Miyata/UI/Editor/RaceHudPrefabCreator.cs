using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUDの各プレハブ（RankImageUI / BoostHUD / ShieldHUD / UnderlineUI）を、専用のCanvasごと1つにまとめた
// RaceHUD プレハブを作る。各HUDはネストしたプレハブとして入るので、個別のプレハブを直せばRaceHUDにも反映される。
// 開いているシーンでは、RaceHUD のインスタンスを置き、元の4つのHUDは削除する（Undo可）。
// 先に Tools/UI/Save HUD Prefabs From Scene で4つのプレハブを作っておくこと。
public static class RaceHudPrefabCreator
{
    private const string PrefabFolder = "Assets/Deverop/AT_Miyata/UI/Prefabs";
    private const string RaceHudPath = PrefabFolder + "/RaceHUD.prefab";

    // 描画順（先にあるものほど奥）
    private static readonly string[] HudNames =
    {
        "UnderlineUI",
        "RankImageUI",
        "BoostHUD",
        "ShieldHUD",
    };

    [MenuItem("Tools/UI/Create RaceHUD Prefab")]
    private static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RaceHudPath) != null)
        {
            Debug.LogWarning($"RaceHudPrefabCreator: {RaceHudPath} は既にあります。上書きしないよう中止しました");
            return;
        }

        // 4つのプレハブがそろっているか確認
        GameObject[] hudPrefabs = new GameObject[HudNames.Length];
        for (int i = 0; i < HudNames.Length; i++)
        {
            string path = $"{PrefabFolder}/{HudNames[i]}.prefab";
            hudPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (hudPrefabs[i] == null)
            {
                Debug.LogError($"RaceHudPrefabCreator: {path} がありません。先に Tools/UI/Save HUD Prefabs From Scene を実行してください");
                return;
            }
        }

        Scene scene = SceneManager.GetActiveScene();

        // 今HUDが置かれているCanvasの設定を引き継ぐ
        GameObject[] sceneHuds = new GameObject[HudNames.Length];
        for (int i = 0; i < HudNames.Length; i++)
        {
            sceneHuds[i] = FindInScene(scene, HudNames[i]);
        }

        Canvas sourceCanvas = FindSourceCanvas(sceneHuds);

        // RaceHUD（専用Canvas）
        GameObject root = new GameObject("RaceHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(root, "Create RaceHUD");
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.additionalShaderChannels |=
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 |
            AdditionalCanvasShaderChannels.TexCoord3 |
            AdditionalCanvasShaderChannels.Tangent;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        if (sourceCanvas != null)
        {
            canvas.sortingOrder = sourceCanvas.sortingOrder;
            canvas.pixelPerfect = sourceCanvas.pixelPerfect;

            CanvasScaler sourceScaler = sourceCanvas.GetComponent<CanvasScaler>();
            if (sourceScaler != null)
            {
                EditorUtility.CopySerialized(sourceScaler, scaler);
            }
        }

        // 各HUDをネストしたプレハブとして入れる
        for (int i = 0; i < hudPrefabs.Length; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefabs[i], root.transform);
            instance.name = HudNames[i];
            instance.transform.SetAsLastSibling();

            // シーン上で調整した値（プレハブに反映されていない上書き）があれば引き継ぐ
            if (sceneHuds[i] != null && PrefabUtility.IsPartOfPrefabInstance(sceneHuds[i]))
            {
                CopyRectTransform(sceneHuds[i].transform as RectTransform, instance.transform as RectTransform);
            }
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, RaceHudPath, InteractionMode.UserAction);
        if (saved == null)
        {
            Debug.LogError("RaceHudPrefabCreator: RaceHUD プレハブの保存に失敗しました");
            return;
        }

        // シーンの元のHUDは RaceHUD に置き換えるので削除する
        foreach (GameObject hud in sceneHuds)
        {
            if (hud != null)
            {
                Undo.DestroyObjectImmediate(hud);
            }
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"RaceHudPrefabCreator: {RaceHudPath} を作成し、シーンのHUDを置き換えました（シーンを保存してください）", saved);
    }

    private static Canvas FindSourceCanvas(GameObject[] sceneHuds)
    {
        foreach (GameObject hud in sceneHuds)
        {
            if (hud == null)
            {
                continue;
            }

            Canvas c = hud.GetComponentInParent<Canvas>(true);
            if (c != null)
            {
                return c.rootCanvas;
            }
        }

        return null;
    }

    private static void CopyRectTransform(RectTransform from, RectTransform to)
    {
        if (from == null || to == null)
        {
            return;
        }

        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition3D = from.anchoredPosition3D;
        to.sizeDelta = from.sizeDelta;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t.GetComponentInParent<Canvas>(true) != null)
                {
                    return t.gameObject;
                }
            }
        }

        return null;
    }
}
