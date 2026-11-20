using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 開いているシーンに、ブースト（左端）とシールド（右端）のゲージ表示を作り、参照をつなぐ。
// どちらも画面端で半分ほど見切れる位置に置く。
// 構成：XxxHUD（CanvasGroup / ShowAfterRaceStart / ChargeGaugeView）
//        ├ Gauge（RingGaugeGraphic）
//        ├ Icon（ブースト or シールドの画像）
//        └ Ring（UI_ring、UIRotatorで回転）
public static class ChargeGaugeSceneSetup
{
    private const string TextureFolder = "Assets/Deverop/AT_Miyata/UI/Textures";
    private const string MaterialFolder = "Assets/Deverop/AT_Miyata/UI/Materials";
    private const string MaterialPath = MaterialFolder + "/UIRingGauge.mat";
    private const string ShaderName = "UI/AT_Miyata/RingGauge";

    // 大きさ・位置（CanvasScalerの基準解像度 1920x1080 での値）
    private const float HudSize = 420f;          // リング画像の大きさ
    private const float VerticalOffset = -180f;  // 画面の縦中央からのずれ（マイナスで下）
    private const float IconSizeRatio = 0.5f;    // リングに対するアイコンの大きさ
    private const float RingSpeed = 20f;         // リングの回転速度（度/秒）

    private static readonly Color BoostFillColor = new Color(0.2f, 0.85f, 1f, 1f);
    private static readonly Color ShieldFillColor = new Color(1f, 0.9f, 0.35f, 1f);

    [MenuItem("Tools/UI/Create Boost & Shield Gauges In Scene")]
    private static void CreateInScene()
    {
        if (Object.FindFirstObjectByType<ChargeGaugeView>(FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning("ChargeGaugeSceneSetup: このシーンには既に ChargeGaugeView があります");
            return;
        }

        RaceManager raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            Debug.LogError("ChargeGaugeSceneSetup: シーンに RaceManager がありません");
            return;
        }

        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogError("ChargeGaugeSceneSetup: シーンに Canvas がありません");
            return;
        }

        Sprite ringSprite = LoadSprite("UI_ring.PNG");
        Sprite boostSprite = LoadSprite("UI_boost.PNG");
        Sprite shieldSprite = LoadSprite("UI_shield.PNG");
        Material gaugeMaterial = LoadOrCreateMaterial();
        if (ringSprite == null || boostSprite == null || shieldSprite == null || gaugeMaterial == null)
        {
            return;
        }

        FindPlayerControllers(raceManager, out BoostController boost, out ShieldController shield);
        if (boost == null || shield == null)
        {
            Debug.LogWarning("ChargeGaugeSceneSetup: プレイヤーの BoostController / ShieldController が見つかりません。作成後にインスペクターで設定してください");
        }

        // ゲージ用の頂点チャンネルを Canvas で有効にする
        Undo.RecordObject(canvas, "Enable Canvas Shader Channels");
        canvas.additionalShaderChannels |=
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 |
            AdditionalCanvasShaderChannels.TexCoord3 |
            AdditionalCanvasShaderChannels.Tangent;

        GameObject boostHud = CreateHud(canvas.transform, "BoostHUD", isLeft: true,
            boostSprite, ringSprite, gaugeMaterial, BoostFillColor,
            ChargeGaugeView.ESource.Boost, raceManager, boost, shield);

        CreateHud(canvas.transform, "ShieldHUD", isLeft: false,
            shieldSprite, ringSprite, gaugeMaterial, ShieldFillColor,
            ChargeGaugeView.ESource.Shield, raceManager, boost, shield);

