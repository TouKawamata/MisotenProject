using UnityEngine;

// シールドの演出の窓口。PlayerManagerのShieldStarted／ShieldEnded／ShieldBlockedを購読し、各演出を呼ぶ。
// 演出を足すときは、BoostPresentationと同じく、Play／Stopなどを持つコンポーネントを作ってここから呼ぶ。
// BoostPresentationと同じく、Playerプレハブの中で完結するため、VContainerには登録せずAwake／OnDestroyで購読・解除する。
// 今あるもの：後方のshild_01の表示（張る／消す）と、防いだときに砕ける演出。
// 予定：防いだときのエフェクト・SE、防いだ後に使えない間の表示。
public class ShieldPresentation : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;

    [Header("演出")]
    [Tooltip("後方のshild_01の見た目（BackShellの中）")]
    [SerializeField] private ShieldShellView _backShellView;

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
        if (_backShellView != null)
        {
            _backShellView.Show();
        }
    }

    // シールドを解除したとき（離した・防いで消えた・ゴールしたとき）。砕けている途中なら、ShieldShellView側で最後まで見せてから隠す。
    private void StopShieldEffect()
    {
        if (_backShellView != null)
        {
            _backShellView.Hide();
        }
    }

    // シールドでブースト接触を防いだ瞬間（この後StopShieldEffectも呼ばれる）。
    private void PlayBlockEffect()
    {
        if (_backShellView != null)
        {
            _backShellView.Shatter();
        }
    }
}
