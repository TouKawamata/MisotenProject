using UnityEngine;

// 妨害弾：当たると、効果の秒数だけ視界が反転する。Playerは画面が反転し（ViewFlipPresentation）、CPUは同じ軸の移動の入力が反転する。
[CreateAssetMenu(fileName = "ViewFlipJammer", menuName = "Jammer/Type/視界反転")]
public class ViewFlipJammerType : JammerTypeProfile
{
    [Header("視界反転")]
    [Tooltip("反転する向き（左右・上下。両方選ぶと180度回したように見える）")]
    [SerializeField] private EViewFlip _flip = EViewFlip.Horizontal;

    public EViewFlip Flip => _flip;

    public override void ApplyToRacer(IGimmickRacer racer)
    {
        racer.Effects.SetViewFlip(this, _flip, EffectDuration);
    }
}
