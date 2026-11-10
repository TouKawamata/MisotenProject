using DG.Tweening;
using UnityEngine;

// ブースト時のカメラ演出。FOVをDOTweenで変える（3人称への切り替えは廃止）。
// カメラの位置・向きはCameraControllerが計算し、ここはFOVを変えるだけ。Play／StopはBoostPresentationから呼ばれる。
// シールドで防いだときにFOVを一瞬広げる演出（PlayFieldOfViewKick。ShieldBlockPresentationから呼ばれる）もここで行う
// （FOVを動かすTweenを1か所にまとめ、ブーストのFOVと取り合わないようにするため）。
public class BoostCameraEffect : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    [Header("FOV")]
    [Tooltip("ブースト中のFOV。通常時のFOVは起動時のカメラの値を使う")]
    [SerializeField] private float _boostFieldOfView = 75f;

    [Header("時間")]
    [Tooltip("ブースト開始時に切り替え切るまでの秒数")]
    [SerializeField] private float _playDuration = 0.35f;
    [SerializeField] private Ease _playEase = Ease.OutCubic;
    [Tooltip("ブースト終了時に元へ戻し切るまでの秒数")]
    [SerializeField] private float _stopDuration = 0.5f;
    [SerializeField] private Ease _stopEase = Ease.InOutSine;

    [Header("シールドで防いだときに、FOVを一瞬広げる")]
    [Tooltip("強さ1のときに広げる量（度）")]
    [SerializeField] private float _kickFieldOfView = 8f;
    [Tooltip("広げ切るまでの秒数（実時間。ヒットストップ中も動く）")]
    [SerializeField] private float _kickInDuration = 0.06f;
    [Tooltip("元のFOVへ戻し切るまでの秒数（実時間）")]
    [SerializeField] private float _kickOutDuration = 0.4f;

    private float _normalFieldOfView;

    // 今向かっているFOV（通常時かブースト中）。FOVを一瞬広げた後は、ここへ戻す。
    private float _targetFieldOfView;
    private Tween _fieldOfViewTween;

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }

        if (_camera == null)
        {
            Debug.LogError("BoostCameraEffect: Camera が未設定です", this);
            enabled = false;
            return;
        }

        _normalFieldOfView = _camera.fieldOfView;
        _targetFieldOfView = _normalFieldOfView;
    }

    private void OnDestroy()
    {
        KillTween();
    }

    public void Play()
    {
        TweenTo(_boostFieldOfView, _playDuration, _playEase);
    }

    public void Stop()
    {
        TweenTo(_normalFieldOfView, _stopDuration, _stopEase);
    }

    // strength：0～1。今のFOVから一瞬広げ、今向かっているFOVへ戻す。
    // 途中でPlay／Stopが呼ばれたら、そちらのTweenで打ち消す。
    public void PlayFieldOfViewKick(float strength)
    {
        float kick = _kickFieldOfView * Mathf.Clamp01(strength);
        if (!enabled || kick <= 0f)
        {
            return;
        }

        _fieldOfViewTween?.Kill();
        _fieldOfViewTween = DOTween.Sequence()
            .Append(_camera.DOFieldOfView(_camera.fieldOfView + kick, _kickInDuration).SetEase(Ease.OutCubic))
            .Append(_camera.DOFieldOfView(_targetFieldOfView, _kickOutDuration).SetEase(Ease.InOutSine))
            .SetUpdate(true);
    }

    // 途中で呼ばれても、今の値から目標へ向かい直す。
    private void TweenTo(float fieldOfView, float duration, Ease ease)
    {
        if (!enabled)
        {
            return;
        }

        KillTween();
        _targetFieldOfView = fieldOfView;
        _fieldOfViewTween = _camera.DOFieldOfView(fieldOfView, duration).SetEase(ease);
    }

    private void KillTween()
    {
        _fieldOfViewTween?.Kill();
        _fieldOfViewTween = null;
    }
}
