using DG.Tweening;
using UnityEngine;

// ブースト時の集中線の演出。全画面シェーダー（S_SpeedLine）に渡す強さ（グローバル変数）をDOTweenで上げ下げする。
// 強さが0の間は ConditionalFullScreenFeature がパスを積まないので、負荷はかからない。
// Play／Stopは、各演出の窓口（BoostPresentation・ShieldBlockPresentation・SpeedRingPresentation）から呼ばれる。
// 同時に使われているときは一番強いものに合わせ、使っているところが全部終わったら消す（ScreenEffectRequests）。
public class SpeedLineEffect : MonoBehaviour
{
    // シェーダー（S_SpeedLine）と ConditionalFullScreenFeature の「強さのプロパティ名」に合わせる
    private static readonly int StrengthId = Shader.PropertyToID("_SpeedLineStrength");

    [Header("強さ")]
    [Tooltip("ブースト中の集中線の強さ。ほかの演出は、これに対する割合で強さを決める")]
    [SerializeField, Range(0f, 1f)] private float _boostStrength = 1f;

    [Header("時間")]
    [Tooltip("ブースト開始時に強さを上げ切るまでの秒数")]
    [SerializeField] private float _playDuration = 0.25f;
    [SerializeField] private Ease _playEase = Ease.OutCubic;
    [Tooltip("ブースト終了時に強さを0へ戻し切るまでの秒数")]
    [SerializeField] private float _stopDuration = 0.4f;
    [SerializeField] private Ease _stopEase = Ease.InOutSine;

    private readonly ScreenEffectRequests _requests = new ScreenEffectRequests();
    private float _strength;
    private float _targetStrength;
    private Tween _tween;

    private void Awake()
    {
        SetStrength(0f);
    }

    private void OnDisable()
    {
        KillTween();
        _requests.Clear();
        _targetStrength = 0f;
        SetStrength(0f);
    }

    private void OnDestroy()
    {
        KillTween();
    }

    // owner：使うところ（呼び出し側のthis）。strengthRate：ブースト中の強さに対する割合（0～1）。
    // 使い終わったら、同じownerでStopを呼ぶ。
    public void Play(object owner, float strengthRate = 1f)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _requests.Set(owner, strengthRate);
        TweenToRequestedStrength();
    }

    // ownerが使い終わる。ほかがまだ使っていれば、その中で一番強いものに合わせる。
    public void Stop(object owner)
    {
        _requests.Remove(owner);
        TweenToRequestedStrength();
    }

    // 単体での見た目の確認用（BoostEffectPreviewController）。
    public void Play()
    {
        Play(this);
    }

    public void Stop()
    {
        Stop(this);
    }

    // 今使われている中で一番強いものへ向かう。強くするときは開始、弱くするときは終了の時間を使う。
    // もうそこへ向かっているときは、やり直さない。
    private void TweenToRequestedStrength()
    {
        float target = _boostStrength * _requests.MaxStrength;
        if (Mathf.Approximately(target, _targetStrength))
        {
            return;
        }

        bool isRising = target > _targetStrength;
        _targetStrength = target;
        TweenTo(target, isRising ? _playDuration : _stopDuration, isRising ? _playEase : _stopEase);
    }

    // 途中で呼ばれても、今の値から目標へ向かい直す。
    private void TweenTo(float target, float duration, Ease ease)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        KillTween();
        _tween = DOTween
            .To(() => _strength, SetStrength, target, duration)
            .SetEase(ease);
    }

    private void SetStrength(float value)
    {
        _strength = value;
        Shader.SetGlobalFloat(StrengthId, value);
    }

    private void KillTween()
    {
        _tween?.Kill();
        _tween = null;
    }
}

