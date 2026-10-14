// RaceManagerが管理する参加者1人分。Racer本体・レース中の状態・グリッド位置の組。
public class RacerEntry
{
    public RacerEntry(IRacer racer, RacerData data, int gridIndex)
    {
        Racer = racer;
        Data = data;
        GridIndex = gridIndex;
    }

    public IRacer Racer { get; }

    public RacerData Data { get; }

    public int GridIndex { get; }
}
