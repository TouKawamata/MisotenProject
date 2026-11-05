using UnityEngine;

// ブースト接触の情報。当てた側のBoostHitDetectorが作り、当てられた側のIBoostHitReceiverへ渡す。
public readonly struct BoostHitInfo
{
    public BoostHitInfo(Vector3 pushDirection, float closeness, bool hitShell = false, float attackerForwardSpeed = 0f)
    {
        PushDirection = pushDirection;
        Closeness = Mathf.Clamp01(closeness);
        HitShell = hitShell;
        AttackerForwardSpeed = attackerForwardSpeed;
    }

    // 押し出す向き（当てた側の左右方向のうち、当てられた側がいる方）。
    // 中心どうしがほぼ一致して決まらないときはVector3.zeroで、当てられた側がコースの中央側を選ぶ。
    public Vector3 PushDirection { get; }

    // 当てた側の左右方向で見た、中心どうしの近さ。1＝中心どうしが一致、0＝端をかすめた。
    public float Closeness { get; }

    // 当てられた側のshild_01（ShieldShellHitbox）に当たったか。falseなら体に当たった。
    // シールド中の相手は、shild_01に当たったときだけ防ぐ（体に当たっても効果なし）。
    public bool HitShell { get; }

    // 当てた瞬間の、当てた側の機首の向きの速さ（m/s）。シールドで防いだ側が、自分の速さとの差から押し出す距離を決める。
    public float AttackerForwardSpeed { get; }

    public bool HasPushDirection => PushDirection.sqrMagnitude > 0f;
}
