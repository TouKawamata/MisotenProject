// RacerDataの読み取り専用の窓口。RaceManager以外（Racer・UI・Debug HUDなど）にはこちらで渡し、誤って書き換えられないようにする。
public interface IReadOnlyRacerData
{
    int RacerId { get; }

    bool IsPlayer { get; }

    // 1始まり。
    int CurrentRank { get; }

    // コース上の距離（m）。CourseProgressの計算元。
    float DistanceOnCourse { get; }

    // 0＝コース始点、1＝ゴール。
    float CourseProgress { get; }

    bool IsFinished { get; }

    // GOからの経過時間（秒）。IsFinishedがfalseの間は0。
    float FinishTime { get; }
}
