using System;
using System.Collections.Generic;
using UnityEngine;

// レーサー1人ぶんのルート（ICourseRouteの実装）。CourseNetwork.CreateRouteで作る。
// ・本線：本線の最寄り点を使う。先に決めた分岐（SetIntendedBranch）があれば、分岐点を過ぎたところでその分岐に入る。
// ・分岐の領域（本線と分岐のトンネルが重なっている区間）：より深く入っている方のトンネル（候補）を走る。
//   深さ＝中心からの距離÷半径。行ったり来たりしないよう、HysteresisDepth以上深いときだけ候補を変える。
//   トンネルが重なっているので、分岐側へ寄っていくと先に候補が切り替わり、本線の壁に止められずに分岐へ入れる。
//   領域を抜けたところで、そのときの候補でルートを確定する（確定後は、そのルートだけを使う）。
// ・分岐：分岐の最寄り点を使う。合流の領域で本線の方へ深く入ったか、分岐の終点に着いたら本線に戻る。
// 区間が切り替わった直後は、向き（前・右・上）だけを、前の区間からCourseNetworkのTransitionDistanceかけてなめらかに移す
// （2本のSplineの向きの差の分、機首・カメラ・上下左右の移動の向きが一瞬で跳ばないように）。中心・トンネル半径はすぐ新しい区間のものにする。
public class CourseRoute : ICourseRoute
{
    // 候補を変えるのに必要な、深さの差（深さ＝中心からの距離÷半径）。
    private const float HysteresisDepth = 0.1f;

    // 分岐の終点のこの距離（m）手前まで来たら、本線に戻す。
    private const float BranchEndMargin = 0.5f;

    private enum EState
    {
        Main,
        ForkZone,
        Branch,
    }

    private readonly CourseNetwork _network;
    private readonly List<CourseBranch> _zoneBranches = new List<CourseBranch>();

    private EState _state = EState.Main;

    // Branch：確定した分岐。ForkZone：候補の分岐（本線が候補ならnull）。Main：null。
    private CourseBranch _branch;
    private CourseBranch _intendedBranch;
    private float _mainDistance;
    private float _branchDistance;

    // 分岐の領域の範囲（本線の距離）。領域に入っている分岐の、分岐点の一番手前〜領域の終わりの一番先。
    private float _zoneStartOnMain = float.PositiveInfinity;
    private float _zoneEndOnMain = float.NegativeInfinity;

    // 直前のAdvanceで走っていた区間のSplineと、それが分岐ならその分岐（切り替わりの検出用。本線ならnull）。
    private CourseSpline _lastSpline;
    private CourseBranch _lastBranch;

    // 区間が切り替わった直後、前の区間の向きから新しい区間の向きへ移している間の、前の区間（移し終えたらnull）と、
    // 切り替わってから新しい区間に沿って進んだ距離（m）。
    private CourseSpline _blendFromSpline;
    private CourseBranch _blendFromBranch;
    private float _blendProgress;
    private float _blendLastDistance;

    public CourseRoute(CourseNetwork network)
    {
        _network = network;
    }

    public event Action<CourseBranch> RouteCommitted;

    public event Action<CourseBranch> MergedToMain;

    public float CourseDistance => _branch != null ? _branch.MainEquivalentDistance(_branchDistance) : _mainDistance;

    public CourseBranch CurrentBranch => _state == EState.Branch ? _branch : null;

    public bool IsInForkZone => _state == EState.ForkZone;

    public CourseBranch IntendedBranch => _intendedBranch;

    public int SegmentChangeCount { get; private set; }

    private CourseSpline MainSpline => _network.MainSpline;

    // 今走っている区間（分岐の領域では候補の区間）のSpline。
    private CourseSpline CurrentSpline => _branch != null ? _branch.Spline : MainSpline;

    public void ResetRoute(Vector3 position)
    {
        _network.EnsureBuilt();
        _state = EState.Main;
        _branch = null;
        _intendedBranch = null;
        ClearZone();
        _branchDistance = 0f;
        _mainDistance = MainSpline != null ? MainSpline.FindNearestDistance(position) : 0f;
        _lastSpline = MainSpline;
        _lastBranch = null;
        _blendFromSpline = null;
        _blendFromBranch = null;
    }

