using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 開いているシーンに、画像版の順位表示（RankImageView）を作り、参照をつなぐ。
// 既存のテキスト版 RankView があればその親Canvasの下に作り、RankView のオブジェクトは非アクティブにする（削除はしない）。
public static class RankImageViewSceneSetup
{
    private const string TextureFolder = "Assets/Deverop/AT_Miyata/UI/Textures";
    private const int NumberCount = 8;

    // 位置・大きさ（CanvasScalerの基準解像度 1920x1080 での値）
    private static readonly Vector2 RootPosition = new Vector2(0f, -30f); // 画面上端中央からのオフセット
    private static readonly Vector2 RootSize = new Vector2(400f, 140f);
    private const float RankScale = 1.2f;   // 順位の数字の大きさ（スプライト元サイズ比）
    private const float TotalScale = 0.6f;  // 人数の数字の大きさ（スプライト元サイズ比）
    private static readonly Vector2 SeparatorSize = new Vector2(40f, 70f);
    private const float SeparatorFontSize = 64f;
    private const float Spacing = 4f;

    [MenuItem("Tools/UI/Create Rank Image View In Scene")]
    private static void CreateInScene()
    {
        if (Object.FindFirstObjectByType<RankImageView>(FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning("RankImageViewSceneSetup: このシーンには既に RankImageView があります");
            return;
        }

        RaceManager raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            Debug.LogError("RankImageViewSceneSetup: シーンに RaceManager がありません");
            return;
        }

        Sprite[] numberSprites = LoadNumberSprites();
        if (numberSprites == null)
        {
            return;
        }

        RankView oldView = Object.FindFirstObjectByType<RankView>(FindObjectsInactive.Include);
        Transform parent = FindParentCanvas(oldView);
        if (parent == null)
        {
            Debug.LogError("RankImageViewSceneSetup: シーンに Canvas がありません");
            return;
        }

        // ルート（上部中央に固定、子を下揃え・中央寄せで横に並べる）
        GameObject root = new GameObject("RankImageUI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create RankImageUI");
        root.layer = parent.gameObject.layer;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        // 親Canvasのスケールがエディタ上で0のままのことがあり、その影響を受けないよう明示的に設定する
        rootRect.localScale = Vector3.one;
        rootRect.localRotation = Quaternion.identity;
        rootRect.anchoredPosition3D = new Vector3(RootPosition.x, RootPosition.y, 0f);
        rootRect.sizeDelta = RootSize;

        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.spacing = Spacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = true;
        layout.childScaleHeight = true;

        Image rankImage = CreateImage(rootRect, "RankImage", numberSprites[0], RankScale);
        GameObject separator = CreateSeparator(rootRect);
        Image totalImage = CreateImage(rootRect, "TotalImage", numberSprites[NumberCount - 1], TotalScale);

        RankImageView view = root.AddComponent<RankImageView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_raceManager").objectReferenceValue = raceManager;
        so.FindProperty("_rankImage").objectReferenceValue = rankImage;
        so.FindProperty("_totalImage").objectReferenceValue = totalImage;
        so.FindProperty("_separator").objectReferenceValue = separator;
        so.FindProperty("_useNativeSize").boolValue = true;
        SerializedProperty spritesProp = so.FindProperty("_numberSprites");
        spritesProp.arraySize = NumberCount;
        for (int i = 0; i < NumberCount; i++)
        {
            spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = numberSprites[i];
        }
        so.ApplyModifiedProperties();

        // 既存のテキスト版は非アクティブにする（削除はしない）
        if (oldView != null && oldView.gameObject.activeSelf)
        {
            Undo.RecordObject(oldView.gameObject, "Disable RankView");
            oldView.gameObject.SetActive(false);
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("RankImageViewSceneSetup: RankImageUI を作成しました（シーンを保存してください）");
    }

    private static Sprite[] LoadNumberSprites()
    {
        Sprite[] sprites = new Sprite[NumberCount];
        for (int i = 0; i < NumberCount; i++)
        {
            string path = $"{TextureFolder}/UI_number{i + 1}.png";
            sprites[i] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprites[i] == null)
            {
                Debug.LogError($"RankImageViewSceneSetup: スプライトが見つかりません: {path}");
                return null;
            }
        }
        return sprites;
    }

    private static Transform FindParentCanvas(RankView oldView)
    {
        if (oldView != null && oldView.transform.parent != null)
        {
            return oldView.transform.parent;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private static Image CreateImage(RectTransform parent, string name, Sprite sprite, float scale)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one * scale;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite; // エディタ上の見た目確認用。実行時は RankImageView が差し替える
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.SetNativeSize();
        return image;
    }

    private static GameObject CreateSeparator(RectTransform parent)
    {
        GameObject go = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = SeparatorSize;

        // 「/」の画像ができたら、このオブジェクトを Image に差し替えればよい
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = "/";
        text.fontSize = SeparatorFontSize;
        text.fontStyle = FontStyles.Italic;
        text.alignment = TextAlignmentOptions.Bottom;
        text.raycastTarget = false;
        return go;
    }
}
