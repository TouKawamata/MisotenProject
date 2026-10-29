using DG.Tweening;
using UnityEngine;

/// <summary>
/// 画面のRGBチャンネルを分離する色収差エフェクト。
///
/// Full Screen Pass Renderer Featureで使用するShaderへ、
/// グローバルShaderプロパティを渡す。
/// </summary>
public sealed class ChromaticAberrationEffect : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_ChromaticAberrationIntensity");

    private static readonly int DirectionId =
        Shader.PropertyToID("_ChromaticAberrationDirection");

    private static readonly int CenterId =
        Shader.PropertyToID("_ChromaticAberrationCenter");

    private static readonly int RadialId =
        Shader.PropertyToID("_ChromaticAberrationRadial");

    private static readonly int EdgePowerId =
        Shader.PropertyToID("_ChromaticAberrationEdgePower");

    private static readonly int PreserveCenterId =
        Shader.PropertyToID("_ChromaticAberrationPreserveCenter");

    [Header("強さ")]
    [Tooltip("再生中の色収差の強さ。UV座標上のずれ量")]
    [SerializeField, Range(0f, 0.1f)]
    private float _playIntensity = 0.015f;

    [Header("方向")]
    [Tooltip("Radialを無効にした場合のRGB分離方向")]
    [SerializeField]
    private Vector2 _direction = new Vector2(1f, 0f);

    [Header("放射状")]
    [Tooltip("中心から外側へ向けてRGBを分離する")]
    [SerializeField]
    private bool _radial = true;

    [Tooltip("放射状エフェクトの中心。通常は画面中央の0.5, 0.5")]
    [SerializeField]
    private Vector2 _center = new Vector2(0.5f, 0.5f);

    [Tooltip("画面中心から外側に向かって強くなる度合い")]
    [SerializeField, Range(0.1f, 5f)]
    private float _edgePower = 1.5f;

    [Tooltip("画面中央付近への色収差を抑える")]
    [SerializeField, Range(0f, 1f)]
    private float _preserveCenter = 0.15f;

    [Header("Play")]
    [Tooltip("色収差を最大強度まで上げる秒数")]
    [SerializeField, Min(0f)]
    private float _playDuration = 0.2f;

    [SerializeField]
    private Ease _playEase = Ease.OutCubic;

    [Header("Stop")]
    [Tooltip("色収差を0へ戻す秒数")]
    [SerializeField, Min(0f)]
    private float _stopDuration = 0.35f;

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
        _playIntensity = Mathf.Clamp(
            _playIntensity,
            0f,
            0.1f
        );

        _center.x = Mathf.Clamp01(_center.x);
        _center.y = Mathf.Clamp01(_center.y);

        _edgePower = Mathf.Clamp(
            _edgePower,
            0.1f,
            5f
        );

        _preserveCenter = Mathf.Clamp01(
            _preserveCenter
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
    /// 現在の強度から設定された強度まで遷移する。
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
    /// 実行中に色収差の強度を変更する。
    /// Play()を呼ぶと、この強度までTweenする。
    /// </summary>
    public void SetPlayIntensity(float value)
    {
        _playIntensity = Mathf.Clamp(
            value,
            0f,
            0.1f
        );
    }

    /// <summary>
    /// 実行中に色収差の中心を変更する。
    /// </summary>
    public void SetCenter(Vector2 center)
    {
        _center = new Vector2(
            Mathf.Clamp01(center.x),
            Mathf.Clamp01(center.y)
        );

        Shader.SetGlobalVector(
            CenterId,
            _center
        );
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

        target = Mathf.Clamp(
            target,
            0f,
            0.1f
        );

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
        _intensity = Mathf.Clamp(
            value,
            0f,
            0.1f
        );

        Shader.SetGlobalFloat(
            IntensityId,
            _intensity
        );
    }

    private void ApplySettings()
    {
        Vector2 normalizedDirection =
            _direction.sqrMagnitude > 0.0001f
                ? _direction.normalized
                : Vector2.right;

        Shader.SetGlobalVector(
            DirectionId,
            normalizedDirection
        );

        Shader.SetGlobalVector(
            CenterId,
            _center
        );

        Shader.SetGlobalFloat(
            RadialId,
            _radial ? 1f : 0f
        );

        Shader.SetGlobalFloat(
            EdgePowerId,
            _edgePower
        );

        Shader.SetGlobalFloat(
            PreserveCenterId,
            _preserveCenter
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