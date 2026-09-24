using System.Collections.Generic;
using UnityEngine;

// 進行度・ゴール判定・順位の計算だけを担当する。周回なし（CourseSpline.IsLoop = false）の1本道が前提。
// ゴール判定は進行度（distance >= goalDistance）に一本化し、通過判定の取りこぼしに影響されないようにする。
public class RaceProgressTracker
{
    private readonly CourseSpline _spline;
    private readonly List<RacerEntry> _rankingBuffer = new List<RacerEntry>();
    private float _goalDistance;

    public RaceProgressTracker(CourseSpline spline)
    {
        _spline = spline;
    }

    public float GoalDistance => _goalDistance;

    // CourseSplineの距離テーブルが構築済みになってから呼ぶ。
    public void SetGoalDistance(float goalDistance)
    {
        _goalDistance = Mathf.Max(0.0001f, goalDistance);
    }

    public void UpdateProgress(List<RacerEntry> entries)
    {
        foreach (RacerEntry entry in entries)
        {
            if (entry.Data.IsFinished)
            {
                continue;
            }

            float distance = _spline.FindNearestDistance(entry.Racer.Transform.position);
            entry.Data.DistanceOnCourse = distance;
            entry.Data.CourseProgress = Mathf.Clamp01(distance / _goalDistance);
        }
    }

    // 今回ゴールしたRacerをnewlyFinishedへ追加する（ゴール演出などのイベント通知はRaceManagerが行う）。
    public void CheckFinish(List<RacerEntry> entries, float elapsedTime, List<RacerData> newlyFinished)
    {
        foreach (RacerEntry entry in entries)
        {
            RacerData data = entry.Data;
            if (data.IsFinished || data.DistanceOnCourse < _goalDistance)
            {
                continue;
            }

            data.IsFinished = true;
            data.FinishTime = elapsedTime;
            data.CourseProgress = 1f;
            newlyFinished.Add(data);
        }
    }

    // ゴール済み（FinishTimeの昇順）→ 未ゴール（CourseProgressの降順）の順に1位から振る。
    public void UpdateRanking(List<RacerEntry> entries)
    {
        _rankingBuffer.Clear();
        _rankingBuffer.AddRange(entries);
        _rankingBuffer.Sort(CompareRank);

        for (int i = 0; i < _rankingBuffer.Count; i++)
        {
            _rankingBuffer[i].Data.CurrentRank = i + 1;
        }
    }

    private static int CompareRank(RacerEntry a, RacerEntry b)
    {
        RacerData dataA = a.Data;
        RacerData dataB = b.Data;

        if (dataA.IsFinished != dataB.IsFinished)
        {
            return dataA.IsFinished ? -1 : 1;
        }

        int result = dataA.IsFinished
            ? dataA.FinishTime.CompareTo(dataB.FinishTime)
            : dataB.DistanceOnCourse.CompareTo(dataA.DistanceOnCourse);

        // 同着・同距離のときは順位がフレームごとに入れ替わらないようIDで固定する。
        return result != 0 ? result : dataA.RacerId.CompareTo(dataB.RacerId);
    }
}
