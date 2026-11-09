using UnityEngine;

// 移動の方向の決め方。比較検証のために切り替える（FlightTuningProfileで設定。PlayerとCPUで共通）。
public enum EMoveDirectionMode
{
    // 入力の角度を45度刻みの8方向に丸める。
    EightWay,

    // 入力の角度をそのまま使う（斜め45度以外の中間の角度へも動ける）。
    FreeAngle,
}

// 移動の入力（IRaceInput.Move）から、実際に動く方向を決める。速度・衝突の計算は方式によらず共通で、ここだけを切り替える。
// 入力は大きさ0～1に制限済み（IRaceInputの約束）で、デッドゾーン・ぶれの除去も入力側で済んでいる前提。
// どちらの方式でも、大きさ（入力の強さ）はそのまま残し、向きだけを変える。
public static class MoveDirectionResolver
{
    private const float EightWayStepDegrees = 45f;

    public static Vector2 Resolve(Vector2 move, EMoveDirectionMode mode)
    {
        float magnitude = move.magnitude;
        if (magnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        // 念のため1を超えないようにしておく（入力側の約束が守られていれば何もしない）。
        magnitude = Mathf.Min(magnitude, 1f);

        if (mode != EMoveDirectionMode.EightWay)
        {
            return move / move.magnitude * magnitude;
        }

        float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / EightWayStepDegrees) * EightWayStepDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)) * magnitude;
    }
}
