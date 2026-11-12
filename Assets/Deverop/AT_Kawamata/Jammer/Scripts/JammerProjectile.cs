using System.Collections.Generic;
using UnityEngine;

// 妨害弾。標的が遠い間はコースに沿って飛び、近づくほど標的へ狙いを寄せる（最後はまっすぐ向かう）。
// ・レーサーを狙う弾：標的のコースのガイド（分岐のあるコースでは標的のルート）に沿って飛ぶ。自分とゴール済みを除く全員が当たる対象で、
//   標的以外でも進路上にいれば当たる。効果を受けるかはIJammerHitReceiverが決める。持たないレーサー（DummyRacerなど）は素通りする
// ・リングを狙う弾：撃ったレーサーのガイドに沿って飛び、標的のリングにだけ当たる
// 速いので物理のTriggerではすり抜けることがある。そのため、このフレームで進んだ線分と、標的の中心との距離で当たりを見る。
// 当たったら消える（効果が無かったとき（無敵中など）も消える）。寿命が尽きた・標的がゴールした・標的のリングが無効になったときも消える。
public class JammerProjectile : MonoBehaviour
{
    private JammerProfile _profile;
    private JammerTypeProfile _type;
    private IReadOnlyRacerData _owner;
    private IReadOnlyRacerData _targetRacer;
    private GameObject _targetObject;
    private IReadOnlyList<IReadOnlyRacerData> _racers;
    private ICourseGuide _guide;
    private Vector3 _direction;
    private float _age;

    // レーサーを狙う弾を撃つ。ownerGuide：標的がガイドを持っていないときに使う（撃ったレーサーのガイド）。
    public void LaunchAtRacer(JammerProfile profile, JammerTypeProfile type, IReadOnlyRacerData owner, IReadOnlyRacerData target,
        IReadOnlyList<IReadOnlyRacerData> racers, ICourseGuide ownerGuide, Vector3 direction)
    {
        ICourseGuide targetGuide = target.Transform.TryGetComponent(out ICourseGuideOwner guideOwner) ? guideOwner.CourseGuide : null;
        Launch(profile, type, owner, racers, targetGuide ?? ownerGuide, direction);
        _targetRacer = target;
    }

    // リングなど、レーサー以外を狙う弾を撃つ。
    public void LaunchAtObject(JammerProfile profile, JammerTypeProfile type, IReadOnlyRacerData owner, GameObject target,
        ICourseGuide ownerGuide, Vector3 direction)
    {
        Launch(profile, type, owner, null, ownerGuide, direction);
        _targetObject = target;
    }

    private void Launch(JammerProfile profile, JammerTypeProfile type, IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers,
        ICourseGuide guide, Vector3 direction)
    {
        _profile = profile;
        _type = type;
        _owner = owner;
        _racers = racers;
        _guide = guide;
        _direction = direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;
        _age = 0f;
    }

    private void Update()
    {
        if (_profile == null || _type == null)
        {
            return;
        }

        _age += Time.deltaTime;
        if (_age >= _profile.Lifetime || !HasValidTarget())
        {
            Destroy(gameObject);
            return;
        }

        Vector3 previousPosition = transform.position;
        Move(previousPosition, Time.deltaTime);

        bool isHit = _targetRacer != null
            ? TryHitRacer(previousPosition, transform.position)
            : TryHitObject(previousPosition, transform.position);

        if (isHit)
        {
            Destroy(gameObject);
        }
    }

    private bool HasValidTarget()
    {
        if (_targetRacer != null)
        {
            return !_targetRacer.IsFinished && _targetRacer.Transform != null;
        }

        return _targetObject != null && _targetObject.activeInHierarchy;
    }

    private Vector3 TargetPosition => _targetRacer != null ? _targetRacer.Transform.position : _targetObject.transform.position;

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
        Vector3 targetPosition = TargetPosition;
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

    // このフレームで進んだ線分に近いレーサーへ当てる（標的以外でも当たる）。
    private bool TryHitRacer(Vector3 start, Vector3 end)
    {
        if (_racers == null)
        {
            return false;
        }

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
                receiver.ReceiveJammerHit(_type);
                return true;
            }
        }

        return false;
    }

    private bool TryHitObject(Vector3 start, Vector3 end)
    {
        float radius = _type is RingSwapJammerType ring ? ring.RingHitRadius : _profile.HitRadius;
        if (GetSqrDistanceToSegment(_targetObject.transform.position, start, end) > radius * radius)
        {
            return false;
        }

        _type.ApplyToTarget(_targetObject);
        return true;
    }

    private static float GetSqrDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentSqrLength = segment.sqrMagnitude;
        float t = segmentSqrLength > 0f ? Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentSqrLength) : 0f;
        return (start + segment * t - point).sqrMagnitude;
    }
}
