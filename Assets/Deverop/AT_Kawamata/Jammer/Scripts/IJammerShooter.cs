// 妨害弾を撃ったレーサーの窓口。撃った弾がレーサーに当たったとき、JammerProjectileから結果を知らせる（RacerManagerBaseが実装する）。
public interface IJammerShooter
{
    // type：当たった弾の種類、result：当てた相手が効果を受けたか・防いだか。リングに当たったときは呼ばない。
    void NotifyJammerLanded(JammerTypeProfile type, EJammerHitResult result);
}
