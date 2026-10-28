using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Minakuchi.Sky
{
    public sealed class PlacedCloudVolumeFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader cloudShader;
        [SerializeField] private TextAsset cloudNoiseData;
        private Texture3D noise;
        private Material material;
        private readonly Dictionary<CloudVolumeObject, CloudPass> passes = new Dictionary<CloudVolumeObject, CloudPass>();
        private readonly List<CloudVolumeObject> ordered = new List<CloudVolumeObject>();
        private readonly List<CloudVolumeObject> removed = new List<CloudVolumeObject>();

        private void ClearPasses()
        {
            foreach (var entry in passes) CoreUtils.Destroy(entry.Value.material);
            passes.Clear();
        }

        public override void Create()
        {
            ClearPasses();
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(noise);
            if (cloudShader) material = CoreUtils.CreateEngineMaterial(cloudShader);
            if (cloudNoiseData && cloudNoiseData.bytes.Length == 64 * 64 * 64 * 4)
            {
                noise = new Texture3D(64, 64, 64, TextureFormat.RGBA32, false)
                {
                    name = "立体雲の形状ノイズ", wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                noise.SetPixelData(cloudNoiseData.bytes, 0);
                noise.Apply(false, true);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection
                || camera.renderType == CameraRenderType.Overlay) return;
            if (!material || !noise) return;
            removed.Clear();
            foreach (var entry in passes)
                if (!entry.Key || !entry.Key.isActiveAndEnabled) removed.Add(entry.Key);
            foreach (var cloud in removed)
            {
                CoreUtils.Destroy(passes[cloud].material);
                passes.Remove(cloud);
            }
            ordered.Clear();
            var view = camera.camera;
            foreach (var cloud in CloudVolumeObject.Instances)
            {
                if (!cloud || !cloud.isActiveAndEnabled || cloud.coverage <= 0) continue;
                if ((view.cullingMask & (1 << cloud.gameObject.layer)) == 0) continue;
                if (view.scene.IsValid() && cloud.gameObject.scene != view.scene) continue;
                ordered.Add(cloud);
            }
            var eye = view.transform.position;
            ordered.Sort((a, b) => (b.transform.position - eye).sqrMagnitude.CompareTo((a.transform.position - eye).sqrMagnitude));
            foreach (var cloud in ordered)
            {
                if (!passes.TryGetValue(cloud, out var cloudPass))
                {
                    cloudPass = new CloudPass
                    {
                        material = new Material(material) { hideFlags = HideFlags.HideAndDontSave },
                        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
                    };
                    cloudPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                    passes.Add(cloud, cloudPass);
                }
                cloud.Apply(cloudPass.material);
                cloudPass.material.SetTexture("_CloudNoise", noise);
                renderer.EnqueuePass(cloudPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            ClearPasses();
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(noise);
        }

        private sealed class CloudPass : ScriptableRenderPass
        {
            public Material material;
            private sealed class PassData { public Material material; public Vector4 texelSize; }
            private static readonly int BufferId = Shader.PropertyToID("_PlacedCloudVolumeBuffer");
            private static readonly int TexelId = Shader.PropertyToID("_PlacedCloudVolumeBuffer_TexelSize");

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                int width = Mathf.Max(1, (camera.cameraTargetDescriptor.width + 1) / 2);
                int height = Mathf.Max(1, (camera.cameraTargetDescriptor.height + 1) / 2);
                var desc = new TextureDesc(width, height, xrReady: camera.xr.enabled)
                {
                    name = "雲の散乱光と透過率", colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    clearBuffer = false, filterMode = FilterMode.Point
                };
                var volume = graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<PassData>("追加の雲と自己遮光", out var data))
                {
                    data.material = material;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(volume, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(volume, BufferId);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1));
                }
                using (var builder = graph.AddRasterRenderPass<PassData>("追加の雲の奥行き合成", out var data))
                {
                    data.material = material;
                    data.texelSize = new Vector4(1f / width, 1f / height, width, height);
                    builder.UseTexture(volume, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        var properties = new MaterialPropertyBlock();
                        properties.SetVector(TexelId, d.texelSize);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 1, MeshTopology.Triangles, 3, 1, properties);
                    });
                }
            }
        }
    }
}
