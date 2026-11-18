using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// CPUから見たコースギミックの種類。
public enum ECpuGimmickKind
{
    // 通ると前へ押し出される（加速リング）。通りたい。
    SpeedRing,

    // 減速リング（加速リングが妨害弾で切り替わったもの）。避けたい。
    SlowRing,

    // 触れると爆発する（周りのレーサーをスタンさせて吹き飛ばす）。避けたい・近くで爆発しそうならシールド。
    Bomb,

    // 入っている間、最高速度が下がる。避けたい。
    SlowZone,

    // その他の障害物（Obstacleレイヤー・体を止めるギミック）。避けたい。
    Obstacle,

    // その他（Bonusレイヤーの知らないギミックなど）。気にしない。
    Ignore,
}

// シーンのコースギミック・妨害弾の弾を、全CPUで共有して探す（FindObjectsByTypeは重いので、CPUごと・毎Tickには呼ばない）。
// ・ギミック：一定間隔で探し直す（爆発した爆弾は非アクティブになるので、使うときにactiveInHierarchyを見る）
// ・妨害弾の弾：1フレームに1回だけ探す（すぐ消えるため）
// ギミックとの当たり（避ける・リングをくぐる）の計算もここにまとめる。
// 加速リング（SpeedRing）が減速リングに切り替わったかは公開されていないため、リフレクションで読む（読めなければ加速リング扱い）。
public static class CpuWorldCache
{
    private const float GimmickRescanInterval = 1f;

    private static readonly List<CourseGimmickBase> _gimmicks = new List<CourseGimmickBase>();
    private static readonly List<JammerProjectile> _projectiles = new List<JammerProjectile>();
    private static readonly Dictionary<CourseGimmickBase, Collider[]> _colliders = new Dictionary<CourseGimmickBase, Collider[]>();
    private static float _lastGimmickScanTime = float.NegativeInfinity;
    private static int _projectileScanFrame = -1;
    private static FieldInfo _ringSlowingField;
    private static bool _hasLookedUpRingField;

    // Enter Play Modeでドメインを読み直さない設定でも、前回の再生の結果が残らないようにする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _gimmicks.Clear();
        _projectiles.Clear();
        _colliders.Clear();
        _lastGimmickScanTime = float.NegativeInfinity;
        _projectileScanFrame = -1;
    }

    public static IReadOnlyList<CourseGimmickBase> Gimmicks
    {
        get
        {
            float now = Time.time;
            if (Mathf.Abs(now - _lastGimmickScanTime) >= GimmickRescanInterval)
            {
                _lastGimmickScanTime = now;
                _gimmicks.Clear();
                _colliders.Clear();
                _gimmicks.AddRange(Object.FindObjectsByType<CourseGimmickBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }

            return _gimmicks;
        }
    }

    public static IReadOnlyList<JammerProjectile> Projectiles
    {
        get
        {
            if (_projectileScanFrame != Time.frameCount)
            {
                _projectileScanFrame = Time.frameCount;
                _projectiles.Clear();
                _projectiles.AddRange(Object.FindObjectsByType<JammerProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }

            return _projectiles;
        }
    }

    public static ECpuGimmickKind Classify(CourseGimmickBase gimmick)
    {
        switch (gimmick)
        {
            case SpeedRing ring:
                return IsRingSlowing(ring) ? ECpuGimmickKind.SlowRing : ECpuGimmickKind.SpeedRing;
            case Bomb _:
                return ECpuGimmickKind.Bomb;
            case SlowZone _:
                return ECpuGimmickKind.SlowZone;
        }

        if (gimmick.IsSolid)
        {
            return ECpuGimmickKind.Obstacle;
        }

        int obstacleLayer = LayerMask.NameToLayer(CourseGimmickBase.ObstacleLayerName);
        foreach (Collider gimmickCollider in GetColliders(gimmick))
        {
            if (gimmickCollider.gameObject.layer == obstacleLayer)
            {
                return ECpuGimmickKind.Obstacle;
            }
        }

        return ECpuGimmickKind.Ignore;
    }

    // ギミックの判定に使われるCollider（Obstacle｜Bonusレイヤー）のうち、pointに一番近いものまでの距離（m）。中なら0。
    public static float DistanceTo(CourseGimmickBase gimmick, Vector3 point)
    {
        float best = float.MaxValue;
        foreach (Collider gimmickCollider in GetColliders(gimmick))
        {
            if (gimmickCollider == null || !gimmickCollider.enabled || !gimmickCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            // 凸でないMeshColliderはClosestPointが使えないので、箱で近似する。
            Vector3 closest = gimmickCollider is MeshCollider mesh && !mesh.convex
                ? gimmickCollider.bounds.ClosestPoint(point)
                : gimmickCollider.ClosestPoint(point);
            best = Mathf.Min(best, Vector3.Distance(closest, point));
        }

        return best;
    }

    // pointが、リングの楕円の内側か。scale：楕円の大きさに掛ける倍率（1より小さいと、確実にくぐれる内側だけ）。
    // リングの判定（SpeedRingのBoxColliderの、x・yを半径にした楕円）に合わせる。
    public static bool IsInsideRing(SpeedRing ring, Vector3 point, float scale)
    {
        if (!ring.TryGetComponent(out BoxCollider area))
        {
            return false;
        }

        Vector3 local = ring.transform.InverseTransformPoint(point) - area.center;
        float radiusX = Mathf.Max(area.size.x * 0.5f * scale, 0.0001f);
        float radiusY = Mathf.Max(area.size.y * 0.5f * scale, 0.0001f);
        float x = local.x / radiusX;
        float y = local.y / radiusY;
        return x * x + y * y <= 1f;
    }

    private static bool IsRingSlowing(SpeedRing ring)
    {
        if (!_hasLookedUpRingField)
        {
            _hasLookedUpRingField = true;
            _ringSlowingField = typeof(SpeedRing).GetField("_isSlowing", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_ringSlowingField == null || _ringSlowingField.FieldType != typeof(bool))
            {
                _ringSlowingField = null;
                Debug.LogWarning("CpuWorldCache: SpeedRingの減速リングかどうか（_isSlowing）が読めないため、CPUはすべて加速リングとして扱います");
            }
        }

        return _ringSlowingField != null && (bool)_ringSlowingField.GetValue(ring);
    }

    private static Collider[] GetColliders(CourseGimmickBase gimmick)
    {
        if (_colliders.TryGetValue(gimmick, out Collider[] colliders))
        {
            return colliders;
        }

        List<Collider> found = new List<Collider>();
        foreach (Collider gimmickCollider in gimmick.GetComponentsInChildren<Collider>(true))
        {
            if (((1 << gimmickCollider.gameObject.layer) & CourseGimmickBase.GimmickLayerMask) != 0)
            {
                found.Add(gimmickCollider);
            }
        }

        colliders = found.ToArray();
        _colliders[gimmick] = colliders;
        return colliders;
    }
}
