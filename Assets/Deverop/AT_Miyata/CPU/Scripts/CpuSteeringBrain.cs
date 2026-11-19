using System.Collections.Generic;
using UnityEngine;

// 上下左右の操作（Move）を決める。考え直すTickで「目標の位置（トンネルの断面での位置）」を決め、毎Tick、そこへ寄せるMoveを出す（2次元のPD制御）。
// 目標の位置は、断面の候補点（中央と、同心円上の点）それぞれに「コスト」を付けて、一番低い点を選ぶ。
//   コスト ＝ 行きたい位置からの遠さ（狙う相手 ＞ 先頭なら中央 ＞ 個性の走行ライン）
//          ＋ 後ろからのブーストの通り道（シールドを張っていないとき）
//          ＋ 前の近いレーサーと重なる
//          ＋ 前方のギミック：爆弾・減速エリア・減速リング・障害物に当たる位置（気づいたものだけ）
//          － 前方の加速リングをくぐれる位置（気づいたものだけ。個性の加速リング志向で強さが変わる）
//          ＋ 今の位置・前の目標からの遠さ（小さい。目標がふらふら変わらないように）
// ギミックに気づくかは、ギミックごとに1回だけ、難易度のGimmickAwarenessで決める（見落としたギミックは、無いものとして走る）。
// 目標はトンネル半径の内側に収める。壁に触れたら（減速するので）、しばらくはさらに内側に収める。
// Moveには難易度のぶれを足す。
public class CpuSteeringBrain
{
    // 目標の位置を、トンネル半径のこの割合より内側に収める。
    private const float LineMargin = 0.8f;

    // 壁に触れてから、この秒数の間は、トンネル半径のWallLineMarginの割合より内側に収める。
    private const float WallRetreatDuration = 1.5f;
    private const float WallLineMargin = 0.55f;

    // 候補点：中央＋同心円（目標の限界に対する割合）×方向の数。
    private static readonly float[] CandidateRings = { 0.35f, 0.7f, 1f };
    private const int CandidateDirections = 12;

    // 行きたい位置からの遠さ1mあたりのコスト。狙う相手がいるときは強める（そろえないと当たらないため）。
    private const float PreferenceWeight = 0.25f;
    private const float TargetPreferenceWeight = 0.6f;

    // 今の位置・前の目標からの遠さ1mあたりのコスト。
    private const float MoveWeight = 0.05f;
    private const float StickWeight = 0.08f;

    // 後ろからのブーストを避けるとき：相手の位置からこの距離（m）以内の候補に、近いほど大きいコストを付ける。
    private const float DodgeWidth = 3.5f;
    private const float DodgeWeight = 6f;

    // 前のレーサーをよけるとき：この距離（m）以内の前の相手の位置から、この距離（m）以内の候補にコストを付ける。
    private const float AvoidAheadDistance = 12f;
    private const float AvoidWidth = 2.5f;
    private const float AvoidWeight = 3f;

    // 避けるギミックに当たる候補のコスト。体の大きさ＋この余裕（m）以内に近づく候補にも、近いほどコストを付ける。
    // 遠いギミックほど弱める（近いギミックを優先して避ける）。
    private const float HazardCost = 30f;
    private const float HazardMargin = 1.5f;
    private const float FarHazardWeight = 0.5f;

    // 加速リング：くぐれる候補（楕円のこの割合より内側）のコストを下げる量。加速リング志向0～1で、小さい～大きい。
    // 減速リング：楕円のこの割合より内側の候補にコストを付ける。
    private const float SpeedRingInnerScale = 0.75f;
    private const float SlowRingOuterScale = 1.1f;
    private const float MinRingBonus = 1f;
    private const float MaxRingBonus = 10f;

    // 走行ラインの中央からの距離＝外寄りの度合い（PreferredLine）×目標の限界×この割合。小さいほど全体が中央寄りになる。
    private const float LineSpread = 0.5f;

