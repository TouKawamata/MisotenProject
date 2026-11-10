using UnityEngine;

// レーサーとコースギミックの接触の情報。RacerGimmickSensorが作って、CourseGimmickBaseへ渡す。
public readonly struct GimmickContact
{
    public GimmickContact(IGimmickRacer racer, Collider collider, Vector3 point, Vector3 normal, Vector3 moveDirection)
    {
        Racer = racer;
        Collider = collider;
        Point = point;
        Normal = normal;
        MoveDirection = moveDirection;
        HitAngle = GetHitAngle(moveDirection, normal);
    }

    // 当たったレーサー。読む・効果を反映するのはここから。
    public IGimmickRacer Racer { get; }

    // 当たったギミックのCollider（ギミックが複数のColliderを持つとき、どれに当たったか）。
    public Collider Collider { get; }

    // 当たった位置（ワールド座標）。
    public Vector3 Point { get; }

    // 当たった面の向き（ギミックの外へ向く）。エリアの中に入っていて決まらないときはVector3.zero。
    public Vector3 Normal { get; }

    // このフレームのレーサーの移動の向き（正規化済み。動いていなければVector3.zero）。
    public Vector3 MoveDirection { get; }

    // 移動の向きと面のなす角（度）。0＝面に平行（かすめた）、90＝真正面。面から離れる向き・面の向きが決まらないときは0。
    public float HitAngle { get; }

    private static float GetHitAngle(Vector3 direction, Vector3 normal)
    {
        return Mathf.Asin(Mathf.Clamp01(-Vector3.Dot(direction, normal))) * Mathf.Rad2Deg;
    }
}