    public void Advance(Vector3 position)
    {
        _network.EnsureBuilt();
        if (MainSpline == null)
        {
            return;
        }

        switch (_state)
        {
            case EState.Main:
                AdvanceOnMain(position);
                break;

            case EState.ForkZone:
                AdvanceInForkZone(position);
                break;

            case EState.Branch:
                AdvanceOnBranch(position);
                break;
        }

        // 区間の切り替わり（候補の切り替え・確定・合流のどれでも）を、ここ1か所で見つけて、向きをなめらかに移し始める。
        CourseSpline current = CurrentSpline;
        if (_lastSpline != null && current != _lastSpline)
        {
            BeginTransition(_lastSpline, _lastBranch, current, position);
        }
        else
        {
            UpdateTransition(current, position);
        }

        _lastSpline = current;
        _lastBranch = _branch;
    }

    public bool SetIntendedBranch(CourseBranch branch)
    {
        if (branch == null)
        {
            _intendedBranch = null;
            return true;
        }

        _network.EnsureBuilt();
        if (_state != EState.Main || !ContainsBranch(branch) || _mainDistance >= branch.ForkZoneEndOnMain)
        {
            return false;
        }

        _intendedBranch = branch;
        return true;
    }

    public void GetUpcomingBranches(float withinDistance, List<CourseBranch> results)
    {
        results.Clear();
        _network.EnsureBuilt();
        if (_state != EState.Main)
        {
            return;
        }

        IReadOnlyList<CourseBranch> branches = _network.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            float distanceToFork = branches[i].ForkDistance - _mainDistance;
            if (distanceToFork > 0f && distanceToFork <= withinDistance)
            {
                results.Add(branches[i]);
            }
        }
    }

    // ----------------------------------------------------
    // 区間の切り替え
    // ----------------------------------------------------

    private void AdvanceOnMain(Vector3 position)
    {
        _mainDistance = MainSpline.FindNearestDistance(position);

        // 先に決めておいた分岐：分岐点を過ぎたら、位置に関係なくその分岐を確定する。分岐の領域を通り過ぎていたら取り消す。
        if (_intendedBranch != null && _mainDistance >= _intendedBranch.ForkDistance)
        {
            CourseBranch intended = _intendedBranch;
            _intendedBranch = null;
            if (_mainDistance < intended.ForkZoneEndOnMain)
            {
                _branch = intended;
                _branchDistance = intended.Spline.FindNearestDistance(position);
                _state = EState.Branch;
                RouteCommitted?.Invoke(intended);
                return;
            }
        }

        ClearZone();
        if (AddZoneBranches())
        {
            _state = EState.ForkZone;
            _branch = null;
            AdvanceInForkZone(position);
        }
    }

    private void AdvanceInForkZone(Vector3 position)
    {
        _mainDistance = MainSpline.FindNearestDistance(position);
        AddZoneBranches();

        // 一番深く入っているトンネルを探し、今の候補よりHysteresisDepth以上深ければ候補を変える。
        float currentDepth = _branch != null ? GetBranchDepth(_branch, position, out _branchDistance) : GetMainDepth(position);
        CourseBranch best = _branch;
        float bestDepth = currentDepth;

        if (_branch != null)
        {
            float mainDepth = GetMainDepth(position);
            if (mainDepth < bestDepth)
            {
                best = null;
                bestDepth = mainDepth;
            }
        }

        for (int i = 0; i < _zoneBranches.Count; i++)
        {
            CourseBranch branch = _zoneBranches[i];
            if (branch == _branch)
            {
                continue;
            }

            float depth = GetBranchDepth(branch, position, out _);
            if (depth < bestDepth)
            {
                best = branch;
                bestDepth = depth;
            }
        }

        if (best != _branch && bestDepth + HysteresisDepth < currentDepth)
        {
            _branch = best;
            if (_branch != null)
            {
                _branchDistance = _branch.Spline.FindNearestDistance(position);
            }
        }

        // 領域を抜けたら、そのときの候補で確定する。
        if (_branch != null)
        {
            if (_branchDistance >= _branch.ForkZoneEnd)
            {
                _state = EState.Branch;
                ClearZone();
                RouteCommitted?.Invoke(_branch);
            }

            return;
        }

        if (_mainDistance >= _zoneEndOnMain)
        {
            _state = EState.Main;
            ClearZone();
            RouteCommitted?.Invoke(null);
            return;
        }

        // 領域より手前へ戻ったら、本線に戻す（ルートは確定しない）。
        if (_mainDistance < _zoneStartOnMain)
        {
            _state = EState.Main;
            ClearZone();
        }
    }

    private void AdvanceOnBranch(Vector3 position)
    {
        CourseBranch branch = _branch;
        _branchDistance = branch.Spline.FindNearestDistance(position);
        if (_branchDistance < branch.MergeZoneStart)
        {
            return;
        }

        bool reachedEnd = _branchDistance >= branch.Spline.TotalLength - BranchEndMargin;
        float branchDepth = GetBranchDepth(branch, position, out _);
        float mainDepth = GetMainDepth(position);
        if (!reachedEnd && mainDepth + HysteresisDepth >= branchDepth)
        {
            return;
        }

        _state = EState.Main;
        _branch = null;
        _mainDistance = MainSpline.FindNearestDistance(position);
        MergedToMain?.Invoke(branch);
    }

    // 区間がfromからtoへ切り替わった。前の区間の向きから、なめらかに移し始める。
    // 移している途中で元の区間へ戻ったとき（分岐の領域で候補が戻ったとき）は、今の混ぜ具合から逆向きに移す（向きが跳ばないように）。
    private void BeginTransition(CourseSpline from, CourseBranch fromBranch, CourseSpline to, Vector3 position)
    {
        SegmentChangeCount++;
        if (_network.ShowDebugLogs)
        {
            LogTransition(from, to, position);
        }

        float length = _network.TransitionDistance;
        _blendProgress = _blendFromSpline == to ? Mathf.Max(0f, length - _blendProgress) : 0f;
        _blendFromSpline = length > 0f ? from : null;
        _blendFromBranch = length > 0f ? fromBranch : null;
        _blendLastDistance = to.FindNearestDistance(position);
    }

    // 新しい区間に沿って進んだ距離を足し、TransitionDistanceを超えたら移し終える。
    private void UpdateTransition(CourseSpline current, Vector3 position)
    {
        if (_blendFromSpline == null)
        {
            return;
        }

        float distance = current.FindNearestDistance(position);
        _blendProgress += Mathf.Abs(distance - _blendLastDistance);
        _blendLastDistance = distance;
        if (_blendProgress >= _network.TransitionDistance)
        {
            _blendFromSpline = null;
            _blendFromBranch = null;
        }
    }

    // つなぎ目の確認用。切り替わった地点での、前後の区間の向き（前・上）の差と、中心のずれ。
    private static void LogTransition(CourseSpline from, CourseSpline to, Vector3 position)
    {
        float fromDistance = from.FindNearestDistance(position);
        float toDistance = to.FindNearestDistance(position);
        float forwardAngle = Vector3.Angle(from.EvaluateForwardByDistance(fromDistance), to.EvaluateForwardByDistance(toDistance));
        float upAngle = Vector3.Angle(from.EvaluateUpByDistance(fromDistance), to.EvaluateUpByDistance(toDistance));
        float centerGap = Vector3.Distance(from.EvaluatePositionByDistance(fromDistance), to.EvaluatePositionByDistance(toDistance));
        Debug.Log($"CourseRoute: 区間の切り替え {from.name} → {to.name}（前の向きの差 {forwardAngle:F1}°、上方向の差 {upAngle:F1}°、中心のずれ {centerGap:F1}m）");
    }

    // 今の本線の距離が、分岐の領域（分岐点〜領域の終わり）に入っている分岐を足す（領域の中にいる間は外さない）。
    // 領域の範囲（本線の距離）も、足した分岐に合わせて広げる。1つでも分岐があればtrue。
    private bool AddZoneBranches()
    {
        IReadOnlyList<CourseBranch> branches = _network.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            CourseBranch branch = branches[i];
            if (_mainDistance < branch.ForkDistance || _mainDistance >= branch.ForkZoneEndOnMain || _zoneBranches.Contains(branch))
            {
                continue;
            }

            _zoneBranches.Add(branch);
            _zoneStartOnMain = Mathf.Min(_zoneStartOnMain, branch.ForkDistance);
            _zoneEndOnMain = Mathf.Max(_zoneEndOnMain, branch.ForkZoneEndOnMain);
        }

        return _zoneBranches.Count > 0;
    }

    private void ClearZone()
    {
        _zoneBranches.Clear();
        _zoneStartOnMain = float.PositiveInfinity;
        _zoneEndOnMain = float.NegativeInfinity;
    }

    private float GetMainDepth(Vector3 position)
    {
        float distance = MainSpline.FindNearestDistance(position);
        return GetDepth(MainSpline, distance, position);
    }

    private static float GetBranchDepth(CourseBranch branch, Vector3 position, out float branchDistance)
    {
        branchDistance = branch.Spline.FindNearestDistance(position);
        return GetDepth(branch.Spline, branchDistance, position);
    }

    // トンネルにどれだけ深く入っているか（中心からの距離÷半径。0＝中心、1＝壁）。
    private static float GetDepth(CourseSpline spline, float distance, Vector3 position)
    {
        float radius = spline.GetTunnelRadiusByDistance(distance);
        if (radius <= 0f)
        {
            return float.PositiveInfinity;
        }

        return Vector3.Distance(position, spline.EvaluatePositionByDistance(distance)) / radius;
    }

    private bool ContainsBranch(CourseBranch branch)
    {
        IReadOnlyList<CourseBranch> branches = _network.Branches;
        for (int i = 0; i < branches.Count; i++)
        {
            if (branches[i] == branch)
            {
                return true;
            }
        }

        return false;
    }

    // ----------------------------------------------------
    // ICourseGuide 実装（今走っている区間で答える）
    // ----------------------------------------------------

    // 問い合わせの位置を、どの区間のどの距離で答えるかを決める。基本は今の区間。
    // 分岐を走っていて、問い合わせの位置が分岐の終点より先（カメラの狙い先など）なら、本線の中心線の方が近ければ本線で答える。
    // 分岐の途中の位置は、合流の領域でも分岐で答える（本線で答えると、自分の位置の向きが区間を切り替える前に本線へ跳ぶため）。
    private void ResolveQuery(Vector3 worldPosition, out CourseSpline spline, out float distance)
    {
        spline = CurrentSpline;
        distance = spline.FindNearestDistance(worldPosition);
        if (_state != EState.Branch || distance < spline.TotalLength - BranchEndMargin)
        {
            return;
        }

        CourseSpline main = MainSpline;
        float mainDistance = main.FindNearestDistance(worldPosition);
        float sqrToCurrent = (worldPosition - spline.EvaluatePositionByDistance(distance)).sqrMagnitude;
        float sqrToMain = (worldPosition - main.EvaluatePositionByDistance(mainDistance)).sqrMagnitude;
        if (sqrToMain < sqrToCurrent)
        {
            spline = main;
            distance = mainDistance;
        }
    }

    public Vector3 GetCenterPosition(Vector3 worldPosition)
    {
        ResolveQuery(worldPosition, out CourseSpline spline, out float distance);
        return spline.EvaluatePositionByDistance(distance);
    }

    public Vector3 GetForward(Vector3 worldPosition)
    {
        GetFrame(worldPosition, out Vector3 forward, out _, out _);
        return forward;
    }

    public Vector3 GetRight(Vector3 worldPosition)
    {
        GetFrame(worldPosition, out _, out Vector3 right, out _);
        return right;
    }

    public Vector3 GetUp(Vector3 worldPosition)
    {
        GetFrame(worldPosition, out _, out _, out Vector3 up);
        return up;
    }

    // その位置での向き（前・右・上）。区間が切り替わった直後は、前の区間の向きから新しい区間の向きへ、
    // 新しい区間に沿って進んだ距離に合わせてなめらかに移す（回転として混ぜるので、3つの向きは直交したまま）。
    private void GetFrame(Vector3 worldPosition, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        ResolveQuery(worldPosition, out CourseSpline spline, out float distance);
        forward = spline.EvaluateForwardByDistance(distance);
        right = spline.EvaluateRightByDistance(distance);
        up = spline.EvaluateUpByDistance(distance);

        float weight = GetBlendWeight();
        if (weight >= 1f)
        {
            return;
        }

        float fromDistance = _blendFromSpline.FindNearestDistance(worldPosition);
        Quaternion from = Quaternion.LookRotation(_blendFromSpline.EvaluateForwardByDistance(fromDistance), _blendFromSpline.EvaluateUpByDistance(fromDistance));
        Quaternion to = Quaternion.LookRotation(forward, up);
        Quaternion blended = Quaternion.Slerp(from, to, weight);
        forward = blended * Vector3.forward;
        right = blended * Vector3.right;
        up = blended * Vector3.up;
    }

    public float GetTunnelRadius(Vector3 worldPosition)
    {
        ResolveQuery(worldPosition, out CourseSpline spline, out float distance);
        return spline.GetTunnelRadiusByDistance(distance);
    }

    // 区間が切り替わった直後の、新しい区間の向き・狙い先をどれだけ使うか（0＝前の区間のまま～1＝新しい区間）。
    // 切り替わってから新しい区間に沿って進んだ距離で、なめらか（SmoothStep）に増やす。移していなければ1。
    private float GetBlendWeight()
    {
        float length = _network.TransitionDistance;
        if (_blendFromSpline == null || length <= 0f)
        {
            return 1f;
        }

        return Mathf.SmoothStep(0f, 1f, _blendProgress / length);
    }

    // ルートに沿って先の位置を返す（CourseSpline.GetLookAheadPositionと同じく、中心からのずれも足す）。
    // 分岐の上で終点を越えたら、合流点から本線の上へ続ける。本線で、先に決めた分岐の分岐点を越えたら、分岐の上へ続ける。
    // 区間が切り替わった直後は、前の区間に沿った先の位置から、新しい区間に沿った先の位置へなめらかに移す
    // （カメラの狙い先が、2本のSplineの先の位置の差の分だけ一瞬で跳ばないように。向きの移し方（GetFrame）と同じ混ぜ具合）。
    public Vector3 GetLookAheadPosition(Vector3 worldPosition, float lookAheadDistance)
    {
        ResolveQuery(worldPosition, out CourseSpline spline, out float distance);
        CourseBranch branch = spline != MainSpline ? _branch : null;
        CourseBranch intended = _state == EState.Main ? _intendedBranch : null;
        Vector3 target = GetLookAheadOn(spline, branch, intended, distance, worldPosition, lookAheadDistance);

        float weight = GetBlendWeight();
        if (weight >= 1f)
        {
            return target;
        }

        float fromDistance = _blendFromSpline.FindNearestDistance(worldPosition);
        Vector3 fromTarget = GetLookAheadOn(_blendFromSpline, _blendFromBranch, null, fromDistance, worldPosition, lookAheadDistance);
        return Vector3.Lerp(fromTarget, target, weight);
    }

    // splineのdistanceの地点から、lookAheadDistance先の位置（中心からのずれも足す）。
    // branchはsplineが分岐ならその分岐（終点を越えたら合流点から本線へ続ける）、本線ならnull。
    // intendedは、本線で分岐点を越えたら分岐の上へ続ける分岐（無ければnull）。
    private Vector3 GetLookAheadOn(CourseSpline spline, CourseBranch branch, CourseBranch intended, float distance, Vector3 worldPosition, float lookAheadDistance)
    {
        Vector3 offsetFromCenter = worldPosition - spline.EvaluatePositionByDistance(distance);
        float target = distance + lookAheadDistance;
        CourseSpline main = MainSpline;

        if (branch != null)
        {
            float branchLength = spline.TotalLength;
            return target > branchLength
                ? main.EvaluatePositionByDistance(branch.MergeDistance + (target - branchLength)) + offsetFromCenter
                : spline.EvaluatePositionByDistance(target) + offsetFromCenter;
        }

        if (intended != null && distance <= intended.ForkDistance && target > intended.ForkDistance)
        {
            return intended.Spline.EvaluatePositionByDistance(target - intended.ForkDistance) + offsetFromCenter;
        }

        return spline.EvaluatePositionByDistance(target) + offsetFromCenter;
    }
}
