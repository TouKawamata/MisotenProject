using UnityEngine;

// 妨害弾：当たると、効果の秒数だけ最高速度が下がる。
[CreateAssetMenu(fileName = "SlowJammer", menuName = "Jammer/Type/減速")]
public class SlowJammerType : JammerTypeProfile
{
    [Header("減速")]
    [Tooltip("最高速度の倍率（0.6＝60%の速さ）")]
    [Range(0f, 1f)]
    [SerializeField] private float _speedMultiplier = 0.6f;
    [Tooltip("倍率を切り替える秒数。効果が切れるときも同じ秒数で戻す")]
    [SerializeField] private float _fadeDuration = 0.2f;

    public override void ApplyToRacer(IGimmickRacer racer)
    {
        racer.Effects.SetMaxSpeedMultiplier(this, _speedMultiplier, EffectDuration, Mathf.Max(0f, _fadeDuration));
    }
}
