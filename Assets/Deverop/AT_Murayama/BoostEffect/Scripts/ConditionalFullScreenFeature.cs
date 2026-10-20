using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 全画面シェーダーを1枚かける Renderer Feature。
/// 「強さ」を保持するグローバル変数が 0 のときはパスを積まないので、使っていない間の負荷が無い。
/// ラジアルブラー・集中線など、グローバル変数で強さを渡す全画面エフェクトで共通に使う
/// （エフェクトごとにこの Feature を1つずつ PC_Renderer に追加する）。
/// </summary>
public class ConditionalFullScreenFeature : ScriptableRendererFeature
{
    [Tooltip("全画面にかけるマテリアル。シェーダーは Blit.hlsl の Vert を使うもの")]
    [SerializeField] private Material _material;

    [Tooltip("マテリアルのパス番号")]
    [SerializeField] private int _passIndex = 0;

    [Tooltip("パスを差し込むタイミング")]
    [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [Header("有効にする条件")]
    [Tooltip("強さを持つグローバル float のプロパティ名（例：_RadialBlurStrength）。この値がしきい値以下ならパスを積まない")]
    [SerializeField] private string _strengthProperty = "_RadialBlurStrength";

    [Tooltip("この値以下は「0」とみなす")]
    [SerializeField] private float _threshold = 0.001f;

    private ConditionalFullScreenPass _pass;
    private int _strengthId;

    public override void Create()
    {
        _strengthId = Shader.PropertyToID(_strengthProperty);
        _pass = new ConditionalFullScreenPass(name)
        {
            renderPassEvent = _injectionPoint
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null)
        {
            return;
        }

        // プレビュー、反射用、エディタのSceneビューカメラには入れない（ゲーム画面のみに適用）
        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
        {
            return;
        }

        // レンダーテクスチャに出力しているサブカメラ（ミニマップやバックミラー等）にはかけない
        if (renderingData.cameraData.targetTexture != null)
        {
            return;
        }

        // 強さが0なら何もしない（ここでパスを積まないのが、負荷を無くす本体）
        if (Shader.GetGlobalFloat(_strengthId) <= _threshold)
        {
            return;
        }

        _pass.Setup(_material, _passIndex);
        renderer.EnqueuePass(_pass);
    }

    private class ConditionalFullScreenPass : ScriptableRenderPass
    {
        private readonly string _passName;
        private Material _material;
        private int _passIndex;

        public ConditionalFullScreenPass(string passName)
        {
            _passName = passName;
        }

        public void Setup(Material material, int passIndex)
        {
            _material = material;
            _passIndex = passIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // 画面そのもの（バックバッファ）には直接ブリットできないので、そのときはかけない
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            // 一時テクスチャにマテリアルを通して描き、元の画面へ書き戻す
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = _passName + "_Temp";
            desc.clearBuffer = false;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, _passIndex),
                _passName + "_Blit");
            renderGraph.AddCopyPass(temp, source, _passName + "_Copy");
        }
    }
}

