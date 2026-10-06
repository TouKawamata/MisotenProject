using UnityEngine;

// ブースト時の演出の窓口。PlayerManagerのBoostStarted／BoostEndedを購読し、各演出のPlay／Stopを呼ぶだけ。
// 演出を足すときは、Play／Stopを持つコンポーネントを作ってここから呼ぶ（PlayerManagerは演出を直接触らない）。
// 未設定の演出は飛ばすので、必要なものだけ割り当てればよい。
// Playerプレハブの中で完結する（シーンに依存しない）ため、VContainerには登録せず、Awake／OnDestroyで購読・解除する
// （イベントはPlaceAt・Tickからしか出ないので、全員のAwakeの後になり、購読が間に合わないことはない）。
public class BoostPresentation : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;

    [Header("演出")]
    [Tooltip("1人称→3人称の切り替えとFOV")]
    [SerializeField] private BoostCameraEffect _cameraEffect;

    // TODO: 以下の演出は未実装。RadialBlurEffect（ラジアルブラー）・SpeedLineEffect（集中線）を、
    //       Play／Stopを持つコンポーネントとして実装したら、コメントアウトを外して追加する（PlayBoostEffect／StopBoostEffectも同様）。
    // [SerializeField] private RadialBlurEffect _radialBlur;
    // [SerializeField] private SpeedLineEffect _speedLines;

    private void Awake()
    {
        if (_playerManager == null)
        {
            Debug.LogError("BoostPresentation: Player Manager が未設定です", this);
            return;
        }

        _playerManager.BoostStarted += PlayBoostEffect;
        _playerManager.BoostEnded += StopBoostEffect;
    }

    private void OnDestroy()
    {
        if (_playerManager == null)
        {
            return;
        }

        _playerManager.BoostStarted -= PlayBoostEffect;
        _playerManager.BoostEnded -= StopBoostEffect;
    }

    private void PlayBoostEffect()
    {
        if (_cameraEffect != null)
        {
            _cameraEffect.Play();
        }

        // TODO: RadialBlurEffect・SpeedLineEffectを実装したら追加する。
        // if (_radialBlur != null)
        // {
        //     _radialBlur.Play();
        // }
        //
        // if (_speedLines != null)
        // {
        //     _speedLines.Play();
        // }
    }

    private void StopBoostEffect()
    {
        if (_cameraEffect != null)
        {
            _cameraEffect.Stop();
        }

        // TODO: RadialBlurEffect・SpeedLineEffectを実装したら追加する。
        // if (_radialBlur != null)
        // {
        //     _radialBlur.Stop();
        // }
        //
        // if (_speedLines != null)
        // {
        //     _speedLines.Stop();
        // }
    }
}
