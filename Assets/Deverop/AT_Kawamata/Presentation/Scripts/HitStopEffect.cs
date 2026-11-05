using UnityEngine;

// ヒットストップ（一瞬だけ時間を止めて、当たった手応えを出す）。Time.timeScaleを下げ、実時間で決めた秒数が経ったら元に戻す。
// 全員の動きと、Time.deltaTimeで動く演出がすべて止まるので、Playerの演出（ShieldBlockPresentation）からだけ呼ぶ（CPUどうしで止めない）。
// 止めている途中で呼ばれたら、残りの秒数を長い方に延ばす。止めている間に他からtimeScaleを変えられていたら、そちらを優先して戻さない。
public class HitStopEffect : MonoBehaviour
{
    [Tooltip("止めている間のTime.timeScale（0＝完全に止める）")]
    [Range(0f, 1f)]
    [SerializeField] private float _timeScale = 0.05f;
    [Tooltip("強さ1のときに止める秒数（実時間）。強さに比例して短くする")]
    [SerializeField] private float _maxDuration = 0.08f;

    private bool _isStopping;
    private float _remainingTime;
    private float _timeScaleToRestore = 1f;

    private void OnDisable()
    {
        Restore();
    }

    private void Update()
    {
        if (!_isStopping)
        {
            return;
        }

        _remainingTime -= Time.unscaledDeltaTime;
        if (_remainingTime <= 0f)
        {
            Restore();
        }
    }

    // strength：0～1。
    public void Play(float strength)
    {
        float duration = Mathf.Clamp01(strength) * _maxDuration;
        if (!isActiveAndEnabled || duration <= 0f)
        {
            return;
        }

        if (!_isStopping)
        {
            _timeScaleToRestore = Time.timeScale;
            Time.timeScale = _timeScale;
            _isStopping = true;
        }

        _remainingTime = Mathf.Max(_remainingTime, duration);
    }

    private void Restore()
    {
        if (!_isStopping)
        {
            return;
        }

        if (Mathf.Approximately(Time.timeScale, _timeScale))
        {
            Time.timeScale = _timeScaleToRestore;
        }

        _isStopping = false;
        _remainingTime = 0f;
    }
}
