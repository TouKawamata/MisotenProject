using UnityEngine;

// RaceManagerがレースの参加者（Player／将来のCPU）に求める窓口。
// 順位などの情報はTickの引数で上から渡し、Racer側からRaceManagerを参照しない。
public interface IRacer
{
    Transform Transform { get; }

    // 配置＋速度・向きなどの内部状態のリセット。
    void PlaceAt(Vector3 position, Quaternion rotation);

    void Tick(float deltaTime, IReadOnlyRacerData data);
}
