using UnityEngine;

// 通常の接触（ブーストなし）で、レーサーどうしの食い込みを押し戻すときの設定の塊。RacerContactResolverが読む。
// ・横から：左右だけで押し戻す（前後には動かさない）。押した側を多く戻し、押された側は少しだけ動く（押しづらい）
// ・後ろから：後ろのレーサーを前の相手より前に出さず、左右へずらして追い抜かせる（前のレーサーは押し出さない）
[CreateAssetMenu(fileName = "RacerContactProfile", menuName = "Race/RacerContactProfile")]
public class RacerContactProfile : ScriptableObject
{
    [Header("当たり方の判定")]
    [Tooltip("食い込みの向きが、コースの左右方向から何度以内なら「横から」とみなすか。これより前後寄りなら「後ろから」")]
    [Range(0f, 90f)]
    [SerializeField] private float _sideContactMaxAngle = 60f;

    [Header("横から")]
    [Tooltip("横から押したとき、押した側が押し戻される割合（残りの分だけ押された側が動く）。1なら相手は全く動かない、0.5なら半分ずつ")]
    [Range(0.5f, 1f)]
    [SerializeField] private float _pusherCorrectionRate = 0.8f;
    [Tooltip("相手へ向かう横の速さ（m/s）が、どちらもこれ未満なら「どちらも押していない」とみなし、半分ずつ押し戻す")]
    [SerializeField] private float _pushingSpeedThreshold = 0.5f;

    [Header("後ろから")]
    [Tooltip("後ろのレーサーを左右へずらして追い抜かせる速さ（m/s）")]
    [SerializeField] private float _rearSlideSpeed = 6f;

    public float SideContactMaxAngle => _sideContactMaxAngle;

    public float PusherCorrectionRate => _pusherCorrectionRate;

    public float PushingSpeedThreshold => Mathf.Max(0f, _pushingSpeedThreshold);

    public float RearSlideSpeed => Mathf.Max(0f, _rearSlideSpeed);
}
