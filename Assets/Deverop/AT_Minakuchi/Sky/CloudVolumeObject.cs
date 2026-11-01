using System.Collections.Generic;
using UnityEngine;

namespace Minakuchi.Sky
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Minakuchi/Sky/Cloud Volume Object")]
    public sealed class CloudVolumeObject : MonoBehaviour
    {
        internal static readonly List<CloudVolumeObject> Instances = new List<CloudVolumeObject>();
        [Tooltip("雲を描く範囲。Transformで位置・回転・スケールを変更できます。")]
        public Vector3 size = new Vector3(18000, 400, 18000);
        [Range(0, 1)] public float coverage = 1;
        [Min(1)] public float patternScale = 1200;
        [Range(0.1f, 5)] public float density = 2;
        [Range(16, 64)] public int steps = 40;
        [Range(0, 3)] public float shadowStrength = 1.3f;
        [Range(0, 2)] public float passageStrength = 1;
        public Vector2 wind = new Vector2(0.06f, 0.032f);
        private Vector2 offset;

        private void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
        }

        private void OnDisable() => Instances.Remove(this);
        private void Update()
        {
            if (Application.isPlaying) offset += wind * Time.deltaTime;
        }

        public Vector3 Size => new Vector3(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));

        public void Apply(Material material)
        {
            // Read the sky's lighting into this material; never change the original cloud's settings.
            var sky = SkyTransitionController.Active;
            if (!sky || !sky.PrepareCloudVolume(material))
            {
                var sun = RenderSettings.sun;
                material.SetVector("_SunDir", sun ? -sun.transform.forward : Vector3.up);
                material.SetFloat("_Night", sky ? sky.night : 0);
                material.SetFloat("_Twilight", 0);
                material.SetFloat("_SunsetAmount", 0);
            }
            material.SetFloat("_Clouds", coverage);
            material.SetFloat("_CloudScale", Mathf.Max(1, patternScale));
            material.SetFloat("_CloudDensity", density);
            material.SetInt("_CloudSteps", Mathf.Clamp(steps, 16, 64));
            material.SetFloat("_CloudShadowStrength", shadowStrength);
            material.SetFloat("_CloudPassageStrength", passageStrength);
            material.SetVector("_CloudOffset", new Vector4(offset.x, offset.y, 0, 0));
            material.SetMatrix("_CloudWorldToLocal", transform.worldToLocalMatrix);
            material.SetVector("_CloudHalfSize", Size * 0.5f);
            material.SetFloat("_CloudHeight", 0);
            material.SetFloat("_CloudThickness", Size.y);
            material.SetFloat("_CloudWorldThickness", transform.TransformVector(Vector3.up * Size.y).magnitude);
        }

        private void OnDrawGizmosSelected()
        {
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.7f, 0.85f, 1, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, Size);
            Gizmos.matrix = previous;
        }
    }
}
