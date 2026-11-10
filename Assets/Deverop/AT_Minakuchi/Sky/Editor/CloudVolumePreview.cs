using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Minakuchi.Sky.Editor
{
    public static class CloudVolumePreview
    {
        [MenuItem("水口/空/立体雲の光と影をプレビュー")]
        public static void Render()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("再生を停止してから実行してください。");
            var scene = EditorSceneManager.NewPreviewScene();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deverop/AT_Minakuchi/Prefab/SkyEnvironment.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var controller = root.GetComponent<SkyTransitionController>();
            controller.useMeshClouds = false;
            controller.showDemoControls = false;
            var apply = typeof(SkyTransitionController).GetMethod("Apply", flags);
            typeof(SkyTransitionController).GetMethod("OnEnable", flags).Invoke(controller, null);
            var cameraObject = new GameObject("雲の検証カメラ");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 70;
            camera.aspect = 16f / 9;
            camera.farClipPlane = 30000;
            camera.scene = scene;
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGBHalf);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var oldTarget = RenderTexture.active;
            string output = Environment.GetEnvironmentVariable("CODEX_CLOUD_PREVIEW_DIR")
                ?? Path.Combine("Temp", "CloudPreviews");
            Directory.CreateDirectory(output);
            Material markerMaterial = null;
            try
            {
                target.Create();
                void Capture(string name, float height, float pitch, float hour, float forward = 0)
                {
                    controller.timeOfDay = hour;
                    apply.Invoke(controller, null);
                    camera.transform.SetPositionAndRotation(new Vector3(0, height, forward), Quaternion.Euler(pitch, 0, 0));
                    // Warmup also compiles the shader variant before capturing the final frame.
                    for (int i = 0; i < 2; i++)
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                }
                Capture("above-morning", 0, 16, 10);
                Capture("above-afternoon", 0, 16, 16);
                Capture("above-shallow", 0, 3, 16);
                Capture("above-evening", 0, 16, 17.5f);
                Capture("inside", -400, 0, 10);
                Capture("below", -900, -20, 10);
                // A continuous ascent and forward flight, using the same density field for every frame.
                // Static above/below screenshots alone do not validate the feeling of crossing a cloud.
                float insideSky = 1, outsideSky = 0;
                float BlueSkyFraction()
                {
                    var pixels = image.GetPixels32();
                    int sky = 0;
                    foreach (var pixel in pixels)
                        if (pixel.b > pixel.r * 1.25f && pixel.b > pixel.g * 1.08f && pixel.b > 90) sky++;
                    return sky / (float)pixels.Length;
                }
                for (int frame = 0; frame < 12; frame++)
                {
                    Capture("passage-" + frame.ToString("D2"), -680 + frame * 65, -6, 10, frame * 85);
                    if (frame == 4) insideSky = BlueSkyFraction();
                    if (frame == 11) outsideSky = BlueSkyFraction();
                }
                if (outsideSky < insideSky + 0.2f)
                    throw new InvalidOperationException($"雲を抜けた視界の変化を確認できません: {insideSky} -> {outsideSky}");
                Debug.Log($"CLOUD_PASSAGE_OK: blue sky {insideSky:P1} -> {outsideSky:P1}");
                for (int frame = 0; frame < 12; frame++)
                    Capture("descent-" + frame.ToString("D2"), 35 - frame * 65, 6, 10, frame * 85);
                // Match the game's Trigger behavior: the observer stays at Y=0 while the layer moves.
                controller.cloudCoverage = 0.34f;
                for (int frame = 0; frame < 12; frame++)
                {
                    controller.cloudHeight = 300 - frame * 70;
                    Capture("trigger-passage-" + frame.ToString("D2"), 0, 0, 10);
                }
                controller.cloudHeight = -400;
                controller.cloudPassageStrength = 0;
                Capture("local-passage-off", -400, 0, 10);
                var withoutPassage = image.GetPixels32();
                controller.cloudPassageStrength = 1;
                Capture("local-passage-on", -400, 0, 10);
                var withPassage = image.GetPixels32();
                double passageChange = 0;
                for (int pixel = 0; pixel < withoutPassage.Length; pixel++)
                    passageChange += Math.Abs(withPassage[pixel].r - withoutPassage[pixel].r)
                        + Math.Abs(withPassage[pixel].g - withoutPassage[pixel].g)
                        + Math.Abs(withPassage[pixel].b - withoutPassage[pixel].b);
                passageChange /= withoutPassage.Length * 3.0 * 255;
                if (passageChange < 0.02) throw new InvalidOperationException("突入演出の画面変化が不足: " + passageChange);
                Debug.Log($"CLOUD_LOCAL_PASSAGE_OK: mean RGB change={passageChange:F3}");
                controller.cloudCoverage = 1;
                markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                markerMaterial.SetColor("_BaseColor", new Color(1, 0.05f, 0.03f));
                Vector3[] positions = { new Vector3(-20, -400, 70), new Vector3(120, -400, 420) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, scene);
                    marker.transform.position = positions[i];
                    marker.transform.localScale = Vector3.one * (i == 0 ? 24 : 100);
                    marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                }
                Color PixelAt(Vector3 position)
                {
                    Vector3 uv = camera.WorldToViewportPoint(position);
                    return image.GetPixel(Mathf.RoundToInt(uv.x * 959), Mathf.RoundToInt(uv.y * 539));
                }
                controller.cloudCoverage = 0;
                Capture("inside-objects-clear", -400, 0, 10);
                Color nearClear = PixelAt(positions[0]), farClear = PixelAt(positions[1]);
                controller.cloudCoverage = 1;
                Capture("inside-objects-fog", -400, 0, 10);
                float nearChange = Vector3.Distance((Vector4)nearClear, (Vector4)PixelAt(positions[0]));
                float farChange = Vector3.Distance((Vector4)farClear, (Vector4)PixelAt(positions[1]));
                if (nearChange < 0.01f || farChange <= nearChange)
                    throw new InvalidOperationException($"雲の奥行き合成を確認できません: near={nearChange}, far={farChange}");
                Debug.Log($"CLOUD_DEPTH_COMPOSITE_OK: near={nearChange:F3}, far={farChange:F3}");
                foreach (string name in new[] { "Minakuchi/CloudVolume", "Minakuchi/TransitionSky", "Minakuchi/MeshCloud" })
                {
                    var shader = Shader.Find(name);
                    if (!shader || ShaderUtil.ShaderHasError(shader))
                    {
                        if (shader) foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(message.message);
                        throw new InvalidOperationException("雲のシェーダーにエラー: " + name);
                    }
                }
                Debug.Log("CLOUD_VOLUME_PREVIEW_OK: " + Path.GetFullPath(output));
            }
            finally
            {
                RenderTexture.active = oldTarget;
                typeof(SkyTransitionController).GetMethod("OnDisable", flags).Invoke(controller, null);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                if (markerMaterial) UnityEngine.Object.DestroyImmediate(markerMaterial);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
