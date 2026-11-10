using DG.Tweening;
using UnityEngine;

/// <summary>
/// 全画面にフィルムグレイン風ノイズを加える演出。
///
/// Full Screen Pass Renderer Featureで使用するシェーダーへ、
/// グローバルシェーダーパラメーターを渡す。
///
/// Play / Stopは外部のPresentation系クラスから呼び出す。
/// </summary>
public sealed class GrainNoiseEffect : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_GrainNoiseIntensity");

    private static readonly int ScaleId =
        Shader.PropertyToID("_GrainNoiseScale");

    private static readonly int SeedId =
        Shader.PropertyToID("_GrainNoiseSeed");

    [Header("強さ")]
    [Tooltip("再生中のグレインノイズの強さ")]
    [SerializeField, Range(0f, 1f)]
    private float _playIntensity = 0.18f;

    [Header("ノイズ設定")]
    [Tooltip("ノイズ粒子の大きさ。1で1ピクセル相当。値が大きいほど粒が大きくなる")]
    [SerializeField, Min(1f)]
    private float _grainScale = 1f;

    [Tooltip("ノイズパターンを毎フレーム更新する")]
    [SerializeField]
    private bool _animateNoise = true;

    [Tooltip("ノイズパターンの変化速度")]
    [SerializeField, Min(0f)]
    private float _noiseSpeed = 24f;

    [Header("Play")]
    [Tooltip("ノイズを最大強度まで上げる秒数")]
    [SerializeField, Min(0f)]
    private float _playDuration = 0.2f;

    [SerializeField]
    private Ease _playEase = Ease.OutCubic;

    [Header("Stop")]
    [Tooltip("ノイズの強度を0へ戻す秒数")]
    [SerializeField, Min(0f)]
    private float _stopDuration = 0.35f;

    [SerializeField]
    private Ease _stopEase = Ease.InOutSine;

    [Header("時間")]
    [Tooltip("Time.timeScaleの影響を受けずにトゥイーンさせる")]
    [SerializeField]
    private bool _useUnscaledTime;

    private float _intensity;
    private float _seed;
    private Tween _tween;

    private void Awake()
    {
        ApplyScale();
        SetSeed(0f);
        SetIntensity(0f);
    }

    private void OnEnable()
    {
        ApplyScale();
        SetSeed(_seed);
        SetIntensity(_intensity);
    }

    private void Update()
    {
        if (!_animateNoise || _intensity <= 0f)
        {
            return;
        }

        float deltaTime = _useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        _seed += deltaTime * _noiseSpeed;

        // 長時間動作時のfloat精度低下を避ける。
        if (_seed >= 10000f)
        {
            _seed -= 10000f;
        }

        SetSeed(_seed);
    }

    private void OnValidate()
    {
        _playIntensity = Mathf.Clamp01(_playIntensity);
        _grainScale = Mathf.Max(1f, _grainScale);
        _noiseSpeed = Mathf.Max(0f, _noiseSpeed);
        _playDuration = Mathf.Max(0f, _playDuration);
        _stopDuration = Mathf.Max(0f, _stopDuration);

        if (Application.isPlaying)
        {
            ApplyScale();
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
    /// グレインノイズを再生する。
    /// 現在の強度から_playIntensityまで遷移する。
    /// </summary>
    public void Play()
    {
        TweenTo(
            _playIntensity,
            _playDuration,
            _playEase
        );
    }

    /// <summary>
    /// グレインノイズを停止する。
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
    /// 即座に停止する。
    /// シーン切り替えや状態リセット用。
    /// </summary>
    public void StopImmediate()
    {
        KillTween();
        SetIntensity(0f);
    }

    /// <summary>
    /// ノイズ粒子の大きさを実行中に変更する。
    /// </summary>
    public void SetGrainScale(float scale)
    {
        _grainScale = Mathf.Max(1f, scale);
        ApplyScale();
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
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => _tween = null);
    }

    private void SetIntensity(float value)
    {
        _intensity = Mathf.Clamp01(value);

        Debug.Log($"Grain Intensity = {_intensity}");

        Shader.SetGlobalFloat(
            IntensityId,
            _intensity
        );
    }

    private void ApplyScale()
    {
        Shader.SetGlobalFloat(
            ScaleId,
            Mathf.Max(1f, _grainScale)
        );
    }

    private void SetSeed(float value)
    {
        Shader.SetGlobalFloat(
            SeedId,
            value
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