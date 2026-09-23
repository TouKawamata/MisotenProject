using UnityEngine;

// ShortcutZoneの子オブジェクトに付けるTrigger Collider用の中継コンポーネント。
[RequireComponent(typeof(Collider))]
public class ShortcutZoneTrigger : MonoBehaviour
{
    [SerializeField] private ShortcutZoneRole role;
    [SerializeField] private ShortcutZone zone;

    private void Awake()
    {
        if (zone == null)
        {
            zone = GetComponentInParent<ShortcutZone>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        zone?.NotifyEnter(role, other);
    }

    private void OnTriggerExit(Collider other)
    {
        zone?.NotifyExit(role, other);
    }
}
