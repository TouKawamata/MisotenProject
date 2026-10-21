using TMPro;
using UnityEngine;

public class RankView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RaceManager _raceManager;

    [Header("UI")]
    [SerializeField] private TMP_Text _rankText;

    private int _previousRank = -1;
    private int _previousRacerCount = -1;

    private void LateUpdate()
    {
        // RaceManagerの初期化前はPlayerDataがnullになるため、
        // 初期化されるまでは更新しない。
        if (_raceManager == null || _raceManager.PlayerData == null)
        {
            return;
        }

        int currentRank = _raceManager.PlayerData.CurrentRank;
        int racerCount = _raceManager.Racers.Count;

        // 値が変わっていないなら、
        // 不要な文字列生成とUI更新を行わない。
        if (currentRank == _previousRank &&
            racerCount == _previousRacerCount)
        {
            return;
        }

        _previousRank = currentRank;
        _previousRacerCount = racerCount;

        UpdateRankText(currentRank, racerCount);
    }

    private void UpdateRankText(int rank, int racerCount)
    {
        _rankText.text = $"{rank} / {racerCount}";
    }
}