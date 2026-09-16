using UnityEngine;

// 現在使用する入力元を管理するセーム。
// 将来トラッキング／ゲームパッド入力を追加する際も、PlayerFlightController側は無改修で済む。
public class RaceInputProvider : MonoBehaviour
{
    public IRaceInput Current { get; private set; }

    private void Awake()
    {
        Current = new KeyboardRaceInput();
    }
}
