using UnityEngine;

// 左右の操作を決める。考え直すTickで「目標の横位置」を決め、毎Tick、そこへ寄せるHorizontalを出す（PD制御）。
// 目標の横位置の優先順位：
//   1. 後ろからのブーストを避ける（シールドを張っていないとき）
//   2. 狙う相手と同じ横位置へ寄せる
//   3. 前の近いレーサーとぶつかりそうなら、空いている側へよける
//   4. 先頭なら中央
//   5. 個性の走行ライン（CPUどうしが一列に重ならないよう、RacerIdで左右を分け、ときどき変える）
// 目標はトンネル半径の内側に収める。壁に触れたら（減速するので）、しばらくはさらに内側に収める。
// Horizontalには難易度のぶれを足す。
public class CpuSteeringBrain
{
    // 目標の横位置を、トンネル半径のこの割合より内側に収める。
    private const float LineMargin = 0.8f;

    // 壁に触れてから、この秒数の間は、トンネル半径のWallLineMarginの割合より内側に収める。
    private const float WallRetreatDuration = 1.5f;
    private const float WallLineMargin = 0.55f;

    // よける対象にする、前のレーサーとの距離（m）と横位置の差（m）。よけるときは、この横位置の差の分だけ離す。
    private const float AvoidAheadDistance = 12f;
    private const float AvoidLateralWidth = 2.5f;

    // 後ろからのブーストを避けるときに、相手から離す横位置の差（m）。
    private const float DodgeLateralWidth = 3.5f;

    // 走行ラインの横位置＝外寄りの度合い（PreferredLine）×目標の横位置の限界×この割合。小さいほど全体が中央寄りになる。
    private const float LineSpread = 0.5f;

    // 走行ラインを変える間隔（秒）。ライン変更の頻度0～1で、長い～短い。実際の間隔は、これに0.7～1.3倍のばらつきを掛ける。
    private const float LineChangeIntervalAtLowFrequency = 15f;
    private const float LineChangeIntervalAtHighFrequency = 3f;

    // ラインを変えたとき、外寄りの度合い（PreferredLine）に足すばらつき。
    private const float LineJitter = 0.2f;

    private float _baseLine;
    private float _lineTimer;
    private bool _isLineInitialized;
    private float _noiseSeed;
    private float _wallRetreatTimer;

    public float TargetLateral { get; private set; }

    public void Think(CpuSituation situation, CpuPersonalityProfile personality, CpuTargetSelector targetSelector, CpuShieldBrain shieldBrain, System.Random random)
    {
        if (!_isLineInitialized)
        {
            InitializeLine(situation, personality, random);
        }

        float margin = _wallRetreatTimer > 0f ? WallLineMargin : LineMargin;
        float limit = Mathf.Max(0f, situation.TunnelRadius * margin);
        TargetLateral = Mathf.Clamp(DecideTargetLateral(situation, targetSelector, shieldBrain, limit), -limit, limit);
    }

    // 毎Tick呼ぶ。走行ライン・壁から離れる時間を進め、目標の横位置へ寄せるHorizontalを返す。
    public float Evaluate(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random, float time)
    {
        UpdateLineTimer(situation.DeltaTime, personality, random);
        UpdateWallRetreat(situation);

        float error = TargetLateral - situation.Lateral;
        float noise = (Mathf.PerlinNoise(time * difficulty.SteeringNoiseFrequency, _noiseSeed) - 0.5f) * 2f * difficulty.SteeringNoise;
        float horizontal = difficulty.SteeringGain * error - difficulty.SteeringDamping * situation.LateralVelocity + noise;
        return Mathf.Clamp(horizontal, -1f, 1f);
    }

    public void Reset()
    {
        _isLineInitialized = false;
        _wallRetreatTimer = 0f;
        TargetLateral = 0f;
    }

