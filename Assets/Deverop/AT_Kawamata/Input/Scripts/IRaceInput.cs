// 入力元（トラッキング／キーボード／ゲームパッド）を隠蔽する共通インターフェース。
// PlayerManagerはこのインターフェースだけを参照し、入力元を意識しない。
// ブースト・シールドは「押した瞬間（Pressed）」で開始し、「押し続けている間（Held）」だけ続く。
public interface IRaceInput
{
    // -1（左）～1（右）
    float Horizontal { get; }

    // このフレームで押されたか（ブーストの開始）
    bool BoostPressed { get; }

    // 押し続けているか（ブーストの継続。離すと終了する）
    bool BoostHeld { get; }

    // このフレームで押されたか（シールドの開始）
    bool ShieldPressed { get; }

    // 押し続けているか（シールドの継続。離すと終了する）
    bool ShieldHeld { get; }

    // このフレームで押されたか（ショートカット用。今回はデバッグログ出力のみ。
    // トラッキング操作での実装は別途行う予定のため、暫定的にキーボードへ割り当てている）
    bool ShortcutPressed { get; }
}
