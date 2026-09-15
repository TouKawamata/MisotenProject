// 入力元（トラッキング／キーボード／ゲームパッド）を隠蔽する共通インターフェース。
// PlayerFlightControllerはこのインターフェースだけを参照し、入力元を意識しない。
public interface IRaceInput
{
    // -1（左）～1（右）
    float Horizontal { get; }

    // このフレームで押されたか
    bool BoostPressed { get; }

    // このフレームで押されたか（今回未使用だがインターフェースとして保持）
    bool MainActionPressed { get; }
}
