using System.Collections.Generic;
using UnityEngine;

// レーサー1人分の、コースギミックとの接触の判定。RacerManagerBaseが飛行のTickの直後に、飛行で動いた量を渡して呼ぶ。
// レーサーはKinematicなので、静的なギミックとは物理の衝突もOnTriggerEnterも当てにできない（1フレームに1m以上動くので、薄いものはすり抜ける）。
// そのため、動いた分をCapsuleCastでなぞり、最後の位置で重なりを調べて、Obstacle｜BonusレイヤーのColliderからCourseGimmickBaseを探して呼ぶ。
// ・固いギミック（IsSolid）：当たった位置まで進め、OnRacerHitの応答（滑る／止まる／通す）で体を動かす
// ・すり抜けるギミック：なぞった途中で触れたもの・最後の位置で重なっているものを「触れている」とし、Enter／Stay／Exitを呼ぶ
// ・CourseGimmickBaseを持たないObstacle｜BonusレイヤーのColliderは、壊れない壁として滑らせる
// ギミックの効果（減速・スタンなど）はギミックがIGimmickRacerで反映するので、ここでは体の位置・速度を直すことだけをする。
public class RacerGimmickSensor
{
    // 1フレームになぞり直す回数の上限（滑った先でも当たることがあるため）。
    private const int MaxSweepCount = 3;

    // 面との間に空けるすき間（m）。次のフレームのCapsuleCastが、重なった状態から始まらないようにする。
    private const float SkinWidth = 0.02f;

    private const float MinMoveDistance = 0.0001f;

    private const int MaxHitCount = 16;

    private const int MaxOverlapCount = 16;

