// 入力元（トラッキング／キーボード／ゲームパッド）を隠蔽する共通インターフェース。
// PlayerFlightControllerはこのインターフェースだけを参照し、入力元を意識しない。
public interface IRaceInput
{
    // -1（左）～1（右）
    float Horizontal { get; }

    // このフレームで押されたか
    bool BoostPressed { get; }

    // このフレームで押されたか（デバッグログ出力のみ、状態管理は持たない）
    bool ShieldPressed { get; }

    // このフレームで押されたか（ショートカット用。今回はデバッグログ出力のみ。
    // トラッキング操作での実装は別途行う予定のため、暫定的にキーボードへ割り当てている）
    bool ShortcutPressed { get; }
}
