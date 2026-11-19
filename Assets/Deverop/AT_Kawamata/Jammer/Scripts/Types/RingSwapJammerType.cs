using UnityEngine;

// 妨害弾：レーサーではなく加速リングを狙い、当たったリングを減速リングに変える。
// 標的はシーンの加速リング（JammerTargetRegistryに登録されたIJammerTarget）のうち、撃ったレーサーより前にある一番近いもの。
// 当たるとReceiveJammerを呼ぶ（減速リングへの切り替えはリング側が行う）。
[CreateAssetMenu(fileName = "RingSwapJammer", menuName = "Jammer/Type/加速リングを減速リングに")]
public class RingSwapJammerType : JammerTypeProfile
{
    [Header("リング")]
    [Tooltip("リングに当たったことにする、リングの中心からの距離（m）")]
    [SerializeField] private float _ringHitRadius = 5f;

    public override bool TargetsRing => true;

    public float RingHitRadius => Mathf.Max(0f, _ringHitRadius);

    public override void ApplyToTarget(IJammerTarget target)
    {
        if (target == null || !target.CanReceiveJammer)
        {
            return;
        }

        target.ReceiveJammer(this);
        Object context = target as Object;
        Debug.Log($"RingSwapJammerType: {(context != null ? context.name : "リング")} に当たった（減速リングへ切り替え）", context);
    }
}
