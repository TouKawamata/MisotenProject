using UnityEngine;

// Splineの制御点マーカー。将来Points配下にSegment等の非制御点オブジェクトが混在しても
// 「どれが制御点か」をこのコンポーネントの有無で判別できるようにしておく。
public class CourseSplinePoint : MonoBehaviour
{
    public Vector3 Position => transform.position;
}
