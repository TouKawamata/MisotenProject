// 妨害弾が当たった結果（IJammerHitReceiver.ReceiveJammerHitの戻り値）。
public enum EJammerHitResult
{
    // 効果なし（ゴール後・無敵中など）
    Ignored,

    // シールドで防いだ
    Blocked,

    // 効果を受けた
    Hit
}
