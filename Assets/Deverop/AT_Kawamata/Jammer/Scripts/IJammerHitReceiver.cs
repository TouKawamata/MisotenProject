// 妨害弾を受けるレーサーの窓口。JammerProjectileが当たったレーサーのこれを呼ぶ（RacerManagerBaseが実装する）。
// 効果を受けるか（無敵中・シールドで防ぐなど）は受ける側が決め、効果の中身は弾の種類（JammerTypeProfile）が決める。
public interface IJammerHitReceiver
{
    EJammerHitResult ReceiveJammerHit(JammerTypeProfile type);
}
