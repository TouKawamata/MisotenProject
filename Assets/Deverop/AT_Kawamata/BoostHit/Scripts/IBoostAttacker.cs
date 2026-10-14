using UnityEngine;

// ブーストで他のレーサーに当たる側の窓口。BoostHitDetectorがこれを見て、ブースト中なら相手のIBoostHitReceiverを呼ぶ。
public interface IBoostAttacker
{
    Transform Transform { get; }

    bool IsBoosting { get; }
}
