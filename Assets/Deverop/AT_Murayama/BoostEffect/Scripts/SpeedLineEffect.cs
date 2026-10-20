using DG.Tweening;
using UnityEngine;

// ブースト時の集中線の演出。全画面シェーダー（S_SpeedLine）に渡す強さ（グローバル変数）をDOTweenで上げ下げする。
// 強さが0の間は ConditionalFullScreenFeature がパスを積まないので、負荷はかからない。
// Play／StopはBoostPresentationから呼ばれる。
public class SpeedLineEffect : MonoBehaviour
{
    // シェーダー（S_SpeedLine）と ConditionalFullScreenFeature の「強さのプロパティ名」に合わせる
    private static readonly int StrengthId = Shader.PropertyToID("_SpeedLineStrength");

    [Header("強さ")]
    [Tooltip("ブースト中の集中線の強さ")]
    [SerializeField, Range(0f, 1f)] private float _boostStrength = 1f;

    [Header("時間")]
    [Tooltip("ブースト開始時に強さを上げ切るまでの秒数")]
    [SerializeField] private float _playDuration = 0.25f;
    [SerializeField] private Ease _playEase = Ease.OutCubic;
    [Tooltip("ブースト終了時に強さを0へ戻し切るまでの秒数")]
    [SerializeField] private float _stopDuration = 0.4f;
    [SerializeField] private Ease _stopEase = Ease.InOutSine;

    private float _strength;
    private Tween _tween;

    private void OnDisable()
    {
        KillTween();
        SetStrength(0f);
    }

    private void OnDestroy()
    {
        KillTween();
    }

    public void Play()
    {
        TweenTo(_boostStrength, _playDuration, _playEase);
    }

    public void Stop()
    {
        TweenTo(0f, _stopDuration, _stopEase);
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

