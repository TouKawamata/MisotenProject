using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ResultPreviewValidation
{
    static ResultPreviewValidation() { EditorApplication.delayCall += RunRequested; }
    private static void RunRequested()
    {
        if (File.Exists("Temp/RequestResultPreview")) Run();
    }
    [MenuItem("水口/リザルト/プレビューを検証")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (File.Exists("Temp/RequestResultPreview")) File.Delete("Temp/RequestResultPreview");
        Scene preview = default;
        Texture2D image = null;
        RenderTexture target = null;
        PlayerResultData previous = RaceResultStore.Latest;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene("Assets/MainScenes/ResultScene.unity");
            GameObject[] roots = preview.GetRootGameObjects();
            T Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).First();
            var canvas = Find<Canvas>();
            var camera = Find<Camera>();
            var view = Find<ResultView>();
            var presentation = Find<ResultPresentation>();
            Find<RawImage>().gameObject.SetActive(false);
            foreach (var root in roots)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) != 0) throw new Exception("Missing script");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var intro = (RectTransform)typeof(ResultPresentation).GetField("_introCover", flags).GetValue(presentation);
            intro.gameObject.SetActive(false);
            // ResultScene直接起動用の値がすべて入るかを先に検証。
            RaceResultStore.Clear();
            typeof(ResultView).GetMethod("OnEnable", flags).Invoke(view, null);
            var time = (TMP_Text)typeof(ResultView).GetField("_time", flags).GetValue(view);
            if (time.text != "--:--.--") throw new Exception("Empty result fallback");
            RaceResultStore.Save(new PlayerResultData(1, 92.589f, 1250f, 12, 3));
            typeof(ResultView).GetMethod("OnEnable", flags).Invoke(view, null);
            if (time.text != "01:32.58" || ResultView.FormatTime(59.996f) != "00:59.99") throw new Exception("Time format");
            // 通常のシーン・未保存の編集内容には触れず、PreviewSceneだけを描画。
            target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.scene = preview;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = 1;
            foreach (var text in roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true))) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            Directory.CreateDirectory("Temp/ResultValidation");
            File.WriteAllBytes("Temp/ResultValidation/result-design.png", image.EncodeToPNG());
            File.WriteAllText("Temp/ResultValidation/design-check.txt", "PASS: ResultScene loaded, five references resolved, missing result fallback, time truncation, preview rendered.");
            Debug.Log("RESULT_DESIGN_VALIDATION_PASSED");
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Temp/ResultValidation");
            File.WriteAllText("Temp/ResultValidation/design-check.txt", e.ToString());
            Debug.LogException(e);
        }
        finally
        {
            RaceResultStore.Save(previous);
            RenderTexture.active = null;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
