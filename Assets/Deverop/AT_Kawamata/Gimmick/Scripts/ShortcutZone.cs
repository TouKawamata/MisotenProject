using UnityEngine;
using UnityEngine.Events;

// ショートカット判定エリア 「在/不在の通知」と「UI表示用のフック（UnityEvent）」だけを担当する。
public class ShortcutZone : MonoBehaviour
{
    [Header("トリガー設定（このエリア用）")]
    [SerializeField] private ShortcutTriggerMethod triggerMethod = ShortcutTriggerMethod.KeyPress;
    [Tooltip("TriggerMethodがHorizontalThresholdのときに使う閾値")]
    [SerializeField] private float horizontalThreshold = 0.9f;

    public UnityEvent OnWarningEnter;
    public UnityEvent OnWarningExit;
    public UnityEvent OnActivationEnter;
    public UnityEvent OnActivationExit;

    public ShortcutTriggerMethod TriggerMethod => triggerMethod;

    public float HorizontalThreshold => horizontalThreshold;

    public void NotifyEnter(ShortcutZoneRole role, Collider other)
    {
        if (role == ShortcutZoneRole.Activation)
        {
            GetReceiver(other)?.SetInShortcutZone(this);
            OnActivationEnter?.Invoke();
        }
        else
        {
            OnWarningEnter?.Invoke();
        }
    }

    public void NotifyExit(ShortcutZoneRole role, Collider other)
    {
        if (role == ShortcutZoneRole.Activation)
        {
            GetReceiver(other)?.SetInShortcutZone(null);
            OnActivationExit?.Invoke();
        }
        else
        {
            OnWarningExit?.Invoke();
        }
    }

    private static IShortcutZoneReceiver GetReceiver(Collider other)
    {
        return other.GetComponentInParent<IShortcutZoneReceiver>();
    }
}
