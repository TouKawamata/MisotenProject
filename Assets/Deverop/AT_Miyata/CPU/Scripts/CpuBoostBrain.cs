using UnityEngine;

// ブーストを押し続けたいかを決める。考え直すTickだけ判断し、それ以外のTickは前の判断を続ける。
//   攻撃：狙う相手が近く、横位置がそろっていて、ゲージが攻撃の閾値以上なら始める。相手がシールドを張ったら、やめる（防がれると損なため）。
//   加速：先のコースが曲がっておらず、ゲージが加速の閾値以上なら始める。ゲージが下限まで減るか、先が曲がっていたらやめる。
// 追い上げ補正（catchUp。＋＝Playerより後ろ）で、加速の閾値を上げ下げする（速度は変えない）。
// 順位が下ほど（RankRatioが大きいほど）ゲージの減りが遅い（始めた瞬間の順位で決まる）ので、加速の閾値を下げて早めに使う。
public class CpuBoostBrain
{
    // 攻撃のブーストを始める、狙う相手との前後の距離（m）。
    private const float AttackBoostRange = 25f;

    // 攻撃を始めるゲージの閾値。攻撃性0～1で、高い～低い。
    private const float AttackGaugeAtLowAggression = 0.6f;
    private const float AttackGaugeAtHighAggression = 0.25f;

    // 加速を始めるゲージの閾値。ブーストの使い方0～1で、高い（ため込む）～低い（すぐ使う）。
    private const float CruiseGaugeAtLowEagerness = 0.95f;
    private const float CruiseGaugeAtHighEagerness = 0.45f;

    // 加速をやめるゲージ。ブーストの使い方0～1で、高い（攻撃用に残す）～0（使い切る）。難易度のBoostReserveより下にはしない。
    private const float CruiseStopGaugeAtLowEagerness = 0.35f;

    // 追い上げ補正が最大のときに、加速の閾値をずらす量。
    private const float CatchUpGaugeShift = 0.3f;

    // 最下位（RankRatio＝1）のときに、加速の閾値を下げる量。
    private const float LowRankGaugeShift = 0.15f;

    private const float MinCruiseGauge = 0.2f;

    public bool WantsBoost { get; private set; }

    // 今のブーストが攻撃のためか（加速のためならfalse）。
    public bool IsAttacking { get; private set; }

    public void Think(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, CpuTargetSelector targetSelector, float catchUp)
    {
        CPUManager owner = situation.Owner;
        float gauge = owner.BoostGaugeNormalized;
        bool canAttack = targetSelector.HasTarget && !targetSelector.Target.IsShielding;
        bool isCurveAhead = situation.CurveAngleAhead > difficulty.BoostCurveLimit;

        if (owner.IsBoosting)
        {
            if (gauge <= difficulty.BoostReserve)
            {
                Stop();
                return;
            }

            // 攻撃中は、相手がいる限り続ける。相手がいなくなったら、加速としてまだ続けてよいかを見る。
            if (IsAttacking && canAttack)
            {
                return;
            }

            IsAttacking = false;
            float stopGauge = Mathf.Max(difficulty.BoostReserve, Mathf.Lerp(CruiseStopGaugeAtLowEagerness, 0f, personality.BoostEagerness));
            WantsBoost = !isCurveAhead && gauge > stopGauge;
            return;
        }

        if (!owner.CanActivateBoost)
        {
            Stop();
            return;
        }

        if (canAttack && IsAttackChance(targetSelector.Target, difficulty, personality, gauge))
        {
            WantsBoost = true;
            IsAttacking = true;
            return;
        }

        float cruiseGauge = Mathf.Lerp(CruiseGaugeAtLowEagerness, CruiseGaugeAtHighEagerness, personality.BoostEagerness)
            - catchUp * CatchUpGaugeShift
            - situation.Self.RankRatio * LowRankGaugeShift;
        IsAttacking = false;
        WantsBoost = !isCurveAhead && gauge >= Mathf.Clamp(cruiseGauge, MinCruiseGauge, 1f);
    }

    public void Reset()
    {
        Stop();
    }

    private static bool IsAttackChance(CpuRacerView target, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, float gauge)
    {
        float attackGauge = Mathf.Lerp(AttackGaugeAtLowAggression, AttackGaugeAtHighAggression, personality.Aggression);
        return target.ForwardGap <= AttackBoostRange
            && Mathf.Abs(target.LateralGap) <= difficulty.AttackAlignTolerance
            && gauge >= attackGauge;
    }

    private void Stop()
    {
        WantsBoost = false;
        IsAttacking = false;
    }
}
