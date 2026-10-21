using UnityEngine;

public class RearRadarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerFlightController _playerFlight;
    [SerializeField] private Transform _cpu;

    [Header("UI")]
    [SerializeField] private RectTransform _cpuIcon;

    [Header("Radar Settings")]
    [SerializeField] private float _radius = 150f;
    [SerializeField] private float _maxDistance = 50f;

    [Header("Icon Size")]
    [SerializeField] private float _minIconScale = 0.5f;
    [SerializeField] private float _maxIconScale = 1.0f;

    private void LateUpdate()
    {
        if (_playerFlight == null || _cpu == null || _cpuIcon == null)
        {
            return;
        }

        Vector3 local =
            _playerFlight.transform.InverseTransformPoint(_cpu.position);

        float distance = local.magnitude;

        // プレイヤーより前にいる、または遠すぎるCPUは表示しない
        bool isBehind = local.z < 0f;
        bool isInRange = distance <= _maxDistance;

        if (!isBehind || !isInRange)
        {
            _cpuIcon.gameObject.SetActive(false);
            return;
        }

        _cpuIcon.gameObject.SetActive(true);

        UpdateIconPosition(local);
        UpdateIconScale(distance);
    }

    private void UpdateIconPosition(Vector3 local)
    {
        float angle = Mathf.Atan2(local.x, -local.z);

        Vector2 position = new Vector2(
            Mathf.Sin(angle),
            -Mathf.Cos(angle)
        ) * _radius;

        _cpuIcon.anchoredPosition = position;
    }

    private void UpdateIconScale(float distance)
    {
        float distance01 = Mathf.Clamp01(distance / _maxDistance);

        float scale = Mathf.Lerp(
            _maxIconScale,
            _minIconScale,
            distance01
        );

        _cpuIcon.localScale = Vector3.one * scale;
    }
}
