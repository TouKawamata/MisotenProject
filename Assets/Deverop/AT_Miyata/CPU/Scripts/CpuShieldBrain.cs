using System.Collections.Generic;
using UnityEngine;

// シールドで防ぐ脅威の種類。
public enum ECpuThreatKind
{
    None,

    // 後ろから、ブースト中で、断面の位置がそろったまま近づいてくるレーサー。
    BoostRacer,

    // 後ろから自分の方へ飛んでくる妨害弾（標的以外でも、通り道にいれば当たるため、通り道にいれば防ぐ）。
    Jammer,

    // 爆発に巻き込まれそうな爆弾（爆風の範囲内にいて、誰か（自分も含む）が触れそう）。
    Bomb,
}

// シールドを張りたいかを決める。
// 脅威（3種類のうち、一番早く届きそうなもの1つ）に気づいてから反応時間が経ったら、難易度の成功率で張るかを1回だけ決める（同じ脅威に何度も抽選しない）。
// 脅威が去っても少しの間は張り続ける。シールド中は最高速度が下がるので、張りっぱなしにはしない。
// 自分がブースト中のとき：
//   ・後ろからのブースト → 張らない（ブースト同士ははじくだけで、スタンしないため）
//   ・妨害弾・爆弾 → 慎重なCPU（慎重さがInterruptBoostCaution以上）だけ、ブーストをやめて張る
// 反応時間を秒で数えるため、考え直す間隔に関係なく毎Tick更新する。
public class CpuShieldBrain
{
    // 後ろのブーストを脅威とみなす距離（m）。慎重さ0～1で、最小～最大の間にする。
    private const float MinThreatRange = 8f;
    private const float MaxThreatRange = 25f;

    // 後ろのブーストを脅威とみなす、断面での距離（m）。
    private const float ThreatOffsetWidth = 3f;

    // 妨害弾を脅威とみなす距離（m）。慎重さ0～1で、最小～最大の間にする。弾は速い（90m/sくらい）ので、ブーストより遠くから見る。
    private const float MinJammerRange = 25f;
    private const float MaxJammerRange = 60f;

    // 妨害弾の通り道（弾の向きの直線）から、この距離（m）以内にいたら防ぐ。
    private const float JammerPathWidth = 5f;

    // 妨害弾が自分の方を向いているとみなす、向きの内積。
    private const float JammerHeadingDot = 0.6f;

    // 爆弾のColliderにこの距離（m）以内まで近づいたレーサーがいたら、爆発しそうとみなす。
    private const float BombTriggerMargin = 5f;

    // 爆弾の爆風の範囲に掛ける割合（端の方は吹き飛ばしが弱いので、少し内側だけを脅威にする）。
    private const float BombRangeRate = 0.9f;

    // 反応時間に掛ける倍率。慎重さ0～1で、遅い～早い。
    private const float ReactionMultiplierAtLowCaution = 1.3f;
    private const float ReactionMultiplierAtHighCaution = 0.7f;

    // 脅威が去ってから張り続ける秒数。慎重さ0～1で、短い～長い。
    private const float MinHoldAfterThreat = 0.2f;
    private const float MaxHoldAfterThreat = 1f;

    // 慎重さがこれ以上なら、妨害弾・爆弾のためにブーストをやめてシールドを張る。
    private const float InterruptBoostCaution = 0.5f;

    private ECpuThreatKind _threatKind = ECpuThreatKind.None;
    private int _threatKey = int.MinValue;
    private float _reactionTimer;
    private bool _hasDecided;
    private bool _willBlock;
    private float _holdTimer;

    public bool WantsShield { get; private set; }

    // シールドのために、ブーストをやめたいか（妨害弾・爆弾で、慎重なとき）。
    public bool WantsToStopBoost { get; private set; }

    // 今の脅威の種類（Noneなら脅威なし）。
    public ECpuThreatKind ThreatKind { get; private set; }

    // 今の脅威の位置（Gizmosの表示用）。
    public Vector3 ThreatPosition { get; private set; }

    // 後ろからのブーストの脅威（避ける操作に使う。シールドとは別に、一番近い1人を毎Tick探す）。
    public bool HasRacerThreat { get; private set; }

