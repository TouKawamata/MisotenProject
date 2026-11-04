using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 反重力バイオガーデンドーム（Floating Sky-Biome Dome）制御コンポーネント。
/// ・100年後の超高層ビル群の合間や超上空に浮遊する自立型閉鎖生態系（空中バイオーム）。
/// ・ジオデシック多面体ガラスドーム、反重力リパルサー環、階層型テラス庭園、中心環境制御コアで構成。
/// ・MaterialPropertyBlock によるDrawCallバッチング維持。
/// ・Update()は浮遊ホバーアニメーション有効時のみ微小な位置・回転更新（無効時はCPU負荷ゼロ）。
/// ・コライダーはデフォルトOFF（背景スレスレ通過時の安全設計）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FloatingSkyBiomeController : MonoBehaviour
{
    public enum BiomePreset
    {
        [InspectorName("🌿 スカイバイオーム標準ドーム (10m / Lush Tropical)")]
        StandardSkyDome = 0,

        [InspectorName("🪐 メガバイオーム球体 (22m / Cyber Bioluminescent)")]
        MegaBiomeSphere = 1,

        [InspectorName("🏔️ 高山テラス・ピナクル (14m / Alpine Terrace)")]
        AlpineTerracePinnacle = 2,

        [InspectorName("🌸 展望ミニポッド (6m / Sakura Arboretum)")]
        ObservationMiniPod = 3,

        [InspectorName("⚙️ カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum DomeStyle
    {
        [InspectorName("半球ドーム型 (Upper Hemisphere)")]
        UpperHemisphere = 0,

        [InspectorName("完全球体浮遊型 (Full Sphere)")]
        FullSphere = 1,

        [InspectorName("吊り下げポッド型 (Suspended Pod)")]
        SuspendedPod = 2
    }

    public enum EcoBiomeType
    {
        [InspectorName("Lush Tropical (熱帯雨林 × エメラルド芝 × シアンコア)")]
        LushTropical = 0,

        [InspectorName("Cyber Bioluminescent (発光胞子 × ネオンバイオレット × 発光植物)")]
        CyberBioluminescent = 1,

        [InspectorName("Alpine Terrace (高山岩石段丘 × 針葉樹 × クリーングリーン)")]
        AlpineTerrace = 2,

        [InspectorName("Sakura Arboretum (桜色キャノピー × 枯山水テラス × 黄金光)")]
        SakuraArboretum = 3,

        [InspectorName("カスタム (Custom)")]
        Custom = 4
    }

    [Header("プリセット ＆ スタイル")]
    [SerializeField] private BiomePreset _preset = BiomePreset.StandardSkyDome;
    [SerializeField] private DomeStyle _style = DomeStyle.UpperHemisphere;
    [SerializeField] private EcoBiomeType _biomeType = EcoBiomeType.LushTropical;

    [Header("寸法パラメーター（m）")]
    [Range(4.0f, 30.0f)]
    [Tooltip("ドームの半径（メートル）")]
    [SerializeField] private float _domeRadius = 10.0f;

    [Header("反重力浮遊アニメーション")]
    [Tooltip("Playモードおよびエディタプレビューでゆっくりとした浮遊ホバーを行うか")]
    [SerializeField] private bool _enableFloatingBob = true;

    [Range(0.05f, 1.0f)]
    [Tooltip("上下浮遊の振幅（m）")]
    [SerializeField] private float _floatAmplitude = 0.25f;

    [Range(2.0f, 10.0f)]
    [Tooltip("上下浮遊の周期（秒）")]
    [SerializeField] private float _floatPeriod = 4.5f;

    [Range(-10.0f, 10.0f)]
    [Tooltip("反重力リングの緩やかな自転速度（deg/s）")]
    [SerializeField] private float _ringSpinSpeed = 2.5f;

    [Header("オプション")]
    [Tooltip("内部の階層型テラス庭園を表示するか")]
    [SerializeField] private bool _showTerracedGarden = true;

    [Tooltip("中心環境制御コアおよび樹木群を表示するか")]
    [SerializeField] private bool _showBioSpire = true;

    [Tooltip("ドーム全体の当たり判定（SphereCollider）を有効にするか。背景配置時はOFF推奨")]
    [SerializeField] private bool _enableCollider = false;

    [Header("コンポーネント参照")]
    [SerializeField] private Renderer _geodesicDomeRenderer;
    [SerializeField] private Renderer _antiGravRingRenderer;
    [SerializeField] private Renderer _terracedGardenRenderer;
    [SerializeField] private Renderer _bioSpireRenderer;
    [SerializeField] private Transform _antiGravRingTransform;
    [SerializeField] private SphereCollider _sphereCollider;

    [Header("カスタムマテリアル（指定時は最優先）")]
    [SerializeField] private Material _customDomeGlassMaterial;
    [SerializeField] private Material _customAntiGravMaterial;
    [SerializeField] private Material _customFoliageMaterial;

    // MaterialPropertyBlockキャッシュ
    private MaterialPropertyBlock _mpbGlass;
    private MaterialPropertyBlock _mpbAntiGrav;
    private MaterialPropertyBlock _mpbFoliage;

    // シェーダープロパティIDキャッシュ
    private static readonly int BaseColorId       = Shader.PropertyToID("_BaseColor");
    private static readonly int AccentColorId     = Shader.PropertyToID("_AccentColor");
    private static readonly int FresnelColorId    = Shader.PropertyToID("_FresnelColor");
    private static readonly int TipColorId        = Shader.PropertyToID("_TipColor");
    private static readonly int SubsurfaceColorId = Shader.PropertyToID("_SubsurfaceColor");
    private static readonly int RepulsorColorId   = Shader.PropertyToID("_RepulsorColor");
    private static readonly int BioGlowColorId    = Shader.PropertyToID("_BioGlowColor");
    private static readonly int MetallicId        = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId      = Shader.PropertyToID("_Smoothness");

    // ランタイム動的メッシュ
    private Mesh _runtimeMeshDome;
    private Mesh _runtimeMeshRing;
    private Mesh _runtimeMeshGarden;
    private Mesh _runtimeMeshSpire;

    // パラメータ変更検知用キャッシュ
    private float _lastRadius = -1f;
    private DomeStyle _lastStyle = (DomeStyle)(-1);

    // 浮遊アニメーションの基準位置
    private Vector3 _initialLocalPosition;
    private bool _hasInitialPosition = false;

    public BiomePreset Preset => _preset;
    public DomeStyle Style => _style;
    public EcoBiomeType BiomeType => _biomeType;
    public float DomeRadius => _domeRadius;

    private void Awake()
    {
        CacheInitialPosition();
        UpdateBiome();
    }

    private void OnEnable()
    {
        CacheInitialPosition();
        UpdateBiome();
    }

    private void CacheInitialPosition()
    {
        if (!_hasInitialPosition || !Application.isPlaying)
        {
            _initialLocalPosition = transform.localPosition;
            _hasInitialPosition = true;
        }
    }

    private void Update()
    {
        if (_enableFloatingBob)
        {
            if (Application.isPlaying)
            {
                CacheInitialPosition();
                float time = Time.time;

                // Gentle floating oscillation (PlayMode only)
                float bobY = Mathf.Sin((time * Mathf.PI * 2.0f) / _floatPeriod) * _floatAmplitude;
                transform.localPosition = _initialLocalPosition + new Vector3(0, bobY, 0);

                // Slow ring rotation
                if (_antiGravRingTransform != null && Mathf.Abs(_ringSpinSpeed) > 0.01f)
                {
                    _antiGravRingTransform.localRotation = Quaternion.Euler(0, time * _ringSpinSpeed, 0);
                }
            }
            else
            {
                // In Edit Mode, keep base position in sync so scene view dragging works smoothly
                _initialLocalPosition = transform.localPosition;
            }
        }
    }

    private double EditorApplicationTime()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
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
                UpdateBiome();
            }
        };
    }
#endif

    private void CleanupRuntimeMeshes()
    {
        SafeDestroyMesh(ref _runtimeMeshDome);
        SafeDestroyMesh(ref _runtimeMeshRing);
        SafeDestroyMesh(ref _runtimeMeshGarden);
        SafeDestroyMesh(ref _runtimeMeshSpire);
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

    public void UpdateBiome()
    {
        EnsureHierarchy();
        EnsureMeshes();
        ApplyVisibility();
        ApplyCollider();
        ApplyMaterialProperties();
    }

    public void FindChildrenIfNeeded()
    {
        if (_geodesicDomeRenderer == null)
        {
            Transform t = transform.Find("GeodesicDome");
            if (t != null) _geodesicDomeRenderer = t.GetComponent<Renderer>();
        }
        if (_antiGravRingRenderer == null)
        {
            Transform t = transform.Find("AntiGravRing");
            if (t != null)
            {
                _antiGravRingRenderer = t.GetComponent<Renderer>();
                _antiGravRingTransform = t;
            }
        }
        if (_terracedGardenRenderer == null)
        {
            Transform t = transform.Find("TerracedGarden");
            if (t != null) _terracedGardenRenderer = t.GetComponent<Renderer>();
        }
        if (_bioSpireRenderer == null)
        {
            Transform t = transform.Find("BioSpire");
            if (t != null) _bioSpireRenderer = t.GetComponent<Renderer>();
        }
    }

    public void EnsureHierarchy()
    {
        FindChildrenIfNeeded();

        CreateChildIfMissing("GeodesicDome", ref _geodesicDomeRenderer, true);
        CreateChildIfMissing("AntiGravRing", ref _antiGravRingRenderer, false);
        CreateChildIfMissing("TerracedGarden", ref _terracedGardenRenderer, false);
        CreateChildIfMissing("BioSpire", ref _bioSpireRenderer, false);

        if (_antiGravRingRenderer != null)
        {
            _antiGravRingTransform = _antiGravRingRenderer.transform;
        }

        if (_sphereCollider == null)
        {
            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider == null && _enableCollider)
            {
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
        }
    }

    private void CreateChildIfMissing(string childName, ref Renderer rendererRef, bool isGlass)
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
            mr.shadowCastingMode = isGlass ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        rendererRef = mr;
    }

    private void EnsureMeshes()
    {
        bool dimsChanged = !Mathf.Approximately(_lastRadius, _domeRadius) || _lastStyle != _style;
        if (dimsChanged)
        {
            CleanupRuntimeMeshes();
            _lastRadius = _domeRadius;
            _lastStyle = _style;
        }

        // 1. Geodesic Dome Glass
        if (_geodesicDomeRenderer != null)
        {
            MeshFilter mf = _geodesicDomeRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshDome == null)
                {
                    _runtimeMeshDome = CreateGeodesicDomeMesh(_domeRadius, _style);
                    _runtimeMeshDome.name = "Runtime_GeodesicDome";
                }
                mf.sharedMesh = _runtimeMeshDome;
            }
        }

        // 2. Anti-Grav Ring
        if (_antiGravRingRenderer != null)
        {
            MeshFilter mf = _antiGravRingRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshRing == null)
                {
                    _runtimeMeshRing = CreateAntiGravRingMesh(_domeRadius, _style);
                    _runtimeMeshRing.name = "Runtime_AntiGravRing";
                }
                mf.sharedMesh = _runtimeMeshRing;
            }
        }

        // 3. Terraced Garden
        if (_terracedGardenRenderer != null)
        {
            MeshFilter mf = _terracedGardenRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshGarden == null)
                {
                    _runtimeMeshGarden = CreateTerracedGardenMesh(_domeRadius, _style);
                    _runtimeMeshGarden.name = "Runtime_TerracedGarden";
                }
                mf.sharedMesh = _runtimeMeshGarden;
            }
        }

        // 4. Bio Spire
        if (_bioSpireRenderer != null)
        {
            MeshFilter mf = _bioSpireRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshSpire == null)
                {
                    _runtimeMeshSpire = CreateBioSpireMesh(_domeRadius, _domeRadius * 1.2f, _style);
                    _runtimeMeshSpire.name = "Runtime_BioSpire";
                }
                mf.sharedMesh = _runtimeMeshSpire;
            }
        }
    }

    private void ApplyVisibility()
    {
        if (_terracedGardenRenderer != null)
            _terracedGardenRenderer.gameObject.SetActive(_showTerracedGarden);

        if (_bioSpireRenderer != null)
            _bioSpireRenderer.gameObject.SetActive(_showBioSpire);
    }

    private void ApplyCollider()
    {
        if (_sphereCollider != null)
        {
            _sphereCollider.enabled = _enableCollider;
            _sphereCollider.radius = _domeRadius * 1.05f;
            _sphereCollider.center = (_style == DomeStyle.UpperHemisphere) ? new Vector3(0, _domeRadius * 0.5f, 0) : Vector3.zero;
        }
    }

    public void ApplyMaterialProperties()
    {
        if (_mpbGlass == null) _mpbGlass = new MaterialPropertyBlock();
        if (_mpbAntiGrav == null) _mpbAntiGrav = new MaterialPropertyBlock();
        if (_mpbFoliage == null) _mpbFoliage = new MaterialPropertyBlock();

        Color glassTint, glassFresnel, ringBase, ringAccent, repulsorColor;
        Color foliageBase, foliageTip, foliageSSS, bioGlow;

        switch (_biomeType)
        {
            case EcoBiomeType.CyberBioluminescent:
                glassTint = new Color(0.35f, 0.15f, 0.45f, 0.25f);
                glassFresnel = new Color(0.0f, 0.95f, 1.0f, 0.9f);
                ringBase = new Color(0.12f, 0.14f, 0.18f, 1.0f);
                ringAccent = new Color(0.25f, 0.30f, 0.40f, 1.0f);
                repulsorColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);
                foliageBase = new Color(0.25f, 0.12f, 0.45f, 1.0f);
                foliageTip = new Color(0.1f, 0.75f, 0.85f, 1.0f);
                foliageSSS = new Color(0.6f, 0.2f, 0.9f, 1.0f);
                bioGlow = new Color(0.0f, 1.0f, 0.9f, 1.0f);
                break;

            case EcoBiomeType.AlpineTerrace:
                glassTint = new Color(0.90f, 0.96f, 1.0f, 0.20f);
                glassFresnel = new Color(0.70f, 0.90f, 1.0f, 0.85f);
                ringBase = new Color(0.22f, 0.24f, 0.26f, 1.0f);
                ringAccent = new Color(0.85f, 0.88f, 0.92f, 1.0f);
                repulsorColor = new Color(0.2f, 0.75f, 1.0f, 1.0f);
                foliageBase = new Color(0.12f, 0.35f, 0.20f, 1.0f);
                foliageTip = new Color(0.35f, 0.65f, 0.38f, 1.0f);
                foliageSSS = new Color(0.6f, 0.85f, 0.3f, 1.0f);
                bioGlow = new Color(0.4f, 0.9f, 0.6f, 0.5f);
                break;

            case EcoBiomeType.SakuraArboretum:
                glassTint = new Color(1.0f, 0.92f, 0.95f, 0.22f);
                glassFresnel = new Color(1.0f, 0.75f, 0.85f, 0.85f);
                ringBase = new Color(0.18f, 0.16f, 0.18f, 1.0f);
                ringAccent = new Color(0.92f, 0.88f, 0.90f, 1.0f);
                repulsorColor = new Color(1.0f, 0.6f, 0.8f, 1.0f);
                foliageBase = new Color(0.65f, 0.25f, 0.40f, 1.0f);
                foliageTip = new Color(0.98f, 0.65f, 0.78f, 1.0f);
                foliageSSS = new Color(1.0f, 0.85f, 0.90f, 1.0f);
                bioGlow = new Color(1.0f, 0.75f, 0.4f, 1.0f);
                break;

            case EcoBiomeType.LushTropical:
            default:
                glassTint = new Color(0.85f, 0.96f, 1.0f, 0.22f);
                glassFresnel = new Color(0.75f, 0.95f, 1.0f, 0.85f);
                ringBase = new Color(0.16f, 0.20f, 0.26f, 1.0f);
                ringAccent = new Color(0.88f, 0.92f, 0.96f, 1.0f);
                repulsorColor = new Color(0.0f, 0.85f, 1.0f, 1.0f);
                foliageBase = new Color(0.16f, 0.52f, 0.20f, 1.0f);
                foliageTip = new Color(0.40f, 0.85f, 0.32f, 1.0f);
                foliageSSS = new Color(0.75f, 0.95f, 0.25f, 1.0f);
                bioGlow = new Color(0.1f, 0.95f, 0.8f, 0.8f);
                break;
        }

        // Apply Glass
        if (_geodesicDomeRenderer != null)
        {
            _geodesicDomeRenderer.GetPropertyBlock(_mpbGlass);
            _mpbGlass.SetColor(BaseColorId, glassTint);
            _mpbGlass.SetColor(FresnelColorId, glassFresnel);
            _geodesicDomeRenderer.SetPropertyBlock(_mpbGlass);
        }

        // Apply Anti-Grav
        if (_antiGravRingRenderer != null)
        {
            _antiGravRingRenderer.GetPropertyBlock(_mpbAntiGrav);
            _mpbAntiGrav.SetColor(BaseColorId, ringBase);
            _mpbAntiGrav.SetColor(AccentColorId, ringAccent);
            _mpbAntiGrav.SetColor(RepulsorColorId, repulsorColor);
            _antiGravRingRenderer.SetPropertyBlock(_mpbAntiGrav);
        }

        // Apply Foliage (Garden + Spire)
        if (_terracedGardenRenderer != null)
        {
            _terracedGardenRenderer.GetPropertyBlock(_mpbFoliage);
            _mpbFoliage.SetColor(BaseColorId, foliageBase);
            _mpbFoliage.SetColor(TipColorId, foliageTip);
            _mpbFoliage.SetColor(SubsurfaceColorId, foliageSSS);
            _mpbFoliage.SetColor(BioGlowColorId, bioGlow);
            _terracedGardenRenderer.SetPropertyBlock(_mpbFoliage);
        }

        if (_bioSpireRenderer != null)
        {
            _bioSpireRenderer.SetPropertyBlock(_mpbFoliage);
        }
    }

    public void ApplyPreset(BiomePreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case BiomePreset.StandardSkyDome:
                _domeRadius = 10.0f;
                _style = DomeStyle.UpperHemisphere;
                _biomeType = EcoBiomeType.LushTropical;
                _floatAmplitude = 0.25f;
                _floatPeriod = 4.5f;
                break;

            case BiomePreset.MegaBiomeSphere:
                _domeRadius = 22.0f;
                _style = DomeStyle.FullSphere;
                _biomeType = EcoBiomeType.CyberBioluminescent;
                _floatAmplitude = 0.45f;
                _floatPeriod = 6.5f;
                break;

            case BiomePreset.AlpineTerracePinnacle:
                _domeRadius = 14.0f;
                _style = DomeStyle.SuspendedPod;
                _biomeType = EcoBiomeType.AlpineTerrace;
                _floatAmplitude = 0.35f;
                _floatPeriod = 5.0f;
                break;

            case BiomePreset.ObservationMiniPod:
                _domeRadius = 6.0f;
                _style = DomeStyle.UpperHemisphere;
                _biomeType = EcoBiomeType.SakuraArboretum;
                _floatAmplitude = 0.18f;
                _floatPeriod = 3.8f;
                break;
        }

        CleanupRuntimeMeshes();
        UpdateBiome();
    }

    public void ApplyTheme(EcoBiomeType theme)
    {
        _biomeType = theme;
        ApplyMaterialProperties();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3D Procedural Mesh Generators
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ジオデシック多面体（Geodesic Sphere / Hemisphere）ガラスメッシュ生成
    /// </summary>
    public static Mesh CreateGeodesicDomeMesh(float radius, DomeStyle style, int latSegments = 16, int lonSegments = 24)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GeodesicDomeMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        int latStart = 0;
        int latEnd = (style == DomeStyle.FullSphere) ? latSegments : latSegments / 2;

        for (int lat = 0; lat <= latEnd; lat++)
        {
            float v = (float)lat / latSegments;
            float phi = v * Mathf.PI; // 0 at top pole, PI at bottom
            float y = Mathf.Cos(phi) * radius;
            float ringR = Mathf.Sin(phi) * radius;

            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float u = (float)lon / lonSegments;
                float theta = u * Mathf.PI * 2.0f;

                float x = Mathf.Sin(theta) * ringR;
                float z = Mathf.Cos(theta) * ringR;

                Vector3 pos = new Vector3(x, y, z);
                verts.Add(pos);
                norms.Add(pos.normalized);
                uvs.Add(new Vector2(u, v));
            }
        }

        int lonCount = lonSegments + 1;
        for (int lat = 0; lat < latEnd; lat++)
        {
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int curr = lat * lonCount + lon;
                int next = curr + lonCount;

                // Clockwise from outside
                tris.Add(curr);
                tris.Add(next);
                tris.Add(curr + 1);

                tris.Add(curr + 1);
                tris.Add(next);
                tris.Add(next + 1);
            }
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 反重力リパルサー環（重力制御リング・スタビライザー翼）メッシュ生成
    /// </summary>
    public static Mesh CreateAntiGravRingMesh(float radius, DomeStyle style, int segments = 32)
    {
        Mesh mesh = new Mesh();
        mesh.name = "AntiGravRingMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float ringMajorR = radius * 1.06f;
        float ringMinorR = radius * 0.08f;
        float ringY      = (style == DomeStyle.SuspendedPod) ? radius * 0.8f : 0.0f;
        int minorSegments = 8;

        // Torus generation
        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            float majorAngle = u * Mathf.PI * 2.0f;
            Vector3 centerOnRing = new Vector3(Mathf.Cos(majorAngle) * ringMajorR, ringY, Mathf.Sin(majorAngle) * ringMajorR);
            Vector3 radialDir = new Vector3(Mathf.Cos(majorAngle), 0, Mathf.Sin(majorAngle));

            for (int j = 0; j <= minorSegments; j++)
            {
                float v = (float)j / minorSegments;
                float minorAngle = v * Mathf.PI * 2.0f;

                Vector3 offset = (radialDir * Mathf.Cos(minorAngle) + Vector3.up * Mathf.Sin(minorAngle)) * ringMinorR;
                Vector3 pos = centerOnRing + offset;
                Vector3 norm = offset.normalized;

                verts.Add(pos);
                norms.Add(norm);
                uvs.Add(new Vector2(u * 4.0f, v));
            }
        }

        int minorCount = minorSegments + 1;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                int curr = i * minorCount + j;
                int next = curr + minorCount;

                // Clockwise from outside: curr -> curr+1 -> next
                tris.Add(curr);
                tris.Add(curr + 1);
                tris.Add(next);

                tris.Add(curr + 1);
                tris.Add(next + 1);
                tris.Add(next);
            }
        }

        // Add 4 stabilizer thruster fins (two-sided)
        for (int f = 0; f < 4; f++)
        {
            float finAngle = (f * 90.0f) * Mathf.Deg2Rad;
            Vector3 finDir = new Vector3(Mathf.Cos(finAngle), 0, Mathf.Sin(finAngle));
            Vector3 cross = Vector3.Cross(finDir, Vector3.up);

            Vector3 finRoot = finDir * (ringMajorR + ringMinorR);
            Vector3 finTip  = finDir * (ringMajorR + ringMinorR * 3.5f);

            // Fin quad
            int s = verts.Count;
            verts.Add(finRoot - cross * 0.1f - Vector3.up * 0.1f);
            verts.Add(finRoot - cross * 0.1f + Vector3.up * 0.1f);
            verts.Add(finTip  + Vector3.up * 0.05f);
            verts.Add(finTip  - Vector3.up * 0.05f);

            norms.Add(cross); norms.Add(cross); norms.Add(cross); norms.Add(cross);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));

            // Front side
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            // Back side
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 階層型テラス庭園（同心円状の緑地・遊歩道プラットフォーム）メッシュ生成
    /// </summary>
    public static Mesh CreateTerracedGardenMesh(float radius, DomeStyle style, int segments = 24)
    {
        Mesh mesh = new Mesh();
        mesh.name = "TerracedGardenMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // 3 concentric terraces (Tier 1: outer, Tier 2: mid, Tier 3: inner plaza)
        float[] tierRadii   = new float[] { radius * 0.92f, radius * 0.65f, radius * 0.35f };
        float[] tierHeights = new float[] { 0.15f,          0.65f,          1.20f };

        for (int t = 0; t < 3; t++)
        {
            float outerR = tierRadii[t];
            float innerR = (t == 2) ? 0.0f : tierRadii[t + 1];
            float y = tierHeights[t];

            int startIdx = verts.Count;

            // Annulus surface
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Outer edge vertex
                verts.Add(new Vector3(cos * outerR, y, sin * outerR));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(u, (float)(t + 1) / 3.0f));

                // Inner edge vertex
                verts.Add(new Vector3(cos * innerR, y, sin * innerR));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(u, (float)t / 3.0f));
            }

            for (int s = 0; s < segments; s++)
            {
                int c = startIdx + s * 2;
                // Clockwise looking down from +Y: inner(s) -> inner(s+1) -> outer(s+1) and inner(s) -> outer(s+1) -> outer(s)
                tris.Add(c + 1);
                tris.Add(c + 3);
                tris.Add(c + 2);

                tris.Add(c + 1);
                tris.Add(c + 2);
                tris.Add(c);
            }
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 中心気候制御スパイヤおよび樹木キャノピークラスタのメッシュ生成
    /// </summary>
    public static Mesh CreateBioSpireMesh(float radius, float height, DomeStyle style)
    {
        Mesh mesh = new Mesh();
        mesh.name = "BioSpireMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // 1. Central tapered spire column
        int segments = 12;
        float baseR = radius * 0.12f;
        float topR  = radius * 0.03f;
        float spireY = (style == DomeStyle.UpperHemisphere) ? height * 0.75f : height * 0.5f;

        int startIdx = verts.Count;
        for (int s = 0; s <= segments; s++)
        {
            float u = (float)s / segments;
            float angle = u * Mathf.PI * 2.0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            verts.Add(new Vector3(cos * baseR, 0, sin * baseR));
            norms.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 0));

            verts.Add(new Vector3(cos * topR, spireY, sin * topR));
            norms.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 1));
        }

        for (int s = 0; s < segments; s++)
        {
            int c = startIdx + s * 2;
            tris.Add(c);
            tris.Add(c + 1);
            tris.Add(c + 2);

            tris.Add(c + 1);
            tris.Add(c + 3);
            tris.Add(c + 2);
        }

        // 2. Add lush foliage canopy clusters (stylized organic foliage domes sprouting around spire)
        int canopyCount = 5;
        for (int c = 0; c < canopyCount; c++)
        {
            float angle = (c * (360.0f / canopyCount) + 25.0f) * Mathf.Deg2Rad;
            float dist = radius * 0.38f;
            float cY   = 0.8f + (c % 2) * (radius * 0.15f);
            float cR   = radius * 0.18f;

            Vector3 canopyCenter = new Vector3(Mathf.Cos(angle) * dist, cY, Mathf.Sin(angle) * dist);
            AddLowPolySphere(verts, norms, uvs, tris, canopyCenter, cR, 8, 6);
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddLowPolySphere(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 center, float r, int lonSegs, int latSegs)
    {
        int start = verts.Count;
        for (int lat = 0; lat <= latSegs; lat++)
        {
            float v = (float)lat / latSegs;
            float phi = v * Mathf.PI;
            float y = Mathf.Cos(phi) * r;
            float ringR = Mathf.Sin(phi) * r;

            for (int lon = 0; lon <= lonSegs; lon++)
            {
                float u = (float)lon / lonSegs;
                float theta = u * Mathf.PI * 2.0f;
                float x = Mathf.Sin(theta) * ringR;
                float z = Mathf.Cos(theta) * ringR;

                Vector3 pos = center + new Vector3(x, y, z);
                verts.Add(pos);
                norms.Add((pos - center).normalized);
                uvs.Add(new Vector2(u, v));
            }
        }

        int lonCount = lonSegs + 1;
        for (int lat = 0; lat < latSegs; lat++)
        {
            for (int lon = 0; lon < lonSegs; lon++)
            {
                int curr = start + lat * lonCount + lon;
                int next = curr + lonCount;

                // Clockwise from outside
                tris.Add(curr);
                tris.Add(next);
                tris.Add(curr + 1);

                tris.Add(curr + 1);
                tris.Add(next);
                tris.Add(next + 1);
            }
        }
    }
}
