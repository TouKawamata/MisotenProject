using UnityEngine;
using UnityEngine.UI;

public class BoostGaugeView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoostController _boost;
    [SerializeField] private ShieldController _shield;

    [Header("UI")]
    [SerializeField] private Image _gaugeFill;

    [Header("Colors")]
    [SerializeField] private Color _availableColor = Color.cyan;
    [SerializeField] private Color _unavailableColor = Color.gray;
    [SerializeField] private Color _overheatedColor = Color.red;

    private void LateUpdate()
    {
        if (_boost == null || _shield == null || _gaugeFill == null)
        {
            return;
        }

        UpdateGauge();
        UpdateGaugeColor();
    }

    private void UpdateGauge()
    {
        _gaugeFill.fillAmount =
            Mathf.Clamp01(_boost.GaugeNormalized);
    }

    private void UpdateGaugeColor()
    {
        bool canActivate =
            _boost.CanActivate && !_shield.IsActive;

        if (_boost.IsOverheated)
        {
            _gaugeFill.color = _overheatedColor;
        }
        else if (canActivate)
        {
            _gaugeFill.color = _availableColor;
        }
        else
        {
            _gaugeFill.color = _unavailableColor;
        }
    }
}