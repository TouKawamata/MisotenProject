using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 任意の3Dモデル（機体・障害物・建物・リング・トロフィー等）にアタッチして、
/// 4種類のホログラムスタイル（王道スキャンライン / 六角形ヘックスシールド / 3D等高線ワイヤーフレーム / サイバーRGBグリッチ）
/// をワンクリックで切り替えて比較・適用できるコンポーネント。
/// </summary>
[ExecuteAlways]
public class ModelHologramController : MonoBehaviour
{
    public enum ModelHologramStyle
    {
        [InspectorName("① 王道スキャンライン (S_ModelHologram)")]
        ClassicScanline = 0,

        [InspectorName("② 六角形ヘックスシールド網目 (S_ModelHologram_HexGrid)")]
        HexShield = 1,

        [InspectorName("③ 3D等高線CADワイヤーフレーム (S_ModelHologram_WireframeGrid)")]
        WireframeGrid = 2,

        [InspectorName("④ サイバーRGB色収差＆頂点崩壊グリッチ (S_ModelHologram_CyberGlitch)")]
        CyberGlitch = 3,

        [InspectorName("⑤ 旗風：サイバーLEDドットマトリクス (S_ModelHologram_LEDMatrix)")]
        FlagStyle_LEDMatrix = 4,

        [InspectorName("⑥ 旗風：流れるサイバーパターン＆スキャンライン (S_ModelHologram_PatternBanner)")]
        FlagStyle_PatternBanner = 5,

        [InspectorName("⑦ 旗風：台座プロジェクター照射＆RGB色収差 (S_ModelHologram_ProjectorBeam)")]
        FlagStyle_ProjectorBeam = 6
    }

    [Header("ホログラムスタイル切り替え")]
    [Tooltip("適用する3Dモデルホログラムの種類")]
    [SerializeField] private ModelHologramStyle _hologramStyle = ModelHologramStyle.ClassicScanline;

    [Header("テクスチャ・カラー設定")]
    [Tooltip("モデルの元テクスチャ（未指定でもホログラム発光します）")]
    [SerializeField] private Texture2D _mainTexture;

    [Tooltip("テクスチャの元のRGBカラー比率を保持するか（0=単色ホログラム, 1=テクスチャのカラーを反映）")]
    [Range(0f, 1f)]
    [SerializeField] private float _useTextureColor = 0.0f;

    [ColorUsage(true, true)]
    [SerializeField] private Color _mainHoloColor = new Color(0.0f, 2.0f, 3.2f, 0.45f);

    [ColorUsage(true, true)]
    [SerializeField] private Color _rimGlowColor = new Color(0.4f, 3.8f, 4.5f, 0.95f);

    [ColorUsage(true, true)]
    [SerializeField] private Color _accentFeatureColor = new Color(0.2f, 4.0f, 3.5f, 1.0f);

    [Header("カスタムマテリアル（指定時は最優先使用）")]
    [Tooltip("指定時はこのマテリアルが最優先でRendererに割り当てられます。null時は上の設定に基づき自動生成されます。")]
    [SerializeField] private Material _customMaterial;

    [Header("別名保存用の設定")]
    [Tooltip("別名保存時のファイル名（未入力の場合は M_Holo_オブジェクト名 で自動生成）")]
    [SerializeField] private string _customSaveName = "";

    public string CustomSaveName
    {
        get => _customSaveName;
        set => _customSaveName = value;
    }

    public Material CustomMaterial
    {
        get => _customMaterial;
        set
        {
            _customMaterial = value;
            UpdateMaterial();
        }
    }

    private Renderer _targetRenderer;
    private Material _runtimeMaterial;
    private ModelHologramStyle _lastStyle = (ModelHologramStyle)(-1);

    private static readonly int MainTexId          = Shader.PropertyToID("_MainTex");
    private static readonly int UseTexColorId      = Shader.PropertyToID("_UseTexColor");
    private static readonly int MainColorId        = Shader.PropertyToID("_MainColor");
    private static readonly int RimColorId         = Shader.PropertyToID("_RimColor");
    private static readonly int HexWireColorId     = Shader.PropertyToID("_HexWireColor");
    private static readonly int GridColorId        = Shader.PropertyToID("_GridColor");
    private static readonly int ScanPlaneColorId   = Shader.PropertyToID("_ScanPlaneColor");
    private static readonly int GlitchGlowColorId  = Shader.PropertyToID("_GlitchGlowColor");
    private static readonly int SubColorId         = Shader.PropertyToID("_SubColor");
    private static readonly int EmitterColorId     = Shader.PropertyToID("_EmitterColor");

    private void OnEnable()
    {
        EnsureTargetRenderer();
        UpdateMaterial();
    }

    private void OnDisable()
    {
        CleanupRuntimeMaterial();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                EnsureTargetRenderer();
                UpdateMaterial();
            }
        };
