using DG.Tweening;
using UnityEngine;

/// <summary>
/// 画面の輝度をシャドウ、中間色、ハイライトの3色に変換する。
///
/// Full Screen Pass Renderer Featureで使用するShaderへ、
/// グローバルShaderプロパティを渡す。
///
/// Play / StopはPresentation系クラスから呼び出す。
/// </summary>
public sealed class TriToneEffect : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_TriToneIntensity");

    private static readonly int ShadowColorId =
        Shader.PropertyToID("_TriToneShadowColor");

    private static readonly int MidtoneColorId =
        Shader.PropertyToID("_TriToneMidtoneColor");

    private static readonly int HighlightColorId =
        Shader.PropertyToID("_TriToneHighlightColor");

    private static readonly int ShadowThresholdId =
        Shader.PropertyToID("_TriToneShadowThreshold");

    private static readonly int HighlightThresholdId =
        Shader.PropertyToID("_TriToneHighlightThreshold");

    private static readonly int BlendSoftnessId =
        Shader.PropertyToID("_TriToneBlendSoftness");

    [Header("強さ")]
    [Tooltip("再生中のTriTone効果の強さ")]
    [SerializeField, Range(0f, 1f)]
    private float _playIntensity = 1f;

    [Header("3色")]
    [Tooltip("暗い部分に使用する色")]
    [SerializeField]
    private Color _shadowColor = new Color(
        0.02f,
        0.05f,
        0.12f,
        1f
    );

    [Tooltip("中間の明るさに使用する色")]
    [SerializeField]
    private Color _midtoneColor = new Color(
        0.1f,
        0.55f,
        0.65f,
        1f
    );

    [Tooltip("明るい部分に使用する色")]
    [SerializeField]
    private Color _highlightColor = new Color(
        1f,
        0.9f,
        0.55f,
        1f
    );

    [Header("輝度範囲")]
    [Tooltip("この輝度付近までをシャドウとして扱う")]
    [SerializeField, Range(0f, 1f)]
    private float _shadowThreshold = 0.33f;

    [Tooltip("この輝度付近からハイライトとして扱う")]
    [SerializeField, Range(0f, 1f)]
    private float _highlightThreshold = 0.66f;

    [Tooltip("3色の境界をぼかす幅。0に近いほど境界が明確になる")]
    [SerializeField, Range(0.001f, 0.5f)]
    private float _blendSoftness = 0.08f;

    [Header("Play")]
    [Tooltip("TriToneの強さを上げ切るまでの秒数")]
    [SerializeField, Min(0f)]
    private float _playDuration = 0.25f;

    [SerializeField]
    private Ease _playEase = Ease.OutCubic;

    [Header("Stop")]
    [Tooltip("TriToneの強さを0へ戻し切るまでの秒数")]
    [SerializeField, Min(0f)]
    private float _stopDuration = 0.4f;

    [SerializeField]
    private Ease _stopEase = Ease.InOutSine;

    [Header("時間")]
    [Tooltip("Time.timeScaleが0でもTweenを進行させる")]
    [SerializeField]
    private bool _useUnscaledTime;

    private float _intensity;
    private Tween _tween;

    private void Awake()
    {
        ApplySettings();
        SetIntensity(0f);
    }

    private void OnEnable()
    {
        ApplySettings();
        SetIntensity(_intensity);
    }

    private void OnValidate()
    {
        _playIntensity = Mathf.Clamp01(_playIntensity);

        _shadowThreshold = Mathf.Clamp01(
            _shadowThreshold
        );

        _highlightThreshold = Mathf.Clamp01(
            _highlightThreshold
        );

        // 順序が逆転しないように最低限の間隔を確保する。
        if (_highlightThreshold <= _shadowThreshold)
        {
            _highlightThreshold = Mathf.Min(
                1f,
                _shadowThreshold + 0.01f
            );
        }

        _blendSoftness = Mathf.Clamp(
            _blendSoftness,
            0.001f,
            0.5f
        );

        _playDuration = Mathf.Max(
            0f,
            _playDuration
        );

        _stopDuration = Mathf.Max(
            0f,
            _stopDuration
        );

        if (Application.isPlaying)
        {
            ApplySettings();
        }
    }

    private void OnDisable()
    {
        KillTween();
        SetIntensity(0f);
    }

    private void OnDestroy()
    {
        KillTween();
    }

    /// <summary>
    /// 現在の強度から_playIntensityまで遷移する。
    /// </summary>
    public void Play()
    {
        ApplySettings();

        TweenTo(
            _playIntensity,
            _playDuration,
            _playEase
        );
    }

    /// <summary>
    /// 現在の強度から0まで遷移する。
    /// </summary>
    public void Stop()
    {
        TweenTo(
            0f,
            _stopDuration,
            _stopEase
        );
    }

    /// <summary>
    /// Tweenを使用せず即座に停止する。
    /// </summary>
    public void StopImmediate()
    {
        KillTween();
        SetIntensity(0f);
    }

    /// <summary>
    /// 実行中に3色を変更する。
    /// </summary>
    public void SetColors(
        Color shadow,
        Color midtone,
        Color highlight
    )
    {
        _shadowColor = shadow;
        _midtoneColor = midtone;
        _highlightColor = highlight;

        ApplyColors();
    }

    private void TweenTo(
        float target,
        float duration,
        Ease ease
    )
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        KillTween();

        target = Mathf.Clamp01(target);

        if (duration <= 0f)
        {
            SetIntensity(target);
            return;
        }

        _tween = DOTween
            .To(
                () => _intensity,
                SetIntensity,
                target,
                duration
            )
            .SetEase(ease)
            .SetUpdate(_useUnscaledTime)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            )
            .OnComplete(() => _tween = null);
    }

    private void SetIntensity(float value)
    {
        _intensity = Mathf.Clamp01(value);

        Shader.SetGlobalFloat(
            IntensityId,
            _intensity
        );
    }

    private void ApplySettings()
    {
        ApplyColors();

        Shader.SetGlobalFloat(
            ShadowThresholdId,
            _shadowThreshold
        );

        Shader.SetGlobalFloat(
            HighlightThresholdId,
            _highlightThreshold
        );

        Shader.SetGlobalFloat(
            BlendSoftnessId,
            _blendSoftness
        );
    }

    private void ApplyColors()
    {
        Shader.SetGlobalColor(
            ShadowColorId,
            _shadowColor
        );

        Shader.SetGlobalColor(
            MidtoneColorId,
            _midtoneColor
        );

        Shader.SetGlobalColor(
            HighlightColorId,
            _highlightColor
        );
    }

    private void KillTween()
    {
        if (_tween == null)
        {
            return;
        }

        _tween.Kill();
        _tween = null;
    }
}