using UnityEngine;

// RaceManagerがレースの参加者（Player／将来のCPU）に求める窓口。
// 順位などの情報はTickの引数で上から渡し、Racer側からRaceManagerを参照しない。
public interface IRacer
{
    Transform Transform { get; }

    // 以下の状態は、RaceManagerがTickの後にRacerDataへ写す。他のレーサー・UIはRacerData経由で読む。
    bool IsBoosting { get; }

    bool IsShielding { get; }

    bool IsStunned { get; }

    // 配置＋速度・向きなどの内部状態のリセット。
    void PlaceAt(Vector3 position, Quaternion rotation);

    void Tick(float deltaTime, IReadOnlyRacerData data);
}
