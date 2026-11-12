using UnityEngine;

// 妨害弾：当たると、効果の秒数だけ上下左右の移動が遅くなる（速さ・加速度の両方）。
[CreateAssetMenu(fileName = "LateralSlowJammer", menuName = "Jammer/Type/左右移動が遅い")]
public class LateralSlowJammerType : JammerTypeProfile
{
    [Header("左右移動")]
    [Tooltip("上下左右の移動の速さ・加速度の倍率（0.4＝40%）")]
    [Range(0f, 1f)]
    [SerializeField] private float _lateralMultiplier = 0.4f;
    [Tooltip("倍率を切り替える秒数。効果が切れるときも同じ秒数で戻す")]
    [SerializeField] private float _fadeDuration = 0.1f;

    public override void ApplyToRacer(IGimmickRacer racer)
    {
        racer.Effects.SetLateralMultiplier(this, _lateralMultiplier, EffectDuration, Mathf.Max(0f, _fadeDuration));
    }
}
