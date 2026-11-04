// 妨害弾を受ける側の窓口。JammerProjectileが当たったレーサーのこれを呼ぶ。
// 効果を受けるかどうかは受ける側が判断して、結果を返す（Stunned／Blocked／Ignored）。
public interface IJammerHitReceiver
{
    EBoostHitResult ReceiveJammerHit();
}
