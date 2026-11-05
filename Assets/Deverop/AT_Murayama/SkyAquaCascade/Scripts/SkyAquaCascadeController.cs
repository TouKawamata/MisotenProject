using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 円形プリズム虹 ＆ 空中ウォーターフォール（Sky Aqua Cascade and Prismatic Halo）一括制御コンポーネント。
/// ・他メンバーの雲実装に一切依存しない「下端の空中完全霧散（Evaporation Fade-Out）」構造。
/// ・ビル壁面・屋上エッジへVキースナップで一発吸着可能な「建築放水スパウト（Spout Hardware）」完備。
/// ・放物線飛び出し〜垂直落下を描く立体コア水流層 ＋ 外側飛沫ベール層の二重3Dシェルメッシュ。
/// ・主虹（42度）＋ 副虹（51度・色順反転）＋ 内側ミー散乱グロー ＋ 水滴スパークルを備えた円形プリズム虹。
/// ・実空間3Dパーティクル（湧き上がる水煙 Rising Mist Plume ＆ 太陽光飛沫粒 Sunlit Droplet Sparkles）をハイブリッド併用。
/// ・MaterialPropertyBlock により同一マテリアルでの DrawCall バッチングを維持。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SkyAquaCascadeController : MonoBehaviour
{
    public enum CascadePreset
    {
        [InspectorName("① 標準：建築スパウト滝 ＆ 円形プリズム虹 (Standard Spout & Halo)")]
        StandardSpoutAndHalo = 0,

        [InspectorName("② ワイドカーテン大滝 ＆ 二重虹 (Wide Terrace Megafall)")]
        WideTerraceMegafall = 1,

        [InspectorName("③ プリズム虹ゲート単体・滝なし (Prismatic Solar Halo Only)")]
        PrismaticSolarHaloOnly = 2,

        [InspectorName("④ 高高度スリム・ミストフォール (Slender High-Altitude Mistfall)")]
        SlenderHighMistfall = 3,

        [InspectorName("カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum RainbowBillboardMode
    {
        [InspectorName("カメラ常時正対 (Full Camera Facing)")]
        FullCameraFacing = 0,

        [InspectorName("Y軸回転のみ追従 (Y-Axis Only)")]
        YAxisOnly = 1,

        [InspectorName("固定角度・コースゲート用 (Fixed Orientation)")]
        FixedOrientation = 2
    }

    [Header("設置スタイル・プリセット")]
    [Tooltip("用途に応じたワンクリックプリセット")]
    [SerializeField] private CascadePreset _preset = CascadePreset.StandardSpoutAndHalo;

    [Header("コンポーネント有効切替")]
    [Tooltip("建築放水口金具（Spout Hardware）を表示するか")]
    [SerializeField] private bool _showSpoutHardware = true;

    [Tooltip("滝メッシュ（コア水流層＋外側飛沫ベール層）を表示するか")]
    [SerializeField] private bool _showWaterfallCascade = true;

    [Tooltip("円形プリズム虹（主虹＋副虹）を表示するか")]
    [SerializeField] private bool _showPrismaticRainbow = true;

    [Tooltip("実空間3D水煙・飛沫パーティクルを有効にするか")]
    [SerializeField] private bool _enable3DMistParticles = true;

    [Header("滝（Waterfall）寸法・流体調整")]
    [Tooltip("落下長（Drop Height: 15m 〜 250m）")]
    [Range(15.0f, 250.0f)]
    [SerializeField] private float _dropHeight = 80.0f;

    [Tooltip("滝の横幅（Cascade Width: 2m 〜 40m）")]
    [Range(2.0f, 40.0f)]
    [SerializeField] private float _cascadeWidth = 8.0f;

    [Tooltip("壁面からの放物線飛び出し距離（Arch Forward Throw: 1m 〜 15m）")]
    [Range(1.0f, 15.0f)]
    [SerializeField] private float _archForwardThrow = 4.5f;

    [Tooltip("水流の落下スクロール速度")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float _flowSpeed = 1.6f;

    [Tooltip("風による飛沫揺らぎ・乱流強さ")]
    [Range(0.0f, 2.0f)]
    [SerializeField] private float _turbulence = 0.45f;

    [Tooltip("背景リアルタイム屈折の強さ（_CameraOpaqueTexture）")]
    [Range(0.0f, 0.12f)]
    [SerializeField] private float _refractionStrength = 0.035f;

    [Tooltip("下部蒸発フェードが始まる高さ割合（0.3 〜 0.9）")]
    [Range(0.3f, 0.9f)]
    [SerializeField] private float _evaporationStart = 0.65f;

    [Header("円形プリズム虹（Rainbow Halo）調整")]
    [Tooltip("円形プリズム虹の半径（5m 〜 80m）")]
    [Range(5.0f, 80.0f)]
    [SerializeField] private float _rainbowRadius = 22.0f;

    [Tooltip("主虹の帯の太さ（正規化比率）")]
    [Range(0.03f, 0.30f)]
    [SerializeField] private float _ringThickness = 0.11f;

    [Tooltip("虹のHDR発光・Bloom強度")]
    [Range(0.2f, 8.0f)]
    [SerializeField] private float _rainbowIntensity = 2.6f;

    [Tooltip("外側の副虹（色順反転）の不透明度")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float _secondaryBowOpacity = 0.42f;

    [Tooltip("滝原点に対する円形プリズム虹の相対位置オフセット (X/Y/Z)")]
    [SerializeField] private Vector3 _rainbowOffset = new Vector3(0.0f, -34.0f, 10.0f);

    [Tooltip("虹のカメラ追従モード（常時正対 / Y軸回転のみ / 固定角度ゲート）")]
    [SerializeField] private RainbowBillboardMode _billboardMode = RainbowBillboardMode.YAxisOnly;

    [Header("子オブジェクト・レンダラー参照")]
    [SerializeField] private Renderer _spoutHardwareRenderer;
    [SerializeField] private Renderer _waterfallCoreRenderer;
    [SerializeField] private Renderer _waterfallVeilRenderer;
    [SerializeField] private Renderer _rainbowHaloRenderer;
    [SerializeField] private Transform _rainbowHaloTransform;
    [SerializeField] private ParticleSystem _mistPlumeParticles;
    [SerializeField] private ParticleSystem _dropletSparkleParticles;

    [Header("カスタムマテリアル（別名保存用）")]
    [SerializeField] private Material _customWaterfallMaterial;
    [SerializeField] private Material _customRainbowMaterial;
    [SerializeField] private Material _mistPlumeMaterial;
    [SerializeField] private Material _dropletSparkleMaterial;
    [SerializeField] private string _customSaveName = "";

    // MaterialPropertyBlockキャッシュ
    private MaterialPropertyBlock _mpbCore;
    private MaterialPropertyBlock _mpbVeil;
    private MaterialPropertyBlock _mpbRainbow;

    // ランタイム動的生成メッシュ（OnDestroyで安全破棄）
    private Mesh _runtimeMeshSpout;
    private Mesh _runtimeMeshCore;
    private Mesh _runtimeMeshVeil;
    private Mesh _runtimeMeshRainbow;

    // パラメータ変更検知キャッシュ
    private float _builtWidth = -1f;
    private float _builtDrop = -1f;
    private float _builtThrow = -1f;
    private float _builtRainbowRadius = -1f;
    private CascadePreset _builtPreset = (CascadePreset)(-1);

    // シェーダープロパティID
    private static readonly int LayerModeId          = Shader.PropertyToID("_LayerMode");
    private static readonly int FlowSpeedId          = Shader.PropertyToID("_FlowSpeed");
    private static readonly int TurbulenceId         = Shader.PropertyToID("_Turbulence");
    private static readonly int StreamTilingXId      = Shader.PropertyToID("_StreamTilingX");
    private static readonly int StreamTilingYId      = Shader.PropertyToID("_StreamTilingY");
    private static readonly int RefractionStrengthId = Shader.PropertyToID("_RefractionStrength");
    private static readonly int EvaporationStartId   = Shader.PropertyToID("_EvaporationStart");

    private static readonly int RainbowIntensityId    = Shader.PropertyToID("_RainbowIntensity");
    private static readonly int RingThicknessId       = Shader.PropertyToID("_RingThickness");
    private static readonly int SecondaryBowOpacityId = Shader.PropertyToID("_SecondaryBowOpacity");

    // 公開プロパティ
    public CascadePreset Preset => _preset;
    public bool ShowSpoutHardware => _showSpoutHardware;
    public bool ShowWaterfallCascade => _showWaterfallCascade;
    public bool ShowPrismaticRainbow => _showPrismaticRainbow;
    public bool Enable3DMistParticles => _enable3DMistParticles;
    public float DropHeight => _dropHeight;
    public float CascadeWidth => _cascadeWidth;
    public float ArchForwardThrow => _archForwardThrow;
    public float FlowSpeed => _flowSpeed;
    public float Turbulence => _turbulence;
    public float RefractionStrength => _refractionStrength;
    public float EvaporationStart => _evaporationStart;
    public float RainbowRadius => _rainbowRadius;
    public float RingThickness => _ringThickness;
    public float RainbowIntensity => _rainbowIntensity;
    public float SecondaryBowOpacity => _secondaryBowOpacity;
    public Vector3 RainbowOffset => _rainbowOffset;
    public RainbowBillboardMode BillboardMode => _billboardMode;

    public Material CustomWaterfallMaterial
    {
        get => _customWaterfallMaterial;
        set { _customWaterfallMaterial = value; UpdateCascade(); }
    }

    public Material CustomRainbowMaterial
    {
        get => _customRainbowMaterial;
        set { _customRainbowMaterial = value; UpdateCascade(); }
    }

    public string CustomSaveName
    {
        get => _customSaveName;
        set => _customSaveName = value;
    }

    private void Awake()
    {
        UpdateCascade();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        UpdateCascade();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        CleanupRuntimeMeshes();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                UpdateCascade();
            }
        };
    }
#endif

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!_showPrismaticRainbow || _rainbowHaloTransform == null || _billboardMode == RainbowBillboardMode.FixedOrientation)
        {
            return;
        }

        // プレビューカメラやシャドウカメラは除外
        if (cam == null || (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView))
        {
            return;
        }

        Vector3 toCam = cam.transform.position - _rainbowHaloTransform.position;
        if (_billboardMode == RainbowBillboardMode.YAxisOnly)
        {
            toCam.y = 0.0f;
            if (toCam.sqrMagnitude > 0.001f)
            {
                _rainbowHaloTransform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
        }
        else if (_billboardMode == RainbowBillboardMode.FullCameraFacing)
        {
            if (toCam.sqrMagnitude > 0.001f)
            {
                _rainbowHaloTransform.rotation = Quaternion.LookRotation(toCam.normalized, cam.transform.up);
            }
        }
    }

    public void ApplyPreset(CascadePreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case CascadePreset.StandardSpoutAndHalo:
                _showSpoutHardware     = true;
                _showWaterfallCascade  = true;
                _showPrismaticRainbow  = true;
                _enable3DMistParticles = true;
                _dropHeight            = 80.0f;
                _cascadeWidth          = 8.0f;
                _archForwardThrow      = 4.5f;
                _flowSpeed             = 1.6f;
                _turbulence            = 0.45f;
                _refractionStrength    = 0.035f;
                _evaporationStart      = 0.65f;
                _rainbowRadius         = 22.0f;
                _ringThickness         = 0.11f;
                _rainbowIntensity      = 2.6f;
                _secondaryBowOpacity   = 0.42f;
                _rainbowOffset         = new Vector3(0.0f, -34.0f, 10.0f);
                _billboardMode         = RainbowBillboardMode.YAxisOnly;
                break;

            case CascadePreset.WideTerraceMegafall:
                _showSpoutHardware     = true;
                _showWaterfallCascade  = true;
                _showPrismaticRainbow  = true;
                _enable3DMistParticles = true;
                _dropHeight            = 120.0f;
                _cascadeWidth          = 20.0f;
                _archForwardThrow      = 7.0f;
                _flowSpeed             = 1.85f;
                _turbulence            = 0.60f;
                _refractionStrength    = 0.045f;
                _evaporationStart      = 0.60f;
                _rainbowRadius         = 38.0f;
                _ringThickness         = 0.125f;
                _rainbowIntensity      = 3.0f;
                _secondaryBowOpacity   = 0.55f;
                _rainbowOffset         = new Vector3(0.0f, -52.0f, 14.0f);
                _billboardMode         = RainbowBillboardMode.YAxisOnly;
                break;

            case CascadePreset.PrismaticSolarHaloOnly:
                _showSpoutHardware     = false;
                _showWaterfallCascade  = false;
                _showPrismaticRainbow  = true;
                _enable3DMistParticles = true;
                _dropHeight            = 60.0f;
                _cascadeWidth          = 8.0f;
                _archForwardThrow      = 4.0f;
                _flowSpeed             = 1.4f;
                _turbulence            = 0.30f;
                _refractionStrength    = 0.030f;
                _evaporationStart      = 0.65f;
                _rainbowRadius         = 32.0f;
                _ringThickness         = 0.12f;
                _rainbowIntensity      = 2.8f;
                _secondaryBowOpacity   = 0.50f;
                _rainbowOffset         = Vector3.zero;
                _billboardMode         = RainbowBillboardMode.FixedOrientation;
                break;

            case CascadePreset.SlenderHighMistfall:
                _showSpoutHardware     = true;
                _showWaterfallCascade  = true;
                _showPrismaticRainbow  = true;
                _enable3DMistParticles = true;
                _dropHeight            = 150.0f;
                _cascadeWidth          = 3.5f;
                _archForwardThrow      = 3.0f;
                _flowSpeed             = 1.4f;
                _turbulence            = 0.55f;
                _refractionStrength    = 0.028f;
                _evaporationStart      = 0.50f;
                _rainbowRadius         = 14.0f;
                _ringThickness         = 0.10f;
                _rainbowIntensity      = 2.3f;
                _secondaryBowOpacity   = 0.35f;
                _rainbowOffset         = new Vector3(0.0f, -55.0f, 7.0f);
                _billboardMode         = RainbowBillboardMode.YAxisOnly;
                break;
        }

        RebuildMeshesFromParameters();
        UpdateCascade();
    }

    public void SetShowSpoutHardware(bool show)
    {
        _showSpoutHardware = show;
        UpdateCascade();
    }

    public void SetShowWaterfallCascade(bool show)
    {
        _showWaterfallCascade = show;
        UpdateCascade();
    }

    public void SetShowPrismaticRainbow(bool show)
    {
        _showPrismaticRainbow = show;
        UpdateCascade();
    }

    public void SetEnable3DMistParticles(bool enable)
    {
        _enable3DMistParticles = enable;
        UpdateCascade();
    }

    public void SetBillboardMode(RainbowBillboardMode mode)
    {
        _billboardMode = mode;
        UpdateCascade();
    }

    public void ClearCustomMaterials()
    {
        _customWaterfallMaterial = null;
        _customRainbowMaterial = null;
#if UNITY_EDITOR
        const string matDir = "Assets/Deverop/AT_Murayama/SkyAquaCascade/Materials";
        if (_waterfallCoreRenderer != null)
        {
            Material defaultCore = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matDir + "/M_AquaCascade_CoreWater.mat");
            if (defaultCore != null) _waterfallCoreRenderer.sharedMaterial = defaultCore;
        }
        if (_rainbowHaloRenderer != null)
        {
            Material defaultRainbow = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matDir + "/M_PrismaticRainbow_Halo.mat");
            if (defaultRainbow != null) _rainbowHaloRenderer.sharedMaterial = defaultRainbow;
        }
#endif
        UpdateCascade();
    }

    public void RebuildMeshesFromParameters()
    {
        _builtWidth = -1f;
        _builtDrop = -1f;
        _builtThrow = -1f;
        _builtRainbowRadius = -1f;
        EnsureMeshes(forceRebuild: true);
    }

    public void UpdateCascade()
    {
        EnsureHierarchy();
        EnsureMeshes(forceRebuild: false);
        ApplyVisibilityAndTransforms();
        ApplyMaterialPropertyBlocks();
        ConfigureParticleSystems();
    }

    private void EnsureHierarchy()
    {
        if (_spoutHardwareRenderer == null)
        {
            Transform t = transform.Find("SpoutHardware");
            if (t != null) _spoutHardwareRenderer = t.GetComponent<Renderer>();
        }
        if (_waterfallCoreRenderer == null)
        {
            Transform t = transform.Find("WaterfallCore");
            if (t != null) _waterfallCoreRenderer = t.GetComponent<Renderer>();
        }
        if (_waterfallVeilRenderer == null)
        {
            Transform t = transform.Find("WaterfallSprayVeil");
            if (t != null) _waterfallVeilRenderer = t.GetComponent<Renderer>();
        }
        if (_rainbowHaloRenderer == null)
        {
            Transform t = transform.Find("PrismaticRainbowHalo");
            if (t != null)
            {
                _rainbowHaloTransform = t;
                _rainbowHaloRenderer = t.GetComponent<Renderer>();
            }
        }
        if (_rainbowHaloTransform == null && _rainbowHaloRenderer != null)
        {
            _rainbowHaloTransform = _rainbowHaloRenderer.transform;
        }
        if (_mistPlumeParticles == null)
        {
            Transform t = transform.Find("MistPlumeParticles");
            if (t != null) _mistPlumeParticles = t.GetComponent<ParticleSystem>();
        }
        if (_dropletSparkleParticles == null)
        {
            Transform t = transform.Find("DropletSparkleParticles");
            if (t != null) _dropletSparkleParticles = t.GetComponent<ParticleSystem>();
        }

#if UNITY_EDITOR
        const string matDir = "Assets/Deverop/AT_Murayama/SkyAquaCascade/Materials";
        if (_mistPlumeMaterial == null)
        {
            _mistPlumeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matDir + "/M_AquaMist_PlumeParticle.mat");
        }
        if (_dropletSparkleMaterial == null)
        {
            _dropletSparkleMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matDir + "/M_AquaDroplet_SparkleParticle.mat");
        }
