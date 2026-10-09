using UnityEngine;

/// <summary>
/// CourseRing を CourseSpline 上の指定位置に自動配置するための補助ツール。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CourseRing))]
public class CourseRingSplinePlacer : MonoBehaviour
{
    [Header("コース参照")]
    [Tooltip("配置基準となる CourseSpline")]
    [SerializeField] private CourseSpline _courseSpline;

    [Header("配置設定")]
    [Tooltip("コースのスタートからの距離（m）")]
    [SerializeField] private float _distance = 0f;

    [Tooltip("コース中心からの左右のズレ（+で右、-で左）")]
    [SerializeField] private float _lateralOffset = 0f;

    [Tooltip("コース中心からの上下のズレ（+で上、-で下）")]
    [SerializeField] private float _verticalOffset = 0f;

    [Header("サイズ設定")]
    [Tooltip("チェックを入れると、CourseSplineのTunnelRadiusに自動的にサイズを合わせます（違うサイズにしたい場合はチェックを外してCourseRingのRadiusを直接変更してください）")]
    [SerializeField] private bool _syncWithTunnelRadius = true;

    private CourseRing _ring;

    private void Awake()
    {
        _ring = GetComponent<CourseRing>();
    }

    private void Update()
    {
        if (_courseSpline == null)
        {
            return;
        }

        // --- 位置と向きの計算 ---
        Vector3 center  = _courseSpline.EvaluatePositionByDistance(_distance);
        Vector3 right   = _courseSpline.EvaluateRightByDistance(_distance);
        Vector3 up      = _courseSpline.EvaluateUpByDistance(_distance);
        Vector3 forward = _courseSpline.EvaluateForwardByDistance(_distance);

        // オフセットを加えて実際の位置を決定
        transform.position = center + right * _lateralOffset + up * _verticalOffset;
        
        // 前方(forward)と上(up)を元に向きを決定
        if (forward != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        // --- サイズの自動同期 ---
        if (_syncWithTunnelRadius && _ring != null)
        {
            // CourseRingのプロパティを更新（内部で勝手にScaleが調整されます）
            _ring.Radius = _courseSpline.TunnelRadius;
        }
    }

    // Inspectorで値を変更した時にも即座に反映するための処理
    private void OnValidate()
    {
        if (_courseSpline != null)
        {
            // 距離を0以上に制限（ループコースならTotalLengthでループさせるのもアリですが、一旦0以上のみ）
            _distance = Mathf.Max(0f, _distance);
        }
    }
}
