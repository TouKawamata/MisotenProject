using UnityEngine;

// 現在使用する入力元を管理するセーム。
// 将来トラッキング／ゲームパッド入力を追加する際も、PlayerManager側は無改修で済む。
// InitializeはPlayerManagerから呼び出される。
public class RaceInputProvider : MonoBehaviour
{
    public IRaceInput Current { get; private set; }

    public void Initialize()
    {
        Current = new KeyboardRaceInput();
    }
}
