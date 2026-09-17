// 入力元（トラッキング／キーボード／ゲームパッド）を隠蔽する共通インターフェース。
// PlayerFlightControllerはこのインターフェースだけを参照し、入力元を意識しない。
public interface IRaceInput
{
    // -1（左）～1（右）
    float Horizontal { get; }

    // -1（下）～1（上）。トンネル方式（パターンE）検証専用
    float Vertical { get; }

    // このフレームで押されたか
    bool BoostPressed { get; }

    // このフレームで押されたか（デバッグログ出力のみ、状態管理は持たない）
    bool ShieldPressed { get; }

    // このフレームで押されたか（今回未使用だがインターフェースとして保持）
    bool MainActionPressed { get; }
}
