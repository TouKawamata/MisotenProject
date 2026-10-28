using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AT_Murayama.AtmosphericScattering
{
    /// <summary>
    /// 深度（空気の厚み）とそこに存在する発光・ライトから「遠景のチリによるボケ」と「光の散乱（In-Scattering）」を計算するコントローラー。
    /// ・Half-Res（1/2解像度）散乱バッファ＋バイラテラル合成により、負荷を約1/4に抑えつつカメラ移動時の遠景の発光チラつきを防止します。
    /// ・Far Clip ギリギリにある巨大オブジェクトも空（Skybox）と誤判定しない厳密な深度判定を備えています。
    /// ・PC_Renderer.asset（共有設定ファイル）を一切書き換えず、このコンポーネントをシーン内のオブジェクトにアタッチするだけで動作します。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AtmosphericScatteringController : MonoBehaviour
    {
        public enum ScatteringPreset
        {
            CyberpunkNight_Reference, // 添付画像再現（シアンブルー散乱＋遠景ビル霞み）
            HeavySmogCity,            // 濃霧・スモッグ都市（強い散乱とボケ）
            ClearNightHaze,           // クリアナイト（控えめな遠景の空気感）
            WarmSunsetHaze            // 夕暮れ・暖色スモッグ
        }

        [Header("有効化・解像度・カメラ適用設定")]
        [Tooltip("大気散乱エフェクトのON/OFF")]
        [SerializeField] private bool _enableEffect = true;

        [Tooltip("エフェクト全体のブレンド強度（0で完全に無効・パスもスキップ）")]
        [Range(0f, 1f)]
        [SerializeField] private float _overallStrength = 1.0f;

        [Tooltip("ON推奨：1/2解像度（Half-Res）でプリフィルタ＆散乱計算を行い、負荷を約1/4に下げつつカメラ移動時の遠景の発光チラつきを解消します")]
        [SerializeField] private bool _useHalfResolutionBuffer = true;

        [Tooltip("エディタのSceneビューにも適用するか")]
        [SerializeField] private bool _applyToSceneView = true;

        [Tooltip("描画パスを差し込むタイミング。BeforeRenderingTransparents にすると手前のコースラインやホログラムがボケません")]
        [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

        [Header("1. 空気の厚み（距離設定 / Optical Depth）")]
        [Tooltip("空気のチリによる霞み・散乱が始まる距離（メートル）")]
        [Min(0f)]
        [SerializeField] private float _startDistance = 35f;

        [Tooltip("空気の厚みが最大（100%）に達する距離（メートル）。これより奥（Farギリギリまで）も100%で維持されます")]
        [Min(10f)]
        [SerializeField] private float _maxDistance = 400f;

        [Tooltip("距離に応じた空気の厚みの増え方カーブ（小さいほど手前から緩やかに立ち上がる）")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _densityCurve = 0.85f;

        [Tooltip("空（Skybox）自体に大気のベース色をどの程度乗せるか（低くすると夜空の暗さを保ちつつビルの光漏れだけ乗る）")]
        [Range(0f, 1f)]
        [SerializeField] private float _skyboxInfluence = 0.08f;

        [Header("2. 遠景のチリによる像のボケ（Atmospheric Blur）")]
        [Tooltip("遠景オブジェクト自体の輪郭やディテールが空気のチリでぼやける半径")]
        [Range(0f, 8.0f)]
        [SerializeField] private float _blurRadius = 2.4f;

        [Tooltip("手前のオブジェクト（コースやモノレールや自機）の輪郭に奥のボケが食い込むのを防ぐ保護強度")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _depthEdgeSharpness = 0.85f;

        [Header("3. 発光・ライトの空気チリ散乱（Light In-Scattering）")]
        [Tooltip("遠景にある窓・ネオン・街灯などの光が周囲の空気のチリに散乱して明るく滲む強さ")]
        [Range(0f, 4.0f)]
        [SerializeField] private float _scatterIntensity = 1.65f;

        [Tooltip("光が周囲の空気に散乱して広がる半径")]
        [Range(0.5f, 16.0f)]
        [SerializeField] private float _scatterRadius = 7.5f;

        [Tooltip("どのくらいの明るさ（輝度）以上のピクセルを『光源（散乱元）』として扱うか")]
        [Range(0f, 2.0f)]
        [SerializeField] private float _scatterThreshold = 0.32f;

        [Tooltip("しきい値付近の滑らかさ")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float _scatterSoftKnee = 0.5f;

        [Tooltip("空気のチリが光を散乱するときの色味（サイバーパンクの青白い湿った空気感）")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _scatterTint = new Color(0.35f, 0.82f, 1.18f, 1.0f);

        [Tooltip("散乱光に元の光源の色（赤い看板なら赤、黄色いライトなら黄色）をどれだけ残すか")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _lightColorPreservation = 0.55f;

        [Header("4. 遠景の発光チラつき防止（Anti-Flicker / Firefly Suppression）")]
        [Tooltip("カメラ移動時に遠景の1ピクセルHDR光がチカチカ暴れるのを抑える輝度重み（Karis Average）強度")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _antiFlickerStrength = 0.85f;

        [Tooltip("散乱計算に入力する最大輝度の上限クランプ（小さいほど遠景の激しい点滅が落ち着く）")]
        [Range(1.0f, 32.0f)]
        [SerializeField] private float _maxScatterLuminance = 8.0f;

        [Tooltip("サンプル回転のランダムジッター量（0にするとカメラ移動時のザラザラしたノイズ揺れが完全に消えます）")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _noiseJitter = 0.0f;

        [Header("5. 大気の環境散乱・暗部シルエット化（Aerial Perspective）")]
        [Tooltip("遠景のビルの黒い影の部分が空気の厚みで浮き上がるベース散乱色")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _atmosphereColor = new Color(0.08f, 0.24f, 0.39f, 1.0f);

        [Tooltip("遠景の暗部（黒）を大気色で持ち上げてシルエット化する濃度")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _atmosphereDensity = 0.58f;

        [Tooltip("遠景に行くほど本来のコントラストを減衰させる割合")]
        [Range(0f, 1.0f)]
        [SerializeField] private float _contrastReduction = 0.45f;

        [Header("参照マテリアル（未設定時は自動生成）")]
        [SerializeField] private Material _scatteringMaterial;

        [Header("テスト用・遠景ビル群生成設定（動作確認用）")]
        [Tooltip("テスト生成した遠景ビル群の親オブジェクト")]
        [SerializeField] private GameObject _testCityRoot;

        private AtmosphericScatteringRenderPass _renderPass;
        private bool _createdRuntimeMaterial;

        // シェーダープロパティIDキャッシュ
        private static readonly int IdOverallStrength = Shader.PropertyToID("_OverallStrength");
        private static readonly int IdStartDistance = Shader.PropertyToID("_StartDistance");
        private static readonly int IdMaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int IdDensityCurve = Shader.PropertyToID("_DensityCurve");
        private static readonly int IdSkyboxInfluence = Shader.PropertyToID("_SkyboxInfluence");
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
        private static readonly int IdHalfResScatteringTex = Shader.PropertyToID("_HalfResScatteringTex");

        public bool EnableEffect
        {
            get => _enableEffect;
            set
            {
                _enableEffect = value;
                UpdateMaterialProperties();
            }
        }

        public bool UseHalfResolutionBuffer
        {
            get => _useHalfResolutionBuffer;
            set
            {
                _useHalfResolutionBuffer = value;
                UpdateMaterialProperties();
            }
        }

        public bool HasTestCity => _testCityRoot != null;

        private void OnEnable()
        {
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
            if (_maxDistance < _startDistance + 1f)
            {
                _maxDistance = _startDistance + 1f;
            }
            UpdateMaterialProperties();
        }

        private void Update()
        {
            UpdateMaterialProperties();
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
                "Hidden/AT_Murayama/AtmosphericScattering",
                "Assets/Deverop/AT_Murayama/AtmosphericScattering/Shaders/S_AtmosphericScattering.shader");

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
                    name = "M_AtmosphericScattering_Runtime",
                    hideFlags = HideFlags.DontSave
                };
                _createdRuntimeMaterial = true;
            }

            if (_renderPass == null)
            {
                _renderPass = new AtmosphericScatteringRenderPass("AtmosphericScattering");
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

        public void UpdateMaterialProperties()
        {
            if (_scatteringMaterial == null)
            {
                EnsureResources();
                if (_scatteringMaterial == null) return;
            }

            float effectiveStrength = _enableEffect ? _overallStrength : 0f;
            _scatteringMaterial.SetFloat(IdOverallStrength, effectiveStrength);
            _scatteringMaterial.SetFloat(IdStartDistance, _startDistance);
            _scatteringMaterial.SetFloat(IdMaxDistance, Mathf.Max(_startDistance + 1f, _maxDistance));
            _scatteringMaterial.SetFloat(IdDensityCurve, _densityCurve);
            _scatteringMaterial.SetFloat(IdSkyboxInfluence, _skyboxInfluence);

            _scatteringMaterial.SetFloat(IdBlurRadius, _blurRadius);
            _scatteringMaterial.SetFloat(IdDepthEdgeSharpness, _depthEdgeSharpness);

            _scatteringMaterial.SetFloat(IdScatterIntensity, _scatterIntensity);
            _scatteringMaterial.SetFloat(IdScatterRadius, _scatterRadius);
            _scatteringMaterial.SetFloat(IdScatterThreshold, _scatterThreshold);
            _scatteringMaterial.SetFloat(IdScatterSoftKnee, _scatterSoftKnee);
            _scatteringMaterial.SetColor(IdScatterTint, _scatterTint);
            _scatteringMaterial.SetFloat(IdLightColorPreservation, _lightColorPreservation);

            _scatteringMaterial.SetFloat(IdMaxScatterLuminance, _maxScatterLuminance);
            _scatteringMaterial.SetFloat(IdAntiFlickerStrength, _antiFlickerStrength);
            _scatteringMaterial.SetFloat(IdNoiseJitter, _noiseJitter);

            _scatteringMaterial.SetColor(IdAtmosphereColor, _atmosphereColor);
            _scatteringMaterial.SetFloat(IdAtmosphereDensity, _atmosphereDensity);
            _scatteringMaterial.SetFloat(IdContrastReduction, _contrastReduction);
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

            // ミニマップ等のサブカメラ（RenderTexture出力）には適用しない
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
        /// インスペクターからワンクリックで空気感プリセットを適用します。
        /// </summary>
        public void ApplyPreset(ScatteringPreset preset)
        {
            _enableEffect = true;
            _overallStrength = 1.0f;
            _antiFlickerStrength = 0.85f;
            _maxScatterLuminance = 8.0f;
            _noiseJitter = 0.0f;

            switch (preset)
            {
                case ScatteringPreset.CyberpunkNight_Reference:
                    _startDistance = 35f;
                    _maxDistance = 400f;
                    _densityCurve = 0.85f;
                    _skyboxInfluence = 0.08f;
                    _blurRadius = 2.4f;
                    _depthEdgeSharpness = 0.85f;
                    _scatterIntensity = 1.65f;
                    _scatterRadius = 7.5f;
                    _scatterThreshold = 0.32f;
                    _scatterSoftKnee = 0.5f;
                    _scatterTint = new Color(0.35f, 0.82f, 1.18f, 1.0f);
                    _lightColorPreservation = 0.55f;
                    _atmosphereColor = new Color(0.08f, 0.24f, 0.39f, 1.0f);
                    _atmosphereDensity = 0.58f;
                    _contrastReduction = 0.45f;
                    break;

                case ScatteringPreset.HeavySmogCity:
                    _startDistance = 20f;
                    _maxDistance = 300f;
                    _densityCurve = 0.7f;
                    _skyboxInfluence = 0.18f;
                    _blurRadius = 4.2f;
                    _depthEdgeSharpness = 0.85f;
                    _scatterIntensity = 2.4f;
                    _scatterRadius = 10.5f;
                    _scatterThreshold = 0.25f;
                    _scatterSoftKnee = 0.6f;
                    _scatterTint = new Color(0.4f, 0.88f, 1.25f, 1.0f);
                    _lightColorPreservation = 0.45f;
                    _atmosphereColor = new Color(0.11f, 0.28f, 0.44f, 1.0f);
                    _atmosphereDensity = 0.72f;
                    _contrastReduction = 0.6f;
                    break;

                case ScatteringPreset.ClearNightHaze:
                    _startDistance = 80f;
                    _maxDistance = 600f;
                    _densityCurve = 1.1f;
                    _skyboxInfluence = 0.03f;
                    _blurRadius = 1.2f;
                    _depthEdgeSharpness = 0.9f;
                    _scatterIntensity = 0.95f;
                    _scatterRadius = 5.0f;
                    _scatterThreshold = 0.45f;
                    _scatterSoftKnee = 0.4f;
                    _scatterTint = new Color(0.3f, 0.7f, 1.0f, 1.0f);
                    _lightColorPreservation = 0.7f;
                    _atmosphereColor = new Color(0.05f, 0.15f, 0.28f, 1.0f);
                    _atmosphereDensity = 0.35f;
                    _contrastReduction = 0.28f;
                    break;

                case ScatteringPreset.WarmSunsetHaze:
                    _startDistance = 35f;
                    _maxDistance = 420f;
                    _densityCurve = 0.85f;
                    _skyboxInfluence = 0.12f;
                    _blurRadius = 2.5f;
                    _depthEdgeSharpness = 0.85f;
                    _scatterIntensity = 1.7f;
                    _scatterRadius = 8.0f;
                    _scatterThreshold = 0.35f;
                    _scatterSoftKnee = 0.5f;
                    _scatterTint = new Color(1.1f, 0.65f, 0.4f, 1.0f);
                    _lightColorPreservation = 0.6f;
                    _atmosphereColor = new Color(0.28f, 0.16f, 0.22f, 1.0f);
                    _atmosphereDensity = 0.55f;
                    _contrastReduction = 0.45f;
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
        /// 添付画像左奥のような「手前〜遠景に並ぶ高層ビル群＋地上付近に密集したネオン・街灯発光」をテスト生成します。
        /// </summary>
        public void GenerateTestCyberCity()
        {
            ClearTestCyberCity();

            Shader buildingShader = FindOrLoadShader(
                "AT_Murayama/AtmosphericScattering/CyberCityTestBuilding",
                "Assets/Deverop/AT_Murayama/AtmosphericScattering/Shaders/S_CyberCityTestBuilding.shader");
            if (buildingShader == null)
            {
                Debug.LogWarning("テストビル用シェーダー AT_Murayama/AtmosphericScattering/CyberCityTestBuilding が見つかりません。");
                return;
            }

            _testCityRoot = new GameObject("TestCyberCity_Preview (ワンクリック削除可能)");
            _testCityRoot.transform.SetParent(transform, false);
            _testCityRoot.transform.localPosition = Vector3.zero;
            _testCityRoot.transform.localRotation = Quaternion.identity;

            Transform refTr = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forwardFlat = Vector3.ProjectOnPlane(refTr.forward, Vector3.up).normalized;
            if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
            Quaternion basisRot = Quaternion.LookRotation(forwardFlat, Vector3.up);
            Vector3 originPos = refTr.position;
            originPos.y -= 12f;

            Material[] buildingMats = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                Material m = new Material(buildingShader)
                {
                    name = $"M_TestBuilding_{i}"
                };
                m.SetFloat("_BuildingSeed", (i + 1) * 17.3f);
                buildingMats[i] = m;
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Material streetNeonCyan = null;
            Material streetNeonWarm = null;
            Material streetNeonRed = null;
            if (unlitShader != null)
            {
                streetNeonCyan = new Material(unlitShader);
                streetNeonCyan.SetColor("_BaseColor", new Color(0.3f, 2.8f, 4.5f, 1f));

                streetNeonWarm = new Material(unlitShader);
                streetNeonWarm.SetColor("_BaseColor", new Color(3.5f, 2.2f, 0.8f, 1f));

                streetNeonRed = new Material(unlitShader);
                streetNeonRed.SetColor("_BaseColor", new Color(3.8f, 0.4f, 0.6f, 1f));
            }

            Random.State oldState = Random.state;
            Random.InitState(20261027);

            int buildingCount = 42;
            for (int i = 0; i < buildingCount; i++)
            {
                float t = (float)i / (buildingCount - 1);
                float dist = Mathf.Lerp(65f, 380f, t) + Random.Range(-12f, 12f);

                float sideOffset = (i % 3 == 0)
                    ? Random.Range(25f, 160f)
                    : Random.Range(-180f, -15f);

                float width = Random.Range(16f, 38f);
                float depth = Random.Range(16f, 38f);
                float height = Random.Range(75f, 240f);

                GameObject bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bldg.name = $"Building_{i:00}_Dist{Mathf.RoundToInt(dist)}m";
                bldg.transform.SetParent(_testCityRoot.transform, true);

                Collider col = bldg.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);

                Vector3 worldPos = originPos + basisRot * new Vector3(sideOffset, height * 0.5f, dist);
                bldg.transform.position = worldPos;
                bldg.transform.rotation = basisRot * Quaternion.Euler(0f, Random.Range(-8f, 8f), 0f);
                bldg.transform.localScale = new Vector3(width, height, depth);

                Renderer r = bldg.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = buildingMats[i % buildingMats.Length];
                }

                if (streetNeonCyan != null)
                {
                    int streetLights = Random.Range(2, 5);
                    for (int s = 0; s < streetLights; s++)
                    {
                        GameObject lightBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        lightBox.name = $"StreetGlow_{s}";
                        lightBox.transform.SetParent(bldg.transform, false);

                        Collider lCol = lightBox.GetComponent<Collider>();
                        if (lCol != null) DestroyImmediate(lCol);

                        float lx = Random.Range(-0.55f, 0.55f);
                        float ly = Random.Range(-0.48f, -0.32f);
                        float lz = -0.52f;
                        lightBox.transform.localPosition = new Vector3(lx, ly, lz);
                        lightBox.transform.localScale = new Vector3(Random.Range(0.08f, 0.25f), Random.Range(0.02f, 0.08f), 0.05f);

                        Renderer lr = lightBox.GetComponent<Renderer>();
                        if (lr != null)
                        {
                            int colorPick = (i + s) % 5;
                            lr.sharedMaterial = (colorPick == 0) ? streetNeonRed : (colorPick == 1 ? streetNeonWarm : streetNeonCyan);
                        }
                    }
                }
            }

            GameObject foregroundBridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foregroundBridge.name = "Foreground_Overpass_Dist35m";
            foregroundBridge.transform.SetParent(_testCityRoot.transform, true);
            Collider fbCol = foregroundBridge.GetComponent<Collider>();
            if (fbCol != null) DestroyImmediate(fbCol);

            foregroundBridge.transform.position = originPos + basisRot * new Vector3(10f, 18f, 42f);
            foregroundBridge.transform.rotation = basisRot * Quaternion.Euler(0f, -25f, 0f);
            foregroundBridge.transform.localScale = new Vector3(8f, 5f, 95f);
            Renderer fbRenderer = foregroundBridge.GetComponent<Renderer>();
            if (fbRenderer != null)
            {
                fbRenderer.sharedMaterial = buildingMats[0];
            }

            Random.state = oldState;
        }

        public void ClearTestCyberCity()
        {
            if (_testCityRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_testCityRoot);
                }
                else
                {
                    DestroyImmediate(_testCityRoot);
                }
                _testCityRoot = null;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("TestCyberCity_Preview"))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// URP 17.3 Render Graph 対応の大気散乱レンダーパス（Half-Res 3パス ＆ Full-Res 1パス両対応）。
        /// </summary>
        private class AtmosphericScatteringRenderPass : ScriptableRenderPass
        {
            private const int PassFullResSingle = 0;
            private const int PassHalfResPrefilter = 1;
            private const int PassHalfResMain = 2;
            private const int PassFullResComposite = 3;

            private readonly string _passName;
            private Material _material;
            private bool _useHalfRes;

            public AtmosphericScatteringRenderPass(string passName)
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

                // Pass 1: フル解像度 → 1/2解像度へ 5タップ・バイラテラル＆Karisプリフィルタ縮小（チラつき・ファイアフライ除去）
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

                // Pass 2: 1/2解像度上で大気散乱＆遠景ボケを計算し、結果をグローバルテクスチャ _HalfResScatteringTex にセット
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
                    builder.SetGlobalTextureAfterPass(halfScattered, IdHalfResScatteringTex);
                }

                // Pass 3: フル解像度の元画像と _HalfResScatteringTex を深度バイラテラル・アップサンプル合成（手前輪郭を1ピクセル単位で保護）
                using (var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, tempFull, _material, PassFullResComposite),
                    _passName + "_FullResComposite",
                    returnBuilder: true))
                {
                    if (resourceData.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseGlobalTexture(IdHalfResScatteringTex, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);
                }

                renderGraph.AddCopyPass(tempFull, source, _passName + "_CopyBack");
            }
        }
    }
}
