using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Minakuchi.AssetSky.Editor
{
    public static class AssetSkyValidation
    {
        private const string Root = "Assets/Deverop/AT_Minakuchi/SkyAssetVersion";

        [MenuItem("水口/アセット版の空/検証とプレビュー作成")]
        public static void Validate()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/AssetTransitionSky.mat");
            if (!material) throw new InvalidOperationException("アセット版の空のマテリアルがありません。");
            foreach (var property in new[] { "_CloudySky", "_PartlyCloudySky", "_NightSky", "_AtmosphereSky" })
                if (!(material.GetTexture(property) is Cubemap)) throw new InvalidOperationException("背景画像の参照エラー：" + property);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deverop/AT_Minakuchi/Prefab/AssetSkyEnvironment.prefab");
            var controller = prefab ? prefab.GetComponent<AssetSkyTransitionController>() : null;
            if (!controller || !controller.cloudySky || !controller.partlyCloudySky || !controller.nightSky || !controller.atmosphereSky)
                throw new InvalidOperationException("アセット版プレハブの背景画像が不足しています。");
            Directory.CreateDirectory(Root + "/Previews");
            RenderPreview(material, "Cloudy", 0, 0);
            RenderPreview(material, "Night", 1, 0);
            RenderPreview(material, "Atmosphere", 0, 1);
            RenderPreview(material, "Sunset", 0.35f, 0);
            foreach (var error in ShaderUtil.GetShaderMessages(material.shader)) Debug.Log("アセット版の空：" + error.message);
            if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("アセット版の空のシェーダーにエラーがあります。");
            AssetDatabase.Refresh();
            Debug.Log("ASSET_SKY_VALIDATION_OK：背景4種・専用プレハブ・曇り／夜空／大気圏の描画を確認しました。");
        }

        private static void RenderPreview(Material template, string name, float night, float space)
        {
            var previousSky = RenderSettings.skybox;
            var previousTarget = RenderTexture.active;
            var sky = new Material(template);
            var go = new GameObject("アセット版の空の検証カメラ") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 0;
            camera.transform.rotation = Quaternion.Euler(-15, 0, 0);
            camera.fieldOfView = 80;
            camera.aspect = 16f / 9;
            var texture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                texture.Create();
                sky.SetFloat("_UseAssetSky", 1);
                sky.SetFloat("_Night", night);
                sky.SetFloat("_Space", space);
                sky.SetFloat("_Clouds", night > 0.5f ? 0.1f : 0.72f);
                sky.SetFloat("_CloudHaze", 0);
                sky.SetFloat("_SunsetAmount", name == "Sunset" ? 1 : 0);
                RenderSettings.skybox = sky;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(Root + "/Previews/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderSettings.skybox = previousSky;
                RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(sky);
            }
        }
    }
}

