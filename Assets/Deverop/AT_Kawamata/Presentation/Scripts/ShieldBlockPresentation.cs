using UnityEngine;

// シールドで防いだ・防がれたときの演出の窓口。レーサーのShieldBlocked（自分が防いだ）と、
// BoostHitLanded（Blocked＝自分のブーストが防がれた）を購読し、各演出を呼ぶ。
// ・防いだとき：ヒットストップ、カメラの揺れ、FOVを一瞬広げる、押し出されている間だけ集中線・ラジアルブラー。強さは押し出しの初速で決める
// ・防がれたとき：ヒットストップとカメラの揺れだけ（強さは一定）
// ヒットストップ・集中線などは画面全体にかかるので、Playerプレハブにだけ置く（CPURacerには置かない）。
// shild_01が砕ける演出はShieldPresentationが行う。未設定の演出は飛ばすので、必要なものだけ割り当てればよい。
// BoostPresentationと同じく、Playerプレハブの中で完結するため、VContainerには登録せずAwake／OnDestroyで購読・解除する。
public class ShieldBlockPresentation : MonoBehaviour
{
    [SerializeField] private RacerManagerBase _playerManager;

    [Header("強さ")]
    [Tooltip("押し出しの初速（m/s）がこれ以上なら、防いだときの演出の強さを最大（1）にする。これより遅ければ、初速に比例して弱くする")]
    [SerializeField] private float _fullStrengthPushSpeed = 30f;
    [Tooltip("自分のブーストがシールドで防がれたときの演出の強さ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _blockedStrength = 0.5f;

    [Header("演出")]
    [SerializeField] private HitStopEffect _hitStop;

    [SerializeField] private CameraShakeEffect _cameraShake;

    [Tooltip("FOVを一瞬広げる（ブーストのカメラ演出と同じコンポーネントを使う）")]
    [SerializeField] private BoostCameraEffect _cameraEffect;

    [Tooltip("押し出されている間の集中線（ブーストと同じコンポーネントを使う）")]
    [SerializeField] private SpeedLineEffect _speedLines;

    [Tooltip("押し出されている間のラジアルブラー（ブーストと同じコンポーネントを使う）")]
    [SerializeField] private RadialBlurEffect _radialBlur;

    // 集中線・ラジアルブラーを止めるまでの残り秒数（ゲームの時間。押し出しと同じく、ヒットストップ中は進まない）。
    private float _pushEffectRemainingTime;

    private void Awake()
    {
        if (_playerManager == null)
        {
            Debug.LogError("ShieldBlockPresentation: Player Manager が未設定です", this);
            return;
        }

        _playerManager.ShieldBlocked += PlayBlockEffect;
        _playerManager.BoostHitLanded += PlayBlockedEffect;
    }

    private void OnDestroy()
    {
        if (_playerManager == null)
        {
            return;
        }

        _playerManager.ShieldBlocked -= PlayBlockEffect;
        _playerManager.BoostHitLanded -= PlayBlockedEffect;
    }

    private void Update()
    {
        if (_pushEffectRemainingTime <= 0f)
        {
            return;
        }

        _pushEffectRemainingTime -= Time.deltaTime;
        if (_pushEffectRemainingTime <= 0f)
        {
            StopPushEffect();
        }
    }

    // 自分のシールドでブースト接触を防いだとき。pushSpeed：押し出しの初速（m/s）、pushDuration：押し出す秒数。
    private void PlayBlockEffect(float pushSpeed, float pushDuration)
    {
        float strength = _fullStrengthPushSpeed > 0f ? Mathf.Clamp01(pushSpeed / _fullStrengthPushSpeed) : 1f;
        PlayImpact(strength);

        if (_cameraEffect != null)
        {
            _cameraEffect.PlayFieldOfViewKick(strength);
        }

        if (strength > 0f && pushDuration > 0f)
        {
            PlayPushEffect(pushDuration);
        }
    }

    // 自分のブーストを当てて、効果があったとき。シールドで防がれたときだけ演出する。
    private void PlayBlockedEffect(EBoostHitResult result)
    {
        if (result == EBoostHitResult.Blocked)
        {
            PlayImpact(_blockedStrength);
        }
    }

    private void PlayImpact(float strength)
    {
        if (_hitStop != null)
        {
            _hitStop.Play(strength);
        }

        if (_cameraShake != null)
        {
            _cameraShake.Play(strength);
        }
    }

    private void PlayPushEffect(float duration)
    {
        _pushEffectRemainingTime = duration;

        if (_speedLines != null)
        {
            _speedLines.Play();
        }

        if (_radialBlur != null)
        {
            _radialBlur.Play();
        }
    }

    // 押し出しが終わったとき。その間にブーストが始まっていたら、ブーストの演出に任せて止めない。
    private void StopPushEffect()
    {
        if (_playerManager.IsBoosting)
        {
            return;
        }

        if (_speedLines != null)
        {
            _speedLines.Stop();
        }

        if (_radialBlur != null)
        {
            _radialBlur.Stop();
        }
    }
}
