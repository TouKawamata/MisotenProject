using UnityEngine;
using UnityEngine.Events;

// ショートカット判定エリア 「在/不在の通知」と「UI表示用のフック（UnityEvent）」だけを担当する。
// 発動エリアの在/不在はPlayer・CPUの両方へ通知する。UI表示用のフック（警告・発動エリアのUnityEvent）は、Playerが出入りしたときだけ呼ぶ
// （CPUが通っても警告を出さない）。
public class ShortcutZone : MonoBehaviour
{
    [Header("トリガー設定（このエリア用）")]
    [SerializeField] private EShortcutTriggerMethod triggerMethod = EShortcutTriggerMethod.KeyPress;
    [Tooltip("TriggerMethodがHorizontalThresholdのときに使う閾値")]
    [SerializeField] private float horizontalThreshold = 0.9f;

    public UnityEvent OnWarningEnter;
    public UnityEvent OnWarningExit;
    public UnityEvent OnActivationEnter;
    public UnityEvent OnActivationExit;

    public EShortcutTriggerMethod TriggerMethod => triggerMethod;

    public float HorizontalThreshold => horizontalThreshold;

    public void NotifyEnter(EShortcutZoneRole role, Collider other)
    {
        bool isPlayer = IsPlayer(other);
        if (role == EShortcutZoneRole.Activation)
        {
            GetReceiver(other)?.SetInShortcutZone(this);
            if (isPlayer)
            {
                OnActivationEnter?.Invoke();
            }
        }
        else if (isPlayer)
        {
            OnWarningEnter?.Invoke();
        }
    }

    public void NotifyExit(EShortcutZoneRole role, Collider other)
    {
        bool isPlayer = IsPlayer(other);
        if (role == EShortcutZoneRole.Activation)
        {
            GetReceiver(other)?.SetInShortcutZone(null);
            if (isPlayer)
            {
                OnActivationExit?.Invoke();
            }
        }
        else if (isPlayer)
        {
            OnWarningExit?.Invoke();
        }
    }

    private static IShortcutZoneReceiver GetReceiver(Collider other)
    {
        return other.GetComponentInParent<IShortcutZoneReceiver>();
    }

    private static bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerManager>() != null;
    }
}
