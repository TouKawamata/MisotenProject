using UnityEngine;

// Playerの窓口。処理の中身はRacerManagerBaseと共通で、入力をRaceInputProvider（キーボード／トラッキング）から受け取るところだけが違う。
// RaceManagerからInitialize/PlaceAt/Tickを呼ばれる。カメラ・UIなど、Playerだけが持つものはこの型で参照する。
public class PlayerManager : RacerManagerBase
{
    [Header("入力")]
    [SerializeField] private RaceInputProvider _inputProvider;

    protected override void InitializeInput()
    {
        _inputProvider.Initialize();
    }

    // RaceInputProviderは、トラッキング入力の初期化などで入力元が差し替わるため、毎Tick読み直す。
    protected override IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data)
    {
        return _inputProvider.Current;
    }
}