#endif
    }

    public void SetHologramStyle(ModelHologramStyle style)
    {
        _hologramStyle = style;
        UpdateMaterial();
    }

    public ModelHologramStyle GetHologramStyle() => _hologramStyle;

    public void Configure(ModelHologramStyle style, Color mainColor, Color rimColor, Color accentColor)
    {
        _hologramStyle = style;
        _mainHoloColor = mainColor;
        _rimGlowColor = rimColor;
        _accentFeatureColor = accentColor;
        UpdateMaterial();
    }

    private void EnsureTargetRenderer()
    {
        if (_targetRenderer == null)
        {
            _targetRenderer = GetComponent<Renderer>();
        }
    }

    /// <summary>
    /// 現在のインスペクター設定（スタイル・テクスチャ・各色）を反映した独立マテリアルインスタンスを新規生成
    /// （別名保存用）
    /// </summary>
    public Material CreateConfiguredMaterial()
    {
        string shaderPath = GetShaderPath(_hologramStyle);
        Shader shader = Shader.Find(shaderPath);
        if (shader == null)
        {
            Debug.LogError($"[ModelHologramController] シェーダー '{shaderPath}' が見つかりません。");
            return null;
        }

        Material mat = new Material(shader);
        ApplyProperties(mat);
        return mat;
    }

    /// <summary>
    /// カスタムマテリアルの割り当てを解除し、インスペクター設定によるリアルタイム調整モードに戻す
    /// </summary>
    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateMaterial();
    }

    public void UpdateMaterial()
    {
        EnsureTargetRenderer();
        if (_targetRenderer == null) return;

        if (_customMaterial != null)
        {
            _targetRenderer.sharedMaterial = _customMaterial;
            return;
        }

        string shaderPath = GetShaderPath(_hologramStyle);
        Shader shader = Shader.Find(shaderPath);
        if (shader == null) return;

        if (_runtimeMaterial == null || _lastStyle != _hologramStyle || _runtimeMaterial.shader != shader)
        {
            CleanupRuntimeMaterial();
            _runtimeMaterial = new Material(shader)
            {
                name = $"M_Runtime_{_hologramStyle}",
                hideFlags = HideFlags.DontSave
            };
            _lastStyle = _hologramStyle;
        }

        ApplyProperties(_runtimeMaterial);
        _targetRenderer.sharedMaterial = _runtimeMaterial;
        _targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _targetRenderer.receiveShadows = false;
    }

    private void ApplyProperties(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty(MainTexId))
        {
            mat.SetTexture(MainTexId, _mainTexture != null ? _mainTexture : Texture2D.whiteTexture);
        }
        if (mat.HasProperty(UseTexColorId))
        {
            mat.SetFloat(UseTexColorId, _useTextureColor);
        }
        if (mat.HasProperty(MainColorId))
        {
            mat.SetColor(MainColorId, _mainHoloColor);
        }
        if (mat.HasProperty(RimColorId))
        {
            mat.SetColor(RimColorId, _rimGlowColor);
        }
        if (mat.HasProperty(HexWireColorId))
        {
            mat.SetColor(HexWireColorId, _accentFeatureColor);
        }
        if (mat.HasProperty(GridColorId))
        {
            mat.SetColor(GridColorId, _accentFeatureColor);
        }
        if (mat.HasProperty(ScanPlaneColorId))
        {
            mat.SetColor(ScanPlaneColorId, _accentFeatureColor);
        }
        if (mat.HasProperty(GlitchGlowColorId))
        {
            mat.SetColor(GlitchGlowColorId, _accentFeatureColor);
        }
        if (mat.HasProperty(SubColorId))
        {
            mat.SetColor(SubColorId, _accentFeatureColor * 0.35f);
        }
        if (mat.HasProperty(EmitterColorId))
        {
            mat.SetColor(EmitterColorId, _accentFeatureColor);
        }
    }

    public static string GetShaderPath(ModelHologramStyle style)
    {
        switch (style)
        {
            case ModelHologramStyle.ClassicScanline:
                return "Custom/Hologram/ModelHologram";
            case ModelHologramStyle.HexShield:
                return "Custom/Hologram/ModelHologram_HexGrid";
            case ModelHologramStyle.WireframeGrid:
                return "Custom/Hologram/ModelHologram_WireframeGrid";
            case ModelHologramStyle.CyberGlitch:
                return "Custom/Hologram/ModelHologram_CyberGlitch";
            case ModelHologramStyle.FlagStyle_LEDMatrix:
                return "Custom/Hologram/ModelHologram_LEDMatrix";
            case ModelHologramStyle.FlagStyle_PatternBanner:
                return "Custom/Hologram/ModelHologram_PatternBanner";
            case ModelHologramStyle.FlagStyle_ProjectorBeam:
                return "Custom/Hologram/ModelHologram_ProjectorBeam";
            default:
                return "Custom/Hologram/ModelHologram";
        }
    }

    private void CleanupRuntimeMaterial()
    {
        if (_runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }
}