    public CpuRacerView RacerThreat { get; private set; }

    public void Update(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random)
    {
        CPUManager owner = situation.Owner;
        HasRacerThreat = TryFindRacerThreat(situation, personality, out CpuRacerView racerThreat);
        RacerThreat = racerThreat;

        FindThreat(situation, personality, out ECpuThreatKind kind, out int key, out Vector3 threatPosition);
        ThreatKind = kind;
        ThreatPosition = threatPosition;
        WantsToStopBoost = false;

        // 自分がブースト中なら、後ろのブーストは防がなくてよい。妨害弾・爆弾は、慎重なCPUだけブーストをやめて防ぐ。
        if (owner.IsBoosting && (kind == ECpuThreatKind.None || kind == ECpuThreatKind.BoostRacer || personality.Caution < InterruptBoostCaution))
        {
            WantsShield = false;
            _holdTimer = 0f;
            return;
        }

        if (kind == ECpuThreatKind.None)
        {
            _threatKind = ECpuThreatKind.None;
            _threatKey = int.MinValue;
            _holdTimer = Mathf.Max(0f, _holdTimer - situation.DeltaTime);
            WantsShield = owner.IsShielding && _holdTimer > 0f;
            return;
        }

        if (kind != _threatKind || key != _threatKey)
        {
            _threatKind = kind;
            _threatKey = key;
            _reactionTimer = difficulty.ReactionTime * Mathf.Lerp(ReactionMultiplierAtLowCaution, ReactionMultiplierAtHighCaution, personality.Caution);
            _hasDecided = false;
        }

        _reactionTimer -= situation.DeltaTime;
        if (!_hasDecided && _reactionTimer <= 0f)
        {
            _hasDecided = true;
            _willBlock = random.NextDouble() < difficulty.ShieldSuccessRate;
        }

        bool decidedToBlock = _hasDecided && _willBlock;
        if (owner.IsBoosting)
        {
            // ブーストを離せば、次のTickからシールドを張れる。
            WantsToStopBoost = decidedToBlock;
            WantsShield = decidedToBlock;
            return;
        }

        WantsShield = decidedToBlock && (owner.IsShielding || owner.CanActivateShield);
        if (WantsShield)
        {
            _holdTimer = Mathf.Lerp(MinHoldAfterThreat, MaxHoldAfterThreat, personality.Caution);
        }
    }

    public void Reset()
    {
        _threatKind = ECpuThreatKind.None;
        _threatKey = int.MinValue;
        _reactionTimer = 0f;
        _hasDecided = false;
        _willBlock = false;
        _holdTimer = 0f;
        WantsShield = false;
        WantsToStopBoost = false;
        ThreatKind = ECpuThreatKind.None;
        HasRacerThreat = false;
        RacerThreat = default;
    }

    // 3種類の脅威のうち、一番早く届きそうなもの。爆弾は爆発が一瞬なので、見つけたら最優先にする。
    private void FindThreat(CpuSituation situation, CpuPersonalityProfile personality, out ECpuThreatKind kind, out int key, out Vector3 position)
    {
        if (TryFindBombThreat(situation, out Bomb bomb))
        {
            kind = ECpuThreatKind.Bomb;
            key = bomb.GetInstanceID();
            position = bomb.transform.position;
            return;
        }

        float bestTime = float.MaxValue;
        kind = ECpuThreatKind.None;
        key = int.MinValue;
        position = Vector3.zero;

        if (TryFindJammerThreat(situation, personality, out JammerProjectile projectile, out float jammerTime))
        {
            bestTime = jammerTime;
            kind = ECpuThreatKind.Jammer;
            key = projectile.GetInstanceID();
            position = projectile.transform.position;
        }

        if (HasRacerThreat && RacerThreat.Data.Transform != null)
        {
            float racerTime = -RacerThreat.ForwardGap / Mathf.Max(0.1f, RacerThreat.RelativeSpeed);
            if (racerTime < bestTime)
            {
                kind = ECpuThreatKind.BoostRacer;
                key = RacerThreat.RacerId;
                position = RacerThreat.Data.Transform.position;
            }
        }
    }

