using TMPro;
using UnityEngine;

public sealed class ResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text _rank;
    [SerializeField] private TMP_Text _time;
    [SerializeField] private TMP_Text _boostDistance;
    [SerializeField] private TMP_Text _speedRingCount;
    [SerializeField] private TMP_Text _hitCount;

    private void OnEnable()
    {
        PlayerResultData result = RaceResultStore.Latest;
        _rank.text = result == null ? "--" : FormatRank(result.Rank);
        _time.text = result == null ? "--:--.--" : FormatTime(result.FinishTime);
        _boostDistance.text = result == null ? "-- m" : $"{Mathf.FloorToInt(result.BoostDistance)} m";
        _speedRingCount.text = result == null ? "--" : result.SpeedRingCount.ToString();
        _hitCount.text = result == null ? "--" : result.HitCount.ToString();
    }

    private static string FormatRank(int rank)
    {
        int lastTwo = rank % 100;
        string suffix = lastTwo >= 11 && lastTwo <= 13 ? "th" :
            rank % 10 == 1 ? "st" : rank % 10 == 2 ? "nd" : rank % 10 == 3 ? "rd" : "th";
        return $"{rank}<size=45%>{suffix}</size>";
    }

    public static string FormatTime(float seconds)
    {
        int cs = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 100f);
        return $"{cs / 6000:00}:{cs / 100 % 60:00}.{cs % 100:00}";
    }
}
