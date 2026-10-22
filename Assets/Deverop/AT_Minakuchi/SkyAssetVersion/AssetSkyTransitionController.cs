using UnityEngine;
using UnityEngine.Rendering;

namespace Minakuchi.AssetSky
{
    [DisallowMultipleComponent]
    public sealed class AssetSkyTransitionController : MonoBehaviour
    {
        public enum SkyState
        {
            [InspectorName("曇り")] Cloudy,
            [InspectorName("薄曇り")] PartlyCloudy,
            [InspectorName("夜空・星空")] Night,
            [InspectorName("大気圏付近")] UpperAtmosphere
        }

        public static string GetStateLabel(SkyState state)
        {
            switch (state)
            {
                case SkyState.PartlyCloudy: return "薄曇り";
                case SkyState.Night: return "夜空・星空";
                case SkyState.UpperAtmosphere: return "大気圏付近";
                default: return "曇り";
            }
        }

        [Header("空の設定")]
        public bool useAssetBackground = true;
        public Cubemap cloudySky, partlyCloudySky, nightSky, atmosphereSky;
        [Min(0)] public float backgroundExposure = 1;
        [Range(0, 360)] public float backgroundRotation;
        [Range(0, 1)] public float dynamicCloudOpacity = 0.25f;
        public bool additionalCelestials;
        public bool enableSunsetTint = true;
        public Color sunsetColor = new Color(1f, 0.28f, 0.055f, 1f);
        [Range(0, 1)] public float sunsetIntensity = 0.85f;
        [Range(0.05f, 1)] public float sunsetSpread = 0.85f;
        [SerializeField] private Material skyTemplate;
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private SkyState initialState = SkyState.Cloudy;
        [Min(0.1f)] public float transitionSeconds = 12;
        [Range(0, 1)] public float cloudCoverage = 0.72f;
        [Range(0, 1)] public float night;
        [Header("時刻と天体（12=正午、0=真夜中）")]
        [Range(0, 24)] public float timeOfDay = 10;
        public bool advanceTime;
        [Min(1)] public float dayLengthSeconds = 600;
        [Range(0, 1)] public float upperAtmosphere;
        public float cloudHeight = 1200;
        [Min(1)] public float cloudScale = 1800;
        public Vector2 wind = new Vector2(0.015f, 0.008f);
        [Tooltip("空や高度の切り替えに合わせて、雲を頭上から足元へ移動させます。OFFでも横方向には流れます。")]
        public bool changeCloudPosition;
        public bool enableCloudFlow = true;
        [Tooltip("雲の層の近くで画面全体を霞ませる演出。OFFなら白い霞を表示しません。再生中も切り替え可能。")]
        public bool enableCloudHaze = true;
        [Min(0)] public float cloudFlowSpeed = 4;
        [Tooltip("ONなら観測カメラの実際の高さで雲の通過と大気圏への移行を制御")]
        public bool followCameraAltitude;
        public Camera observerCamera;
        public float atmosphereStartHeight = 1600;
        public float atmosphereEndHeight = 6000;
        [Header("SkyTestScene用（他のシーンではOFF）")]
        public bool showDemoControls;
        [Tooltip("テストカメラの生成・移動・視線制御。OFFならゲームカメラを使う。再生中も切り替え可能")]
        public bool enableDemoCameraControl;

        private Material runtimeSky, previousSky;
        private Light previousSun;
        private AmbientMode previousAmbientMode;
        private Color previousSkyColor, previousEquator, previousGround;
        private float previousReflection;
        private Camera demoCamera;
        private Vector3 demoBasePosition;
        private float clock, altitude;
        private Vector2 cloudOffset;
        private float cloudHeightFrom, cloudHeightTarget;
        private Vector4 from, target;
        private float progress = 1;
        private bool ownsEnvironment;
        private bool demoTravel;
        private SkyState selected;
        public bool IsDemoActor(Collider actor) => showDemoControls && demoCamera && actor.transform == demoCamera.transform;

