using UnityEngine;

namespace AT_Murayama.StadiumCrowd
{
    /// <summary>
    /// 観客席の途中を途切れさせる（ゲート・通路・柱・着地エリア・コース貫通部などを空ける）ための除外ボリューム。
    /// StadiumCrowdGenerator の子オブジェクト（または登録リスト）として配置し、
    /// Sceneビューで移動・回転・スケール変更するだけで、範囲内の観客を自動的に除外します。
    /// </summary>
    [ExecuteAlways]
    public class CrowdExclusionZone : MonoBehaviour
    {
        public enum ZoneShape
        {
            [InspectorName("ボックス (Box / 直方体)")]
            Box,

            [InspectorName("シリンダー・円柱 (Cylinder / 円形抜き)")]
            Cylinder,

            [InspectorName("スフィア・球体 (Sphere / 球状抜き)")]
            Sphere
        }

        [Tooltip("除外判定を有効にするか")]
        public bool isActive = true;

        [Tooltip("除外エリアの形状")]
        public ZoneShape shape = ZoneShape.Box;

        [Tooltip("ローカルサイズ（TransformのScaleと掛け合わされます）")]
        public Vector3 size = new Vector3(12f, 15f, 12f);

        [Tooltip("Sceneビューで除外範囲のギズモを表示するか")]
        public bool showGizmo = true;

        /// <summary>
        /// 指定したワールド座標が除外ゾーン内部に含まれているか判定します（0 GC Alloc）
        /// </summary>
        public bool ContainsWorldPoint(Vector3 worldPos)
        {
            if (!isActive || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            Vector3 half = size * 0.5f;

            switch (shape)
            {
                case ZoneShape.Box:
                    return Mathf.Abs(localPos.x) <= half.x &&
                           Mathf.Abs(localPos.y) <= half.y &&
                           Mathf.Abs(localPos.z) <= half.z;

                case ZoneShape.Cylinder:
                    if (Mathf.Abs(localPos.y) > half.y) return false;
                    float rx = half.x > 0.001f ? localPos.x / half.x : 0f;
                    float rz = half.z > 0.001f ? localPos.z / half.z : 0f;
                    return (rx * rx + rz * rz) <= 1f;

                case ZoneShape.Sphere:
                    float sx = half.x > 0.001f ? localPos.x / half.x : 0f;
                    float sy = half.y > 0.001f ? localPos.y / half.y : 0f;
                    float sz = half.z > 0.001f ? localPos.z / half.z : 0f;
                    return (sx * sx + sy * sy + sz * sz) <= 1f;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmo || !isActive) return;

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Color prevColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1.0f, 0.25f, 0.2f, 0.18f);

            if (shape == ZoneShape.Box)
            {
                Gizmos.DrawCube(Vector3.zero, size);
                Gizmos.color = new Color(1.0f, 0.35f, 0.2f, 0.85f);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            else
            {
                Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.Scale(size * 0.5f);
                Gizmos.DrawSphere(Vector3.zero, 1f);
                Gizmos.color = new Color(1.0f, 0.35f, 0.2f, 0.85f);
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
            }

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;
        }
#endif
    }
}

