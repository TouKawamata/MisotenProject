using System.Threading;
using Cysharp.Threading.Tasks;

// スタートの演出（Timelineなど）の窓口。RaceManagerが、スタートグリッドへの配置の後・カウントダウンの前に再生し、終わるまで待つ。
// 演出の間、RaceManagerのStateはReadyのまま（RacerのTickは呼ばれないので、Playerは配置された位置から動かない）。
// 実装するMonoBehaviourをRaceManagerのIntroに割り当てる。スキップする場合も、PlayAsyncを終わらせればよい。
public interface IRaceIntro
{
    UniTask PlayAsync(CancellationToken token);
}
