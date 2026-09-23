using UnityEngine;
using VContainer;
using VContainer.Unity;

// ゲームシーンのInitialize/Tickの呼び出し元（VContainer試験導入）。
// 現在はPlayerManagerをエントリーポイントとして登録し、IInitializable.Initialize / ITickable.Tickを駆動させる。
public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private PlayerManager _playerManager;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint(_ => _playerManager, Lifetime.Singleton);
    }
}
