using System.Collections.Generic;
using UnityEngine;

// 妨害弾。標的が遠い間はコースに沿って飛び、近づくほど標的へ狙いを寄せる（最後はまっすぐ向かう）。
// 自分とゴール済みのレーサーを除く全員が当たる対象で、標的以外でも進路上にいれば当たる。
// 速いので物理のTriggerではすり抜けることがある。そのため、このフレームで進んだ線分とレーサーの中心との距離で当たりを見る。
// 当たった相手の効果はIJammerHitReceiverが決める。IJammerHitReceiverを持たないレーサー（DummyRacerなど）は素通りする。
public class JammerProjectile : MonoBehaviour
{
    private JammerProfile _profile;
    private IReadOnlyRacerData _owner;
    private IReadOnlyRacerData _target;
    private IReadOnlyList<IReadOnlyRacerData> _racers;
    private ICourseGuide _guide;
    private Vector3 _direction;
    private float _age;

    public void Launch(JammerProfile profile, IReadOnlyRacerData owner, IReadOnlyRacerData target, IReadOnlyList<IReadOnlyRacerData> racers, ICourseGuide guide, Vector3 direction)
    {
        _profile = profile;
        _owner = owner;
        _target = target;
        _racers = racers;
        _guide = guide;
        _direction = direction.normalized;
    }

    private void Update()
    {
        if (_profile == null)
        {
            return;
        }

        _age += Time.deltaTime;
        if (_age >= _profile.Lifetime || _target.IsFinished)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 previousPosition = transform.position;
        Move(previousPosition, Time.deltaTime);

        if (TryHitRacer(previousPosition, transform.position))
        {
            Destroy(gameObject);
        }
    }

    private void Move(Vector3 position, float deltaTime)
    {
        Vector3 toAim = GetAimPosition(position) - position;
        if (toAim.sqrMagnitude > 0.0001f)
        {
            float maxTurnRadians = _profile.TurnSpeed * Mathf.Deg2Rad * deltaTime;
            _direction = Vector3.RotateTowards(_direction, toAim.normalized, maxTurnRadians, 0f).normalized;
        }

        transform.SetPositionAndRotation(position + _direction * (_profile.ProjectileSpeed * deltaTime), Quaternion.LookRotation(_direction));
    }

    // 遠いほどコースの先読み点、近いほど標的に寄せた点を狙う。先読み点は中心からのズレを保つので、コースと平行に飛ぶ。
    private Vector3 GetAimPosition(Vector3 position)
    {
        Vector3 targetPosition = _target.Transform.position;
        if (_guide == null)
        {
            return targetPosition;
        }

        float distance = Vector3.Distance(position, targetPosition);
        float homingRate = Mathf.InverseLerp(_profile.HomingStartDistance, _profile.DirectHomingDistance, distance);
        if (homingRate >= 1f)
        {
            return targetPosition;
        }

        return Vector3.Lerp(_guide.GetLookAheadPosition(position, _profile.CourseLookAhead), targetPosition, homingRate);
    }

    // このフレームで進んだ線分に近いレーサーへ当てる。効果が無かったとき（無敵中など）も弾は消える。
    private bool TryHitRacer(Vector3 start, Vector3 end)
    {
        float hitSqrDistance = _profile.HitRadius * _profile.HitRadius;

        foreach (IReadOnlyRacerData racer in _racers)
        {
            if (racer.RacerId == _owner.RacerId || racer.IsFinished)
            {
                continue;
            }

            if (GetSqrDistanceToSegment(racer.Transform.position, start, end) > hitSqrDistance)
            {
                continue;
            }

            if (racer.Transform.TryGetComponent(out IJammerHitReceiver receiver))
            {
                receiver.ReceiveJammerHit();
                return true;
            }
        }

        return false;
    }

    private static float GetSqrDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentSqrLength = segment.sqrMagnitude;
        float t = segmentSqrLength > 0f ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentSqrLength) : 0f;
        return (start + segment * t - point).sqrMagnitude;
    }
}
