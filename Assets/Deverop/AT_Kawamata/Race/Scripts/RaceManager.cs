using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

// レース全体（スタートグリッドへの配置 → カウントダウン → 走行 → ゴール → レース終了）を統括する。
// 状態遷移と「いつ何をするか」の指示だけを持ち、配置の計算はStartGrid、進行度・順位・ゴール判定はRaceProgressTrackerへ任せる。
// 各RacerのTickはここから呼ぶ（カウントダウン中はTickを呼ばないことで移動不可にする）。
// UI・演出は直接触らず、イベントを出すだけにする。Initialize/Start/Tick/DisposeはGameLifetimeScope（VContainer）から呼び出される。
public class RaceManager : MonoBehaviour, IInitializable, IStartable, ITickable, IDisposable
{
    private const int PlayerRacerId = 0;

    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private CourseSpline _courseSpline;
    [SerializeField] private RaceSettings _settings;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private readonly List<RacerEntry> _entries = new List<RacerEntry>();
    private readonly List<IReadOnlyRacerData> _racerDataList = new List<IReadOnlyRacerData>();
    private readonly List<RacerData> _newlyFinished = new List<RacerData>();

    private StartGrid _startGrid;
    private RaceProgressTracker _progressTracker;
    private RacerEntry _playerEntry;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isInitialized;

    // カウントが変わったとき（3, 2, 1, …）。0はGO。
    public event Action<int> CountdownChanged;

    // GOの瞬間。
    public event Action RaceStarted;

    // 各Racerがゴールしたとき。
    public event Action<IReadOnlyRacerData> RacerFinished;

    // レース終了（全員ゴール、またはPlayerのゴールから一定時間後）。
    public event Action RaceFinished;

    public ERaceState State { get; private set; } = ERaceState.Ready;

    // GOからの経過時間（秒）。Time.timeではなく自前で積み上げる（将来ポーズを入れてもズレないように）。
    public float ElapsedTime { get; private set; }

    public float GoalDistance => _progressTracker != null ? _progressTracker.GoalDistance : 0f;

    public IReadOnlyList<IReadOnlyRacerData> Racers => _racerDataList;

    // Initialize前はnull。
    public IReadOnlyRacerData PlayerData => _playerEntry?.Data;

    public void Initialize()
    {
        if (_playerManager == null || _courseSpline == null || _settings == null)
        {
            Debug.LogError("RaceManager: PlayerManager / CourseSpline / RaceSettings が未設定です", this);
            return;
        }

        _playerManager.Initialize();

        _entries.Clear();
        _racerDataList.Clear();
        _playerEntry = new RacerEntry(_playerManager, new RacerData(PlayerRacerId, true), _settings.PlayerGridIndex);
        AddEntry(_playerEntry);

        _startGrid = new StartGrid(_courseSpline, _settings);
        _progressTracker = new RaceProgressTracker(_courseSpline);
        _cancellationTokenSource = new CancellationTokenSource();
        State = ERaceState.Ready;
        _isInitialized = true;
    }

    // InitializeはLifetimeScopeのAwake内で呼ばれ、CourseSplineの距離テーブルがまだ無い可能性があるため、
    // 配置とカウントダウンは全Awakeが終わった後のStartから始める。
    // MonoBehaviourのStartメッセージとして二重に呼ばれないよう、明示的なインターフェイス実装にする。
    void IStartable.Start()
    {
        if (!_isInitialized)
        {
            return;
        }

        StartSequenceAsync(_cancellationTokenSource.Token).Forget();
    }

    public void Tick()
    {
        if (!_isInitialized)
        {
            return;
        }

        float dt = Time.deltaTime;
        switch (State)
        {
            case ERaceState.Racing:
                ElapsedTime += dt;
                TickRacers(dt);
                _progressTracker.UpdateProgress(_entries);
                CheckFinish();
                _progressTracker.UpdateRanking(_entries);
                CheckRaceEnd();
                break;

            case ERaceState.Finished:
                // ゴール後の走行区間。順位・タイムは確定済みなので動かすだけ。
                TickRacers(dt);
                break;
        }
    }

    public void Dispose()
    {
        if (_cancellationTokenSource == null)
        {
            return;
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    private void AddEntry(RacerEntry entry)
    {
        _entries.Add(entry);
        _racerDataList.Add(entry.Data);
    }

    private async UniTaskVoid StartSequenceAsync(CancellationToken token)
    {
        SetupGoalDistance();
        PlaceRacers();
        State = ERaceState.Countdown;

        for (int count = _settings.CountdownSeconds; count > 0; count--)
        {
            CountdownChanged?.Invoke(count);
            Log($"カウントダウン {count}");

            bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled)
            {
                return;
            }
        }

        CountdownChanged?.Invoke(0);
        ElapsedTime = 0f;
        State = ERaceState.Racing;
        RaceStarted?.Invoke();
        Log("GO");
    }

    private void SetupGoalDistance()
    {
        float goalDistance = _courseSpline.TotalLength - _settings.GoalDistanceFromEnd;
        if (goalDistance <= _settings.StartDistance)
        {
            Debug.LogWarning($"RaceManager: ゴール距離（{goalDistance:F1}m）がスタート距離（{_settings.StartDistance:F1}m）以下です。コース長（{_courseSpline.TotalLength:F1}m）とRaceSettingsを見直してください", this);
        }

        _progressTracker.SetGoalDistance(goalDistance);
    }

    private void PlaceRacers()
    {
        foreach (RacerEntry entry in _entries)
        {
            _startGrid.GetPose(entry.GridIndex, out Vector3 position, out Quaternion rotation);
            entry.Racer.PlaceAt(position, rotation);
            entry.Data.ResetProgress();
        }

        // カウントダウン中のDebug HUD・UI用に、配置直後の進行度と順位を出しておく。
        _progressTracker.UpdateProgress(_entries);
        _progressTracker.UpdateRanking(_entries);
    }

    private void TickRacers(float dt)
    {
        foreach (RacerEntry entry in _entries)
        {
            entry.Racer.Tick(dt, entry.Data);
        }
    }

    private void CheckFinish()
    {
        _newlyFinished.Clear();
        _progressTracker.CheckFinish(_entries, ElapsedTime, _newlyFinished);

        foreach (RacerData data in _newlyFinished)
        {
            RacerFinished?.Invoke(data);
            Log($"ゴール Racer{data.RacerId} タイム {data.FinishTime:F2}秒");
        }
    }

    private void CheckRaceEnd()
    {
        bool isAllFinished = true;
        foreach (RacerEntry entry in _entries)
        {
            if (!entry.Data.IsFinished)
            {
                isAllFinished = false;
                break;
            }
        }

        RacerData playerData = _playerEntry.Data;
        bool isPlayerFinishDelayElapsed = playerData.IsFinished && ElapsedTime - playerData.FinishTime >= _settings.FinishDelayAfterPlayerGoal;

        if (!isAllFinished && !isPlayerFinishDelayElapsed)
        {
            return;
        }

        State = ERaceState.Finished;
        RaceFinished?.Invoke();
        Log("レース終了");
    }

    private void Log(string message)
    {
        if (_showDebugLogs)
        {
            Debug.Log($"RaceManager: {message}", this);
        }
    }
}
