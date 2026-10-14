using UnityEngine;

// 現在使用する入力元を管理するセーム。
// 将来トラッキング／ゲームパッド入力を追加する際も、PlayerManager側は無改修で済む。
// InitializeはPlayerManagerから呼び出される。
public class RaceInputProvider : MonoBehaviour
{
    public IRaceInput Current { get; private set; }

    public void Initialize()
    {
        // 通常はここでKeyboardRaceInputが入り、その後TrackingRaceInputBootstrapper.AwakeのSetInputSourceで差し替えられる。
        // SetInputSourceが先に呼ばれていた場合は、その入力元を上書きしない。
        if (Current == null)
        {
            Current = new KeyboardRaceInput();
        }
    }

    // 他コンポーネント（トラッキング入力の初期化処理など）から入力元を差し替えるための窓口。
    public void SetInputSource(IRaceInput input)
    {
        Current = input;
    }
}
