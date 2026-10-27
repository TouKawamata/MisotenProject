using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Minakuchi.Sky
{
    public sealed class CloudVolumeFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader cloudShader;
        [SerializeField] private TextAsset cloudNoiseData;
        private Texture3D noise;
        private Material material;
        private CloudPass pass;

        public override void Create()
        {
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
            pass = new CloudPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection
                || camera.renderType == CameraRenderType.Overlay) return;
            var controller = SkyTransitionController.Active;
            if (!material || !noise || !controller || !controller.PrepareCloudVolume(material)) return;
            material.SetTexture("_CloudNoise", noise);
            pass.material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(noise);
        }

        private sealed class CloudPass : ScriptableRenderPass
        {
            public Material material;
            private sealed class PassData { public Material material; public Vector4 texelSize; }
            private static readonly int BufferId = Shader.PropertyToID("_CloudVolumeBuffer");
            private static readonly int TexelId = Shader.PropertyToID("_CloudVolumeBuffer_TexelSize");

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
                using (var builder = graph.AddRasterRenderPass<PassData>("立体雲と自己遮光", out var data))
                {
                    data.material = material;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(volume, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(volume, BufferId);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1));
                }
                using (var builder = graph.AddRasterRenderPass<PassData>("雲の段差除去と奥行き合成", out var data))
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
