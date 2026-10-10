using UnityEngine;

// Splineの制御点マーカー。将来Points配下にSegment等の非制御点オブジェクトが混在しても
// 「どれが制御点か」をこのコンポーネントの有無で判別できるようにしておく。
// 向きはTransformのRotationではなく、ここのBank Angleで指定する（Rotationは使わない）。
public class CourseSplinePoint : MonoBehaviour
{
    [Tooltip("コースのバンク（度）。進行方向を軸にした回転で、TransformのRotation Zと同じ向き（＋で左に傾く）。" +
             "点と点の間は角度をそのまま補間する（0→270なら4分の3回転）。180°以上・360°超も指定できる")]
    [SerializeField] private float bankAngle = 0f;

    [Tooltip("トンネル半径の倍率（1＝CourseSplineのTunnel Radiusそのまま）。点と点の間はなめらかに補間する")]
    [SerializeField, Min(0.01f)] private float radiusScale = 1f;

    public Vector3 Position => transform.position;

    // エディタの「RotationをBank Angleへ変換」から書き換えるためにsetterを持たせる。
    public float BankAngle
    {
        get => bankAngle;
        set => bankAngle = value;
    }

    public float RadiusScale => radiusScale;
}