#endif

        // Prefab Asset 自体（シーン外）の編集時は子 GameObject の動的生成を行わない（UnityException 防止）
        if (!gameObject.scene.IsValid())
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (_spoutHardwareRenderer == null)
            {
                _spoutHardwareRenderer = CreateChildMeshRenderer("SpoutHardware", matDir + "/M_AquaSpout_Hardware.mat", castShadows: true);
            }
            if (_waterfallCoreRenderer == null)
            {
                _waterfallCoreRenderer = CreateChildMeshRenderer("WaterfallCore", matDir + "/M_AquaCascade_CoreWater.mat", castShadows: false);
            }
            if (_waterfallVeilRenderer == null)
            {
                _waterfallVeilRenderer = CreateChildMeshRenderer("WaterfallSprayVeil", matDir + "/M_AquaCascade_SprayVeil.mat", castShadows: false);
            }
            if (_rainbowHaloRenderer == null)
            {
                _rainbowHaloRenderer = CreateChildMeshRenderer("PrismaticRainbowHalo", matDir + "/M_PrismaticRainbow_Halo.mat", castShadows: false);
                _rainbowHaloTransform = _rainbowHaloRenderer.transform;
            }
        }
#endif

        if (_mistPlumeParticles == null)
        {
            _mistPlumeParticles = CreateOrGetParticleSystem("MistPlumeParticles", _mistPlumeMaterial);
        }
        if (_dropletSparkleParticles == null)
        {
            _dropletSparkleParticles = CreateOrGetParticleSystem("DropletSparkleParticles", _dropletSparkleMaterial);
        }
    }

