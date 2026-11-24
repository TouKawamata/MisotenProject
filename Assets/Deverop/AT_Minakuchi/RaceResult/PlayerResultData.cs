// ゴール時点の値だけを保持する、変更不可の結果。
public sealed class PlayerResultData
{
    public int Rank { get; }
    public float FinishTime { get; }
    public float BoostDistance { get; }
    public int SpeedRingCount { get; }
    public int HitCount { get; }

    public PlayerResultData(int rank, float finishTime, float boostDistance, int speedRingCount, int hitCount)
    {
        Rank = rank;
        FinishTime = finishTime;
        BoostDistance = boostDistance;
        SpeedRingCount = speedRingCount;
        HitCount = hitCount;
    }
}
