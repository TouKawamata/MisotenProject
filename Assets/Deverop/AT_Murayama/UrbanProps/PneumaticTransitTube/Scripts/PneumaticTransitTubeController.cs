using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 垂直空気圧エレベーターチューブ＆旅客カプセル（Pneumatic Transit Tube & Capsule）制御コンポーネント。
/// ・100年後の超高層ビル外壁に設置された透明真空/空気圧チューブ輸送システム。
/// ・透明ガラスチューブ、等間隔の補強リング、壁面支持ブラケット、端部エアロック端末、内部昇降カプセルで構成。
/// ・ビル外壁の取付面（Z=0）またはチューブ中心を基準にスナップ可能。
/// ・MaterialPropertyBlock によるDrawCallバッチング維持。
/// ・Update()はカプセル移動アニメーション時のみ軽量位置更新（静止時はCPU負荷ゼロ）。
/// ・コライダーはデフォルトOFF（背景スレスレ通過時の安全設計）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PneumaticTransitTubeController : MonoBehaviour
{
    public enum TubePreset
    {
        [InspectorName("🚀 スカイスクレイパー・エクスプレス (80m / Cyber Cyan)")]
        SkyscraperExpress = 0,

        [InspectorName("🏢 標準ビル外壁トランジット (35m / Clean White)")]
        StandardTransit = 1,

        [InspectorName("🌆 展望シースルーピラー (45m / Corporate Black)")]
        ObservationPillar = 2,

        [InspectorName("🏗️ 重工業貨物チューブ (50m / Industrial Amber)")]
        IndustrialFreight = 3,

        [InspectorName("⚙️ カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum ColorTheme
    {
        [InspectorName("Clean White (白亜建築 × サテンフレーム)")]
        CleanWhite = 0,

        [InspectorName("Cyber Cyan (ネオンシアン × ダークグラファイト)")]
        CyberCyan = 1,

        [InspectorName("Industrial Amber (セーフティアンバー × 重防食鋼)")]
        IndustrialAmber = 2,

        [InspectorName("Corporate Black (ブロンズスモーク × チタンブラック)")]
        CorporateBlack = 3,

        [InspectorName("カスタム (Custom)")]
        Custom = 4
    }

    [Header("プリセット ＆ テーマ")]
    [SerializeField] private TubePreset _preset = TubePreset.StandardTransit;
    [SerializeField] private ColorTheme _colorTheme = ColorTheme.CleanWhite;

    [Header("チューブ寸法（m）")]
    [Range(15.0f, 120.0f)]
    [Tooltip("チューブの全高（メートル）")]
    [SerializeField] private float _tubeHeight = 35.0f;

    [Range(0.8f, 3.5f)]
    [Tooltip("チューブの半径（メートル）")]
    [SerializeField] private float _tubeRadius = 1.5f;

    [Range(3.0f, 15.0f)]
    [Tooltip("補強リングの間隔（メートル）")]
    [SerializeField] private float _ringSpacing = 6.0f;

    [Range(0.5f, 6.0f)]
    [Tooltip("壁面マウントの奥行き（チューブ中心から壁面までの距離）")]
    [SerializeField] private float _wallBracketDepth = 2.2f;

    [Header("カプセルポッド制御")]
    [Tooltip("Playモードおよびエディタプレビューでカプセルを自動昇降させるか")]
    [SerializeField] private bool _animateCapsule = true;

    [Range(3.0f, 40.0f)]
    [Tooltip("カプセルの昇降速度（m/s）")]
    [SerializeField] private float _capsuleSpeed = 15.0f;

    [Range(0.0f, 1.0f)]
    [Tooltip("カプセルの手動位置（0.0=最下部、1.0=最上部。アニメーションOFF時に有効）")]
    [SerializeField] private float _capsuleProgress = 0.35f;

    [Header("オプション")]
    [Tooltip("壁面支持ブラケットを表示するか（自立型にする場合はOFF）")]
    [SerializeField] private bool _showWallMounts = true;

    [Tooltip("上下端部のエアロック端末ドームを表示するか")]
    [SerializeField] private bool _showEndTerminals = true;

    [Tooltip("チューブの当たり判定（CapsuleCollider）を有効にするか。背景配置時はOFF推奨")]
    [SerializeField] private bool _enableCollider = false;

    [Header("コンポーネント参照")]
    [SerializeField] private Renderer _glassTubeRenderer;
    [SerializeField] private Renderer _structuralRingsRenderer;
    [SerializeField] private Renderer _wallMountsRenderer;
    [SerializeField] private Renderer _endTerminalsRenderer;
    [SerializeField] private Renderer _capsulePodRenderer;
    [SerializeField] private Transform _capsuleTransform;
    [SerializeField] private CapsuleCollider _tubeCollider;

    [Header("カスタムマテリアル（指定時は最優先）")]
    [SerializeField] private Material _customGlassMaterial;
    [SerializeField] private Material _customStructureMaterial;
    [SerializeField] private Material _customCapsuleMaterial;

    // MaterialPropertyBlockキャッシュ
    private MaterialPropertyBlock _mpbGlass;
    private MaterialPropertyBlock _mpbStructure;
    private MaterialPropertyBlock _mpbCapsule;

    // シェーダープロパティIDキャッシュ
    private static readonly int BaseColorId       = Shader.PropertyToID("_BaseColor");
    private static readonly int AccentColorId     = Shader.PropertyToID("_AccentColor");
    private static readonly int FresnelColorId    = Shader.PropertyToID("_FresnelColor");
    private static readonly int RailColorId       = Shader.PropertyToID("_RailColor");
    private static readonly int EmissionColorId   = Shader.PropertyToID("_EmissionColor");
    private static readonly int ThrusterColorId   = Shader.PropertyToID("_ThrusterColor");
    private static readonly int SmoothnessId      = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicId        = Shader.PropertyToID("_Metallic");

    // ランタイム動的メッシュ
    private Mesh _runtimeMeshGlass;
    private Mesh _runtimeMeshRings;
    private Mesh _runtimeMeshMounts;
    private Mesh _runtimeMeshTerminals;
    private Mesh _runtimeMeshCapsule;

    // パラメータ変更検知用キャッシュ
    private float _lastHeight = -1f;
    private float _lastRadius = -1f;
    private float _lastSpacing = -1f;
    private float _lastBracketDepth = -1f;

    // アニメーション用変数
    private float _currentProgress = 0.0f;
    private float _travelDirection = 1.0f;

    public TubePreset Preset => _preset;
    public ColorTheme Theme => _colorTheme;
    public float TubeHeight => _tubeHeight;
    public float TubeRadius => _tubeRadius;
    public float RingSpacing => _ringSpacing;
    public float WallBracketDepth => _wallBracketDepth;
    public bool AnimateCapsule => _animateCapsule;
    public float CapsuleSpeed => _capsuleSpeed;

    private void Awake()
    {
        UpdateTube();
    }

    private void OnEnable()
    {
        UpdateTube();
    }

    private void Update()
    {
        if (_animateCapsule)
        {
            float availableTravel = Mathf.Max(2.0f, _tubeHeight - _tubeRadius * 4.0f);
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            _currentProgress += (_capsuleSpeed / availableTravel) * dt * _travelDirection;
            if (_currentProgress >= 1.0f)
            {
                _currentProgress = 1.0f;
                _travelDirection = -1.0f;
            }
            else if (_currentProgress <= 0.0f)
            {
                _currentProgress = 0.0f;
                _travelDirection = 1.0f;
            }

            ApplyCapsulePosition(_currentProgress);
        }
        else
        {
            ApplyCapsulePosition(_capsuleProgress);
        }
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
                UpdateTube();
            }
        };
    }
#endif

    private void CleanupRuntimeMeshes()
    {
        SafeDestroyMesh(ref _runtimeMeshGlass);
        SafeDestroyMesh(ref _runtimeMeshRings);
        SafeDestroyMesh(ref _runtimeMeshMounts);
        SafeDestroyMesh(ref _runtimeMeshTerminals);
        SafeDestroyMesh(ref _runtimeMeshCapsule);
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

    public void UpdateTube()
    {
        EnsureHierarchy();
        EnsureMeshes();
        ApplyVisibility();
        ApplyCollider();
        ApplyMaterialProperties();
        ApplyCapsulePosition(_animateCapsule ? _currentProgress : _capsuleProgress);
    }

    public void FindChildrenIfNeeded()
    {
        if (_glassTubeRenderer == null)
        {
            Transform t = transform.Find("GlassTube");
            if (t != null) _glassTubeRenderer = t.GetComponent<Renderer>();
        }
        if (_structuralRingsRenderer == null)
        {
            Transform t = transform.Find("StructuralRings");
            if (t != null) _structuralRingsRenderer = t.GetComponent<Renderer>();
        }
        if (_wallMountsRenderer == null)
        {
            Transform t = transform.Find("WallMounts");
            if (t != null) _wallMountsRenderer = t.GetComponent<Renderer>();
        }
        if (_endTerminalsRenderer == null)
        {
            Transform t = transform.Find("EndTerminals");
            if (t != null) _endTerminalsRenderer = t.GetComponent<Renderer>();
        }
        if (_capsulePodRenderer == null)
        {
            Transform t = transform.Find("CapsulePod");
            if (t != null)
            {
                _capsulePodRenderer = t.GetComponent<Renderer>();
                _capsuleTransform = t;
            }
        }
    }

    public void EnsureHierarchy()
    {
        FindChildrenIfNeeded();

        CreateChildIfMissing("GlassTube", ref _glassTubeRenderer, "S_PneumaticTubeGlass");
        CreateChildIfMissing("StructuralRings", ref _structuralRingsRenderer, "S_PneumaticTubeStructure");
        CreateChildIfMissing("WallMounts", ref _wallMountsRenderer, "S_PneumaticTubeStructure");
        CreateChildIfMissing("EndTerminals", ref _endTerminalsRenderer, "S_PneumaticTubeStructure");

        if (_capsulePodRenderer == null)
        {
            Transform cap = transform.Find("CapsulePod");
            if (cap == null)
            {
                GameObject go = new GameObject("CapsulePod");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, _tubeHeight * 0.35f, 0);
                cap = go.transform;
            }
            _capsuleTransform = cap;

            MeshFilter mf = cap.GetComponent<MeshFilter>();
            if (mf == null) mf = cap.gameObject.AddComponent<MeshFilter>();

            MeshRenderer mr = cap.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = cap.gameObject.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            _capsulePodRenderer = mr;
        }

        if (_tubeCollider == null)
        {
            _tubeCollider = GetComponent<CapsuleCollider>();
            if (_tubeCollider == null && _enableCollider)
            {
                _tubeCollider = gameObject.AddComponent<CapsuleCollider>();
            }
        }
    }

    private void CreateChildIfMissing(string childName, ref Renderer rendererRef, string shaderName)
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
            mr.shadowCastingMode = (childName == "GlassTube") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        rendererRef = mr;
    }

    private void EnsureMeshes()
    {
        bool dimsChanged = !Mathf.Approximately(_lastHeight, _tubeHeight) ||
                           !Mathf.Approximately(_lastRadius, _tubeRadius) ||
                           !Mathf.Approximately(_lastSpacing, _ringSpacing) ||
                           !Mathf.Approximately(_lastBracketDepth, _wallBracketDepth);

        if (dimsChanged)
        {
            CleanupRuntimeMeshes();
            _lastHeight = _tubeHeight;
            _lastRadius = _tubeRadius;
            _lastSpacing = _ringSpacing;
            _lastBracketDepth = _wallBracketDepth;
        }

        // 1. Glass Tube
        if (_glassTubeRenderer != null)
        {
            MeshFilter mf = _glassTubeRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshGlass == null)
                {
                    _runtimeMeshGlass = CreateTubeGlassMesh(_tubeHeight, _tubeRadius);
                    _runtimeMeshGlass.name = "Runtime_TubeGlass";
                }
                mf.sharedMesh = _runtimeMeshGlass;
            }
        }

        // 2. Structural Rings
        if (_structuralRingsRenderer != null)
        {
            MeshFilter mf = _structuralRingsRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshRings == null)
                {
                    _runtimeMeshRings = CreateStructuralRingsMesh(_tubeHeight, _tubeRadius, _ringSpacing);
                    _runtimeMeshRings.name = "Runtime_StructuralRings";
                }
                mf.sharedMesh = _runtimeMeshRings;
            }
        }

        // 3. Wall Mounts
        if (_wallMountsRenderer != null)
        {
            MeshFilter mf = _wallMountsRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshMounts == null)
                {
                    _runtimeMeshMounts = CreateWallMountsMesh(_tubeHeight, _tubeRadius, _ringSpacing, _wallBracketDepth);
                    _runtimeMeshMounts.name = "Runtime_WallMounts";
                }
                mf.sharedMesh = _runtimeMeshMounts;
            }
        }

        // 4. End Terminals
        if (_endTerminalsRenderer != null)
        {
            MeshFilter mf = _endTerminalsRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshTerminals == null)
                {
                    _runtimeMeshTerminals = CreateEndTerminalsMesh(_tubeHeight, _tubeRadius);
                    _runtimeMeshTerminals.name = "Runtime_EndTerminals";
                }
                mf.sharedMesh = _runtimeMeshTerminals;
            }
        }

        // 5. Capsule Pod
        if (_capsulePodRenderer != null)
        {
            MeshFilter mf = _capsulePodRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (_runtimeMeshCapsule == null)
                {
                    _runtimeMeshCapsule = CreateCapsulePodMesh(_tubeRadius);
                    _runtimeMeshCapsule.name = "Runtime_CapsulePod";
                }
                mf.sharedMesh = _runtimeMeshCapsule;
            }
        }
    }

    private void ApplyVisibility()
    {
        if (_wallMountsRenderer != null)
            _wallMountsRenderer.gameObject.SetActive(_showWallMounts);

        if (_endTerminalsRenderer != null)
            _endTerminalsRenderer.gameObject.SetActive(_showEndTerminals);
    }

    private void ApplyCollider()
    {
        if (_tubeCollider != null)
        {
            _tubeCollider.enabled = _enableCollider;
            _tubeCollider.direction = 1; // Y-axis
            _tubeCollider.radius = _tubeRadius * 1.1f;
            _tubeCollider.height = _tubeHeight;
            _tubeCollider.center = new Vector3(0, _tubeHeight * 0.5f, 0);
        }
    }

    private void ApplyCapsulePosition(float progress)
    {
        if (_capsuleTransform != null)
        {
            // Smooth ease in/out for elevator travel
            float smoothProgress = Mathf.SmoothStep(0.0f, 1.0f, progress);
            float bottomLimit = _tubeRadius * 2.2f;
            float topLimit = _tubeHeight - _tubeRadius * 2.2f;
            float currentY = Mathf.Lerp(bottomLimit, topLimit, smoothProgress);

            _capsuleTransform.localPosition = new Vector3(0, currentY, 0);

            // Capsule orientation: facing up when going up, facing down when descending (optional subtle tilt)
            _capsuleTransform.localRotation = Quaternion.identity;
        }
    }

    public void ApplyMaterialProperties()
    {
        if (_mpbGlass == null) _mpbGlass = new MaterialPropertyBlock();
        if (_mpbStructure == null) _mpbStructure = new MaterialPropertyBlock();
        if (_mpbCapsule == null) _mpbCapsule = new MaterialPropertyBlock();

        Color glassTint, glassFresnel, structBase, structAccent, ledColor, capsuleBase, thrusterColor;
        float metallic, smoothness;

        switch (_colorTheme)
        {
            case ColorTheme.CyberCyan:
                glassTint = new Color(0.2f, 0.85f, 1.0f, 0.28f);
                glassFresnel = new Color(0.0f, 0.95f, 1.0f, 0.9f);
                structBase = new Color(0.18f, 0.22f, 0.28f, 1.0f);
                structAccent = new Color(0.08f, 0.10f, 0.14f, 1.0f);
                ledColor = new Color(0.0f, 1.0f, 1.0f, 1.0f);
                capsuleBase = new Color(0.92f, 0.96f, 1.0f, 1.0f);
                thrusterColor = new Color(0.0f, 0.9f, 1.0f, 1.0f);
                metallic = 0.75f;
                smoothness = 0.8f;
                break;

            case ColorTheme.IndustrialAmber:
                glassTint = new Color(1.0f, 0.85f, 0.6f, 0.32f);
                glassFresnel = new Color(1.0f, 0.75f, 0.2f, 0.85f);
                structBase = new Color(0.85f, 0.55f, 0.1f, 1.0f);
                structAccent = new Color(0.14f, 0.14f, 0.16f, 1.0f);
                ledColor = new Color(1.0f, 0.8f, 0.1f, 1.0f);
                capsuleBase = new Color(0.88f, 0.82f, 0.75f, 1.0f);
                thrusterColor = new Color(1.0f, 0.65f, 0.1f, 1.0f);
                metallic = 0.5f;
                smoothness = 0.6f;
                break;

            case ColorTheme.CorporateBlack:
                glassTint = new Color(0.35f, 0.38f, 0.45f, 0.35f);
                glassFresnel = new Color(0.9f, 0.85f, 0.7f, 0.75f);
                structBase = new Color(0.12f, 0.13f, 0.15f, 1.0f);
                structAccent = new Color(0.05f, 0.05f, 0.06f, 1.0f);
                ledColor = new Color(1.0f, 0.9f, 0.6f, 1.0f);
                capsuleBase = new Color(0.15f, 0.16f, 0.18f, 1.0f);
                thrusterColor = new Color(1.0f, 0.85f, 0.5f, 1.0f);
                metallic = 0.9f;
                smoothness = 0.9f;
                break;

            case ColorTheme.CleanWhite:
            default:
                glassTint = new Color(0.92f, 0.97f, 1.0f, 0.22f);
                glassFresnel = new Color(0.75f, 0.92f, 1.0f, 0.8f);
                structBase = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                structAccent = new Color(0.20f, 0.22f, 0.26f, 1.0f);
                ledColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
                capsuleBase = new Color(0.96f, 0.97f, 0.99f, 1.0f);
                thrusterColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);
                metallic = 0.4f;
                smoothness = 0.7f;
                break;
        }

        // Apply Glass
        if (_glassTubeRenderer != null)
        {
            _glassTubeRenderer.GetPropertyBlock(_mpbGlass);
            _mpbGlass.SetColor(BaseColorId, glassTint);
            _mpbGlass.SetColor(FresnelColorId, glassFresnel);
            _mpbGlass.SetColor(RailColorId, ledColor);
            _glassTubeRenderer.SetPropertyBlock(_mpbGlass);
        }

        // Apply Structure (Rings, Mounts, Terminals)
        if (_structuralRingsRenderer != null)
        {
            _structuralRingsRenderer.GetPropertyBlock(_mpbStructure);
            _mpbStructure.SetColor(BaseColorId, structBase);
            _mpbStructure.SetColor(AccentColorId, structAccent);
            _mpbStructure.SetColor(EmissionColorId, ledColor);
            _mpbStructure.SetFloat(MetallicId, metallic);
            _mpbStructure.SetFloat(SmoothnessId, smoothness);
            _structuralRingsRenderer.SetPropertyBlock(_mpbStructure);
        }

        if (_wallMountsRenderer != null)
        {
            _wallMountsRenderer.SetPropertyBlock(_mpbStructure);
        }
        if (_endTerminalsRenderer != null)
        {
            _endTerminalsRenderer.SetPropertyBlock(_mpbStructure);
        }

        // Apply Capsule
        if (_capsulePodRenderer != null)
        {
            _capsulePodRenderer.GetPropertyBlock(_mpbCapsule);
            _mpbCapsule.SetColor(BaseColorId, capsuleBase);
            _mpbCapsule.SetColor(AccentColorId, structAccent);
            _mpbCapsule.SetColor(ThrusterColorId, thrusterColor);
            _mpbCapsule.SetFloat(MetallicId, metallic);
            _mpbCapsule.SetFloat(SmoothnessId, smoothness);
            _capsulePodRenderer.SetPropertyBlock(_mpbCapsule);
        }
    }

    public void ApplyPreset(TubePreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case TubePreset.SkyscraperExpress:
                _tubeHeight = 80.0f;
                _tubeRadius = 1.8f;
                _ringSpacing = 10.0f;
                _wallBracketDepth = 2.6f;
                _capsuleSpeed = 25.0f;
                _colorTheme = ColorTheme.CyberCyan;
                break;

            case TubePreset.StandardTransit:
                _tubeHeight = 35.0f;
                _tubeRadius = 1.5f;
                _ringSpacing = 6.0f;
                _wallBracketDepth = 2.2f;
                _capsuleSpeed = 15.0f;
                _colorTheme = ColorTheme.CleanWhite;
                break;

            case TubePreset.ObservationPillar:
                _tubeHeight = 45.0f;
                _tubeRadius = 2.2f;
                _ringSpacing = 8.0f;
                _wallBracketDepth = 3.2f;
                _capsuleSpeed = 10.0f;
                _colorTheme = ColorTheme.CorporateBlack;
                break;

            case TubePreset.IndustrialFreight:
                _tubeHeight = 50.0f;
                _tubeRadius = 2.5f;
                _ringSpacing = 5.0f;
                _wallBracketDepth = 3.6f;
                _capsuleSpeed = 12.0f;
                _colorTheme = ColorTheme.IndustrialAmber;
                break;
        }

        CleanupRuntimeMeshes();
        UpdateTube();
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
    /// 透明ガラスチューブの円筒メッシュ生成
    /// </summary>
    public static Mesh CreateTubeGlassMesh(float height, float radius, int segments = 24)
    {
        Mesh mesh = new Mesh();
        mesh.name = "TubeGlassMesh";

        int ringVerts = segments + 1;
        int verticalSlices = Mathf.Max(2, Mathf.RoundToInt(height / 4.0f));
        int totalVerts = ringVerts * (verticalSlices + 1);

        Vector3[] vertices = new Vector3[totalVerts];
        Vector3[] normals  = new Vector3[totalVerts];
        Vector2[] uvs      = new Vector2[totalVerts];
        List<int> triangles = new List<int>();

        for (int y = 0; y <= verticalSlices; y++)
        {
            float v = (float)y / verticalSlices;
            float currentY = v * height;

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;

                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                int index = y * ringVerts + s;
                vertices[index] = new Vector3(x, currentY, z);
                normals[index]  = new Vector3(x / radius, 0, z / radius);
                uvs[index]      = new Vector2(u, v);
            }
        }

        for (int y = 0; y < verticalSlices; y++)
        {
            for (int s = 0; s < segments; s++)
            {
                int curr = y * ringVerts + s;
                int next = curr + ringVerts;

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

    /// <summary>
    /// 等間隔の補強リング（カラー・カラーフランジ）メッシュ生成
    /// </summary>
    public static Mesh CreateStructuralRingsMesh(float height, float radius, float ringSpacing, int segments = 24)
    {
        Mesh mesh = new Mesh();
        mesh.name = "StructuralRingsMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float ringThickness = 0.35f;
        float outerRadius   = radius * 1.14f;

        int ringCount = Mathf.FloorToInt(height / ringSpacing);
        for (int r = 1; r <= ringCount; r++)
        {
            float ringY = r * ringSpacing;
            if (ringY > height - 0.5f) break;

            float yBot = ringY - ringThickness * 0.5f;
            float yTop = ringY + ringThickness * 0.5f;

            // 1. Outer cylindrical band
            int outerStart = verts.Count;
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float xOut = cos * outerRadius;
                float zOut = sin * outerRadius;

                verts.Add(new Vector3(xOut, yBot, zOut));
                norms.Add(new Vector3(cos, 0, sin));
                uvs.Add(new Vector2(u, 0));

                verts.Add(new Vector3(xOut, yTop, zOut));
                norms.Add(new Vector3(cos, 0, sin));
                uvs.Add(new Vector2(u, 1));
            }

            for (int s = 0; s < segments; s++)
            {
                int c = outerStart + s * 2;
                tris.Add(c);
                tris.Add(c + 1);
                tris.Add(c + 2);

                tris.Add(c + 1);
                tris.Add(c + 3);
                tris.Add(c + 2);
            }

            // 2. Top annular cap (facing +Y)
            int topStart = verts.Count;
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts.Add(new Vector3(cos * radius, yTop, sin * radius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(u, 0));

                verts.Add(new Vector3(cos * outerRadius, yTop, sin * outerRadius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(u, 1));
            }

            for (int s = 0; s < segments; s++)
            {
                int c = topStart + s * 2;
                tris.Add(c);
                tris.Add(c + 2);
                tris.Add(c + 3);

                tris.Add(c);
                tris.Add(c + 3);
                tris.Add(c + 1);
            }

            // 3. Bottom annular cap (facing -Y)
            int botStart = verts.Count;
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts.Add(new Vector3(cos * radius, yBot, sin * radius));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(u, 0));

                verts.Add(new Vector3(cos * outerRadius, yBot, sin * outerRadius));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(u, 1));
            }

            for (int s = 0; s < segments; s++)
            {
                int c = botStart + s * 2;
                tris.Add(c);
                tris.Add(c + 3);
                tris.Add(c + 2);

                tris.Add(c);
                tris.Add(c + 1);
                tris.Add(c + 3);
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
    /// チューブをビル外壁に固定する支持ブラケット（カンチレバー金具）メッシュ生成
    /// </summary>
    public static Mesh CreateWallMountsMesh(float height, float radius, float ringSpacing, float depth)
    {
        Mesh mesh = new Mesh();
        mesh.name = "WallMountsMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        int ringCount = Mathf.FloorToInt(height / ringSpacing);
        float armWidth = radius * 0.4f;
        float armHeight = 0.28f;

        for (int r = 1; r <= ringCount; r++)
        {
            float ringY = r * ringSpacing;
            if (ringY > height - 0.5f) break;

            // Box beam stretching from z = 0 (tube rear) to z = -depth (building wall)
            float zFront = -radius;
            float zBack  = -depth;
            float yMin   = ringY - armHeight * 0.5f;
            float yMax   = ringY + armHeight * 0.5f;
            float xMin   = -armWidth * 0.5f;
            float xMax   =  armWidth * 0.5f;

            AddBoxGeometry(verts, norms, uvs, tris,
                new Vector3(xMin, yMin, zBack),
                new Vector3(xMax, yMax, zFront));

            // Wall flange mounting plate at z = -depth
            float plateMargin = radius * 0.35f;
            AddBoxGeometry(verts, norms, uvs, tris,
                new Vector3(xMin - plateMargin, yMin - plateMargin, zBack - 0.08f),
                new Vector3(xMax + plateMargin, yMax + plateMargin, zBack));
        }

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// チューブ上端・下端のエアロック端部ドームメッシュ生成
    /// </summary>
    public static Mesh CreateEndTerminalsMesh(float height, float radius, int segments = 24)
    {
        Mesh mesh = new Mesh();
        mesh.name = "EndTerminalsMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // Bottom station dock (y = -0.5 to y = 1.0)
        AddHemisphere(verts, norms, uvs, tris, new Vector3(0, 0, 0), radius * 1.18f, segments, false);
        // Top dome cap (y = height to y = height + radius)
        AddHemisphere(verts, norms, uvs, tris, new Vector3(0, height, 0), radius * 1.15f, segments, true);

        mesh.vertices  = verts.ToArray();
        mesh.normals   = norms.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// カプセル旅客ポッドの流線型メッシュ生成
    /// </summary>
    public static Mesh CreateCapsulePodMesh(float tubeRadius, int segments = 20)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CapsulePodMesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float podRadius = tubeRadius * 0.72f;
        float podHalfLength = podRadius * 1.8f;
        int verticalSlices = 14;

        // Aerodynamic bullet capsule profile:
        // y from -podHalfLength to +podHalfLength
        for (int i = 0; i <= verticalSlices; i++)
        {
            float v = (float)i / verticalSlices;
            float y = Mathf.Lerp(-podHalfLength, podHalfLength, v);

            // Shape radius factor (rounded nose at top, tapered boat tail at bottom)
            float normY = y / podHalfLength; // -1 to +1
            float rFactor = (normY >= 0.0f)
                ? Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - normY * normY)) // Hemispherical nose
                : Mathf.Lerp(0.6f, 1.0f, normY + 1.0f);             // Streamlined tapered tail

            float currentRadius = podRadius * Mathf.Max(0.1f, rFactor);

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float angle = u * Mathf.PI * 2.0f;

                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float x = cos * currentRadius;
                float z = sin * currentRadius;

                Vector3 pos = new Vector3(x, y, z);
                Vector3 norm = new Vector3(cos, normY * 0.5f, sin).normalized;

                verts.Add(pos);
                norms.Add(norm);
                uvs.Add(new Vector2(u, v));
            }
        }

        int ringVerts = segments + 1;
        for (int i = 0; i < verticalSlices; i++)
        {
            for (int s = 0; s < segments; s++)
            {
                int curr = i * ringVerts + s;
                int next = curr + ringVerts;

                // Clockwise winding from outside
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

    private static void AddBoxGeometry(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 min, Vector3 max)
    {
        Vector3 center = (min + max) * 0.5f;
        Vector3 h = (max - min) * 0.5f;
        Vector3[] corners = new Vector3[]
        {
            center + new Vector3(-h.x, -h.y, -h.z), // 0: -X -Y -Z
            center + new Vector3( h.x, -h.y, -h.z), // 1: +X -Y -Z
            center + new Vector3( h.x,  h.y, -h.z), // 2: +X +Y -Z
            center + new Vector3(-h.x,  h.y, -h.z), // 3: -X +Y -Z
            center + new Vector3(-h.x, -h.y,  h.z), // 4: -X -Y +Z
            center + new Vector3( h.x, -h.y,  h.z), // 5: +X -Y +Z
            center + new Vector3( h.x,  h.y,  h.z), // 6: +X +Y +Z
            center + new Vector3(-h.x,  h.y,  h.z)  // 7: -X +Y +Z
        };

        // 6 faces: 前(+Z)、後(-Z)、左(-X)、右(+X)、上(+Y)、下(-Y) - All clockwise facing outward
        AddQuad(verts, norms, uvs, tris, corners[0], corners[1], corners[2], corners[3], Vector3.back);
        AddQuad(verts, norms, uvs, tris, corners[5], corners[4], corners[7], corners[6], Vector3.forward);
        AddQuad(verts, norms, uvs, tris, corners[4], corners[0], corners[3], corners[7], Vector3.left);
        AddQuad(verts, norms, uvs, tris, corners[1], corners[5], corners[6], corners[2], Vector3.right);
        AddQuad(verts, norms, uvs, tris, corners[3], corners[2], corners[6], corners[7], Vector3.up);
        AddQuad(verts, norms, uvs, tris, corners[4], corners[5], corners[1], corners[0], Vector3.down);
    }

    private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 norm)
    {
        int start = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
        norms.Add(norm); norms.Add(norm); norms.Add(norm); norms.Add(norm);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

        // Clockwise winding when viewed along normal
        tris.Add(start);
        tris.Add(start + 2);
        tris.Add(start + 1);

        tris.Add(start);
        tris.Add(start + 3);
        tris.Add(start + 2);
    }

    private static void AddHemisphere(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 center, float radius, int segments, bool isTop)
    {
        int startIdx = verts.Count;
        int rings = 6;
        int ringVerts = segments + 1;

        for (int r = 0; r <= rings; r++)
        {
            float phi = ((float)r / rings) * (Mathf.PI * 0.5f);
            float yOffset = Mathf.Sin(phi) * radius * (isTop ? 1.0f : -1.0f);
            float rCurrent = Mathf.Cos(phi) * radius;

            for (int s = 0; s <= segments; s++)
            {
                float theta = ((float)s / segments) * Mathf.PI * 2.0f;
                float x = Mathf.Cos(theta) * rCurrent;
                float z = Mathf.Sin(theta) * rCurrent;

                Vector3 pos = center + new Vector3(x, yOffset, z);
                Vector3 norm = (pos - center).normalized;

                verts.Add(pos);
                norms.Add(norm);
                uvs.Add(new Vector2((float)s / segments, (float)r / rings));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int curr = startIdx + r * ringVerts + s;
                int next = curr + ringVerts;

                if (isTop)
                {
                    // Clockwise from outside: curr -> next -> curr+1
                    tris.Add(curr);
                    tris.Add(next);
                    tris.Add(curr + 1);

                    tris.Add(curr + 1);
                    tris.Add(next);
                    tris.Add(next + 1);
                }
                else
                {
                    // Clockwise from outside: curr -> curr+1 -> next
                    tris.Add(curr);
                    tris.Add(curr + 1);
                    tris.Add(next);

                    tris.Add(curr + 1);
                    tris.Add(next + 1);
                    tris.Add(next);
                }
            }
        }
    }
}
