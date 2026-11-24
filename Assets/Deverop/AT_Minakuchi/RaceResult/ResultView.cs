using TMPro;
using UnityEngine;

public sealed class ResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text _rank;
    [SerializeField] private TMP_Text _time;
    [SerializeField] private TMP_Text _timeRank;
    [SerializeField, Min(0f)] private float _sRankMaxSeconds = 90f;
    [SerializeField, Min(0f)] private float _aRankMaxSeconds = 120f;
    [SerializeField] private TMP_Text _boostDistance;
    [SerializeField] private TMP_Text _speedRingCount;
    [SerializeField] private TMP_Text _hitCount;

    private void OnEnable()
    {
        PlayerResultData result = RaceResultStore.Latest;
        _rank.text = result == null ? "--" : FormatRank(result.Rank);
        _time.text = result == null ? "--:--.--" : FormatTime(result.FinishTime);
        UpdateTimeRank(result);
        _boostDistance.text = result == null ? "-- m" : $"{Mathf.FloorToInt(result.BoostDistance)} m";
        _speedRingCount.text = result == null ? "--" : result.SpeedRingCount.ToString();
        _hitCount.text = result == null ? "--" : result.HitCount.ToString();
    }

    private void UpdateTimeRank(PlayerResultData result)
    {
        if (result == null)
        {
            _timeRank.text = "–";
            _timeRank.colorGradient = new VertexGradient(new Color32(180, 186, 196, 255));
            return;
        }

        if (result.FinishTime <= _sRankMaxSeconds)
            SetTimeRank("S", new Color32(255, 246, 170, 255), new Color32(242, 174, 35, 255));
        else if (result.FinishTime <= _aRankMaxSeconds)
            SetTimeRank("A", new Color32(255, 151, 144, 255), new Color32(244, 58, 53, 255));
        else
            SetTimeRank("B", new Color32(145, 220, 255, 255), new Color32(51, 134, 255, 255));
    }

    private void SetTimeRank(string rank, Color top, Color bottom)
    {
        _timeRank.text = rank;
        _timeRank.colorGradient = new VertexGradient(top, top, bottom, bottom);
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
