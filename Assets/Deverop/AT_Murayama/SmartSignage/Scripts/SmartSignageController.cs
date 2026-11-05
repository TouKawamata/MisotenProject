using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フロストガラス・スマートサイネージ（SmartSignage）制御コンポーネント。
/// ・100年後のクリーンな空中都市（青空・白亜の超高層建築・空飛ぶスーツレース）のための透過フロストガラスサイネージ。
/// ・URP Forward+ の背景透過ブラー（13タップ・ポアソン核・RGB光学的分散）、鏡面光沢、動的反射光スイープ、微細面取りエッジ発光をGPU完結で実現。
/// ・Update() 不使用（CPU負荷ゼロ）、MaterialPropertyBlock により同一マテリアルでのDrawCallバッチングを完全維持。
/// ・4つの用途プリセット（テレメトリHUD、風況・気象ステーション、レース順位表、ルート航法ガイド）と4つの設置スタイルに対応。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SmartSignageController : MonoBehaviour
{
    public enum SignageStyle
    {
        [InspectorName("① スクリーン単体 (Screen Only / 純浮遊)")]
        ScreenOnly = 0,

        [InspectorName("② エアロフレーム枠付き (Aero Framed / 白亜ベゼル)")]
        AeroFramed = 1,

        [InspectorName("③ 反重力パイロン浮遊型 (Floating Pylons)")]
        FloatingPylons = 2,

        [InspectorName("④ 壁面マウント型 (Wall Mount / ビル外壁取付)")]
        WallMount = 3
    }

    public enum SignagePreset
    {
        [InspectorName("テレメトリHUD (Telemetry HUD - 高度/速度/風速/波形グラフ)")]
        TelemetryHUD = 0,

        [InspectorName("風況・気象ステーション (Wind & Weather Station)")]
        WeatherStation = 1,

        [InspectorName("レース順位表 (Race Leaderboard - ラップ/順位/タイム)")]
        RaceLeaderboard = 2,

        [InspectorName("ルート航法ガイド (Route Guide - ウェイポイント/空路)")]
        RouteGuide = 3,

        [InspectorName("カスタム設定 (Custom)")]
        Custom = 4
    }

    public enum AspectRatioPreset
    {
        [InspectorName("16:9 (横長ワイド - 8.0m x 4.5m)")]
        Ratio_16_9 = 0,

        [InspectorName("21:9 (シネマ超ワイド - 10.5m x 4.5m)")]
        Ratio_21_9 = 1,

        [InspectorName("4:3 (標準HUD - 6.0m x 4.5m)")]
        Ratio_4_3 = 2,

        [InspectorName("1:1 (正方形スクエア - 5.0m x 5.0m)")]
        Ratio_1_1 = 3,

        [InspectorName("9:16 (縦型サイネージ - 4.5m x 8.0m)")]
        Ratio_9_16 = 4,

        [InspectorName("カスタムサイズ")]
        Custom = 5
    }

    public enum UIColorPreset
    {
        [InspectorName("ソフトシアン (Soft Cyan #C8F0FF)")]
        SoftCyan = 0,

        [InspectorName("ピュアホワイト (Pure White #FFFFFF)")]
        PureWhite = 1,

        [InspectorName("ホログラムスカイ (Hologram Sky #70D0FF)")]
        HologramSky = 2,

        [InspectorName("エアロライム (Aero Lime #70FFB8)")]
        AeroLime = 3,

        [InspectorName("サンライズゴールド (Sunrise Gold #FFE080)")]
        SunriseGold = 4
    }

    [Header("設置スタイル・プリセット・サイズ")]
    [Tooltip("サイネージの設置形態（単体浮遊／枠付き／反重力パイロン／壁面固定）")]
    [SerializeField] private SignageStyle _style = SignageStyle.ScreenOnly;

    [Tooltip("機能プリセット（テレメトリHUD、気象、順位表、ルート案内）")]
    [SerializeField] private SignagePreset _preset = SignagePreset.TelemetryHUD;

    [Tooltip("アスペクト比プリセット")]
    [SerializeField] private AspectRatioPreset _aspectRatio = AspectRatioPreset.Ratio_16_9;

    [Tooltip("サイネージの横幅（m）")]
    [Min(0.5f)]
    [SerializeField] private float _signWidth = 8.0f;

    [Tooltip("サイネージの縦幅（m）")]
    [Min(0.5f)]
    [SerializeField] private float _signHeight = 4.5f;

    [Tooltip("外枠・パイロン・マウント金具を表示するか")]
    [SerializeField] private bool _showFrame = true;

    [Header("フロストガラス光学的パラメータ")]
    [Tooltip("URP Opaque Texture による背景フロストガラス透過ブラーを有効化")]
    [SerializeField] private bool _useSceneBlur = true;

    [Range(0.0f, 0.05f)]
    [Tooltip("フロストガラスのぼかし半径")]
    [SerializeField] private float _blurRadius = 0.015f;

    [Range(0.0f, 0.04f)]
    [Tooltip("ガラス面取り部および法線による光学的屈折量")]
    [SerializeField] private float _refractionAmount = 0.008f;

    [Range(0.0f, 1.0f)]
    [Tooltip("サンドブラスト微粒子によるガラス表面のザラつき感")]
    [SerializeField] private float _roughnessNoise = 0.35f;

    [Tooltip("フロストガラス本体の乳白色ティントおよび透過度")]
    [SerializeField] private Color _glassTint = new Color(0.92f, 0.97f, 1.0f, 0.22f);

    [Range(0.0f, 3.0f)]
    [Tooltip("太陽光（メインライト）の鏡面反射光沢（Specular Sheen）")]
    [SerializeField] private float _specularIntensity = 1.3f;

    [Range(4.0f, 128.0f)]
    [Tooltip("鏡面反射の絞り込み（シャープネス）")]
    [SerializeField] private float _specularSmoothness = 64.0f;

    [Tooltip("視野角連動のガラス外周フレネル発光色")]
    [SerializeField] private Color _fresnelColor = new Color(0.75f, 0.95f, 1.0f, 0.85f);

    [Range(1.0f, 8.0f)]
    [Tooltip("フレネル指数の鋭さ")]
    [SerializeField] private float _fresnelPower = 3.2f;

    [Tooltip("丸角パネル外周の面取りベベル発光色")]
    [SerializeField] private Color _edgeGlowColor = new Color(0.85f, 0.97f, 1.0f, 0.9f);

    [Range(0.0f, 3.0f)]
    [Tooltip("丸角パネル外周の面取りベベル発光強度")]
    [SerializeField] private float _edgeGlowIntensity = 1.4f;

    [Range(0.0f, 3.0f)]
    [Tooltip("ガラス表面を流れる反射光ハイライトの移動速度")]
    [SerializeField] private float _sweepSpeed = 0.4f;

    [Range(0.0f, 1.0f)]
    [Tooltip("ガラス表面を流れる反射光ハイライトの強度")]
    [SerializeField] private float _sweepIntensity = 0.35f;

    [Header("UI テレメトリ表示パラメータ")]
    [Tooltip("表示するUIテレメトリテクスチャ（未指定時はプロシージャルHUDが自動描画されます）")]
    [SerializeField] private Texture2D _mainTexture;

    [ColorUsage(true, true)]
    [Tooltip("UI文字・グラフィックの発光アクセントカラー（URP Bloom連動 HDR）")]
    [SerializeField] private Color _uiColor = new Color(0.85f, 0.97f, 1.0f, 1.0f);

    [Range(0.5f, 6.0f)]
    [Tooltip("UI発光強度倍率（高輝度で美しいブルームを発生）")]
    [SerializeField] private float _uiIntensity = 2.4f;

    [Range(0.0f, 1.0f)]
    [Tooltip("UI文字・図形の不透明度")]
    [SerializeField] private float _uiOpacity = 0.95f;

    [Tooltip("テレメトリ波形グラフの動的リアルタイムアニメーション")]
    [SerializeField] private bool _dynamicTelemetry = true;

    [Range(0.0f, 5.0f)]
    [Tooltip("テレメトリ波形のパルス速度")]
    [SerializeField] private float _graphWaveSpeed = 1.8f;

    [Range(0.0f, 0.05f)]
    [Tooltip("テレメトリ波形の振幅")]
    [SerializeField] private float _graphWaveAmp = 0.008f;

    [Header("コンポーネント参照")]
    [Tooltip("フロストガラス表示板のRenderer")]
    [SerializeField] private Renderer _screenRenderer;

    [Tooltip("外枠・パイロン・金具オブジェクト（ある場合）")]
    [SerializeField] private GameObject _frameObject;

    [Header("カスタムマテリアル（指定時は最優先使用）")]
    [Tooltip("手動保存したマテリアルを直接割り当てたい場合に指定")]
    [SerializeField] private Material _customMaterial;

    [Tooltip("別名保存時のファイル名（未入力時は自動生成）")]
    [SerializeField] private string _customSaveName = "";

    // MaterialPropertyBlockキャッシュ（CPU負荷ゼロ & DrawCall維持）
    private MaterialPropertyBlock _mpb;

    // シェーダープロパティIDキャッシュ
    private static readonly int UseSceneBlurId       = Shader.PropertyToID("_UseSceneBlur");
    private static readonly int BlurRadiusId         = Shader.PropertyToID("_BlurRadius");
    private static readonly int RefractionAmountId   = Shader.PropertyToID("_RefractionAmount");
    private static readonly int RoughnessNoiseId     = Shader.PropertyToID("_RoughnessNoise");
    private static readonly int GlassTintId          = Shader.PropertyToID("_GlassTint");
    private static readonly int SpecularIntensityId  = Shader.PropertyToID("_SpecularIntensity");
    private static readonly int SpecularSmoothnessId = Shader.PropertyToID("_SpecularSmoothness");
    private static readonly int FresnelColorId       = Shader.PropertyToID("_FresnelColor");
    private static readonly int FresnelPowerId       = Shader.PropertyToID("_FresnelPower");
    private static readonly int EdgeGlowColorId      = Shader.PropertyToID("_EdgeGlowColor");
    private static readonly int EdgeGlowIntensityId  = Shader.PropertyToID("_EdgeGlowIntensity");
    private static readonly int SweepSpeedId         = Shader.PropertyToID("_SweepSpeed");
    private static readonly int SweepIntensityId     = Shader.PropertyToID("_SweepIntensity");
    private static readonly int MainTexId            = Shader.PropertyToID("_MainTex");
    private static readonly int HasCustomTexId       = Shader.PropertyToID("_HasCustomTex");
    private static readonly int AspectRatioId        = Shader.PropertyToID("_AspectRatio");
    private static readonly int UIColorId            = Shader.PropertyToID("_UIColor");
    private static readonly int UIIntensityId        = Shader.PropertyToID("_UIIntensity");
    private static readonly int UIOpacityId          = Shader.PropertyToID("_UIOpacity");
    private static readonly int DynamicTelemetryId   = Shader.PropertyToID("_DynamicTelemetry");
    private static readonly int GraphWaveSpeedId     = Shader.PropertyToID("_GraphWaveSpeed");
    private static readonly int GraphWaveAmpId       = Shader.PropertyToID("_GraphWaveAmp");

    // ランタイム動的メッシュ（フォールバック用・OnDestroyで完全破棄）
    private Mesh _runtimeMeshScreen;
    private Mesh _runtimeMeshFrame;

    public SignageStyle Style => _style;
    public SignagePreset Preset => _preset;
    public AspectRatioPreset AspectRatio => _aspectRatio;
    public float SignWidth => _signWidth;
    public float SignHeight => _signHeight;
    public bool ShowFrame => _showFrame;
    public Color UIColor => _uiColor;
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
        if (!gameObject.scene.IsValid())
        {
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
    }

    /// <summary>
    /// サイネージの全表示設定・メッシュ・マテリアルプロパティを反映（Update不使用）
    /// </summary>
    public void UpdateSignboard()
    {
        FindChildrenIfNeeded();
        EnsureMeshes();
        ApplyFrameVisibility();
        ApplyScaleAndTransform();
        ApplyMaterialProperties();
    }

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
                    if (r.gameObject != gameObject && !r.name.Contains("Frame") && !r.name.Contains("Pylon") && !r.name.Contains("Mount"))
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
            if (frameT == null) frameT = transform.Find("Pylons");
            if (frameT == null) frameT = transform.Find("MountArm");
            if (frameT != null) _frameObject = frameT.gameObject;
        }
    }

    private void ApplyFrameVisibility()
    {
        if (_frameObject != null)
        {
            bool shouldShow = _showFrame && (_style != SignageStyle.ScreenOnly);
            if (_frameObject.activeSelf != shouldShow)
            {
                _frameObject.SetActive(shouldShow);
            }
        }
    }

    public void EnsureMeshes()
    {
        if (_screenRenderer != null)
        {
            MeshFilter smf = _screenRenderer.GetComponent<MeshFilter>();
            if (smf != null)
            {
                AssignMeshWithFallback(smf, "SmartSign_Screen_RoundedRect", ref _runtimeMeshScreen, () => CreateRoundedScreenMesh(1.0f, 1.0f, 0.08f, 0.02f));
            }
        }

        if (_frameObject != null && _style != SignageStyle.ScreenOnly)
        {
            MeshFilter fmf = _frameObject.GetComponent<MeshFilter>();
            if (fmf != null)
            {
                switch (_style)
                {
                    case SignageStyle.AeroFramed:
                        AssignMeshWithFallback(fmf, "SmartSign_AeroFrame", ref _runtimeMeshFrame, () => CreateAeroFrameMesh(1.0f, 1.0f, 0.08f, 0.04f, 0.035f));
                        break;
                    case SignageStyle.FloatingPylons:
                        AssignMeshWithFallback(fmf, "SmartSign_FloatingPylons", ref _runtimeMeshFrame, () => CreateFloatingPylonsMesh(1.0f, 1.0f));
                        break;
                    case SignageStyle.WallMount:
                        AssignMeshWithFallback(fmf, "SmartSign_WallMount", ref _runtimeMeshFrame, () => CreateWallMountMesh(1.0f, 1.0f));
                        break;
                }
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
        Mesh assetMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Deverop/AT_Murayama/SmartSignage/Meshes/{expectedAssetName}.asset");
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

    private void ApplyScaleAndTransform()
    {
        if (_screenRenderer != null)
        {
            Vector3 screenPos = Vector3.zero;
            if (_style == SignageStyle.WallMount)
            {
                // 壁面マウント時はマウントベースから手前(+Z)に突き出す
                screenPos = new Vector3(0f, 0f, 0.6f);
            }
            _screenRenderer.transform.localPosition = screenPos;
            _screenRenderer.transform.localScale = new Vector3(_signWidth, _signHeight, 1.0f);
        }

        if (_frameObject != null)
        {
            Vector3 framePos = Vector3.zero;
            if (_style == SignageStyle.WallMount)
            {
                framePos = Vector3.zero; // ピボットが壁面との接合点
            }
            _frameObject.transform.localPosition = framePos;
            _frameObject.transform.localScale = new Vector3(_signWidth, _signHeight, 1.0f);
        }
    }

    public void ApplyMaterialProperties()
    {
        if (_screenRenderer == null) return;

        if (_customMaterial != null)
        {
            if (_screenRenderer.sharedMaterial != _customMaterial)
            {
                _screenRenderer.sharedMaterial = _customMaterial;
            }
            return;
        }

        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }

        _screenRenderer.GetPropertyBlock(_mpb);

        _mpb.SetFloat(UseSceneBlurId, _useSceneBlur ? 1.0f : 0.0f);
        _mpb.SetFloat(BlurRadiusId, _blurRadius);
        _mpb.SetFloat(RefractionAmountId, _refractionAmount);
        _mpb.SetFloat(RoughnessNoiseId, _roughnessNoise);
        _mpb.SetColor(GlassTintId, _glassTint);

        _mpb.SetFloat(SpecularIntensityId, _specularIntensity);
        _mpb.SetFloat(SpecularSmoothnessId, _specularSmoothness);
        _mpb.SetColor(FresnelColorId, _fresnelColor);
        _mpb.SetFloat(FresnelPowerId, _fresnelPower);
        _mpb.SetColor(EdgeGlowColorId, _edgeGlowColor);
        _mpb.SetFloat(EdgeGlowIntensityId, _edgeGlowIntensity);

        _mpb.SetFloat(SweepSpeedId, _sweepSpeed);
        _mpb.SetFloat(SweepIntensityId, _sweepIntensity);

        if (_mainTexture != null)
        {
            _mpb.SetTexture(MainTexId, _mainTexture);
            _mpb.SetFloat(HasCustomTexId, 1.0f);
        }
        else
        {
            _mpb.SetFloat(HasCustomTexId, 0.0f);
        }

        float aspect = (_signHeight > 0.001f) ? (_signWidth / _signHeight) : 1.7777f;
        _mpb.SetFloat(AspectRatioId, aspect);

        _mpb.SetColor(UIColorId, _uiColor);
        _mpb.SetFloat(UIIntensityId, _uiIntensity);
        _mpb.SetFloat(UIOpacityId, _uiOpacity);

        _mpb.SetFloat(DynamicTelemetryId, _dynamicTelemetry ? 1.0f : 0.0f);
        _mpb.SetFloat(GraphWaveSpeedId, _graphWaveSpeed);
        _mpb.SetFloat(GraphWaveAmpId, _graphWaveAmp);

        _screenRenderer.SetPropertyBlock(_mpb);
    }

    // ─────────────────────────────────────────────────────────────
    // プリセット適用 API
    // ─────────────────────────────────────────────────────────────

    public void ApplyPreset(SignagePreset preset)
    {
        _preset = preset;
        switch (preset)
        {
            case SignagePreset.TelemetryHUD:
                _uiColor = new Color(0.85f, 0.97f, 1.0f, 1.0f); // Soft Cyan
                _uiIntensity = 2.4f;
                _glassTint = new Color(0.92f, 0.97f, 1.0f, 0.22f);
                _blurRadius = 0.015f;
                _refractionAmount = 0.008f;
                _dynamicTelemetry = true;
                LoadPresetTexture("Tex_SmartSign_TelemetryHUD");
                break;

            case SignagePreset.WeatherStation:
                _uiColor = new Color(0.70f, 0.92f, 1.0f, 1.0f); // Sky Blue
                _uiIntensity = 2.2f;
                _glassTint = new Color(0.88f, 0.95f, 1.0f, 0.25f);
                _blurRadius = 0.018f;
                _refractionAmount = 0.007f;
                _dynamicTelemetry = true;
                LoadPresetTexture("Tex_SmartSign_WeatherStation");
                break;

            case SignagePreset.RaceLeaderboard:
                _uiColor = new Color(1.0f, 1.0f, 1.0f, 1.0f); // Pure White & Gold
                _uiIntensity = 2.6f;
                _glassTint = new Color(0.95f, 0.97f, 1.0f, 0.20f);
                _blurRadius = 0.012f;
                _refractionAmount = 0.006f;
                _dynamicTelemetry = false;
                LoadPresetTexture("Tex_SmartSign_RaceLeaderboard");
                break;

            case SignagePreset.RouteGuide:
                _uiColor = new Color(0.65f, 1.0f, 0.85f, 1.0f); // Aero Emerald
                _uiIntensity = 2.5f;
                _glassTint = new Color(0.90f, 0.98f, 0.98f, 0.24f);
                _blurRadius = 0.016f;
                _refractionAmount = 0.009f;
                _dynamicTelemetry = true;
                LoadPresetTexture("Tex_SmartSign_RouteGuide");
                break;
        }
        UpdateSignboard();
    }

    private void LoadPresetTexture(string texName)
    {
#if UNITY_EDITOR
        string path = $"Assets/Deverop/AT_Murayama/SmartSignage/Textures/{texName}.png";
        Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            _mainTexture = tex;
        }
#endif
    }

    public void ApplyStyle(SignageStyle style)
    {
        _style = style;
        UpdateSignboard();
    }

    public void SetAspectRatio(AspectRatioPreset ratioPreset)
    {
        _aspectRatio = ratioPreset;
        switch (ratioPreset)
        {
            case AspectRatioPreset.Ratio_16_9:
                _signWidth = 8.0f;
                _signHeight = 4.5f;
                break;
            case AspectRatioPreset.Ratio_21_9:
                _signWidth = 10.5f;
                _signHeight = 4.5f;
                break;
            case AspectRatioPreset.Ratio_4_3:
                _signWidth = 6.0f;
                _signHeight = 4.5f;
                break;
            case AspectRatioPreset.Ratio_1_1:
                _signWidth = 5.0f;
                _signHeight = 5.0f;
                break;
            case AspectRatioPreset.Ratio_9_16:
                _signWidth = 4.5f;
                _signHeight = 8.0f;
                break;
        }
        UpdateSignboard();
    }

    public void ApplyUIColor(UIColorPreset colorPreset)
    {
        switch (colorPreset)
        {
            case UIColorPreset.SoftCyan:
                _uiColor = new Color(0.78f, 0.94f, 1.0f, 1.0f);
                break;
            case UIColorPreset.PureWhite:
                _uiColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case UIColorPreset.HologramSky:
                _uiColor = new Color(0.44f, 0.82f, 1.0f, 1.0f);
                break;
            case UIColorPreset.AeroLime:
                _uiColor = new Color(0.44f, 1.0f, 0.72f, 1.0f);
                break;
            case UIColorPreset.SunriseGold:
                _uiColor = new Color(1.0f, 0.88f, 0.50f, 1.0f);
                break;
        }
        UpdateSignboard();
    }

    public void SetShowFrame(bool show)
    {
        _showFrame = show;
        ApplyFrameVisibility();
    }

    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateSignboard();
    }

    public Material CreateConfiguredMaterial()
    {
        Shader shader = Shader.Find("Custom/SmartSignage/S_FrostedGlassSignboard");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.name = string.IsNullOrWhiteSpace(_customSaveName) ? $"M_SmartSign_{gameObject.name}" : _customSaveName;

        mat.SetFloat(UseSceneBlurId, _useSceneBlur ? 1.0f : 0.0f);
        mat.SetFloat(BlurRadiusId, _blurRadius);
        mat.SetFloat(RefractionAmountId, _refractionAmount);
        mat.SetFloat(RoughnessNoiseId, _roughnessNoise);
        mat.SetColor(GlassTintId, _glassTint);

        mat.SetFloat(SpecularIntensityId, _specularIntensity);
        mat.SetFloat(SpecularSmoothnessId, _specularSmoothness);
        mat.SetColor(FresnelColorId, _fresnelColor);
        mat.SetFloat(FresnelPowerId, _fresnelPower);
        mat.SetColor(EdgeGlowColorId, _edgeGlowColor);
        mat.SetFloat(EdgeGlowIntensityId, _edgeGlowIntensity);

        mat.SetFloat(SweepSpeedId, _sweepSpeed);
        mat.SetColor(UIColorId, _uiColor);
        mat.SetFloat(UIIntensityId, _uiIntensity);
        mat.SetFloat(UIOpacityId, _uiOpacity);

        if (_mainTexture != null)
        {
            mat.SetTexture(MainTexId, _mainTexture);
            mat.SetFloat(HasCustomTexId, 1.0f);
        }

        float aspect = (_signHeight > 0.001f) ? (_signWidth / _signHeight) : 1.7777f;
        mat.SetFloat(AspectRatioId, aspect);
        mat.SetFloat(DynamicTelemetryId, _dynamicTelemetry ? 1.0f : 0.0f);
        mat.SetFloat(Shader.PropertyToID("_Cull"), 2.0f);
        mat.SetFloat(GraphWaveSpeedId, _graphWaveSpeed);
        mat.SetFloat(GraphWaveAmpId, _graphWaveAmp);

        return mat;
    }

    // ─────────────────────────────────────────────────────────────
    // プロシージャルメッシュ生成エンジン（丸角フロストガラス・ベゼル・パイロン）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 丸角（フィレット）と面取り（ベベル）を持つフロストガラスパネルメッシュを生成（中心基準 1m x 1m）
    /// </summary>
    public static Mesh CreateRoundedScreenMesh(float width = 1.0f, float height = 1.0f, float cornerRadius = 0.08f, float thickness = 0.02f)
    {
        Mesh mesh = new Mesh { name = "SmartSign_Screen_RoundedRect" };
        int cornerSegments = 8;
        float hx = width * 0.5f;
        float hy = height * 0.5f;
        float r = Mathf.Min(cornerRadius, Mathf.Min(hx, hy) * 0.45f);
        float halfT = thickness * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // 輪郭（Perimeter）ポイントを生成（反時計回り）
        List<Vector2> contour = new List<Vector2>();

        // 角中心座標: 0:右下(+x,-y), 1:右上(+x,+y), 2:左上(-x,+y), 3:左下(-x,-y)
        Vector2[] cornerCenters = new Vector2[]
        {
            new Vector2(hx - r, -hy + r),
            new Vector2(hx - r, hy - r),
            new Vector2(-hx + r, hy - r),
            new Vector2(-hx + r, -hy + r)
        };
        float[] startAngles = new float[] { -90f, 0f, 90f, 180f };

        for (int c = 0; c < 4; c++)
        {
            Vector2 cc = cornerCenters[c];
            float baseAngle = startAngles[c];
            for (int s = 0; s < cornerSegments; s++)
            {
                float ang = (baseAngle + (90f * s / cornerSegments)) * Mathf.Deg2Rad;
                contour.Add(new Vector2(cc.x + Mathf.Cos(ang) * r, cc.y + Mathf.Sin(ang) * r));
            }
        }
        int numP = contour.Count;

        // 1. 前面（Front Face: z = -halfT, normal = (0,0,-1)）
        int frontCenterIdx = verts.Count;
        verts.Add(new Vector3(0f, 0f, -halfT));
        norms.Add(new Vector3(0f, 0f, -1f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        int frontRimStart = verts.Count;
        for (int i = 0; i < numP; i++)
        {
            Vector2 p = contour[i];
            verts.Add(new Vector3(p.x, p.y, -halfT));
            norms.Add(new Vector3(0f, 0f, -1f));
            uvs.Add(new Vector2((p.x + hx) / width, (p.y + hy) / height));
        }

        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            tris.Add(frontCenterIdx);
            tris.Add(frontRimStart + next);
            tris.Add(frontRimStart + i);
        }

        // 2. 背面（Back Face: z = +halfT, normal = (0,0,1)）
        int backCenterIdx = verts.Count;
        verts.Add(new Vector3(0f, 0f, halfT));
        norms.Add(new Vector3(0f, 0f, 1f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        int backRimStart = verts.Count;
        for (int i = 0; i < numP; i++)
        {
            Vector2 p = contour[i];
            verts.Add(new Vector3(p.x, p.y, halfT));
            norms.Add(new Vector3(0f, 0f, 1f));
            // 背面はUVを左右反転して裏からも正しく読めるようにマッピング
            uvs.Add(new Vector2(1.0f - (p.x + hx) / width, (p.y + hy) / height));
        }

        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            tris.Add(backCenterIdx);
            tris.Add(backRimStart + i);
            tris.Add(backRimStart + next);
        }

        // 3. 面取り側面（Side Bevel Rim: 法線が外周に向かい光を集約）
        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            Vector2 p0 = contour[i];
            Vector2 p1 = contour[next];

            Vector2 edge = (p1 - p0).normalized;
            Vector3 sideNorm = new Vector3(edge.y, -edge.x, 0f).normalized;

            int v0 = verts.Count;
            verts.Add(new Vector3(p0.x, p0.y, -halfT));
            verts.Add(new Vector3(p1.x, p1.y, -halfT));
            verts.Add(new Vector3(p1.x, p1.y, halfT));
            verts.Add(new Vector3(p0.x, p0.y, halfT));

            norms.Add(sideNorm);
            norms.Add(sideNorm);
            norms.Add(sideNorm);
            norms.Add(sideNorm);

            uvs.Add(new Vector2((p0.x + hx) / width, 0f));
            uvs.Add(new Vector2((p1.x + hx) / width, 0f));
            uvs.Add(new Vector2((p1.x + hx) / width, 1f));
            uvs.Add(new Vector2((p0.x + hx) / width, 1f));

            tris.Add(v0);
            tris.Add(v0 + 1);
            tris.Add(v0 + 2);

            tris.Add(v0);
            tris.Add(v0 + 2);
            tris.Add(v0 + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 丸角スクリーンにぴったり沿う完全閉鎖3Dエアロダイナミック外枠ベゼルメッシュを生成（前面・背面・外側面・内側面完備）
    /// </summary>
    public static Mesh CreateAeroFrameMesh(float width = 1.0f, float height = 1.0f, float cornerRadius = 0.08f, float bezelWidth = 0.04f, float thickness = 0.035f)
    {
        Mesh mesh = new Mesh { name = "SmartSign_AeroFrame" };
        int cornerSegments = 8;
        float hxIn = width * 0.5f;
        float hyIn = height * 0.5f;
        float rIn  = Mathf.Min(cornerRadius, Mathf.Min(hxIn, hyIn) * 0.45f);

        float hxOut = hxIn + bezelWidth;
        float hyOut = hyIn + bezelWidth;
        float rOut  = rIn + bezelWidth;
        float halfT = thickness * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        List<Vector2> inLoop  = GenerateContour(hxIn, hyIn, rIn, cornerSegments);
        List<Vector2> outLoop = GenerateContour(hxOut, hyOut, rOut, cornerSegments);
        int numP = inLoop.Count;

        // 1. 前面ベゼルリング (z = -halfT, normal = (0, 0, -1))
        int frontBaseIdx = verts.Count;
        for (int i = 0; i < numP; i++)
        {
            Vector2 pIn = inLoop[i];
            Vector2 pOut = outLoop[i];

            verts.Add(new Vector3(pIn.x, pIn.y, -halfT));
            norms.Add(new Vector3(0f, 0f, -1f));
            uvs.Add(new Vector2(0f, (float)i / numP));

            verts.Add(new Vector3(pOut.x, pOut.y, -halfT));
            norms.Add(new Vector3(0f, 0f, -1f));
            uvs.Add(new Vector2(1f, (float)i / numP));
        }

        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            int iIn0  = frontBaseIdx + i * 2;
            int iOut0 = frontBaseIdx + i * 2 + 1;
            int iIn1  = frontBaseIdx + next * 2;
            int iOut1 = frontBaseIdx + next * 2 + 1;

            // -Z視点で時計回り
            tris.Add(iIn0);
            tris.Add(iIn1);
            tris.Add(iOut1);

            tris.Add(iIn0);
            tris.Add(iOut1);
            tris.Add(iOut0);
        }

        // 2. 背面ベゼルリング (z = +halfT, normal = (0, 0, 1))
        int backBaseIdx = verts.Count;
        for (int i = 0; i < numP; i++)
        {
            Vector2 pIn = inLoop[i];
            Vector2 pOut = outLoop[i];

            verts.Add(new Vector3(pIn.x, pIn.y, halfT));
            norms.Add(new Vector3(0f, 0f, 1f));
            uvs.Add(new Vector2(0f, (float)i / numP));

            verts.Add(new Vector3(pOut.x, pOut.y, halfT));
            norms.Add(new Vector3(0f, 0f, 1f));
            uvs.Add(new Vector2(1f, (float)i / numP));
        }

        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            int iIn0  = backBaseIdx + i * 2;
            int iOut0 = backBaseIdx + i * 2 + 1;
            int iIn1  = backBaseIdx + next * 2;
            int iOut1 = backBaseIdx + next * 2 + 1;

            // +Z視点で時計回り
            tris.Add(iIn0);
            tris.Add(iOut1);
            tris.Add(iIn1);

            tris.Add(iIn0);
            tris.Add(iOut0);
            tris.Add(iOut1);
        }

        // 3. 外周側面（外側に向く法線）
        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            Vector2 p0 = outLoop[i];
            Vector2 p1 = outLoop[next];
            Vector2 edge = (p1 - p0).normalized;
            Vector3 sideNorm = new Vector3(edge.y, -edge.x, 0f).normalized;

            int v = verts.Count;
            verts.Add(new Vector3(p0.x, p0.y, -halfT)); // v
            verts.Add(new Vector3(p1.x, p1.y, -halfT)); // v+1
            verts.Add(new Vector3(p1.x, p1.y,  halfT)); // v+2
            verts.Add(new Vector3(p0.x, p0.y,  halfT)); // v+3

            for (int k = 0; k < 4; k++) norms.Add(sideNorm);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            // 外側視点で時計回り
            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
        }

        // 4. 内周側面（スクリーンとの接合面・中心に向く法線）
        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            Vector2 p0 = inLoop[i];
            Vector2 p1 = inLoop[next];
            Vector2 edge = (p1 - p0).normalized;
            Vector3 innerNorm = new Vector3(-edge.y, edge.x, 0f).normalized;

            int v = verts.Count;
            verts.Add(new Vector3(p0.x, p0.y,  halfT)); // v
            verts.Add(new Vector3(p1.x, p1.y,  halfT)); // v+1
            verts.Add(new Vector3(p1.x, p1.y, -halfT)); // v+2
            verts.Add(new Vector3(p0.x, p0.y, -halfT)); // v+3

            for (int k = 0; k < 4; k++) norms.Add(innerNorm);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            // 内側視点で時計回り
            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 上下の反重力エミッターパイロンメッシュを生成
    /// </summary>
    public static Mesh CreateFloatingPylonsMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartSign_FloatingPylons" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float hx = width * 0.5f;
        float hy = height * 0.5f;
        float pylonW = width * 0.4f;
        float pylonH = 0.08f;
        float pylonD = 0.08f;

        // 上部パイロン
        AddBox(verts, norms, uvs, tris, new Vector3(0f, hy + pylonH * 0.5f, 0f), new Vector3(pylonW, pylonH, pylonD));
        // 下部パイロン
        AddBox(verts, norms, uvs, tris, new Vector3(0f, -hy - pylonH * 0.5f, 0f), new Vector3(pylonW, pylonH, pylonD));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ビル外壁に吸着固定するカンチレバーマウント金具メッシュを生成
    /// </summary>
    public static Mesh CreateWallMountMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartSign_WallMount" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        // 壁面接合ベースプレート（Z = 0）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, 0f, 0.02f), new Vector3(0.5f, 0.5f, 0.04f));
        // 水平カンチレバーアーム（Z = 0.04 から 0.58 まで）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, 0f, 0.31f), new Vector3(0.12f, 0.12f, 0.54f));
        // 看板背面結合ブラケット（Z = 0.58）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, 0f, 0.58f), new Vector3(width * 0.5f, 0.16f, 0.04f));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector2> GenerateContour(float hx, float hy, float r, int cornerSegments)
    {
        List<Vector2> list = new List<Vector2>();
        Vector2[] cornerCenters = new Vector2[]
        {
            new Vector2(hx - r, -hy + r),
            new Vector2(hx - r, hy - r),
            new Vector2(-hx + r, hy - r),
            new Vector2(-hx + r, -hy + r)
        };
        float[] startAngles = new float[] { -90f, 0f, 90f, 180f };

        for (int c = 0; c < 4; c++)
        {
            Vector2 cc = cornerCenters[c];
            float baseAngle = startAngles[c];
            for (int s = 0; s < cornerSegments; s++)
            {
                float ang = (baseAngle + (90f * s / cornerSegments)) * Mathf.Deg2Rad;
                list.Add(new Vector2(cc.x + Mathf.Cos(ang) * r, cc.y + Mathf.Sin(ang) * r));
            }
        }
        return list;
    }

    private static void AddBox(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3[] corners = new Vector3[]
        {
            center + new Vector3(-h.x, -h.y, -h.z),
            center + new Vector3( h.x, -h.y, -h.z),
            center + new Vector3( h.x,  h.y, -h.z),
            center + new Vector3(-h.x,  h.y, -h.z),
            center + new Vector3(-h.x, -h.y,  h.z),
            center + new Vector3( h.x, -h.y,  h.z),
            center + new Vector3( h.x,  h.y,  h.z),
            center + new Vector3(-h.x,  h.y,  h.z)
        };

        // 6 faces: 前、後、左、右、上、下
        AddQuad(verts, norms, uvs, tris, corners[0], corners[1], corners[2], corners[3], Vector3.back);
        AddQuad(verts, norms, uvs, tris, corners[5], corners[4], corners[7], corners[6], Vector3.forward);
        AddQuad(verts, norms, uvs, tris, corners[4], corners[0], corners[3], corners[7], Vector3.left);
        AddQuad(verts, norms, uvs, tris, corners[1], corners[5], corners[6], corners[2], Vector3.right);
        AddQuad(verts, norms, uvs, tris, corners[3], corners[2], corners[6], corners[7], Vector3.up);
        AddQuad(verts, norms, uvs, tris, corners[4], corners[5], corners[1], corners[0], Vector3.down);
    }

    private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal)
    {
        int start = verts.Count;
        verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
        norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
        tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
    }
}
