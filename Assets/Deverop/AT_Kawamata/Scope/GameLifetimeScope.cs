using UnityEngine;
using VContainer;
using VContainer.Unity;

// ゲームシーンのInitialize/Start/Tickの呼び出し元（VContainer試験導入）。
// RaceManagerをエントリーポイントとして登録し、PlayerなどのRacerはRaceManager経由で駆動させる。
// CountdownUIはRaceManagerのイベントを購読するため、Initialize/Disposeのためだけに登録する。
public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private RaceManager _raceManager;
    [SerializeField] private CountdownUI _countdownUI;

    protected override void Configure(IContainerBuilder builder)
    {
        // 未設定のまま登録するとVContainer内部でNullReferenceExceptionになり原因が分かりにくいため、ここで弾く。
        if (_raceManager == null)
        {
            Debug.LogError("GameLifetimeScope: Race Manager が未設定です", this);
        }
        else
        {
            builder.RegisterEntryPoint(_ => _raceManager, Lifetime.Singleton);
        }

        if (_countdownUI == null)
        {
            Debug.LogError("GameLifetimeScope: Countdown UI が未設定です", this);
        }
        else
        {
            builder.RegisterEntryPoint(_ => _countdownUI, Lifetime.Singleton);
        }
    }
}
