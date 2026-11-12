using UnityEngine;

// Playerの窓口。処理の中身はRacerManagerBaseと共通で、入力をRaceInputProvider（キーボード／トラッキング）から受け取るところだけが違う。
// RaceManagerからInitialize/PlaceAt/Tickを呼ばれる。カメラ・UIなど、Playerだけが持つものはこの型で参照する。
public class PlayerManager : RacerManagerBase
{
    [Header("入力")]
    [SerializeField] private RaceInputProvider _inputProvider;

    [Header("妨害弾")]
    [Tooltip("妨害弾の発射台（Qキーで撃つ）。未設定なら撃たない")]
    [SerializeField] private JammerLauncher _jammerLauncher;

    // Playerは視界反転を画面で見せる（ViewFlipPresentation）ので、入力は反転しない。
    protected override bool InvertsMoveOnViewFlip => false;

    protected override void InitializeInput()
    {
        _inputProvider.Initialize();
    }

    // RaceInputProviderは、トラッキング入力の初期化などで入力元が差し替わるため、毎Tick読み直す。
    // GetInputはゴール前のTickごとに1回呼ばれるので、妨害弾の更新もここで行う（スタン中は撃てない）。
    protected override IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data)
    {
        if (_jammerLauncher != null)
        {
            _jammerLauncher.Tick(deltaTime, data, Racers, CourseGuide, !IsStunned);
        }

        return _inputProvider.Current;
    }
}