    private readonly CapsuleCollider _body;
    private readonly PlayerFlightController _flightController;
    private readonly IGimmickRacer _racer;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHitCount];
    private readonly Collider[] _overlaps = new Collider[MaxOverlapCount];

    // このフレームで触れた、すり抜けるギミックと、その接触の情報。
    private readonly Dictionary<CourseGimmickBase, GimmickContact> _touched = new Dictionary<CourseGimmickBase, GimmickContact>();

    // 前のフレームまでに入った（Enterを呼んだ）、すり抜けるギミック。
    private readonly HashSet<CourseGimmickBase> _inside = new HashSet<CourseGimmickBase>();
    private readonly List<CourseGimmickBase> _buffer = new List<CourseGimmickBase>();

    public RacerGimmickSensor(CapsuleCollider body, PlayerFlightController flightController, IGimmickRacer racer)
    {
        _body = body;
        _flightController = flightController;
        _racer = racer;

        if (CourseGimmickBase.GimmickLayerMask == 0)
        {
            Debug.LogWarning($"RacerGimmickSensor: {CourseGimmickBase.ObstacleLayerName}／{CourseGimmickBase.BonusLayerName}レイヤーが見つからないため、ギミックを判定しません");
        }
    }

    // 置き直し・ゴールのとき。入っていたギミックにExitを出して、空にする。
    public void Reset()
    {
        _touched.Clear();
        _buffer.Clear();
        _buffer.AddRange(_inside);
        _inside.Clear();

        foreach (CourseGimmickBase gimmick in _buffer)
        {
            if (gimmick != null)
            {
                gimmick.OnRacerExit(_racer);
            }
        }
    }

    // displacement：このフレームの飛行で動いた量（ワールド座標）。体はすでに動いた後の位置にある。
    public void Resolve(Vector3 displacement, float deltaTime)
    {
        int layerMask = CourseGimmickBase.GimmickLayerMask;
        if (layerMask == 0)
        {
            return;
        }

        Vector3 movedPosition = _body.transform.position;
        Vector3 position = movedPosition - displacement;
        Vector3 remaining = displacement;
        Vector3 moveDirection = displacement.sqrMagnitude > MinMoveDistance * MinMoveDistance ? displacement.normalized : Vector3.zero;
        _touched.Clear();

        bool isCorrected = PushOutOfSolidOverlaps(ref position, layerMask);

        for (int i = 0; i < MaxSweepCount; i++)
        {
            float distance = remaining.magnitude;
            if (distance < MinMoveDistance)
            {
                break;
            }

            Vector3 direction = remaining / distance;
            if (!SweepToFirstBlock(position, direction, distance, layerMask, out float travel, out EGimmickBlockResponse response, out Vector3 normal))
            {
                position += remaining;
                break;
            }

            position += direction * travel;
            remaining -= direction * travel;
            isCorrected = true;

            // 面へ向かう速度だけを消す（面に沿った速度は減らさない＝摩擦力0）。
            _flightController.ApplyContactCorrection(Vector3.zero, normal);
            if (response == EGimmickBlockResponse.Stop)
            {
                break;
            }

            remaining = Vector3.ProjectOnPlane(remaining, normal);
        }

        if (isCorrected)
        {
            _flightController.ApplyContactCorrection(position - movedPosition, Vector3.zero);
        }

        CollectOverlaps(position, moveDirection, layerMask);
        DispatchEnterStayExit(deltaTime);
    }

    // なぞった途中で触れた、すり抜けるギミックを記録しながら、最初に体を止める固いものを探す。見つからなければfalse。
    private bool SweepToFirstBlock(Vector3 position, Vector3 direction, float distance, int layerMask, out float travel, out EGimmickBlockResponse response, out Vector3 normal)
    {
        travel = distance;
        response = EGimmickBlockResponse.Slide;
        normal = Vector3.zero;

        GetCapsule(position, out Vector3 point1, out Vector3 point2, out float radius);
        int count = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, _hits, distance + SkinWidth, layerMask, QueryTriggerInteraction.Collide);
        SortHitsByDistance(count);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            Collider hitCollider = hit.collider;
            CourseGimmickBase gimmick = hitCollider.GetComponentInParent<CourseGimmickBase>();
            if (gimmick != null && !gimmick.isActiveAndEnabled)
            {
                continue;
            }

            // なぞり始めに重なっていたもの（distance 0・point 0が返る）。
            bool startsInside = hit.distance <= 0f && hit.point == Vector3.zero;

            if (gimmick != null && !gimmick.IsSolid)
            {
                if (!_touched.ContainsKey(gimmick))
                {
                    _touched[gimmick] = startsInside
                        ? CreateOverlapContact(hitCollider, position, direction)
                        : new GimmickContact(_racer, hitCollider, hit.point, hit.normal, direction);
                }

                continue;
            }

            // 固いもの。重なりは先に押し出してあり、面から離れる向きなら止めない。
            if (startsInside || Vector3.Dot(direction, hit.normal) >= 0f)
            {
                continue;
            }

            EGimmickBlockResponse hitResponse = gimmick != null
                ? gimmick.OnRacerHit(new GimmickContact(_racer, hitCollider, hit.point, hit.normal, direction))
                : EGimmickBlockResponse.Slide;

            if (hitResponse == EGimmickBlockResponse.PassThrough)
            {
                continue;
            }

            travel = Mathf.Clamp(hit.distance - SkinWidth, 0f, distance);
            response = hitResponse;
            normal = hit.normal;
            return true;
        }

        return false;
    }

    // CapsuleCastは開始時点で重なっている相手の面を返さないので、固いものは先に押し出しておく（レーサーどうしの押し戻しの直後など）。
    // 押し出しだけで、OnRacerHitは呼ばない。
    private bool PushOutOfSolidOverlaps(ref Vector3 position, int layerMask)
    {
        GetCapsule(position, out Vector3 point1, out Vector3 point2, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, _overlaps, layerMask, QueryTriggerInteraction.Collide);

        bool isCorrected = false;
        Quaternion rotation = _body.transform.rotation;
        for (int i = 0; i < count; i++)
        {
            Collider other = _overlaps[i];
            CourseGimmickBase gimmick = other.GetComponentInParent<CourseGimmickBase>();
            if (gimmick != null && (!gimmick.IsSolid || !gimmick.isActiveAndEnabled))
            {
                continue;
            }

            bool isOverlapping = Physics.ComputePenetration(
                _body, position, rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out float depth);

            if (!isOverlapping)
            {
                continue;
            }

            position += direction * (depth + SkinWidth);
            _flightController.ApplyContactCorrection(Vector3.zero, direction);
            isCorrected = true;
        }

        return isCorrected;
    }

    // 最後の位置で重なっている、すり抜けるギミックも「触れている」に入れる（エリアの中にいる間など）。
    private void CollectOverlaps(Vector3 position, Vector3 moveDirection, int layerMask)
    {
        GetCapsule(position, out Vector3 point1, out Vector3 point2, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, _overlaps, layerMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider other = _overlaps[i];
            CourseGimmickBase gimmick = other.GetComponentInParent<CourseGimmickBase>();
            if (gimmick == null || gimmick.IsSolid || !gimmick.isActiveAndEnabled || _touched.ContainsKey(gimmick))
            {
                continue;
            }

            _touched[gimmick] = CreateOverlapContact(other, position, moveDirection);
        }
    }

    // 前のフレームから触れ続けているものにはStay、新しく触れたものにはEnter、触れなくなったもの（無効になったものも）にはExitを呼ぶ。
    private void DispatchEnterStayExit(float deltaTime)
    {
        _buffer.Clear();
        _buffer.AddRange(_inside);
        foreach (CourseGimmickBase gimmick in _buffer)
        {
            // Destroyされたものには呼べないので、外すだけにする。
            if (gimmick == null)
            {
                _inside.Remove(gimmick);
                continue;
            }

            if (!_touched.ContainsKey(gimmick) || !gimmick.isActiveAndEnabled)
            {
                _inside.Remove(gimmick);
                gimmick.OnRacerExit(_racer);
            }
        }

        foreach (KeyValuePair<CourseGimmickBase, GimmickContact> pair in _touched)
        {
            if (_inside.Add(pair.Key))
            {
                pair.Key.OnRacerEnter(pair.Value);
            }
            else
            {
                pair.Key.OnRacerStay(pair.Value, deltaTime);
            }
        }
    }

    // 重なっているときの接触の情報。体の中心に一番近いColliderの表面の点と、そこから体の中心への向き（中心が中にあれば向きは0）。
    private GimmickContact CreateOverlapContact(Collider other, Vector3 position, Vector3 moveDirection)
    {
        Vector3 center = GetCenter(position);
        Vector3 point = GetClosestPoint(other, center);
        Vector3 offset = center - point;
        Vector3 normal = offset.sqrMagnitude > MinMoveDistance * MinMoveDistance ? offset.normalized : Vector3.zero;
        return new GimmickContact(_racer, other, normal == Vector3.zero ? center : point, normal, moveDirection);
    }

    // Collider.ClosestPointは、凸でないMeshColliderなどには使えないので、そのときはBoundsで近似する。
    private static Vector3 GetClosestPoint(Collider other, Vector3 position)
    {
        bool isSupported = other is BoxCollider || other is SphereCollider || other is CapsuleCollider || (other is MeshCollider mesh && mesh.convex);
        return isSupported ? other.ClosestPoint(position) : other.bounds.ClosestPoint(position);
    }

    // CapsuleCastNonAllocの結果は距離の順ではないので、近い順に並べる（数が少ないので挿入ソート）。
    private void SortHitsByDistance(int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            int j = i - 1;
            while (j >= 0 && _hits[j].distance > hit.distance)
            {
                _hits[j + 1] = _hits[j];
                j--;
            }

            _hits[j + 1] = hit;
        }
    }

    // positionに体があるときの、カプセルの中心（ワールド座標）。
    private Vector3 GetCenter(Vector3 position)
    {
        Transform bodyTransform = _body.transform;
        Vector3 scale = GetAbsoluteScale(bodyTransform);
        return position + bodyTransform.rotation * Vector3.Scale(_body.center, scale);
    }

    // positionに体があるときの、カプセルの両端の球の中心と半径（ワールド座標）。
    private void GetCapsule(Vector3 position, out Vector3 point1, out Vector3 point2, out float radius)
    {
        Transform bodyTransform = _body.transform;
        Vector3 scale = GetAbsoluteScale(bodyTransform);

        Vector3 axis;
        float axisScale;
        float radiusScale;
        switch (_body.direction)
        {
            case 0:
                axis = bodyTransform.right;
                axisScale = scale.x;
                radiusScale = Mathf.Max(scale.y, scale.z);
                break;

            case 2:
                axis = bodyTransform.forward;
                axisScale = scale.z;
                radiusScale = Mathf.Max(scale.x, scale.y);
                break;

            default:
                axis = bodyTransform.up;
                axisScale = scale.y;
                radiusScale = Mathf.Max(scale.x, scale.z);
                break;
        }

        radius = _body.radius * radiusScale;
        float halfLength = Mathf.Max(0f, _body.height * axisScale * 0.5f - radius);
        Vector3 center = GetCenter(position);
        point1 = center + axis * halfLength;
        point2 = center - axis * halfLength;
    }

    private static Vector3 GetAbsoluteScale(Transform target)
    {
        Vector3 scale = target.lossyScale;
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}
