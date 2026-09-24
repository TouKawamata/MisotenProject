// レース中に変わる参加者ごとの状態。書き込みはRaceManager（RaceProgressTracker）だけが行い、
// 外へはIReadOnlyRacerDataとして渡す。
public class RacerData : IReadOnlyRacerData
{
    public RacerData(int racerId, bool isPlayer)
    {
        RacerId = racerId;
        IsPlayer = isPlayer;
    }

    public int RacerId { get; }

    public bool IsPlayer { get; }

    public int CurrentRank { get; set; }

    public float DistanceOnCourse { get; set; }

    public float CourseProgress { get; set; }

    public bool IsFinished { get; set; }

    public float FinishTime { get; set; }

    public void ResetProgress()
    {
        CurrentRank = 0;
        DistanceOnCourse = 0f;
        CourseProgress = 0f;
        IsFinished = false;
        FinishTime = 0f;
    }
}
