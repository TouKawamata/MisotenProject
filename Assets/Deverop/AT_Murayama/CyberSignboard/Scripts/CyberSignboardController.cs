using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動的サイバー看板・ホログラムサイネージ（CyberSignboard）制御コンポーネント。
/// ・GPU完結シェーダーとMaterialPropertyBlockにより、DrawCallバッチングを維持しCPU負荷ゼロ（Update不使用）を実現。
/// ・4つの形状（壁面型・空中浮遊型・コーナー曲面型・袖看板型）と枠あり/枠なし表示に対応。
/// ・インスペクター拡張からの1クリックカラープリセット・アスペクト比調整・マテリアル別名保存に対応。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CyberSignboardController : MonoBehaviour
{
    public enum SignboardShape
    {
        [InspectorName("① 壁面フラット型 (Flat Wall)")]
        WallFlat = 0,

        [InspectorName("② 空中浮遊プロジェクター型 (Floating Holo)")]
        FloatingHolo = 1,

        [InspectorName("③ コーナー曲面型 (Curved Corner)")]
        CurvedCorner = 2,

        [InspectorName("④ 袖看板・突出し型 (Blade Sign)")]
        BladeSign = 3
    }

    public enum ColorPreset
    {
        [InspectorName("サイバーシアン (Cyan #00F0FF)")]
        Cyan = 0,

        [InspectorName("ネオンピンク (Pink #FF0055)")]
        NeonPink = 1,

        [InspectorName("警告アンバー (Amber #FF9900)")]
        WarningAmber = 2,

        [InspectorName("マトリクスグリーン (Green #00FF66)")]
        MatrixGreen = 3
    }

    public enum AspectRatioPreset
    {
        [InspectorName("16:9 (横長ワイド)")]
        Ratio_16_9 = 0,

        [InspectorName("4:3 (標準ビジョン)")]
        Ratio_4_3 = 1,

        [InspectorName("1:1 (正方形スクエア)")]
        Ratio_1_1 = 2,

        [InspectorName("9:16 (縦長サイネージ)")]
        Ratio_9_16 = 3,

        [InspectorName("カスタムサイズ")]
        Custom = 4
    }

    [Header("看板形状・アスペクト比")]
    [Tooltip("看板の基本形状")]
    [SerializeField] private SignboardShape _shape = SignboardShape.WallFlat;

    [Tooltip("アスペクト比プリセット")]
    [SerializeField] private AspectRatioPreset _aspectRatio = AspectRatioPreset.Ratio_16_9;

    [Tooltip("看板の横幅（m）")]
    [Min(0.5f)]
    [SerializeField] private float _signWidth = 8.0f;

    [Tooltip("看板の縦幅（m）")]
    [Min(0.5f)]
    [SerializeField] private float _signHeight = 4.5f;

    [Tooltip("外枠・マウント金具・台座を表示するか")]
    [SerializeField] private bool _showFrame = true;

    [Header("テクスチャ・カラー演出（HDR）")]
    [Tooltip("看板に表示する画像テクスチャ（未指定時は内蔵プロシージャルHUDパターンが自動描画されます）")]
    [SerializeField] private Texture2D _mainTexture;

    [ColorUsage(true, true)]
    [Tooltip("基本ネオン発光色（URP Bloom連動）")]
    [SerializeField] private Color _baseColor = new Color(0.0f, 2.5f, 3.8f, 0.9f);

    [Range(0.5f, 10.0f)]
    [Tooltip("HDRエミッション発光強度倍率")]
    [SerializeField] private float _emissionMultiplier = 2.0f;

    [Range(0.0f, 1.0f)]
    [Tooltip("プロシージャル・サイバーHUDオーバーレイ強度（グリッド、警告帯、コーナー金具等）")]
    [SerializeField] private float _cyberOverlayIntensity = 1.0f;

    [Header("GPUアニメーション演出")]
    [Range(0.0f, 1.0f)]
    [Tooltip("デジタルグリッチ歪みの強度")]
    [SerializeField] private float _glitchIntensity = 0.4f;

    [Range(0.0f, 5.0f)]
    [Tooltip("スキャンラインのスクロール速度")]
    [SerializeField] private float _scanlineSpeed = 2.5f;

    [Range(10.0f, 300.0f)]
    [Tooltip("スキャンラインの密度")]
    [SerializeField] private float _scanlineCount = 80.0f;

    [Range(0.0f, 0.08f)]
    [Tooltip("RGB色収差（色ズレ）の距離")]
    [SerializeField] private float _chromaticSplit = 0.015f;

    [Header("コンポーネント参照")]
    [Tooltip("サイバー看板の表示板Renderer")]
    [SerializeField] private Renderer _screenRenderer;

    [Tooltip("外枠・金具・台座オブジェクト")]
    [SerializeField] private GameObject _frameObject;

    [Tooltip("浮遊型プロジェクタービーム光オブジェクト（ある場合）")]
    [SerializeField] private GameObject _beamObject;

    [Header("カスタムマテリアル（指定時は最優先使用）")]
    [Tooltip("手動保存したマテリアルを直接割り当てたい場合に指定")]
    [SerializeField] private Material _customMaterial;

    [Tooltip("別名保存時のファイル名（未入力時は自動生成）")]
    [SerializeField] private string _customSaveName = "";

    // MaterialPropertyBlockキャッシュ（CPU負荷ゼロ & DrawCall維持）
    private MaterialPropertyBlock _mpb;
    private MaterialPropertyBlock _beamMpb;

    // シェーダープロパティIDキャッシュ
    private static readonly int BaseColorId             = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionMultiplierId    = Shader.PropertyToID("_EmissionMultiplier");
    private static readonly int CyberOverlayIntensityId = Shader.PropertyToID("_CyberOverlayIntensity");
    private static readonly int GlitchIntensityId       = Shader.PropertyToID("_GlitchIntensity");
    private static readonly int ScanlineSpeedId         = Shader.PropertyToID("_ScanlineSpeed");
    private static readonly int ScanlineCountId         = Shader.PropertyToID("_ScanlineCount");
    private static readonly int ChromaticSplitId        = Shader.PropertyToID("_ChromaticSplit");
    private static readonly int MainTexId               = Shader.PropertyToID("_MainTex");
    private static readonly int HasCustomTexId          = Shader.PropertyToID("_HasCustomTex");
    private static readonly int AspectRatioId           = Shader.PropertyToID("_AspectRatio");

    // ランタイム動的メッシュ（フォールバック用・OnDestroyで完全破棄）
    private Mesh _runtimeMeshScreen;
    private Mesh _runtimeMeshFrame;
    private Mesh _runtimeMeshBeam;

    public SignboardShape Shape => _shape;
    public AspectRatioPreset AspectRatio => _aspectRatio;
    public float SignWidth => _signWidth;
    public float SignHeight => _signHeight;
    public bool ShowFrame => _showFrame;
    public Color BaseColor => _baseColor;
    public Texture2D MainTexture => _mainTexture;
    public Material CustomMaterial
    {
        get => _customMaterial;
        set
        {
            _customMaterial = value;
            UpdateSignboard();
        }
    }
    public string CustomSaveName
    {
        get => _customSaveName;
        set => _customSaveName = value;
    }

    private void Awake()
    {
        UpdateSignboard();
    }

    private void OnEnable()
    {
        UpdateSignboard();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMeshes();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate中のSetActive警告を避けるため、TransformとMaterialPropertyBlockは即時反映し、
        // SetActiveやMesh差し替えを含む全体更新は安全にスケジュールする
        if (!gameObject.scene.IsValid())
        {
            // プレハブアセット編集時など
            FindChildrenIfNeeded();
            ApplyScaleAndTransform();
            ApplyMaterialProperties();
            return;
        }

        FindChildrenIfNeeded();
        ApplyScaleAndTransform();
        ApplyMaterialProperties();

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
            {
                UpdateSignboard();
            }
        };
    }
