using UnityEngine;

// Catmull-Romの数式計算のみを担当する。GameObject/Transformには一切依存しない。
public static class CourseSplineEvaluator
{
    public static Vector3 EvaluatePosition(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // 位置の解析的な微分（接線）。正規化はしていないため、Forwardが必要な場合は呼び出し側で正規化する。
    public static Vector3 EvaluateTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t * t
        );
    }
}
