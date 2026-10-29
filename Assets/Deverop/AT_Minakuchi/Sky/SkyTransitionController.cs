using UnityEngine;
using UnityEngine.Rendering;

namespace Minakuchi.Sky
{
    [DisallowMultipleComponent]
    public sealed class SkyTransitionController : MonoBehaviour
    {
        public static SkyTransitionController Active { get; private set; }
        [Header("空の設定")]
        [SerializeField] private Material skyTemplate;
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        public bool atmosphereEnabled;
        public bool enableSunsetTint = true;
        public Color sunsetColor = new Color(1f, 0.28f, 0.055f, 1f);
        [Range(0, 1)] public float sunsetIntensity = 0.85f;
        [Range(0.05f, 1)] public float sunsetSpread = 0.85f;
        [Range(18.01f, 23)] public float sunsetEndHour = 19.25f;
        [Range(16, 23)] public float nightStartHour = 17;
        [Range(18.1f, 24)] public float nightCompleteHour = 21.5f;
        [Min(0.1f)] public float transitionSeconds = 12;
        [Range(0, 1)] public float cloudCoverage = 1f;
        [Range(0, 1)] public float night;
        [Header("時刻と天体（12=正午、0=真夜中）")]
        [Range(0, 24)] public float timeOfDay = 10;
        [Range(0, 1)] public float upperAtmosphere;
        public float cloudHeight = -400;
        [Min(1)] public float cloudScale = 1200;
        [Min(1)] public float cloudThickness = 400;
        [Range(0.1f, 5)] public float cloudDensity = 2f;
        public bool useMeshClouds;
        [Range(16, 64)] public int cloudSteps = 40;
        [Range(0, 3)] public float cloudShadowStrength = 1.3f;
        [Range(0, 1)] public float cloudTopContrast = 1f;
        [UnityEngine.Serialization.FormerlySerializedAs("cloudTopWarmth")]
        [Range(0, 1)] public float cloudTopBlueTint = 0.65f;
        [Range(0, 1)] public float cloudMorningShadow = 0.35f;
        [Range(0, 2)] public float cloudPassageStrength = 1f;
        [SerializeField] private Shader cloudMeshShader;
        private MeshCloudLayer meshClouds;
        public Vector2 wind = new Vector2(0.015f, 0.008f);
        public bool enableCloudFlow = true;
        [Min(0)] public float cloudFlowSpeed = 4;
        [Header("SkyTestScene用（他のシーンではOFF）")]
        public bool showDemoControls;

        private Material runtimeSky, previousSky;
        private Light previousSun;
        private AmbientMode previousAmbientMode;
        private Color previousSkyColor, previousEquator, previousGround;
        private float previousReflection;
        private float clock;
        private Vector2 cloudOffset;
        private float cloudHeightFrom, cloudHeightTarget;
        private Vector3 from, target;
        private float progress = 1;
        private bool ownsEnvironment;
        private bool previousAtmosphereEnabled;
        private bool transitionControlsTime;
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
            Active = this;
            if (useMeshClouds)
            {
                var shader = cloudMeshShader ? cloudMeshShader : Shader.Find("Minakuchi/MeshCloud");
                if (shader) meshClouds = MeshCloudLayer.Create(shader);
            }
            RenderSettings.skybox = runtimeSky;
            ownsEnvironment = true;
            upperAtmosphere = atmosphereEnabled ? 1 : 0;
            previousAtmosphereEnabled = atmosphereEnabled;
            target = new Vector3(cloudCoverage, timeOfDay, upperAtmosphere);
            Apply();
        }

        /// <summary>Switch the atmosphere and cloud amount without selecting a day/night preset.</summary>
        public void TransitionTo(bool atmosphere, float clouds, float seconds,
            float? destinationHour = null, float? destinationCloudHeight = null)
        {
            atmosphereEnabled = atmosphere;
            previousAtmosphereEnabled = atmosphere;
            transitionSeconds = Mathf.Max(0.1f, seconds);
            transitionControlsTime = destinationHour.HasValue;
            from = new Vector3(cloudCoverage, timeOfDay, upperAtmosphere);
            float hour = destinationHour.HasValue
                ? timeOfDay + Mathf.Repeat(destinationHour.Value - timeOfDay, 24) : timeOfDay;
            target = new Vector3(Mathf.Clamp01(clouds), hour, atmosphere ? 1 : 0);
            cloudHeightFrom = cloudHeight;
            cloudHeightTarget = destinationCloudHeight ?? cloudHeight;
            progress = 0;
        }
        private void SetValues(Vector3 v)
        {
            cloudCoverage = v.x;
            if (transitionControlsTime) timeOfDay = v.y;
            upperAtmosphere = v.z;
        }

        private void Update()
        {
            if (atmosphereEnabled != previousAtmosphereEnabled)
                TransitionTo(atmosphereEnabled, cloudCoverage, transitionSeconds);
            clock += Time.deltaTime;
            if (enableCloudFlow) cloudOffset += wind * (cloudFlowSpeed * Time.deltaTime);
            if (progress < 1)
            {
                progress = Mathf.Min(1, progress + Time.deltaTime / Mathf.Max(0.1f, transitionSeconds));
                SetValues(Vector3.Lerp(from, target, Mathf.SmoothStep(0, 1, progress)));
                cloudHeight = Mathf.Lerp(cloudHeightFrom, cloudHeightTarget, Mathf.SmoothStep(0, 1, progress));
            }
            if (progress >= 1) timeOfDay = Mathf.Repeat(timeOfDay, 24);
            Apply();
        }

