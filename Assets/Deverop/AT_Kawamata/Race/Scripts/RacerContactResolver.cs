using System.Collections.Generic;
using UnityEngine;

// 通常の接触（ブーストなし）で、レーサーどうしの食い込みを押し戻す。RaceManagerがRacerのTickの後に1フレームに1回呼ぶ。
// レーサーはすべてKinematicで位置をスクリプトで動かしているため、物理の衝突は使わず、
// Physics.ComputePenetrationで食い込みの向きと深さだけを求めて、当たり方ごとに位置を直す。
// ・横から：左右だけで押し戻す（前後には動かさない）。押した側を多く戻し、押した側の相手へ向かう横速度を消す（押しづらい）
// ・後ろから：後ろのレーサーだけを前の相手より前に出さないよう戻し、左右へずらして追い抜かせる（前のレーサーは押し出さない）
// 人数が少ないので、全員の組み合わせを総当たりで調べる。
public class RacerContactResolver
{
    // 後ろから当たったとき、2人の中心どうしの左右の距離がこれ未満なら、ずらす向きはコースの中央側にする。
    private const float UndeterminedSideDistance = 0.05f;

    // 食い込みの向きと、押し戻す軸（左右／前後）が離れているほど多く動かす必要があるが、離れすぎて大きく飛ばないよう下限を設ける。
    private const float MinAxisAlignment = 0.5f;

    private readonly ICourseGuide _guide;
    private readonly RacerContactProfile _profile;
    private readonly List<IRacerContactBody> _bodies = new List<IRacerContactBody>();

    public RacerContactResolver(ICourseGuide guide, RacerContactProfile profile)
    {
        _guide = guide;
        _profile = profile;
    }

    public void Add(IRacerContactBody body)
    {
        _bodies.Add(body);
    }

    public void Resolve(float deltaTime)
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            for (int j = i + 1; j < _bodies.Count; j++)
            {
                ResolvePair(_bodies[i], _bodies[j], deltaTime);
            }
        }
    }

    private void ResolvePair(IRacerContactBody a, IRacerContactBody b, float deltaTime)
    {
        if (a.IgnoresContact || b.IgnoresContact)
        {
            return;
        }

        Collider colliderA = a.BodyCollider;
        Collider colliderB = b.BodyCollider;
        if (colliderA == null || colliderB == null || !colliderA.enabled || !colliderB.enabled)
        {
            return;
        }

        Transform transformA = colliderA.transform;
        Transform transformB = colliderB.transform;

        // direction：aをbから離す向き。depth：その向きに動かせば離れる距離。
        bool isOverlapping = Physics.ComputePenetration(
            colliderA, transformA.position, transformA.rotation,
            colliderB, transformB.position, transformB.rotation,
            out Vector3 direction, out float depth);

        if (!isOverlapping || depth <= 0f)
        {
            return;
        }

        Vector3 middle = (transformA.position + transformB.position) * 0.5f;
        Vector3 right = _guide.GetRight(middle);
        Vector3 forward = _guide.GetForward(middle);
        float lateral = Vector3.Dot(direction, right);
        float longitudinal = Vector3.Dot(direction, forward);
        float angleFromSide = Mathf.Atan2(Mathf.Abs(longitudinal), Mathf.Abs(lateral)) * Mathf.Rad2Deg;

        if (angleFromSide <= _profile.SideContactMaxAngle)
        {
            ResolveSideContact(a, b, right, lateral, depth);
        }
        else
        {
            ResolveRearContact(a, b, right, forward, longitudinal, depth, deltaTime);
        }
    }

    // 左右だけで押し戻す。押した側（相手へ向かう横の速さが大きい方）を多く戻す。
    private void ResolveSideContact(IRacerContactBody a, IRacerContactBody b, Vector3 right, float lateral, float depth)
    {
        Vector3 awayFromBForA = right * (lateral >= 0f ? 1f : -1f);
        float lateralDistance = depth / Mathf.Max(Mathf.Abs(lateral), MinAxisAlignment);

        float approachA = Mathf.Max(0f, -Vector3.Dot(a.Velocity, awayFromBForA));
        float approachB = Mathf.Max(0f, Vector3.Dot(b.Velocity, awayFromBForA));

        float shareA;
        if (Mathf.Max(approachA, approachB) < _profile.PushingSpeedThreshold)
        {
            shareA = 0.5f;
        }
        else
        {
            shareA = approachA >= approachB ? _profile.PusherCorrectionRate : 1f - _profile.PusherCorrectionRate;
        }

        a.ApplyContactCorrection(awayFromBForA * (lateralDistance * shareA), awayFromBForA);
        b.ApplyContactCorrection(-awayFromBForA * (lateralDistance * (1f - shareA)), -awayFromBForA);
    }

    // 後ろのレーサーだけを、前に出られないよう戻しつつ、左右へずらす。
    private void ResolveRearContact(IRacerContactBody a, IRacerContactBody b, Vector3 right, Vector3 forward, float longitudinal, float depth, float deltaTime)
    {
        // aを離す向きが後ろ向きなら、aが後ろ。
        bool isARear = longitudinal < 0f;
        IRacerContactBody rear = isARear ? a : b;
        IRacerContactBody front = isARear ? b : a;

        float backDistance = depth / Mathf.Max(Mathf.Abs(longitudinal), MinAxisAlignment);

        Vector3 rearPosition = rear.BodyCollider.transform.position;
        Vector3 frontPosition = front.BodyCollider.transform.position;
        float sideOffset = Vector3.Dot(rearPosition - frontPosition, right);
        float side;
        if (Mathf.Abs(sideOffset) >= UndeterminedSideDistance)
        {
            side = Mathf.Sign(sideOffset);
        }
        else
        {
            float offsetToCenter = Vector3.Dot(_guide.GetCenterPosition(rearPosition) - rearPosition, right);
            side = offsetToCenter >= 0f ? 1f : -1f;
        }

        Vector3 slide = right * (side * _profile.RearSlideSpeed * deltaTime);

        // 前進の速度は消さない（抜けた瞬間にそのまま加速して追い抜けるように）。
        rear.ApplyContactCorrection(-forward * backDistance + slide, Vector3.zero);
    }
}
