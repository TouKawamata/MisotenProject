using UnityEngine;

// 入力元（トラッキング／キーボード／ゲームパッド／CPU）を隠蔽する共通インターフェース。
// PlayerManager・CPUManagerはこのインターフェースだけを参照し、入力元を意識しない。
// ブースト・シールドは「押した瞬間（Pressed）」で開始し、「押し続けている間（Held）」だけ続く。
// ブーストは、押し続けている間にゲージが十分戻れば、押し直さなくても再開する（RacerManagerBase）。
// ブースト・シールドは移動と同時に入力してよい。
public interface IRaceInput
{
    // 移動の入力。x：右が正・左が負、y：上が正・下が負。ニュートラルはVector2.zero。
    // 大きさは0～1に制限した値を返すこと（斜めでも1を超えない。Vector2.ClampMagnitudeなど）。
    // 向き（中間の角度）と入力の強さは、そのまま残して渡す（8方向への丸めは飛行制御側で行う）。
    // 受け取る側（RacerManagerBase・PlayerFlightController）は、この前提で扱い、正規化し直さない。
    Vector2 Move { get; }

    // このフレームで押されたか（ブーストの開始）
    bool BoostPressed { get; }

    // 押し続けているか（ブーストの継続。離すと終了する）
    bool BoostHeld { get; }

    // このフレームで押されたか（シールドの開始）
    bool ShieldPressed { get; }

    // 押し続けているか（シールドの継続。離すと終了する）
    bool ShieldHeld { get; }

    // このフレームで押されたか（妨害弾の発射。JammerLauncherを持つレーサーだけが撃つ）。
    // 撃たない入力（トラッキング入力・CPUの判断など）は実装しなくてよい（常にfalse）。CPUに撃たせるときは、判断の側でtrueを返す。
    bool JammerPressed => false;
}
