using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CPUManager))]
public sealed class CpuBoostTackleEffect : MonoBehaviour
{
    [SerializeField] private TackleEffect _effectPrefab;
    [SerializeField] private Vector3 _localOffset = new Vector3(0f, 1f, 2f);

    private CPUManager _racer;
    private TackleEffect _effect;

    private void Awake() => _racer = GetComponent<CPUManager>();

    private void LateUpdate()
    {
        bool show = _racer.isActiveAndEnabled && _racer.IsBoosting;
        if (show && _effect == null && _effectPrefab != null)
        {
            _effect = Instantiate(_effectPrefab, transform, false);
            _effect.transform.localPosition = _localOffset;
        }

        if (_effect != null && _effect.gameObject.activeSelf != show)
            _effect.gameObject.SetActive(show);
    }

    private void OnDisable()
    {
        if (_effect != null)
            _effect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_effect != null)
            Destroy(_effect.gameObject);
    }
}
