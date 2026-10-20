// 「押し続けたいか」から、IRaceInputのPressed（押した瞬間）・Held（押し続けている）を作る。
// RacerManagerBaseは押した瞬間が無いとブースト・シールドを始めないため、
// 押したのに始まらなかった（発動できなかった・すぐ終わった）ときは、一度離してから押し直す（押しっぱなしで止まらないように）。
public class CpuButtonLatch
{
    // 押してから、始まったかを確かめるまでのTick数。押したTickの後で始まるので、次のTickから見る。
    private const int ActivationCheckTicks = 2;

    private int _ticksSincePress;

    public bool Pressed { get; private set; }

    public bool Held { get; private set; }

    // wants：押し続けたいか。isActive：今、その機能（ブースト・シールド）が動いているか。
    public void Update(bool wants, bool isActive)
    {
        Pressed = false;
        if (!wants)
        {
            Held = false;
            return;
        }

        if (!Held)
        {
            Pressed = true;
            Held = true;
            _ticksSincePress = 0;
            return;
        }

        _ticksSincePress++;
        if (!isActive && _ticksSincePress >= ActivationCheckTicks)
        {
            // このTickは離し、次のTickで押し直す。
            Held = false;
        }
    }

    public void Reset()
    {
        Pressed = false;
        Held = false;
        _ticksSincePress = 0;
    }
}