        private void OnEnable()
        {
            if (!skyTemplate) { Debug.LogError("空のマテリアルが設定されていません。", this); enabled = false; return; }
            previousSky = RenderSettings.skybox;
            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousSkyColor = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousReflection = RenderSettings.reflectionIntensity;
            runtimeSky = new Material(skyTemplate) { name = "空のマテリアル（実行中）" };
            RenderSettings.skybox = runtimeSky;
            ownsEnvironment = true;
            if (!observerCamera) observerCamera = Camera.main;
            SyncDemoCamera();
            selected = initialState;
            target = Preset(initialState);
            SetValues(target);
            Apply();
        }

        private Vector4 Preset(SkyState state)
        {
            switch (state)
            {
                case SkyState.PartlyCloudy: return new Vector4(0.34f, 10, 0, 0);
                case SkyState.Night: return new Vector4(0.10f, 22, 0, 0);
                case SkyState.UpperAtmosphere: return new Vector4(0.72f, timeOfDay, 1, atmosphereEndHeight);
                default: return new Vector4(0.72f, 10, 0, 0);
            }
        }

        public void TransitionTo(SkyState state)
        {
            TransitionTo(state, transitionSeconds, Preset(state).y);
        }

        /// <summary>Each trigger supplies its own duration and destination time.</summary>
        public void TransitionTo(SkyState state, float seconds, float destinationHour)
        {
            TransitionTo(state, seconds, destinationHour, null);
        }

        public void TransitionTo(SkyState state, float seconds, float destinationHour, float? destinationCloudHeight)
        {
            selected = state;
            transitionSeconds = Mathf.Max(0.1f, seconds);
            from = new Vector4(cloudCoverage, timeOfDay, upperAtmosphere, altitude);
            target = Preset(state);
            // Take the next forward occurrence, including a midnight crossing.
            target.y = timeOfDay + Mathf.Repeat(destinationHour - timeOfDay, 24);
            cloudHeightFrom = cloudHeight;
            cloudHeightTarget = destinationCloudHeight ?? cloudHeight;
            progress = 0;
        }

        [ContextMenu("曇りへ")] public void ToCloudy() => TransitionTo(SkyState.Cloudy);
        [ContextMenu("薄曇りへ")] public void ToPartlyCloudy() => TransitionTo(SkyState.PartlyCloudy);
        [ContextMenu("星空へ")] public void ToNight() => TransitionTo(SkyState.Night);
        [ContextMenu("大気圏付近へ")] public void ToUpperAtmosphere() => TransitionTo(SkyState.UpperAtmosphere);

        private void SetValues(Vector4 v)
        {
            cloudCoverage = v.x; timeOfDay = v.y; upperAtmosphere = v.z; altitude = v.w;
        }

        private void Update()
        {
            SyncDemoCamera();
            clock += Time.deltaTime;
            if (enableCloudFlow) cloudOffset += wind * (cloudFlowSpeed * Time.deltaTime);
            if (progress < 1)
            {
                progress = Mathf.Min(1, progress + Time.deltaTime / Mathf.Max(0.1f, transitionSeconds));
                SetValues(Vector4.Lerp(from, target, Mathf.SmoothStep(0, 1, progress)));
                cloudHeight = Mathf.Lerp(cloudHeightFrom, cloudHeightTarget, Mathf.SmoothStep(0, 1, progress));
            }
            else if (advanceTime) timeOfDay += Time.deltaTime * 24 / Mathf.Max(1, dayLengthSeconds);
            if (progress >= 1) timeOfDay = Mathf.Repeat(timeOfDay, 24);
            if (demoTravel && demoCamera) demoBasePosition += Vector3.forward * (Time.deltaTime * 12);
            // Altitude belongs to the sky effect; changing the sky must not move the camera.
            if (demoCamera) demoCamera.transform.position = demoBasePosition;
            Apply();
        }

