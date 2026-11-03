using UnityEngine;
using UnityEngine.Serialization;

// カメラの揺れの演出。Playで、横揺れ（左右の位置のずれ）と横の傾きを、右→左→中央の一往復だけ行う（Duration秒で終わる）。
// どちらも sin(2π × 経過の割合) の形で動かす（右の端・左の端で幅・角度いっぱいになり、最後は中央に戻る）。
// 揺れはICameraShakeView経由でCameraControllerに渡すだけ（カメラのTransformは触らない）。
// 揺れている間は、左右入力による傾きの代わりに揺れの傾きを使ってもらう（ShakeTiltWeight。両方が足されて傾き過ぎないように）。
// ヒットストップ中も揺らすため、実時間（unscaledDeltaTime）で進める。ShieldBlockPresentationから呼ばれる。
public class CameraShakeEffect : MonoBehaviour
{
    // 左右入力による傾きと揺れの傾きを入れ替える時間（Duration秒に対する割合）。始まり・終わりで傾きが飛ばないようにする。
    // 右の端（経過の割合0.25）より短くし、端では揺れの傾きだけになるようにする。
    private const float TiltBlendRate = 0.15f;

    [Tooltip("揺れを足す相手（ICameraShakeViewとしてだけ使う）")]
    [SerializeField] private CameraController _cameraController;

    [Tooltip("強さ1のときの横揺れの幅（m。中央から右・左の端までの距離）")]
    [FormerlySerializedAs("_maxAmplitude")]
    [SerializeField] private float _amplitude = 0.3f;
    [Tooltip("強さ1のときの横の傾き（度。中央から右・左の端までの角度）。左右入力による傾きと同じ向きに傾く")]
    [SerializeField] private float _tiltAngle = 4f;
    [Tooltip("右→左→中央の一往復にかける秒数（実時間）")]
    [SerializeField] private float _duration = 0.35f;

    private ICameraShakeView _view;
    private float _strength;
    private float _elapsed;

    private bool IsPlaying => _strength > 0f;

    private void Awake()
    {
        if (_cameraController == null)
        {
            Debug.LogError("CameraShakeEffect: Camera Controller が未設定です", this);
            enabled = false;
            return;
        }

        _view = _cameraController;
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        float t = _duration > 0f ? _elapsed / _duration : 1f;
        if (t >= 1f)
        {
            StopShake();
            return;
        }

        float wave = Mathf.Sin(t * 2f * Mathf.PI);
        _view.ShakeOffset = new Vector3(wave * _amplitude * _strength, 0f, 0f);
        _view.ShakeTilt = wave * _tiltAngle * _strength;
        _view.ShakeTiltWeight = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f - t) / TiltBlendRate);
    }

    // strength：0～1。揺れている途中で呼ばれたら、今の揺れより強いときだけ最初から揺れ直す。
    public void Play(float strength)
    {
        if (!enabled)
        {
            return;
        }

        strength = Mathf.Clamp01(strength);
        if (strength <= 0f || strength <= _strength)
        {
            return;
        }

        _strength = strength;
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

        StopShake();
        Play(1f);
    }

    private void StopShake()
    {
        _strength = 0f;
        _elapsed = 0f;
        if (_view == null)
        {
            return;
        }

        _view.ShakeOffset = Vector3.zero;
        _view.ShakeTilt = 0f;
        _view.ShakeTiltWeight = 0f;
    }
}