#endif

    /// <summary>
    /// ランタイム動的メッシュの安全破棄（メモリリーク完全防止）
    /// </summary>
    private void CleanupRuntimeMeshes()
    {
        if (_runtimeMeshScreen != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMeshScreen);
            else DestroyImmediate(_runtimeMeshScreen);
            _runtimeMeshScreen = null;
        }
        if (_runtimeMeshFrame != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMeshFrame);
            else DestroyImmediate(_runtimeMeshFrame);
            _runtimeMeshFrame = null;
        }
        if (_runtimeMeshBeam != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMeshBeam);
            else DestroyImmediate(_runtimeMeshBeam);
            _runtimeMeshBeam = null;
        }
    }

    /// <summary>
    /// 看板の表示設定・プロパティをRendererおよび外枠に反映（Update不使用・イベント時のみ実行）
    /// </summary>
    public void UpdateSignboard()
    {
        FindChildrenIfNeeded();
        EnsureMeshes();
        ApplyFrameVisibility();
        ApplyScaleAndTransform();
        ApplyMaterialProperties();
    }

    /// <summary>
    /// 子階層のScreen / Frame / Beamを自動探索
    /// </summary>
    private void FindChildrenIfNeeded()
    {
        if (_screenRenderer == null)
        {
            Transform screenT = transform.Find("Screen");
            if (screenT != null)
            {
                _screenRenderer = screenT.GetComponent<Renderer>();
            }
            else
            {
                Renderer[] rends = GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r.gameObject != gameObject && !r.name.Contains("Frame") && !r.name.Contains("Mount") && !r.name.Contains("Base") && !r.name.Contains("Beam"))
                    {
                        _screenRenderer = r;
                        break;
                    }
                }
            }
        }

        if (_frameObject == null)
        {
            Transform frameT = transform.Find("Frame");
            if (frameT == null) frameT = transform.Find("MountArm");
            if (frameT == null) frameT = transform.Find("ProjectorBase");
            if (frameT != null) _frameObject = frameT.gameObject;
        }

        if (_beamObject == null)
        {
            Transform beamT = transform.Find("ProjectorBeam");
            if (beamT != null) _beamObject = beamT.gameObject;
        }
    }

    /// <summary>
    /// 外枠・金具の表示/非表示を切り替え
    /// </summary>
    private void ApplyFrameVisibility()
    {
        if (_frameObject != null && _frameObject.activeSelf != _showFrame)
        {
            _frameObject.SetActive(_showFrame);
        }
        if (_beamObject != null)
        {
            bool shouldShowBeam = _showFrame && (_shape == SignboardShape.FloatingHolo);
            if (_beamObject.activeSelf != shouldShowBeam)
            {
                _beamObject.SetActive(shouldShowBeam);
            }
        }
    }

    /// <summary>
    /// 各形状に応じた適切なメッシュが割り当てられているかを保証（全4形状対応・ランタイムフォールバック付き）
    /// </summary>
    public void EnsureMeshes()
    {
        if (_screenRenderer != null)
        {
            MeshFilter smf = _screenRenderer.GetComponent<MeshFilter>();
            if (smf != null)
            {
                switch (_shape)
                {
                    case SignboardShape.WallFlat:
                        AssignMeshWithFallback(smf, "WallScreenMesh", ref _runtimeMeshScreen, () => CreateWallScreenMesh());
                        break;
                    case SignboardShape.FloatingHolo:
                        AssignMeshWithFallback(smf, "HoloScreenMesh", ref _runtimeMeshScreen, () => CreateHoloScreenMesh());
                        break;
                    case SignboardShape.CurvedCorner:
                        AssignMeshWithFallback(smf, "CurvedSignboardMesh", ref _runtimeMeshScreen, () => CreateCurvedScreenMesh(2.0f, 1.0f, 32));
                        break;
                    case SignboardShape.BladeSign:
                        AssignMeshWithFallback(smf, "BladeScreenMesh", ref _runtimeMeshScreen, () => CreateBladeScreenMesh());
                        break;
                }
            }
        }

        if (_frameObject != null)
        {
            MeshFilter fmf = _frameObject.GetComponent<MeshFilter>();
            if (fmf != null)
            {
                switch (_shape)
                {
                    case SignboardShape.WallFlat:
                        AssignMeshWithFallback(fmf, "WallFrameMesh", ref _runtimeMeshFrame, () => CreateWallFrameMesh());
                        break;
                    case SignboardShape.FloatingHolo:
                        AssignMeshWithFallback(fmf, "HoloBaseMesh", ref _runtimeMeshFrame, () => CreateHoloBaseMesh());
                        break;
                    case SignboardShape.CurvedCorner:
                        AssignMeshWithFallback(fmf, "CurvedFrameMesh", ref _runtimeMeshFrame, () => CreateCurvedFrameMesh(2.0f, 1.0f, 32));
                        break;
                    case SignboardShape.BladeSign:
                        AssignMeshWithFallback(fmf, "BladeFrameMesh", ref _runtimeMeshFrame, () => CreateBladeFrameMesh());
                        break;
                }
            }
        }

        if (_beamObject != null && _shape == SignboardShape.FloatingHolo)
        {
            MeshFilter bmf = _beamObject.GetComponent<MeshFilter>();
            if (bmf != null)
            {
                AssignMeshWithFallback(bmf, "HoloBeamMesh", ref _runtimeMeshBeam, () => CreateHoloBeamMesh());
            }
        }
    }

    private void AssignMeshWithFallback(MeshFilter mf, string expectedAssetName, ref Mesh runtimeCache, System.Func<Mesh> generator)
    {
        string runtimeName = "Runtime_" + expectedAssetName;
        if (mf.sharedMesh != null && (mf.sharedMesh.name == expectedAssetName || mf.sharedMesh.name == runtimeName))
        {
            return;
        }

#if UNITY_EDITOR
        Mesh assetMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Deverop/AT_Murayama/CyberSignboard/Meshes/{expectedAssetName}.asset");
        if (assetMesh != null)
        {
            mf.sharedMesh = assetMesh;
            return;
        }
#endif
        if (runtimeCache == null || runtimeCache.name != runtimeName)
        {
            if (runtimeCache != null)
            {
                if (Application.isPlaying) Destroy(runtimeCache);
                else DestroyImmediate(runtimeCache);
            }
            runtimeCache = generator();
            runtimeCache.name = runtimeName;
            runtimeCache.hideFlags = HideFlags.DontSave;
        }
        mf.sharedMesh = runtimeCache;
    }

    /// <summary>
    /// アスペクト比とサイズを各形状の正規化メッシュ（1m基準）に応じて正確にスケール反映
    /// </summary>
    private void ApplyScaleAndTransform()
    {
        float w = Mathf.Max(0.5f, _signWidth);
        float h = Mathf.Max(0.5f, _signHeight);

        if (_screenRenderer != null)
        {
            Transform sT = _screenRenderer.transform;
            switch (_shape)
            {
                case SignboardShape.WallFlat:
                    // 壁面型: WallScreenMesh自体が Z=0.04m に頂点を持つため localPosition は (0,0,0)。
                    // WallFrameMesh (内枠 ±0.5, Z=0.04) と誤差ゼロで完全一致する。
                    sT.localPosition = Vector3.zero;
                    sT.localScale = new Vector3(w, h, 1.0f);
                    break;

                case SignboardShape.FloatingHolo:
                    // 空中浮遊型: ピボットは看板中心 (0,0,0)
                    sT.localPosition = Vector3.zero;
                    sT.localScale = new Vector3(w, h, 1.0f);
                    break;

                case SignboardShape.CurvedCorner:
                    // コーナー曲面型: ピボットはビルの90度角 (0,0,0)。
                    // 基準半径 R=2.0m（90度円弧長 = π ≒ 3.14159265m）
                    float rScale = w / Mathf.PI;
                    sT.localPosition = Vector3.zero;
                    sT.localScale = new Vector3(rScale, h, rScale);
                    break;

                case SignboardShape.BladeSign:
                    // 袖看板型: ピボットは壁面取付面 (X=0)。
                    // 枠あり時は BladeFrameMesh の外周リム枠中心 (X = 0.85 * w) にピタッと合わせ、
                    // 枠なし（ScreenOnly）時は壁面 X=0 から直接突き出すよう (X = 0.5 * w) に配置する。
                    bool hasActiveFrame = _showFrame && (_frameObject != null);
                    float centerOffsetX = hasActiveFrame ? (0.85f * w) : (0.5f * w);
                    sT.localPosition = new Vector3(centerOffsetX, 0.0f, 0.0f);
                    sT.localScale = new Vector3(w, h, 1.0f);
                    break;
            }
        }

        if (_frameObject != null)
        {
            Transform fT = _frameObject.transform;
            switch (_shape)
            {
                case SignboardShape.WallFlat:
                    // 壁面ベベルフレーム: WallFrameMesh (背面Z=0, 前面Z=0.08, 内枠±0.5, 外枠±0.54)
                    // Screen と同じ (w, h, 1.0f) スケールにすることで、内枠が画面端と完全一致し厚み8cmを維持。
                    fT.localPosition = Vector3.zero;
                    fT.localScale = new Vector3(w, h, 1.0f);
                    break;

                case SignboardShape.FloatingHolo:
                    // 浮遊型プロジェクター台座: 画面下辺 (-h*0.5) の 0.6m 下に配置
                    fT.localPosition = new Vector3(0.0f, -h * 0.5f - 0.6f, 0.0f);
                    fT.localScale = new Vector3(1.2f, 1.0f, 1.2f);
                    break;

                case SignboardShape.CurvedCorner:
                    // コーナー曲面フレーム: スクリーンと同一のスケール・ピボットで上下レール＆柱が完全一致
                    float rScale = w / Mathf.PI;
                    fT.localPosition = Vector3.zero;
                    fT.localScale = new Vector3(rScale, h, rScale);
                    break;

                case SignboardShape.BladeSign:
                    // 袖看板一体型フレーム (BladeFrameMesh: 壁面ベース X=0、アーム X=0.04〜0.35、外周枠 X=0.35〜1.35 [中心0.85])
                    // (w, h, 1.0f) スケールで壁面 X=0 密着と画面外周枠の完全一致を同時に達成し、厚みZも潰れない。
                    fT.localPosition = Vector3.zero;
                    fT.localScale = new Vector3(w, h, 1.0f);
                    break;
            }
        }

        if (_beamObject != null && _shape == SignboardShape.FloatingHolo)
        {
            // HoloBeamMesh は Y=0 (台座) から Y=0.6m (画面下辺) へ広がるメッシュ。
            // Yスケールを 1.0f にすることで、台座 (-h*0.5 - 0.6) から画面下辺 (-h*0.5) へ誤差ゼロで接続される。
            Transform bT = _beamObject.transform;
            bT.localPosition = new Vector3(0.0f, -h * 0.5f - 0.6f, 0.0f);
            bT.localScale = new Vector3(w, 1.0f, 1.0f);
        }
    }

    /// <summary>
    /// MaterialPropertyBlockを用いてマテリアルプロパティを適用（DrawCall維持）
    /// </summary>
    private void ApplyMaterialProperties()
    {
        float w = Mathf.Max(0.5f, _signWidth);
        float h = Mathf.Max(0.5f, _signHeight);

        if (_screenRenderer != null)
        {
            // カスタムマテリアルが明示指定されている場合はそれを優先
            if (_customMaterial != null && _screenRenderer.sharedMaterial != _customMaterial)
            {
                _screenRenderer.sharedMaterial = _customMaterial;
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _screenRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, _baseColor);
            _mpb.SetFloat(EmissionMultiplierId, _emissionMultiplier);
            _mpb.SetFloat(CyberOverlayIntensityId, _cyberOverlayIntensity);
            _mpb.SetFloat(GlitchIntensityId, _glitchIntensity);
            _mpb.SetFloat(ScanlineSpeedId, _scanlineSpeed);
            _mpb.SetFloat(ScanlineCountId, _scanlineCount);
            _mpb.SetFloat(ChromaticSplitId, _chromaticSplit);
            _mpb.SetFloat(HasCustomTexId, _mainTexture != null ? 1.0f : 0.0f);
            _mpb.SetFloat(AspectRatioId, w / h);

            if (_mainTexture != null)
            {
                _mpb.SetTexture(MainTexId, _mainTexture);
            }

            _screenRenderer.SetPropertyBlock(_mpb);
        }

        // プロジェクタービーム光がある場合、独立したMPBで半透明ビーム色を確実に反映
        if (_beamObject != null)
        {
            Renderer beamRend = _beamObject.GetComponent<Renderer>();
            if (beamRend != null)
            {
                if (_beamMpb == null) _beamMpb = new MaterialPropertyBlock();
                beamRend.GetPropertyBlock(_beamMpb);
                Color beamColor = new Color(_baseColor.r * 0.55f, _baseColor.g * 0.55f, _baseColor.b * 0.55f, _baseColor.a * 0.35f);
                _beamMpb.SetColor(BaseColorId, beamColor);
                _beamMpb.SetFloat(EmissionMultiplierId, _emissionMultiplier * 0.65f);
                _beamMpb.SetFloat(CyberOverlayIntensityId, 0.25f);
                _beamMpb.SetFloat(GlitchIntensityId, _glitchIntensity * 0.5f);
                _beamMpb.SetFloat(ScanlineSpeedId, _scanlineSpeed * 1.5f);
                _beamMpb.SetFloat(ScanlineCountId, 40.0f);
                _beamMpb.SetFloat(ChromaticSplitId, _chromaticSplit);
                _beamMpb.SetFloat(HasCustomTexId, 0.0f);
                _beamMpb.SetFloat(AspectRatioId, w / 0.6f);
                beamRend.SetPropertyBlock(_beamMpb);
            }
        }
    }

    /// <summary>
    /// カラープリセットの即時反映
    /// </summary>
    public void ApplyColorPreset(ColorPreset preset)
    {
        switch (preset)
        {
            case ColorPreset.Cyan:
                _baseColor = new Color(0.0f, 2.5f, 3.8f, 0.9f);
                break;
            case ColorPreset.NeonPink:
                _baseColor = new Color(3.8f, 0.15f, 2.0f, 0.9f);
                break;
            case ColorPreset.WarningAmber:
                _baseColor = new Color(3.8f, 1.8f, 0.0f, 0.9f);
                break;
            case ColorPreset.MatrixGreen:
                _baseColor = new Color(0.0f, 3.5f, 1.2f, 0.9f);
                break;
        }
        UpdateSignboard();
    }

    /// <summary>
    /// アスペクト比プリセットの適用
    /// </summary>
    public void SetAspectRatio(AspectRatioPreset preset)
    {
        _aspectRatio = preset;
        switch (preset)
        {
            case AspectRatioPreset.Ratio_16_9:
                if (_shape == SignboardShape.CurvedCorner)
                {
                    _signWidth = 3.14f;
                    _signHeight = 4.5f;
                }
                else
                {
                    _signWidth = 8.0f;
                    _signHeight = 4.5f;
                }
                break;
            case AspectRatioPreset.Ratio_4_3:
                _signWidth = 8.0f;
                _signHeight = 6.0f;
                break;
            case AspectRatioPreset.Ratio_1_1:
                _signWidth = 6.0f;
                _signHeight = 6.0f;
                break;
            case AspectRatioPreset.Ratio_9_16:
                _signWidth = 4.5f;
                _signHeight = 8.0f;
                break;
            case AspectRatioPreset.Custom:
                break;
        }
        UpdateSignboard();
    }

    /// <summary>
    /// 外枠表示フラグの切り替え（袖看板等の位置オフセットも連動更新）
    /// </summary>
    public void SetShowFrame(bool show)
    {
        _showFrame = show;
        ApplyFrameVisibility();
        ApplyScaleAndTransform();
    }

    /// <summary>
    /// 現在のパラメータを焼き込んだ新規マテリアルインスタンスを生成（別名保存用）
    /// </summary>
    public Material CreateConfiguredMaterial()
    {
        Shader shader = Shader.Find("Custom/CyberSignboard/S_CyberSignboard");
        if (shader == null && _screenRenderer != null && _screenRenderer.sharedMaterial != null)
        {
            shader = _screenRenderer.sharedMaterial.shader;
        }
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.SetColor(BaseColorId, _baseColor);
        mat.SetFloat(EmissionMultiplierId, _emissionMultiplier);
        mat.SetFloat(CyberOverlayIntensityId, _cyberOverlayIntensity);
        mat.SetFloat(GlitchIntensityId, _glitchIntensity);
        mat.SetFloat(ScanlineSpeedId, _scanlineSpeed);
        mat.SetFloat(ScanlineCountId, _scanlineCount);
        mat.SetFloat(ChromaticSplitId, _chromaticSplit);
        mat.SetFloat(HasCustomTexId, _mainTexture != null ? 1.0f : 0.0f);
        mat.SetFloat(AspectRatioId, Mathf.Max(0.5f, _signWidth) / Mathf.Max(0.5f, _signHeight));
        if (_mainTexture != null) mat.SetTexture(MainTexId, _mainTexture);

        return mat;
    }

    /// <summary>
    /// カスタムマテリアル割り当てを解除しインスペクター調整に戻す
    /// </summary>
    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateSignboard();
    }

    // ─────────────────────────────────────────────────────────────
    // プロシージャルメッシュ生成（ランタイムフォールバック＆エディタアセット保存共用）
    // ※空中都市で下や裏から見ても面抜けしない完全閉鎖立体メッシュ
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ① 壁面型スクリーンメッシュ（ピボットは背面中心 Z=0、画面は Z=0.04m に配置）
    /// </summary>
    public static Mesh CreateWallScreenMesh()
    {
        Mesh m = new Mesh();
        Vector3[] v = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0.04f),
            new Vector3( 0.5f, -0.5f, 0.04f),
            new Vector3( 0.5f,  0.5f, 0.04f),
            new Vector3(-0.5f,  0.5f, 0.04f)
        };
        Vector3[] n = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        Vector2[] uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        int[] t = new int[] { 0, 2, 1, 0, 3, 2 };

        m.vertices = v;
        m.normals = n;
        m.uv = uv;
        m.triangles = t;
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ① 壁面型フレームメッシュ（ピボット背面中心 Z=0、厚み 0.08m、内枠 ±0.50m、外枠 ±0.54m の完全閉鎖ベベル枠）
    /// </summary>
    public static Mesh CreateWallFrameMesh()
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float w = 0.54f;
        float h = 0.54f;
        float iw = 0.50f;
        float ih = 0.50f;
        float depth = 0.08f;
        float screenZ = 0.04f;

        // 背面プレート (Z = 0, 法線 Vector3.back)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f),
            Vector3.back);

        // 前面内部バックプレート (Z = 0.038f, 画面裏の隙間遮蔽用, 法線 Vector3.forward)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(iw, -ih, 0.038f), new Vector3(-iw, -ih, 0.038f), new Vector3(-iw, ih, 0.038f), new Vector3(iw, ih, 0.038f),
            Vector3.forward);

        // 外周4辺の側面 (Z=0 から Z=depth)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-w, h, 0f), new Vector3(w, h, 0f), new Vector3(w, h, depth), new Vector3(-w, h, depth),
            Vector3.up);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(w, -h, 0f), new Vector3(-w, -h, 0f), new Vector3(-w, -h, depth), new Vector3(w, -h, depth),
            Vector3.down);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(-w, h, depth), new Vector3(-w, -h, depth),
            Vector3.left);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(w, h, 0f), new Vector3(w, -h, 0f), new Vector3(w, -h, depth), new Vector3(w, h, depth),
            Vector3.right);

        // 前面ベベル枠 (外枠 w,Z=depth から 内枠 iw,Z=screenZ へ)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-w, h, depth), new Vector3(w, h, depth), new Vector3(iw, ih, screenZ), new Vector3(-iw, ih, screenZ),
            Vector3.forward);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(w, -h, depth), new Vector3(-w, -h, depth), new Vector3(-iw, -ih, screenZ), new Vector3(iw, -ih, screenZ),
            Vector3.forward);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-w, -h, depth), new Vector3(-w, h, depth), new Vector3(-iw, ih, screenZ), new Vector3(-iw, -ih, screenZ),
            Vector3.forward);
        AddQuad(verts, norms, uvs, tris,
            new Vector3(w, h, depth), new Vector3(w, -h, depth), new Vector3(iw, -ih, screenZ), new Vector3(iw, ih, screenZ),
            Vector3.forward);

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ② 空中浮遊型スクリーン（中心ピボット (0,0,0) のQuad）
    /// </summary>
    public static Mesh CreateHoloScreenMesh()
    {
        Mesh m = new Mesh();
        Vector3[] v = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        Vector3[] n = new Vector3[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        Vector2[] uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        int[] t = new int[] { 0, 2, 1, 0, 3, 2 };

        m.vertices = v;
        m.normals = n;
        m.uv = uv;
        m.triangles = t;
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ② 空中浮遊型プロジェクター台座（上下蓋付き八角形ドローンポッド、下から見ても底抜けしない）
    /// </summary>
    public static Mesh CreateHoloBaseMesh()
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        int sides = 8;
        float r = 0.45f;
        float h = 0.16f;

        for (int i = 0; i < sides; i++)
        {
            float a0 = (i / (float)sides) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;

            Vector3 t0 = new Vector3(r * Mathf.Cos(a0),  h * 0.5f, r * Mathf.Sin(a0));
            Vector3 t1 = new Vector3(r * Mathf.Cos(a1),  h * 0.5f, r * Mathf.Sin(a1));
            Vector3 b0 = new Vector3(r * Mathf.Cos(a0), -h * 0.5f, r * Mathf.Sin(a0));
            Vector3 b1 = new Vector3(r * Mathf.Cos(a1), -h * 0.5f, r * Mathf.Sin(a1));

            // 上面ファン
            AddTri(verts, norms, uvs, tris, new Vector3(0, h * 0.5f, 0), t0, t1, Vector3.up);
            // 底面ファン（下から見上げた際の面抜け防止）
            AddTri(verts, norms, uvs, tris, new Vector3(0, -h * 0.5f, 0), b1, b0, Vector3.down);
            // 側面
            Vector3 sideNorm = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
            AddQuad(verts, norms, uvs, tris, b0, t0, t1, b1, sideNorm);
        }

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ② 空中浮遊型プロジェクタービーム光（台座 Y=0 から スクリーン下辺 Y=0.6m へ広がるクロスファン）
    /// </summary>
    public static Mesh CreateHoloBeamMesh()
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        Vector3 apex = Vector3.zero;
        float targetY = 0.6f;
        float halfW = 0.5f;

        // 前面ビームシート
        AddQuad(verts, norms, uvs, tris,
            apex + new Vector3(-0.08f, 0f, 0f), apex + new Vector3(0.08f, 0f, 0f),
            new Vector3(halfW, targetY, 0f), new Vector3(-halfW, targetY, 0f), Vector3.forward);

        // 立体感を出すクロスビームシート
        AddQuad(verts, norms, uvs, tris,
            apex + new Vector3(0f, 0f, -0.08f), apex + new Vector3(0f, 0f, 0.08f),
            new Vector3(0f, targetY, 0.2f), new Vector3(0f, targetY, -0.2f), Vector3.right);

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ③ コーナー曲面型スクリーンメッシュ（ピボットは直角の頂点原点 (0,0)、半径 radius、高さ height、90度円弧）
    /// </summary>
    public static Mesh CreateCurvedScreenMesh(float radius, float height, int segments)
    {
        Mesh m = new Mesh();
        int vertCount = (segments + 1) * 2;
        Vector3[] v = new Vector3[vertCount];
        Vector3[] n = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        int[] t = new int[segments * 6];

        float halfH = height * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float ratio = (float)i / segments;
            float angle = ratio * Mathf.PI * 0.5f; // 0度 (+X) から 90度 (+Z)
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            float x = radius * cosA;
            float z = radius * sinA;
            Vector3 norm = new Vector3(cosA, 0f, sinA);

            int bIdx = i * 2;
            int tIdx = bIdx + 1;

            v[bIdx] = new Vector3(x, -halfH, z);
            v[tIdx] = new Vector3(x,  halfH, z);

            n[bIdx] = norm;
            n[tIdx] = norm;

            uv[bIdx] = new Vector2(ratio, 0f);
            uv[tIdx] = new Vector2(ratio, 1f);

            if (i < segments)
            {
                int triIdx = i * 6;
                t[triIdx + 0] = bIdx;
                t[triIdx + 1] = tIdx;
                t[triIdx + 2] = bIdx + 2;

                t[triIdx + 3] = bIdx + 2;
                t[triIdx + 4] = tIdx;
                t[triIdx + 5] = tIdx + 2;
            }
        }

        m.vertices = v;
        m.normals = n;
        m.uv = uv;
        m.triangles = t;
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ③ コーナー曲面型フレームメッシュ（上レール・下レール・左右エッジ支柱、全6面閉鎖で下や裏からも抜けない）
    /// </summary>
    public static Mesh CreateCurvedFrameMesh(float radius, float height, int segments)
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float halfH = height * 0.5f;
        float rInner = radius - 0.05f;
        float rOuter = radius + 0.05f;
        float railH = 0.05f;

        // 上レール (完全4面＋両端キャップ閉鎖)
        AddCurvedRailClosed(verts, norms, uvs, tris, rInner, rOuter, halfH, halfH + railH, segments);
        // 下レール (完全4面＋両端キャップ閉鎖)
        AddCurvedRailClosed(verts, norms, uvs, tris, rInner, rOuter, -halfH - railH, -halfH, segments);

        // 左右エッジ柱 (0度側と90度側、完全6面Box)
        AddBox(verts, norms, uvs, tris, new Vector3(radius, 0f, 0f), new Vector3(0.12f, height + railH * 2f, 0.12f));
        AddBox(verts, norms, uvs, tris, new Vector3(0f, 0f, radius), new Vector3(0.12f, height + railH * 2f, 0.12f));

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    private static void AddCurvedRailClosed(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        float rIn, float rOut, float y0, float y1, int segs)
    {
        for (int i = 0; i < segs; i++)
        {
            float a0 = ((float)i / segs) * Mathf.PI * 0.5f;
            float a1 = ((float)(i + 1) / segs) * Mathf.PI * 0.5f;

            float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
            float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

            Vector3 inT0  = new Vector3(rIn * c0,  y1, rIn * s0);
            Vector3 inT1  = new Vector3(rIn * c1,  y1, rIn * s1);
            Vector3 outT0 = new Vector3(rOut * c0, y1, rOut * s0);
            Vector3 outT1 = new Vector3(rOut * c1, y1, rOut * s1);

            Vector3 inB0  = new Vector3(rIn * c0,  y0, rIn * s0);
            Vector3 inB1  = new Vector3(rIn * c1,  y0, rIn * s1);
            Vector3 outB0 = new Vector3(rOut * c0, y0, rOut * s0);
            Vector3 outB1 = new Vector3(rOut * c1, y0, rOut * s1);

            Vector3 nOut = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
            Vector3 nIn  = -nOut;

            // 上面 (Up)
            AddQuad(verts, norms, uvs, tris, outT0, inT0, inT1, outT1, Vector3.up);
            // 下面 (Down - 下から見上げた際の面抜け防止)
            AddQuad(verts, norms, uvs, tris, inB0, outB0, outB1, inB1, Vector3.down);
            // 外側面 (Outer)
            AddQuad(verts, norms, uvs, tris, outB0, outT0, outT1, outB1, nOut);
            // 内側面 (Inner)
            AddQuad(verts, norms, uvs, tris, inB1, inT1, inT0, inB0, nIn);
        }
    }

    /// <summary>
    /// ④ 袖看板型スクリーンメッシュ（両面表示 Quad、中心原点 (0,0,0)、幅 1m × 高さ 1m）
    /// </summary>
    public static Mesh CreateBladeScreenMesh()
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float hW = 0.5f;
        float hH = 0.5f;
        float zOffset = 0.02f;

        // 表面 (+Z)
        verts.Add(new Vector3(-hW, -hH, zOffset));
        verts.Add(new Vector3( hW, -hH, zOffset));
        verts.Add(new Vector3( hW,  hH, zOffset));
        verts.Add(new Vector3(-hW,  hH, zOffset));
        norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        tris.AddRange(new int[] { 0, 2, 1, 0, 3, 2 });

        // 裏面 (-Z) ※文字が反転しないようUVのXを合わせる
        int bStart = verts.Count;
        verts.Add(new Vector3( hW, -hH, -zOffset));
        verts.Add(new Vector3(-hW, -hH, -zOffset));
        verts.Add(new Vector3(-hW,  hH, -zOffset));
        verts.Add(new Vector3( hW,  hH, -zOffset));
        norms.Add(Vector3.back); norms.Add(Vector3.back); norms.Add(Vector3.back); norms.Add(Vector3.back);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        tris.AddRange(new int[] { bStart, bStart + 2, bStart + 1, bStart, bStart + 3, bStart + 2 });

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// ④ 袖看板型フレームメッシュ（壁面ベース金具 X=0 + 2本のアーム + 看板外周リム枠 X=0.35〜1.35 [中心0.85]）
    /// </summary>
    public static Mesh CreateBladeFrameMesh()
    {
        Mesh m = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // 壁面マウントベース (X = 0〜0.04, Y = -0.6〜0.6, Z = -0.1〜0.1)
        AddBox(verts, norms, uvs, tris, new Vector3(0.02f, 0f, 0f), new Vector3(0.04f, 1.2f, 0.2f));

        // 水平アーム2本 (上アーム Y=0.35, 下アーム Y=-0.35, X = 0.04〜0.35)
        AddBox(verts, norms, uvs, tris, new Vector3(0.195f,  0.35f, 0f), new Vector3(0.31f, 0.06f, 0.06f));
        AddBox(verts, norms, uvs, tris, new Vector3(0.195f, -0.35f, 0f), new Vector3(0.31f, 0.06f, 0.06f));

        // 看板本体の金属外枠 (X = 0.35〜1.35 [中心 0.85, 内幅 1.0], Y = -0.52〜0.52 [内高 1.0], 厚み Z=0.08m)
        AddBox(verts, norms, uvs, tris, new Vector3(0.85f,  0.52f, 0f), new Vector3(1.04f, 0.04f, 0.08f)); // 上枠
        AddBox(verts, norms, uvs, tris, new Vector3(0.85f, -0.52f, 0f), new Vector3(1.04f, 0.04f, 0.08f)); // 下枠
        AddBox(verts, norms, uvs, tris, new Vector3(0.33f,  0f, 0f),    new Vector3(0.04f, 1.08f, 0.08f)); // 基部縦枠
        AddBox(verts, norms, uvs, tris, new Vector3(1.37f,  0f, 0f),    new Vector3(0.04f, 1.08f, 0.08f)); // 先端縦枠

        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n)
    {
        int start = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
        norms.Add(n);  norms.Add(n);  norms.Add(n);  norms.Add(n);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        tris.Add(start + 0); tris.Add(start + 2); tris.Add(start + 1);
        tris.Add(start + 0); tris.Add(start + 3); tris.Add(start + 2);
    }

    private static void AddTri(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n)
    {
        int start = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2);
        norms.Add(n);  norms.Add(n);  norms.Add(n);
        uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
        tris.Add(start + 0); tris.Add(start + 2); tris.Add(start + 1);
    }

    private static void AddBox(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
    {
        Vector3 hs = size * 0.5f;
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3(-hs.x, -hs.y,  hs.z), center + new Vector3( hs.x, -hs.y,  hs.z),
            center + new Vector3( hs.x,  hs.y,  hs.z), center + new Vector3(-hs.x,  hs.y,  hs.z), Vector3.forward);
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3( hs.x, -hs.y, -hs.z), center + new Vector3(-hs.x, -hs.y, -hs.z),
            center + new Vector3(-hs.x,  hs.y, -hs.z), center + new Vector3( hs.x,  hs.y, -hs.z), Vector3.back);
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3(-hs.x, -hs.y, -hs.z), center + new Vector3(-hs.x, -hs.y,  hs.z),
            center + new Vector3(-hs.x,  hs.y,  hs.z), center + new Vector3(-hs.x,  hs.y, -hs.z), Vector3.left);
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3( hs.x, -hs.y,  hs.z), center + new Vector3( hs.x, -hs.y, -hs.z),
            center + new Vector3( hs.x,  hs.y, -hs.z), center + new Vector3( hs.x,  hs.y,  hs.z), Vector3.right);
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3(-hs.x,  hs.y,  hs.z), center + new Vector3( hs.x,  hs.y,  hs.z),
            center + new Vector3( hs.x,  hs.y, -hs.z), center + new Vector3(-hs.x,  hs.y, -hs.z), Vector3.up);
        AddQuad(verts, norms, uvs, tris,
            center + new Vector3(-hs.x, -hs.y, -hs.z), center + new Vector3( hs.x, -hs.y, -hs.z),
            center + new Vector3( hs.x, -hs.y,  hs.z), center + new Vector3(-hs.x, -hs.y,  hs.z), Vector3.down);
    }
}
