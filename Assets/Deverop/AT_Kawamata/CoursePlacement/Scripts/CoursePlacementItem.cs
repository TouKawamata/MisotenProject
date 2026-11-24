using UnityEngine;

// 配置ツールが計算した「何を・どこに・どの向きで置くか」の1件。生成はエディタ（CoursePlacementGenerator）が行う。
public readonly struct CoursePlacementItem
{
    public readonly GameObject Prefab;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly float Scale;

    // 看板の矢印の角度（度。看板のローカルXY平面で、0=+X、90=+Y）。矢印が無いものはNaN。
    public readonly float ArrowAngle;

    // まとめて置く子オブジェクトの名前（ギミックの種類ごとなど）。空ならコンテナの直下。
    public readonly string Group;

    public CoursePlacementItem(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, float arrowAngle = float.NaN, string group = "")
    {
        Prefab = prefab;
        Position = position;
        Rotation = rotation;
        Scale = scale;
        ArrowAngle = arrowAngle;
        Group = group ?? "";
    }

    public bool HasArrow => !float.IsNaN(ArrowAngle);
}