#if UNITY_EDITOR
    private MeshRenderer CreateChildMeshRenderer(string childName, string matPath, bool castShadows)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        mr.receiveShadows = castShadows;
        Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null)
        {
            mr.sharedMaterial = mat;
        }
        return mr;
    }
#endif

    private ParticleSystem CreateOrGetParticleSystem(string childName, Material mat)
    {
        Transform existing = transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        if (existing == null)
        {
            go.transform.SetParent(transform, false);
        }

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = go.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            if (mat != null)
            {
                psr.sharedMaterial = mat;
            }
        }
        return ps;
    }

    private bool MatchesPresetDimensions(CascadePreset preset)
    {
        switch (preset)
        {
            case CascadePreset.StandardSpoutAndHalo:
                return Mathf.Abs(_dropHeight - 80.0f) < 0.05f &&
                       Mathf.Abs(_cascadeWidth - 8.0f) < 0.05f &&
                       Mathf.Abs(_archForwardThrow - 4.5f) < 0.05f &&
                       Mathf.Abs(_rainbowRadius - 22.0f) < 0.05f;
            case CascadePreset.WideTerraceMegafall:
                return Mathf.Abs(_dropHeight - 120.0f) < 0.05f &&
                       Mathf.Abs(_cascadeWidth - 20.0f) < 0.05f &&
                       Mathf.Abs(_archForwardThrow - 7.0f) < 0.05f &&
                       Mathf.Abs(_rainbowRadius - 38.0f) < 0.05f;
            case CascadePreset.PrismaticSolarHaloOnly:
                return Mathf.Abs(_dropHeight - 60.0f) < 0.05f &&
                       Mathf.Abs(_cascadeWidth - 8.0f) < 0.05f &&
                       Mathf.Abs(_archForwardThrow - 4.0f) < 0.05f &&
                       Mathf.Abs(_rainbowRadius - 32.0f) < 0.05f;
            case CascadePreset.SlenderHighMistfall:
                return Mathf.Abs(_dropHeight - 150.0f) < 0.05f &&
                       Mathf.Abs(_cascadeWidth - 3.5f) < 0.05f &&
                       Mathf.Abs(_archForwardThrow - 3.0f) < 0.05f &&
                       Mathf.Abs(_rainbowRadius - 14.0f) < 0.05f;
            default:
                return false;
        }
    }

    private void EnsureMeshes(bool forceRebuild)
    {
        bool paramsChanged = forceRebuild ||
                             Mathf.Abs(_builtWidth - _cascadeWidth) > 0.01f ||
                             Mathf.Abs(_builtDrop - _dropHeight) > 0.01f ||
                             Mathf.Abs(_builtThrow - _archForwardThrow) > 0.01f ||
                             Mathf.Abs(_builtRainbowRadius - _rainbowRadius) > 0.01f ||
                             _builtPreset != _preset;

        if (!paramsChanged)
        {
            return;
        }

        _builtWidth = _cascadeWidth;
        _builtDrop = _dropHeight;
        _builtThrow = _archForwardThrow;
        _builtRainbowRadius = _rainbowRadius;
        _builtPreset = _preset;

#if UNITY_EDITOR
        // 標準プリセット寸法と一致する場合はディスク上の永続ネイティブMeshアセットを優先割り当て
        if (MatchesPresetDimensions(_preset))
        {
            const string meshDir = "Assets/Deverop/AT_Murayama/SkyAquaCascade/Meshes";
            string suffix = GetPresetAssetSuffix(_preset);
            if (!string.IsNullOrEmpty(suffix))
            {
                Mesh spoutAsset   = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"{meshDir}/Mesh_Spout_{suffix}.asset");
                Mesh coreAsset    = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"{meshDir}/Mesh_CascadeCore_{suffix}.asset");
                Mesh veilAsset    = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"{meshDir}/Mesh_CascadeVeil_{suffix}.asset");
                Mesh rainbowAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"{meshDir}/Mesh_RainbowHalo_{suffix}.asset");

                if (spoutAsset != null && coreAsset != null && veilAsset != null && rainbowAsset != null)
                {
                    AssignMeshFilter(_spoutHardwareRenderer, spoutAsset);
                    AssignMeshFilter(_waterfallCoreRenderer, coreAsset);
                    AssignMeshFilter(_waterfallVeilRenderer, veilAsset);
                    AssignMeshFilter(_rainbowHaloRenderer, rainbowAsset);
                    return;
                }
            }
        }
