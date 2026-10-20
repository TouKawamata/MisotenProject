using System;
using System.Collections.Generic;
using UnityEngine;

// CPUの窓口。処理の中身はRacerManagerBase（PlayerManagerと共通）で、入力をCPUの入力（判断）から受け取るところだけが違う。
// 入力は、Inspectorの「Input Source」（IRaceInputを実装したコンポーネント）か、SetInputSource（コードから渡す）で設定する。
// 入力がICpuRaceInputなら、毎Tickの最初にUpdateInputを呼んでから値を読む。どちらも無ければ、入力なし（コースに沿ってまっすぐ飛ぶ）で走る。
// RaceManagerの「CPU」の欄に入れると、RaceManagerがSetRacers・Initializeを呼び、Playerと同じくPlaceAt・Tickで動かす。
public class CPUManager : RacerManagerBase
{
    private static readonly IRaceInput NoInput = new NoRaceInput();

    [Header("入力")]
    [Tooltip("CPUの入力（判断）。IRaceInput（できればICpuRaceInput）を実装したコンポーネントを入れる。未設定で、SetInputSourceも呼ばれなければ、入力なしで走る")]
    [SerializeField] private MonoBehaviour _inputSource;

    private IRaceInput _input;

    // 参加者全員（自分も含む）のRacerData。CPUの入力が周りのレーサーを見るのに使う。RaceManagerがInitializeの前に渡す。
    public IReadOnlyList<IReadOnlyRacerData> Racers { get; private set; } = Array.Empty<IReadOnlyRacerData>();

    // コードから入力を渡す（MonoBehaviourでない入力の場合）。Inspectorの設定より優先する。いつ呼んでも、次のTickから使う。
    public void SetInputSource(IRaceInput input)
    {
        _input = input;
    }

    public void SetRacers(IReadOnlyList<IReadOnlyRacerData> racers)
    {
        Racers = racers ?? Array.Empty<IReadOnlyRacerData>();
    }

    protected override void InitializeInput()
    {
        // SetInputSourceで先に渡されていれば、そちらを使う。
        if (_input == null && _inputSource != null)
        {
            if (_inputSource is IRaceInput input)
            {
                _input = input;
            }
            else
            {
                Debug.LogError($"CPUManager: {name} の Input Source（{_inputSource.GetType().Name}）はIRaceInputを実装していません", this);
            }
        }

        if (_input == null)
        {
            Debug.LogWarning($"CPUManager: {name} の入力が未設定のため、入力なし（コースに沿ってまっすぐ飛ぶ）で走ります", this);
        }
    }

    protected override IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data)
    {
        IRaceInput input = _input ?? NoInput;
        if (input is ICpuRaceInput cpuInput)
        {
            cpuInput.UpdateInput(this, deltaTime, data);
        }

        return input;
    }

    // 入力なし。左右もボタンも押さない（コース中央への補正で、コースに沿ってまっすぐ飛ぶ）。
    private class NoRaceInput : IRaceInput
    {
        public float Horizontal => 0f;

        public bool BoostPressed => false;

        public bool BoostHeld => false;

        public bool ShieldPressed => false;

        public bool ShieldHeld => false;

        public bool ShortcutPressed => false;
    }
}