        private void Apply()
        {
            if (!runtimeSky) return;
            float space = upperAtmosphere;
            float angle = (timeOfDay - 6) * 15 * Mathf.Deg2Rad;
            Vector3 sunDirection = Quaternion.Euler(0, 25, 0) * new Vector3(0, Mathf.Sin(angle), Mathf.Cos(angle));
            Vector3 moonDirection = -sunDirection;
            float hour = Mathf.Repeat(timeOfDay, 24);
            float sunsetEnd = Mathf.Max(18.01f, sunsetEndHour);
            float nightStart = Mathf.Clamp(nightStartHour, 16, 23);
            float nightEnd = Mathf.Max(nightStart + 0.1f, nightCompleteHour);
            night = hour >= 12
                ? Mathf.InverseLerp(nightStart, nightEnd, hour)
                : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.5f, 6.5f, hour));
            float twilight = hour >= 12
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17, sunsetEnd, hour))
                : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.5f, 7, hour));
            runtimeSky.SetFloat("_Night", night);
            runtimeSky.SetFloat("_Twilight", twilight);
            float sunset = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16, 18, hour))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, sunsetEnd, hour)));
            runtimeSky.SetFloat("_SunsetAmount", enableSunsetTint ? sunset : 0);
            runtimeSky.SetColor("_SunsetColor", sunsetColor);
            runtimeSky.SetFloat("_SunsetIntensity", sunsetIntensity);
            float sunsetDescent = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, sunsetEnd, hour));
            runtimeSky.SetFloat("_SunsetSpread", sunsetSpread * Mathf.Lerp(1, 0.025f, sunsetDescent));
            runtimeSky.SetFloat("_Space", space);
            // The renderer feature composites volume clouds against scene depth.
            runtimeSky.SetFloat("_Clouds", 0);
            runtimeSky.SetFloat("_CloudHeight", cloudHeight);
            runtimeSky.SetFloat("_CloudScale", cloudScale);
            runtimeSky.SetFloat("_CloudThickness", Mathf.Max(1, cloudThickness));
            runtimeSky.SetFloat("_CloudDensity", cloudDensity);
            runtimeSky.SetFloat("_Clock", clock);
            runtimeSky.SetVector("_Wind", new Vector4(wind.x, wind.y, 0, 0));
            runtimeSky.SetVector("_CloudOffset", new Vector4(cloudOffset.x, cloudOffset.y, 0, 0));
            runtimeSky.SetVector("_SunDir", sunDirection);
            runtimeSky.SetVector("_MoonDir", moonDirection);
            if (meshClouds)
                meshClouds.Apply(cloudHeight, cloudThickness, cloudScale, cloudCoverage, cloudDensity,
                    cloudOffset, sunDirection, night, twilight, enableSunsetTint ? sunset * sunsetIntensity : 0, sunsetColor);
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
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.4f, 0.55f, 0.75f), new Color(0.025f, 0.04f, 0.09f), Mathf.Max(night, space * 0.8f)) * Mathf.Lerp(1, 0.55f, twilight * (1-night));
            RenderSettings.ambientEquatorColor = RenderSettings.ambientSkyColor * 0.65f;
            RenderSettings.ambientGroundColor = RenderSettings.ambientSkyColor * 0.3f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(1, 0.15f, Mathf.Max(night, space));
        }
        private void OnGUI()
        {
            if (!showDemoControls || !runtimeSky) return;
            GUILayout.BeginArea(new Rect(16, 16, 340, 330), GUI.skin.box);
            GUILayout.Label("空の調整");
            bool atmosphere = GUILayout.Toggle(atmosphereEnabled, "大気圏表示を有効にする");
            if (atmosphere != atmosphereEnabled)
                TransitionTo(atmosphere, cloudCoverage, transitionSeconds);
            GUILayout.Label("切り替え時間（秒）：" + transitionSeconds.ToString("F1"));
            transitionSeconds = GUILayout.HorizontalSlider(transitionSeconds, 1, 40);
            GUILayout.Label("雲の量：" + cloudCoverage.ToString("F2"));
            float coverage = GUILayout.HorizontalSlider(cloudCoverage, 0, 1);
            if (!Mathf.Approximately(coverage, cloudCoverage)) { progress = 1; cloudCoverage = coverage; }
            GUILayout.Label("時刻（時）：" + Mathf.Repeat(timeOfDay, 24).ToString("F1"));
            float hour = GUILayout.HorizontalSlider(Mathf.Repeat(timeOfDay, 24), 0, 23.99f);
            if (!Mathf.Approximately(hour, Mathf.Repeat(timeOfDay, 24))) { progress = 1; timeOfDay = hour; }
            GUILayout.EndArea();
        }

        public bool PrepareCloudVolume(Material material)
        {
            if (!isActiveAndEnabled || !runtimeSky || meshClouds || cloudCoverage <= 0) return false;
            material.CopyPropertiesFromMaterial(runtimeSky);
            material.SetFloat("_Clouds", cloudCoverage);
            material.SetInt("_CloudSteps", Mathf.Clamp(cloudSteps, 16, 64));
            material.SetFloat("_CloudShadowStrength", cloudShadowStrength);
            material.SetFloat("_CloudTopContrast", cloudTopContrast);
            material.SetFloat("_CloudTopBlueTint", cloudTopBlueTint);
            material.SetFloat("_CloudMorningShadow", cloudMorningShadow);
            material.SetFloat("_CloudHour", Mathf.Repeat(timeOfDay, 24));
            material.SetFloat("_CloudPassageStrength", cloudPassageStrength);
            return true;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            if (meshClouds)
            {
                meshClouds.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(meshClouds.gameObject);
                else DestroyImmediate(meshClouds.gameObject);
                meshClouds = null;
            }
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
            if (runtimeSky)
            {
                if (Application.isPlaying) Destroy(runtimeSky);
                else DestroyImmediate(runtimeSky);
                runtimeSky = null;
            }
        }
    }
}
