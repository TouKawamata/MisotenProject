using UnityEngine;

// カメラの揺れの演出。Playで揺れ始め、だんだん小さくなって止まる（振れ幅＝強さ×最大の振れ幅×（1 − 経過の割合）²）。
// 揺れはPerlinノイズで作り、ICameraShakeView経由でCameraControllerに渡すだけ（カメラのTransformは触らない）。
// ヒットストップ中も揺らすため、実時間（unscaledDeltaTime）で進める。ShieldBlockPresentationから呼ばれる。
public class CameraShakeEffect : MonoBehaviour
{
    // 横と縦のノイズが同じ形にならないよう、ノイズを読む位置をずらす量。
    private const float VerticalNoiseOffset = 17.3f;

    [Tooltip("揺れを足す相手（ICameraShakeViewとしてだけ使う）")]
    [SerializeField] private CameraController _cameraController;

    [Tooltip("強さ1のときの最大の振れ幅（m。カメラの左右・上下）")]
    [SerializeField] private float _maxAmplitude = 0.3f;
    [Tooltip("揺れの細かさ。大きいほど速く揺れる")]
    [SerializeField] private float _frequency = 25f;
    [Tooltip("揺れが止まるまでの秒数（実時間）")]
    [SerializeField] private float _duration = 0.35f;

    private ICameraShakeView _view;
    private float _amplitude;
    private float _elapsed;
    private float _noiseSeed;

    private void Awake()
    {
        if (_cameraController == null)
        {
            Debug.LogError("CameraShakeEffect: Camera Controller が未設定です", this);
            enabled = false;
            return;
        }

        _view = _cameraController;
        _noiseSeed = Random.value * 100f;
    }

    private void OnDisable()
    {
        _amplitude = 0f;
        if (_view != null)
        {
            _view.ShakeOffset = Vector3.zero;
        }
    }

    private void Update()
    {
        if (_amplitude <= 0f)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        float amplitude = GetCurrentAmplitude();
        if (amplitude <= 0f)
        {
            _amplitude = 0f;
            _view.ShakeOffset = Vector3.zero;
            return;
        }

        float noiseTime = _elapsed * _frequency;
        float x = (Mathf.PerlinNoise(_noiseSeed, noiseTime) * 2f - 1f) * amplitude;
        float y = (Mathf.PerlinNoise(_noiseSeed + VerticalNoiseOffset, noiseTime) * 2f - 1f) * amplitude;
        _view.ShakeOffset = new Vector3(x, y, 0f);
    }

    // strength：0～1。揺れている途中で呼ばれたら、今の揺れより強いときだけ揺れ直す。
    public void Play(float strength)
    {
        if (!enabled)
        {
            return;
        }

        float amplitude = Mathf.Clamp01(strength) * _maxAmplitude;
        if (amplitude <= 0f || amplitude < GetCurrentAmplitude())
        {
            return;
        }

        _amplitude = amplitude;
        _elapsed = 0f;
    }

    // 調整用。Play中にInspectorのメニュー（⋮）から、強さ1で揺らす。
    [ContextMenu("揺れを再生（強さ1）")]
    private void PlayForTuning()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("CameraShakeEffect: 揺れはPlay中だけ再生できます", this);
            return;
        }

        Play(1f);
    }

    private float GetCurrentAmplitude()
    {
        if (_amplitude <= 0f)
        {
            return 0f;
        }

        float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        float remaining = 1f - t;
        return _amplitude * remaining * remaining;
    }
}
