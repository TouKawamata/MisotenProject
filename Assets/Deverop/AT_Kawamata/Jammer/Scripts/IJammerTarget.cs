using System.Collections.Generic;
using UnityEngine;

// レーサー以外で、妨害弾を受けるもの（加速リングなど）の窓口。リング用の弾（RingSwapJammerType）が当たると呼ばれる。
// 例：加速リング（SpeedRing）が実装し、ReceiveJammerで減速リングに切り替わる。
// 実装する側はOnEnable／OnDisableでJammerTargetRegistryに登録・解除する（発射台はそこから標的を探す）。
public interface IJammerTarget
{
    // 弾が狙う位置（リングの中心など）。
    Vector3 JammerAimPosition { get; }

    // 今、妨害弾を受けられるか（すでに減速リングになっている・無効になっているならfalse）。
    bool CanReceiveJammer { get; }

    void ReceiveJammer(JammerTypeProfile type);
}

// シーンにある、妨害弾を受けるもの（IJammerTarget）の一覧。レーサーのプレハブからシーンのリングを参照できないので、リング側が自分で登録する。
public static class JammerTargetRegistry
{
    private static readonly List<IJammerTarget> _targets = new List<IJammerTarget>();

    public static IReadOnlyList<IJammerTarget> Targets => _targets;

    public static void Register(IJammerTarget target)
    {
        if (target != null && !_targets.Contains(target))
        {
            _targets.Add(target);
        }
    }

    public static void Unregister(IJammerTarget target)
    {
        _targets.Remove(target);
    }

    // ドメインリロードを切っていても、前のプレイの登録が残らないようにする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        _targets.Clear();
    }
}
