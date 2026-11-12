using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スマート方向サイン・矢印サイネージ（SmartArrowSignage）統合コントローラー。
/// ・SmartSignageをベースに「文字ではなく方向・矢印を示す」次世代サインシステム。
/// ・超巨大スケール（数百メートル）に拡大してもジャギーやボケが一切発生しない数学的SDF（Signed Distance Field）ベクター描画。
/// ・8種の矢印スタイル（鋭角レーシング、太めサイン、二重シェブロン、三重シェブロン、ネオン管、クアンタム、ハザード警戒、カーブ案内）。
/// ・5種のアニメーション（進行方向ウェーブフロー、点滅フラッシュ、呼吸パルス、スキャンライン、静止）。
/// ・3種の背景モード（フロストガラス物理透過、純粋空中ホログラム、ミニマル半透明ガラス）。
/// ・6種の設置スタイル（スクリーン単体、壁面マウント、天井吊り下げ、フロアスタンド、エアロフレーム、反重力パイロン）。
/// ・Update() 不使用（CPU負荷 0.00ms / 0 GC Alloc）、MaterialPropertyBlockによるDrawCallバッチング維持。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SmartArrowSignageController : MonoBehaviour
{
    public enum MountingStyle
    {
        [InspectorName("① スクリーン単体 (Screen Only / 純浮遊)")]
        ScreenOnly = 0,

        [InspectorName("② 壁面マウント (Wall Mount / ビル壁面・トンネル金具)")]
        WallMount = 1,

        [InspectorName("③ 天井吊り下げ (Ceiling Hanger / 高架・トラス懸架)")]
        CeilingHanger = 2,

        [InspectorName("④ フロアスタンド (Floor Stand / 地面・路面設置支柱)")]
        FloorStand = 3,

        [InspectorName("⑤ エアロフレーム枠付き (Aero Framed / 白亜ベゼル)")]
        AeroFramed = 4,

        [InspectorName("⑥ 反重力パイロン浮遊型 (Floating Pylons / 上下エネルギーポッド)")]
        FloatingPylons = 5
    }

    public enum ArrowPattern
    {
        [InspectorName("⚡ ① 鋭角レーシング (Sharp Racing Chevron)")]
        SharpRacing = 0,

        [InspectorName("🛑 ② 太めサイン (Bold Highway Navigation)")]
        BoldSign = 1,

        [InspectorName("⏩ ③ 二重シェブロン (Double Chevron)")]
        DoubleChevron = 2,

        [InspectorName("⏭️ ④ 三重連続シェブロン (Triple Speed Chevron)")]
        TripleChevron = 3,

        [InspectorName("💡 ⑤ ネオン管中空 (Neon Tube Outline)")]
        NeonTube = 4,

        [InspectorName("💠 ⑥ クアンタム・セグメント (Quantum Lattice)")]
        QuantumLattice = 5,

        [InspectorName("⚠️ ⑦ ハザード警戒 (Hazard Warning Stripes)")]
        HazardStripes = 6,

        [InspectorName("↩️ ⑧ カーブ案内アーク (Curved Speed Turn)")]
        CurvedSpeed = 7
    }

    public enum SignMode
    {
        [InspectorName("① 物理透過フロストガラス (Frosted Glass / 背景屈折・光沢)")]
        FrostedGlass = 0,

        [InspectorName("② 純粋空中ホログラム (Pure Hologram / 背景ガラスなし純浮遊)")]
        PureHologram = 1,

        [InspectorName("③ ミニマル半透明ガラス (Minimal Tinted Glass / 軽量)")]
        MinimalGlass = 2,

        [InspectorName("④ サイバーグリッド・ホロ (Cyber Grid Holo / 走査線・方眼メッシュ浮遊)")]
        CyberGridHolo = 3,

        [InspectorName("⑤ ダークエアロ・バイザー (Dark Aero Visor / 偏光スモーク・直射日光耐性)")]
        DarkAeroVisor = 4,

        [InspectorName("⑥ 反重力エネルギーシールド (Energy Shield / ハニカム六角・プラズマ波)")]
        EnergyShield = 5,

        [InspectorName("⑦ オーロラプリズム・光彩ガラス (Aurora Dichroic Prism / 虹色偏光)")]
        AuroraPrism = 6,

        [InspectorName("⑧ ネオンエッジ・ホロフレーム (Neon Wireframe Holo / 外周サイバー発光クリア)")]
        NeonWireframeHolo = 7,

        [InspectorName("⑨ スターダスト・微粒子マトリクス (Star Dust Matrix / 空中光粒子ダスト＆浮遊)")]
        StarDustMatrix = 8
    }

    public enum AnimationMode
    {
        [InspectorName("🌊 流れる進行方向ウェーブ (Flowing Wave)")]
        Flowing = 0,

        [InspectorName("⚡ 点滅フラッシュ (Blinking Flash)")]
        Blinking = 1,

        [InspectorName("💓 呼吸パルス (Breathing Pulse)")]
        Pulse = 2,

        [InspectorName("📏 スキャンライン (Holo Scanline)")]
        ScanlineFlow = 3,

        [InspectorName("⏸️ 静止・常時点灯 (Static Const)")]
        Static = 4
    }

    public enum AspectRatioPreset
    {
        [InspectorName("16:9 (ワイド標準 - 8.0m x 4.5m)")]
        Ratio_16_9 = 0,

        [InspectorName("21:9 (シネマ超ワイド - 10.5m x 4.5m)")]
        Ratio_21_9 = 1,

        [InspectorName("32:9 (ウルトラロング誘導帯 - 16.0m x 4.5m)")]
        Ratio_32_9 = 2,

        [InspectorName("4:3 (標準HUD - 6.0m x 4.5m)")]
        Ratio_4_3 = 3,

        [InspectorName("1:1 (正方形スクエア - 5.0m x 5.0m)")]
        Ratio_1_1 = 4,

        [InspectorName("9:16 (縦型サイネージ - 4.5m x 8.0m)")]
        Ratio_9_16 = 5,

        [InspectorName("カスタムサイズ (Custom)")]
        Custom = 6
    }

    public enum ArrowColorPreset
    {
        [InspectorName("レーシングシアン (Neon Cyan)")]
        NeonCyan = 0,

        [InspectorName("スピードライム (Speed Lime)")]
        SpeedLime = 1,

        [InspectorName("コーションアンバー (Caution Amber)")]
        CautionAmber = 2,

        [InspectorName("デンジャークリムゾン (Danger Crimson)")]
        DangerCrimson = 3,

        [InspectorName("ハイウェイホワイト (Pure White)")]
        PureWhite = 4,

        [InspectorName("サイバーゴールド (Cyber Gold)")]
        CyberGold = 5,

        [InspectorName("バイオレットクアンタム (Violet Quantum)")]
        VioletQuantum = 6
    }

    // ─────────────────────────────────────────────────────────────
    // インスペクター設定フィールド
    // ─────────────────────────────────────────────────────────────

    [Header("設置スタイル・背景モード・サイズ")]
    [Tooltip("看板の設置形式（スクリーン単体／壁面／天井／フロアスタンド／枠付き／浮遊パイロン）")]
    [SerializeField] private MountingStyle _mountingStyle = MountingStyle.ScreenOnly;

    [Tooltip("背景表現モード（フロストガラス／純粋空中ホログラム／ミニマルガラス）")]
    [SerializeField] private SignMode _signMode = SignMode.FrostedGlass;

    [Tooltip("アスペクト比プリセット")]
    [SerializeField] private AspectRatioPreset _aspectRatio = AspectRatioPreset.Ratio_16_9;

    [Min(0.2f)]
    [Tooltip("看板の横幅（m）")]
    [SerializeField] private float _signWidth = 8.0f;

    [Min(0.2f)]
    [Tooltip("看板の縦幅（m）")]
    [SerializeField] private float _signHeight = 4.5f;

    [Tooltip("外枠・パイロン・金具オブジェクトを表示するか")]
    [SerializeField] private bool _showFrame = true;

    [Header("SDF ベクター矢印パラメータ（拡大しても超鮮明）")]
    [Tooltip("矢印の幾何学パターン")]
    [SerializeField] private ArrowPattern _arrowPattern = ArrowPattern.SharpRacing;

    [Range(-180f, 180f)]
    [Tooltip("矢印の回転角度（度数法。0=右, 90=上, 180=左, -90=下）")]
    [SerializeField] private float _arrowAngle = 0f;

    [Range(0.2f, 3.0f)]
    [Tooltip("矢印の全体スケール倍率")]
    [SerializeField] private float _arrowScale = 1.0f;

    [Range(0.2f, 3.0f)]
    [Tooltip("矢印の幅・高さアスペクト比調整")]
    [SerializeField] private float _arrowAspect = 1.0f;

    [Range(1, 6)]
    [Tooltip("連続配置する矢印・シェブロンの個数")]
    [SerializeField] private int _arrowRepeat = 1;

    [Range(0.1f, 1.0f)]
    [Tooltip("連続配置時の矢印同士の間隔")]
    [SerializeField] private float _arrowSpacing = 0.35f;

    [Tooltip("矢印のXY微小オフセット")]
    [SerializeField] private Vector2 _arrowOffset = Vector2.zero;

    [Header("アニメーション")]
    [Tooltip("アニメーション方式（進行方向フロー／点滅／パルス／スキャンライン／静止）")]
    [SerializeField] private AnimationMode _animMode = AnimationMode.Flowing;

    [Range(0.0f, 10.0f)]
    [Tooltip("アニメーションの速度")]
    [SerializeField] private float _animSpeed = 2.5f;

    [Header("カラー・発光（URP Bloom連動 HDR）")]
    [ColorUsage(true, true)]
    [Tooltip("矢印のプライマリ発光色")]
    [SerializeField] private Color _arrowColor = new Color(0.25f, 0.90f, 1.0f, 1.0f);

    [ColorUsage(true, true)]
    [Tooltip("矢印のセカンダリ発光色（グラデーション・ハザードストライプ時）")]
    [SerializeField] private Color _secondaryColor = new Color(1.0f, 0.45f, 0.1f, 1.0f);

    [Range(0.5f, 10.0f)]
    [Tooltip("発光強度倍率（高輝度で美しいBloomを発生）")]
    [SerializeField] private float _emissionMultiplier = 2.8f;

    [ColorUsage(true, true)]
    [Tooltip("矢印中心ホットコアの発光色")]
    [SerializeField] private Color _coreColor = Color.white;

    [Range(0.0f, 5.0f)]
    [Tooltip("矢印中心ホットコアの強度")]
    [SerializeField] private float _coreIntensity = 2.0f;

    [Range(0.0f, 0.25f)]
    [Tooltip("矢印外周のアウターソフトグロー拡散半径")]
    [SerializeField] private float _glowWidth = 0.06f;

    [Range(0.0f, 3.0f)]
    [Tooltip("アウターソフトグローの輝度強度")]
    [SerializeField] private float _glowIntensity = 1.2f;

    [Range(0.01f, 0.15f)]
    [Tooltip("ネオン管・中空アウトラインの太さ")]
    [SerializeField] private float _outlineWidth = 0.04f;

    [Range(5.0f, 50.0f)]
    [Tooltip("ハザード警戒ストライプの縞模様密度")]
    [SerializeField] private float _stripeDensity = 22.0f;

    [Header("HUD テクニカル装飾マーク")]
    [Tooltip("四隅のテクニカルコーナーブラケットやサイド目盛りを表示")]
    [SerializeField] private bool _showHUDDecorations = true;

    [ColorUsage(true, true)]
    [Tooltip("HUD装飾マークのアクセント色")]
    [SerializeField] private Color _hudColor = new Color(0.75f, 0.95f, 1.0f, 0.7f);

    [Header("フロストガラス光学的パラメータ（SignMode=FrostedGlass時）")]
    [Tooltip("URP Opaque Texture による背景フロストガラス透過ブラー")]
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
    [Tooltip("太陽光（メインライト）の鏡面反射光沢")]
    [SerializeField] private float _specularIntensity = 1.3f;

    [Range(4.0f, 128.0f)]
    [Tooltip("鏡面反射の絞り込み（シャープネス）")]
    [SerializeField] private float _specularSmoothness = 64.0f;

    [Tooltip("ガラス外周フレネル発光色")]
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

    [Range(0.0f, 1.0f)]
    [Tooltip("ガラス表面を流れる反射光ハイライトの位相オフセット（0〜1）。看板ごとにタイミングをずらせます")]
    [SerializeField] private float _sweepPhaseOffset = 0f;

    [Tooltip("看板のワールド設置座標から自動でランダムな位相差（シード）を付与してタイミングを自然に分散させるか")]
    [SerializeField] private bool _autoStaggerPhaseByPosition = true;

    [Header("コンポーネント参照")]
    [Tooltip("表示板のRenderer")]
    [SerializeField] private Renderer _screenRenderer;

    [Tooltip("外枠・パイロン・金具オブジェクト")]
    [SerializeField] private GameObject _frameObject;

    [Header("カスタムマテリアル（指定時は最優先使用）")]
    [Tooltip("手動保存したマテリアルを直接割り当てたい場合に指定")]
    [SerializeField] private Material _customMaterial;

    [Tooltip("別名保存時のファイル名（未入力時は自動生成）")]
    [SerializeField] private string _customSaveName = "";

    // MaterialPropertyBlockキャッシュ（CPU負荷ゼロ & DrawCall維持）
    private MaterialPropertyBlock _mpb;

    // シェーダープロパティIDキャッシュ
    private static readonly int SignModeId           = Shader.PropertyToID("_SignMode");
    private static readonly int ArrowPatternId       = Shader.PropertyToID("_ArrowPattern");
    private static readonly int ArrowAngleId         = Shader.PropertyToID("_ArrowAngle");
    private static readonly int ArrowScaleId         = Shader.PropertyToID("_ArrowScale");
    private static readonly int ArrowAspectId        = Shader.PropertyToID("_ArrowAspect");
    private static readonly int ArrowRepeatId        = Shader.PropertyToID("_ArrowRepeat");
    private static readonly int ArrowSpacingId       = Shader.PropertyToID("_ArrowSpacing");
    private static readonly int ArrowOffsetId        = Shader.PropertyToID("_ArrowOffset");
    private static readonly int AnimModeId           = Shader.PropertyToID("_AnimMode");
    private static readonly int AnimSpeedId          = Shader.PropertyToID("_AnimSpeed");
    private static readonly int ArrowColorId         = Shader.PropertyToID("_ArrowColor");
    private static readonly int SecondaryColorId     = Shader.PropertyToID("_SecondaryColor");
    private static readonly int EmissionMultiplierId = Shader.PropertyToID("_EmissionMultiplier");
    private static readonly int CoreColorId          = Shader.PropertyToID("_CoreColor");
    private static readonly int CoreIntensityId      = Shader.PropertyToID("_CoreIntensity");
    private static readonly int GlowWidthId          = Shader.PropertyToID("_GlowWidth");
    private static readonly int GlowIntensityId      = Shader.PropertyToID("_GlowIntensity");
    private static readonly int OutlineWidthId       = Shader.PropertyToID("_OutlineWidth");
    private static readonly int StripeDensityId      = Shader.PropertyToID("_StripeDensity");
    private static readonly int ShowHUDDecorationsId = Shader.PropertyToID("_ShowHUDDecorations");
    private static readonly int HUDColorId           = Shader.PropertyToID("_HUDColor");
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
    private static readonly int SweepPhaseOffsetId   = Shader.PropertyToID("_SweepPhaseOffset");
    private static readonly int AspectRatioId        = Shader.PropertyToID("_AspectRatio");

    // ランタイム動的メッシュ（フォールバック用）
    private Mesh _runtimeMeshScreen;
    private Mesh _runtimeMeshFrame;

    public MountingStyle Style => _mountingStyle;
    public ArrowPattern Pattern => _arrowPattern;
    public SignMode Mode => _signMode;
    public AnimationMode Anim => _animMode;
    public AspectRatioPreset AspectRatio => _aspectRatio;
    public float ArrowAngle => _arrowAngle;
    public float SignWidth => _signWidth;
    public float SignHeight => _signHeight;
    public bool ShowFrame => _showFrame;
    public bool ShowHUDDecorations => _showHUDDecorations;
    public Color ArrowColor => _arrowColor;
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

    public float SweepPhaseOffset
    {
        get => _sweepPhaseOffset;
        set
        {
            _sweepPhaseOffset = Mathf.Clamp01(value);
            ApplyMaterialProperties();
        }
    }

    public bool AutoStaggerPhaseByPosition
    {
        get => _autoStaggerPhaseByPosition;
        set
        {
            _autoStaggerPhaseByPosition = value;
            ApplyMaterialProperties();
        }
    }

    public void SetSweepPhaseOffset(float offset)
    {
        SweepPhaseOffset = offset;
    }

    public void RandomizeSweepPhase()
    {
        SweepPhaseOffset = UnityEngine.Random.value;
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
    /// 看板の全設定・メッシュ・Transform・シェーダーパラメータを反映
    /// </summary>
    public void UpdateSignboard()
    {
        FindChildrenIfNeeded();
        EnsureFrameObject();
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
                    if (r.gameObject != gameObject && !r.name.Contains("Frame") && !r.name.Contains("Pylon") && !r.name.Contains("Mount") && !r.name.Contains("Hanger") && !r.name.Contains("Stand"))
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
            if (frameT == null) frameT = transform.Find("CeilingHanger");
            if (frameT == null) frameT = transform.Find("FloorStand");
            if (frameT == null) frameT = transform.Find("Pylons");
            if (frameT != null) _frameObject = frameT.gameObject;
        }
    }

    private void EnsureFrameObject()
    {
        if (_frameObject == null)
        {
            Transform frameT = transform.Find("Frame");
            if (frameT == null) frameT = transform.Find("MountArm");
            if (frameT == null) frameT = transform.Find("CeilingHanger");
            if (frameT == null) frameT = transform.Find("FloorStand");
            if (frameT == null) frameT = transform.Find("Pylons");
            if (frameT != null)
            {
                _frameObject = frameT.gameObject;
            }
        }

        // 金具マウントスタイル選択時、frameObject が未存在であれば自動的に子オブジェクトを生成
        if (_frameObject == null && _mountingStyle != MountingStyle.ScreenOnly)
        {
            GameObject newFrame = new GameObject("Frame");
            newFrame.transform.SetParent(transform, false);
            newFrame.transform.localPosition = Vector3.zero;
            newFrame.transform.localRotation = Quaternion.identity;
            newFrame.transform.localScale = new Vector3(_signWidth, _signHeight, 1.0f);
            newFrame.AddComponent<MeshFilter>();
            MeshRenderer mr = newFrame.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            _frameObject = newFrame;
        }

        // 金具マテリアル同期（白亜ベゼル枠 / 反重力パイロン vs ダークマウント金具）
        if (_frameObject != null && _mountingStyle != MountingStyle.ScreenOnly)
        {
            MeshRenderer mr = _frameObject.GetComponent<MeshRenderer>();
            if (mr != null)
            {
#if UNITY_EDITOR
                string matName = (_mountingStyle == MountingStyle.AeroFramed || _mountingStyle == MountingStyle.FloatingPylons)
                    ? "M_SmartArrow_Frame_White"
                    : "M_SmartArrow_Frame_Dark";
                if (mr.sharedMaterial == null || mr.sharedMaterial.name != matName)
                {
                    Material frameMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Deverop/AT_Murayama/SmartArrowSignage/Materials/{matName}.mat");
                    if (frameMat != null)
                    {
                        mr.sharedMaterial = frameMat;
                    }
                }
#endif
            }
        }
    }

    private void ApplyFrameVisibility()
    {
        if (_frameObject != null)
        {
            bool shouldShow = _showFrame && (_mountingStyle != MountingStyle.ScreenOnly);
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
                AssignMeshWithFallback(smf, "SmartArrow_Screen_RoundedRect", ref _runtimeMeshScreen, () => CreateRoundedScreenMesh(1.0f, 1.0f, 0.08f, 0.02f));
            }
#if UNITY_EDITOR
            if (_screenRenderer.sharedMaterial == null && _customMaterial == null)
            {
                Material defMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/SmartArrowSignage/Materials/M_SmartArrow_NeonCyan.mat");
                if (defMat != null)
                {
                    _screenRenderer.sharedMaterial = defMat;
                }
            }
#endif
        }

        if (_frameObject != null && _mountingStyle != MountingStyle.ScreenOnly)
        {
            MeshFilter fmf = _frameObject.GetComponent<MeshFilter>();
            if (fmf != null)
            {
                switch (_mountingStyle)
                {
                    case MountingStyle.AeroFramed:
                        AssignMeshWithFallback(fmf, "SmartArrow_AeroFrame", ref _runtimeMeshFrame, () => CreateAeroFrameMesh(1.0f, 1.0f, 0.08f, 0.04f, 0.035f));
                        break;
                    case MountingStyle.WallMount:
                        AssignMeshWithFallback(fmf, "SmartArrow_WallMount", ref _runtimeMeshFrame, () => CreateWallMountMesh(1.0f, 1.0f));
                        break;
                    case MountingStyle.CeilingHanger:
                        AssignMeshWithFallback(fmf, "SmartArrow_CeilingHanger", ref _runtimeMeshFrame, () => CreateCeilingHangerMesh(1.0f, 1.0f));
                        break;
                    case MountingStyle.FloorStand:
                        AssignMeshWithFallback(fmf, "SmartArrow_FloorStand", ref _runtimeMeshFrame, () => CreateFloorStandMesh(1.0f, 1.0f));
                        break;
                    case MountingStyle.FloatingPylons:
                        AssignMeshWithFallback(fmf, "SmartArrow_FloatingPylons", ref _runtimeMeshFrame, () => CreateFloatingPylonsMesh(1.0f, 1.0f));
                        break;
                }
            }
        }
    }

    private void AssignMeshWithFallback(MeshFilter mf, string expectedAssetName, ref Mesh runtimeCache, Func<Mesh> generator)
    {
        string runtimeName = "Runtime_" + expectedAssetName;
        if (mf.sharedMesh != null && (mf.sharedMesh.name == expectedAssetName || mf.sharedMesh.name == runtimeName))
        {
            return;
        }

#if UNITY_EDITOR
        Mesh assetMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Deverop/AT_Murayama/SmartArrowSignage/Meshes/{expectedAssetName}.asset");
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
            if (_mountingStyle == MountingStyle.WallMount && _showFrame)
            {
                // 壁面マウント金具表示時はマウントベースから手前(+Z)に突き出す
                screenPos = new Vector3(0f, 0f, 0.60f);
            }
            _screenRenderer.transform.localPosition = screenPos;
            _screenRenderer.transform.localScale = new Vector3(_signWidth, _signHeight, 1.0f);
        }

        if (_frameObject != null)
        {
            _frameObject.transform.localPosition = Vector3.zero;
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

        _mpb.SetFloat(SignModeId, (float)_signMode);
        _mpb.SetFloat(ArrowPatternId, (float)_arrowPattern);
        _mpb.SetFloat(ArrowAngleId, _arrowAngle);
        _mpb.SetFloat(ArrowScaleId, _arrowScale);
        _mpb.SetFloat(ArrowAspectId, _arrowAspect);
        _mpb.SetFloat(ArrowRepeatId, (float)_arrowRepeat);
        _mpb.SetFloat(ArrowSpacingId, _arrowSpacing);
        _mpb.SetVector(ArrowOffsetId, new Vector4(_arrowOffset.x, _arrowOffset.y, 0f, 0f));

        _mpb.SetFloat(AnimModeId, (float)_animMode);
        _mpb.SetFloat(AnimSpeedId, _animSpeed);

        _mpb.SetColor(ArrowColorId, _arrowColor);
        _mpb.SetColor(SecondaryColorId, _secondaryColor);
        _mpb.SetFloat(EmissionMultiplierId, _emissionMultiplier);
        _mpb.SetColor(CoreColorId, _coreColor);
        _mpb.SetFloat(CoreIntensityId, _coreIntensity);
        _mpb.SetFloat(GlowWidthId, _glowWidth);
        _mpb.SetFloat(GlowIntensityId, _glowIntensity);
        _mpb.SetFloat(OutlineWidthId, _outlineWidth);
        _mpb.SetFloat(StripeDensityId, _stripeDensity);

        _mpb.SetFloat(ShowHUDDecorationsId, _showHUDDecorations ? 1.0f : 0.0f);
        _mpb.SetColor(HUDColorId, _hudColor);

        // フロストガラスパラメータ
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

        float effectiveSweepPhase = _sweepPhaseOffset;
        if (_autoStaggerPhaseByPosition)
        {
            Vector3 pos = transform.position;
            float posHash = Mathf.Repeat(Mathf.Abs(Mathf.Sin(pos.x * 12.9898f + pos.y * 78.233f + pos.z * 45.164f)) * 43758.5453f, 1.0f);
            effectiveSweepPhase = Mathf.Repeat(_sweepPhaseOffset + posHash, 1.0f);
        }
        _mpb.SetFloat(SweepPhaseOffsetId, effectiveSweepPhase);

        float aspect = (_signHeight > 0.001f) ? (_signWidth / _signHeight) : 1.7777f;
        _mpb.SetFloat(AspectRatioId, aspect);

        _screenRenderer.SetPropertyBlock(_mpb);
    }

    // ─────────────────────────────────────────────────────────────
    // プリセット・プロパティ変更 API
    // ─────────────────────────────────────────────────────────────

    public void SetArrowPattern(ArrowPattern pattern)
    {
        _arrowPattern = pattern;
        UpdateSignboard();
    }

    public void SetArrowAngle(float angleDegrees)
    {
        _arrowAngle = angleDegrees;
        UpdateSignboard();
    }

    public void SetMountingStyle(MountingStyle style)
    {
        _mountingStyle = style;
        _showFrame = (style != MountingStyle.ScreenOnly);
        UpdateSignboard();
    }

    public void SetShowHUDDecorations(bool show)
    {
        _showHUDDecorations = show;
        UpdateSignboard();
    }

    public void SetSignMode(SignMode mode)
    {
        _signMode = mode;
        UpdateSignboard();
    }

    public void SetAnimationMode(AnimationMode anim)
    {
        _animMode = anim;
        UpdateSignboard();
    }

    public void SetArrowRepeat(int repeatCount, float spacing = 0.35f)
    {
        _arrowRepeat = Mathf.Clamp(repeatCount, 1, 6);
        _arrowSpacing = spacing;
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
            case AspectRatioPreset.Ratio_32_9:
                _signWidth = 16.0f;
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

    public void ApplyColorPreset(ArrowColorPreset colorPreset)
    {
        switch (colorPreset)
        {
            case ArrowColorPreset.NeonCyan:
                _arrowColor = new Color(0.20f, 0.92f, 1.0f, 1.0f);
                _secondaryColor = new Color(1.0f, 0.45f, 0.1f, 1.0f);
                _coreColor = Color.white;
                _hudColor = new Color(0.70f, 0.95f, 1.0f, 0.7f);
                break;
            case ArrowColorPreset.SpeedLime:
                _arrowColor = new Color(0.35f, 1.0f, 0.60f, 1.0f);
                _secondaryColor = new Color(1.0f, 0.90f, 0.20f, 1.0f);
                _coreColor = new Color(0.95f, 1.0f, 0.90f, 1.0f);
                _hudColor = new Color(0.60f, 1.0f, 0.75f, 0.7f);
                break;
            case ArrowColorPreset.CautionAmber:
                _arrowColor = new Color(1.0f, 0.65f, 0.05f, 1.0f);
                _secondaryColor = new Color(1.0f, 0.20f, 0.05f, 1.0f);
                _coreColor = new Color(1.0f, 0.95f, 0.80f, 1.0f);
                _hudColor = new Color(1.0f, 0.85f, 0.40f, 0.7f);
                break;
            case ArrowColorPreset.DangerCrimson:
                _arrowColor = new Color(1.0f, 0.15f, 0.25f, 1.0f);
                _secondaryColor = new Color(1.0f, 0.85f, 0.10f, 1.0f);
                _coreColor = Color.white;
                _hudColor = new Color(1.0f, 0.40f, 0.50f, 0.7f);
                break;
            case ArrowColorPreset.PureWhite:
                _arrowColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                _secondaryColor = new Color(0.60f, 0.85f, 1.0f, 1.0f);
                _coreColor = Color.white;
                _hudColor = new Color(0.85f, 0.95f, 1.0f, 0.7f);
                break;
            case ArrowColorPreset.CyberGold:
                _arrowColor = new Color(1.0f, 0.82f, 0.25f, 1.0f);
                _secondaryColor = new Color(1.0f, 0.40f, 0.05f, 1.0f);
                _coreColor = new Color(1.0f, 0.98f, 0.85f, 1.0f);
                _hudColor = new Color(1.0f, 0.90f, 0.50f, 0.7f);
                break;
            case ArrowColorPreset.VioletQuantum:
                _arrowColor = new Color(0.85f, 0.35f, 1.0f, 1.0f);
                _secondaryColor = new Color(0.15f, 0.90f, 1.0f, 1.0f);
                _coreColor = Color.white;
                _hudColor = new Color(0.90f, 0.60f, 1.0f, 0.7f);
                break;
        }
        UpdateSignboard();
    }

    public void SetShowFrame(bool show)
    {
        _showFrame = show;
        if (show && _mountingStyle == MountingStyle.ScreenOnly)
        {
            _mountingStyle = MountingStyle.WallMount;
        }
        UpdateSignboard();
    }

    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateSignboard();
    }

    public Material CreateConfiguredMaterial()
    {
        Shader shader = Shader.Find("Custom/SmartArrowSignage/S_SmartArrowSignage");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.name = string.IsNullOrWhiteSpace(_customSaveName) ? $"M_SmartArrow_{gameObject.name}" : _customSaveName;

        mat.SetFloat(SignModeId, (float)_signMode);
        mat.SetFloat(ArrowPatternId, (float)_arrowPattern);
        mat.SetFloat(ArrowAngleId, _arrowAngle);
        mat.SetFloat(ArrowScaleId, _arrowScale);
        mat.SetFloat(ArrowAspectId, _arrowAspect);
        mat.SetFloat(ArrowRepeatId, (float)_arrowRepeat);
        mat.SetFloat(ArrowSpacingId, _arrowSpacing);
        mat.SetVector(ArrowOffsetId, new Vector4(_arrowOffset.x, _arrowOffset.y, 0f, 0f));

        mat.SetFloat(AnimModeId, (float)_animMode);
        mat.SetFloat(AnimSpeedId, _animSpeed);

        mat.SetColor(ArrowColorId, _arrowColor);
        mat.SetColor(SecondaryColorId, _secondaryColor);
        mat.SetFloat(EmissionMultiplierId, _emissionMultiplier);
        mat.SetColor(CoreColorId, _coreColor);
        mat.SetFloat(CoreIntensityId, _coreIntensity);
        mat.SetFloat(GlowWidthId, _glowWidth);
        mat.SetFloat(GlowIntensityId, _glowIntensity);
        mat.SetFloat(OutlineWidthId, _outlineWidth);
        mat.SetFloat(StripeDensityId, _stripeDensity);

        mat.SetFloat(ShowHUDDecorationsId, _showHUDDecorations ? 1.0f : 0.0f);
        mat.SetColor(HUDColorId, _hudColor);

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
        mat.SetFloat(SweepIntensityId, _sweepIntensity);
        mat.SetFloat(SweepPhaseOffsetId, _sweepPhaseOffset);

        float aspect = (_signHeight > 0.001f) ? (_signWidth / _signHeight) : 1.7777f;
        mat.SetFloat(AspectRatioId, aspect);

        // レンダーステート（ブレンドモード・カリング・深度）
        if (_signMode == SignMode.PureHologram)
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }
        else
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);

        return mat;
    }

    // ─────────────────────────────────────────────────────────────
    // プロシージャルメッシュ生成エンジン（全6種の設置スタイル対応）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ① 丸角と面取りを持つフロストガラス・スクリーンメッシュ（中心基準 1m x 1m）
    /// </summary>
    public static Mesh CreateRoundedScreenMesh(float width = 1.0f, float height = 1.0f, float cornerRadius = 0.08f, float thickness = 0.02f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_Screen_RoundedRect" };
        int cornerSegments = 8;
        float hx = width * 0.5f;
        float hy = height * 0.5f;
        float r = Mathf.Min(cornerRadius, Mathf.Min(hx, hy) * 0.45f);
        float halfT = thickness * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        List<Vector2> contour = GenerateContour(hx, hy, r, cornerSegments);
        int numP = contour.Count;

        // 1. 前面 (Front Face: z = -halfT, normal = (0,0,-1))
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

        // 2. 背面 (Back Face: z = +halfT, normal = (0,0,1))
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
            // 背面UVも前面と同一の座標系に一致させ、3D空間内での矢印方向・位置の整合性を保証
            uvs.Add(new Vector2((p.x + hx) / width, (p.y + hy) / height));
        }

        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            tris.Add(backCenterIdx);
            tris.Add(backRimStart + i);
            tris.Add(backRimStart + next);
        }

        // 3. 面取り側面 (Side Bevel Rim)
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

            for (int k = 0; k < 4; k++) norms.Add(sideNorm);
            uvs.Add(new Vector2((p0.x + hx) / width, 0f));
            uvs.Add(new Vector2((p1.x + hx) / width, 0f));
            uvs.Add(new Vector2((p1.x + hx) / width, 1f));
            uvs.Add(new Vector2((p0.x + hx) / width, 1f));

            tris.Add(v0); tris.Add(v0 + 1); tris.Add(v0 + 2);
            tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ② 壁面カンチレバーマウント金具メッシュ
    /// </summary>
    public static Mesh CreateWallMountMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_WallMount" };
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

    /// <summary>
    /// ③ 天井吊り下げ金具メッシュ（上部フランジ・ツイン懸架ロッド・クランプ）
    /// </summary>
    public static Mesh CreateCeilingHangerMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_CeilingHanger" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float hy = height * 0.5f;
        float rodX = width * 0.35f;
        float hangerTopY = hy + 0.35f;
        float rodHeight = 0.35f;

        // 左側天井フランジ & 懸架ロッド
        AddBox(verts, norms, uvs, tris, new Vector3(-rodX, hangerTopY, 0f), new Vector3(0.12f, 0.04f, 0.12f));
        AddBox(verts, norms, uvs, tris, new Vector3(-rodX, hy + rodHeight * 0.5f, 0f), new Vector3(0.04f, rodHeight, 0.04f));
        AddBox(verts, norms, uvs, tris, new Vector3(-rodX, hy + 0.02f, 0f), new Vector3(0.09f, 0.08f, 0.07f));

        // 右側天井フランジ & 懸架ロッド
        AddBox(verts, norms, uvs, tris, new Vector3(rodX, hangerTopY, 0f), new Vector3(0.12f, 0.04f, 0.12f));
        AddBox(verts, norms, uvs, tris, new Vector3(rodX, hy + rodHeight * 0.5f, 0f), new Vector3(0.04f, rodHeight, 0.04f));
        AddBox(verts, norms, uvs, tris, new Vector3(rodX, hy + 0.02f, 0f), new Vector3(0.09f, 0.08f, 0.07f));

        // 水平補強ブレース（2本のロッド間を結合）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, hy + rodHeight * 0.7f, 0f), new Vector3(rodX * 2.0f, 0.03f, 0.03f));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ④ フロアスタンド支柱＆ベース金具メッシュ（地面設置・重厚エアロ台座）
    /// </summary>
    public static Mesh CreateFloorStandMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_FloorStand" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float hy = height * 0.5f;
        float standBaseY = -hy - 0.35f;
        float pillarHeight = 0.35f;

        // 地面接地ベースプレート（重厚なヘビーデューティ台座）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, standBaseY, 0f), new Vector3(width * 0.6f, 0.05f, 0.45f));
        // 中央支持支柱パイロン
        AddBox(verts, norms, uvs, tris, new Vector3(0f, -hy - pillarHeight * 0.5f, 0f), new Vector3(0.16f, pillarHeight, 0.12f));
        // 後方ブレースガセット（傾斜支え）
        AddBox(verts, norms, uvs, tris, new Vector3(0f, -hy - pillarHeight * 0.5f, 0.10f), new Vector3(0.08f, pillarHeight * 0.8f, 0.10f));
        // 看板下部固定ブラケットクランプ
        AddBox(verts, norms, uvs, tris, new Vector3(0f, -hy - 0.02f, 0f), new Vector3(width * 0.4f, 0.08f, 0.07f));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// ⑤ エアロダイナミック外枠ベゼルメッシュ（全周閉鎖3Dフレーム）
    /// </summary>
    public static Mesh CreateAeroFrameMesh(float width = 1.0f, float height = 1.0f, float cornerRadius = 0.08f, float bezelWidth = 0.04f, float thickness = 0.035f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_AeroFrame" };
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

        // 前面ベゼル
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

            tris.Add(iIn0); tris.Add(iIn1); tris.Add(iOut1);
            tris.Add(iIn0); tris.Add(iOut1); tris.Add(iOut0);
        }

        // 背面ベゼル
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

            tris.Add(iIn0); tris.Add(iOut1); tris.Add(iIn1);
            tris.Add(iIn0); tris.Add(iOut0); tris.Add(iOut1);
        }

        // 外周側面
        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            Vector2 p0 = outLoop[i];
            Vector2 p1 = outLoop[next];
            Vector2 edge = (p1 - p0).normalized;
            Vector3 sideNorm = new Vector3(edge.y, -edge.x, 0f).normalized;

            int v = verts.Count;
            verts.Add(new Vector3(p0.x, p0.y, -halfT));
            verts.Add(new Vector3(p1.x, p1.y, -halfT));
            verts.Add(new Vector3(p1.x, p1.y,  halfT));
            verts.Add(new Vector3(p0.x, p0.y,  halfT));

            for (int k = 0; k < 4; k++) norms.Add(sideNorm);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
        }

        // 内周側面
        for (int i = 0; i < numP; i++)
        {
            int next = (i + 1) % numP;
            Vector2 p0 = inLoop[i];
            Vector2 p1 = inLoop[next];
            Vector2 edge = (p1 - p0).normalized;
            Vector3 innerNorm = new Vector3(-edge.y, edge.x, 0f).normalized;

            int v = verts.Count;
            verts.Add(new Vector3(p0.x, p0.y,  halfT));
            verts.Add(new Vector3(p1.x, p1.y,  halfT));
            verts.Add(new Vector3(p1.x, p1.y, -halfT));
            verts.Add(new Vector3(p0.x, p0.y, -halfT));

            for (int k = 0; k < 4; k++) norms.Add(innerNorm);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

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
    /// ⑥ 上下反重力エミッターパイロンメッシュ
    /// </summary>
    public static Mesh CreateFloatingPylonsMesh(float width = 1.0f, float height = 1.0f)
    {
        Mesh mesh = new Mesh { name = "SmartArrow_FloatingPylons" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs   = new List<Vector2>();
        List<int> tris      = new List<int>();

        float hy = height * 0.5f;
        float pylonW = width * 0.45f;
        float pylonH = 0.08f;
        float pylonD = 0.08f;

        // 上部パイロン
        AddBox(verts, norms, uvs, tris, new Vector3(0f, hy + pylonH * 0.6f, 0f), new Vector3(pylonW, pylonH, pylonD));
        // 下部パイロン
        AddBox(verts, norms, uvs, tris, new Vector3(0f, -hy - pylonH * 0.6f, 0f), new Vector3(pylonW, pylonH, pylonD));

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
        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

        tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
        tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
    }
}
