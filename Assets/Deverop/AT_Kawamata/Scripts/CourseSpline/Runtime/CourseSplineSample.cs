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

    public CourseSplineSample(float distance, float normalizedT, Vector3 position, Vector3 forward, Vector3 up, Vector3 right)
    {
        Distance = distance;
        NormalizedT = normalizedT;
        Position = position;
        Forward = forward;
        Up = up;
        Right = right;
    }
}
