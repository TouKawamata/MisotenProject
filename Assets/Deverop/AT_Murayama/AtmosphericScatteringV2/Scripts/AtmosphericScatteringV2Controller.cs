using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AT_Murayama.AtmosphericScatteringV2
{
    /// <summary>
    /// 大気散乱 V2（AtmosphericScatteringV2）コントローラー。
    /// ・昼夜時刻補間（Time of Day）
    /// ・太陽方向 Mie 前方散乱（Henyey-Greenstein 位相関数＋色相維持ソフトクランプ）
    /// ・指数オーバーフロー完全防止・高度フォグ（Height Fog）
    /// ・2000〜3000mメガロポリス対応2段距離カーブ（Dual-Range）
    /// ・1/2解像度（Half-Res）3パス＋バイラテラル深度保護合成（低負荷＆チラつき防止）
    /// を完全統合した大気散乱システムです。
    /// 既存V1とは完全独立しており、他フォルダの自作クラスへの型依存は一切ありません。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AtmosphericScatteringV2Controller : MonoBehaviour
    {
        public enum MasterPresetV2
        {
            CyberNight_V1Compatible, // V1の CyberpunkNight_Reference と100%同じパラメータ＆全拡張トグルOFF
            RealisticDay_Clear,      // 昼・DualRange ON・HeightFog ON・Mie ON
            GoldenHour_Sunset,       // 夕・DualRange ON・HeightFog ON・Mie ON
            MegaCity_DayNightAuto    // TimeOfDay補間 ON・DualRange ON・HeightFog ON・Mie ON
        }

        public enum TimeSourceMode
        {
            Manual,       // インスペクターの TimeOfDay スライダー値を使用
            SunElevation  // Directional Light の仰角から自動算出
        }

        // =============================================================
        // 1. 基本動作・解像度設定
        // =============================================================
        [Header("有効化・解像度・カメラ適用設定")]
        [Tooltip("大気散乱エフェクトのON/OFF")]
        [SerializeField] private bool _enableEffect = true;

        [Tooltip("エフェクト全体のブレンド強度（0で完全に無効・描画パスもスキップ）")]
        [Range(0f, 1f)]
        [SerializeField] private float _overallStrength = 1.0f;

        [Tooltip("ON推奨：1/2解像度（Half-Res）でプリフィルタ＆散乱計算を行い、負荷を約1/4に下げつつカメラ移動時の遠景の発光チラつきを解消します")]
        [SerializeField] private bool _useHalfResolutionBuffer = true;

        [Tooltip("エディタのSceneビューにも適用するか")]
        [SerializeField] private bool _applyToSceneView = true;

        [Tooltip("描画パスを差し込むタイミング。BeforeRenderingTransparents にすると手前のコースラインやホログラムがボケません")]
        [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

        // =============================================================
        // 2. 拡張機能トグル（個別 ON/OFF 可能）
        // =============================================================
        [Header("拡張機能トグル（個別有効化）")]
        [Tooltip("2段距離カーブ（Dual-Range）：近景視認性を保護しつつ2000〜3000mの超遠景まで滑らかに大気遠近法を適用")]
        [SerializeField] private bool _enableDualRange = false;

        [Tooltip("高度フォグ（Height Fog）：基準高度から上空に向かって指数減衰し、地平線や下向き視点でのオーバーフローを防止")]
        [SerializeField] private bool _enableHeightFog = false;

        [Tooltip("太陽方向 Mie 散乱：太陽（Directional Light）方向を向いた際の空気中の前方散乱グロー")]
        [SerializeField] private bool _enableMieScattering = false;

        [Tooltip("昼夜プロファイル補間（Time of Day）：昼/夕/夜の各プロファイルを時刻値に応じて滑らかに補間")]
        [SerializeField] private bool _enableTimeOfDay = false;

        // =============================================================
        // 3. 昼夜・時刻設定
        // =============================================================
        [Header("昼夜・時刻設定（Time of Day）")]
        [Tooltip("時刻の決定ソース（手動スライダー または 太陽光仰角連動）")]
        [SerializeField] private TimeSourceMode _timeSource = TimeSourceMode.Manual;

        [Tooltip("時刻値（0.0=昼, 0.5=夕暮れ, 1.0=夜）")]
        [Range(0f, 1f)]
        [SerializeField] private float _timeOfDay = 0.0f;

        [Tooltip("太陽光参照ライト（未設定時は RenderSettings.sun を自動参照・ライト設定値は読み取るだけで書き換えません）")]
        [SerializeField] private Light _sunLight;

        // =============================================================
        // 4. プロファイル束
        // =============================================================
        [Header("プロファイル設定")]
        [Tooltip("TimeOfDay OFF 時に使用される単一アクティブプロファイル")]
        [SerializeField] private AtmosphereProfileV2 _activeProfile;

        [Tooltip("TimeOfDay ON 時に使用される昼プロファイル (TimeOfDay = 0.0)")]
        [SerializeField] private AtmosphereProfileV2 _dayProfile;

        [Tooltip("TimeOfDay ON 時に使用される夕方プロファイル (TimeOfDay = 0.5)")]
        [SerializeField] private AtmosphereProfileV2 _sunsetProfile;

        [Tooltip("TimeOfDay ON 時に使用される夜プロファイル (TimeOfDay = 1.0)")]
        [SerializeField] private AtmosphereProfileV2 _nightProfile;

        // =============================================================
        // 5. マテリアル参照
        // =============================================================
        [Header("参照マテリアル（未設定時は自動生成）")]
        [SerializeField] private Material _scatteringMaterial;

        private AtmosphericScatteringV2RenderPass _renderPass;
        private bool _createdRuntimeMaterial;

        // シェーダープロパティIDキャッシュ
        private static readonly int IdOverallStrength = Shader.PropertyToID("_OverallStrength");

        private static readonly int IdStartDistance = Shader.PropertyToID("_StartDistance");
        private static readonly int IdMaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int IdDensityCurve = Shader.PropertyToID("_DensityCurve");
        private static readonly int IdSkyboxInfluence = Shader.PropertyToID("_SkyboxInfluence");

        private static readonly int IdASV2UseDualRange = Shader.PropertyToID("_ASV2_UseDualRange");
        private static readonly int IdASV2NearEndDistance = Shader.PropertyToID("_ASV2_NearEndDistance");
        private static readonly int IdASV2FarEndDistance = Shader.PropertyToID("_ASV2_FarEndDistance");
        private static readonly int IdASV2NearMaxAirMass = Shader.PropertyToID("_ASV2_NearMaxAirMass");
        private static readonly int IdASV2FarDensityCurve = Shader.PropertyToID("_ASV2_FarDensityCurve");

        private static readonly int IdBlurRadius = Shader.PropertyToID("_BlurRadius");
        private static readonly int IdDepthEdgeSharpness = Shader.PropertyToID("_DepthEdgeSharpness");

        private static readonly int IdScatterIntensity = Shader.PropertyToID("_ScatterIntensity");
        private static readonly int IdScatterRadius = Shader.PropertyToID("_ScatterRadius");
        private static readonly int IdScatterThreshold = Shader.PropertyToID("_ScatterThreshold");
        private static readonly int IdScatterSoftKnee = Shader.PropertyToID("_ScatterSoftKnee");
        private static readonly int IdScatterTint = Shader.PropertyToID("_ScatterTint");
        private static readonly int IdLightColorPreservation = Shader.PropertyToID("_LightColorPreservation");

        private static readonly int IdMaxScatterLuminance = Shader.PropertyToID("_MaxScatterLuminance");
        private static readonly int IdAntiFlickerStrength = Shader.PropertyToID("_AntiFlickerStrength");
        private static readonly int IdNoiseJitter = Shader.PropertyToID("_NoiseJitter");

        private static readonly int IdAtmosphereColor = Shader.PropertyToID("_AtmosphereColor");
        private static readonly int IdAtmosphereDensity = Shader.PropertyToID("_AtmosphereDensity");
        private static readonly int IdContrastReduction = Shader.PropertyToID("_ContrastReduction");

        private static readonly int IdASV2HeightFogStrength = Shader.PropertyToID("_ASV2_HeightFogStrength");
        private static readonly int IdASV2FogBaseHeight = Shader.PropertyToID("_ASV2_FogBaseHeight");
        private static readonly int IdASV2HeightFalloff = Shader.PropertyToID("_ASV2_HeightFalloff");
        private static readonly int IdASV2SkyHeightClear = Shader.PropertyToID("_ASV2_SkyHeightClear");

        private static readonly int IdASV2MieIntensity = Shader.PropertyToID("_ASV2_MieIntensity");
        private static readonly int IdASV2MieAnisotropy = Shader.PropertyToID("_ASV2_MieAnisotropy");
        private static readonly int IdASV2SkyboxMieInfluence = Shader.PropertyToID("_ASV2_SkyboxMieInfluence");
        private static readonly int IdASV2MieMaxLuminance = Shader.PropertyToID("_ASV2_MieMaxLuminance");
        private static readonly int IdASV2MieTint = Shader.PropertyToID("_ASV2_MieTint");
        private static readonly int IdASV2SunDirection = Shader.PropertyToID("_ASV2_SunDirection");
        private static readonly int IdASV2SunColor = Shader.PropertyToID("_ASV2_SunColor");

        private static readonly int IdASV2HalfResScatteringTex = Shader.PropertyToID("_ASV2_HalfResScatteringTex");

        // =============================================================
        // プロパティアクセサ
        // =============================================================
        public bool EnableEffect
        {
            get => _enableEffect;
            set { _enableEffect = value; UpdateMaterialProperties(); }
        }

        public float OverallStrength
        {
            get => _overallStrength;
            set { _overallStrength = Mathf.Clamp01(value); UpdateMaterialProperties(); }
        }

        public bool UseHalfResolutionBuffer
        {
            get => _useHalfResolutionBuffer;
            set { _useHalfResolutionBuffer = value; UpdateMaterialProperties(); }
        }

        public bool ApplyToSceneView
        {
            get => _applyToSceneView;
            set => _applyToSceneView = value;
        }

        public RenderPassEvent InjectionPoint
        {
            get => _injectionPoint;
            set => _injectionPoint = value;
        }

        public bool EnableDualRange
        {
            get => _enableDualRange;
            set { _enableDualRange = value; UpdateMaterialProperties(); }
        }

        public bool EnableHeightFog
        {
            get => _enableHeightFog;
            set { _enableHeightFog = value; UpdateMaterialProperties(); }
        }

        public bool EnableMieScattering
        {
            get => _enableMieScattering;
            set { _enableMieScattering = value; UpdateMaterialProperties(); }
        }

        public bool EnableTimeOfDay
        {
            get => _enableTimeOfDay;
            set { _enableTimeOfDay = value; UpdateMaterialProperties(); }
        }

        public TimeSourceMode TimeSource
        {
            get => _timeSource;
            set { _timeSource = value; UpdateMaterialProperties(); }
        }

        public float TimeOfDay
        {
            get => _timeOfDay;
            set { _timeOfDay = Mathf.Clamp01(value); UpdateMaterialProperties(); }
        }

        public Light SunLight
        {
            get => _sunLight;
            set { _sunLight = value; UpdateMaterialProperties(); }
        }

        public AtmosphereProfileV2 ActiveProfile
        {
            get => _activeProfile;
            set { _activeProfile = value; UpdateMaterialProperties(); }
        }

        public AtmosphereProfileV2 DayProfile
        {
            get => _dayProfile;
            set { _dayProfile = value; UpdateMaterialProperties(); }
        }

        public AtmosphereProfileV2 SunsetProfile
        {
            get => _sunsetProfile;
            set { _sunsetProfile = value; UpdateMaterialProperties(); }
        }

        public AtmosphereProfileV2 NightProfile
        {
            get => _nightProfile;
            set { _nightProfile = value; UpdateMaterialProperties(); }
        }

        /// <summary>
        /// 実効時刻（SunElevationモード時は太陽仰角から自動算出された値、Manual時は_timeOfDay）
        /// </summary>
        public float EffectiveTimeOfDay
        {
            get
            {
                if (_timeSource == TimeSourceMode.SunElevation)
                {
                    return ComputeTimeFromSunElevation();
                }
                return _timeOfDay;
            }
        }

        /// <summary>
        /// 現在のフォグ計算が到達する実効最長距離（Dual-Range時はfarEndDistance、通常時はmaxDistance）
        /// </summary>
        public float EffectiveFarDistance
        {
            get
            {
                AtmosphereProfileV2 p = GetCurrentEffectiveProfile();
                if (_enableDualRange)
                {
                    return Mathf.Max(p.maxDistance, p.farEndDistance);
                }
                return p.maxDistance;
            }
        }

        // =============================================================
        // ライフサイクル（Updateメソッドは作らない）
        // =============================================================
        private void Reset()
        {
            EnsureProfilesInitialized();
        }

        private void OnEnable()
        {
            EnsureProfilesInitialized();
            EnsureResources();
            UpdateMaterialProperties();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            CleanupRuntimeMaterial();
        }

        private void OnValidate()
        {
            EnsureProfilesInitialized();

            // 距離の妥当性保護
            if (_activeProfile.maxDistance < _activeProfile.startDistance + 1f)
            {
                _activeProfile.maxDistance = _activeProfile.startDistance + 1f;
            }
            if (_activeProfile.nearEndDistance < _activeProfile.startDistance + 1f)
            {
                _activeProfile.nearEndDistance = _activeProfile.startDistance + 1f;
            }
            if (_activeProfile.farEndDistance < _activeProfile.nearEndDistance + 1f)
            {
                _activeProfile.farEndDistance = _activeProfile.nearEndDistance + 1f;
            }

            if (_dayProfile.maxDistance < _dayProfile.startDistance + 1f)
            {
                _dayProfile.maxDistance = _dayProfile.startDistance + 1f;
            }
            if (_sunsetProfile.maxDistance < _sunsetProfile.startDistance + 1f)
            {
                _sunsetProfile.maxDistance = _sunsetProfile.startDistance + 1f;
            }
            if (_nightProfile.maxDistance < _nightProfile.startDistance + 1f)
            {
                _nightProfile.maxDistance = _nightProfile.startDistance + 1f;
            }

            UpdateMaterialProperties();
        }

        /// <summary>
        /// ゼロ初期化トラップを確実に防止し、未初期化プロファイルに安全なデフォルト値を代入します。
        /// </summary>
        public void EnsureProfilesInitialized()
        {
            if (!_activeProfile.isInitialized)
            {
                _activeProfile = AtmosphereProfileV2.CreateDefaultV1CompatibleNight();
            }
            if (!_dayProfile.isInitialized)
            {
                _dayProfile = AtmosphereProfileV2.CreateDefaultDay();
            }
            if (!_sunsetProfile.isInitialized)
            {
                _sunsetProfile = AtmosphereProfileV2.CreateDefaultSunset();
            }
            if (!_nightProfile.isInitialized)
            {
                _nightProfile = AtmosphereProfileV2.CreateDefaultNight();
            }
        }

        private Light GetActiveSunLight()
        {
            if (_sunLight != null)
            {
                return _sunLight;
            }
            return RenderSettings.sun;
        }

        /// <summary>
        /// 太陽光（Directional Light）の仰角から時刻値（0.0〜1.0）を自動算出します。
        /// Light コンポーネントは読み取るだけで一切変更しません。
        /// </summary>
        public float ComputeTimeFromSunElevation()
        {
            Light sun = GetActiveSunLight();
            if (sun == null)
            {
                return _timeOfDay;
            }

            Vector3 sunDir = -sun.transform.forward;
            float sinElev = sunDir.y;

            // 仕様書 §4-1：
            // sunDir.y >= 0.25f（仰角約 +14.5°以上）→ 0.0（昼）
            // sunDir.y == 0.0f（日の出・日の入り）→ 0.5（夕）
            // sunDir.y <= -0.18f（仰角約 -10.4°以下）→ 1.0（夜）
            if (sinElev >= 0.25f)
            {
                return 0.0f;
            }
            if (sinElev >= 0.0f)
            {
                float factor = Mathf.InverseLerp(0.25f, 0.0f, sinElev);
                return Mathf.SmoothStep(0.0f, 0.5f, factor);
            }
            if (sinElev >= -0.18f)
            {
                float factor = Mathf.InverseLerp(0.0f, -0.18f, sinElev);
                return Mathf.SmoothStep(0.5f, 1.0f, factor);
            }
            return 1.0f;
        }

        /// <summary>
        /// 現在の有効な大気プロファイルを取得します（TimeOfDay有効時は補間結果を返す）。
        /// </summary>
        public AtmosphereProfileV2 GetCurrentEffectiveProfile()
        {
            EnsureProfilesInitialized();
            if (!_enableTimeOfDay)
            {
                return _activeProfile;
            }

            float t = EffectiveTimeOfDay;
            if (t <= 0.5f)
            {
                return AtmosphereProfileV2.Lerp(_dayProfile, _sunsetProfile, t * 2f);
            }
            else
            {
                return AtmosphereProfileV2.Lerp(_sunsetProfile, _nightProfile, (t - 0.5f) * 2f);
            }
        }

        private static Shader FindOrLoadShader(string shaderName, string assetPath)
        {
            Shader s = Shader.Find(shaderName);
#if UNITY_EDITOR
            if (s == null && !string.IsNullOrEmpty(assetPath))
            {
                s = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            }
#endif
            return s;
        }

        private void EnsureResources()
        {
            Shader shader = FindOrLoadShader(
                "Hidden/AT_Murayama/AtmosphericScatteringV2",
                "Assets/Deverop/AT_Murayama/AtmosphericScatteringV2/Shaders/S_AtmosphericScatteringV2.shader");

            if (_scatteringMaterial != null && (_scatteringMaterial.shader == null || _scatteringMaterial.shader != shader))
            {
                if (_createdRuntimeMaterial)
                {
                    CleanupRuntimeMaterial();
                }
                else if (shader != null)
                {
                    _scatteringMaterial.shader = shader;
                }
            }

            if (_scatteringMaterial == null && shader != null)
            {
                _scatteringMaterial = new Material(shader)
                {
                    name = "M_AtmosphericScatteringV2_Runtime",
                    hideFlags = HideFlags.DontSave
                };
                _createdRuntimeMaterial = true;
            }

            if (_renderPass == null)
            {
                _renderPass = new AtmosphericScatteringV2RenderPass("AtmosphericScatteringV2");
            }
        }

        private void CleanupRuntimeMaterial()
        {
            if (_createdRuntimeMaterial && _scatteringMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_scatteringMaterial);
                }
                else
                {
                    DestroyImmediate(_scatteringMaterial);
                }
                _scatteringMaterial = null;
                _createdRuntimeMaterial = false;
            }
        }

        /// <summary>
        /// シェーダーマテリアルの uniform 変数を更新します。
        /// トグルが OFF の機能は、プロファイルの値を書き換えずに無効値（0）をシェーダーへ渡します。
        /// </summary>
        public void UpdateMaterialProperties()
        {
            if (_scatteringMaterial == null)
            {
                EnsureResources();
                if (_scatteringMaterial == null) return;
            }

            AtmosphereProfileV2 p = GetCurrentEffectiveProfile();

            float effectiveStrength = _enableEffect ? _overallStrength : 0f;
            _scatteringMaterial.SetFloat(IdOverallStrength, effectiveStrength);

            // Optical Depth
            _scatteringMaterial.SetFloat(IdStartDistance, p.startDistance);
            _scatteringMaterial.SetFloat(IdMaxDistance, Mathf.Max(p.startDistance + 1f, p.maxDistance));
            _scatteringMaterial.SetFloat(IdDensityCurve, p.densityCurve);
            _scatteringMaterial.SetFloat(IdSkyboxInfluence, p.skyboxInfluence);

            // Dual-Range (P1)
            float useDualRange = _enableDualRange ? 1.0f : 0.0f;
            _scatteringMaterial.SetFloat(IdASV2UseDualRange, useDualRange);
            _scatteringMaterial.SetFloat(IdASV2NearEndDistance, Mathf.Max(p.startDistance + 1f, p.nearEndDistance));
            _scatteringMaterial.SetFloat(IdASV2FarEndDistance, Mathf.Max(p.nearEndDistance + 1f, p.farEndDistance));
            _scatteringMaterial.SetFloat(IdASV2NearMaxAirMass, p.nearMaxAirMass);
            _scatteringMaterial.SetFloat(IdASV2FarDensityCurve, p.farDensityCurve);

            // Blur
            _scatteringMaterial.SetFloat(IdBlurRadius, p.blurRadius);
            _scatteringMaterial.SetFloat(IdDepthEdgeSharpness, p.depthEdgeSharpness);

            // Light In-Scattering
            _scatteringMaterial.SetFloat(IdScatterIntensity, p.scatterIntensity);
            _scatteringMaterial.SetFloat(IdScatterRadius, p.scatterRadius);
            _scatteringMaterial.SetFloat(IdScatterThreshold, p.scatterThreshold);
            _scatteringMaterial.SetFloat(IdScatterSoftKnee, p.scatterSoftKnee);
            _scatteringMaterial.SetColor(IdScatterTint, p.scatterTint);
            _scatteringMaterial.SetFloat(IdLightColorPreservation, p.lightColorPreservation);

            // Anti-Flicker
            _scatteringMaterial.SetFloat(IdMaxScatterLuminance, p.maxScatterLuminance);
            _scatteringMaterial.SetFloat(IdAntiFlickerStrength, p.antiFlickerStrength);
            _scatteringMaterial.SetFloat(IdNoiseJitter, p.noiseJitter);

            // Aerial Perspective
            _scatteringMaterial.SetColor(IdAtmosphereColor, p.atmosphereColor);
            _scatteringMaterial.SetFloat(IdAtmosphereDensity, p.atmosphereDensity);
            _scatteringMaterial.SetFloat(IdContrastReduction, p.contrastReduction);

            // Height Fog (P2)
            float effectiveHeightFog = _enableHeightFog ? p.heightFogStrength : 0.0f;
            _scatteringMaterial.SetFloat(IdASV2HeightFogStrength, effectiveHeightFog);
            _scatteringMaterial.SetFloat(IdASV2FogBaseHeight, p.fogBaseHeight);
            _scatteringMaterial.SetFloat(IdASV2HeightFalloff, Mathf.Max(0.0001f, p.heightFalloff));
            _scatteringMaterial.SetFloat(IdASV2SkyHeightClear, Mathf.Max(0.1f, p.skyHeightClear));

            // Mie Scattering (P4)
            float effectiveMieIntensity = _enableMieScattering ? p.mieIntensity : 0.0f;
            _scatteringMaterial.SetFloat(IdASV2MieIntensity, effectiveMieIntensity);
            _scatteringMaterial.SetFloat(IdASV2MieAnisotropy, Mathf.Clamp(p.mieAnisotropy, 0f, 0.92f));
            _scatteringMaterial.SetFloat(IdASV2SkyboxMieInfluence, p.skyboxMieInfluence);
            _scatteringMaterial.SetFloat(IdASV2MieMaxLuminance, Mathf.Max(0.1f, p.mieMaxLuminance));
            _scatteringMaterial.SetColor(IdASV2MieTint, p.mieTint);

            // 太陽の向きと光色
            Light sun = GetActiveSunLight();
            Vector3 sunDirWS = Vector3.up;
            Color sunColor = Color.white;
            if (sun != null)
            {
                sunDirWS = -sun.transform.forward;
                sunColor = sun.color * sun.intensity;
            }
            _scatteringMaterial.SetVector(IdASV2SunDirection, new Vector4(sunDirWS.x, sunDirWS.y, sunDirWS.z, 0f));
            _scatteringMaterial.SetColor(IdASV2SunColor, sunColor);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!_enableEffect || _overallStrength <= 0.001f)
            {
                return;
            }

            CameraType camType = cam.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection)
            {
                return;
            }

            if (camType == CameraType.SceneView && !_applyToSceneView)
            {
                return;
            }

            // ミニマップ等の RenderTexture 出力カメラには大気散乱を適用しない
            if (cam.targetTexture != null && camType == CameraType.Game)
            {
                return;
            }

            EnsureResources();
            if (_scatteringMaterial == null || _renderPass == null)
            {
                return;
            }

            UpdateMaterialProperties();

            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            if (camData == null || camData.scriptableRenderer == null)
            {
                return;
            }

            _renderPass.Setup(_scatteringMaterial, _injectionPoint, _useHalfResolutionBuffer);
            camData.scriptableRenderer.EnqueuePass(_renderPass);
        }

        /// <summary>
        /// マスタープリセットをワンクリックで適用します。
        /// </summary>
        public void ApplyMasterPreset(MasterPresetV2 preset)
        {
            _enableEffect = true;
            _overallStrength = 1.0f;
            _useHalfResolutionBuffer = true;

            switch (preset)
            {
                case MasterPresetV2.CyberNight_V1Compatible:
                    _enableDualRange = false;
                    _enableHeightFog = false;
                    _enableMieScattering = false;
                    _enableTimeOfDay = false;
                    _activeProfile = AtmosphereProfileV2.CreateDefaultV1CompatibleNight();
                    break;

                case MasterPresetV2.RealisticDay_Clear:
                    _enableDualRange = true;
                    _enableHeightFog = true;
                    _enableMieScattering = true;
                    _enableTimeOfDay = false;
                    _activeProfile = AtmosphereProfileV2.CreateDefaultDay();
                    break;

                case MasterPresetV2.GoldenHour_Sunset:
                    _enableDualRange = true;
                    _enableHeightFog = true;
                    _enableMieScattering = true;
                    _enableTimeOfDay = false;
                    _activeProfile = AtmosphereProfileV2.CreateDefaultSunset();
                    break;

                case MasterPresetV2.MegaCity_DayNightAuto:
                    _enableDualRange = true;
                    _enableHeightFog = true;
                    _enableMieScattering = true;
                    _enableTimeOfDay = true;
                    _timeSource = TimeSourceMode.Manual;
                    _dayProfile = AtmosphereProfileV2.CreateDefaultDay();
                    _sunsetProfile = AtmosphereProfileV2.CreateDefaultSunset();
                    _nightProfile = AtmosphereProfileV2.CreateDefaultNight();
                    break;
            }

            UpdateMaterialProperties();
        }

        public void SetMaterialAsset(Material mat)
        {
            CleanupRuntimeMaterial();
            _scatteringMaterial = mat;
            _createdRuntimeMaterial = false;
            UpdateMaterialProperties();
        }

        public Material GetActiveMaterial()
        {
            EnsureResources();
            return _scatteringMaterial;
        }

        /// <summary>
        /// URP 17.3 Render Graph 対応の大気散乱V2レンダーパス（Half-Res 3パス ＆ Full-Res 1パス両対応）。
        /// グローバルテクスチャ _ASV2_HalfResScatteringTex により V1 との衝突を完全遮断。
        /// </summary>
        private class AtmosphericScatteringV2RenderPass : ScriptableRenderPass
        {
            private const int PassFullResSingle = 0;
            private const int PassHalfResPrefilter = 1;
            private const int PassHalfResMain = 2;
            private const int PassFullResComposite = 3;

            private readonly string _passName;
            private Material _material;
            private bool _useHalfRes;

            public AtmosphericScatteringV2RenderPass(string passName)
            {
                _passName = passName;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(Material material, RenderPassEvent injectionPoint, bool useHalfRes)
            {
                _material = material;
                _useHalfRes = useHalfRes;
                renderPassEvent = injectionPoint;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    return;
                }

                TextureDesc fullDesc = renderGraph.GetTextureDesc(source);
                fullDesc.clearBuffer = false;

                if (_useHalfRes && fullDesc.width >= 4 && fullDesc.height >= 4)
                {
                    RecordHalfResolutionPasses(renderGraph, resourceData, source, fullDesc);
                }
                else
                {
                    RecordFullResolutionSinglePass(renderGraph, resourceData, source, fullDesc);
                }
            }

            private void RecordFullResolutionSinglePass(
                RenderGraph renderGraph,
                UniversalResourceData resourceData,
                TextureHandle source,
                TextureDesc fullDesc)
            {
                fullDesc.name = _passName + "_TempFull";
                TextureHandle tempFull = renderGraph.CreateTexture(fullDesc);

                using (var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, tempFull, _material, PassFullResSingle),
                    _passName + "_FullResSingle",
                    returnBuilder: true))
                {
                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseAllGlobalTextures(true);
                }

                renderGraph.AddCopyPass(tempFull, source, _passName + "_CopyBack");
            }

            private void RecordHalfResolutionPasses(
                RenderGraph renderGraph,
                UniversalResourceData resourceData,
                TextureHandle source,
                TextureDesc fullDesc)
            {
                // 1/2 解像度（ピクセル数 1/4）のテクスチャディスクリプタを作成
                TextureDesc halfDesc = fullDesc;
                halfDesc.width = Mathf.Max(1, fullDesc.width / 2);
                halfDesc.height = Mathf.Max(1, fullDesc.height / 2);
                halfDesc.filterMode = FilterMode.Bilinear;
                halfDesc.clearBuffer = false;

                halfDesc.name = _passName + "_HalfPrefilter";
                TextureHandle halfPrefilter = renderGraph.CreateTexture(halfDesc);

                halfDesc.name = _passName + "_HalfScattered";
                TextureHandle halfScattered = renderGraph.CreateTexture(halfDesc);

                fullDesc.name = _passName + "_TempComposite";
                TextureHandle tempFull = renderGraph.CreateTexture(fullDesc);

                // Pass 1: フル解像度 → 1/2解像度へ 5タップ・バイラテラル＆Karisプリフィルタ縮小
                using (var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, halfPrefilter, _material, PassHalfResPrefilter),
                    _passName + "_HalfResPrefilter",
                    returnBuilder: true))
                {
                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseAllGlobalTextures(true);
                }

                // Pass 2: 1/2解像度上で大気散乱＆遠景ボケを計算し、_ASV2_HalfResScatteringTex にセット
                using (var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(halfPrefilter, halfScattered, _material, PassHalfResMain),
                    _passName + "_HalfResScattering",
                    returnBuilder: true))
                {
                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseAllGlobalTextures(true);
                    builder.SetGlobalTextureAfterPass(halfScattered, IdASV2HalfResScatteringTex);
                }

                // Pass 3: フル解像度の元画像と _ASV2_HalfResScatteringTex を深度バイラテラル・アップサンプル合成
                using (var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, tempFull, _material, PassFullResComposite),
                    _passName + "_FullResComposite",
                    returnBuilder: true))
                {
                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseGlobalTexture(IdASV2HalfResScatteringTex, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);
                }

                renderGraph.AddCopyPass(tempFull, source, _passName + "_CopyBack");
            }
        }
    }
}
