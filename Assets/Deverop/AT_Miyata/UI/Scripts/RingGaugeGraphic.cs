using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リング状のプログレスゲージ（uGUI）。UI/AT_Miyata/RingGauge シェーダーで描く。
/// 真下から円に沿って、角度で色が付いていく。色が付く前の部分は半透明のグレーで表示する。
/// パラメータは頂点データで渡すので、エディタ上でもインスペクターを触るとすぐ見た目に反映される。
/// 四角形のうち、短い辺に合わせた正方形の中に描く。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class RingGaugeGraphic : MaskableGraphic
{
    private const string ShaderName = "UI/AT_Miyata/RingGauge";

    // シェーダーへ渡すのに使う頂点チャンネル
    private const AdditionalCanvasShaderChannels RequiredChannels =
        AdditionalCanvasShaderChannels.TexCoord1 |
        AdditionalCanvasShaderChannels.TexCoord2 |
        AdditionalCanvasShaderChannels.TexCoord3 |
        AdditionalCanvasShaderChannels.Tangent;

    private static Material s_defaultMaterial;

    [Header("Progress")]
    [SerializeField, Range(0f, 1f)] private float _progress = 0.5f;

    [Header("Shape")]
    [Tooltip("リングの外側の半径（1 = 四角形の端）")]
    [SerializeField, Range(0f, 1f)] private float _outerRadius = 0.68f;

    [Tooltip("リングの幅（外側の半径からの厚み）")]
    [SerializeField, Range(0f, 1f)] private float _thickness = 0.08f;

    [Tooltip("色が付き始める角度（度）。0 = 真下")]
    [SerializeField, Range(-180f, 180f)] private float _startAngle = 0f;

    [Tooltip("ゲージとして使う弧の長さ（度）。360で一周")]
    [SerializeField, Range(1f, 360f)] private float _arcAngle = 180f;

    [Tooltip("ON：下→左→上（時計回り） / OFF：下→右→上（反時計回り）")]
    [SerializeField] private bool _clockwise = false;

    [Header("Colors")]
    [Tooltip("色が付いた部分の色")]
    [SerializeField] private Color _fillColor = new Color(0.2f, 0.9f, 1f, 1f);

    [Tooltip("色が付く前の部分の色")]
    [SerializeField] private Color _emptyColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    /// <summary>0～1の進捗。変わったときだけ再描画する。</summary>
    public float Progress
    {
        get => _progress;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _progress))
            {
                return;
            }

            _progress = clamped;
            SetVerticesDirty();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value)
            {
                return;
            }

            _fillColor = value;
            SetVerticesDirty();
        }
    }

    public Color EmptyColor
    {
        get => _emptyColor;
        set
        {
            if (_emptyColor == value)
            {
                return;
            }

            _emptyColor = value;
            SetVerticesDirty();
        }
    }

    public override Material defaultMaterial
    {
        get
        {
            if (s_defaultMaterial == null)
            {
                // ビルドではシェーダーが含まれない可能性があるので、本番はマテリアルアセットを Material 欄に設定すること
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    return base.defaultMaterial;
                }

                s_defaultMaterial = new Material(shader) { name = "UIRingGauge (Default)" };
            }

            return s_defaultMaterial;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureCanvasChannels();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnsureCanvasChannels();
    }

    private void EnsureCanvasChannels()
    {
        Canvas c = canvas;
        if (c == null)
        {
            return;
        }

        if ((c.additionalShaderChannels & RequiredChannels) != RequiredChannels)
        {
            c.additionalShaderChannels |= RequiredChannels;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        Vector2 center = rect.center;
        float half = size * 0.5f;

        float outer = _outerRadius;
        float inner = Mathf.Max(0f, _outerRadius - _thickness);
        float direction = _clockwise ? -1f : 1f;

        Vector4 gauge = new Vector4(_progress, inner, outer, 0f);
        Vector4 arc = new Vector4(_startAngle * Mathf.Deg2Rad, _arcAngle * Mathf.Deg2Rad, direction, 0f);
        // 頂点データで渡す色は自動で色空間変換されないので、Linearのプロジェクトでは自前で変換する
        bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        Vector4 fill = isLinear ? _fillColor.linear : _fillColor;
        Vector4 empty = isLinear ? _emptyColor.linear : _emptyColor;
        Color32 tint = color;

        AddVertex(vh, center + new Vector2(-half, -half), new Vector2(0f, 0f), tint, gauge, arc, fill, empty);
        AddVertex(vh, center + new Vector2(-half, half), new Vector2(0f, 1f), tint, gauge, arc, fill, empty);
        AddVertex(vh, center + new Vector2(half, half), new Vector2(1f, 1f), tint, gauge, arc, fill, empty);
        AddVertex(vh, center + new Vector2(half, -half), new Vector2(1f, 0f), tint, gauge, arc, fill, empty);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Vector2 uv, Color32 tint,
        Vector4 gauge, Vector4 arc, Vector4 fill, Vector4 empty)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = position;
        v.color = tint;
        v.uv0 = uv;
        v.uv1 = gauge;
        v.uv2 = arc;
        v.uv3 = fill;
        v.tangent = empty;
        vh.AddVert(v);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureCanvasChannels();
    }
#endif
}
