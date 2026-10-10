using UnityEngine;

// Player/Camera側がCourseSplineの実装に直接依存しないための抽象化。
public interface ICourseGuide
{
    Vector3 GetCenterPosition(Vector3 worldPosition);

    Vector3 GetForward(Vector3 worldPosition);

    Vector3 GetRight(Vector3 worldPosition);

    Vector3 GetUp(Vector3 worldPosition);

    Vector3 GetLookAheadPosition(Vector3 worldPosition, float lookAheadDistance);

    // その場所のトンネル半径（制御点のRadius Scaleを反映した値）。
    float GetTunnelRadius(Vector3 worldPosition);
}
