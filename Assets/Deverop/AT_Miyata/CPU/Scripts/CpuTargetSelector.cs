using UnityEngine;

// ブーストで狙う相手を選ぶ。PlayerもCPUも同じ扱いで、自分以外の全員に点数を付けて一番高い相手を狙う。
//   点数 ＝ 近さ ＋ 横位置のそろい具合 ＋ 順位ボーナス（自分より上） ＋ 執念ボーナス（直近に自分へ当てた相手）
// シールド中（当てると防がれて相手が前へ押し出される）・スタン中（無敵）の相手は狙わない。
// 狙う相手が頻繁に変わらないよう、今の相手が有効なうちは、点数が一番の相手の一定割合以上なら変えない。
// 誰に当てられたかはBoostHitInfoに含まれないため、スタンした瞬間に近くでブーストしていた相手を、当てた相手とみなす。
public class CpuTargetSelector
{
    // 狙う前後の距離（m）。攻撃性0～1で、最小～最大の間にする。
    private const float MinAttackRange = 10f;
    private const float MaxAttackRange = 45f;

    // これより近い（m）相手は、もう横に並んでいるので狙わない。
    private const float MinAttackGap = 1f;

    // 狙うのに必要な点数。攻撃性0～1で、高い～低い（攻撃的なほど少しの好機でも狙う）。
    private const float ScoreThresholdAtLowAggression = 1.4f;
    private const float ScoreThresholdAtHighAggression = 0.5f;

    private const float RankBonus = 0.3f;

    // 執念ボーナスと、執念を持ち続ける秒数（執念0～1に掛ける）。執念の相手は、狙う距離も伸ばす。
    private const float MaxGrudgeBonus = 1.5f;
    private const float MaxGrudgeDuration = 15f;
    private const float GrudgeRangeMultiplier = 1.5f;

    // スタンした瞬間、当てた相手とみなす範囲（前後の差の大きさ、m）。
    private const float AttackerSearchRange = 15f;

    // 今の相手を続ける条件（一番の相手の点数に対する割合）。
    private const float KeepTargetScoreRatio = 0.8f;

    private int _targetId = -1;
    private int _grudgeId = -1;
    private float _grudgeTimer;
    private bool _wasStunned;

    public bool HasTarget { get; private set; }

    // 狙っている相手の、このTickの様子。HasTargetがfalseなら無効。
    public CpuRacerView Target { get; private set; }

    // 執念を持っている相手のRacerId。いなければ-1。
    public int GrudgeId => _grudgeTimer > 0f ? _grudgeId : -1;

    // think：考え直すTickか。狙う相手を選び直すのは考え直すときだけで、それ以外のTickは今の相手の様子を最新にするだけ。
    public void Update(CpuSituation situation, CpuPersonalityProfile personality, bool think)
    {
        UpdateGrudge(situation, personality);
        RefreshTarget(situation, personality);

        if (think)
        {
            SelectTarget(situation, personality);
        }
    }

    public void Reset()
    {
        ClearTarget();
        _grudgeId = -1;
        _grudgeTimer = 0f;
        _wasStunned = false;
    }

    private void UpdateGrudge(CpuSituation situation, CpuPersonalityProfile personality)
    {
        _grudgeTimer = Mathf.Max(0f, _grudgeTimer - situation.DeltaTime);

        bool isStunned = situation.Owner.IsStunned;
        if (isStunned && !_wasStunned && TryFindAttacker(situation, out int attackerId) && personality.Vengefulness > 0f)
        {
            _grudgeId = attackerId;
            _grudgeTimer = MaxGrudgeDuration * personality.Vengefulness;
        }

        _wasStunned = isStunned;
    }

    private static bool TryFindAttacker(CpuSituation situation, out int attackerId)
    {
        attackerId = -1;
        float bestDistance = float.MaxValue;
        foreach (CpuRacerView other in situation.Others)
        {
            if (!other.IsBoosting)
            {
                continue;
            }

            float distance = Mathf.Abs(other.ForwardGap) + Mathf.Abs(other.LateralGap);
            if (Mathf.Abs(other.ForwardGap) <= AttackerSearchRange && distance < bestDistance)
            {
                bestDistance = distance;
                attackerId = other.RacerId;
            }
        }

        return attackerId >= 0;
    }

    // 今の相手の様子を最新にする。いなくなった・狙えなくなったら外す。
    private void RefreshTarget(CpuSituation situation, CpuPersonalityProfile personality)
    {
        if (!HasTarget)
        {
            return;
        }

        if (situation.TryGetRacer(_targetId, out CpuRacerView view) && Evaluate(situation, personality, view) > 0f)
        {
            Target = view;
        }
        else
        {
            ClearTarget();
        }
    }

    private void SelectTarget(CpuSituation situation, CpuPersonalityProfile personality)
    {
        float bestScore = 0f;
        CpuRacerView best = default;
        bool hasBest = false;
        foreach (CpuRacerView other in situation.Others)
        {
            float score = Evaluate(situation, personality, other);
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
                hasBest = true;
            }
        }

        float threshold = Mathf.Lerp(ScoreThresholdAtLowAggression, ScoreThresholdAtHighAggression, personality.Aggression);
        if (!hasBest || bestScore < threshold)
        {
            ClearTarget();
            return;
        }

        if (HasTarget && best.RacerId != _targetId && Evaluate(situation, personality, Target) >= bestScore * KeepTargetScoreRatio)
        {
            return;
        }

        _targetId = best.RacerId;
        Target = best;
        HasTarget = true;
    }

    // 狙えない相手は0。
    private float Evaluate(CpuSituation situation, CpuPersonalityProfile personality, CpuRacerView other)
    {
        if (other.IsShielding || other.IsStunned)
        {
            return 0f;
        }

        bool isGrudge = other.RacerId == GrudgeId;
        float range = Mathf.Lerp(MinAttackRange, MaxAttackRange, personality.Aggression) * (isGrudge ? GrudgeRangeMultiplier : 1f);
        if (other.ForwardGap < MinAttackGap || other.ForwardGap > range)
        {
            return 0f;
        }

        float closeness = 1f - other.ForwardGap / range;
        float alignment = 1f - Mathf.Clamp01(Mathf.Abs(other.LateralGap) / Mathf.Max(0.1f, situation.TunnelRadius));
        float rank = other.Data.CurrentRank < situation.Self.CurrentRank ? RankBonus : 0f;
        float grudge = isGrudge ? MaxGrudgeBonus * personality.Vengefulness : 0f;
        return closeness + alignment + rank + grudge;
    }

    private void ClearTarget()
    {
        _targetId = -1;
        Target = default;
        HasTarget = false;
    }
}
