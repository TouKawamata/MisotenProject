using UnityEngine;

// 距離テーブルの1要素。距離ベース評価・最近傍検索の両方で使う。
public readonly struct CourseSplineSample
{
    public readonly float Distance;
    public readonly float NormalizedT;
    public readonly Vector3 Position;
    public readonly Vector3 Forward;
    public readonly Vector3 Up;
    public readonly Vector3 Right;

    // トンネル半径の倍率（制御点のRadius Scaleを補間した値）。実際の半径はCourseSplineのTunnel Radiusを掛けて求める。
    public readonly float RadiusScale;

    public CourseSplineSample(float distance, float normalizedT, Vector3 position, Vector3 forward, Vector3 up, Vector3 right, float radiusScale)
    {
        Distance = distance;
        NormalizedT = normalizedT;
        Position = position;
        Forward = forward;
        Up = up;
        Right = right;
        RadiusScale = radiusScale;
    }
}
