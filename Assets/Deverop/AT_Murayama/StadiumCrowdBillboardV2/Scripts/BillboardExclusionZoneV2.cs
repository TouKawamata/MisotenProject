using UnityEngine;

namespace AT_Murayama.StadiumCrowdBillboardV2
{
    /// <summary>
    /// 【V2・ビルボード版専用（完全独立）】任意の立体領域から観客を除外するためのボリューム。
    /// 既存の StadiumCrowd / StadiumCrowdBillboard / StadiumCrowdV2 とは完全に独立しています。
    /// </summary>
    [ExecuteAlways]
    public class BillboardExclusionZoneV2 : MonoBehaviour
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

        [Tooltip("適用する階層インデックス（-1 = 全階層, 0 = 1階席のみ, 1 = 2階席のみ...）")]
        public int targetTierIndex = -1;

        [Tooltip("Sceneビューで除外範囲のギズモを表示するか")]
        public bool showGizmo = true;

        public bool AppliesToTier(int tierIndex)
        {
            return targetTierIndex < 0 || targetTierIndex == tierIndex;
        }

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
            Gizmos.color = new Color(1.0f, 0.25f, 0.2f, 0.16f);

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
