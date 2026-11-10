using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 画面系Effectをまとめて管理するController。
///
/// Inspectorでプリセットごとに使用するEffectを選択し、
/// Play / Stopをまとめて実行する。
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenNoiseEffectController : MonoBehaviour
{
    [Serializable]
    public sealed class EffectPreset
    {
        [Tooltip("Inspectorに表示するプリセット名")]
        [SerializeField]
        private string _displayName = "New Preset";

        [Header("Noise Effects")]

        [Tooltip("GrainNoise")]
        [SerializeField]
        private bool _useGrainNoise;

        [Tooltip("TriTone")]
        [SerializeField]
        private bool _useTriTone;

        [Tooltip("ChromaticAberration")]
        [SerializeField]
        private bool _useChromaticAberration;

        [Tooltip("RGBBlockNoise")]
        [SerializeField]
        private bool _useRGBBlockNoise;

        [Header("Glitch Effects")]

        [Tooltip("ベーススキャンライン")]
        [SerializeField]
        private bool _useBaseScanLine;

        [Tooltip("上方向へ移動するスキャンライン")]
        [SerializeField]
        private bool _useScanLineUp;

        [Tooltip("波形ワープ")]
        [SerializeField]
        private bool _useWaveformWarp;

        [Tooltip("変形ライン")]
        [SerializeField]
        private bool _useDeformLine;

        public string DisplayName =>
            _displayName;

        public bool UseGrainNoise =>
            _useGrainNoise;

        public bool UseTriTone =>
            _useTriTone;

        public bool UseChromaticAberration =>
            _useChromaticAberration;

        public bool UseRGBBlockNoise =>
            _useRGBBlockNoise;

        public bool UseBaseScanLine =>
            _useBaseScanLine;

        public bool UseScanLineUp =>
            _useScanLineUp;

        public bool UseWaveformWarp =>
            _useWaveformWarp;

        public bool UseDeformLine =>
            _useDeformLine;
    }

    // =========================================================
    // Effect参照
    // =========================================================

    [Header("Noise Effect参照")]

    [SerializeField]
    private GrainNoiseEffect _grainNoise;

    [SerializeField]
    private TriToneEffect _triTone;

    [SerializeField]
    private ChromaticAberrationEffect
        _chromaticAberration;

    [SerializeField]
    private RGBBlockNoiseEffect _rgbBlockNoise;

    [Header("Glitch Effect参照")]

    [Tooltip(
        "BaseScanLine / ScanLineUp / WaveformWarp / " +
        "DeformLineを管理するController"
    )]
    [SerializeField]
    private GlitchEffectsController _glitchEffects;

    // =========================================================
    // Preset
    // =========================================================

    [Header("プリセット")]

    [SerializeField]
    private List<EffectPreset> _presets =
        new List<EffectPreset>();

    [Tooltip("現在選択しているプリセット")]
    [SerializeField, Min(0)]
    private int _selectedPresetIndex;

    // =========================================================
    // Settings
    // =========================================================

    [Header("切り替え設定")]

    [Tooltip(
        "プリセット切り替え時、" +
        "新しいプリセットに含まれないEffectを停止する"
    )]
    [SerializeField]
    private bool _stopUnusedEffects = true;

    [Tooltip(
        "Controllerが無効になったとき、" +
        "すべてのEffectを停止する"
    )]
    [SerializeField]
    private bool _stopAllOnDisable = true;

    // =========================================================
    // Runtime
    // =========================================================

    private int _playingPresetIndex = -1;
    private bool _isPlaying;

    // =========================================================
    // Properties
    // =========================================================

    public int PresetCount =>
        _presets != null
            ? _presets.Count
            : 0;

    public int SelectedPresetIndex =>
        _selectedPresetIndex;

    public bool IsPlaying =>
        _isPlaying;

    public string SelectedPresetName
    {
        get
        {
            EffectPreset preset =
                GetPreset(_selectedPresetIndex);

            return preset != null
                ? preset.DisplayName
                : "未選択";
        }
    }

    public string PlayingPresetName
    {
        get
        {
            if (!_isPlaying)
            {
                return "停止中";
            }

            EffectPreset preset =
                GetPreset(_playingPresetIndex);

            return preset != null
                ? preset.DisplayName
                : "停止中";
        }
    }

    // =========================================================
    // Unity
    // =========================================================

    private void Reset()
    {
        AutoFindEffects();
    }

    private void Awake()
    {
        ClampSelectedPresetIndex();
    }

    private void OnValidate()
    {
        ClampSelectedPresetIndex();
    }

    private void OnDisable()
    {
        if (!_stopAllOnDisable)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        StopAll();
    }

    // =========================================================
    // Public
    // =========================================================

    /// <summary>
    /// Inspectorで選択されているプリセットを再生する。
    /// </summary>
    public void Play()
    {
        PlayPreset(_selectedPresetIndex);
    }

    /// <summary>
    /// 現在再生中のプリセットを停止する。
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying)
        {
            return;
        }

        EffectPreset preset =
            GetPreset(_playingPresetIndex);

        if (preset == null)
        {
            StopAll();
            return;
        }

        StopPresetEffects(preset);

        _playingPresetIndex = -1;
        _isPlaying = false;
    }

    /// <summary>
    /// 管理しているすべてのEffectを停止する。
    /// </summary>
    public void StopAll()
    {
        if (_grainNoise != null)
        {
            _grainNoise.Stop();
        }

        if (_triTone != null)
        {
            _triTone.Stop();
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.Stop();
        }

        if (_rgbBlockNoise != null)
        {
            _rgbBlockNoise.Stop();
        }

        if (_glitchEffects != null)
        {
            _glitchEffects.StopAllEffects();
        }

        _playingPresetIndex = -1;
        _isPlaying = false;
    }

    /// <summary>
    /// プリセットを選択する。
    /// 選択するだけで再生はしない。
    /// </summary>
    public void SelectPreset(int index)
    {
        if (_presets == null ||
            _presets.Count == 0)
        {
            _selectedPresetIndex = 0;
            return;
        }

        _selectedPresetIndex = Mathf.Clamp(
            index,
            0,
            _presets.Count - 1
        );
    }

    /// <summary>
    /// 指定番号のプリセットを再生する。
    /// </summary>
    public void PlayPreset(int index)
    {
        EffectPreset preset =
            GetPreset(index);

        if (preset == null)
        {
            Debug.LogWarning(
                "ScreenNoiseEffectController: " +
                $"Preset {index} は存在しません。",
                this
            );

            return;
        }

        _selectedPresetIndex = index;

        ApplyPreset(preset);

        _playingPresetIndex = index;
        _isPlaying = true;
    }

    /// <summary>
    /// 名前からプリセットを再生する。
    /// </summary>
    public void PlayPreset(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return;
        }

        if (_presets == null)
        {
            return;
        }

        for (int i = 0; i < _presets.Count; i++)
        {
            EffectPreset preset =
                _presets[i];

            if (preset == null)
            {
                continue;
            }

            if (string.Equals(
                preset.DisplayName,
                presetName,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                PlayPreset(i);
                return;
            }
        }

        Debug.LogWarning(
            "ScreenNoiseEffectController: " +
            $"Preset「{presetName}」が見つかりません。",
            this
        );
    }

    /// <summary>
    /// 次のプリセットを再生する。
    /// </summary>
    public void PlayNext()
    {
        if (_presets == null ||
            _presets.Count == 0)
        {
            return;
        }

        int next =
            (_selectedPresetIndex + 1)
            % _presets.Count;

        PlayPreset(next);
    }

    /// <summary>
    /// 前のプリセットを再生する。
    /// </summary>
    public void PlayPrevious()
    {
        if (_presets == null ||
            _presets.Count == 0)
        {
            return;
        }

        int previous =
            _selectedPresetIndex - 1;

        if (previous < 0)
        {
            previous =
                _presets.Count - 1;
        }

        PlayPreset(previous);
    }

    /// <summary>
    /// 同一GameObjectまたは子階層から参照を自動取得する。
    /// </summary>
    public void AutoFindEffects()
    {
        if (_grainNoise == null)
        {
            _grainNoise =
                GetComponentInChildren<
                    GrainNoiseEffect
                >(true);
        }

        if (_triTone == null)
        {
            _triTone =
                GetComponentInChildren<
                    TriToneEffect
                >(true);
        }

        if (_chromaticAberration == null)
        {
            _chromaticAberration =
                GetComponentInChildren<
                    ChromaticAberrationEffect
                >(true);
        }

        if (_rgbBlockNoise == null)
        {
            _rgbBlockNoise =
                GetComponentInChildren<
                    RGBBlockNoiseEffect
                >(true);
        }

        if (_glitchEffects == null)
        {
            _glitchEffects =
                GetComponentInChildren<
                    GlitchEffectsController
                >(true);
        }
    }

    // =========================================================
    // Apply
    // =========================================================

    private void ApplyPreset(
        EffectPreset preset
    )
    {
        ApplyGrainNoise(
            preset.UseGrainNoise
        );

        ApplyTriTone(
            preset.UseTriTone
        );

        ApplyChromaticAberration(
            preset.UseChromaticAberration
        );

        ApplyRGBBlockNoise(
            preset.UseRGBBlockNoise
        );

        ApplyGlitchEffects(preset);
    }

    // =========================================================
    // Standard Effects
    // =========================================================

    private void ApplyGrainNoise(
        bool shouldPlay
    )
    {
        if (_grainNoise == null)
        {
            return;
        }

        if (shouldPlay)
        {
            _grainNoise.Play();
        }
        else if (_stopUnusedEffects)
        {
            _grainNoise.Stop();
        }
    }

    private void ApplyTriTone(
        bool shouldPlay
    )
    {
        if (_triTone == null)
        {
            return;
        }

        if (shouldPlay)
        {
            _triTone.Play();
        }
        else if (_stopUnusedEffects)
        {
            _triTone.Stop();
        }
    }

    private void ApplyChromaticAberration(
        bool shouldPlay
    )
    {
        if (_chromaticAberration == null)
        {
            return;
        }

        if (shouldPlay)
        {
            _chromaticAberration.Play();
        }
        else if (_stopUnusedEffects)
        {
            _chromaticAberration.Stop();
        }
    }

    private void ApplyRGBBlockNoise(
        bool shouldPlay
    )
    {
        if (_rgbBlockNoise == null)
        {
            return;
        }

        if (shouldPlay)
        {
            _rgbBlockNoise.Play();
        }
        else if (_stopUnusedEffects)
        {
            _rgbBlockNoise.Stop();
        }
    }

    // =========================================================
    // Glitch Effects
    // =========================================================

    private void ApplyGlitchEffects(
        EffectPreset preset
    )
    {
        if (_glitchEffects == null)
        {
            return;
        }

        bool useAny =
            preset.UseBaseScanLine ||
            preset.UseScanLineUp ||
            preset.UseWaveformWarp ||
            preset.UseDeformLine;

        // GlitchEffectsControllerは内部で4Effectをまとめて
        // 管理しているため、プリセット切り替え時には
        // 一度リセットしてから再生する。
        if (_stopUnusedEffects)
        {
            _glitchEffects.StopAllEffects();
        }

        if (!useAny)
        {
            return;
        }

        if (preset.UseBaseScanLine)
        {
            _glitchEffects.PlayBaseScanLine();
        }

        if (preset.UseScanLineUp)
        {
            _glitchEffects.PlayScanLineUp();
        }

        if (preset.UseWaveformWarp)
        {
            _glitchEffects.PlayWaveformWarp();
        }

        if (preset.UseDeformLine)
        {
            _glitchEffects.PlayDeformLine();
        }
    }

    // =========================================================
    // Stop
    // =========================================================

    private void StopPresetEffects(
        EffectPreset preset
    )
    {
        if (preset.UseGrainNoise &&
            _grainNoise != null)
        {
            _grainNoise.Stop();
        }

        if (preset.UseTriTone &&
            _triTone != null)
        {
            _triTone.Stop();
        }

        if (preset.UseChromaticAberration &&
            _chromaticAberration != null)
        {
            _chromaticAberration.Stop();
        }

        if (preset.UseRGBBlockNoise &&
            _rgbBlockNoise != null)
        {
            _rgbBlockNoise.Stop();
        }

        bool usesGlitch =
            preset.UseBaseScanLine ||
            preset.UseScanLineUp ||
            preset.UseWaveformWarp ||
            preset.UseDeformLine;

        if (usesGlitch &&
            _glitchEffects != null)
        {
            _glitchEffects.StopAllEffects();
        }
    }

    // =========================================================
    // Utility
    // =========================================================

    private EffectPreset GetPreset(
        int index
    )
    {
        if (_presets == null)
        {
            return null;
        }

        if (index < 0 ||
            index >= _presets.Count)
        {
            return null;
        }

        return _presets[index];
    }

    private void ClampSelectedPresetIndex()
    {
        if (_presets == null ||
            _presets.Count == 0)
        {
            _selectedPresetIndex = 0;

            return;
        }

        _selectedPresetIndex = Mathf.Clamp(
            _selectedPresetIndex,
            0,
            _presets.Count - 1
        );
    }
}