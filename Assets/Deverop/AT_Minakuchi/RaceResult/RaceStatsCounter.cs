using UnityEngine;

// GameSceneだけに置く。RaceManagerとRacerDataには書き込まない。
[DisallowMultipleComponent]
public sealed class RaceStatsCounter : MonoBehaviour
{
    [SerializeField] private RaceManager _raceManager;
    [SerializeField] private PlayerManager _player;
    private Vector3 _previousPosition;
    private bool _hasPosition;
    private bool _saved;
    private float _boostDistance;
    private int _speedRingCount;
    private int _hitCount;

    private bool CanCount => !_saved && Time.timeScale > 0f &&
        _raceManager != null && _raceManager.State == ERaceState.Racing &&
        _raceManager.PlayerData != null && !_raceManager.PlayerData.IsFinished;

    private void Awake()
    {
        RaceResultStore.Clear();
        ResultBackdropStore.Clear();
    }

    private void OnEnable()
    {
        if (_raceManager == null) _raceManager = FindFirstObjectByType<RaceManager>();
        if (_player == null) _player = FindFirstObjectByType<PlayerManager>();
        if (_raceManager == null || _player == null)
        {
            Debug.LogError("RaceStatsCounter: RaceManager / PlayerManager が必要です", this);
            enabled = false;
            return;
        }
        _raceManager.RaceStarted += ResetStats;
        _raceManager.RacerFinished += OnRacerFinished;
        _player.GimmickActivated += OnGimmickActivated;
        _player.StunStarted += OnStunStarted;
        _player.JammerHit += OnJammerHit;
    }

    private void OnDisable()
    {
        if (_raceManager != null)
        {
            _raceManager.RaceStarted -= ResetStats;
            _raceManager.RacerFinished -= OnRacerFinished;
        }
        if (_player != null)
        {
            _player.GimmickActivated -= OnGimmickActivated;
            _player.StunStarted -= OnStunStarted;
            _player.JammerHit -= OnJammerHit;
        }
    }

    private void ResetStats()
    {
        RaceResultStore.Clear();
        _boostDistance = 0f;
        _speedRingCount = 0;
        _hitCount = 0;
        _saved = false;
        _previousPosition = _player.transform.position;
        _hasPosition = true;
    }

    private void LateUpdate()
    {
        if (_player == null) return;
        SampleDistance(CanCount);
    }

    private void SampleDistance(bool count)
    {
        Vector3 position = _player.transform.position;
        if (count && _hasPosition && _raceManager.PlayerData.IsBoosting)
            _boostDistance += Vector3.Distance(_previousPosition, position);
        // 非ブースト中・ポーズ中の移動を次の加算に含めない。
        _previousPosition = position;
        _hasPosition = true;
    }

    private void OnGimmickActivated(CourseGimmickBase gimmick)
    {
        if (CanCount && gimmick is SpeedRing ring && !ring.IsSlowing)
            _speedRingCount++;
    }

    private void OnStunStarted()
    {
        if (CanCount) _hitCount++;
    }

    private void OnJammerHit(JammerTypeProfile profile)
    {
        if (CanCount) _hitCount++;
    }

    private void OnRacerFinished(IReadOnlyRacerData data)
    {
        if (!data.IsPlayer || _saved) return;
        // 通知時にはIsFinishedがtrue。ゴールしたフレームの移動を最後に取り込む。
        SampleDistance(_raceManager.State == ERaceState.Racing && Time.timeScale > 0f);
        _saved = true;
        RaceResultStore.Save(new PlayerResultData(data.CurrentRank, data.FinishTime,
            _boostDistance, _speedRingCount, _hitCount));
    }
}
