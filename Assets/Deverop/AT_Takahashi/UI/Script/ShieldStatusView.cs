using TMPro;
using UnityEngine;

public class ShieldStatusView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShieldController _shield;

    [Header("State UI")]
    [SerializeField] private GameObject _availableView;
    [SerializeField] private GameObject _activeView;
    [SerializeField] private GameObject _brokenView;

    [Header("Broken UI")]
    [SerializeField] private TMP_Text _remainingTimeText;

    private void LateUpdate()
    {
        if (_shield == null)
        {
            return;
        }

        UpdateState();
    }

    private void UpdateState()
    {
        if (_shield.IsActive)
        {
            ShowActive();
            return;
        }

        if (_shield.IsBroken)
        {
            ShowBroken();
            return;
        }

        if (_shield.CanActivate)
        {
            ShowAvailable();
            return;
        }

        // Ç«ÇÃèÛë‘Ç…Ç‡äYìñÇµÇ»Ç¢èÍçá
        HideAll();
    }

    private void ShowAvailable()
    {
        _availableView.SetActive(true);
        _activeView.SetActive(false);
        _brokenView.SetActive(false);
    }

    private void ShowActive()
    {
        _availableView.SetActive(false);
        _activeView.SetActive(true);
        _brokenView.SetActive(false);
    }

    private void ShowBroken()
    {
        _availableView.SetActive(false);
        _activeView.SetActive(false);
        _brokenView.SetActive(true);

        float remainingTime =
            Mathf.Max(0f, _shield.BrokenRemainingTime);

        if (_remainingTimeText != null)
        {
            _remainingTimeText.SetText("{0:0.0}", remainingTime);
        }
    }

    private void HideAll()
    {
        _availableView.SetActive(false);
        _activeView.SetActive(false);
        _brokenView.SetActive(false);
    }
}