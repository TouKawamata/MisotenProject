using UnityEngine;

// 妨害弾：当たると、効果の秒数だけブーストができなくなる（ブースト中なら止まる）。
[CreateAssetMenu(fileName = "BoostLockJammer", menuName = "Jammer/Type/ブースト不可")]
public class BoostLockJammerType : JammerTypeProfile
{
    [Header("ブースト不可")]
    [Tooltip("ON：ブースト不可の間、ブーストのゲージも回復しない")]
    [SerializeField] private bool _stopsBoostRecovery = false;

    public override void ApplyToRacer(IGimmickRacer racer)
    {
        racer.Effects.LockBoost(this, EffectDuration, _stopsBoostRecovery);
    }
}
