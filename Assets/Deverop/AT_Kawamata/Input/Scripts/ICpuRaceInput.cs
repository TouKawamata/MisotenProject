// CPUの入力（判断）の窓口。IRaceInputに、毎Tickの判断の更新を足したもの。
// CPUManagerがTickの最初に1回だけUpdateInputを呼び、その後でIRaceInputの値（Horizontal・BoostPressedなど）を読む。
// そのため、判断はUpdateInputの中でまとめて行い、値はそのTickの間は変えないこと（Pressedが二重に出たり消えたりしないように）。
// IRaceInputだけを実装した入力でもCPUManagerは動く（その場合は、入力側が自分で値を更新する）。
public interface ICpuRaceInput : IRaceInput
{
    // owner：入力を使うCPU。位置（Transform）・速さ・ゲージ・シールドが使えるか・コース（CourseGuide）・全員のRacerData（Racers）を読める。
    // data：このCPU自身のRacerData（順位・コース上の距離など）。
    void UpdateInput(CPUManager owner, float deltaTime, IReadOnlyRacerData data);
}
