using System.Collections.Generic;
using UnityEngine;

// コースに沿って物を置くときの計算をまとめたもの（看板・ギミックの配置ツールが使う）。
// 向き・半径はCourseSplineの距離ベースの評価（ベイクしたサンプル）から取る。
public static class CoursePlacementUtility
{
    // コース上のある距離での、位置・向き・トンネル半径。
    public readonly struct CourseFrame
    {
        public readonly float Distance;
        public readonly Vector3 Center;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly float Radius;

        public CourseFrame(float distance, Vector3 center, Vector3 forward, Vector3 right, Vector3 up, float radius)
        {
            Distance = distance;
            Center = center;
            Forward = forward;
            Right = right;
            Up = up;
            Radius = radius;
        }

        public Quaternion Rotation => Quaternion.LookRotation(Forward, Up);

        // 断面の円の方向。0°=右、90°=上、180°=左、270°=下。
        public Vector3 DirectionAround(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return Right * Mathf.Cos(rad) + Up * Mathf.Sin(rad);
        }

        // 中心からangleDegの方向へradiusだけ離れた点。
        public Vector3 PointAround(float angleDeg, float radius)
        {
            return Center + DirectionAround(angleDeg) * radius;
        }
    }

    public static CourseFrame GetFrame(CourseSpline spline, float distance)
    {
        return new CourseFrame(
            distance,
            spline.EvaluatePositionByDistance(distance),
            spline.EvaluateForwardByDistance(distance),
            spline.EvaluateRightByDistance(distance),
            spline.EvaluateUpByDistance(distance),
            spline.GetTunnelRadiusByDistance(distance));
    }

    // 1mあたりに左右へ曲がる角度（度）。＋なら右へ曲がる（インコースは右）、−なら左。
    // distance±windowの進行方向を、distanceでの上方向を軸にした平面へ投影して比べる（上下の曲がりは含めない）。
    public static float GetTurnRate(CourseSpline spline, float distance, float window)
    {
        window = Mathf.Max(0.5f, window);
        float from = ClampOrWrap(spline, distance - window);
        float to = ClampOrWrap(spline, distance + window);

        Vector3 up = spline.EvaluateUpByDistance(distance);
        Vector3 forwardFrom = Vector3.ProjectOnPlane(spline.EvaluateForwardByDistance(from), up);
        Vector3 forwardTo = Vector3.ProjectOnPlane(spline.EvaluateForwardByDistance(to), up);
        if (forwardFrom.sqrMagnitude < 0.0001f || forwardTo.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        float span = spline.IsLoop ? window * 2f : Mathf.Max(0.5f, to - from);
        return Vector3.SignedAngle(forwardFrom, forwardTo, up) / span;
    }

    // 坂の角度（度）。＋は上り、−は下り。
    public static float GetSlope(Vector3 forward)
    {
        return Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // positionが、splinesのどれかのトンネル（半径＋margin）の中に入っているか。
    public static bool IsInsideAnyTunnel(Vector3 position, IReadOnlyList<CourseSpline> splines, float margin)
    {
        for (int i = 0; i < splines.Count; i++)
        {
            CourseSpline spline = splines[i];
            if (spline == null || spline.TotalLength <= 0f)
            {
                continue;
            }

            float nearest = spline.FindNearestDistance(position);
            float radius = spline.GetTunnelRadiusByDistance(nearest) + margin;
            if ((spline.EvaluatePositionByDistance(nearest) - position).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    // 看板の面の上で、worldDirectionが指す向きの角度（度）。看板の+Z方向を見たときに、0=右、90=上、180=左、-90=下。
    public static float GetArrowAngleOnSign(Quaternion signRotation, Vector3 worldDirection)
    {
        Vector3 right = signRotation * Vector3.right;
        Vector3 up = signRotation * Vector3.up;
        float x = Vector3.Dot(worldDirection, right);
        float y = Vector3.Dot(worldDirection, up);
        if (x * x + y * y < 0.000001f)
        {
            return 90f;
        }

        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    public static float ClampOrWrap(CourseSpline spline, float distance)
    {
        float total = spline.TotalLength;
        if (total <= 0f)
        {
            return 0f;
        }

        if (spline.IsLoop)
        {
            return Mathf.Repeat(distance, total);
        }

        return Mathf.Clamp(distance, 0f, total);
    }

    public static float RandomRange(System.Random rng, float min, float max)
    {
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
