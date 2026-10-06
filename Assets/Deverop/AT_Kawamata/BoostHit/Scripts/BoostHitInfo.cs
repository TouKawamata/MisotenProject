using UnityEngine;

// ブースト接触の情報。当てた側のBoostHitDetectorが作り、当てられた側のIBoostHitReceiverへ渡す。
public readonly struct BoostHitInfo
{
    public BoostHitInfo(Vector3 pushDirection, float closeness)
    {
        PushDirection = pushDirection;
        Closeness = Mathf.Clamp01(closeness);
    }

    // 押し出す向き（当てた側の左右方向のうち、当てられた側がいる方）。
    // 中心どうしがほぼ一致して決まらないときはVector3.zeroで、当てられた側がコースの中央側を選ぶ。
    public Vector3 PushDirection { get; }

    // 当てた側の左右方向で見た、中心どうしの近さ。1＝中心どうしが一致、0＝端をかすめた。
    public float Closeness { get; }

    public bool HasPushDirection => PushDirection.sqrMagnitude > 0f;
}
