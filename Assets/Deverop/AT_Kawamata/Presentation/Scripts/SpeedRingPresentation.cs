using UnityEngine;

// 加速リングを通ったときの画面の演出。FOVを一瞬広げ、加速している間（リングの効果時間）だけ集中線・ラジアルブラー（ブーストと同じもの）を出す。
// 減速リングになっているときは出さない。続けて通ったときは、その分だけ演出を延ばす。
// 未設定の演出は飛ばすので、必要なものだけ割り当てればよい。
public class SpeedRingPresentation : GimmickPresentationBase<SpeedRing>
{
    [Header("強さ")]
    [Tooltip("集中線・ラジアルブラーの強さ。ブースト中の強さに対する割合（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _screenEffectStrength = 0.7f;
    [Tooltip("FOVを一瞬広げる量。1でBoostCameraEffectの「強さ1のときに広げる量」になる")]
    [Range(0f, 1f)]
    [SerializeField] private float _fieldOfViewKickStrength = 0.5f;

    [Header("演出")]
    [Tooltip("FOVを一瞬広げる（ブーストのカメラ演出と同じコンポーネントを使う）")]
    [SerializeField] private BoostCameraEffect _cameraEffect;

    [Tooltip("加速している間の集中線（ブーストと同じコンポーネントを使う）")]
    [SerializeField] private SpeedLineEffect _speedLines;

    [Tooltip("加速している間のラジアルブラー（ブーストと同じコンポーネントを使う）")]
    [SerializeField] private RadialBlurEffect _radialBlur;

    // 集中線・ラジアルブラーを止めるまでの残り秒数（ゲームの時間。押し出しと同じく、ヒットストップ中は進まない）。
    private float _remainingTime;

    private void Update()
    {
        if (_remainingTime <= 0f)
        {
            return;
        }

        _remainingTime -= Time.deltaTime;
        if (_remainingTime <= 0f)
        {
            StopScreenEffect();
        }
    }

    // 演出中に無効になると、Updateで止められず残ってしまうので、ここで止める。
    private void OnDisable()
    {
        _remainingTime = 0f;
        StopScreenEffect();
    }

    protected override void Play(SpeedRing ring)
    {
        // イベントは無効のときも届くので、止められない演出を始めないようにする。
        if (!isActiveAndEnabled || ring.IsSlowing)
        {
            return;
        }

        if (_cameraEffect != null)
        {
            _cameraEffect.PlayFieldOfViewKick(_fieldOfViewKickStrength);
        }

        _remainingTime = Mathf.Max(_remainingTime, ring.Duration);

        if (_speedLines != null)
        {
            _speedLines.Play(this, _screenEffectStrength);
        }

        if (_radialBlur != null)
        {
            _radialBlur.Play(this, _screenEffectStrength);
        }
    }

    private void StopScreenEffect()
    {
        if (_speedLines != null)
        {
            _speedLines.Stop(this);
        }

        if (_radialBlur != null)
        {
            _radialBlur.Stop(this);
        }
    }
}
