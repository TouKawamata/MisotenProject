using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

// レース全体（スタートグリッドへの配置 → カウントダウン → 走行 → ゴール → レース終了）を統括する。
// 状態遷移と「いつ何をするか」の指示だけを持ち、配置の計算はStartGrid、進行度・順位・ゴール判定はRaceProgressTracker、
// 通常の接触（ブーストなし）の食い込みの押し戻しはRacerContactResolverへ任せる。
// 各RacerのTickはここから呼ぶ（カウントダウン中はTickを呼ばないことで移動不可にする）。
// UI・演出は直接触らず、イベントを出すだけにする。Initialize/Start/Tick/DisposeはGameLifetimeScope（VContainer）から呼び出される。
public class RaceManager : MonoBehaviour, IInitializable, IStartable, ITickable, IDisposable
{
    private const int PlayerRacerId = 0;
    private const string PlayerDisplayName = "Player";

    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private CourseSpline _courseSpline;
    [SerializeField] private RaceSettings _settings;

    [Header("CPU")]
    [Tooltip("Player以外の参加者（IRacerを実装したコンポーネントを持つGameObject）。Playerのグリッド番号を飛ばして、空いているグリッドに上から順に並べる")]
    [SerializeField] private GameObject[] _cpuRacers;

    [Header("通常の接触")]
    [Tooltip("ブーストなしの接触で、レーサーどうしの食い込みを押し戻す設定。未設定なら押し戻さない（重なったまま走る）")]
    [SerializeField] private RacerContactProfile _contactProfile;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private readonly List<RacerEntry> _entries = new List<RacerEntry>();
    private readonly List<IReadOnlyRacerData> _racerDataList = new List<IReadOnlyRacerData>();
    private readonly List<RacerData> _newlyFinished = new List<RacerData>();

    private StartGrid _startGrid;
    private RaceProgressTracker _progressTracker;
    private RacerContactResolver _contactResolver;
    private RacerEntry _playerEntry;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isInitialized;
    private int _lastPlayerRank;

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
        _playerEntry = new RacerEntry(_playerManager, new RacerData(PlayerRacerId, true, PlayerDisplayName), _settings.PlayerGridIndex);
        AddEntry(_playerEntry);
        AddCpuEntries();

        _startGrid = new StartGrid(_courseSpline, _settings);
        _progressTracker = new RaceProgressTracker(_courseSpline);
        SetupContactResolver();
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
                UpdateGoalStop(dt);
                TickRacers(dt);
                _contactResolver?.Resolve(dt);
                _progressTracker.UpdateProgress(_entries);
                CheckFinish();
                LogPlayerRankChange();
                CheckRaceEnd();
                break;

            case ERaceState.Finished:
                // ゴール後の停止までの区間。順位・タイムは確定済みなので動かすだけ。
                UpdateGoalStop(dt);
                TickRacers(dt);
                _contactResolver?.Resolve(dt);
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

    // CPUはPlayerのグリッド番号を飛ばして、0番から順に空いているグリッドへ割り当てる。RacerIdは1から振る。
    private void AddCpuEntries()
    {
        if (_cpuRacers == null)
        {
            return;
        }

        int gridIndex = 0;
        int racerId = PlayerRacerId + 1;
        foreach (GameObject cpuObject in _cpuRacers)
        {
            if (cpuObject == null)
            {
                Debug.LogWarning("RaceManager: CPUの欄に未設定の要素があるため、飛ばします", this);
                continue;
            }

            if (!cpuObject.TryGetComponent(out IRacer racer))
            {
                Debug.LogError($"RaceManager: {cpuObject.name} にIRacerを実装したコンポーネントが無いため、参加させません", cpuObject);
                continue;
            }

            if (gridIndex == _settings.PlayerGridIndex)
            {
                gridIndex++;
            }

            AddEntry(new RacerEntry(racer, new RacerData(racerId, false, cpuObject.name), gridIndex));
            gridIndex++;
            racerId++;
        }
    }

    // IRacerContactBodyを実装している参加者だけを、通常の接触の押し戻しの対象にする。
    private void SetupContactResolver()
    {
        _contactResolver = null;
        if (_contactProfile == null)
        {
            Debug.LogWarning("RaceManager: Contact Profile が未設定のため、通常の接触の押し戻しは行いません", this);
            return;
        }

        _contactResolver = new RacerContactResolver(_courseSpline, _contactProfile);
        foreach (RacerEntry entry in _entries)
        {
            if (entry.Racer is IRacerContactBody body)
            {
                _contactResolver.Add(body);
            }
        }
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
            Log($"配置 {entry.Data.DisplayName} グリッド{entry.GridIndex}");
        }

        // カウントダウン中のDebug HUD・UI用に、配置直後の進行度と順位を出しておく。
        _progressTracker.UpdateProgress(_entries);
        _progressTracker.UpdateRanking(_entries);
        _lastPlayerRank = _playerEntry.Data.CurrentRank;
    }

    // ゴール済みのRacerに、停止までの速度の倍率を渡す（実際の減速はRacer側が行う）。
    private void UpdateGoalStop(float dt)
    {
        foreach (RacerEntry entry in _entries)
        {
            RacerData data = entry.Data;
            if (!data.IsFinished)
            {
                continue;
            }

            data.TimeSinceFinish += dt;
            data.SpeedMultiplier = _settings.EvaluateGoalStopSpeedMultiplier(data.TimeSinceFinish);
        }
    }

    // Tickの後に、Racerの状態（ブースト中など）をRacerDataへ写す。
    private void TickRacers(float dt)
    {
        foreach (RacerEntry entry in _entries)
        {
            entry.Racer.Tick(dt, entry.Data);

            RacerData data = entry.Data;
            data.IsBoosting = entry.Racer.IsBoosting;
            data.IsShielding = entry.Racer.IsShielding;
            data.IsStunned = entry.Racer.IsStunned;
        }
    }

    // ゴール判定 → 順位更新 → ゴールの通知の順にし、RacerFinishedを受け取る側で確定した順位を読めるようにする。
    private void CheckFinish()
    {
        _newlyFinished.Clear();
        _progressTracker.CheckFinish(_entries, ElapsedTime, _newlyFinished);
        _progressTracker.UpdateRanking(_entries);

        foreach (RacerData data in _newlyFinished)
        {
            RacerFinished?.Invoke(data);
            Log($"ゴール {data.DisplayName} {data.CurrentRank}位 タイム {data.FinishTime:F2}秒");
        }
    }

    // DummyRacerなどとの順位の入れ替わりを確認するためのログ。
    private void LogPlayerRankChange()
    {
        int rank = _playerEntry.Data.CurrentRank;
        if (rank == _lastPlayerRank)
        {
            return;
        }

        _lastPlayerRank = rank;
        Log($"Playerの順位 {rank}位 / {_entries.Count}人");
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
