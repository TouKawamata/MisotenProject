using UnityEngine;

// shild_01の当たり判定。Playerの子のFrontShell（ブースト中）／BackShell（シールド中）の中の、当たり判定用のオブジェクトに付ける。
// Shield LayerのTriggerのColliderを持ち、発動している間だけPlayerManagerがSetActiveで有効にする（それ以外は判定しない）。
// Colliderは見た目（shild_01のメッシュ）とは別にし、体の中心からshild_01の端までを覆う（体との間にすき間を作らない）。
// 当てられた側のBoostHitDetectorから見て「shild_01に当たった」ことの目印にもなる（BoostHitInfo.HitShell）。
public class ShieldShellHitbox : MonoBehaviour
{
    [Tooltip("shild_01の当たり判定のCollider（Is TriggerをON）。未設定なら、このGameObjectのColliderを使う")]
    [SerializeField] private Collider _collider;

    public bool IsActive => _collider != null && _collider.enabled;

    private void Awake()
    {
        if (_collider == null)
        {
            _collider = GetComponent<Collider>();
        }

        if (_collider == null)
        {
            Debug.LogError($"ShieldShellHitbox: {name} にColliderがありません", this);
            return;
        }

        if (!_collider.isTrigger)
        {
            Debug.LogWarning($"ShieldShellHitbox: {name} のColliderがTriggerではありません。Kinematic同士は衝突しないため、判定が取れない可能性があります（Is TriggerをONにしてください）", this);
        }

        // 発動するまでは判定しない。
        _collider.enabled = false;
    }

    public void SetActive(bool isActive)
    {
        if (_collider != null && _collider.enabled != isActive)
        {
            _collider.enabled = isActive;
        }
    }
}
