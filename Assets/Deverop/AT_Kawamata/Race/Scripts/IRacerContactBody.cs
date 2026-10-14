using UnityEngine;

// 通常の接触（ブーストなし）で、食い込みを押し戻すための窓口。RacerContactResolverから呼ばれる。
public interface IRacerContactBody
{
    // 食い込みの計算に使う体のCollider（Triggerでもよい）。
    Collider BodyCollider { get; }

    // ワールド座標の速度。横から接触したとき、どちらが押しているかの判定に使う。
    Vector3 Velocity { get; }

    // ブースト中・横ずれ中など、通常の接触を処理しない間はtrue（ブースト接触の処理を優先し、押し出しと打ち消し合わないようにする）。
    bool IgnoresContact { get; }

    // offset（ワールド座標の移動量）だけ位置を直す。
    // blockNormalがゼロでなければ、その逆向き（相手へ向かう向き）の速度を消す（壁に当たったのと同じ）。
    void ApplyContactCorrection(Vector3 offset, Vector3 blockNormal);
}
