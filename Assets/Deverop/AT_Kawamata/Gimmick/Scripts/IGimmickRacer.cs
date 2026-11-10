using UnityEngine;

// コースギミック（CourseGimmickBase）・妨害弾が、当たったレーサーを読む・効果を反映するための窓口。RacerManagerBaseが実装する。
// ギミック側は、ここに無いレーサーの中身（BoostController・PlayerFlightControllerなど）を直接触らない。
// 足りない操作があれば、ギミックごとにRacerManagerBaseへ専用の処理を足すのではなく、ここに汎用の操作として足す。
public interface IGimmickRacer
{
    // RaceManagerが持つ、このレーサーの情報（RacerId・IsPlayer・順位・コース上の距離など）。最初のTickの前はnull。
    IReadOnlyRacerData Data { get; }

    Transform Transform { get; }

    Vector3 Velocity { get; }

    // 機首の向きの速さ（m/s）。
    float ForwardSpeed { get; }

    bool IsBoosting { get; }

    bool IsShielding { get; }

    // スタン中（入力を受け付けない間）。
    bool IsStunned { get; }

    // スタン中と、その後・シールドで防いだ後の無敵時間。
    bool IsInvincible { get; }

    // このレーサーのコースへの問い合わせ口（分岐のあるコースでは、自分のルート）。
    ICourseGuide CourseGuide { get; }

    // 時間付きの効果（減速・左右移動が遅い・ブースト禁止など）。
    IRacerEffects Effects { get; }

    // 機首の向きへdistance（m）をduration秒かけて押し出す。負なら後ろへ下げる。速度ではなく位置に足すので、最高速度に関係なく動く。
    // 初速（m/s）で決めたいときは、LateralPush.GetDistanceForInitialSpeed(初速, 秒数)で距離に直す。
    void ApplyForwardPush(float distance, float duration);

    // コースの左右方向へdistance（m）をduration秒かけて押し出す。directionは左右どちら向きかだけを使う（決まらなければコースの中央側）。
    void ApplyLateralPush(Vector3 direction, float distance, float duration);

    // スタンさせる（入力を受け付けない。ブーストも止める）。解けた後は、BoostHitProfileの無敵時間が続く。
    void Stun(float duration);

    // ブーストを止める。lockDuration：再発動できない秒数。
    void StopBoost(float lockDuration = 0f);

    // その場でぴたっと止める（前進・上下左右の速度を0にする）。
    void StopFlight();

    // シールドを張っていれば、1回防いでシールドを消す（防いだ後の無敵時間とShieldBlockedも出す）。張っていなければfalse。
    bool TryBlockWithShield();

    // ギミックが効果を出したことを、レーサーの演出（GimmickPresentationBase）へ知らせる。CourseGimmickBase.NotifyActivatedから呼ばれる。
    void NotifyGimmickActivated(CourseGimmickBase gimmick);
}
