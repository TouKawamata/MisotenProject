using UnityEngine;

// ブースト中に他のレーサーと重なったら、相手のIBoostHitReceiver.ReceiveBoostHitを呼ぶ。
// 効果を受けるかどうか（シールド・無敵・お互いにブースト中か）は、当てられた側が判断する。
// Rigidbodyを持つGameObject（Player・DummyRacerのルート）に付ける。
// レーサーはすべてKinematicなので、ColliderがTriggerでないと接触の通知が来ない（Kinematic同士は衝突しないため）。
// そのため、各レーサーは自分か子にTriggerのColliderを持つこと（無ければ起動時に警告を出す）。
// 重なっている間は呼び続けるため、同じ相手に何度も効果が出ないようにする処理は、当てられた側の無敵時間に任せる。
//
// 近さ：当てた側の左右方向で見た、中心どうしの距離dを「自分の横幅の1/2 ＋ 相手の横幅の1/2」で割合にしたもの。
//       中心どうしが一致で1、体の端どうしがかすめただけなら0。横ずれの距離はこの近さで変わる。
public class BoostHitDetector : MonoBehaviour
{
    // 中心どうしの左右の距離がこれ未満なら、押し出す向きを決めない（当てられた側がコースの中央側を選ぶ）。
    private const float UndeterminedSideDistance = 0.05f;

    [Tooltip("自分の横幅の1/2（m）。当てた・当てられたときの近さの計算に使う。Colliderの横幅にそろえる")]
    [SerializeField] private float _halfWidth = 0.5f;

    private IBoostAttacker _attacker;

    public float HalfWidth => Mathf.Max(0f, _halfWidth);

    private void Awake()
    {
        _attacker = GetComponentInParent<IBoostAttacker>();
        if (_attacker == null)
        {
            Debug.LogError($"BoostHitDetector: {name} の親にIBoostAttackerを実装したコンポーネントがありません", this);
        }

        WarnIfNoTriggerCollider();
    }

    private void WarnIfNoTriggerCollider()
    {
        foreach (Collider ownCollider in GetComponentsInChildren<Collider>())
        {
            if (ownCollider.isTrigger)
            {
                return;
            }
        }

        Debug.LogWarning($"BoostHitDetector: {name} にTriggerのColliderがありません。Kinematic同士は衝突しないため、ブースト接触の判定が取れない可能性があります（ColliderのIs TriggerをONにしてください）", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (_attacker == null || !_attacker.IsBoosting)
        {
            return;
        }

        Transform attackerTransform = _attacker.Transform;
        if (other.transform == attackerTransform || other.transform.IsChildOf(attackerTransform))
        {
            return;
        }

        IBoostHitReceiver receiver = other.GetComponentInParent<IBoostHitReceiver>();
        if (receiver is not Component receiverComponent)
        {
            return;
        }

        receiver.ReceiveBoostHit(CreateHitInfo(attackerTransform, receiverComponent));
    }

    private BoostHitInfo CreateHitInfo(Transform attackerTransform, Component receiverComponent)
    {
        BoostHitDetector receiverDetector = receiverComponent.GetComponentInChildren<BoostHitDetector>();
        float receiverHalfWidth = receiverDetector != null ? receiverDetector.HalfWidth : 0f;
        float range = HalfWidth + receiverHalfWidth;

        Vector3 right = attackerTransform.right;
        float lateral = Vector3.Dot(receiverComponent.transform.position - attackerTransform.position, right);
        float closeness = range > 0f ? 1f - Mathf.Clamp01(Mathf.Abs(lateral) / range) : 0f;
        Vector3 pushDirection = Mathf.Abs(lateral) < UndeterminedSideDistance ? Vector3.zero : right * Mathf.Sign(lateral);

        return new BoostHitInfo(pushDirection, closeness);
    }
}
