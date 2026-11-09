using System;
using System.Collections.Generic;
using UnityEngine;

// 分岐のあるコースで、レーサー1人ぶんの「今どの区間（本線／分岐）を走っているか」を持つガイド。
// ICourseGuideとして、今走っている区間の中心・向き・トンネル半径を返すので、機体・カメラ・CPUの状況把握はそのまま使える。
// 区間の切り替え（分岐の領域での候補・ルートの確定・合流）は、毎TickのAdvanceで進める（PlayerFlightControllerが呼ぶ）。
public interface ICourseRoute : ICourseGuide
{
    // 本線の距離に直した進行度（m）。分岐の上でも、本線を走るレーサーと順位を比べられる。
    float CourseDistance { get; }

    // 確定して走っている分岐。本線を走っているとき・分岐の領域の中で確定する前はnull。
    CourseBranch CurrentBranch { get; }

    // 分岐の領域の中（まだルートを確定していない）か。
    bool IsInForkZone { get; }

    // 先に決めておいた分岐（SetIntendedBranch）。決めていなければnull。
    CourseBranch IntendedBranch { get; }

    // 走る区間（本線／分岐。分岐の領域では候補）が切り替わった回数。切り替わった瞬間は、中心が別のSplineの中心線へ跳ぶので、
    // 中心の動きから進んだ距離を測る側（PlayerFlightControllerの向きの追従）は、この値が変わったTickは進んだ距離に数えない。
    int SegmentChangeCount { get; }

    // 分岐の領域でルートを確定した瞬間（引数は確定した分岐。本線に決めたときはnull）。
    // 先に決めておいた分岐（SetIntendedBranch）へ、分岐点で入ったときも出す。
    event Action<CourseBranch> RouteCommitted;

    // 分岐から本線へ戻った瞬間（引数は抜けた分岐）。
    event Action<CourseBranch> MergedToMain;

    // 今の位置から、区間の切り替えを進める。毎Tickの最初に1回呼ぶ。
    void Advance(Vector3 position);

    // スタート・リトライで置き直したとき。本線から始め直し、先に決めておいた分岐も取り消す。
    void ResetRoute(Vector3 position);

    // 次の分岐で入る分岐を先に決める（CPU用）。分岐点を過ぎたところで、位置に関係なくその分岐を確定する
    // （確定すると、中心・向きが分岐の中心線になるので、中心からの横位置で操作を決めるCPUは、そのまま分岐へ寄っていく）。
    // nullなら取り消し（位置で決める）。本線を走っていて、その分岐の領域をまだ抜けていないときだけ受け付ける（受け付けたらtrue）。
    bool SetIntendedBranch(CourseBranch branch);

    // 本線を走っているとき、この先withinDistance（m）以内に分岐点がある分岐を、近い順にresultsへ入れる（resultsは最初に空にする）。
    void GetUpcomingBranches(float withinDistance, List<CourseBranch> results);
}
