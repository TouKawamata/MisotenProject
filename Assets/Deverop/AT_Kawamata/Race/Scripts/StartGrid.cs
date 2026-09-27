using UnityEngine;

// スタートグリッドの位置・向きの計算だけを担当する。
// startDistanceを先頭列とし、後ろの列ほど手前、横はコース中央を基準に左右へ等間隔で並べる。
public class StartGrid
{
    private readonly CourseSpline _spline;
    private readonly RaceSettings _settings;

    public StartGrid(CourseSpline spline, RaceSettings settings)
    {
        _spline = spline;
        _settings = settings;
    }

    public void GetPose(int gridIndex, out Vector3 position, out Quaternion rotation)
    {
        int columns = _settings.Columns;
        int row = gridIndex / columns;
        int col = gridIndex % columns;

        float distance = _settings.StartDistance - row * _settings.RowSpacing;
        if (distance < 0f)
        {
            Debug.LogWarning($"StartGrid: グリッド{gridIndex}がコース始点より手前になるため、始点に置きます。RaceSettingsのStartDistanceを大きくしてください");
            distance = 0f;
        }

        float lateral = (col - (columns - 1) * 0.5f) * _settings.ColumnSpacing;
        if (_spline.TunnelRadius > 0f && Mathf.Abs(lateral) > _spline.TunnelRadius)
        {
            Debug.LogWarning($"StartGrid: グリッド{gridIndex}の横位置（{lateral:F1}m）がTunnelRadius（{_spline.TunnelRadius:F1}m）の外です。ColumnSpacing／Columnsを見直してください");
        }

        Vector3 up = _spline.EvaluateUpByDistance(distance);
        position = _spline.EvaluatePositionByDistance(distance) + _spline.EvaluateRightByDistance(distance) * lateral;
        rotation = Quaternion.LookRotation(_spline.EvaluateForwardByDistance(distance), up);
    }
}
