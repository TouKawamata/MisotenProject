using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物理ホログラムプロジェクター台座（Hologram Projector Spire / Pedestal）制御コンポーネント。
/// ・ビル屋上やテラスに配置するメカニカル架台 ＆ 上空へ放射される半透明集光ビームコーン。
/// ・既存のホログラム（3Dモデル・旗・看板など）の直下に置くことで、「なぜ空中にホログラムが投影されているのか」の物理的説得力を付与。
/// ・上端の「HologramAnchor」Transformに既存ホログラムを配置・ペアリング可能。
/// ・MaterialPropertyBlock によるDrawCallバッチング維持。
/// ・Update() 不使用（CPU負荷ゼロ）。
/// ・コライダーはデフォルトOFF（背景スレスレ通過時の安全設計）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class HologramProjectorSpireController : MonoBehaviour
{
    public enum SpirePreset
    {
        [InspectorName("🔷 クアッド・スパイヤ (4基 / Cyber Cyan / 15m)")]
        QuadSpireArray = 0,

        [InspectorName("🏗️ 重工業三脚プラットフォーム (3基 / Industrial Amber / 20m)")]
        HeavyTripodPlatform = 1,

        [InspectorName("🏢 屋上グランド・アレイ (6基 / Clean White / 25m)")]
        RooftopGrandArray = 2,

        [InspectorName("🟢 コンパクト・ペデスタル (4基 / Emerald Green / 8m)")]
        CompactPedestal = 3,

        [InspectorName("⚙️ カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum EmitterConfig
    {
        [InspectorName("4基配置 (Quad 4-Head)")]
        Quad_4 = 4,

        [InspectorName("3基配置 (Tripod 3-Head)")]
        Tripod_3 = 3,

        [InspectorName("6基配置 (Hexa 6-Head)")]
        Hexa_6 = 6,

        [InspectorName("8基リング配置 (Ring 8-Head)")]
        Ring_8 = 8
    }

    public enum ColorTheme
    {
        [InspectorName("Cyber Cyan (ネオンシアン × ダークアロイ)")]
        CyberCyan = 0,

        [InspectorName("Industrial Amber (セーフティアンバー × 重防食鋼)")]
        IndustrialAmber = 1,

        [InspectorName("Clean White (白亜建築 × アイスブルー光)")]
        CleanWhite = 2,

        [InspectorName("Stealth Carbon (マットカーボン × エメラルドグリーン)")]
        StealthCarbon = 3,

        [InspectorName("カスタム (Custom)")]
        Custom = 4
    }

    [Header("プリセット ＆ テーマ")]
    [SerializeField] private SpirePreset _preset = SpirePreset.QuadSpireArray;
    [SerializeField] private EmitterConfig _emitterConfig = EmitterConfig.Quad_4;
    [SerializeField] private ColorTheme _colorTheme = ColorTheme.CyberCyan;

    [Header("台座寸法（m）")]
    [Range(1.0f, 8.0f)]
    [Tooltip("架台ベースの半径（メートル）")]
    [SerializeField] private float _baseRadius = 3.0f;

    [Range(0.3f, 3.0f)]
    [Tooltip("架台ベースの高さ・厚み（メートル）")]
    [SerializeField] private float _baseHeight = 1.0f;

    [Header("プロジェクションビーム（m）")]
    [Tooltip("上空への照射ライトコーンを表示するか")]
    [SerializeField] private bool _showBeam = true;

    [Range(4.0f, 40.0f)]
    [Tooltip("ホログラム投影焦点の高さ（ビーム到達高度）")]
    [SerializeField] private float _beamHeight = 15.0f;

    [Range(1.0f, 15.0f)]
    [Tooltip("上空でのビーム広がり半径（メートル）")]
    [SerializeField] private float _beamTopRadius = 6.0f;

    [Range(0.2f, 5.0f)]
    [Tooltip("ビームの光量・輝度マルチプライヤー")]
    [SerializeField] private float _beamIntensity = 1.8f;

    [Header("オプション")]
    [Tooltip("台座の当たり判定（MeshCollider / BoxCollider）を有効にするか")]
    [SerializeField] private bool _enableCollider = false;

    [Header("コンポーネント参照")]
    [SerializeField] private Renderer _pedestalBaseRenderer;
    [SerializeField] private Renderer _emitterHeadsRenderer;
    [SerializeField] private Renderer _projectorCoreRenderer;
    [SerializeField] private Renderer _beamConeRenderer;
    [SerializeField] private Transform _hologramAnchor;
    [SerializeField] private BoxCollider _baseCollider;

    [Header("カスタムマテリアル（指定時は最優先）")]
    [SerializeField] private Material _customBaseMaterial;
    [SerializeField] private Material _customBeamMaterial;

    // MaterialPropertyBlockキャッシュ
    private MaterialPropertyBlock _mpbBase;
    private MaterialPropertyBlock _mpbBeam;

    // シェーダープロパティIDキャッシュ
    private static readonly int BaseColorId       = Shader.PropertyToID("_BaseColor");
    private static readonly int AccentColorId     = Shader.PropertyToID("_AccentColor");
    private static readonly int VentColorId       = Shader.PropertyToID("_VentColor");
    private static readonly int LedColorId        = Shader.PropertyToID("_LedColor");
    private static readonly int BeamColorId       = Shader.PropertyToID("_BeamColor");
    private static readonly int BeamIntensityId   = Shader.PropertyToID("_BeamIntensity");
    private static readonly int MetallicId        = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId      = Shader.PropertyToID("_Smoothness");

    // ランタイム動的メッシュ
    private Mesh _runtimeMeshBase;
    private Mesh _runtimeMeshHeads;
    private Mesh _runtimeMeshCore;
    private Mesh _runtimeMeshBeam;

    // パラメータ変更検知用キャッシュ
    private float _lastBaseRadius = -1f;
    private float _lastBaseHeight = -1f;
    private float _lastBeamHeight = -1f;
    private float _lastBeamTopRadius = -1f;
    private EmitterConfig _lastEmitterConfig = (EmitterConfig)(-1);

    public SpirePreset Preset => _preset;
    public EmitterConfig Configuration => _emitterConfig;
    public ColorTheme Theme => _colorTheme;
    public float BaseRadius => _baseRadius;
    public float BaseHeight => _baseHeight;
    public float BeamHeight => _beamHeight;
    public float BeamTopRadius => _beamTopRadius;
    public Transform HologramAnchor => _hologramAnchor;

    private void Awake()
    {
        UpdateSpire();
    }

    private void OnEnable()
    {
        UpdateSpire();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMeshes();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        FindChildrenIfNeeded();
        ApplyCollider();
        ApplyMaterialProperties();

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
            {
                UpdateSpire();
            }
        };
    }
#endif

    private void CleanupRuntimeMeshes()
    {
        SafeDestroyMesh(ref _runtimeMeshBase);
        SafeDestroyMesh(ref _runtimeMeshHeads);
        SafeDestroyMesh(ref _runtimeMeshCore);
        SafeDestroyMesh(ref _runtimeMeshBeam);
    }

    private void SafeDestroyMesh(ref Mesh m)
    {
        if (m != null)
        {
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }
    }

    public void UpdateSpire()
    {
        EnsureHierarchy();
        EnsureMeshes();
        ApplyVisibility();
        ApplyCollider();
        ApplyMaterialProperties();
        ApplyAnchorPosition();
    }

    public void FindChildrenIfNeeded()
    {
        if (_pedestalBaseRenderer == null)
        {
            Transform t = transform.Find("PedestalBase");
            if (t != null) _pedestalBaseRenderer = t.GetComponent<Renderer>();
        }
        if (_emitterHeadsRenderer == null)
        {
            Transform t = transform.Find("EmitterHeads");
            if (t != null) _emitterHeadsRenderer = t.GetComponent<Renderer>();
        }
        if (_projectorCoreRenderer == null)
        {
            Transform t = transform.Find("ProjectorCore");
            if (t != null) _projectorCoreRenderer = t.GetComponent<Renderer>();
        }
        if (_beamConeRenderer == null)
        {
            Transform t = transform.Find("BeamCone");
            if (t != null) _beamConeRenderer = t.GetComponent<Renderer>();
        }
        if (_hologramAnchor == null)
        {
            _hologramAnchor = transform.Find("HologramAnchor");
        }
    }

    public void EnsureHierarchy()
    {
        FindChildrenIfNeeded();

        CreateChildIfMissing("PedestalBase", ref _pedestalBaseRenderer, false);
        CreateChildIfMissing("EmitterHeads", ref _emitterHeadsRenderer, false);
        CreateChildIfMissing("ProjectorCore", ref _projectorCoreRenderer, false);
        CreateChildIfMissing("BeamCone", ref _beamConeRenderer, true);

        if (_hologramAnchor == null)
        {
            GameObject anchor = new GameObject("HologramAnchor");
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = new Vector3(0, _beamHeight, 0);
            _hologramAnchor = anchor.transform;
        }

        if (_baseCollider == null)
        {
            _baseCollider = GetComponent<BoxCollider>();
            if (_baseCollider == null && _enableCollider)
            {
                _baseCollider = gameObject.AddComponent<BoxCollider>();
            }
        }
    }

    private void CreateChildIfMissing(string childName, ref Renderer rendererRef, bool isBeam)
    {
        if (rendererRef != null) return;
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            child = go.transform;
        }

        MeshFilter mf = child.GetComponent<MeshFilter>();
        if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = child.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = child.gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = isBeam ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = !isBeam;
        }
        rendererRef = mr;
    }

    private void EnsureMeshes()
    {
        bool dimsChanged = !Mathf.Approximately(_lastBaseRadius, _baseRadius) ||
                           !Mathf.Approximately(_lastBaseHeight, _baseHeight) ||
                           !Mathf.Approximately(_lastBeamHeight, _beamHeight) ||
                           !Mathf.Approximately(_lastBeamTopRadius, _beamTopRadius) ||
                           _lastEmitterConfig != _emitterConfig;

        if (dimsChanged)
        {
            CleanupRuntimeMeshes();
            _lastBaseRadius = _baseRadius;
            _lastBaseHeight = _baseHeight;
            _lastBeamHeight = _beamHeight;
            _lastBeamTopRadius = _beamTopRadius;
            _lastEmitterConfig = _emitterConfig;
        }

        // 1. Pedestal Base
        if (_pedestalBaseRenderer != null)
        {
            MeshFilter mf = _pedestalBaseRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshBase == null)
                {
                    _runtimeMeshBase = CreatePedestalBaseMesh(_baseRadius, _baseHeight);
                    _runtimeMeshBase.name = "Runtime_PedestalBase";
                }
                mf.sharedMesh = _runtimeMeshBase;
            }
        }

        // 2. Emitter Heads
        if (_emitterHeadsRenderer != null)
        {
            MeshFilter mf = _emitterHeadsRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshHeads == null)
                {
                    _runtimeMeshHeads = CreateEmitterHeadsMesh(_baseRadius, _baseHeight, (int)_emitterConfig, _beamHeight);
                    _runtimeMeshHeads.name = "Runtime_EmitterHeads";
                }
                mf.sharedMesh = _runtimeMeshHeads;
            }
        }

        // 3. Projector Core
        if (_projectorCoreRenderer != null)
        {
            MeshFilter mf = _projectorCoreRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshCore == null)
                {
                    _runtimeMeshCore = CreateProjectorCoreMesh(_baseRadius, _baseHeight);
                    _runtimeMeshCore.name = "Runtime_ProjectorCore";
                }
                mf.sharedMesh = _runtimeMeshCore;
            }
        }

        // 4. Beam Cone
        if (_beamConeRenderer != null)
        {
            MeshFilter mf = _beamConeRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshBeam == null)
                {
                    _runtimeMeshBeam = CreateBeamConeMesh(_baseRadius * 0.72f, _beamTopRadius, _beamHeight, _baseHeight);
                    _runtimeMeshBeam.name = "Runtime_BeamCone";
                }
                mf.sharedMesh = _runtimeMeshBeam;
            }
        }
    }

    private void ApplyVisibility()
    {
        if (_beamConeRenderer != null)
            _beamConeRenderer.gameObject.SetActive(_showBeam);
    }

    private void ApplyCollider()
    {
        if (_baseCollider != null)
        {
            _baseCollider.enabled = _enableCollider;
            _baseCollider.size = new Vector3(_baseRadius * 2.0f, _baseHeight, _baseRadius * 2.0f);
            _baseCollider.center = new Vector3(0, _baseHeight * 0.5f, 0);
        }
    }

    private void ApplyAnchorPosition()
    {
        if (_hologramAnchor != null)
        {
            _hologramAnchor.localPosition = new Vector3(0, _beamHeight, 0);
        }
    }

    public void ApplyMaterialProperties()
    {
        if (_mpbBase == null) _mpbBase = new MaterialPropertyBlock();
        if (_mpbBeam == null) _mpbBeam = new MaterialPropertyBlock();

        Color baseAlloy, accentTrim, ventColor, ledColor, beamColor;
        float metallic, smoothness;

        switch (_colorTheme)
        {
            case ColorTheme.IndustrialAmber:
                baseAlloy = new Color(0.85f, 0.55f, 0.1f, 1.0f);
                accentTrim = new Color(0.14f, 0.14f, 0.16f, 1.0f);
                ventColor = new Color(1.0f, 0.65f, 0.1f, 1.0f);
                ledColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                beamColor = new Color(1.0f, 0.75f, 0.15f, 1.0f);
                metallic = 0.55f;
                smoothness = 0.55f;
                break;

            case ColorTheme.CleanWhite:
                baseAlloy = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                accentTrim = new Color(0.20f, 0.24f, 0.30f, 1.0f);
                ventColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);
                ledColor = new Color(0.5f, 0.95f, 1.0f, 1.0f);
                beamColor = new Color(0.6f, 0.92f, 1.0f, 1.0f);
                metallic = 0.40f;
                smoothness = 0.75f;
                break;

            case ColorTheme.StealthCarbon:
                baseAlloy = new Color(0.10f, 0.11f, 0.13f, 1.0f);
                accentTrim = new Color(0.25f, 0.26f, 0.28f, 1.0f);
                ventColor = new Color(0.0f, 1.0f, 0.5f, 1.0f);
                ledColor = new Color(0.2f, 1.0f, 0.6f, 1.0f);
                beamColor = new Color(0.1f, 1.0f, 0.55f, 1.0f);
                metallic = 0.85f;
                smoothness = 0.85f;
                break;

            case ColorTheme.CyberCyan:
            default:
                baseAlloy = new Color(0.18f, 0.22f, 0.28f, 1.0f);
                accentTrim = new Color(0.85f, 0.90f, 0.95f, 1.0f);
                ventColor = new Color(0.0f, 0.85f, 1.0f, 1.0f);
                ledColor = new Color(0.0f, 1.0f, 0.9f, 1.0f);
                beamColor = new Color(0.0f, 0.88f, 1.0f, 1.0f);
                metallic = 0.80f;
                smoothness = 0.70f;
                break;
        }

        // Apply Base & Machinery
        if (_pedestalBaseRenderer != null)
        {
            _pedestalBaseRenderer.GetPropertyBlock(_mpbBase);
            _mpbBase.SetColor(BaseColorId, baseAlloy);
            _mpbBase.SetColor(AccentColorId, accentTrim);
            _mpbBase.SetColor(VentColorId, ventColor);
            _mpbBase.SetColor(LedColorId, ledColor);
            _mpbBase.SetFloat(MetallicId, metallic);
            _mpbBase.SetFloat(SmoothnessId, smoothness);
            _pedestalBaseRenderer.SetPropertyBlock(_mpbBase);
        }

        if (_emitterHeadsRenderer != null)
        {
            _emitterHeadsRenderer.SetPropertyBlock(_mpbBase);
        }
        if (_projectorCoreRenderer != null)
        {
            _projectorCoreRenderer.SetPropertyBlock(_mpbBase);
        }

        // Apply Beam Cone
        if (_beamConeRenderer != null)
        {
            _beamConeRenderer.GetPropertyBlock(_mpbBeam);
            _mpbBeam.SetColor(BeamColorId, beamColor);
            _mpbBeam.SetFloat(BeamIntensityId, _beamIntensity);
            _beamConeRenderer.SetPropertyBlock(_mpbBeam);
        }
    }

    public void ApplyPreset(SpirePreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case SpirePreset.QuadSpireArray:
                _emitterConfig = EmitterConfig.Quad_4;
                _baseRadius = 3.0f;
                _baseHeight = 1.0f;
                _beamHeight = 15.0f;
                _beamTopRadius = 6.0f;
                _beamIntensity = 1.8f;
                _colorTheme = ColorTheme.CyberCyan;
                break;

            case SpirePreset.HeavyTripodPlatform:
                _emitterConfig = EmitterConfig.Tripod_3;
                _baseRadius = 4.0f;
                _baseHeight = 1.4f;
                _beamHeight = 20.0f;
                _beamTopRadius = 8.0f;
                _beamIntensity = 2.2f;
                _colorTheme = ColorTheme.IndustrialAmber;
                break;

            case SpirePreset.RooftopGrandArray:
                _emitterConfig = EmitterConfig.Hexa_6;
                _baseRadius = 5.0f;
                _baseHeight = 1.2f;
                _beamHeight = 25.0f;
                _beamTopRadius = 10.0f;
                _beamIntensity = 2.0f;
                _colorTheme = ColorTheme.CleanWhite;
                break;

            case SpirePreset.CompactPedestal:
                _emitterConfig = EmitterConfig.Quad_4;
                _baseRadius = 1.6f;
                _baseHeight = 0.6f;
                _beamHeight = 8.0f;
                _beamTopRadius = 3.5f;
                _beamIntensity = 1.5f;
                _colorTheme = ColorTheme.StealthCarbon;
                break;
        }

        CleanupRuntimeMeshes();
        UpdateSpire();
    }

    public void ApplyTheme(ColorTheme theme)
    {
        _colorTheme = theme;
        ApplyMaterialProperties();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3D Procedural Mesh Generators
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 重厚な八角形機械台座（Pedestal Base）メッシュ生成
    /// </summary>
    public static Mesh CreatePedestalBaseMesh(float radius, float height, int sides = 8)
    {
        Mesh mesh = new Mesh();
        mesh.name = "PedestalBaseMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // 2-tier stepped polygon platform
        float rBottom = radius * 1.08f;
        float rMid    = radius;
        float rTop    = radius * 0.92f;
        float yBottom = 0.0f;
        float yStep   = height * 0.35f;
        float yTop    = height;

        // Sides: Step 1 (Bottom to Step)
        AddPolygonCylinderSide(verts, norms, uvs, tris, yBottom, yStep, rBottom, rMid, sides);
        // Step ledge
        AddPolygonAnnulus(verts, norms, uvs, tris, yStep, rMid, rTop, sides);
        // Sides: Step 2 (Step to Top)
        AddPolygonCylinderSide(verts, norms, uvs, tris, yStep, yTop, rTop, rTop * 0.95f, sides);
        // Top face
        AddPolygonCap(verts, norms, uvs, tris, yTop, rTop * 0.95f, sides, true);

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 傾斜照射ヘッド（Emitter Heads）アレイメッシュ生成
    /// </summary>
    public static Mesh CreateEmitterHeadsMesh(float baseRadius, float baseHeight, int count, float targetHeight)
    {
        Mesh mesh = new Mesh();
        mesh.name = "EmitterHeadsMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float emitterPlacementR = baseRadius * 0.72f;
        float headR = baseRadius * 0.16f;
        float headLen = headR * 2.2f;

        Vector3 targetFocus = new Vector3(0, targetHeight, 0);

        for (int i = 0; i < count; i++)
        {
            float angle = (i * (360.0f / count)) * Mathf.Deg2Rad;
            Vector3 basePos = new Vector3(Mathf.Cos(angle) * emitterPlacementR, baseHeight, Mathf.Sin(angle) * emitterPlacementR);

            // Vector pointing towards projection focus
            Vector3 aimDir = (targetFocus - basePos).normalized;
            Quaternion rot = Quaternion.LookRotation(aimDir, Vector3.up);

            // Add cylindrical turret nozzle pointing in aimDir
            AddAngledCylinder(verts, norms, uvs, tris, basePos, rot, headR, headLen, 8);
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 中央集光光学クリスタル・一次発振ドーム（Projector Core）メッシュ生成
    /// </summary>
    public static Mesh CreateProjectorCoreMesh(float baseRadius, float baseHeight)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProjectorCoreMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float coreR = baseRadius * 0.32f;
        float coreH = baseHeight * 0.45f;
        int segments = 12;

        // Core central cylinder turret
        for (int s = 0; s <= segments; s++)
        {
            float u = (float)s / segments;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            verts.Add(new Vector3(cos * coreR, baseHeight, sin * coreR));
            norms.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 0));

            verts.Add(new Vector3(cos * (coreR * 0.7f), baseHeight + coreH, sin * (coreR * 0.7f)));
            norms.Add(new Vector3(cos, 0.4f, sin).normalized);
            uvs.Add(new Vector2(u, 1));
        }

        for (int s = 0; s < segments; s++)
        {
            int c = s * 2;
            tris.Add(c);
            tris.Add(c + 1);
            tris.Add(c + 2);

            tris.Add(c + 1);
            tris.Add(c + 3);
            tris.Add(c + 2);
        }

        // Top emitter condenser lens cap
        AddPolygonCap(verts, norms, uvs, tris, baseHeight + coreH, coreR * 0.7f, segments, true);

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 上空へ広がる半透明集光ビームコーン（Beam Cone）メッシュ生成
    /// </summary>
    public static Mesh CreateBeamConeMesh(float bottomRadius, float topRadius, float height, float baseHeight, int segments = 32)
    {
        Mesh mesh = new Mesh();
        mesh.name = "BeamConeMesh";

        int ringVerts = segments + 1;
        int verticalSlices = 8;
        int totalVerts = ringVerts * (verticalSlices + 1);

        Vector3[] vertices = new Vector3[totalVerts];
        Vector3[] normals  = new Vector3[totalVerts];
        Vector2[] uvs      = new Vector2[totalVerts];
        List<int> triangles = new List<int>();

        for (int y = 0; y <= verticalSlices; y++)
        {
            float v = (float)y / verticalSlices;
            float currentY = Mathf.Lerp(baseHeight, height, v);
            float currentR = Mathf.Lerp(bottomRadius, topRadius, v);

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int index = y * ringVerts + s;
                vertices[index] = new Vector3(cos * currentR, currentY, sin * currentR);
                normals[index]  = new Vector3(cos, 0, sin);
                uvs[index]      = new Vector2(u, v);
            }
        }

        for (int y = 0; y < verticalSlices; y++)
        {
            for (int s = 0; s < segments; s++)
            {
                int curr = y * ringVerts + s;
                int next = curr + ringVerts;

                // Standard single-sided faces (Shader Cull Off provides double-sided additive rendering without polygon doubling)
                triangles.Add(curr);
                triangles.Add(next);
                triangles.Add(curr + 1);

                triangles.Add(curr + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.vertices  = vertices;
        mesh.normals   = normals;
        mesh.uv        = uvs;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddPolygonCylinderSide(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        float yBottom, float yTop, float rBottom, float rTop, int sides)
    {
        int start = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float u = (float)s / sides;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            verts.Add(new Vector3(cos * rBottom, yBottom, sin * rBottom));
            norms.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 0));

            verts.Add(new Vector3(cos * rTop, yTop, sin * rTop));
            norms.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 1));
        }

        for (int s = 0; s < sides; s++)
        {
            int c = start + s * 2;
            tris.Add(c);
            tris.Add(c + 1);
            tris.Add(c + 2);

            tris.Add(c + 1);
            tris.Add(c + 3);
            tris.Add(c + 2);
        }
    }

    private static void AddPolygonAnnulus(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        float y, float rOuter, float rInner, int sides)
    {
        int start = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float u = (float)s / sides;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            verts.Add(new Vector3(cos * rOuter, y, sin * rOuter));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(u, 1));

            verts.Add(new Vector3(cos * rInner, y, sin * rInner));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(u, 0));
        }

        for (int s = 0; s < sides; s++)
        {
            int c = start + s * 2;
            // Clockwise looking down from +Y: inner(s) -> inner(s+1) -> outer(s+1) and inner(s) -> outer(s+1) -> outer(s)
            tris.Add(c + 1);
            tris.Add(c + 3);
            tris.Add(c + 2);

            tris.Add(c + 1);
            tris.Add(c + 2);
            tris.Add(c);
        }
    }

    private static void AddPolygonCap(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        float y, float radius, int sides, bool isTop)
    {
        int centerIdx = verts.Count;
        verts.Add(new Vector3(0, y, 0));
        norms.Add(isTop ? Vector3.up : Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int startRing = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float angle = ((float)s / sides) * Mathf.PI * 2.0f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            verts.Add(new Vector3(x, y, z));
            norms.Add(isTop ? Vector3.up : Vector3.down);
            uvs.Add(new Vector2(x / (radius * 2.0f) + 0.5f, z / (radius * 2.0f) + 0.5f));
        }

        for (int s = 0; s < sides; s++)
        {
            if (isTop)
            {
                // Clockwise from above (+Y)
                tris.Add(centerIdx);
                tris.Add(startRing + s + 1);
                tris.Add(startRing + s);
            }
            else
            {
                // Clockwise from below (-Y)
                tris.Add(centerIdx);
                tris.Add(startRing + s);
                tris.Add(startRing + s + 1);
            }
        }
    }

    private static void AddAngledCylinder(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 origin, Quaternion rot, float radius, float length, int sides)
    {
        int start = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float u = (float)s / sides;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle) * radius;
            float sin = Mathf.Sin(angle) * radius;

            Vector3 localBottom = new Vector3(cos, sin, 0);
            Vector3 localTop    = new Vector3(cos, sin, length);

            Vector3 worldBottom = origin + rot * localBottom;
            Vector3 worldTop    = origin + rot * localTop;
            Vector3 worldNorm   = rot * new Vector3(cos / radius, sin / radius, 0);

            verts.Add(worldBottom);
            norms.Add(worldNorm);
            uvs.Add(new Vector2(u, 0));

            verts.Add(worldTop);
            norms.Add(worldNorm);
            uvs.Add(new Vector2(u, 1));
        }

        for (int s = 0; s < sides; s++)
        {
            int c = start + s * 2;
            // Clockwise from outside: c -> c+2 -> c+1 and c+1 -> c+2 -> c+3
            tris.Add(c);
            tris.Add(c + 2);
            tris.Add(c + 1);

            tris.Add(c + 1);
            tris.Add(c + 2);
            tris.Add(c + 3);
        }

        // Front nozzle lens cap (facing +Z in local space)
        int capCenter = verts.Count;
        Vector3 worldCenterTop = origin + rot * new Vector3(0, 0, length);
        Vector3 worldForward = rot * Vector3.forward;
        verts.Add(worldCenterTop);
        norms.Add(worldForward);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int ringStart = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float u = (float)s / sides;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle) * radius;
            float sin = Mathf.Sin(angle) * radius;
            Vector3 worldP = origin + rot * new Vector3(cos, sin, length);
            verts.Add(worldP);
            norms.Add(worldForward);
            uvs.Add(new Vector2(cos / (radius * 2.0f) + 0.5f, sin / (radius * 2.0f) + 0.5f));
        }

        for (int s = 0; s < sides; s++)
        {
            tris.Add(capCenter);
            tris.Add(ringStart + s);
            tris.Add(ringStart + s + 1);
        }
    }
}
