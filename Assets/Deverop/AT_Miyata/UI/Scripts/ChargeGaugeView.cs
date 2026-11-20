using UnityEngine;

/// <summary>
/// ブースト・シールドのチャージを RingGaugeGraphic に渡す。
/// ブースト：ゲージ残量（BoostController.GaugeNormalized）
/// シールド：使える状態なら1。破壊後は、再び使えるようになるまでの回復具合（0→1）
/// </summary>
public class ChargeGaugeView : MonoBehaviour
{
    public enum ESource
    {
        Boost,
        Shield
    }

    [Header("Source")]
    [SerializeField] private ESource _source = ESource.Boost;
    [SerializeField] private BoostController _boost;
    [SerializeField] private ShieldController _shield;

    [Header("UI")]
    [SerializeField] private RingGaugeGraphic _gauge;

    private void Start()
    {
        // プレハブから置いたときなど、未設定ならプレイヤーのものを探す
        if (_source == ESource.Boost && _boost == null)
        {
            _boost = HudReferenceResolver.FindPlayerComponent<BoostController>();
        }

        if (_source == ESource.Shield && _shield == null)
        {
            _shield = HudReferenceResolver.FindPlayerComponent<ShieldController>();
        }
    }

    private void LateUpdate()
    {
        if (_gauge == null)
        {
            return;
        }

        switch (_source)
        {
            case ESource.Boost:
                if (_boost != null)
                {
                    _gauge.Progress = _boost.GaugeNormalized;
                }

                break;

            case ESource.Shield:
                if (_shield != null)
                {
                    _gauge.Progress = GetShieldCharge(_shield);
                }

                break;
        }
    }

    private static float GetShieldCharge(ShieldController shield)
    {
        if (!shield.IsBroken)
        {
            return 1f;
        }

        float duration = shield.BrokenDuration;
        if (duration <= 0f)
        {
            return 1f;
        }

        return 1f - Mathf.Clamp01(shield.BrokenRemainingTime / duration);
    }
}
