using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Minakuchi.Sky.Editor
{
    public static class SkyTwilightPreview
    {
        private const string Root = "Assets/Deverop/AT_Minakuchi/Sky";
        [MenuItem("水口/空/夕暮れの段階をプレビュー")]
        public static void Render()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してからプレビューしてください。");
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var originalSky = RenderSettings.skybox;
            var originalSun = RenderSettings.sun;
            var mode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientSkyColor;
            var equator = RenderSettings.ambientEquatorColor;
            var ground = RenderSettings.ambientGroundColor;
            float reflection = RenderSettings.reflectionIntensity;
            var active = RenderTexture.active;
            SkyTransitionController source = UnityEngine.Object.FindFirstObjectByType<SkyTransitionController>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deverop/AT_Minakuchi/Prefab/SkyEnvironment.prefab");
            var clone = UnityEngine.Object.Instantiate(prefab);
            clone.hideFlags = HideFlags.HideAndDontSave;
            var controller = clone.GetComponent<SkyTransitionController>();
            controller.showDemoControls = false;
            if (source)
            {
                controller.sunsetColor = source.sunsetColor;
                controller.sunsetIntensity = source.sunsetIntensity;
                controller.sunsetSpread = source.sunsetSpread;
                controller.enableSunsetTint = source.enableSunsetTint;
                controller.sunsetEndHour = source.sunsetEndHour;
                controller.nightStartHour = source.nightStartHour;
                controller.nightCompleteHour = source.nightCompleteHour;
            }
            var template = (Material)new SerializedObject(controller).FindProperty("skyTemplate").objectReferenceValue;
            var material = new Material(template);
            typeof(SkyTransitionController).GetField("runtimeSky", flags).SetValue(controller, material);
            var apply = typeof(SkyTransitionController).GetMethod("Apply", flags);
            var cameraObject = new GameObject("夕暮れ検証用カメラ") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.rotation = Quaternion.Euler(-12, 205, 0);
            camera.fieldOfView = 80;
            camera.aspect = 16f / 9;
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                RenderSettings.skybox = material;
                controller.cloudCoverage = 0.34f;
                controller.upperAtmosphere = 0;
                Directory.CreateDirectory(Root + "/TwilightPreviews");
                foreach (float hour in new[] { 18.1f, 18.3f, 19.25f, 20.4f, 21.5f })
                {
                    controller.timeOfDay = hour;
                    apply.Invoke(controller, null);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    image.Apply();
                    string name = hour.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    File.WriteAllBytes(Root + "/TwilightPreviews/" + name + ".png", image.EncodeToPNG());
                }
                if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("空のシェーダーにエラーがあります。");
                Debug.Log("SKY_TWILIGHT_VALIDATION_OK：18.1時・18.3時・19.25時・20.4時・21.5時の描画確認完了。");
            }
            finally
            {
                RenderSettings.skybox = originalSky;
                RenderSettings.sun = originalSun;
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = ambient;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.reflectionIntensity = reflection;
                RenderTexture.active = active;
                typeof(SkyTransitionController).GetField("runtimeSky", flags).SetValue(controller, null);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
            AssetDatabase.Refresh();
        }
    }
}
