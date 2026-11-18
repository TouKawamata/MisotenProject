using System.Collections.Generic;
using UnityEngine;

// 自分から見た、他のレーサー1人の様子（コースの向きを基準にした値）。
// 横位置は、トンネルの断面での位置（x＝右、y＝上、m）。移動が上下左右の平行移動になったため、2次元で持つ。
public readonly struct CpuRacerView
{
    public CpuRacerView(IReadOnlyRacerData data, float forwardGap, Vector2 offset, Vector2 offsetGap, float relativeSpeed)
    {
        Data = data;
        ForwardGap = forwardGap;
        Offset = offset;
        OffsetGap = offsetGap;
        RelativeSpeed = relativeSpeed;
    }

    public IReadOnlyRacerData Data { get; }

    public int RacerId => Data.RacerId;

    // コース上の前後の差（m）。＋＝相手が前、－＝相手が後ろ。
    public float ForwardGap { get; }

    // 相手の、コース中央からのずれ（m。x＝右、y＝上）。
    public Vector2 Offset { get; }

    // 相手のずれ－自分のずれ（m）。大きさが、断面で見た相手との距離。
    public Vector2 OffsetGap { get; }

    // 前後の差が広がる速さ（m/s）。＋＝相手の方が速い（前の相手なら離れていく、後ろの相手なら近づいてくる）。
    public float RelativeSpeed { get; }

    public bool IsBoosting => Data.IsBoosting;

    public bool IsShielding => Data.IsShielding;

    public bool IsStunned => Data.IsStunned;
}

// CPUから見た、前方のコースギミック1つの様子。
public readonly struct CpuGimmickView
{
    public CpuGimmickView(CourseGimmickBase gimmick, ECpuGimmickKind kind, float forwardDistance, Vector3 courseCenter, Vector3 right, Vector3 up, Vector2 offset)
    {
        Gimmick = gimmick;
        Kind = kind;
        ForwardDistance = forwardDistance;
        CourseCenter = courseCenter;
        Right = right;
        Up = up;
        Offset = offset;
    }

    public CourseGimmickBase Gimmick { get; }

    public ECpuGimmickKind Kind { get; }

    // 自分からギミックまでの、コースの前向きの距離（m）。
    public float ForwardDistance { get; }

    // ギミックの場所の、コースの中心・右・上。断面の位置（Vector2）をワールドの位置に直すのに使う。
    public Vector3 CourseCenter { get; }

    public Vector3 Right { get; }

    public Vector3 Up { get; }

    // ギミックの中心の、コース中央からのずれ（m。x＝右、y＝上）。
    public Vector2 Offset { get; }

    public int Id => Gimmick.GetInstanceID();

    // 断面の位置（自分のずれと同じ基準）を、ギミックの場所のワールドの位置に直す。
    public Vector3 ToWorld(Vector2 offset)
    {
        return CourseCenter + Right * offset.x + Up * offset.y;
    }
}

// CPUの判断に使う、そのTickの状況。CpuPerceptionが毎Tick書き直し、各Brainは読むだけ。
public class CpuSituation
{
    private readonly List<CpuRacerView> _others = new List<CpuRacerView>();
    private readonly List<CpuGimmickView> _gimmicks = new List<CpuGimmickView>();

    public CPUManager Owner { get; set; }

    public IReadOnlyRacerData Self { get; set; }

    // 妨害弾の発射台（レーサーのプレハブに付いていれば）。無ければnull。
    public JammerLauncher Launcher { get; set; }

    public float DeltaTime { get; set; }

    // 自分の、コース中央からのずれ（m。x＝右、y＝上）と、断面での速さ（m/s）。
    public Vector2 Offset { get; set; }

    public Vector2 PlanarVelocity { get; set; }

    public float TunnelRadius { get; set; }

    // ギミックとの当たりを見るときの、自分の体の大きさ（m）。
    public float BodyRadius { get; set; }

    // 今いる場所のコースの中心と右・上・前（Gizmosの表示、断面の位置の計算用）。
    public Vector3 CourseCenter { get; set; }

    public Vector3 CourseRight { get; set; }

    public Vector3 CourseUp { get; set; }

    public Vector3 CourseForward { get; set; }

    // 少し先（難易度のCurveCheckDistance）のコースが、今の向きからどれだけ曲がっているか（度）。
    public float CurveAngleAhead { get; set; }

    // Playerとの前後の差（m）。＋＝Playerが前。HasPlayerがfalseなら0。
    public bool HasPlayer { get; set; }

    public float GapToPlayer { get; set; }

    // 自分とゴール済みのレーサーを除いた、他のレーサー全員。
    public List<CpuRacerView> Others => _others;

    // 自分のコース上で、前方（難易度のGimmickLookAhead以内）にある、有効なギミック。近い順。
    public List<CpuGimmickView> Gimmicks => _gimmicks;

    public Vector3 ToWorld(Vector2 offset)
    {
        return CourseCenter + CourseRight * offset.x + CourseUp * offset.y;
    }

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
