using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedMeterView : MonoBehaviour
{
    private const float MpsToKmh = 3.6f;

    [Header("References")]
    [SerializeField] private PlayerFlightController _playerFlight;
    [SerializeField] private BoostProfile _boostProfile;

    [Header("UI")]
    [SerializeField] private Image _speedGaugeFill;
    [SerializeField] private TMP_Text _speedText;

    private void LateUpdate()
    {
        if (_playerFlight == null || _boostProfile == null)
        {
            return;
        }

        float speedMps = Mathf.Max(0f, _playerFlight.ForwardSpeed);
        float maxSpeedMps = _boostProfile.BoostMaxSpeed;

        UpdateSpeedText(speedMps);
        UpdateSpeedGauge(speedMps, maxSpeedMps);
    }

    private void UpdateSpeedText(float speedMps)
    {
        float speedKmh = speedMps * MpsToKmh;

        _speedText.SetText("{0:0} km/h", speedKmh);
    }

    private void UpdateSpeedGauge(float speedMps, float maxSpeedMps)
    {
        if (maxSpeedMps <= 0f)
        {
            _speedGaugeFill.fillAmount = 0f;
            return;
        }

        _speedGaugeFill.fillAmount =
            Mathf.Clamp01(speedMps / maxSpeedMps);
    }
}