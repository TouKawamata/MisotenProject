using UnityEngine;

// Playerの窓口。処理の中身はRacerManagerBaseと共通で、入力をRaceInputProvider（キーボード／トラッキング）から受け取るところと、
// 先頭から離されているほどブーストの消費を遅くする補正（BoostCatchUpProfile）・妨害弾を当てたときのゲージの回復
// （難易度ごと。CpuDifficultyProfile）があるところだけが違う。
// RaceManagerからInitialize/PlaceAt/Tickを呼ばれる。カメラ・UIなど、Playerだけが持つものはこの型で参照する。
public class PlayerManager : RacerManagerBase
{
    [Header("入力")]
    [SerializeField] private RaceInputProvider _inputProvider;

    [Header("ブーストの補正")]
    [Tooltip("先頭から離されているほど、ブーストの消費を遅くする補正。未設定なら補正なし")]
    [SerializeField] private BoostCatchUpProfile _boostCatchUpProfile;

    // Playerは視界反転を画面で見せる（ViewFlipPresentation）ので、入力は反転しない。
    protected override bool InvertsMoveOnViewFlip => false;

    protected override void InitializeInput()
    {
        _inputProvider.Initialize();
    }

    // RaceInputProviderは、トラッキング入力の初期化などで入力元が差し替わるため、毎Tick読み直す。
    protected override IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data)
    {
        return _inputProvider.Current;
    }

    protected override float GetBoostConsumptionMultiplier(IReadOnlyRacerData data)
    {
        return _boostCatchUpProfile != null ? _boostCatchUpProfile.EvaluateConsumptionMultiplier(GetGapToLeader(data)) : 1f;
    }

    // 相手が効果を受けたときだけ（防がれた・無敵中だったときは除く）、難易度の設定に応じてブーストゲージを回復する。
    // 難易度はCpuDifficultyController（全CPUの一括の設定）を見る。シーンに無ければ回復しない。
    protected override void OnJammerLanded(EJammerHitResult result)
    {
        CpuDifficultyController controller = CpuDifficultyController.Current;
        if (result != EJammerHitResult.Hit || controller == null || controller.Difficulty == null)
        {
            return;
        }

        if (controller.Difficulty.PlayerGainsBoostOnJammerHit)
        {
            AddBoostGauge(controller.Difficulty.PlayerBoostGainOnJammerHit);
        }
    }

    // 一番前にいるレーサーとの、コース上の距離の差（m）。自分が先頭なら0。
    // ゴールしたレーサーの距離はゴールした時点のまま止まるので、その後はゴール地点までの差になる。
    private float GetGapToLeader(IReadOnlyRacerData data)
    {
        float gap = 0f;
        foreach (IReadOnlyRacerData racer in Racers)
        {
            gap = Mathf.Max(gap, racer.DistanceOnCourse - data.DistanceOnCourse);
        }

        return gap;
    }
}
