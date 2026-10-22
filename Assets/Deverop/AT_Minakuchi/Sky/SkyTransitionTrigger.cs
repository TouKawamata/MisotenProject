using System.Collections.Generic;
using UnityEngine;

namespace Minakuchi.Sky
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SkyTransitionTrigger : MonoBehaviour
    {
        [Tooltip("未設定なら有効なSkyTransitionControllerをシーン内から検索")]
        [InspectorName("制御する空（省略可）")] public SkyTransitionController sky;
        [Tooltip("ONで大気圏付近の空、OFFで通常の空にします。時刻は独立しています。")]
        public bool atmosphereEnabled;
        [Range(0, 1), Tooltip("通過後の雲の量。0は雲なし、1は雲が多い状態。")]
        public float destinationCloudCoverage = 0.72f;
        [Tooltip("このポイントを通過してから切り替えが完了するまでの秒数")]
        [InspectorName("切り替え時間（秒）"), Min(0.1f)] public float transitionSeconds = 8;
        [Tooltip("ONなら指定時刻へ移行。OFFなら現在の時刻を維持します。自動時間経過はありません。")]
        [InspectorName("到達時刻を指定")] public bool overrideTimeOfDay;
        [InspectorName("到達時刻（0〜24時）"), Range(0, 24)] public float destinationHour = 22;
        [Tooltip("ONならこのポイントの切り替え時間で雲を指定した高さへ移動。OFFなら雲の高さを維持します。")]
        public bool overrideCloudHeight;
        [Tooltip("雲の層の移動先の高さ。現在値より大きいと上へ、小さいと下へ移動。負の値も指定できます。")]
        public float destinationCloudHeight = 1200;
        [Tooltip("空欄なら全てのCollider。既定値はPlayerタグのみ")]
        [InspectorName("通過対象のタグ（空欄＝全て）")] public string requiredTag = "Player";
        [Tooltip("指定した場合はタグの代わりにこのオブジェクトと子Colliderだけを判定")]
        [InspectorName("通過対象オブジェクト（省略可）")] public Transform targetActor;
        [InspectorName("一度だけ発動")] public bool triggerOnce = true;
        private bool fired;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        private void OnValidate() => GetComponent<BoxCollider>().isTrigger = true;
        private void OnDisable() { occupants.Clear(); fired = false; }

        private void OnTriggerEnter(Collider other)
        {
            Transform actor = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
            if (!sky) sky = FindFirstObjectByType<SkyTransitionController>();
            if (!sky) { Debug.LogWarning("制御する空が見つかりません。通過ポイントに空の制御を設定してください。", this); return; }
            if (targetActor)
            {
                if (!other.transform.IsChildOf(targetActor) && !actor.IsChildOf(targetActor)) return;
            }
            else if (!string.IsNullOrEmpty(requiredTag) && actor.tag != requiredTag && actor.root.tag != requiredTag) return;
            if (fired && triggerOnce) return;
            bool alreadyInside = occupants.Count > 0;
            occupants.Add(other);
            if (alreadyInside) return;
            sky.TransitionTo(atmosphereEnabled, destinationCloudCoverage, transitionSeconds,
                overrideTimeOfDay ? (float?)destinationHour : null,
                overrideCloudHeight ? (float?)destinationCloudHeight : null);
            fired = true;
        }

        private void OnTriggerExit(Collider other) => occupants.Remove(other);

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = atmosphereEnabled
                ? new Color(0.4f, 0.25f, 1, 0.2f) : new Color(0, 0.8f, 1, 0.2f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