    // 走行ラインを変える間隔（秒）。ライン変更の頻度0～1で、長い～短い。実際の間隔は、これに0.7～1.3倍のばらつきを掛ける。
    private const float LineChangeIntervalAtLowFrequency = 15f;
    private const float LineChangeIntervalAtHighFrequency = 3f;

    // ラインを変えたとき、外寄りの度合い（PreferredLine）に足すばらつき。
    private const float LineJitter = 0.2f;

    // CPUどうしが断面の同じ方向に重ならないよう、RacerIdごとに走行ラインの角度をこれだけずらす（黄金角）。
    private const float LineAngleStepDegrees = 137.5f;

    private readonly Dictionary<int, bool> _noticedGimmicks = new Dictionary<int, bool>();
    private readonly List<Vector2> _candidates = new List<Vector2>();

    private Vector2 _baseLine;
    private float _lineTimer;
    private bool _isLineInitialized;
    private float _noiseSeedX;
    private float _noiseSeedY;
    private float _wallRetreatTimer;
    private bool _hasPreviousTarget;

    // 目標の位置（m。x＝右、y＝上）。
    public Vector2 TargetOffset { get; private set; }

    // 今、ギミックを避けている・リングをくぐろうとしているか（頭上の表示用）。
    public bool IsAvoidingGimmick { get; private set; }

    public bool IsSeekingRing { get; private set; }

    public void Think(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, CpuTargetSelector targetSelector,
        CpuShieldBrain shieldBrain, System.Random random)
    {
        if (!_isLineInitialized)
        {
            InitializeLine(situation, personality, random);
        }

        float margin = _wallRetreatTimer > 0f ? WallLineMargin : LineMargin;
        float limit = Mathf.Max(0f, situation.TunnelRadius * margin);

        bool hasTarget = targetSelector.HasTarget;
        Vector2 preferred = DecidePreferred(situation, targetSelector, limit);
        float preferenceWeight = hasTarget ? TargetPreferenceWeight : PreferenceWeight;
        bool dodges = shieldBrain.HasRacerThreat && !situation.Owner.IsShielding;
        float ringBonus = Mathf.Lerp(MinRingBonus, MaxRingBonus, personality.RingPreference);

        BuildCandidates(situation, preferred, targetSelector, limit);

        float bestCost = float.MaxValue;
        Vector2 best = Vector2.zero;
        bool bestAvoids = false;
        bool bestSeeksRing = false;
        foreach (Vector2 candidate in _candidates)
        {
            float cost = preferenceWeight * Vector2.Distance(candidate, preferred)
                + MoveWeight * Vector2.Distance(candidate, situation.Offset)
                + (_hasPreviousTarget ? StickWeight * Vector2.Distance(candidate, TargetOffset) : 0f);

            if (dodges)
            {
                cost += DodgeWeight * Mathf.Max(0f, DodgeWidth - Vector2.Distance(candidate, shieldBrain.RacerThreat.Offset));
            }

            cost += EvaluateBlockers(situation, targetSelector, candidate);
            cost += EvaluateGimmicks(situation, difficulty, random, candidate, ringBonus, out bool avoids, out bool seeksRing);

            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
                bestAvoids = avoids;
                bestSeeksRing = seeksRing;
            }
        }

