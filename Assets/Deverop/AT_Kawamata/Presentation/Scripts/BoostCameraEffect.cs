using DG.Tweening;
using UnityEngine;

// ブースト時のカメラ演出。1人称→3人称への切り替え（IThirdPersonView.ThirdPersonWeight）とFOVをDOTweenで変える。
// カメラの位置・向きはCameraControllerが計算し、ここは度合いとFOVを変えるだけ。Play／StopはBoostPresentationから呼ばれる。
public class BoostCameraEffect : MonoBehaviour
{
    [Tooltip("1人称／3人称の度合いを変える相手（IThirdPersonViewとしてだけ使う）")]
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private Camera _camera;

    [Header("3人称への切り替え")]
    [Tooltip("ブースト中に3人称へ切り替えるか。OFFならFOVだけ変える")]
    [SerializeField] private bool _switchToThirdPerson = true;

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

    private IThirdPersonView _view;
    private float _normalFieldOfView;
    private Tween _viewTween;
    private Tween _fieldOfViewTween;

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }

        if (_cameraController == null || _camera == null)
        {
            Debug.LogError("BoostCameraEffect: Camera Controller / Camera が未設定です", this);
            enabled = false;
            return;
        }

        _view = _cameraController;
        _normalFieldOfView = _camera.fieldOfView;
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    public void Play()
    {
        TweenTo(_switchToThirdPerson ? 1f : 0f, _boostFieldOfView, _playDuration, _playEase);
    }

    public void Stop()
    {
        TweenTo(0f, _normalFieldOfView, _stopDuration, _stopEase);
    }

    // 途中で呼ばれても、今の値から目標へ向かい直す。
    private void TweenTo(float thirdPersonWeight, float fieldOfView, float duration, Ease ease)
    {
        if (!enabled)
        {
            return;
        }

        KillTweens();
        _viewTween = DOTween
            .To(() => _view.ThirdPersonWeight, value => _view.ThirdPersonWeight = value, thirdPersonWeight, duration)
            .SetEase(ease);
        _fieldOfViewTween = _camera.DOFieldOfView(fieldOfView, duration).SetEase(ease);
    }

    private void KillTweens()
    {
        _viewTween?.Kill();
        _fieldOfViewTween?.Kill();
        _viewTween = null;
        _fieldOfViewTween = null;
    }
}
