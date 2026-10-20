using UnityEngine;

// シールドを張りたいかを決める。
// 脅威＝後ろの一定距離以内で、ブースト中で、横位置がそろったまま近づいてくる相手（一番近い1人）。
// 脅威に気づいてから反応時間が経ったら、難易度の成功率で張るかを1回だけ決める（同じ脅威に何度も抽選しない）。
// 脅威が去っても少しの間は張り続ける。シールド中は最高速度が下がるので、張りっぱなしにはしない。
// 自分がブースト中なら張らない（ブースト同士ははじくだけで、スタンしないため。ブースト中はそもそもシールドを張れない）。
// 反応時間を秒で数えるため、考え直す間隔に関係なく毎Tick更新する。
public class CpuShieldBrain
{
    // 脅威とみなす後ろの距離（m）。慎重さ0～1で、最小～最大の間にする。
    private const float MinThreatRange = 8f;
    private const float MaxThreatRange = 25f;

    // 脅威とみなす横位置の差（m）。
    private const float ThreatLateralWidth = 3f;

    // 反応時間に掛ける倍率。慎重さ0～1で、遅い～早い。
    private const float ReactionMultiplierAtLowCaution = 1.3f;
    private const float ReactionMultiplierAtHighCaution = 0.7f;

    // 脅威が去ってから張り続ける秒数。慎重さ0～1で、短い～長い。
    private const float MinHoldAfterThreat = 0.2f;
    private const float MaxHoldAfterThreat = 1f;

    private int _threatId = -1;
    private float _reactionTimer;
    private bool _hasDecided;
    private bool _willBlock;
    private float _holdTimer;

    public bool WantsShield { get; private set; }

    public bool HasThreat { get; private set; }

    // 脅威の相手の、このTickの様子。HasThreatがfalseなら無効。
    public CpuRacerView Threat { get; private set; }

    public void Update(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random)
    {
        CPUManager owner = situation.Owner;
        HasThreat = TryFindThreat(situation, personality, out CpuRacerView threat);
        Threat = threat;

        if (owner.IsBoosting)
        {
            WantsShield = false;
            _holdTimer = 0f;
            return;
        }

        if (!HasThreat)
        {
            _threatId = -1;
            _holdTimer = Mathf.Max(0f, _holdTimer - situation.DeltaTime);
            WantsShield = owner.IsShielding && _holdTimer > 0f;
            return;
        }

        if (threat.RacerId != _threatId)
        {
            _threatId = threat.RacerId;
            _reactionTimer = difficulty.ReactionTime * Mathf.Lerp(ReactionMultiplierAtLowCaution, ReactionMultiplierAtHighCaution, personality.Caution);
            _hasDecided = false;
        }

        _reactionTimer -= situation.DeltaTime;
        if (!_hasDecided && _reactionTimer <= 0f)
        {
            _hasDecided = true;
            _willBlock = random.NextDouble() < difficulty.ShieldSuccessRate;
        }

        WantsShield = _hasDecided && _willBlock && (owner.IsShielding || owner.CanActivateShield);
        if (WantsShield)
        {
            _holdTimer = Mathf.Lerp(MinHoldAfterThreat, MaxHoldAfterThreat, personality.Caution);
        }
    }

    public void Reset()
    {
        _threatId = -1;
        _reactionTimer = 0f;
        _hasDecided = false;
        _willBlock = false;
        _holdTimer = 0f;
        WantsShield = false;
        HasThreat = false;
        Threat = default;
    }

    private static bool TryFindThreat(CpuSituation situation, CpuPersonalityProfile personality, out CpuRacerView threat)
    {
        float range = Mathf.Lerp(MinThreatRange, MaxThreatRange, personality.Caution);
        float bestGap = float.MinValue;
        threat = default;
        bool found = false;
        foreach (CpuRacerView other in situation.Others)
        {
            bool isBehind = other.ForwardGap < 0f && other.ForwardGap >= -range;
            bool isApproaching = other.RelativeSpeed > 0f;
            bool isAligned = Mathf.Abs(other.LateralGap) <= ThreatLateralWidth;
            if (!other.IsBoosting || !isBehind || !isApproaching || !isAligned)
            {
                continue;
            }

            // 後ろの相手はForwardGapが負なので、大きいほど近い。
            if (other.ForwardGap > bestGap)
            {
                bestGap = other.ForwardGap;
                threat = other;
                found = true;
            }
        }

        return found;
    }
}
