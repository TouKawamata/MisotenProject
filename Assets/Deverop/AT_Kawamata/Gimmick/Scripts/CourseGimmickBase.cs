using System;
using UnityEngine;

// コースギミック（障害物・加速パネル・減速エリア・加速リングなど）の継承元。
// 当たったかどうかの判定はレーサー側（RacerGimmickSensor）が1か所で行い、このクラスの On〇〇 を呼ぶ。ギミック側は判定を書かない。
// レーサーはKinematicで1フレームに1m以上動くため、OnTriggerEnterに頼ると薄いギミックをすり抜け、障害物の面の向きも取れないため。
//
// 作り方：
// 1. このクラスを継承し、ColliderをこのGameObjectか子に付けて、レイヤーをObstacle（障害物・減速など）かBonus（加速など）にする。
//    Is Triggerはどちらでもよい（判定はレイヤーで行う。物理の衝突はさせないので、ONにしておくと安全）。
// 2. 体を止める障害物ならIsSolidをtrueにしてOnRacerHitを、すり抜けるエリア・パネル・リングならOnRacerEnter/Stay/Exitを書く。
// 3. 効果は、渡されたGimmickContact.Racer（IGimmickRacer）の操作・Effectsで反映する。RacerManagerBaseは触らない。
// 4. 効果を出したらNotifyActivatedを呼ぶ。演出はそれを受けて出す（ギミック側の演出はActivated、画面の演出はGimmickPresentationBase）。
//
// レーサーごとの状態（このレーサーには一度だけ効かせる、など）が要るときは、IGimmickRacerをキーにして派生クラスで持つ。
public abstract class CourseGimmickBase : MonoBehaviour
{
    public const string ObstacleLayerName = "Obstacle";
    public const string BonusLayerName = "Bonus";

    private static int _layerMask = -1;

    // ギミックとして判定するレイヤー（Obstacle｜Bonus）。
    public static int GimmickLayerMask
    {
        get
        {
            if (_layerMask < 0)
            {
                _layerMask = LayerMask.GetMask(ObstacleLayerName, BonusLayerName);
            }

            return _layerMask;
        }
    }

    // ギミックが効果を出した瞬間（NotifyActivatedから）。ギミック側の演出（壊れるエフェクト・SEなど）をつなぐ。引数は効果を受けたレーサー。
    public event Action<CourseGimmickBase, IGimmickRacer> Activated;

    // true：体を止める障害物（OnRacerHitが呼ばれる）。false：すり抜けるエリア・パネル・リング（OnRacerEnter/Stay/Exitが呼ばれる）。
    public virtual bool IsSolid => false;

    protected virtual void Awake()
    {
        if (GetComponentInChildren<Collider>(true) == null)
        {
            Debug.LogWarning($"{GetType().Name}: {name} にColliderがありません。レーサーとの接触を判定できません", this);
        }

        foreach (Collider gimmickCollider in GetComponentsInChildren<Collider>(true))
        {
            if (((1 << gimmickCollider.gameObject.layer) & GimmickLayerMask) == 0)
            {
                Debug.LogWarning($"{GetType().Name}: {gimmickCollider.name} のレイヤーが{ObstacleLayerName}／{BonusLayerName}ではありません。このColliderは判定されません", gimmickCollider);
            }
        }
    }

    // 以下はRacerGimmickSensorだけが呼ぶ。

    // すり抜けるギミック：レーサーが入った（触れた）瞬間。1フレームで通り抜けた薄いギミックでも呼ばれる。
    public virtual void OnRacerEnter(in GimmickContact contact)
    {
    }

    // すり抜けるギミック：入っている（触れている）間、Enterの次のTickから毎Tick。
    public virtual void OnRacerStay(in GimmickContact contact, float deltaTime)
    {
    }

    // すり抜けるギミック：出た瞬間。ギミックが無効になったとき・レーサーが置き直された／ゴールしたときも呼ばれる。
    public virtual void OnRacerExit(IGimmickRacer racer)
    {
    }

    // 固いギミック：レーサーの体が当たったとき。体をどう動かすかを返す（効果はcontact.Racerで反映する）。
    // 当たり続けていると毎Tick呼ばれる。滑った先でもう一度当たると、1フレームに複数回呼ばれることもある。
    public virtual EGimmickBlockResponse OnRacerHit(in GimmickContact contact)
    {
        return EGimmickBlockResponse.Slide;
    }

    // 効果を出したことを知らせる（演出用）。ギミックのActivatedと、レーサーのGimmickActivatedの両方を出す。
    protected void NotifyActivated(IGimmickRacer racer)
    {
        Activated?.Invoke(this, racer);
        racer?.NotifyGimmickActivated(this);
    }
}