    private static bool TryFindRacerThreat(CpuSituation situation, CpuPersonalityProfile personality, out CpuRacerView threat)
    {
        float range = Mathf.Lerp(MinThreatRange, MaxThreatRange, personality.Caution);
        float bestGap = float.MinValue;
        threat = default;
        bool found = false;
        foreach (CpuRacerView other in situation.Others)
        {
            bool isBehind = other.ForwardGap < 0f && other.ForwardGap >= -range;
            bool isApproaching = other.RelativeSpeed > 0f;
            bool isAligned = other.OffsetGap.magnitude <= ThreatOffsetWidth;
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

    // 自分の方を向いて飛んでくる妨害弾のうち、一番近いもの。time：届くまでのおおよその秒数。
    // 自分が撃った弾は、自分より前で前を向いているので、ここには入らない。
    private static bool TryFindJammerThreat(CpuSituation situation, CpuPersonalityProfile personality, out JammerProjectile threat, out float time)
    {
        float range = Mathf.Lerp(MinJammerRange, MaxJammerRange, personality.Caution);
        Vector3 position = situation.Owner.Transform.position;
        float bestDistance = float.MaxValue;
        threat = null;
        time = float.MaxValue;

        IReadOnlyList<JammerProjectile> projectiles = CpuWorldCache.Projectiles;
        for (int i = 0; i < projectiles.Count; i++)
        {
            JammerProjectile projectile = projectiles[i];
            if (projectile == null)
            {
                continue;
            }

            // リングを狙う弾はレーサーに当たらない。シールドで防げない種類は、張っても意味がない。
            if (projectile.TargetRacer == null || projectile.Type == null || !projectile.Type.CanBeBlockedByShield)
            {
                continue;
            }

            Vector3 toSelf = position - projectile.transform.position;
            float distance = toSelf.magnitude;
            if (distance > range || distance < 0.001f || distance >= bestDistance)
            {
                continue;
            }

            Vector3 heading = projectile.transform.forward;
            float along = Vector3.Dot(toSelf, heading);
            bool isHeadingToSelf = along / distance >= JammerHeadingDot;
            bool isOnPath = Vector3.Cross(heading, toSelf).magnitude <= JammerPathWidth;
            if (isHeadingToSelf && isOnPath)
            {
                bestDistance = distance;
                threat = projectile;
            }
        }

        if (threat == null)
        {
            return false;
        }

        // 弾の速さは読めないので、自分より十分速いものとして、距離を前進の速さの倍くらいで割った値にしておく（比べるためだけ）。
        time = bestDistance / Mathf.Max(10f, situation.Owner.ForwardSpeed * 2f);
        return true;
    }

    // 爆風の範囲内にいて、自分か他のレーサーが触れそうな、有効な爆弾。
    private static bool TryFindBombThreat(CpuSituation situation, out Bomb threat)
    {
        Vector3 position = situation.Owner.Transform.position;
        threat = null;

        IReadOnlyList<CourseGimmickBase> gimmicks = CpuWorldCache.Gimmicks;
        for (int i = 0; i < gimmicks.Count; i++)
        {
            if (!(gimmicks[i] is Bomb bomb) || bomb == null || !bomb.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 bombPosition = bomb.transform.position;
            if (Vector3.Distance(position, bombPosition) > bomb.BlastRadius * BombRangeRate)
            {
                continue;
            }

            float triggerDistance = situation.BodyRadius + BombTriggerMargin;
            if (CpuWorldCache.DistanceTo(bomb, position) <= triggerDistance || IsAnyoneNear(situation, bomb, triggerDistance))
            {
                threat = bomb;
                return true;
            }
        }

        return false;
    }

    private static bool IsAnyoneNear(CpuSituation situation, Bomb bomb, float triggerDistance)
    {
        foreach (CpuRacerView other in situation.Others)
        {
            if (other.Data.Transform != null && CpuWorldCache.DistanceTo(bomb, other.Data.Transform.position) <= triggerDistance)
            {
                return true;
            }
        }

        return false;
    }
}