    // 壁に触れた瞬間、目標の横位置がまだ壁寄りなら、考え直すのを待たずにすぐ内側へ寄せる。
    private void UpdateWallRetreat(CpuSituation situation)
    {
        _wallRetreatTimer = Mathf.Max(0f, _wallRetreatTimer - situation.DeltaTime);
        if (!situation.Owner.IsTouchingWall)
        {
            return;
        }

        _wallRetreatTimer = WallRetreatDuration;
        float limit = Mathf.Max(0f, situation.TunnelRadius * WallLineMargin);
        TargetLateral = Mathf.Clamp(TargetLateral, -limit, limit);
    }

    private float DecideTargetLateral(CpuSituation situation, CpuTargetSelector targetSelector, CpuShieldBrain shieldBrain, float limit)
    {
        if (shieldBrain.HasThreat && !situation.Owner.IsShielding)
        {
            return AwayFrom(shieldBrain.Threat.Lateral, situation.Lateral, DodgeLateralWidth, limit);
        }

        if (targetSelector.HasTarget)
        {
            return targetSelector.Target.Lateral;
        }

        // 先頭なら、よける相手も並ぶ相手もいないので、中央を走る。
        if (IsLeading(situation))
        {
            return 0f;
        }

        float baseLateral = _baseLine * limit * LineSpread;
        if (TryFindBlocker(situation, targetSelector, out CpuRacerView blocker))
        {
            return AwayFrom(blocker.Lateral, baseLateral, AvoidLateralWidth, limit);
        }

        return baseLateral;
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

    // 前の近いレーサーのうち、一番近くて横位置が重なっている相手。
    private static bool TryFindBlocker(CpuSituation situation, CpuTargetSelector targetSelector, out CpuRacerView blocker)
    {
        float bestGap = float.MaxValue;
        blocker = default;
        bool found = false;
        foreach (CpuRacerView other in situation.Others)
        {
            bool isAhead = other.ForwardGap > 0f && other.ForwardGap <= AvoidAheadDistance;
            bool isOverlapping = Mathf.Abs(other.LateralGap) < AvoidLateralWidth;
            bool isTarget = targetSelector.HasTarget && other.RacerId == targetSelector.Target.RacerId;
            if (isAhead && isOverlapping && !isTarget && other.ForwardGap < bestGap)
            {
                bestGap = other.ForwardGap;
                blocker = other;
                found = true;
            }
        }

        return found;
    }

    // 相手（otherLateral）から横にwidth離れた、左右2つの位置のうち、preferredLateralに近く、トンネルの内側に収まる方。
    private static float AwayFrom(float otherLateral, float preferredLateral, float width, float limit)
    {
        float left = otherLateral - width;
        float right = otherLateral + width;
        bool canLeft = left >= -limit;
        bool canRight = right <= limit;
        if (canLeft != canRight)
        {
            return canLeft ? left : right;
        }

        return Mathf.Abs(left - preferredLateral) <= Mathf.Abs(right - preferredLateral) ? left : right;
    }

    private void InitializeLine(CpuSituation situation, CpuPersonalityProfile personality, System.Random random)
    {
        float side = situation.Self.RacerId % 2 == 0 ? 1f : -1f;
        _baseLine = side * personality.PreferredLine;
        _lineTimer = NextLineInterval(personality, random);
        _noiseSeed = (float)random.NextDouble() * 100f;
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

        float side = random.NextDouble() < 0.5 ? -1f : 1f;
        float jitter = ((float)random.NextDouble() * 2f - 1f) * LineJitter;
        _baseLine = side * Mathf.Clamp01(personality.PreferredLine + jitter);
        _lineTimer = NextLineInterval(personality, random);
    }

    private static float NextLineInterval(CpuPersonalityProfile personality, System.Random random)
    {
        float interval = Mathf.Lerp(LineChangeIntervalAtLowFrequency, LineChangeIntervalAtHighFrequency, personality.LineChangeFrequency);
        return interval * Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble());
    }
}
