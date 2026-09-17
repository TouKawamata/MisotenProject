// PlayerFlightControllerがSpline情報をどう扱って移動するかの検証用パターン。
// AERO-STRIKER Spline / Player 検証仕様のパターンA・B・C・Eに、追加検証用のF（手動操作）を加えたもの。
// D（強制ガイド方式）は検証終了のため削除済み。
public enum SplineFollowMode
{
    FreeFlight,                  // A: 現状方式（自由飛行、Splineへは弱く追従するだけ）
    RoadSurface,                 // B: 路面方式（Spline断面に沿った移動）
    FreeFlightWithRoadAttitude,  // C: 自由飛行＋路面姿勢（AとBの中間案）
    Tunnel,                      // E: トンネル方式（Spline周囲を3次元的に移動）
    ManualDrive                  // F: 手動操作方式（Eと同じトンネル空間内を、向いている方向へそのまま前進する）
}
