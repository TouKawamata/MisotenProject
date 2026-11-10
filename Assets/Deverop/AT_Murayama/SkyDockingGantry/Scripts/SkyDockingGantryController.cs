using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 空中ドッキングアーム＆給電プラットフォーム（Sky Docking Gantry）制御コンポーネント。
/// ・100年後の空中都市スーツレースにおける物理的な実体・建築構造物・交通インフラ。
/// ・ビル外壁の取付金具背面（Z=0, Y=0）にルート原点（Pivot: 0,0,0）を配置し、Vキースナップで壁面・テラスに一発密着。
/// ・Update() 不使用（CPU負荷ゼロ）、パラメータ変更時のみ更新。
/// ・3大設置スタイル（壁面カンチレバー型 / テラス据え置き型 / コーナー抱き込み型）に対応。
/// ・プロシージャル警戒帯（ハザードストライプ）および金網グレーチング床の制御を完備。
/// ・デフォルトはコライダーOFF（背景スレスレ通過時の安全設計）。天板のみのBoxColliderを任意で有効化可能。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SkyDockingGantryController : MonoBehaviour
{
    public enum GantryStyle
    {
        [InspectorName("① 壁面カンチレバー型 (Cantilever Wall)")]
        CantileverWall = 0,

        [InspectorName("② テラス据え置き型 (Terrace Deck)")]
        TerraceDeck = 1,

        [InspectorName("③ コーナー抱き込み型 (Corner Wrap)")]
        CornerWrap = 2
    }

    public enum GantryPreset
    {
        [InspectorName("標準カンチレバー (Standard 12m)")]
        StandardCantilever = 0,

        [InspectorName("ロングスパン給電アーム (Long-Reach 24m)")]
        LongReachGantry = 1,

        [InspectorName("テラス・屋上据え置き (Terrace 10m)")]
        TerraceDeck = 2,

        [InspectorName("大型ツイン発着ピット (Heavy Twin Pad 16m)")]
        HeavyTwinPad = 3,

        [InspectorName("カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum ColorTheme
    {
        [InspectorName("Clean White (白塗装 × 黄色警戒帯)")]
        CleanWhite = 0,

        [InspectorName("Industrial Navy (ダークネイビー × 黄色警戒帯)")]
        IndustrialNavy = 1,

        [InspectorName("Construction Orange (セーフティオレンジ × 黒警戒帯)")]
        ConstructionOrange = 2,

        [InspectorName("Grating Metal (透過金網床 × 亜鉛メッキ鋼)")]
        GratingMetal = 3,

        [InspectorName("カスタム (Custom)")]
        Custom = 4
    }

    [Header("設置スタイル ＆ プリセット")]
    [Tooltip("ビルの形状に合わせた設置スタイル（壁面 / テラス床 / コーナー角）")]
    [SerializeField] private GantryStyle _style = GantryStyle.CantileverWall;

    [Tooltip("用途に応じたワンクリックサイズプリセット")]
    [SerializeField] private GantryPreset _preset = GantryPreset.StandardCantilever;

    [Tooltip("カラーテーマ（白亜都市調 / 重工業ネイビー / 保守オレンジ / 金網グレーチング）")]
    [SerializeField] private ColorTheme _colorTheme = ColorTheme.CleanWhite;

    [Header("パラメトリック寸法（m）")]
    [Tooltip("壁面から空中への突き出し長（Reach: 6m 〜 30m）")]
    [Range(6.0f, 30.0f)]
    [SerializeField] private float _reachLength = 12.0f;

    [Tooltip("発着デッキの横幅（Width: 4m 〜 16m）")]
    [Range(4.0f, 16.0f)]
    [SerializeField] private float _deckWidth = 8.0f;

    [Tooltip("発着デッキの前後の奥行き（Depth: 4m 〜 16m）")]
    [Range(4.0f, 16.0f)]
    [SerializeField] private float _deckDepth = 6.0f;

    [Tooltip("デッキ天板の厚み（m）")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float _deckThickness = 0.4f;

    [Tooltip("壁面トラスの垂れ下がり深さ（Drop: 2m 〜 10m）")]
    [Range(2.0f, 10.0f)]
    [SerializeField] private float _cantileverDrop = 4.5f;

    [Header("設備・構造オプション")]
    [Tooltip("安全キャットウォーク手すりを表示するか")]
    [SerializeField] private bool _hasRailing = true;

    [Tooltip("給電・ドッキングコネクタアームを表示するか")]
    [SerializeField] private bool _hasPowerArm = true;

    [Tooltip("床面を金属メッシュ格子（グレーチング透過）にするか")]
    [SerializeField] private bool _hasGrating = false;

    [Tooltip("天板の当たり判定（コライダー）を有効にするか。レース背景用はOFF推奨")]
    [SerializeField] private bool _enableCollider = false;

    [Header("コンポーネント参照")]
    [SerializeField] private Renderer _wallBracketRenderer;
    [SerializeField] private Renderer _trussArmRenderer;
    [SerializeField] private Renderer _platformDeckRenderer;
    [SerializeField] private Renderer _railingRenderer;
    [SerializeField] private Renderer _powerArmRenderer;
    [SerializeField] private BoxCollider _deckCollider;

    [Header("カスタムマテリアル（指定時は最優先）")]
    [Tooltip("別名保存したカスタムマテリアルを直接適用したい場合に指定")]
    [SerializeField] private Material _customMaterial;

    [Tooltip("別名保存時のファイル名（未入力時は自動生成）")]
    [SerializeField] private string _customSaveName = "";

    // MaterialPropertyBlockキャッシュ（CPU負荷ゼロ & DrawCall維持）
    private MaterialPropertyBlock _mpbDeck;
    private MaterialPropertyBlock _mpbFrame;

    // シェーダープロパティIDキャッシュ
    private static readonly int BaseColorId          = Shader.PropertyToID("_BaseColor");
    private static readonly int DarkAccentColorId    = Shader.PropertyToID("_DarkAccentColor");
    private static readonly int MetallicId           = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId         = Shader.PropertyToID("_Smoothness");
    private static readonly int HazardEnableId       = Shader.PropertyToID("_HazardEnable");
    private static readonly int HazardWidthId        = Shader.PropertyToID("_HazardWidth");
    private static readonly int HazardTilingId       = Shader.PropertyToID("_HazardTiling");
    private static readonly int HazardColorAId       = Shader.PropertyToID("_HazardColorA");
    private static readonly int HazardColorBId       = Shader.PropertyToID("_HazardColorB");
    private static readonly int GratingEnableId      = Shader.PropertyToID("_GratingEnable");
    private static readonly int GratingTilingId      = Shader.PropertyToID("_GratingTiling");
    private static readonly int GratingBarThicknessId = Shader.PropertyToID("_GratingBarThickness");
    private static readonly int CutoffId             = Shader.PropertyToID("_Cutoff");
    private static readonly int TreadEnableId        = Shader.PropertyToID("_TreadEnable");
    private static readonly int TreadTilingId        = Shader.PropertyToID("_TreadTiling");
    private static readonly int AspectId             = Shader.PropertyToID("_Aspect");

    // ランタイム動的メッシュ（OnDestroyで安全破棄）
    private Mesh _runtimeMeshBracket;
    private Mesh _runtimeMeshTruss;
    private Mesh _runtimeMeshDeck;
    private Mesh _runtimeMeshRailing;
    private Mesh _runtimeMeshPowerArm;

    public GantryStyle Style => _style;
    public GantryPreset Preset => _preset;
    public ColorTheme CurrentColorTheme => _colorTheme;
    public float ReachLength => _reachLength;
    public float DeckWidth => _deckWidth;
    public float DeckDepth => _deckDepth;
    public float DeckThickness => _deckThickness;
    public float CantileverDrop => _cantileverDrop;
    public bool HasRailing => _hasRailing;
    public bool HasPowerArm => _hasPowerArm;
    public bool HasGrating => _hasGrating;
    public bool EnableCollider => _enableCollider;

    public Material CustomMaterial
    {
        get => _customMaterial;
        set
        {
            _customMaterial = value;
            UpdateGantry();
        }
    }

    public string CustomSaveName
    {
        get => _customSaveName;
        set => _customSaveName = value;
    }

    private void Awake()
    {
        UpdateGantry();
    }

    private void OnEnable()
    {
        UpdateGantry();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMeshes();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid())
        {
            FindChildrenIfNeeded();
            ApplyVisibility();
            ApplyCollider();
            ApplyMaterialProperties();
            return;
        }

        FindChildrenIfNeeded();
        ApplyVisibility();
        ApplyCollider();
        ApplyMaterialProperties();

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
            {
                UpdateGantry();
            }
        };
    }
#endif

    private void CleanupRuntimeMeshes()
    {
        SafeDestroyMesh(ref _runtimeMeshBracket);
        SafeDestroyMesh(ref _runtimeMeshTruss);
        SafeDestroyMesh(ref _runtimeMeshDeck);
        SafeDestroyMesh(ref _runtimeMeshRailing);
        SafeDestroyMesh(ref _runtimeMeshPowerArm);
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

    /// <summary>
    /// 全サブオブジェクト・メッシュ・コライダー・マテリアルを更新（Update不使用）
    /// </summary>
    public void UpdateGantry()
    {
        EnsureHierarchy();
        EnsureMeshes();
        ApplyVisibility();
        ApplyCollider();
        ApplyMaterialProperties();
    }

    public void FindChildrenIfNeeded()
    {
        if (_wallBracketRenderer == null)
        {
            Transform t = transform.Find("WallBracket");
            if (t != null) _wallBracketRenderer = t.GetComponent<Renderer>();
        }
        if (_trussArmRenderer == null)
        {
            Transform t = transform.Find("TrussArm");
            if (t != null) _trussArmRenderer = t.GetComponent<Renderer>();
        }
        if (_platformDeckRenderer == null)
        {
            Transform t = transform.Find("PlatformDeck");
            if (t != null) _platformDeckRenderer = t.GetComponent<Renderer>();
        }
        if (_railingRenderer == null)
        {
            Transform t = transform.Find("Railing");
            if (t != null) _railingRenderer = t.GetComponent<Renderer>();
        }
        if (_powerArmRenderer == null)
        {
            Transform t = transform.Find("PowerArm");
            if (t != null) _powerArmRenderer = t.GetComponent<Renderer>();
        }
        if (_deckCollider == null)
        {
            if (_platformDeckRenderer != null)
            {
                _deckCollider = _platformDeckRenderer.GetComponent<BoxCollider>();
            }
        }
    }

    public void EnsureHierarchy()
    {
        FindChildrenIfNeeded();

        if (_wallBracketRenderer == null || _trussArmRenderer == null ||
            _platformDeckRenderer == null || _railingRenderer == null || _powerArmRenderer == null)
        {
            CreateChildIfMissing("WallBracket", ref _wallBracketRenderer, false);
            CreateChildIfMissing("TrussArm", ref _trussArmRenderer, false);
            CreateChildIfMissing("PlatformDeck", ref _platformDeckRenderer, true);
            CreateChildIfMissing("Railing", ref _railingRenderer, false);
            CreateChildIfMissing("PowerArm", ref _powerArmRenderer, false);

            if (_platformDeckRenderer != null && _deckCollider == null)
            {
                _deckCollider = _platformDeckRenderer.GetComponent<BoxCollider>();
                if (_deckCollider == null)
                {
                    _deckCollider = _platformDeckRenderer.gameObject.AddComponent<BoxCollider>();
                    _deckCollider.enabled = _enableCollider;
                }
            }
        }
    }

    private void CreateChildIfMissing(string childName, ref Renderer rendererRef, bool isDeck)
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
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

#if UNITY_EDITOR
        if (mr.sharedMaterial == null)
        {
            string matName = isDeck ? "M_Gantry_CleanWhite.mat" : "M_Gantry_FrameDark.mat";
            string matPath = $"Assets/Deverop/AT_Murayama/SkyDockingGantry/Materials/{matName}";
            Material defaultMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (defaultMat != null) mr.sharedMaterial = defaultMat;
        }
#endif

        rendererRef = mr;
    }

    public bool IsStandardCantilever()
    {
        return _style == GantryStyle.CantileverWall &&
               Mathf.Approximately(_reachLength, 12.0f) &&
               Mathf.Approximately(_deckWidth, 8.0f) &&
               Mathf.Approximately(_deckDepth, 6.0f) &&
               Mathf.Approximately(_deckThickness, 0.4f) &&
               Mathf.Approximately(_cantileverDrop, 4.5f);
    }

    private void EnsureMeshes()
    {
        bool nonStandard = !IsStandardCantilever();

        // 1. WallBracket Mesh
        if (_wallBracketRenderer != null)
        {
            MeshFilter mf = _wallBracketRenderer.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || nonStandard || mf.sharedMesh.name.StartsWith("Runtime_")))
            {
                SafeDestroyMesh(ref _runtimeMeshBracket);
                _runtimeMeshBracket = CreateWallBracketMesh(_deckWidth, _cantileverDrop, 0.35f, _style);
                _runtimeMeshBracket.name = "Runtime_Gantry_WallBracket";
                mf.sharedMesh = _runtimeMeshBracket;
            }
        }

        // 2. TrussArm Mesh
        if (_trussArmRenderer != null)
        {
            MeshFilter mf = _trussArmRenderer.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || nonStandard || mf.sharedMesh.name.StartsWith("Runtime_")))
            {
                SafeDestroyMesh(ref _runtimeMeshTruss);
                _runtimeMeshTruss = CreateTrussArmMesh(_reachLength, _deckWidth, _cantileverDrop, _style);
                _runtimeMeshTruss.name = "Runtime_Gantry_TrussArm";
                mf.sharedMesh = _runtimeMeshTruss;
            }
        }

        // 3. PlatformDeck Mesh
        if (_platformDeckRenderer != null)
        {
            MeshFilter mf = _platformDeckRenderer.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || nonStandard || mf.sharedMesh.name.StartsWith("Runtime_")))
            {
                SafeDestroyMesh(ref _runtimeMeshDeck);
                float deckStartZ = Mathf.Max(0.5f, _reachLength - _deckDepth);
                _runtimeMeshDeck = CreatePlatformDeckMesh(deckStartZ, _reachLength, _deckWidth, _deckThickness);
                _runtimeMeshDeck.name = "Runtime_Gantry_PlatformDeck";
                mf.sharedMesh = _runtimeMeshDeck;
            }
        }

        // 4. Railing Mesh
        if (_railingRenderer != null)
        {
            MeshFilter mf = _railingRenderer.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || nonStandard || mf.sharedMesh.name.StartsWith("Runtime_")))
            {
                SafeDestroyMesh(ref _runtimeMeshRailing);
                float deckStartZ = Mathf.Max(0.5f, _reachLength - _deckDepth);
                _runtimeMeshRailing = CreateRailingMesh(deckStartZ, _reachLength, _deckWidth, 1.1f);
                _runtimeMeshRailing.name = "Runtime_Gantry_Railing";
                mf.sharedMesh = _runtimeMeshRailing;
            }
        }

        // 5. PowerArm Mesh
        if (_powerArmRenderer != null)
        {
            MeshFilter mf = _powerArmRenderer.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || nonStandard || mf.sharedMesh.name.StartsWith("Runtime_") || mf.sharedMesh.name == "Gantry_WallBracket"))
            {
                SafeDestroyMesh(ref _runtimeMeshPowerArm);
                _runtimeMeshPowerArm = CreatePowerArmMesh(_reachLength, _deckWidth, _deckThickness);
                _runtimeMeshPowerArm.name = "Runtime_Gantry_PowerArm";
                mf.sharedMesh = _runtimeMeshPowerArm;
            }
        }
    }

    private void ApplyVisibility()
    {
        if (_railingRenderer != null)
        {
            _railingRenderer.gameObject.SetActive(_hasRailing);
        }
        if (_powerArmRenderer != null)
        {
            _powerArmRenderer.gameObject.SetActive(_hasPowerArm);
        }
    }

    private void ApplyCollider()
    {
        if (_deckCollider == null && _platformDeckRenderer != null)
        {
            _deckCollider = _platformDeckRenderer.GetComponent<BoxCollider>();
        }

        if (_enableCollider)
        {
            if (_deckCollider == null && _platformDeckRenderer != null)
            {
                _deckCollider = _platformDeckRenderer.gameObject.AddComponent<BoxCollider>();
            }

            if (_deckCollider != null)
            {
                _deckCollider.enabled = true;
                float deckStartZ = Mathf.Max(0.5f, _reachLength - _deckDepth);
                float len = _reachLength - deckStartZ;
                float centerZ = deckStartZ + len * 0.5f;
                _deckCollider.center = new Vector3(0.0f, -_deckThickness * 0.5f, centerZ);
                _deckCollider.size = new Vector3(_deckWidth, _deckThickness, len);
            }
        }
        else
        {
            if (_deckCollider != null)
            {
                _deckCollider.enabled = false;
            }
        }
    }

    public void ApplyMaterialProperties()
    {
        if (_mpbDeck == null) _mpbDeck = new MaterialPropertyBlock();
        if (_mpbFrame == null) _mpbFrame = new MaterialPropertyBlock();

        Color baseCol;
        Color frameCol;
        Color accentCol;
        Color hazA;
        Color hazB;
        float metallic = 0.35f;
        float smoothness = 0.65f;
        float grating = _hasGrating ? 1.0f : 0.0f;

        switch (_colorTheme)
        {
            case ColorTheme.CleanWhite:
                baseCol   = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                frameCol  = new Color(0.90f, 0.92f, 0.94f, 1.0f); // 白亜の未来都市に溶け込むマットホワイト塗装トラス
                accentCol = new Color(0.16f, 0.18f, 0.22f, 1.0f); // 接合部・取付ブラケットのダークアクセント
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f); // Yellow
                hazB      = new Color(0.12f, 0.12f, 0.14f, 1.0f); // Dark Charcoal
                metallic  = 0.30f;
                smoothness = 0.65f;
                break;

            case ColorTheme.IndustrialNavy:
                baseCol   = new Color(0.12f, 0.18f, 0.26f, 1.0f);
                frameCol  = new Color(0.18f, 0.24f, 0.32f, 1.0f);
                accentCol = new Color(0.08f, 0.11f, 0.16f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.08f, 0.10f, 0.12f, 1.0f);
                metallic  = 0.55f;
                smoothness = 0.55f;
                break;

            case ColorTheme.ConstructionOrange:
                baseCol   = new Color(0.88f, 0.40f, 0.05f, 1.0f);
                frameCol  = new Color(0.85f, 0.38f, 0.05f, 1.0f);
                accentCol = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                hazA      = new Color(1.0f, 1.0f, 1.0f, 1.0f);   // White
                hazB      = new Color(0.85f, 0.15f, 0.05f, 1.0f); // Red/Orange
                metallic  = 0.25f;
                smoothness = 0.50f;
                break;

            case ColorTheme.GratingMetal:
                baseCol   = new Color(0.70f, 0.72f, 0.75f, 1.0f);
                frameCol  = new Color(0.65f, 0.68f, 0.72f, 1.0f);
                accentCol = new Color(0.20f, 0.22f, 0.26f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                metallic  = 0.80f;
                smoothness = 0.60f;
                grating   = 1.0f;
                break;

            default:
                baseCol   = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                frameCol  = new Color(0.90f, 0.92f, 0.94f, 1.0f);
                accentCol = new Color(0.16f, 0.18f, 0.22f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                break;
        }

        // 1. Deck Material Property Block (Hazard stripes + Grating/Tread + Aspect Correction)
        if (_platformDeckRenderer != null)
        {
            if (_customMaterial != null)
            {
                _platformDeckRenderer.sharedMaterial = _customMaterial;
            }
            float deckAspect = Mathf.Max(0.05f, _deckDepth / Mathf.Max(0.1f, _deckWidth));

            _platformDeckRenderer.GetPropertyBlock(_mpbDeck);
            _mpbDeck.SetColor(BaseColorId, baseCol);
            _mpbDeck.SetColor(DarkAccentColorId, accentCol);
            _mpbDeck.SetColor(HazardColorAId, hazA);
            _mpbDeck.SetColor(HazardColorBId, hazB);
            _mpbDeck.SetFloat(HazardEnableId, 1.0f);
            _mpbDeck.SetFloat(HazardWidthId, 0.08f);
            _mpbDeck.SetFloat(HazardTilingId, 24.0f);
            _mpbDeck.SetFloat(GratingEnableId, grating);
            _mpbDeck.SetFloat(GratingTilingId, 24.0f);
            _mpbDeck.SetFloat(GratingBarThicknessId, 0.18f);
            _mpbDeck.SetFloat(CutoffId, 0.5f);
            _mpbDeck.SetFloat(TreadEnableId, (grating < 0.5f) ? 1.0f : 0.0f);
            _mpbDeck.SetFloat(TreadTilingId, 36.0f);
            _mpbDeck.SetFloat(AspectId, deckAspect);
            _mpbDeck.SetFloat(MetallicId, metallic);
            _mpbDeck.SetFloat(SmoothnessId, smoothness);
            _platformDeckRenderer.SetPropertyBlock(_mpbDeck);
        }

        // 2. Structural Frame Material Property Block (WallBracket, TrussArm, Railing, PowerArm)
        _mpbFrame.Clear();
        _mpbFrame.SetColor(BaseColorId, frameCol);
        _mpbFrame.SetColor(DarkAccentColorId, accentCol);
        _mpbFrame.SetFloat(HazardEnableId, 0.0f);
        _mpbFrame.SetFloat(GratingEnableId, 0.0f);
        _mpbFrame.SetFloat(TreadEnableId, 0.0f);
        _mpbFrame.SetFloat(MetallicId, 0.55f);
        _mpbFrame.SetFloat(SmoothnessId, 0.60f);

        if (_wallBracketRenderer != null) _wallBracketRenderer.SetPropertyBlock(_mpbFrame);
        if (_trussArmRenderer != null)    _trussArmRenderer.SetPropertyBlock(_mpbFrame);
        if (_railingRenderer != null)     _railingRenderer.SetPropertyBlock(_mpbFrame);
        if (_powerArmRenderer != null)    _powerArmRenderer.SetPropertyBlock(_mpbFrame);
    }

    public void ApplyPreset(GantryPreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case GantryPreset.StandardCantilever:
                _style          = GantryStyle.CantileverWall;
                _reachLength    = 12.0f;
                _deckWidth      = 8.0f;
                _deckDepth      = 6.0f;
                _cantileverDrop = 4.5f;
                _hasRailing     = true;
                _hasPowerArm    = true;
                _hasGrating     = false;
                _colorTheme     = ColorTheme.CleanWhite;
                break;

            case GantryPreset.LongReachGantry:
                _style          = GantryStyle.CantileverWall;
                _reachLength    = 24.0f;
                _deckWidth      = 10.0f;
                _deckDepth      = 8.0f;
                _cantileverDrop = 7.0f;
                _hasRailing     = true;
                _hasPowerArm    = true;
                _hasGrating     = false;
                _colorTheme     = ColorTheme.IndustrialNavy;
                break;

            case GantryPreset.TerraceDeck:
                _style          = GantryStyle.TerraceDeck;
                _reachLength    = 10.0f;
                _deckWidth      = 8.0f;
                _deckDepth      = 6.0f;
                _cantileverDrop = 2.5f;
                _hasRailing     = true;
                _hasPowerArm    = false;
                _hasGrating     = false;
                _colorTheme     = ColorTheme.CleanWhite;
                break;

            case GantryPreset.HeavyTwinPad:
                _style          = GantryStyle.CantileverWall;
                _reachLength    = 16.0f;
                _deckWidth      = 16.0f;
                _deckDepth      = 8.0f;
                _cantileverDrop = 5.5f;
                _hasRailing     = true;
                _hasPowerArm    = true;
                _hasGrating     = true;
                _colorTheme     = ColorTheme.ConstructionOrange;
                break;
        }

        RebuildMeshesFromParameters();
        UpdateGantry();
    }

    public void SetStyle(GantryStyle style)
    {
        _style = style;
        RebuildMeshesFromParameters();
        UpdateGantry();
    }

    public void SetColorTheme(ColorTheme theme)
    {
        _colorTheme = theme;
        if (theme == ColorTheme.GratingMetal) _hasGrating = true;
        UpdateGantry();
    }

    public void SetDimensions(float reach, float width, float depth)
    {
        _reachLength = Mathf.Clamp(reach, 6.0f, 30.0f);
        _deckWidth   = Mathf.Clamp(width, 4.0f, 16.0f);
        _deckDepth   = Mathf.Clamp(depth, 4.0f, 16.0f);
        RebuildMeshesFromParameters();
        UpdateGantry();
    }

    public void SetRailing(bool hasRailing)
    {
        _hasRailing = hasRailing;
        ApplyVisibility();
    }

    public void SetPowerArm(bool hasPowerArm)
    {
        _hasPowerArm = hasPowerArm;
        ApplyVisibility();
    }

    public void SetGrating(bool hasGrating)
    {
        _hasGrating = hasGrating;
        UpdateGantry();
    }

    public void SetEnableCollider(bool enable)
    {
        _enableCollider = enable;
        ApplyCollider();
    }

    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateGantry();
    }

    /// <summary>
    /// 現在のパラメータに合わせてランタイム動的メッシュを完全再生成
    /// </summary>
    public void RebuildMeshesFromParameters()
    {
        CleanupRuntimeMeshes();

        float deckStartZ = Mathf.Max(0.5f, _reachLength - _deckDepth);

        _runtimeMeshBracket  = CreateWallBracketMesh(_deckWidth, _cantileverDrop, 0.35f, _style);
        _runtimeMeshBracket.name = "Runtime_Gantry_WallBracket";

        _runtimeMeshTruss    = CreateTrussArmMesh(_reachLength, _deckWidth, _cantileverDrop, _style);
        _runtimeMeshTruss.name = "Runtime_Gantry_TrussArm";

        _runtimeMeshDeck     = CreatePlatformDeckMesh(deckStartZ, _reachLength, _deckWidth, _deckThickness);
        _runtimeMeshDeck.name = "Runtime_Gantry_PlatformDeck";

        _runtimeMeshRailing  = CreateRailingMesh(deckStartZ, _reachLength, _deckWidth, 1.1f);
        _runtimeMeshRailing.name = "Runtime_Gantry_Railing";

        _runtimeMeshPowerArm = CreatePowerArmMesh(_reachLength, _deckWidth, _deckThickness);
        _runtimeMeshPowerArm.name = "Runtime_Gantry_PowerArm";

        if (_wallBracketRenderer != null)
        {
            var mf = _wallBracketRenderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _runtimeMeshBracket;
        }
        if (_trussArmRenderer != null)
        {
            var mf = _trussArmRenderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _runtimeMeshTruss;
        }
        if (_platformDeckRenderer != null)
        {
            var mf = _platformDeckRenderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _runtimeMeshDeck;
        }
        if (_railingRenderer != null)
        {
            var mf = _railingRenderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _runtimeMeshRailing;
        }
        if (_powerArmRenderer != null)
        {
            var mf = _powerArmRenderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _runtimeMeshPowerArm;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // プロシージャル 3D メッシュ生成アルゴリズム（幾何学的に閉じた完全閉鎖メッシュ）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ① 壁面取付ブラケット（WallBracket）メッシュ生成
    /// ピボット背面（Z=0）で壁面に完全密着する取付ベース板とガセット補強リブ
    /// </summary>
    public static Mesh CreateWallBracketMesh(float deckWidth, float dropHeight, float thickness, GantryStyle style)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Gantry_WallBracket";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int>     tris  = new List<int>();

        float bWidth = Mathf.Max(2.5f, deckWidth * 0.9f);
        float bHeight = dropHeight + 0.6f;
        float halfW = bWidth * 0.5f;

        if (style == GantryStyle.CornerWrap)
        {
            // コーナー抱き込み型：L字に折れ曲がった2面の壁面プレート
            float wingLen = 2.0f;
            // Left wing (X = -halfW to -halfW+wingLen, angled 90 deg along Z)
            AddBox(verts, norms, uvs, tris, new Vector3(-halfW - wingLen * 0.5f, -dropHeight * 0.5f, wingLen * 0.5f), new Vector3(wingLen, bHeight, thickness));
            AddBox(verts, norms, uvs, tris, new Vector3(+halfW + wingLen * 0.5f, -dropHeight * 0.5f, wingLen * 0.5f), new Vector3(wingLen, bHeight, thickness));
            // Center connecting brace
            AddBox(verts, norms, uvs, tris, new Vector3(0.0f, -dropHeight * 0.5f, thickness * 0.5f), new Vector3(bWidth, bHeight, thickness));
        }
        else if (style == GantryStyle.TerraceDeck)
        {
            // テラス据え置き型：床面に伏せるアンカーベースプレート
            float plateDepth = 2.5f;
            AddBox(verts, norms, uvs, tris, new Vector3(0.0f, -0.2f, plateDepth * 0.5f), new Vector3(bWidth, 0.3f, plateDepth));
            // 縦のショートスタンド
            AddBox(verts, norms, uvs, tris, new Vector3(-halfW * 0.8f, 0.0f, 0.4f), new Vector3(0.35f, 0.6f, 0.6f));
            AddBox(verts, norms, uvs, tris, new Vector3(+halfW * 0.8f, 0.0f, 0.4f), new Vector3(0.35f, 0.6f, 0.6f));
        }
        else
        {
            // 壁面カンチレバー型：Z=0に密着する背面板（Back Plate at Z=0 to Z=thickness）
            Vector3 center = new Vector3(0.0f, -dropHeight * 0.5f + 0.1f, thickness * 0.5f);
            AddBox(verts, norms, uvs, tris, center, new Vector3(bWidth, bHeight, thickness));

            // 上部アームマウント突起（左右2箇所）
            float topY = 0.0f;
            AddBox(verts, norms, uvs, tris, new Vector3(-halfW * 0.85f, topY - 0.2f, thickness + 0.25f), new Vector3(0.4f, 0.6f, 0.5f));
            AddBox(verts, norms, uvs, tris, new Vector3(+halfW * 0.85f, topY - 0.2f, thickness + 0.25f), new Vector3(0.4f, 0.6f, 0.5f));

            // 下部トラス斜材マウント突起（左右2箇所）
            float botY = -dropHeight;
            AddBox(verts, norms, uvs, tris, new Vector3(-halfW * 0.7f, botY + 0.2f, thickness + 0.25f), new Vector3(0.45f, 0.65f, 0.5f));
            AddBox(verts, norms, uvs, tris, new Vector3(+halfW * 0.7f, botY + 0.2f, thickness + 0.25f), new Vector3(0.45f, 0.65f, 0.5f));

            // 三角ガセット補強リブ（左右各2枚）
            AddBeam(verts, norms, uvs, tris, new Vector3(-halfW * 0.85f, topY - 0.2f, thickness), new Vector3(-halfW * 0.7f, botY + 0.2f, thickness), 0.1f, 0.25f);
            AddBeam(verts, norms, uvs, tris, new Vector3(+halfW * 0.85f, topY - 0.2f, thickness), new Vector3(+halfW * 0.7f, botY + 0.2f, thickness), 0.1f, 0.25f);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ② 三角カンチレバートラスアーム（TrussArm）メッシュ生成
    /// メイン鋼管ビーム、斜めブレース、横つなぎ梁による機能美トラス構造
    /// </summary>
    public static Mesh CreateTrussArmMesh(float reachLength, float deckWidth, float dropHeight, GantryStyle style)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Gantry_TrussArm";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int>     tris  = new List<int>();

        float halfW = deckWidth * 0.5f;
        float beamSize = 0.28f;
        float braceSize = 0.18f;

        // 1. 上弦主梁（Top Chords: 左右2本、水平に壁面から最先端まで走る）
        Vector3 tlStart = new Vector3(-halfW * 0.85f, -0.2f, 0.2f);
        Vector3 tlEnd   = new Vector3(-halfW * 0.85f, -0.2f, reachLength);
        Vector3 trStart = new Vector3(+halfW * 0.85f, -0.2f, 0.2f);
        Vector3 trEnd   = new Vector3(+halfW * 0.85f, -0.2f, reachLength);

        AddBeam(verts, norms, uvs, tris, tlStart, tlEnd, beamSize, beamSize);
        AddBeam(verts, norms, uvs, tris, trStart, trEnd, beamSize, beamSize);

        // 2. 下弦斜材トラス（Cantilever Bottom Chords: 壁面下部から前上方に伸びて支える）
        float bottomReach = (style == GantryStyle.TerraceDeck) ? reachLength * 0.6f : reachLength * 0.85f;
        float botStartY = (style == GantryStyle.TerraceDeck) ? -0.3f : -dropHeight;

        Vector3 blStart = new Vector3(-halfW * 0.70f, botStartY, 0.2f);
        Vector3 blEnd   = new Vector3(-halfW * 0.85f, -0.25f, bottomReach);
        Vector3 brStart = new Vector3(+halfW * 0.70f, botStartY, 0.2f);
        Vector3 brEnd   = new Vector3(+halfW * 0.85f, -0.25f, bottomReach);

        AddBeam(verts, norms, uvs, tris, blStart, blEnd, beamSize * 1.15f, beamSize * 1.15f);
        AddBeam(verts, norms, uvs, tris, brStart, brEnd, beamSize * 1.15f, beamSize * 1.15f);

        // 3. トラス斜材ブレース（Warren Truss Bay）
        int bayCount = Mathf.Clamp(Mathf.RoundToInt(reachLength / 3.0f), 3, 8);
        for (int i = 0; i <= bayCount; i++)
        {
            float t = (float)i / bayCount;
            float z = Mathf.Lerp(0.5f, reachLength, t);

            // 上部横つなぎ梁（Transverse Tie）
            Vector3 topL = new Vector3(-halfW * 0.85f, -0.2f, z);
            Vector3 topR = new Vector3(+halfW * 0.85f, -0.2f, z);
            AddBeam(verts, norms, uvs, tris, topL, topR, braceSize, braceSize);

            // 下弦との垂直／斜め支柱
            if (z <= bottomReach)
            {
                float tBot = z / bottomReach;
                Vector3 botL = Vector3.Lerp(blStart, blEnd, tBot);
                Vector3 botR = Vector3.Lerp(brStart, brEnd, tBot);

                // 垂直ストラット
                AddBeam(verts, norms, uvs, tris, topL, botL, braceSize, braceSize);
                AddBeam(verts, norms, uvs, tris, topR, botR, braceSize, braceSize);

                // 下部横つなぎ梁
                AddBeam(verts, norms, uvs, tris, botL, botR, braceSize, braceSize);

                // 対角斜めブレース（Diagonal Web Member）
                if (i < bayCount)
                {
                    float nextT = (float)(i + 1) / bayCount;
                    float nextZ = Mathf.Lerp(0.5f, reachLength, nextT);
                    if (nextZ <= bottomReach)
                    {
                        float nextTBot = nextZ / bottomReach;
                        Vector3 nextBotL = Vector3.Lerp(blStart, blEnd, nextTBot);
                        Vector3 nextBotR = Vector3.Lerp(brStart, brEnd, nextTBot);
                        Vector3 nextTopL = new Vector3(-halfW * 0.85f, -0.2f, nextZ);
                        Vector3 nextTopR = new Vector3(+halfW * 0.85f, -0.2f, nextZ);

                        AddBeam(verts, norms, uvs, tris, botL, nextTopL, braceSize * 0.85f, braceSize * 0.85f);
                        AddBeam(verts, norms, uvs, tris, botR, nextTopR, braceSize * 0.85f, braceSize * 0.85f);
                    }
                }
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ③ 発着デッキ天板（PlatformDeck）メッシュ生成
    /// 上面UV (0..1) が正確に外周ハザード帯・グレーチングシェーダーと連動する完全立体プレート
    /// </summary>
    public static Mesh CreatePlatformDeckMesh(float startZ, float endZ, float deckWidth, float thickness)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Gantry_PlatformDeck";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int>     tris  = new List<int>();

        float halfW = deckWidth * 0.5f;
        float yTop = 0.0f;
        float yBot = -thickness;

        // 8頂点（上面4点、底面4点）
        Vector3 v0 = new Vector3(-halfW, yTop, startZ); // Top Back-Left
        Vector3 v1 = new Vector3(+halfW, yTop, startZ); // Top Back-Right
        Vector3 v2 = new Vector3(+halfW, yTop, endZ);   // Top Front-Right
        Vector3 v3 = new Vector3(-halfW, yTop, endZ);   // Top Front-Left

        Vector3 v4 = new Vector3(-halfW, yBot, startZ); // Bot Back-Left
        Vector3 v5 = new Vector3(+halfW, yBot, startZ); // Bot Back-Right
        Vector3 v6 = new Vector3(+halfW, yBot, endZ);   // Bot Front-Right
        Vector3 v7 = new Vector3(-halfW, yBot, endZ);   // Bot Front-Left

        // 1. 上面（Top Face: UVが 0..1 に正規化され、シェーダーの外周判定と完全合致）
        AddFaceWithUV(verts, norms, uvs, tris, v0, v1, v2, v3, Vector3.up,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        // 2. 底面（Bottom Face）
        AddFaceWithUV(verts, norms, uvs, tris, v7, v6, v5, v4, Vector3.down,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));

        // 3. 前面（Front Face: 空中側先端）
        AddFaceWithUV(verts, norms, uvs, tris, v3, v2, v6, v7, Vector3.forward,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));

        // 4. 背面（Back Face: ビル側基部）
        AddFaceWithUV(verts, norms, uvs, tris, v1, v0, v4, v5, Vector3.back,
            new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f));

        // 5. 左面（Left Face）
        AddFaceWithUV(verts, norms, uvs, tris, v0, v3, v7, v4, Vector3.left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));

        // 6. 右面（Right Face）
        AddFaceWithUV(verts, norms, uvs, tris, v2, v1, v5, v6, Vector3.right,
            new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f));

        // デッキ外周の厚みエッジ補強フレーム（下部フランジリム）
        float rimWidth = 0.15f;
        AddSolidBox(verts, norms, uvs, tris, new Vector3(0.0f, yBot - 0.1f, endZ - rimWidth * 0.5f), new Vector3(deckWidth, 0.2f, rimWidth));
        AddSolidBox(verts, norms, uvs, tris, new Vector3(-halfW + rimWidth * 0.5f, yBot - 0.1f, (startZ + endZ) * 0.5f), new Vector3(rimWidth, 0.2f, endZ - startZ));
        AddSolidBox(verts, norms, uvs, tris, new Vector3(+halfW - rimWidth * 0.5f, yBot - 0.1f, (startZ + endZ) * 0.5f), new Vector3(rimWidth, 0.2f, endZ - startZ));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ④ 安全キャットウォーク手すり（Railing）メッシュ生成
    /// 高さ1.1mの人間スケール対比パイプ手すり＋中間バー＋巾木（トーボード）
    /// </summary>
    public static Mesh CreateRailingMesh(float startZ, float endZ, float deckWidth, float railingHeight)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Gantry_Railing";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int>     tris  = new List<int>();

        float halfW = deckWidth * 0.5f - 0.08f;
        float railPipeSize = 0.06f;
        float midPipeSize = 0.04f;
        float postSize = 0.06f;
        float toeHeight = 0.14f;

        float yTop = railingHeight;
        float yMid = railingHeight * 0.52f;

        // 1. トップ手すりパイプ（Top Rail: 左辺、前端、右辺の3方）
        Vector3 tlBack  = new Vector3(-halfW, yTop, startZ);
        Vector3 tlFront = new Vector3(-halfW, yTop, endZ - 0.05f);
        Vector3 trFront = new Vector3(+halfW, yTop, endZ - 0.05f);
        Vector3 trBack  = new Vector3(+halfW, yTop, startZ);

        AddBeam(verts, norms, uvs, tris, tlBack, tlFront, railPipeSize, railPipeSize);
        AddBeam(verts, norms, uvs, tris, tlFront, trFront, railPipeSize, railPipeSize);
        AddBeam(verts, norms, uvs, tris, trFront, trBack, railPipeSize, railPipeSize);

        // 2. 中間安全バー（Mid Rail）
        Vector3 mlBack  = new Vector3(-halfW, yMid, startZ);
        Vector3 mlFront = new Vector3(-halfW, yMid, endZ - 0.05f);
        Vector3 mrFront = new Vector3(+halfW, yMid, endZ - 0.05f);
        Vector3 mrBack  = new Vector3(+halfW, yMid, startZ);

        AddBeam(verts, norms, uvs, tris, mlBack, mlFront, midPipeSize, midPipeSize);
        AddBeam(verts, norms, uvs, tris, mlFront, mrFront, midPipeSize, midPipeSize);
        AddBeam(verts, norms, uvs, tris, mrFront, mrBack, midPipeSize, midPipeSize);

        // 3. 巾木（Toe Board / Kick Plate: 高さ14cm）
        float len = endZ - startZ;
        AddBox(verts, norms, uvs, tris, new Vector3(-halfW, toeHeight * 0.5f, startZ + len * 0.5f), new Vector3(0.03f, toeHeight, len));
        AddBox(verts, norms, uvs, tris, new Vector3(+halfW, toeHeight * 0.5f, startZ + len * 0.5f), new Vector3(0.03f, toeHeight, len));
        AddBox(verts, norms, uvs, tris, new Vector3(0.0f, toeHeight * 0.5f, endZ - 0.05f), new Vector3(deckWidth - 0.16f, toeHeight, 0.03f));

        // 4. 垂直支柱（Vertical Posts: 1.5m〜2.0m間隔で配置）
        int sidePosts = Mathf.Clamp(Mathf.RoundToInt(len / 1.8f), 2, 8);
        for (int i = 0; i <= sidePosts; i++)
        {
            float z = Mathf.Lerp(startZ, endZ - 0.05f, (float)i / sidePosts);
            AddBeam(verts, norms, uvs, tris, new Vector3(-halfW, 0.0f, z), new Vector3(-halfW, yTop, z), postSize, postSize);
            AddBeam(verts, norms, uvs, tris, new Vector3(+halfW, 0.0f, z), new Vector3(+halfW, yTop, z), postSize, postSize);
        }

        int frontPosts = Mathf.Clamp(Mathf.RoundToInt(deckWidth / 2.2f), 2, 6);
        for (int i = 1; i < frontPosts; i++)
        {
            float x = Mathf.Lerp(-halfW, +halfW, (float)i / frontPosts);
            AddBeam(verts, norms, uvs, tris, new Vector3(x, 0.0f, endZ - 0.05f), new Vector3(x, yTop, endZ - 0.05f), postSize, postSize);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ⑤ 給電・ドッキングコネクタアーム（PowerArm）メッシュ生成
    /// プラットフォーム前端で空中に突き出す充電・係留用ロボットアーム
    /// </summary>
    public static Mesh CreatePowerArmMesh(float reachLength, float deckWidth, float deckThickness)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Gantry_PowerArm";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int>     tris  = new List<int>();

        // デッキ前端の中央に設置される台座
        Vector3 basePos = new Vector3(0.0f, 0.1f, reachLength - 0.3f);
        AddBox(verts, norms, uvs, tris, basePos, new Vector3(0.8f, 0.25f, 0.6f));

        // 第1ピボット回転軸シリンダー/ボックス
        Vector3 j1 = basePos + new Vector3(0.0f, 0.35f, 0.0f);
        AddBox(verts, norms, uvs, tris, j1, new Vector3(0.45f, 0.45f, 0.45f));

        // 第1アームブーム（前方斜め上に伸びる）
        Vector3 j2 = j1 + new Vector3(0.0f, 0.7f, 1.2f);
        AddBeam(verts, norms, uvs, tris, j1, j2, 0.22f, 0.22f);
        AddBox(verts, norms, uvs, tris, j2, new Vector3(0.35f, 0.35f, 0.35f));

        // 第2アームブーム（前方斜め下に伸びる）
        Vector3 headPos = j2 + new Vector3(0.0f, -0.3f, 1.4f);
        AddBeam(verts, norms, uvs, tris, j2, headPos, 0.18f, 0.18f);

        // 油圧シリンダーロッド
        Vector3 hydStart = j1 + new Vector3(0.0f, 0.1f, 0.2f);
        Vector3 hydEnd   = j2 + new Vector3(0.0f, -0.15f, -0.3f);
        AddBeam(verts, norms, uvs, tris, hydStart, hydEnd, 0.08f, 0.08f);

        // 給電ドッキングヘッド（磁気カップリング・高圧電極ピン）
        AddBox(verts, norms, uvs, tris, headPos, new Vector3(0.55f, 0.55f, 0.3f));
        AddBox(verts, norms, uvs, tris, headPos + new Vector3(0.0f, 0.0f, 0.25f), new Vector3(0.35f, 0.35f, 0.25f));
        AddBox(verts, norms, uvs, tris, headPos + new Vector3(0.0f, 0.0f, 0.45f), new Vector3(0.15f, 0.15f, 0.2f));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─────────────────────────────────────────────────────────────
    // メッシュ組み立て幾何学ヘルパー
    // ─────────────────────────────────────────────────────────────

    private static void AddFaceWithUV(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        int idx = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
        norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
        uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);

        tris.Add(idx);     tris.Add(idx + 1); tris.Add(idx + 2);
        tris.Add(idx);     tris.Add(idx + 2); tris.Add(idx + 3);
    }

    private static void AddBox(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;

        Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z);
        Vector3 p1 = center + new Vector3(+h.x, -h.y, -h.z);
        Vector3 p2 = center + new Vector3(+h.x, +h.y, -h.z);
        Vector3 p3 = center + new Vector3(-h.x, +h.y, -h.z);

        Vector3 p4 = center + new Vector3(-h.x, -h.y, +h.z);
        Vector3 p5 = center + new Vector3(+h.x, -h.y, +h.z);
        Vector3 p6 = center + new Vector3(+h.x, +h.y, +h.z);
        Vector3 p7 = center + new Vector3(-h.x, +h.y, +h.z);

        // Top (+Y)
        AddFaceWithUV(verts, norms, uvs, tris, p3, p2, p6, p7, Vector3.up,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        // Bot (-Y)
        AddFaceWithUV(verts, norms, uvs, tris, p4, p5, p1, p0, Vector3.down,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
        // Front (+Z)
        AddFaceWithUV(verts, norms, uvs, tris, p7, p6, p5, p4, Vector3.forward,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
        // Back (-Z)
        AddFaceWithUV(verts, norms, uvs, tris, p2, p3, p0, p1, Vector3.back,
            new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f));
        // Left (-X)
        AddFaceWithUV(verts, norms, uvs, tris, p3, p7, p4, p0, Vector3.left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
        // Right (+X)
        AddFaceWithUV(verts, norms, uvs, tris, p6, p2, p1, p5, Vector3.right,
            new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f));
    }

    private static void AddSolidBox(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;

        Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z);
        Vector3 p1 = center + new Vector3(+h.x, -h.y, -h.z);
        Vector3 p2 = center + new Vector3(+h.x, +h.y, -h.z);
        Vector3 p3 = center + new Vector3(-h.x, +h.y, -h.z);

        Vector3 p4 = center + new Vector3(-h.x, -h.y, +h.z);
        Vector3 p5 = center + new Vector3(+h.x, -h.y, +h.z);
        Vector3 p6 = center + new Vector3(+h.x, +h.y, +h.z);
        Vector3 p7 = center + new Vector3(-h.x, +h.y, +h.z);

        Vector2 sUV = new Vector2(-1f, -1f);

        // Top (+Y)
        AddFaceWithUV(verts, norms, uvs, tris, p3, p2, p6, p7, Vector3.up, sUV, sUV, sUV, sUV);
        // Bot (-Y)
        AddFaceWithUV(verts, norms, uvs, tris, p4, p5, p1, p0, Vector3.down, sUV, sUV, sUV, sUV);
        // Front (+Z)
        AddFaceWithUV(verts, norms, uvs, tris, p7, p6, p5, p4, Vector3.forward, sUV, sUV, sUV, sUV);
        // Back (-Z)
        AddFaceWithUV(verts, norms, uvs, tris, p2, p3, p0, p1, Vector3.back, sUV, sUV, sUV, sUV);
        // Left (-X)
        AddFaceWithUV(verts, norms, uvs, tris, p3, p7, p4, p0, Vector3.left, sUV, sUV, sUV, sUV);
        // Right (+X)
        AddFaceWithUV(verts, norms, uvs, tris, p6, p2, p1, p5, Vector3.right, sUV, sUV, sUV, sUV);
    }

    private static void AddBeam(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 start, Vector3 end, float width, float height)
    {
        Vector3 dir = (end - start);
        float len = dir.magnitude;
        if (len < 0.001f) return;
        dir /= len;

        Vector3 up = (Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.92f) ? Vector3.forward : Vector3.up;
        Vector3 right = Vector3.Cross(up, dir).normalized;
        up = Vector3.Cross(dir, right).normalized;

        Vector3 hR = right * (width * 0.5f);
        Vector3 hU = up * (height * 0.5f);

        Vector3 p0 = start - hR - hU;
        Vector3 p1 = start + hR - hU;
        Vector3 p2 = start + hR + hU;
        Vector3 p3 = start - hR + hU;

        Vector3 p4 = end - hR - hU;
        Vector3 p5 = end + hR - hU;
        Vector3 p6 = end + hR + hU;
        Vector3 p7 = end - hR + hU;

        // 4 side faces
        AddFaceWithUV(verts, norms, uvs, tris, p0, p1, p5, p4, -up,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        AddFaceWithUV(verts, norms, uvs, tris, p1, p2, p6, p5, right,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        AddFaceWithUV(verts, norms, uvs, tris, p2, p3, p7, p6, up,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        AddFaceWithUV(verts, norms, uvs, tris, p3, p0, p4, p7, -right,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        // End caps
        AddFaceWithUV(verts, norms, uvs, tris, p3, p2, p1, p0, -dir,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
        AddFaceWithUV(verts, norms, uvs, tris, p4, p5, p6, p7, dir,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
    }
}
