using System.Collections.Generic;
using UnityEngine;

// 周りの状況を、コースの向きを基準にした値（前後の差・横ずれ）にまとめてCpuSituationへ書く。
// 前後の差はワールドの距離ではなくDistanceOnCourseの差を使う（カーブでも前後関係が崩れないように）。
// RacerDataには速度が無いので、前後の差の変化から相手との速さの差を出す（ばらつくので少しならす）。
public class CpuPerception
{
    // 前後の差の変化の速さをならす強さ（1/秒）。大きいほど今の値をすぐ使う。
    private const float RelativeSpeedSmoothing = 10f;

    private readonly Dictionary<int, float> _previousGaps = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _relativeSpeeds = new Dictionary<int, float>();

    // コースの情報が取れなければ（CPUManagerのInitialize前など）falseを返す。
    public bool Update(CpuSituation situation, CPUManager owner, float deltaTime, IReadOnlyRacerData self, CpuDifficultyProfile difficulty)
    {
        ICourseGuide guide = owner.CourseGuide;
        if (guide == null || self == null)
        {
            return false;
        }

        situation.Owner = owner;
        situation.Self = self;
        situation.DeltaTime = deltaTime;

        Vector3 position = owner.Transform.position;
        Vector3 center = guide.GetCenterPosition(position);
        Vector3 right = guide.GetRight(position);
        situation.CourseCenter = center;
        situation.CourseRight = right;
        situation.Lateral = Vector3.Dot(position - center, right);
        situation.LateralVelocity = Vector3.Dot(owner.Velocity, right);
        situation.TunnelRadius = guide.GetTunnelRadius(position);

        Vector3 lookAheadPosition = guide.GetLookAheadPosition(position, difficulty.CurveCheckDistance);
        situation.CurveAngleAhead = Vector3.Angle(guide.GetForward(position), guide.GetForward(lookAheadPosition));

        UpdateOthers(situation, guide, deltaTime, self);
        return true;
    }

    // 置き直し（PlaceAt）でレースをやり直すときに呼ぶ。前回の差が残っていると、速さの差が飛ぶため。
    public void Reset()
    {
        _previousGaps.Clear();
        _relativeSpeeds.Clear();
    }

    private void UpdateOthers(CpuSituation situation, ICourseGuide guide, float deltaTime, IReadOnlyRacerData self)
    {
        situation.Others.Clear();
        situation.HasPlayer = false;
        situation.GapToPlayer = 0f;

        IReadOnlyList<IReadOnlyRacerData> racers = situation.Owner.Racers;
        for (int i = 0; i < racers.Count; i++)
        {
            IReadOnlyRacerData other = racers[i];
            if (other == null || other.RacerId == self.RacerId || other.Transform == null)
            {
                continue;
            }

            float gap = other.DistanceOnCourse - self.DistanceOnCourse;
            if (other.IsPlayer)
            {
                situation.HasPlayer = true;
                situation.GapToPlayer = gap;
            }

            // ゴール済みの相手は、狙う・避ける対象にしない（順位の比較だけに使う）。
            if (other.IsFinished)
            {
                continue;
            }

            Vector3 otherPosition = other.Transform.position;
            float otherLateral = Vector3.Dot(otherPosition - guide.GetCenterPosition(otherPosition), guide.GetRight(otherPosition));
            float relativeSpeed = UpdateRelativeSpeed(other.RacerId, gap, deltaTime);
            situation.Others.Add(new CpuRacerView(other, gap, otherLateral, otherLateral - situation.Lateral, relativeSpeed));
        }
    }

    private float UpdateRelativeSpeed(int racerId, float gap, float deltaTime)
    {
        _relativeSpeeds.TryGetValue(racerId, out float relativeSpeed);
        if (deltaTime > 0f && _previousGaps.TryGetValue(racerId, out float previousGap))
        {
            float rawSpeed = (gap - previousGap) / deltaTime;
            relativeSpeed = Mathf.Lerp(relativeSpeed, rawSpeed, 1f - Mathf.Exp(-RelativeSpeedSmoothing * deltaTime));
        }

        _previousGaps[racerId] = gap;
        _relativeSpeeds[racerId] = relativeSpeed;
        return relativeSpeed;
    }
}