        TargetOffset = best;
        _hasPreviousTarget = true;
        IsSeekingRing = bestSeeksRing;
        IsAvoidingGimmick = !bestSeeksRing && bestAvoids;
    }

    // 毎Tick呼ぶ。走行ライン・壁から離れる時間を進め、目標の位置へ寄せるMove（大きさ0～1）を返す。
    public Vector2 Evaluate(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random, float time)
    {
        UpdateLineTimer(situation.DeltaTime, personality, random);
        UpdateWallRetreat(situation);

        Vector2 error = TargetOffset - situation.Offset;
        float noiseTime = time * difficulty.SteeringNoiseFrequency;
        Vector2 noise = new Vector2(
            (Mathf.PerlinNoise(noiseTime, _noiseSeedX) - 0.5f) * 2f,
            (Mathf.PerlinNoise(noiseTime, _noiseSeedY) - 0.5f) * 2f) * difficulty.SteeringNoise;
        Vector2 move = difficulty.SteeringGain * error - difficulty.SteeringDamping * situation.PlanarVelocity + noise;
        return Vector2.ClampMagnitude(move, 1f);
    }

    public void Reset()
    {
        _isLineInitialized = false;
        _wallRetreatTimer = 0f;
        _hasPreviousTarget = false;
        _noticedGimmicks.Clear();
        TargetOffset = Vector2.zero;
        IsAvoidingGimmick = false;
        IsSeekingRing = false;
    }

    // 壁に触れた瞬間、目標の位置がまだ壁寄りなら、考え直すのを待たずにすぐ内側へ寄せる。
    private void UpdateWallRetreat(CpuSituation situation)
    {
        _wallRetreatTimer = Mathf.Max(0f, _wallRetreatTimer - situation.DeltaTime);
        if (!situation.Owner.IsTouchingWall)
        {
            return;
        }

        _wallRetreatTimer = WallRetreatDuration;
        float limit = Mathf.Max(0f, situation.TunnelRadius * WallLineMargin);
        TargetOffset = Vector2.ClampMagnitude(TargetOffset, limit);
    }

    // 行きたい位置：狙う相手 ＞ 先頭なら中央 ＞ 個性の走行ライン。
    private Vector2 DecidePreferred(CpuSituation situation, CpuTargetSelector targetSelector, float limit)
    {
        if (targetSelector.HasTarget)
        {
            return Vector2.ClampMagnitude(targetSelector.Target.Offset, limit);
        }

        // 先頭なら、よける相手も並ぶ相手もいないので、中央を走る。
        if (IsLeading(situation))
        {
            return Vector2.zero;
        }

        return _baseLine * (limit * LineSpread);
    }

    private void BuildCandidates(CpuSituation situation, Vector2 preferred, CpuTargetSelector targetSelector, float limit)
    {
        _candidates.Clear();
        _candidates.Add(Vector2.zero);
        foreach (float ring in CandidateRings)
        {
            float radius = limit * ring;
            for (int i = 0; i < CandidateDirections; i++)
            {
                float angle = i * Mathf.PI * 2f / CandidateDirections;
                _candidates.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        // 行きたい位置・今の位置・前の目標・リングの中心も候補にする（同心円の点の間にあっても選べるように）。
        _candidates.Add(Vector2.ClampMagnitude(preferred, limit));
        _candidates.Add(Vector2.ClampMagnitude(situation.Offset, limit));
        if (_hasPreviousTarget)
        {
            _candidates.Add(Vector2.ClampMagnitude(TargetOffset, limit));
        }

        if (targetSelector.HasTarget)
        {
            _candidates.Add(Vector2.ClampMagnitude(targetSelector.Target.Offset, limit));
        }

        foreach (CpuGimmickView gimmick in situation.Gimmicks)
        {
            if (gimmick.Kind == ECpuGimmickKind.SpeedRing)
            {
                _candidates.Add(Vector2.ClampMagnitude(gimmick.Offset, limit));
            }
        }
    }

    // 前の近いレーサー（狙う相手以外）と重なる候補のコスト。
    private static float EvaluateBlockers(CpuSituation situation, CpuTargetSelector targetSelector, Vector2 candidate)
    {
        float cost = 0f;
        foreach (CpuRacerView other in situation.Others)
        {
            bool isAhead = other.ForwardGap > 0f && other.ForwardGap <= AvoidAheadDistance;
            bool isTarget = targetSelector.HasTarget && other.RacerId == targetSelector.Target.RacerId;
            if (isAhead && !isTarget)
            {
                cost += AvoidWeight * Mathf.Max(0f, AvoidWidth - Vector2.Distance(candidate, other.Offset));
            }
        }

        return cost;
    }

    // 前方のギミックによるコスト。avoids：避けるギミックに当たる候補か。seeksRing：加速リングをくぐれる候補か。
    private float EvaluateGimmicks(CpuSituation situation, CpuDifficultyProfile difficulty, System.Random random, Vector2 candidate, float ringBonus,
        out bool avoids, out bool seeksRing)
    {
        avoids = false;
        seeksRing = false;
        float cost = 0f;
        float lookAhead = Mathf.Max(1f, difficulty.GimmickLookAhead);
        float clearance = situation.BodyRadius + HazardMargin;

        foreach (CpuGimmickView gimmick in situation.Gimmicks)
        {
            if (!HasNoticed(gimmick, difficulty, random))
            {
                continue;
            }

            float nearness = Mathf.Lerp(1f, FarHazardWeight, Mathf.Clamp01(gimmick.ForwardDistance / lookAhead));
            Vector3 point = gimmick.ToWorld(candidate);
            switch (gimmick.Kind)
            {
                case ECpuGimmickKind.SpeedRing:
                    if (CpuWorldCache.IsInsideRing((SpeedRing)gimmick.Gimmick, point, SpeedRingInnerScale))
                    {
                        cost -= ringBonus * nearness;
                        seeksRing = true;
                    }

                    break;
                case ECpuGimmickKind.SlowRing:
                    if (CpuWorldCache.IsInsideRing((SpeedRing)gimmick.Gimmick, point, SlowRingOuterScale))
                    {
                        cost += HazardCost * nearness;
                        avoids = true;
                    }

                    break;
                default:
                    float distance = CpuWorldCache.DistanceTo(gimmick.Gimmick, point);
                    if (distance < clearance)
                    {
                        cost += HazardCost * nearness * (1f - distance / clearance * 0.5f);
                        avoids = true;
                    }

                    break;
            }
        }

        return cost;
    }

    // ギミックに気づいたか。ギミックごとに、最初に見えたときに1回だけ決める。
    private bool HasNoticed(CpuGimmickView gimmick, CpuDifficultyProfile difficulty, System.Random random)
    {
        int id = gimmick.Id;
        if (!_noticedGimmicks.TryGetValue(id, out bool noticed))
        {
            noticed = random.NextDouble() < difficulty.GimmickAwareness;
            _noticedGimmicks[id] = noticed;
        }

        return noticed;
    }

    // 前に誰もいない（ゴール済みのレーサーは除く）。
    private static bool IsLeading(CpuSituation situation)
    {
        foreach (CpuRacerView other in situation.Others)
        {
            if (other.ForwardGap > 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void InitializeLine(CpuSituation situation, CpuPersonalityProfile personality, System.Random random)
    {
        float angle = situation.Self.RacerId * LineAngleStepDegrees * Mathf.Deg2Rad;
        _baseLine = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * personality.PreferredLine;
        _lineTimer = NextLineInterval(personality, random);
        _noiseSeedX = (float)random.NextDouble() * 100f;
        _noiseSeedY = (float)random.NextDouble() * 100f + 100f;
        _isLineInitialized = true;
    }

    private void UpdateLineTimer(float deltaTime, CpuPersonalityProfile personality, System.Random random)
    {
        if (!_isLineInitialized)
        {
            return;
        }

        _lineTimer -= deltaTime;
        if (_lineTimer > 0f)
        {
            return;
        }

        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float jitter = ((float)random.NextDouble() * 2f - 1f) * LineJitter;
        _baseLine = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Clamp01(personality.PreferredLine + jitter);
        _lineTimer = NextLineInterval(personality, random);
    }

    private static float NextLineInterval(CpuPersonalityProfile personality, System.Random random)
    {
        float interval = Mathf.Lerp(LineChangeIntervalAtLowFrequency, LineChangeIntervalAtHighFrequency, personality.LineChangeFrequency);
        return interval * Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble());
    }
}
