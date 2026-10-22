using System.Collections.Generic;
using UnityEngine;

// バックミラーを出すかどうかの判定。Player以外のレーサーが「ブースト中」「Playerの後ろの一定距離以内」「近づいている」を
// すべて満たしたら出す。条件が外れても最低表示時間の間は出したままにする（ちらつき防止）。
// RaceManagerの公開している値（Racers / PlayerData）を読むだけで、既存のファイルは変えない。
public class BackMirrorTrigger : MonoBehaviour
{
    [SerializeField] private RaceManager _raceManager;

    [Header("条件")]
    [Tooltip("Playerの後ろ、この距離（m、コース上の距離の差）以内でブーストしている相手がいたら出す")]
    [SerializeField] private float _detectDistance = 40f;

    [Tooltip("ONなら、前のフレームより差が縮んでいる（近づいている）相手だけにする")]
    [SerializeField] private bool _requireApproaching = true;

    [Tooltip("条件が外れてから消すまでの最低時間（秒）")]
    [SerializeField] private float _minShowTime = 1.5f;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs;

    // RacerIdごとの、前のフレームのPlayerとの差（m）。
    private readonly Dictionary<int, float> _previousGaps = new Dictionary<int, float>();

    private float _hideTimer;

    // 出すかどうか。BackMirrorViewが読む。
    public bool ShouldShow { get; private set; }

    // 条件を満たした相手のうち、一番近い相手。いなければnull。
    public IReadOnlyRacerData Threat { get; private set; }

    private void Awake()
    {
        if (_raceManager == null)
        {
            Debug.LogError("BackMirrorTrigger: Race Manager が未設定です", this);
            enabled = false;
        }
    }

    private void Update()
    {
        IReadOnlyRacerData threat = FindThreat();
        if (threat != Threat && _showDebugLogs)
        {
            Debug.Log(threat != null ? $"BackMirrorTrigger: 後ろから {threat.DisplayName} がブーストで接近" : "BackMirrorTrigger: 接近なし", this);
        }

        Threat = threat;

        if (threat != null)
        {
            _hideTimer = _minShowTime;
            ShouldShow = true;
            return;
        }

        _hideTimer -= Time.deltaTime;
        if (_hideTimer <= 0f || !IsRacing())
        {
            _hideTimer = 0f;
            ShouldShow = false;
        }
    }

    private IReadOnlyRacerData FindThreat()
    {
        IReadOnlyRacerData player = _raceManager.PlayerData;
        if (player == null || !IsRacing())
        {
            _previousGaps.Clear();
            return null;
        }

        IReadOnlyRacerData nearest = null;
        float nearestGap = float.MaxValue;
        foreach (IReadOnlyRacerData racer in _raceManager.Racers)
        {
            if (racer.IsPlayer)
            {
                continue;
            }

            // ＋なら相手がPlayerの後ろにいる。
            float gap = player.DistanceOnCourse - racer.DistanceOnCourse;
            bool hasPrevious = _previousGaps.TryGetValue(racer.RacerId, out float previousGap);
            _previousGaps[racer.RacerId] = gap;

            if (!racer.IsBoosting || racer.IsFinished || gap <= 0f || gap > _detectDistance)
            {
                continue;
            }

            if (_requireApproaching && (!hasPrevious || gap >= previousGap))
            {
                continue;
            }

            if (gap < nearestGap)
            {
                nearestGap = gap;
                nearest = racer;
            }
        }

        return nearest;
    }

    // Playerがゴールした後・カウントダウン中は出さない。
    private bool IsRacing()
    {
        IReadOnlyRacerData player = _raceManager.PlayerData;
        return _raceManager.State == ERaceState.Racing && player != null && !player.IsFinished;
    }
}
