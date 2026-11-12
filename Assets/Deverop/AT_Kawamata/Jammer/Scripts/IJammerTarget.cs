// レーサー以外で、妨害弾を受けるもの（加速リングなど）の窓口。リング用の弾（RingSwapJammerType）が当たると呼ばれる。
// 例：加速リング（CourseGimmickBase・Bonusレイヤー）が実装し、ReceiveJammerで減速リングに切り替わる。
public interface IJammerTarget
{
    void ReceiveJammer(JammerTypeProfile type);
}
