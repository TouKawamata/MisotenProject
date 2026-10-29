using DG.Tweening;
using UnityEngine;

/// <summary>
/// ランダムな矩形領域にRGB分離と水平方向のずれを発生させる。
///
/// Full Screen Pass Renderer Featureで使用するShaderへ、
/// グローバルShaderプロパティを渡す。
/// </summary>
public sealed class RGBBlockNoiseEffect : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_RGBBlockNoiseIntensity");

    private static readonly int BlockCountId =
        Shader.PropertyToID("_RGBBlockNoiseBlockCount");

    private static readonly int DensityId =
        Shader.PropertyToID("_RGBBlockNoiseDensity");

    private static readonly int DisplacementId =
        Shader.PropertyToID("_RGBBlockNoiseDisplacement");

    private static readonly int ColorSeparationId =
        Shader.PropertyToID("_RGBBlockNoiseColorSeparation");

    private static readonly int SeedId =
        Shader.PropertyToID("_RGBBlockNoiseSeed");

    private static readonly int VerticalInfluenceId =
        Shader.PropertyToID("_RGBBlockNoiseVerticalInfluence");

    [Header("強さ")]
    [Tooltip("再生中のRGBブロックノイズの強さ")]
    [SerializeField, Range(0f, 1f)]
    private float _playIntensity = 1f;

    [Header("ブロック")]
    [Tooltip("画面を横方向と縦方向に分割する数")]
    [SerializeField]
    private Vector2 _blockCount = new Vector2(12f, 36f);

    [Tooltip("ノイズが表示されるブロックの割合")]
    [SerializeField, Range(0f, 1f)]
    private float _density = 0.25f;

    [Header("ずれ")]
    [Tooltip("ブロック全体の最大移動量。UV座標基準")]
    [SerializeField, Range(0f, 0.2f)]
    private float _displacement = 0.04f;

    [Tooltip("RGBチャンネル間の最大分離量。UV座標基準")]
    [SerializeField, Range(0f, 0.1f)]
    private float _colorSeparation = 0.012f;

    [Tooltip("縦方向のずれ量。0なら横方向のみ")]
    [SerializeField, Range(0f, 1f)]
    private float _verticalInfluence = 0.1f;

    [Header("アニメーション")]
    [Tooltip("ノイズパターンを一定間隔で更新する")]
    [SerializeField]
    private bool _animateNoise = true;

    [Tooltip("1秒間にノイズパターンを更新する回数")]
    [SerializeField, Range(1f, 60f)]
    private float _noiseFrequency = 18f;

    [Header("Play")]
    [Tooltip("ノイズを設定強度まで上げる秒数")]
    [SerializeField, Min(0f)]
    private float _playDuration = 0.08f;

    [SerializeField]
    private Ease _playEase = Ease.OutCubic;

    [Header("Stop")]
    [Tooltip("ノイズを0へ戻す秒数")]
    [SerializeField, Min(0f)]
    private float _stopDuration = 0.2f;

    [SerializeField]
    private Ease _stopEase = Ease.InOutSine;

    [Header("時間")]
    [Tooltip("Time.timeScaleが0でもTweenとノイズ更新を進める")]
    [SerializeField]
    private bool _useUnscaledTime;

    private float _intensity;
    private float _animationTime;
    private float _seed;
    private Tween _tween;

    private void Awake()
    {
        ApplySettings();
        SetSeed(0f);
        SetIntensity(0f);
    }

    private void OnEnable()
    {
        ApplySettings();
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

        _animationTime += deltaTime;

        float interval = 1f / Mathf.Max(1f, _noiseFrequency);

        if (_animationTime < interval)
        {
            return;
        }

        // フレームレートに依存しないよう、更新間隔分だけ減算する。
        _animationTime %= interval;

        // Seedを整数単位で進めることで、パターンを瞬間的に切り替える。
        _seed += 1f;

        if (_seed >= 10000f)
        {
            _seed = 0f;
        }

        SetSeed(_seed);
    }

    private void OnValidate()
    {
        _playIntensity = Mathf.Clamp01(_playIntensity);

        _blockCount.x = Mathf.Max(1f, _blockCount.x);
        _blockCount.y = Mathf.Max(1f, _blockCount.y);

        _density = Mathf.Clamp01(_density);

        _displacement = Mathf.Clamp(
            _displacement,
            0f,
            0.2f
        );

        _colorSeparation = Mathf.Clamp(
            _colorSeparation,
            0f,
            0.1f
        );

        _verticalInfluence = Mathf.Clamp01(
            _verticalInfluence
        );

        _noiseFrequency = Mathf.Clamp(
            _noiseFrequency,
            1f,
            60f
        );

        _playDuration = Mathf.Max(0f, _playDuration);
        _stopDuration = Mathf.Max(0f, _stopDuration);

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

        // 再生するたびに少し異なるパターンから開始する。
        _seed += 17f;
        SetSeed(_seed);

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
    /// ノイズパターンを即座に更新する。
    /// </summary>
    public void RefreshNoise()
    {
        _seed += 1f;
        SetSeed(_seed);
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

    private void SetSeed(float value)
    {
        Shader.SetGlobalFloat(
            SeedId,
            value
        );
    }

    private void ApplySettings()
    {
        Shader.SetGlobalVector(
            BlockCountId,
            new Vector4(
                Mathf.Max(1f, _blockCount.x),
                Mathf.Max(1f, _blockCount.y),
                0f,
                0f
            )
        );

        Shader.SetGlobalFloat(
            DensityId,
            _density
        );

        Shader.SetGlobalFloat(
            DisplacementId,
            _displacement
        );

        Shader.SetGlobalFloat(
            ColorSeparationId,
            _colorSeparation
        );

        Shader.SetGlobalFloat(
            VerticalInfluenceId,
            _verticalInfluence
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