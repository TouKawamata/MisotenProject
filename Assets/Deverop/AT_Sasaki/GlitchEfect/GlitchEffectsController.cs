using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class GlitchEffectsController : MonoBehaviour
{
    [Header("Glitch Material")]
    [SerializeField] private Material glitchMaterial;

    [Header("Effect Duration")]
    [SerializeField] private float effectDuration = 1.0f;

    // Shader Property IDs
    private static readonly int BaseIntensity =
        Shader.PropertyToID("_BaseScanLineIntensity");

    private static readonly int ScanUpIntensity =
        Shader.PropertyToID("_ScanLineUpIntensity");

    private static readonly int ScanPosition =
        Shader.PropertyToID("_ScanPosition");

    private static readonly int WaveIntensity =
        Shader.PropertyToID("_WaveformWarpIntensity");

    private static readonly int WaveTime =
        Shader.PropertyToID("_WaveTime");

    private static readonly int DeformIntensity =
        Shader.PropertyToID("_DeformLineIntensity");

    private static readonly int DeformPosition =
        Shader.PropertyToID("_DeformLinePosition");

    // Tweens
    private Tween baseTween;
    private Tween scanIntensityTween;
    private Tween scanPositionTween;
    private Tween waveIntensityTween;
    private Tween waveTimeTween;
    private Tween deformIntensityTween;
    private Tween deformPositionTween;

    private void Awake()
    {
        if (glitchMaterial == null)
        {
            Debug.LogError("GlitchMaterialが設定されていません。", this);
            enabled = false;
            return;
        }

        ResetEffects();
    }

    // キーボード操作
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            PlayBaseScanLine();

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            PlayScanLineUp();

        if (Keyboard.current.digit7Key.wasPressedThisFrame)
            PlayWaveformWarp();

        if (Keyboard.current.digit8Key.wasPressedThisFrame)
            PlayDeformLine();

        if (Keyboard.current.digit9Key.wasPressedThisFrame)
            PlayAllEffects();

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
            StopAllEffects();
    }

    // ① Base Scan Line
    public void PlayBaseScanLine()
    {
        baseTween?.Kill();

        glitchMaterial.SetFloat(BaseIntensity, 0f);

        baseTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(BaseIntensity),
                x => glitchMaterial.SetFloat(BaseIntensity, x),
                1f,
                effectDuration * 0.3f
            ))
            .AppendInterval(effectDuration * 0.4f)
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(BaseIntensity),
                x => glitchMaterial.SetFloat(BaseIntensity, x),
                0f,
                effectDuration * 0.3f
            ));
    }

    // ② Scan Line Up
    public void PlayScanLineUp()
    {
        scanIntensityTween?.Kill();
        scanPositionTween?.Kill();

        glitchMaterial.SetFloat(ScanUpIntensity, 0f);
        glitchMaterial.SetFloat(ScanPosition, 0f);

        scanIntensityTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(ScanUpIntensity),
                x => glitchMaterial.SetFloat(ScanUpIntensity, x),
                1f,
                effectDuration * 0.2f
            ))
            .AppendInterval(effectDuration * 0.6f)
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(ScanUpIntensity),
                x => glitchMaterial.SetFloat(ScanUpIntensity, x),
                0f,
                effectDuration * 0.2f
            ));

        scanPositionTween = DOTween.To(
            () => glitchMaterial.GetFloat(ScanPosition),
            x => glitchMaterial.SetFloat(ScanPosition, x),
            1f,
            effectDuration
        );
    }

    // ③ Waveform Warp
    public void PlayWaveformWarp()
    {
        waveIntensityTween?.Kill();
        waveTimeTween?.Kill();

        glitchMaterial.SetFloat(WaveIntensity, 0f);
        glitchMaterial.SetFloat(WaveTime, 0f);

        waveIntensityTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(WaveIntensity),
                x => glitchMaterial.SetFloat(WaveIntensity, x),
                1f,
                effectDuration * 0.2f
            ))
            .AppendInterval(effectDuration * 0.6f)
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(WaveIntensity),
                x => glitchMaterial.SetFloat(WaveIntensity, x),
                0f,
                effectDuration * 0.2f
            ));

        waveTimeTween = DOTween.To(
            () => glitchMaterial.GetFloat(WaveTime),
            x => glitchMaterial.SetFloat(WaveTime, x),
            Mathf.PI * 2f,
            effectDuration
        );
    }

    // ④ Deform Line
    public void PlayDeformLine()
    {
        deformIntensityTween?.Kill();
        deformPositionTween?.Kill();

        glitchMaterial.SetFloat(DeformIntensity, 0f);
        glitchMaterial.SetFloat(DeformPosition, 0f);

        deformIntensityTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(DeformIntensity),
                x => glitchMaterial.SetFloat(DeformIntensity, x),
                1f,
                effectDuration * 0.2f
            ))
            .AppendInterval(effectDuration * 0.6f)
            .Append(DOTween.To(
                () => glitchMaterial.GetFloat(DeformIntensity),
                x => glitchMaterial.SetFloat(DeformIntensity, x),
                0f,
                effectDuration * 0.2f
            ));

        deformPositionTween = DOTween.To(
            () => glitchMaterial.GetFloat(DeformPosition),
            x => glitchMaterial.SetFloat(DeformPosition, x),
            1f,
            effectDuration
        );
    }

    // 4種類を同時再生
    public void PlayAllEffects()
    {
        StopAllEffects();

        PlayBaseScanLine();
        PlayScanLineUp();
        PlayWaveformWarp();
        PlayDeformLine();
    }

    // すべての演出を停止
    public void StopAllEffects()
    {
        baseTween?.Kill();
        scanIntensityTween?.Kill();
        scanPositionTween?.Kill();
        waveIntensityTween?.Kill();
        waveTimeTween?.Kill();
        deformIntensityTween?.Kill();
        deformPositionTween?.Kill();

        ResetEffects();
    }

    // Shaderの値を初期化
    private void ResetEffects()
    {
        glitchMaterial.SetFloat(BaseIntensity, 0f);

        glitchMaterial.SetFloat(ScanUpIntensity, 0f);
        glitchMaterial.SetFloat(ScanPosition, 0f);

        glitchMaterial.SetFloat(WaveIntensity, 0f);
        glitchMaterial.SetFloat(WaveTime, 0f);

        glitchMaterial.SetFloat(DeformIntensity, 0f);
        glitchMaterial.SetFloat(DeformPosition, 0f);
    }

    private void OnDisable()
    {
        if (glitchMaterial != null)
        {
            StopAllEffects();
        }
    }
}