using UnityEngine;

// ブースト接触の横ずれを「何mを何秒で」動かすための進み具合を管理する（イーズアウト：最初に大きく動き、だんだん止まる）。
// 毎フレームAdvanceで「今回動かす分（m）」を返すだけで、実際に位置をずらすのは呼び出し側
// （PlayerFlightController／DummyRacer）が行う。飛行の慣性や最高速度とは別に動かすため、打ち消されない。
public class LateralPush
{
    private float _signedDistance;
    private float _duration;
    private float _elapsed;

    public bool IsActive => _elapsed < _duration;

    // signedDistance：コースの右方向を正とした押し出す距離（m）。動いている途中で呼ばれたら、そこから新しく押し出し直す。
    public void Start(float signedDistance, float duration)
    {
        _signedDistance = signedDistance;
        _duration = Mathf.Max(0.0001f, duration);
        _elapsed = 0f;
    }

    // 今回のフレームで動かす距離（m。コースの右方向が正）。動いていなければ0。
    public float Advance(float deltaTime)
    {
        if (!IsActive)
        {
            return 0f;
        }

        float previous = EaseOut(_elapsed / _duration);
        _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
        float current = EaseOut(_elapsed / _duration);
        return (current - previous) * _signedDistance;
    }

    public void Stop()
    {
        _signedDistance = 0f;
        _duration = 0f;
        _elapsed = 0f;
    }

    private static float EaseOut(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse;
    }
}
