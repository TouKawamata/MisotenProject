using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 開いているシーンに、バックミラー一式（判定・カメラ・画面上部の表示）を作り、参照をつなぐ。
// シーンにあるRaceManagerを使う。既にBackMirrorTriggerがあるシーンでは作らない（二重に置かないように）。
public static class BackMirrorSceneCreator
{
    // 表示の大きさ（CanvasScalerの基準解像度 1920x1080 での値）。RenderTextureの大きさ（BackMirrorCameraの初期値 640x200）と縦横比をそろえる。
    private static readonly Vector2 MirrorSize = new Vector2(640f, 200f);
    private const float FrameThickness = 8f;
    private const float TopMargin = 16f;

    [MenuItem("Tools/BackMirror/Create In Scene")]
    private static void CreateInScene()
    {
        if (Object.FindFirstObjectByType<BackMirrorTrigger>() != null)
        {
            Debug.LogWarning("BackMirrorSceneCreator: このシーンには既にバックミラーがあります");
            return;
        }

        RaceManager raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            Debug.LogError("BackMirrorSceneCreator: シーンに RaceManager がありません");
            return;
        }

        GameObject root = new GameObject("BackMirror");
        Undo.RegisterCreatedObjectUndo(root, "Create BackMirror");

        BackMirrorTrigger trigger = root.AddComponent<BackMirrorTrigger>();
        SetReference(trigger, "_raceManager", raceManager);

        BackMirrorCamera mirrorCamera = CreateCamera(root.transform, raceManager);
        CreateView(root.transform, trigger, mirrorCamera);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("BackMirrorSceneCreator: バックミラーを作りました", root);
    }

    private static BackMirrorCamera CreateCamera(Transform parent, RaceManager raceManager)
    {
        GameObject cameraObject = new GameObject("BackMirrorCamera");
        cameraObject.transform.SetParent(parent, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 150f;

        // 影・ポストエフェクトなし（負荷を抑える）。
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = false;
        cameraData.renderPostProcessing = false;
        cameraData.antialiasing = AntialiasingMode.None;

        BackMirrorCamera mirrorCamera = cameraObject.AddComponent<BackMirrorCamera>();
        SetReference(mirrorCamera, "_raceManager", raceManager);
        return mirrorCamera;
    }

    private static void CreateView(Transform parent, BackMirrorTrigger trigger, BackMirrorCamera mirrorCamera)
    {
        GameObject canvasObject = new GameObject("BackMirrorCanvas");
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 枠（画面上部の中央）。CanvasGroupで枠ごとフェードする。
        GameObject frameObject = new GameObject("Frame", typeof(RectTransform));
        frameObject.transform.SetParent(canvasObject.transform, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = new Vector2(0f, -TopMargin);
        frameRect.sizeDelta = MirrorSize + Vector2.one * FrameThickness * 2f;

        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        frameImage.raycastTarget = false;

        CanvasGroup canvasGroup = frameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // ミラーの映像（枠の内側いっぱい）。
        GameObject imageObject = new GameObject("MirrorImage", typeof(RectTransform));
        imageObject.transform.SetParent(frameObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.one * FrameThickness;
        imageRect.offsetMax = -Vector2.one * FrameThickness;

        RawImage rawImage = imageObject.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        BackMirrorView view = canvasObject.AddComponent<BackMirrorView>();
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("_trigger").objectReferenceValue = trigger;
        serialized.FindProperty("_mirrorCamera").objectReferenceValue = mirrorCamera;
        serialized.FindProperty("_rawImage").objectReferenceValue = rawImage;
        serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
