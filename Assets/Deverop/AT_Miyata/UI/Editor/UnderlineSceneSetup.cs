using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 開いているシーンに、画面下端のアンダーライン（UI_underline）を作る。
// スタート（カウントダウン終了）後に表示されるよう ShowAfterRaceStart を付ける。
public static class UnderlineSceneSetup
{
    private const string SpritePath = "Assets/Deverop/AT_Miyata/UI/Textures/UI_underline.PNG";
    private const string ObjectName = "UnderlineUI";

    [MenuItem("Tools/UI/Create Underline In Scene")]
    private static void CreateInScene()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogError("UnderlineSceneSetup: シーンに Canvas がありません");
            return;
        }

        if (canvas.transform.Find(ObjectName) != null)
        {
            Debug.LogWarning($"UnderlineSceneSetup: このシーンには既に {ObjectName} があります");
            return;
        }

        RaceManager raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            Debug.LogError("UnderlineSceneSetup: シーンに RaceManager がありません");
            return;
        }

        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
        {
            Debug.LogError($"UnderlineSceneSetup: スプライトが見つかりません: {SpritePath}");
            return;
        }

        GameObject go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create " + ObjectName);
        go.layer = canvas.gameObject.layer;

        // 画面下端の中央に、元画像（1920x1080）と同じ位置・大きさで置く
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(canvas.transform, false);
        rect.SetAsFirstSibling(); // 他のUIより奥に描く
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        // 親Canvasのスケールがエディタ上で0のままのことがあるので明示的に設定する
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.SetNativeSize();

        ShowAfterRaceStart show = go.AddComponent<ShowAfterRaceStart>();
        SerializedObject showSo = new SerializedObject(show);
        showSo.FindProperty("_raceManager").objectReferenceValue = raceManager;
        showSo.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"UnderlineSceneSetup: {ObjectName} を作成しました（シーンを保存してください）");
    }

    private static Canvas FindCanvas()
    {
        // 順位表示と同じ Canvas に置く
        RankImageView rankView = Object.FindFirstObjectByType<RankImageView>(FindObjectsInactive.Include);
        if (rankView != null)
        {
            Canvas rankCanvas = rankView.GetComponentInParent<Canvas>(true);
            if (rankCanvas != null)
            {
                return rankCanvas.rootCanvas;
            }
        }

        return Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
            ?? Object.FindFirstObjectByType<Canvas>();
    }
}
