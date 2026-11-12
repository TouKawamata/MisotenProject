using UnityEngine;

// 妨害弾：レーサーではなく加速リングを狙い、当たったリングを減速リングに変える。
// 標的はJammerLauncherのRing Targetsのうち、撃ったレーサーより前にある一番近いもの。
// 当たった標的がIJammerTargetを持っていればReceiveJammerを呼ぶ（減速リングへの切り替えはリング側が行う）。
// 加速リングができるまでは仮の標的（ただのGameObject）を置けるよう、持っていなければログだけ出す。
[CreateAssetMenu(fileName = "RingSwapJammer", menuName = "Jammer/Type/加速リングを減速リングに")]
public class RingSwapJammerType : JammerTypeProfile
{
    [Header("リング")]
    [Tooltip("リングに当たったことにする、リングの中心からの距離（m）")]
    [SerializeField] private float _ringHitRadius = 5f;

    public override bool TargetsRing => true;

    public float RingHitRadius => Mathf.Max(0f, _ringHitRadius);

    public override void ApplyToTarget(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        IJammerTarget jammerTarget = target.GetComponentInParent<IJammerTarget>();
        if (jammerTarget != null)
        {
            jammerTarget.ReceiveJammer(this);
            Debug.Log($"RingSwapJammerType: {target.name} に当たった（減速リングへ切り替え）", target);
            return;
        }

        Debug.Log($"RingSwapJammerType: {target.name} に当たった（IJammerTargetを持っていないので、何も変わらない）", target);
    }
}
