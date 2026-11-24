using System.Collections.Generic;
using UnityEngine;

// 周りの状況を、コースの向きを基準にした値（前後の差・断面でのずれ）にまとめてCpuSituationへ書く。
// 前後の差はワールドの距離ではなくDistanceOnCourseの差を使う（カーブでも前後関係が崩れないように）。
// RacerDataには速度が無いので、前後の差の変化から相手との速さの差を出す（ばらつくので少しならす）。
// 断面でのずれは、コースのGetRight（x）・GetUp（y）への射影（移動が上下左右の平行移動のため）。
// 前方のコースギミックも、自分のコース上（トンネルの中）にあるものだけ、近い順に集める。
public class CpuPerception
{
    // 前後の差の変化の速さをならす強さ（1/秒）。大きいほど今の値をすぐ使う。
    private const float RelativeSpeedSmoothing = 10f;

    // ギミックを自分のコース上とみなす、トンネルの壁からはみ出してよい距離（m）。分岐の別のルートにあるものを除くため。
    private const float GimmickOnCourseMargin = 5f;

    // 体の大きさが取れないときに使う値（m）。
    private const float DefaultBodyRadius = 1.5f;

    private readonly Dictionary<int, float> _previousGaps = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _relativeSpeeds = new Dictionary<int, float>();
    private bool _hasLookedUpLauncher;
    private JammerLauncher _launcher;

    // コースの情報が取れなければ（CPUManagerのInitialize前など）falseを返す。
    public bool Update(CpuSituation situation, CPUManager owner, float deltaTime, IReadOnlyRacerData self, CpuDifficultyProfile difficulty)
    {
        ICourseGuide guide = owner.CourseGuide;
        if (guide == null || self == null)
        {
            return false;
        }

        if (!_hasLookedUpLauncher)
        {
            _hasLookedUpLauncher = true;
            _launcher = owner.GetComponentInChildren<JammerLauncher>(true);
        }

        situation.Owner = owner;
        situation.Self = self;
        situation.Launcher = _launcher;
        situation.DeltaTime = deltaTime;

        Vector3 position = owner.Transform.position;
        Vector3 center = guide.GetCenterPosition(position);
        Vector3 right = guide.GetRight(position);
        Vector3 up = guide.GetUp(position);
        Vector3 forward = guide.GetForward(position);
        situation.CourseCenter = center;
        situation.CourseRight = right;
        situation.CourseUp = up;
        situation.CourseForward = forward;
        situation.Offset = ToOffset(position - center, right, up);
        situation.PlanarVelocity = ToOffset(owner.Velocity, right, up);
        situation.TunnelRadius = guide.GetTunnelRadius(position);
        situation.BodyRadius = GetBodyRadius(owner);

        Vector3 lookAheadPosition = guide.GetLookAheadPosition(position, difficulty.CurveCheckDistance);
        situation.CurveAngleAhead = Vector3.Angle(forward, guide.GetForward(lookAheadPosition));

        UpdateOthers(situation, guide, deltaTime, self);
        UpdateGimmicks(situation, guide, position, forward, difficulty.GimmickLookAhead);
        return true;
    }

    // 置き直し（PlaceAt）でレースをやり直すときに呼ぶ。前回の差が残っていると、速さの差が飛ぶため。
    public void Reset()
    {
        _previousGaps.Clear();
        _relativeSpeeds.Clear();
    }

    public static Vector2 ToOffset(Vector3 vector, Vector3 right, Vector3 up)
    {
        return new Vector2(Vector3.Dot(vector, right), Vector3.Dot(vector, up));
    }

    private static float GetBodyRadius(CPUManager owner)
    {
        Collider body = owner.BodyCollider;
        if (body == null)
        {
            return DefaultBodyRadius;
        }

        Vector3 extents = body.bounds.extents;
        return Mathf.Max(0.5f, Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
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
            Vector2 otherOffset = ToOffset(otherPosition - guide.GetCenterPosition(otherPosition), guide.GetRight(otherPosition), guide.GetUp(otherPosition));
            float relativeSpeed = UpdateRelativeSpeed(other.RacerId, gap, deltaTime);
            situation.Others.Add(new CpuRacerView(other, gap, otherOffset, otherOffset - situation.Offset, relativeSpeed));
        }
    }

    private static void UpdateGimmicks(CpuSituation situation, ICourseGuide guide, Vector3 position, Vector3 forward, float lookAhead)
    {
        situation.Gimmicks.Clear();
        if (lookAhead <= 0f)
        {
            return;
        }

        IReadOnlyList<CourseGimmickBase> gimmicks = CpuWorldCache.Gimmicks;
        for (int i = 0; i < gimmicks.Count; i++)
        {
            CourseGimmickBase gimmick = gimmicks[i];
            if (gimmick == null || !gimmick.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 gimmickPosition = gimmick.transform.position;
            float forwardDistance = Vector3.Dot(gimmickPosition - position, forward);
            if (forwardDistance < -situation.BodyRadius || forwardDistance > lookAhead)
            {
                continue;
            }

            Vector3 center = guide.GetCenterPosition(gimmickPosition);
            if (Vector3.Distance(center, gimmickPosition) > guide.GetTunnelRadius(gimmickPosition) + GimmickOnCourseMargin)
            {
                continue;
            }

            ECpuGimmickKind kind = CpuWorldCache.Classify(gimmick);
            if (kind == ECpuGimmickKind.Ignore)
            {
                continue;
            }

            Vector3 right = guide.GetRight(gimmickPosition);
            Vector3 up = guide.GetUp(gimmickPosition);
            situation.Gimmicks.Add(new CpuGimmickView(gimmick, kind, forwardDistance, center, right, up, ToOffset(gimmickPosition - center, right, up)));
        }

        situation.Gimmicks.Sort((a, b) => a.ForwardDistance.CompareTo(b.ForwardDistance));
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
