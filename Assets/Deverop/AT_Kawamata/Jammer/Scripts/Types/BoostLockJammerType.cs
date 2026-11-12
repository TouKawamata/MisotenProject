using UnityEngine;

// 妨害弾：当たると、効果の秒数だけブーストができなくなる（ブースト中なら止まる）。
[CreateAssetMenu(fileName = "BoostLockJammer", menuName = "Jammer/Type/ブースト不可")]
public class BoostLockJammerType : JammerTypeProfile
{
    public override void ApplyToRacer(IGimmickRacer racer)
    {
        racer.Effects.LockBoost(this, EffectDuration);
    }
}
