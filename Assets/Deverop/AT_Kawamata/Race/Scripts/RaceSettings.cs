using UnityEngine;

// レース前に決まっている設定の塊（スタートグリッド・カウントダウン・ゴール位置など）。
// RaceManagerはこの値を読むだけで、レース中に変わる状態はRacerDataが持つ。
[CreateAssetMenu(fileName = "RaceSettings", menuName = "Race/RaceSettings")]
public class RaceSettings : ScriptableObject
{
    [Header("スタートグリッド")]
    [Tooltip("先頭列を置くコース上の距離（m）。後ろの列ほど手前に並ぶため、後列がコース始点より手前にならないよう余裕を持たせる")]
    [SerializeField] private float _startDistance = 30f;
    [Tooltip("列（前後）の間隔（m）")]
    [SerializeField] private float _rowSpacing = 8f;
    [Tooltip("横の間隔（m）。左右の広がりはCourseSplineのTunnelRadiusの内側に収める")]
    [SerializeField] private float _columnSpacing = 5f;
    [Tooltip("1列に並べる人数")]
    [SerializeField] private int _columns = 2;
    [Tooltip("Playerを置くグリッド番号（0始まり。0＝先頭列の左端）")]
    [SerializeField] private int _playerGridIndex = 0;

    [Header("カウントダウン")]
    [Tooltip("GOまでのカウント数（秒）")]
    [SerializeField] private int _countdownSeconds = 3;

    [Header("ゴール")]
    [Tooltip("コース終点からどれだけ手前にゴールを置くか（m）。ゴール後の走行区間になる")]
    [SerializeField] private float _goalDistanceFromEnd = 100f;
    [Tooltip("Playerがゴールしてから、レース終了（RaceFinished）にするまでの秒数")]
    [SerializeField] private float _finishDelayAfterPlayerGoal = 3f;

    public float StartDistance => _startDistance;

    public float RowSpacing => _rowSpacing;

    public float ColumnSpacing => _columnSpacing;

    public int Columns => Mathf.Max(1, _columns);

    public int PlayerGridIndex => Mathf.Max(0, _playerGridIndex);

    public int CountdownSeconds => Mathf.Max(0, _countdownSeconds);

    public float GoalDistanceFromEnd => Mathf.Max(0f, _goalDistanceFromEnd);

    public float FinishDelayAfterPlayerGoal => Mathf.Max(0f, _finishDelayAfterPlayerGoal);
}
