using UnityEngine;

// シールドの演出の窓口。未実装（外枠だけ）。PlayerManagerのShieldStarted／ShieldEnded／ShieldBlockedを購読し、各演出を呼ぶ。
// 演出を足すときは、BoostPresentationと同じく、Play／Stopなどを持つコンポーネントを作ってここから呼ぶ。
// BoostPresentationと同じく、Playerプレハブの中で完結するため、VContainerには登録せずAwake／OnDestroyで購読・解除する。
// 予定：レーサーの後ろの半球のメッシュ＋エフェクト（張る／消す）、防いだときのエフェクト・SE、防いだ後に使えない間の表示。
public class ShieldPresentation : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;

    private void Awake()
    {
        if (_playerManager == null)
        {
            Debug.LogError("ShieldPresentation: Player Manager が未設定です", this);
            return;
        }

        _playerManager.ShieldStarted += PlayShieldEffect;
        _playerManager.ShieldEnded += StopShieldEffect;
        _playerManager.ShieldBlocked += PlayBlockEffect;
    }

    private void OnDestroy()
    {
        if (_playerManager == null)
        {
            return;
        }

        _playerManager.ShieldStarted -= PlayShieldEffect;
        _playerManager.ShieldEnded -= StopShieldEffect;
        _playerManager.ShieldBlocked -= PlayBlockEffect;
    }

    // シールドを張ったとき。
    private void PlayShieldEffect()
    {
    }

    // シールドを解除したとき（離した・防いで消えた・ゴールしたとき）。
    private void StopShieldEffect()
    {
    }

    // シールドでブースト接触を防いだ瞬間（この後StopShieldEffectも呼ばれる）。
    private void PlayBlockEffect()
    {
    }
}
