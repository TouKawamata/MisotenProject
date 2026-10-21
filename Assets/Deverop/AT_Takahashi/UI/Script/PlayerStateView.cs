using UnityEngine;

public class PlayerStateView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerManager _playerManager;

    [Header("State UI")]
    [SerializeField] private GameObject _normalView;
    [SerializeField] private GameObject _boostView;
    [SerializeField] private GameObject _shieldView;
    [SerializeField] private GameObject _stunnedView;

    private PlayerState _currentState = PlayerState.None;

    private enum PlayerState
    {
        None,
        Normal,
        Boost,
        Shield,
        Stunned
    }

    private void LateUpdate()
    {
        if (_playerManager == null)
        {
            return;
        }

        PlayerState newState = GetCurrentState();

        if (newState == _currentState)
        {
            return;
        }

        _currentState = newState;
        UpdateView(newState);
    }

    private PlayerState GetCurrentState()
    {
        // ÉXÉ^ÉìÇç≈óDêÊ
        if (_playerManager.IsStunned)
        {
            return PlayerState.Stunned;
        }

        if (_playerManager.IsBoosting)
        {
            return PlayerState.Boost;
        }

        if (_playerManager.IsShielding)
        {
            return PlayerState.Shield;
        }

        return PlayerState.Normal;
    }

    private void UpdateView(PlayerState state)
    {
        _normalView.SetActive(state == PlayerState.Normal);
        _boostView.SetActive(state == PlayerState.Boost);
        _shieldView.SetActive(state == PlayerState.Shield);
        _stunnedView.SetActive(state == PlayerState.Stunned);
    }
}