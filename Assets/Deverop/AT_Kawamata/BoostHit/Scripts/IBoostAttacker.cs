using UnityEngine;

// ブーストで他のレーサーに当たる側の窓口。BoostHitDetectorがこれを見て、ブースト中なら相手のIBoostHitReceiverを呼ぶ。
public interface IBoostAttacker
{
    Transform Transform { get; }

    bool IsBoosting { get; }

    // 機首の向きの速さ（m/s）。当てた瞬間の値をBoostHitInfoで相手に渡し、シールドで防がれたときの押し出し距離に使う。
    float ForwardSpeed { get; }
}
