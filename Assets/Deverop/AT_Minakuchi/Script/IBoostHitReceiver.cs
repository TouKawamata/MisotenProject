// ブースト中のレーサーに当てられる側の窓口。効果を受けるかどうか（シールド・無敵など）は受け取った側が判断し、結果を返す。
public interface IBoostHitReceiver
{
    EBoostHitResult ReceiveBoostHit(BoostHitInfo hit);
}
