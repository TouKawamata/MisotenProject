using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 頂点シェーダーで滑らかに揺れる「分割済みフラッグメッシュ（6形状対応）」を生成し、
/// ホログラム無し通常旗 ＋ 3種のホログラム旗 ＋ 3Dモデル汎用ホログラムの計5種のシェーダーや
/// 任意のテクスチャ・内蔵模様をワンクリックで切り替えて比較できるコンポーネント。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HologramFlagMesh : MonoBehaviour
{
    public enum HologramShaderType
    {
        [InspectorName("① 案A：空間スキャンライン旗 (S_HologramFlag_Scanline)")]
        Flag_Scanline = 0,

        [InspectorName("② 案B：LEDドットマトリクス旗 (S_HologramFlag_LEDMatrix)")]
        Flag_LEDMatrix = 1,

        [InspectorName("③ 案C：RGBグリッチ＆照射ビーム旗 (S_HologramFlag_CyberGlitch)")]
        Flag_CyberGlitch = 2,

        [InspectorName("④ 3Dモデル汎用ホログラム (S_ModelHologram)")]
        Model_Hologram = 3,

        [InspectorName("⑤ ホログラム無し：通常布の揺れる旗 (S_StandardWavingFlag)")]
        Flag_StandardCloth = 4
    }

    public enum FlagMeshShape
    {
        [InspectorName("1. 標準の長方形フラッグ (Rectangle)")]
        Rectangle = 0,

        [InspectorName("2. 燕尾・V字二股フラッグ (Swallowtail)")]
        Swallowtail = 1,

        [InspectorName("3. 三角ペナントフラッグ (TrianglePennant)")]
        TrianglePennant = 2,

        [InspectorName("4. 先端斜めカットフラッグ (AngledSlash)")]
        AngledSlash = 3,

        [InspectorName("5. 細長吹き流しストリーマー (TaperedStreamer)")]
        TaperedStreamer = 4,

        [InspectorName("6. 上端固定・縦長垂れ幕バナー (VerticalBanner)")]
        VerticalBanner = 5
    }

    public enum BuiltinPatternType
    {
        [InspectorName("テクスチャのみ（内蔵模様OFF）")]
        TextureOnly = 0,

        [InspectorName("🏁 チェッカーフラッグ")]
        CheckerFlag = 1,

        [InspectorName("⏩ サイバーシェブロン矢印")]
        CyberArrows = 2,

        [InspectorName("⚡ 斜めレーシングストライプ")]
        RacingStripes = 3
    }

    [Header("シェーダー ＆ 形状 ＆ 絵柄プリセット切り替え")]
    [Tooltip("使用するシェーダーの種類（ホログラム旗3種 / 通常布旗 / 3Dモデルホログラム）")]
    [SerializeField] private HologramShaderType _shaderType = HologramShaderType.Flag_Scanline;

    [Tooltip("旗メッシュの形状バリエーション（長方形 / 燕尾V字 / 三角ペナント / 斜めカット / 吹き流し / 縦垂れ幕）")]
    [SerializeField] private FlagMeshShape _meshShape = FlagMeshShape.Rectangle;

    [Tooltip("旗に貼りたいテクスチャ（未指定でも下の内蔵パターンで模様を表示できます）")]
    [SerializeField] private Texture2D _flagTexture;

    [Tooltip("テクスチャ未指定時（または重ね掛け用）の内蔵プロシージャル模様")]
    [SerializeField] private BuiltinPatternType _patternType = BuiltinPatternType.CheckerFlag;

    [Header("色・波揺れ調整")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _mainHoloColor = new Color(0.0f, 2.4f, 3.2f, 0.85f);

    [ColorUsage(true, true)]
    [SerializeField] private Color _borderHoloColor = new Color(0.6f, 4.0f, 4.5f, 1.0f);

    [Tooltip("風で揺れる波の大きさ（m）")]
    [Range(0f, 1.5f)]
    [SerializeField] private float _waveAmplitude = 0.35f;

    [Tooltip("風で揺れる波の速さ")]
    [Range(0f, 15f)]
    [SerializeField] private float _waveSpeed = 6.5f;

    [Header("旗メッシュ自動生成設定（Quadでは曲がらないため分割板を生成）")]
    [Tooltip("自動生成した旗メッシュを使うか（保存済みメッシュを手動割当する場合や3Dモデルの場合はOFF）")]
    [SerializeField] private bool _generateSubdividedFlagMesh = true;

    [Tooltip("旗の横幅（m）")]
    [SerializeField] private float _flagWidth = 4.0f;

    [Tooltip("旗の縦幅（m）")]
    [SerializeField] private float _flagHeight = 2.0f;

    [Tooltip("横方向の分割数（大きいほど波が滑らかになります。24〜40推奨）")]
    [Range(4, 80)]
    [SerializeField] private int _segmentsX = 32;

    [Tooltip("縦方向の分割数（8〜24推奨）")]
    [Range(2, 40)]
    [SerializeField] private int _segmentsY = 16;

    [Header("カスタムマテリアル（指定時はそのマテリアルを優先使用）")]
    [SerializeField] private Material _customMaterial;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _runtimeMaterial;
    private HologramShaderType _lastShaderType = (HologramShaderType)(-1);

    private static readonly int MainTexId       = Shader.PropertyToID("_MainTex");
    private static readonly int PatternModeId   = Shader.PropertyToID("_PatternMode");
    private static readonly int MainColorId     = Shader.PropertyToID("_MainColor");
    private static readonly int BorderColorId   = Shader.PropertyToID("_BorderColor");
    private static readonly int RimColorId      = Shader.PropertyToID("_RimColor");
    private static readonly int EmitterColorId  = Shader.PropertyToID("_EmitterColor");
    private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int WaveSpeedId     = Shader.PropertyToID("_WaveSpeed");

    private void OnEnable()
    {
        RebuildAll();
    }

    private void OnDisable()
    {
        CleanupRuntimeMaterial();
    }

    private void OnValidate()
    {
        _flagWidth = Mathf.Max(0.2f, _flagWidth);
        _flagHeight = Mathf.Max(0.2f, _flagHeight);
        _segmentsX = Mathf.Clamp(_segmentsX, 4, 80);
        _segmentsY = Mathf.Clamp(_segmentsY, 2, 40);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                RebuildAll();
            }
        };
#endif
    }

    public void SetShaderType(HologramShaderType type)
    {
        _shaderType = type;
        if (type == HologramShaderType.Flag_StandardCloth && _mainHoloColor.maxColorComponent > 1.2f)
        {
            // 通常布モードに切り替えた際、HDR色が眩しすぎれば見やすい標準色へ自動補正
            _mainHoloColor = new Color(0.95f, 0.95f, 0.98f, 1.0f);
            _borderHoloColor = new Color(1.0f, 0.75f, 0.15f, 1.0f);
        }
        else if (type != HologramShaderType.Flag_StandardCloth && _mainHoloColor.maxColorComponent <= 1.0f)
        {
            _mainHoloColor = new Color(0.0f, 2.4f, 3.2f, 0.85f);
            _borderHoloColor = new Color(0.6f, 4.0f, 4.5f, 1.0f);
        }
        RebuildAll();
    }

    public HologramShaderType GetShaderType() => _shaderType;

    public void SetMeshShape(FlagMeshShape shape)
    {
        _meshShape = shape;
        RebuildAll();
    }

    public FlagMeshShape GetMeshShape() => _meshShape;

    public void SetPatternType(BuiltinPatternType pat)
    {
        _patternType = pat;
        ApplyMaterialProperties();
    }

    public BuiltinPatternType GetPatternType() => _patternType;

    public void ConfigurePreset(
        HologramShaderType shaderType,
        FlagMeshShape meshShape,
        BuiltinPatternType patternType,
        Color mainColor,
        Color borderColor,
        bool generateFlagMesh)
    {
        _shaderType = shaderType;
        _meshShape = meshShape;
        _patternType = patternType;
        _mainHoloColor = mainColor;
        _borderHoloColor = borderColor;
        _generateSubdividedFlagMesh = generateFlagMesh;
        RebuildAll();
    }

    public void RebuildAll()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_generateSubdividedFlagMesh && _meshFilter != null)
        {
            _meshFilter.sharedMesh = CreateFlagMesh(_meshShape, _flagWidth, _flagHeight, _segmentsX, _segmentsY);
        }

        EnsureMaterial();
        ApplyMaterialProperties();
    }

    // ──────────────────────────────────────────────
    // 6種類の形状に対応した分割フラッグメッシュ生成（アセット保存にも共用）
    // ──────────────────────────────────────────────

    public static Mesh CreateFlagMesh(FlagMeshShape shape, float width, float height, int segX = 32, int segY = 16)
    {
        int cols = Mathf.Clamp(segX, 4, 80);
        int rows = Mathf.Clamp(segY, 2, 40);
        int vertCount = (cols + 1) * (rows + 1);

        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[cols * rows * 6];

        float effectiveW = width;
        float effectiveH = height;
        if (shape == FlagMeshShape.TaperedStreamer)
        {
            effectiveW = width * 1.35f;
            effectiveH = height * 0.55f;
        }
        else if (shape == FlagMeshShape.VerticalBanner)
        {
            effectiveW = height * 0.9f;  // 横幅
            effectiveH = width * 1.05f;  // 縦の長さ
        }

        float halfH = effectiveH * 0.5f;

        for (int y = 0; y <= rows; y++)
        {
            float v = (float)y / rows; // 0(下端) 〜 1(上端)
            float centeredV = (v - 0.5f) * 2f; // -1 〜 +1

            for (int x = 0; x <= cols; x++)
            {
                float u = (float)x / cols; // 0(根元) 〜 1(先端)
                Vector3 pos;

                switch (shape)
                {
                    case FlagMeshShape.Swallowtail:
                    {
                        // 燕尾形（先端の中央がV字型に内側へ切れ込む二股フラッグ）
                        float vCutDepth = 0.32f; // V字の切れ込み深さ比率
                        float edgeFactor = Mathf.Abs(centeredV); // 中央で0、上下端で1
                        float xReach = Mathf.Lerp(1f - vCutDepth, 1f, edgeFactor);
                        float posX = u * effectiveW * xReach;
                        float posY = Mathf.Lerp(-halfH, halfH, v);
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }

                    case FlagMeshShape.TrianglePennant:
                    {
                        // 三角ペナント（先端 u=1 に向かって上下幅が 0 近くに絞られる三角形フラッグ）
                        float taper = Mathf.Lerp(1f, 0.03f, u);
                        float posX = u * effectiveW;
                        float posY = centeredV * halfH * taper;
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }

                    case FlagMeshShape.AngledSlash:
                    {
                        // 斜めスラッシュカット（上辺が長く、下辺が斜めにスパッと切れたレーシングフラッグ）
                        float slashReach = Mathf.Lerp(0.62f, 1.0f, v);
                        float posX = u * effectiveW * slashReach;
                        float posY = Mathf.Lerp(-halfH, halfH, v);
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }

                    case FlagMeshShape.TaperedStreamer:
                    {
                        // 細長吹き流しストリーマー（先端に向かって緩やかに細くなるロングリボン）
                        float taper = Mathf.Lerp(1f, 0.22f, u);
                        float posX = u * effectiveW;
                        float posY = centeredV * halfH * taper;
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }

                    case FlagMeshShape.VerticalBanner:
                    {
                        // 上端固定・縦長垂れ幕バナー（Y=0 が上端バーに固定され、下方向 -Y へ垂れ下がり先端が燕尾Vカット）
                        float vCut = Mathf.Lerp(0.82f, 1.0f, Mathf.Abs(centeredV));
                        float posX = centeredV * (effectiveW * 0.5f);
                        float posY = -u * effectiveH * vCut;
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }

                    case FlagMeshShape.Rectangle:
                    default:
                    {
                        // 標準の長方形フラッグ
                        float posX = u * effectiveW;
                        float posY = Mathf.Lerp(-halfH, halfH, v);
                        pos = new Vector3(posX, posY, 0f);
                        break;
                    }
                }

                int idx = y * (cols + 1) + x;
                vertices[idx] = pos;
                normals[idx] = new Vector3(0f, 0f, -1f);
                uvs[idx] = new Vector2(u, v);
            }
        }

        int t = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int i00 = y * (cols + 1) + x;
                int i10 = i00 + 1;
                int i01 = (y + 1) * (cols + 1) + x;
                int i11 = i01 + 1;

                triangles[t++] = i00;
                triangles[t++] = i01;
                triangles[t++] = i10;

                triangles[t++] = i10;
                triangles[t++] = i01;
                triangles[t++] = i11;
            }
        }

        Mesh mesh = new Mesh
        {
            name = $"Mesh_Flag_{shape}"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);

        // 頂点シェーダーで前後に大きく揺れてもカリングされないよう余裕のあるBoundsを設定
        Vector3 boundsCenter = (shape == FlagMeshShape.VerticalBanner)
            ? new Vector3(0f, -effectiveH * 0.5f, 0f)
            : new Vector3(effectiveW * 0.5f, 0f, 0f);
        mesh.bounds = new Bounds(boundsCenter, new Vector3(effectiveW + 3f, effectiveH + 3f, 5f));

        return mesh;
    }

    private void EnsureMaterial()
    {
        if (_meshRenderer == null) return;

        if (_customMaterial != null)
        {
            _meshRenderer.sharedMaterial = _customMaterial;
            return;
        }

        string shaderName = GetShaderPath(_shaderType);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) return;

        if (_runtimeMaterial == null || _lastShaderType != _shaderType || _runtimeMaterial.shader != shader)
        {
            CleanupRuntimeMaterial();
            _runtimeMaterial = new Material(shader)
            {
                name = $"M_Runtime_{_shaderType}",
                hideFlags = HideFlags.DontSave
            };
            _lastShaderType = _shaderType;
        }

        _meshRenderer.sharedMaterial = _runtimeMaterial;
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
    }

    private void ApplyMaterialProperties()
    {
        Material targetMat = (_customMaterial != null) ? _customMaterial : _runtimeMaterial;
        if (targetMat == null) return;

        if (targetMat.HasProperty(MainTexId))
        {
            targetMat.SetTexture(MainTexId, _flagTexture != null ? _flagTexture : Texture2D.whiteTexture);
        }
        if (targetMat.HasProperty(PatternModeId))
        {
            targetMat.SetFloat(PatternModeId, (float)_patternType);
        }
        if (targetMat.HasProperty(MainColorId))
        {
            targetMat.SetColor(MainColorId, _mainHoloColor);
        }
        if (targetMat.HasProperty(BorderColorId))
        {
            targetMat.SetColor(BorderColorId, _borderHoloColor);
        }
        if (targetMat.HasProperty(RimColorId))
        {
            targetMat.SetColor(RimColorId, _borderHoloColor);
        }
        if (targetMat.HasProperty(EmitterColorId))
        {
            targetMat.SetColor(EmitterColorId, _borderHoloColor);
        }
        if (targetMat.HasProperty(WaveAmplitudeId))
        {
            targetMat.SetFloat(WaveAmplitudeId, _waveAmplitude);
        }
        if (targetMat.HasProperty(WaveSpeedId))
        {
            targetMat.SetFloat(WaveSpeedId, _waveSpeed);
        }
    }

    public static string GetShaderPath(HologramShaderType type)
    {
        switch (type)
        {
            case HologramShaderType.Flag_Scanline:
                return "Custom/Hologram/HologramFlag_Scanline";
            case HologramShaderType.Flag_LEDMatrix:
                return "Custom/Hologram/HologramFlag_LEDMatrix";
            case HologramShaderType.Flag_CyberGlitch:
                return "Custom/Hologram/HologramFlag_CyberGlitch";
            case HologramShaderType.Model_Hologram:
                return "Custom/Hologram/ModelHologram";
            case HologramShaderType.Flag_StandardCloth:
                return "Custom/Hologram/StandardWavingFlag";
            default:
                return "Custom/Hologram/HologramFlag_Scanline";
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