#endif

        // カスタム寸法時はパラメトリック3Dメッシュを動的生成
        CleanupRuntimeMeshes();

        _runtimeMeshSpout   = BuildSpoutHardwareMesh(_cascadeWidth, _archForwardThrow);
        _runtimeMeshCore    = BuildWaterfallCoreMesh(_cascadeWidth, _dropHeight, _archForwardThrow);
        _runtimeMeshVeil    = BuildWaterfallSprayVeilMesh(_cascadeWidth, _dropHeight, _archForwardThrow);
        _runtimeMeshRainbow = BuildPrismaticRainbowMesh(_rainbowRadius);

        AssignMeshFilter(_spoutHardwareRenderer, _runtimeMeshSpout);
        AssignMeshFilter(_waterfallCoreRenderer, _runtimeMeshCore);
        AssignMeshFilter(_waterfallVeilRenderer, _runtimeMeshVeil);
        AssignMeshFilter(_rainbowHaloRenderer, _runtimeMeshRainbow);
    }

    public static string GetPresetAssetSuffix(CascadePreset preset)
    {
        switch (preset)
        {
            case CascadePreset.StandardSpoutAndHalo:   return "Standard";
            case CascadePreset.WideTerraceMegafall:    return "WideMegafall";
            case CascadePreset.PrismaticSolarHaloOnly: return "SolarGate";
            case CascadePreset.SlenderHighMistfall:    return "SlenderMistfall";
            default: return "";
        }
    }

    private static void AssignMeshFilter(Renderer r, Mesh m)
    {
        if (r == null || m == null) return;
        MeshFilter mf = r.GetComponent<MeshFilter>();
        if (mf != null)
        {
            mf.sharedMesh = m;
        }
    }

    private void ApplyVisibilityAndTransforms()
    {
        if (_spoutHardwareRenderer != null)
        {
            _spoutHardwareRenderer.gameObject.SetActive(_showSpoutHardware);
        }
        if (_waterfallCoreRenderer != null)
        {
            _waterfallCoreRenderer.gameObject.SetActive(_showWaterfallCascade);
        }
        if (_waterfallVeilRenderer != null)
        {
            _waterfallVeilRenderer.gameObject.SetActive(_showWaterfallCascade);
        }
        if (_rainbowHaloRenderer != null)
        {
            _rainbowHaloRenderer.gameObject.SetActive(_showPrismaticRainbow);
        }
        if (_rainbowHaloTransform != null)
        {
            _rainbowHaloTransform.localPosition = _rainbowOffset;
            if (_billboardMode == RainbowBillboardMode.FixedOrientation)
            {
                _rainbowHaloTransform.localRotation = Quaternion.identity;
            }
        }
    }

    private void ApplyMaterialPropertyBlocks()
    {
        float tilingX = Mathf.Clamp(_cascadeWidth * 0.38f, 1.0f, 10.0f);
        float tilingY = Mathf.Clamp(_dropHeight * 0.065f, 1.5f, 14.0f);

        if (_waterfallCoreRenderer != null)
        {
            if (_customWaterfallMaterial != null)
            {
                _waterfallCoreRenderer.sharedMaterial = _customWaterfallMaterial;
            }
            if (_mpbCore == null) _mpbCore = new MaterialPropertyBlock();
            _waterfallCoreRenderer.GetPropertyBlock(_mpbCore);
            _mpbCore.SetFloat(LayerModeId, 0.0f);
            _mpbCore.SetFloat(FlowSpeedId, _flowSpeed);
            _mpbCore.SetFloat(TurbulenceId, _turbulence);
            _mpbCore.SetFloat(StreamTilingXId, tilingX);
            _mpbCore.SetFloat(StreamTilingYId, tilingY);
            _mpbCore.SetFloat(RefractionStrengthId, _refractionStrength);
            _mpbCore.SetFloat(EvaporationStartId, _evaporationStart);
            _waterfallCoreRenderer.SetPropertyBlock(_mpbCore);
        }

        if (_waterfallVeilRenderer != null)
        {
            if (_mpbVeil == null) _mpbVeil = new MaterialPropertyBlock();
            _waterfallVeilRenderer.GetPropertyBlock(_mpbVeil);
            _mpbVeil.SetFloat(LayerModeId, 1.0f);
            _mpbVeil.SetFloat(FlowSpeedId, _flowSpeed * 1.18f);
            _mpbVeil.SetFloat(TurbulenceId, _turbulence * 1.25f);
            _mpbVeil.SetFloat(StreamTilingXId, tilingX * 1.25f);
            _mpbVeil.SetFloat(StreamTilingYId, tilingY * 1.15f);
            _mpbVeil.SetFloat(RefractionStrengthId, _refractionStrength * 0.65f);
            _mpbVeil.SetFloat(EvaporationStartId, Mathf.Max(0.25f, _evaporationStart - 0.12f));
            _waterfallVeilRenderer.SetPropertyBlock(_mpbVeil);
        }

        if (_rainbowHaloRenderer != null)
        {
            if (_customRainbowMaterial != null)
            {
                _rainbowHaloRenderer.sharedMaterial = _customRainbowMaterial;
            }
            if (_mpbRainbow == null) _mpbRainbow = new MaterialPropertyBlock();
            _rainbowHaloRenderer.GetPropertyBlock(_mpbRainbow);
            _mpbRainbow.SetFloat(RainbowIntensityId, _rainbowIntensity);
            _mpbRainbow.SetFloat(RingThicknessId, _ringThickness);
            _mpbRainbow.SetFloat(SecondaryBowOpacityId, _secondaryBowOpacity);
            _rainbowHaloRenderer.SetPropertyBlock(_mpbRainbow);
        }
    }

    private void ConfigureParticleSystems()
    {
        // 1. Rising Mist Plume (湧き上がる水煙クラウド)
        if (_mistPlumeParticles != null)
        {
            bool activeMist = _enable3DMistParticles && _showWaterfallCascade;
            _mistPlumeParticles.gameObject.SetActive(activeMist);
            if (activeMist)
            {
                float evapCenterY = -_dropHeight * Mathf.Lerp(_evaporationStart, 0.92f, 0.55f);
                float plumeZ = _archForwardThrow + 1.2f;
                _mistPlumeParticles.transform.localPosition = new Vector3(0.0f, evapCenterY, plumeZ);

                var main = _mistPlumeParticles.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 5.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
                float baseMistSize = Mathf.Clamp(_cascadeWidth * 0.85f + _dropHeight * 0.08f, 6.0f, 28.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(baseMistSize * 0.7f, baseMistSize * 1.35f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
                main.startColor = new Color(0.95f, 0.98f, 1.0f, 0.42f);
                main.gravityModifier = -0.06f; // ふわっと湧き上がる上昇気流効果
                main.maxParticles = 64;

                var emission = _mistPlumeParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = Mathf.Clamp(6.0f + _cascadeWidth * 0.45f, 6.0f, 18.0f);

                var shape = _mistPlumeParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(
                    Mathf.Max(3.0f, _cascadeWidth * 1.15f),
                    Mathf.Max(8.0f, _dropHeight * (1.0f - _evaporationStart) * 0.75f),
                    Mathf.Max(3.0f, _archForwardThrow * 0.85f)
                );

                var col = _mistPlumeParticles.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.92f, 0.97f, 1.0f), 0.0f),
                        new GradientColorKey(Color.white, 0.5f),
                        new GradientColorKey(new Color(0.88f, 0.95f, 1.0f), 1.0f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.0f, 0.0f),
                        new GradientAlphaKey(0.55f, 0.25f),
                        new GradientAlphaKey(0.45f, 0.65f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var sol = _mistPlumeParticles.sizeOverLifetime;
                sol.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve(
                    new Keyframe(0.0f, 0.55f),
                    new Keyframe(0.5f, 1.0f),
                    new Keyframe(1.0f, 1.45f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

                if (_mistPlumeMaterial != null)
                {
                    ParticleSystemRenderer psr = _mistPlumeParticles.GetComponent<ParticleSystemRenderer>();
                    if (psr != null && psr.sharedMaterial == null) psr.sharedMaterial = _mistPlumeMaterial;
                }
                if (Application.isPlaying && !_mistPlumeParticles.isPlaying)
                {
                    _mistPlumeParticles.Play();
                }
            }
        }

        // 2. Sunlit Droplet Sparkles (太陽光で煌めく3D飛沫粒)
        if (_dropletSparkleParticles != null)
        {
            bool activeSparkles = _enable3DMistParticles && (_showWaterfallCascade || _showPrismaticRainbow);
            _dropletSparkleParticles.gameObject.SetActive(activeSparkles);
            if (activeSparkles)
            {
                Vector3 centerPos = _showWaterfallCascade
                    ? new Vector3(0.0f, -_dropHeight * 0.45f, _archForwardThrow)
                    : _rainbowOffset;
                _dropletSparkleParticles.transform.localPosition = centerPos;

                var main = _dropletSparkleParticles.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.35f);
                main.startColor = new Color(0.90f, 0.98f, 1.0f, 0.85f);
                main.gravityModifier = _showWaterfallCascade ? 0.25f : -0.02f;
                main.maxParticles = 96;

                var emission = _dropletSparkleParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = _showWaterfallCascade
                    ? Mathf.Clamp(14.0f + _cascadeWidth * 0.8f, 14.0f, 36.0f)
                    : Mathf.Clamp(_rainbowRadius * 0.65f, 10.0f, 28.0f);

                var shape = _dropletSparkleParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                if (_showWaterfallCascade)
                {
                    shape.scale = new Vector3(
                        Mathf.Max(4.0f, _cascadeWidth * 1.35f),
                        Mathf.Max(12.0f, _dropHeight * 0.75f),
                        Mathf.Max(4.0f, _archForwardThrow * 1.2f)
                    );
                }
                else
                {
                    shape.scale = new Vector3(_rainbowRadius * 1.4f, _rainbowRadius * 1.4f, 6.0f);
                }

                var col = _dropletSparkleParticles.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.85f, 0.97f, 1.0f), 0.0f),
                        new GradientColorKey(Color.white, 0.5f),
                        new GradientColorKey(new Color(0.75f, 0.92f, 1.0f), 1.0f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.0f, 0.0f),
                        new GradientAlphaKey(0.95f, 0.2f),
                        new GradientAlphaKey(0.85f, 0.7f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                if (_dropletSparkleMaterial != null)
                {
                    ParticleSystemRenderer psr = _dropletSparkleParticles.GetComponent<ParticleSystemRenderer>();
                    if (psr != null && psr.sharedMaterial == null) psr.sharedMaterial = _dropletSparkleMaterial;
                }
                if (Application.isPlaying && !_dropletSparkleParticles.isPlaying)
                {
                    _dropletSparkleParticles.Play();
                }
            }
        }
    }

    private void CleanupRuntimeMeshes()
    {
        DestroyRuntimeMesh(ref _runtimeMeshSpout);
        DestroyRuntimeMesh(ref _runtimeMeshCore);
        DestroyRuntimeMesh(ref _runtimeMeshVeil);
        DestroyRuntimeMesh(ref _runtimeMeshRainbow);
    }

    private static void DestroyRuntimeMesh(ref Mesh m)
    {
        if (m == null) return;
        if (Application.isPlaying)
        {
            Destroy(m);
        }
        else
        {
            DestroyImmediate(m);
        }
        m = null;
    }

    // =========================================================================
    // 3D プロシージャル幾何学メッシュ生成群（全ポリゴン外向き時計回り・閉鎖立体）
    // =========================================================================

    /// <summary>
    /// 1. 白亜の建築放水スパウト金具メッシュ（壁面マウントフランジ ＋ カンチレバー導水路 ＋ サイドフィン ＋ 下部支持ストラット）
    /// Pivot (0, 0, 0) はビル壁面接着面・放水口高。
    /// </summary>
    public static Mesh BuildSpoutHardwareMesh(float cascadeWidth, float archThrow)
    {
        Mesh mesh = new Mesh
        {
            name = "Runtime_AquaCascade_Spout"
        };

        List<Vector3> verts = new List<Vector3>(256);
        List<Vector3> norms = new List<Vector3>(256);
        List<Vector2> uvs   = new List<Vector2>(256);
        List<int>     tris  = new List<int>(384);

        float halfW = Mathf.Max(1.0f, cascadeWidth * 0.5f);
        float spoutLen = Mathf.Clamp(archThrow * 0.55f, 1.2f, 4.5f);
        float flangeW = halfW + 1.1f;
        float wallThick = 0.45f;

        // (A) 壁面マウントバックプレート (Z: 0.0 .. 0.45, Y: -2.2 .. +1.2)
        AddBox(verts, norms, uvs, tris,
            new Vector3(-flangeW, -2.2f, 0.0f),
            new Vector3( flangeW,  1.2f, wallThick),
            new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.14f));

        // (B) 導水路の底板 (Z: 0.35 .. spoutLen, Y: -0.45 .. 0.0)
        AddBox(verts, norms, uvs, tris,
            new Vector3(-(halfW + 0.35f), -0.45f, 0.35f),
            new Vector3( (halfW + 0.35f),  0.0f,  spoutLen),
            new Vector2(0.0f, 0.25f), new Vector2(1.0f, 0.90f));

        // (C) 内部ウェット導水路水面プレート (UV.y = 2.0 でウェットシアン発色)
        AddBox(verts, norms, uvs, tris,
            new Vector3(-halfW, -0.02f, 0.40f),
            new Vector3( halfW,  0.08f, spoutLen - 0.05f),
            new Vector2(0.0f, 1.8f), new Vector2(1.0f, 2.2f));

        // (D) 左右のエアロサイドウォール（側壁フィン）
        float finThick = 0.40f;
        AddBox(verts, norms, uvs, tris,
            new Vector3(-(halfW + finThick), -0.45f, 0.35f),
            new Vector3(-halfW,               0.75f, spoutLen + 0.2f),
            new Vector2(0.0f, 0.30f), new Vector2(1.0f, 0.95f));

        AddBox(verts, norms, uvs, tris,
            new Vector3(halfW,              -0.45f, 0.35f),
            new Vector3(halfW + finThick,    0.75f, spoutLen + 0.2f),
            new Vector2(0.0f, 0.30f), new Vector2(1.0f, 0.95f));

        // (E) 下部斜め支持ストラット梁（左右2本＋中央リブ）
        int strutCount = cascadeWidth >= 12.0f ? 3 : 2;
        for (int i = 0; i < strutCount; i++)
        {
            float t = strutCount == 1 ? 0.5f : (float)i / (strutCount - 1);
            float sx = Mathf.Lerp(-halfW * 0.82f, halfW * 0.82f, t);
            AddSlantedStrut(verts, norms, uvs, tris,
                new Vector3(sx, -1.95f, 0.35f),
                new Vector3(sx, -0.40f, spoutLen * 0.82f),
                0.22f);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 2. 水流コア層メッシュ（Core Liquid Ribbon）
    /// 放物線を描いて飛び出し垂直に落下する表裏閉鎖立体曲面メッシュ。
    /// </summary>
    public static Mesh BuildWaterfallCoreMesh(float cascadeWidth, float dropHeight, float archThrow)
    {
        Mesh mesh = new Mesh
        {
            name = "Runtime_AquaCascade_Core"
        };

        const int xSegs = 20;
        const int ySegs = 48;
        int ringVerts = (xSegs + 1) * 2; // 前面 + 背面
        int totalVerts = (ySegs + 1) * ringVerts;

        List<Vector3> verts  = new List<Vector3>(totalVerts);
        List<Vector3> norms  = new List<Vector3>(totalVerts);
        List<Vector2> uvs    = new List<Vector2>(totalVerts);
        List<Color>   colors = new List<Color>(totalVerts);
        List<int>     tris   = new List<int>(xSegs * ySegs * 12);

        float halfW = Mathf.Max(1.0f, cascadeWidth * 0.5f);
        float spoutLipZ = Mathf.Clamp(archThrow * 0.55f, 1.2f, 4.5f) - 0.15f;

        for (int iy = 0; iy <= ySegs; iy++)
        {
            float v = (float)iy / ySegs;
            EvaluateCascadeSpine(v, dropHeight, archThrow, spoutLipZ, out float spineY, out float spineZ, out Vector3 forwardNormal);

            // 落下するにつれて少し幅が広がり、下端でエアレーション膨張する
            float widthScale = 1.0f + 0.14f * v;
            float ribbonThickness = Mathf.Lerp(0.32f, 0.85f, v);
            float vertAlpha = Mathf.SmoothStep(1.0f, 0.0f, Mathf.InverseLerp(0.88f, 1.0f, v));

            for (int side = 0; side < 2; side++)
            {
                float normalSign = (side == 0) ? 1.0f : -1.0f;
                for (int ix = 0; ix <= xSegs; ix++)
                {
                    float u = (float)ix / xSegs;
                    float centeredU = u * 2.0f - 1.0f; // -1..+1

                    // 3D C字断面アーチ ＋ 水柱の縦畝（Fluting）で太陽光の多面スペキュラを作る
                    float archBowZ = (1.0f - centeredU * centeredU) * Mathf.Clamp(archThrow * 0.16f, 0.25f, 1.4f);
                    float flutingZ = Mathf.Sin(u * Mathf.PI * 7.0f) * 0.12f * (1.0f - 0.5f * v);
                    float edgeTaper = Mathf.Sin(u * Mathf.PI); // 両端で厚みを0へ絞る

                    float x = centeredU * halfW * widthScale;
                    float zOffset = archBowZ + flutingZ + normalSign * (ribbonThickness * 0.5f * edgeTaper);

                    Vector3 pos = new Vector3(x, spineY, spineZ + zOffset);
                    Vector3 n = new Vector3(centeredU * 0.28f, forwardNormal.y * normalSign, forwardNormal.z * normalSign).normalized;

                    verts.Add(pos);
                    norms.Add(n);
                    uvs.Add(new Vector2(u, v));
                    colors.Add(new Color(1.0f, 1.0f, 1.0f, vertAlpha));
                }
            }
        }

        int stride = xSegs + 1;
        for (int iy = 0; iy < ySegs; iy++)
        {
            int row0Front = iy * ringVerts;
            int row1Front = (iy + 1) * ringVerts;
            int row0Back  = row0Front + stride;
            int row1Back  = row1Front + stride;

            for (int ix = 0; ix < xSegs; ix++)
            {
                // 前面 (+Z向き法線: 外側から見て時計回り)
                int f00 = row0Front + ix;
                int f10 = row0Front + ix + 1;
                int f01 = row1Front + ix;
                int f11 = row1Front + ix + 1;
                tris.Add(f00); tris.Add(f11); tris.Add(f10);
                tris.Add(f00); tris.Add(f01); tris.Add(f11);

                // 背面 (-Z向き法線: 裏側から見て時計回り)
                int b00 = row0Back + ix;
                int b10 = row0Back + ix + 1;
                int b01 = row1Back + ix;
                int b11 = row1Back + ix + 1;
                tris.Add(b00); tris.Add(b10); tris.Add(b11);
                tris.Add(b00); tris.Add(b11); tris.Add(b01);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 3. 外側飛沫ベール層メッシュ（Outer Spray Veil）
    /// コア水流層の外側を立体的に包み込み、下部で大きく水煙として広がるシェルメッシュ。
    /// </summary>
    public static Mesh BuildWaterfallSprayVeilMesh(float cascadeWidth, float dropHeight, float archThrow)
    {
        Mesh mesh = new Mesh
        {
            name = "Runtime_AquaCascade_SprayVeil"
        };

        const int xSegs = 20;
        const int ySegs = 40;
        int stride = xSegs + 1;

        List<Vector3> verts  = new List<Vector3>((ySegs + 1) * stride);
        List<Vector3> norms  = new List<Vector3>((ySegs + 1) * stride);
        List<Vector2> uvs    = new List<Vector2>((ySegs + 1) * stride);
        List<Color>   colors = new List<Color>((ySegs + 1) * stride);
        List<int>     tris   = new List<int>(xSegs * ySegs * 6);

        float halfW = Mathf.Max(1.2f, cascadeWidth * 0.56f);
        float spoutLipZ = Mathf.Clamp(archThrow * 0.55f, 1.2f, 4.5f);

        for (int iy = 0; iy <= ySegs; iy++)
        {
            float v = (float)iy / ySegs;
            EvaluateCascadeSpine(v, dropHeight, archThrow, spoutLipZ, out float spineY, out float spineZ, out Vector3 forwardNormal);

            // 下部に向かうほどミストが大きく外側に広がる
            float mistFlare = 1.0f + 0.42f * v * v;
            float veilDepth = Mathf.Lerp(0.8f, Mathf.Max(2.5f, archThrow * 0.65f), v);
            float vertAlpha = Mathf.SmoothStep(1.0f, 0.0f, Mathf.InverseLerp(0.82f, 1.0f, v));

            for (int ix = 0; ix <= xSegs; ix++)
            {
                float u = (float)ix / xSegs;
                // 半円筒状にコア水流を包み込む角度 (-PI/2 .. +PI/2)
                float theta = (u - 0.5f) * Mathf.PI * 0.92f;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);

                float x = sinT * halfW * mistFlare;
                float z = spineZ + cosT * veilDepth;

                Vector3 pos = new Vector3(x, spineY, z);
                Vector3 n = new Vector3(sinT * 0.65f, forwardNormal.y * 0.4f, cosT).normalized;

                verts.Add(pos);
                norms.Add(n);
                uvs.Add(new Vector2(u, v));
                colors.Add(new Color(1.0f, 1.0f, 1.0f, vertAlpha));
            }
        }

        for (int iy = 0; iy < ySegs; iy++)
        {
            int r0 = iy * stride;
            int r1 = (iy + 1) * stride;
            for (int ix = 0; ix < xSegs; ix++)
            {
                int i00 = r0 + ix;
                int i10 = r0 + ix + 1;
                int i01 = r1 + ix;
                int i11 = r1 + ix + 1;

                tris.Add(i00); tris.Add(i11); tris.Add(i10);
                tris.Add(i00); tris.Add(i01); tris.Add(i11);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 4. 円形プリズム虹ハロー用 3D光学レンズドームメッシュ
    /// </summary>
    public static Mesh BuildPrismaticRainbowMesh(float rainbowRadius)
    {
        Mesh mesh = new Mesh
        {
            name = "Runtime_AquaCascade_RainbowHalo"
        };

        const int radialSegs  = 8;
        const int angularSegs = 64;
        float radius = Mathf.Max(2.0f, rainbowRadius);
        float domeDepth = radius * 0.08f;

        List<Vector3> verts  = new List<Vector3>((radialSegs + 1) * (angularSegs + 1));
        List<Vector3> norms  = new List<Vector3>((radialSegs + 1) * (angularSegs + 1));
        List<Vector2> uvs    = new List<Vector2>((radialSegs + 1) * (angularSegs + 1));
        List<Color>   colors = new List<Color>((radialSegs + 1) * (angularSegs + 1));
        List<int>     tris   = new List<int>(radialSegs * angularSegs * 6);

        for (int ir = 0; ir <= radialSegs; ir++)
        {
            float rNorm = (float)ir / radialSegs; // 0..1
            float r = rNorm * radius;
            float zCurve = (1.0f - rNorm * rNorm) * domeDepth;

            for (int ia = 0; ia <= angularSegs; ia++)
            {
                float aNorm = (float)ia / angularSegs;
                float angle = aNorm * Mathf.PI * 2.0f;
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);

                float x = cosA * r;
                float y = sinA * r;

                verts.Add(new Vector3(x, y, zCurve));
                norms.Add(new Vector3(cosA * rNorm * 0.18f, sinA * rNorm * 0.18f, 1.0f).normalized);
                uvs.Add(new Vector2(cosA * rNorm * 0.5f + 0.5f, sinA * rNorm * 0.5f + 0.5f));
                colors.Add(Color.white);
            }
        }

        int stride = angularSegs + 1;
        for (int ir = 0; ir < radialSegs; ir++)
        {
            int r0 = ir * stride;
            int r1 = (ir + 1) * stride;
            for (int ia = 0; ia < angularSegs; ia++)
            {
                int i00 = r0 + ia;
                int i10 = r0 + ia + 1;
                int i01 = r1 + ia;
                int i11 = r1 + ia + 1;

                tris.Add(i00); tris.Add(i01); tris.Add(i11);
                tris.Add(i00); tris.Add(i11); tris.Add(i10);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // 放物線飛び出し〜垂直落下の中心背骨軌道を計算
    private static void EvaluateCascadeSpine(
        float v,
        float dropHeight,
        float archThrow,
        float spoutLipZ,
        out float spineY,
        out float spineZ,
        out Vector3 forwardNormal)
    {
        // v: 0(放水口) -> 1(下端)
        // 上部で水平方向へ放物線飛翔し、重力加速しながら垂直落下へ漸近する滑らかなC2曲線
        float launchFactor = 1.0f - Mathf.Exp(-v * 6.5f);
        spineZ = spoutLipZ + archThrow * launchFactor;

        // 上端は緩やかに下がり始め、すぐに等加速度〜終端速度落下へ移行
        float easedV = v * v * 0.22f + v * 0.78f;
        spineY = -dropHeight * easedV;

        float dzdv = archThrow * 6.5f * Mathf.Exp(-v * 6.5f);
        float dydv = -dropHeight * (0.44f * v + 0.78f);
        Vector3 tangent = new Vector3(0.0f, dydv, dzdv).normalized;
        forwardNormal = new Vector3(0.0f, tangent.z, -tangent.y).normalized;
    }

    private static void AddBox(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector2> uvs,
        List<int> tris,
        Vector3 min,
        Vector3 max,
        Vector2 uvMin,
        Vector2 uvMax)
    {
        // Front (+Z)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
            Vector3.forward, uvMin, uvMax);

        // Back (-Z)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            Vector3.back, uvMin, uvMax);

        // Top (+Y)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            Vector3.up, uvMin, uvMax);

        // Bottom (-Y)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            Vector3.down, uvMin, uvMax);

        // Right (+X)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            Vector3.right, uvMin, uvMax);

        // Left (-X)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(min.x, max.y, min.z),
            Vector3.left, uvMin, uvMax);
    }

    private static void AddSlantedStrut(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector2> uvs,
        List<int> tris,
        Vector3 p0,
        Vector3 p1,
        float halfRadius)
    {
        Vector3 dir = (p1 - p0).normalized;
        Vector3 right = Vector3.right * halfRadius;
        Vector3 up = Vector3.Cross(dir, Vector3.right).normalized * halfRadius;

        Vector3 b0 = p0 - right - up;
        Vector3 b1 = p0 + right - up;
        Vector3 b2 = p0 + right + up;
        Vector3 b3 = p0 - right + up;

        Vector3 t0 = p1 - right - up;
        Vector3 t1 = p1 + right - up;
        Vector3 t2 = p1 + right + up;
        Vector3 t3 = p1 - right + up;

        Vector2 uv0 = new Vector2(0.0f, 0.2f);
        Vector2 uv1 = new Vector2(1.0f, 0.7f);

        AddQuad(verts, norms, uvs, tris, b0, b1, t1, t0, -up.normalized, uv0, uv1);
        AddQuad(verts, norms, uvs, tris, b2, b3, t3, t2,  up.normalized, uv0, uv1);
        AddQuad(verts, norms, uvs, tris, b1, b2, t2, t1,  Vector3.right, uv0, uv1);
        AddQuad(verts, norms, uvs, tris, b3, b0, t0, t3,  Vector3.left,  uv0, uv1);
    }

    private static void AddQuad(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector2> uvs,
        List<int> tris,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector3 normal,
        Vector2 uvMin,
        Vector2 uvMax)
    {
        int baseIdx = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
        norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
        uvs.Add(new Vector2(uvMin.x, uvMin.y));
        uvs.Add(new Vector2(uvMax.x, uvMin.y));
        uvs.Add(new Vector2(uvMax.x, uvMax.y));
        uvs.Add(new Vector2(uvMin.x, uvMax.y));

        // Unity時計回り（v0 -> v1 -> v2, v0 -> v2 -> v3）で表面法線が normal と一致
        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }
}