        Selection.activeGameObject = boostHud;
        EditorSceneManager.MarkSceneDirty(boostHud.scene);
        Debug.Log("ChargeGaugeSceneSetup: BoostHUD / ShieldHUD を作成しました（シーンを保存してください）");
    }

    private static GameObject CreateHud(Transform parent, string name, bool isLeft,
        Sprite iconSprite, Sprite ringSprite, Material gaugeMaterial, Color fillColor,
        ChargeGaugeView.ESource source, RaceManager raceManager, BoostController boost, ShieldController shield)
    {
        // ルート：画面の左端（右端）に中心を置き、半分見切れさせる
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        root.layer = parent.gameObject.layer;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.SetParent(parent, false);
        float anchorX = isLeft ? 0f : 1f;
        rootRect.anchorMin = new Vector2(anchorX, 0.5f);
        rootRect.anchorMax = new Vector2(anchorX, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        // 親Canvasのスケールがエディタ上で0のままのことがあるので明示的に設定する
        rootRect.localScale = Vector3.one;
        rootRect.localRotation = Quaternion.identity;
        rootRect.anchoredPosition3D = new Vector3(0f, VerticalOffset, 0f);
        rootRect.sizeDelta = new Vector2(HudSize, HudSize);

        // ゲージ（リングとアイコンの間）
        RingGaugeGraphic gauge = CreateChild(rootRect, "Gauge", Vector2.one * HudSize).AddComponent<RingGaugeGraphic>();
        gauge.material = gaugeMaterial;
        gauge.raycastTarget = false;
        SerializedObject gaugeSo = new SerializedObject(gauge);
        gaugeSo.FindProperty("_fillColor").colorValue = fillColor;
        // 見えている側（画面の内側）の半円を、下から上へ埋める
        gaugeSo.FindProperty("_arcAngle").floatValue = 180f;
        gaugeSo.FindProperty("_clockwise").boolValue = !isLeft;
        gaugeSo.FindProperty("_progress").floatValue = 0.6f; // エディタ上の見た目確認用
        gaugeSo.ApplyModifiedPropertiesWithoutUndo();

        // アイコン
        Image icon = CreateChild(rootRect, "Icon", Vector2.one * HudSize * IconSizeRatio).AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // リング（回転）
        GameObject ringGo = CreateChild(rootRect, "Ring", Vector2.one * HudSize);
        Image ring = ringGo.AddComponent<Image>();
        ring.sprite = ringSprite;
        ring.preserveAspect = true;
        ring.raycastTarget = false;
        UIRotator rotator = ringGo.AddComponent<UIRotator>();
        SerializedObject rotatorSo = new SerializedObject(rotator);
        // 左右で逆回転にする
        rotatorSo.FindProperty("_degreesPerSecond").floatValue = isLeft ? -RingSpeed : RingSpeed;
        rotatorSo.ApplyModifiedPropertiesWithoutUndo();

        // カウントダウンが終わるまで隠す
        ShowAfterRaceStart show = root.AddComponent<ShowAfterRaceStart>();
        SerializedObject showSo = new SerializedObject(show);
        showSo.FindProperty("_raceManager").objectReferenceValue = raceManager;
        showSo.ApplyModifiedPropertiesWithoutUndo();

        // チャージの受け渡し
        ChargeGaugeView view = root.AddComponent<ChargeGaugeView>();
        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("_source").enumValueIndex = (int)source;
        viewSo.FindProperty("_boost").objectReferenceValue = boost;
        viewSo.FindProperty("_shield").objectReferenceValue = shield;
        viewSo.FindProperty("_gauge").objectReferenceValue = gauge;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject CreateChild(RectTransform parent, string name, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = size;
        return go;
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

    private static void FindPlayerControllers(RaceManager raceManager, out BoostController boost, out ShieldController shield)
    {
        boost = null;
        shield = null;

        Object playerManager = new SerializedObject(raceManager).FindProperty("_playerManager")?.objectReferenceValue;
        if (playerManager == null)
        {
            return;
        }

        SerializedObject playerSo = new SerializedObject(playerManager);
        boost = playerSo.FindProperty("_boost")?.objectReferenceValue as BoostController;
        shield = playerSo.FindProperty("_shield")?.objectReferenceValue as ShieldController;

        Component playerComponent = playerManager as Component;
        if (playerComponent != null)
        {
            if (boost == null)
            {
                boost = playerComponent.GetComponentInChildren<BoostController>(true);
            }

            if (shield == null)
            {
                shield = playerComponent.GetComponentInChildren<ShieldController>(true);
            }
        }
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = $"{TextureFolder}/{fileName}";
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
        {
            Debug.LogError($"ChargeGaugeSceneSetup: スプライトが見つかりません: {path}");
        }

        return sprite;
    }

    private static Material LoadOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"ChargeGaugeSceneSetup: シェーダーが見つかりません: {ShaderName}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Deverop/AT_Miyata/UI", "Materials");
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
