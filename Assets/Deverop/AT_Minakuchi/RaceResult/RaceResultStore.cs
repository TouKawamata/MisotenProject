using UnityEngine;

public static class RaceResultStore
{
    public static PlayerResultData Latest { get; private set; }

    public static void Clear() => Latest = null;
    public static void Save(PlayerResultData result) => Latest = result;

    // Domain Reloadを無効にして再生した場合も前回の値を残さない。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Clear();
}
