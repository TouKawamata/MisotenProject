using System.Collections.Generic;
using UnityEngine;

// 配置ツール・トンネル表示が扱う1本のコース（本線、または分岐）。
// 分岐の上の距離は、本線の距離に直して比べる（CourseBranch.MainEquivalentDistance：分岐点〜合流点の本線の長さに比例で合わせる）。
// こうすると「500〜1000mに置く」のような範囲を、本線と分岐で同じ意味にできる。
public sealed class CourseTarget
{
    public readonly CourseSpline Spline;

    // 分岐ならその分岐（分岐点・合流点を計算済み）。本線ならnull。
    public readonly CourseBranch Branch;

    private CourseTarget(CourseSpline spline, CourseBranch branch)
    {
        Spline = spline;
        Branch = branch;
    }

    public bool IsBranch => Branch != null;

    public string Name => Spline != null ? Spline.name : "";

    // このコースの上の距離を、本線の距離に直す。
    public float ToMainDistance(float distance)
    {
        return IsBranch ? Branch.MainEquivalentDistance(distance) : distance;
    }

    // 本線の距離で指定した範囲（mainEndが0以下なら本線の最後まで）を、このコースの上の距離の範囲に直す。範囲がこのコースに無ければfalse。
    public bool TryGetRange(CourseSpline mainSpline, float mainStart, float mainEnd, out float start, out float end)
    {
        float mainTotal = mainSpline != null ? mainSpline.TotalLength : Spline.TotalLength;
        float resolvedStart = Mathf.Clamp(mainStart, 0f, mainTotal);
        float resolvedEnd = mainEnd <= 0f ? mainTotal : Mathf.Clamp(mainEnd, 0f, mainTotal);

        if (!IsBranch)
        {
            start = Mathf.Min(resolvedStart, Spline.TotalLength);
            end = Mathf.Min(resolvedEnd, Spline.TotalLength);
            return end > start;
        }

        // MainEquivalentDistanceは分岐の長さに比例した直線なので、逆にたどって分岐の上の距離にする。
        float fork = Branch.ForkDistance;
        float merge = Branch.MergeDistance;
        float branchLength = Spline.TotalLength;
        start = Mathf.InverseLerp(fork, merge, resolvedStart) * branchLength;
        end = Mathf.InverseLerp(fork, merge, resolvedEnd) * branchLength;
        return end > start;
    }

    // 使うコースの一覧。networkがあれば本線と分岐すべて（includeMain／includeBranchesで絞る）、無ければsplineだけ。
    // spline自体が分岐（CourseBranchが付いている）なら、分岐として扱う（範囲を本線の距離で比べるため）。
    public static List<CourseTarget> Collect(CourseSpline spline, CourseNetwork network, bool includeMain = true, bool includeBranches = true)
    {
        List<CourseTarget> targets = new List<CourseTarget>();

        if (network != null && network.MainSpline != null)
        {
            CourseSpline main = network.MainSpline;
            if (includeMain && main.TotalLength > 0f)
            {
                targets.Add(new CourseTarget(main, null));
            }

            if (!includeBranches || network.ConfiguredBranches == null)
            {
                return targets;
            }

            foreach (CourseBranch branch in network.ConfiguredBranches)
            {
                CourseTarget target = FromBranch(branch, main);
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        if (spline == null || spline.TotalLength <= 0f)
        {
            return targets;
        }

        if (spline.TryGetComponent(out CourseBranch ownBranch))
        {
            CourseTarget target = FromBranch(ownBranch, ownBranch.MainSpline);
            if (target != null && includeBranches)
            {
                targets.Add(target);
            }

            return targets;
        }

        if (includeMain)
        {
            targets.Add(new CourseTarget(spline, null));
        }

        return targets;
    }

    // 一覧のSplineだけ（重なりを調べるときに使う）。
    public static List<CourseSpline> SplinesOf(List<CourseTarget> targets)
    {
        List<CourseSpline> splines = new List<CourseSpline>(targets.Count);
        foreach (CourseTarget target in targets)
        {
            splines.Add(target.Spline);
        }

        return splines;
    }

    private static CourseTarget FromBranch(CourseBranch branch, CourseSpline main)
    {
        if (branch == null || main == null || branch.MainSpline != main || branch.Spline == null || branch.Spline.TotalLength <= 0f)
        {
            return null;
        }

        // 分岐点・合流点を求められない分岐は使わない（Gizmoから毎フレーム呼ばれるのでログは出さない。エディタの警告で知らせる）。
        branch.Rebuild();
        if (!branch.IsValid)
        {
            return null;
        }

        return new CourseTarget(branch.Spline, branch);
    }
}
