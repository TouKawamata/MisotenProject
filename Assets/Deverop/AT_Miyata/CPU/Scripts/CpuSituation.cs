using System.Collections.Generic;
using UnityEngine;

// 自分から見た、他のレーサー1人の様子（コースの向きを基準にした値）。
public readonly struct CpuRacerView
{
    public CpuRacerView(IReadOnlyRacerData data, float forwardGap, float lateral, float lateralGap, float relativeSpeed)
    {
        Data = data;
        ForwardGap = forwardGap;
        Lateral = lateral;
        LateralGap = lateralGap;
        RelativeSpeed = relativeSpeed;
    }

    public IReadOnlyRacerData Data { get; }

    public int RacerId => Data.RacerId;

    // コース上の前後の差（m）。＋＝相手が前、－＝相手が後ろ。
    public float ForwardGap { get; }

    // 相手の、コース中央からの横ずれ（m。右が＋）。
    public float Lateral { get; }

    // 相手の横ずれ－自分の横ずれ（m）。＋＝相手が右。
    public float LateralGap { get; }

    // 前後の差が広がる速さ（m/s）。＋＝相手の方が速い（前の相手なら離れていく、後ろの相手なら近づいてくる）。
    public float RelativeSpeed { get; }

    public bool IsBoosting => Data.IsBoosting;

    public bool IsShielding => Data.IsShielding;

    public bool IsStunned => Data.IsStunned;
}

// CPUの判断に使う、そのTickの状況。CpuPerceptionが毎Tick書き直し、各Brainは読むだけ。
public class CpuSituation
{
    private readonly List<CpuRacerView> _others = new List<CpuRacerView>();

    public CPUManager Owner { get; set; }

    public IReadOnlyRacerData Self { get; set; }

    public float DeltaTime { get; set; }

    // 自分の、コース中央からの横ずれ（m。右が＋）と、横の速さ（m/s）。
    public float Lateral { get; set; }

    public float LateralVelocity { get; set; }

    public float TunnelRadius { get; set; }

    // 今いる場所のコースの中心と右方向（Gizmosの表示用）。
    public Vector3 CourseCenter { get; set; }

    public Vector3 CourseRight { get; set; }

    // 少し先（難易度のCurveCheckDistance）のコースが、今の向きからどれだけ曲がっているか（度）。
    public float CurveAngleAhead { get; set; }

    // Playerとの前後の差（m）。＋＝Playerが前。HasPlayerがfalseなら0。
    public bool HasPlayer { get; set; }

    public float GapToPlayer { get; set; }

    // 自分とゴール済みのレーサーを除いた、他のレーサー全員。
    public List<CpuRacerView> Others => _others;

    public bool TryGetRacer(int racerId, out CpuRacerView view)
    {
        foreach (CpuRacerView other in _others)
        {
            if (other.RacerId == racerId)
            {
                view = other;
                return true;
            }
        }

        view = default;
        return false;
    }
}
