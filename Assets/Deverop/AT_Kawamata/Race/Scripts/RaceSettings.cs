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

    [Header("ゴール後の停止")]
    [Tooltip("ゴールしてから完全に停止するまでの秒数。0なら即停止。ゴール後の走行区間（GoalDistanceFromEnd）を走り切らない長さにする")]
    [SerializeField] private float _goalStopDuration = 2f;
    [Tooltip("停止までの速度の落ち方。横軸＝経過の割合（0＝ゴール、1＝停止）、縦軸＝ゴールした瞬間の速度に対する倍率（1＝そのまま、0＝停止）。横軸が1に達した時点で必ず停止する")]
    [SerializeField] private AnimationCurve _goalStopCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public float StartDistance => _startDistance;

    public float RowSpacing => _rowSpacing;

    public float ColumnSpacing => _columnSpacing;

    public int Columns => Mathf.Max(1, _columns);

    public int PlayerGridIndex => Mathf.Max(0, _playerGridIndex);

    public int CountdownSeconds => Mathf.Max(0, _countdownSeconds);

    public float GoalDistanceFromEnd => Mathf.Max(0f, _goalDistanceFromEnd);

    public float FinishDelayAfterPlayerGoal => Mathf.Max(0f, _finishDelayAfterPlayerGoal);

    public float GoalStopDuration => Mathf.Max(0f, _goalStopDuration);

    // ゴールからの経過時間に対する速度の倍率（1＝そのまま、0＝停止）。GoalStopDurationを過ぎたら必ず0を返す。
    public float EvaluateGoalStopSpeedMultiplier(float timeSinceFinish)
    {
        float duration = GoalStopDuration;
        if (duration <= 0f || timeSinceFinish >= duration)
        {
            return 0f;
        }

        if (_goalStopCurve == null || _goalStopCurve.length == 0)
        {
            return 1f - timeSinceFinish / duration;
        }

        return Mathf.Clamp01(_goalStopCurve.Evaluate(timeSinceFinish / duration));
    }
}