        private void Apply()
        {
            if (!runtimeSky) return;
            bool hasBackgrounds = cloudySky && partlyCloudySky && nightSky && atmosphereSky;
            runtimeSky.SetFloat("_UseAssetSky", useAssetBackground && hasBackgrounds ? 1 : 0);
            if (hasBackgrounds)
            {
                runtimeSky.SetTexture("_CloudySky", cloudySky);
                runtimeSky.SetTexture("_PartlyCloudySky", partlyCloudySky);
                runtimeSky.SetTexture("_NightSky", nightSky);
                runtimeSky.SetTexture("_AtmosphereSky", atmosphereSky);
            }
            runtimeSky.SetFloat("_BackgroundExposure", backgroundExposure);
            runtimeSky.SetFloat("_BackgroundRotation", backgroundRotation);
            runtimeSky.SetFloat("_DynamicCloudOpacity", dynamicCloudOpacity);
            runtimeSky.SetFloat("_AdditionalCelestials", additionalCelestials ? 1 : 0);
            float hour = Mathf.Repeat(timeOfDay, 24);
            float sunset = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16, 18, hour))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 20, hour)));
            runtimeSky.SetFloat("_SunsetAmount", enableSunsetTint ? sunset : 0);
            runtimeSky.SetColor("_SunsetColor", sunsetColor);
            runtimeSky.SetFloat("_SunsetIntensity", sunsetIntensity);
            runtimeSky.SetFloat("_SunsetSpread", sunsetSpread);
            Camera view = demoCamera ? demoCamera : observerCamera;
            if (!view) { observerCamera = Camera.main; view = observerCamera; }
            float height = followCameraAltitude && view ? view.transform.position.y : altitude;
            float space = followCameraAltitude
                ? Mathf.InverseLerp(atmosphereStartHeight, Mathf.Max(atmosphereStartHeight + 1, atmosphereEndHeight), height)
                : upperAtmosphere;
            float angle = (timeOfDay - 6) * 15 * Mathf.Deg2Rad;
            Vector3 sunDirection = Quaternion.Euler(0, 25, 0) * new Vector3(0, Mathf.Sin(angle), Mathf.Cos(angle));
            Vector3 moonDirection = -sunDirection;
            night = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.15f, 0.12f, sunDirection.y));
            runtimeSky.SetFloat("_Night", night);
            runtimeSky.SetFloat("_Space", space);
            runtimeSky.SetFloat("_Clouds", cloudCoverage);
            runtimeSky.SetFloat("_Altitude", changeCloudPosition ? height : 0);
            runtimeSky.SetFloat("_CloudHeight", cloudHeight);
            runtimeSky.SetFloat("_CloudHaze", enableCloudHaze ? 1 : 0);
            runtimeSky.SetFloat("_CloudScale", cloudScale);
            runtimeSky.SetFloat("_Clock", clock);
            runtimeSky.SetVector("_Wind", new Vector4(wind.x, wind.y, 0, 0));
            runtimeSky.SetVector("_CloudOffset", new Vector4(cloudOffset.x, cloudOffset.y, 0, 0));
            runtimeSky.SetVector("_Observer", view ? (Vector4)view.transform.position : Vector4.zero);
            runtimeSky.SetVector("_SunDir", sunDirection);
            runtimeSky.SetVector("_MoonDir", moonDirection);
            if (sun)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDirection);
                sun.intensity = Mathf.Lerp(1.1f, 0, night) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(sunDirection.y * 4));
                sun.color = Color.Lerp(new Color(1, 0.38f, 0.15f), new Color(1, 0.94f, 0.84f), Mathf.Clamp01(sunDirection.y * 3));
            }
            if (moon)
            {
                moon.transform.rotation = Quaternion.LookRotation(-moonDirection);
                moon.intensity = night * 0.22f;
                moon.color = new Color(0.57f, 0.68f, 1);
            }
            RenderSettings.sun = night > 0.5f ? moon : sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.4f, 0.55f, 0.75f), new Color(0.025f, 0.04f, 0.09f), Mathf.Max(night, space * 0.8f));
            RenderSettings.ambientEquatorColor = RenderSettings.ambientSkyColor * 0.65f;
            RenderSettings.ambientGroundColor = RenderSettings.ambientSkyColor * 0.3f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(1, 0.15f, Mathf.Max(night, space));
        }

        private void SyncDemoCamera()
        {
            if (showDemoControls && enableDemoCameraControl)
            {
                if (!demoCamera) CreateDemoCamera();
            }
            else
            {
                ReleaseDemoCamera();
            }
        }

        private void ReleaseDemoCamera()
        {
            demoTravel = false;
            if (!demoCamera) return;
            // Disable immediately so the game camera resumes rendering in this frame.
            demoCamera.gameObject.SetActive(false);
            Destroy(demoCamera.gameObject);
            demoCamera = null;
        }

        private void CreateDemoCamera()
        {
            var go = new GameObject("テスト用カメラ（実行中）");
            go.transform.SetParent(transform, false);
            demoCamera = go.AddComponent<Camera>();
            if (observerCamera) demoCamera.CopyFrom(observerCamera);
            demoCamera.clearFlags = CameraClearFlags.Skybox;
            demoCamera.depth = 100;
            demoCamera.farClipPlane = 50000;
            demoCamera.targetTexture = null;
            go.tag = "Player";
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            go.AddComponent<SphereCollider>().radius = 0.5f;
            demoBasePosition = new Vector3(0, 8, -20);
            go.transform.SetPositionAndRotation(demoBasePosition, Quaternion.Euler(-15, 0, 0));
        }

        private void OnGUI()
        {
            if (!showDemoControls || !runtimeSky) return;
            GUILayout.BeginArea(new Rect(16, 16, 340, 430), GUI.skin.box);
            GUILayout.Label("空のテスト ／ " + GetStateLabel(selected));
            GUILayout.Label("曇り・薄曇りから夜空・大気圏へ");
            enableDemoCameraControl = GUILayout.Toggle(enableDemoCameraControl, "テストカメラ制御を有効にする");
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enableDemoCameraControl;
            demoTravel = GUILayout.Toggle(demoTravel, "通過ポイントへ前進する（＋Z方向）");
            if (GUILayout.Button("テストの開始位置に戻る"))
            {
                demoBasePosition = new Vector3(0, 8, -20);
                demoTravel = false;
                ToCloudy();
            }
            GUI.enabled = previousEnabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("曇り")) ToCloudy();
            if (GUILayout.Button("薄曇り")) ToPartlyCloudy();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("夜空・星空")) ToNight();
            if (GUILayout.Button("大気圏付近")) ToUpperAtmosphere();
            GUILayout.EndHorizontal();
            GUILayout.Label("切り替え時間（秒）：" + transitionSeconds.ToString("F1"));
            transitionSeconds = GUILayout.HorizontalSlider(transitionSeconds, 1, 40);
            GUILayout.Label("雲の量：" + cloudCoverage.ToString("F2"));
            float coverage = GUILayout.HorizontalSlider(cloudCoverage, 0, 1);
            if (!Mathf.Approximately(coverage, cloudCoverage)) { progress = 1; cloudCoverage = coverage; }
            GUILayout.Label("時刻（時）：" + Mathf.Repeat(timeOfDay, 24).ToString("F1"));
            float hour = GUILayout.HorizontalSlider(Mathf.Repeat(timeOfDay, 24), 0, 23.99f);
            if (!Mathf.Approximately(hour, Mathf.Repeat(timeOfDay, 24))) { progress = 1; timeOfDay = hour; }
            GUILayout.Label("見た目の高度：" + altitude.ToString("F0") + " m");
            float height = GUILayout.HorizontalSlider(altitude, 0, atmosphereEndHeight);
            if (!Mathf.Approximately(height, altitude))
            {
                progress = 1; altitude = height;
                upperAtmosphere = Mathf.InverseLerp(atmosphereStartHeight, atmosphereEndHeight, altitude);
            }
            if (enableDemoCameraControl && demoCamera)
            {
                GUILayout.Label("視線の上下 ／ 左右");
                Vector3 euler = demoCamera.transform.eulerAngles;
                float pitch = euler.x > 180 ? euler.x - 360 : euler.x;
                pitch = GUILayout.HorizontalSlider(pitch, -85, 85);
                float yaw = GUILayout.HorizontalSlider(euler.y, 0, 360);
                demoCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            if (ownsEnvironment && RenderSettings.skybox == runtimeSky)
            {
                RenderSettings.skybox = previousSky;
                RenderSettings.sun = previousSun;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientSkyColor = previousSkyColor;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.reflectionIntensity = previousReflection;
            }
            ownsEnvironment = false;
            ReleaseDemoCamera();
            if (runtimeSky) Destroy(runtimeSky);
        }
    }
}
