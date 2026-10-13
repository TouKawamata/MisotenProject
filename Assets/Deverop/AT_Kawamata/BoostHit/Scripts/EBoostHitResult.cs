// ブースト接触の結果。当てられた側（IBoostHitReceiver）が返し、当てた側（BoostHitDetector）はIgnored以外を通知する。
public enum EBoostHitResult
{
    // 効果なし（無敵中・ゴール後・はじいた直後の間隔中など）。重なっている間は毎フレーム呼ばれるため、ほとんどはこれになる
    Ignored,

    // お互いにブースト中で、はじき合った
    Bounced,

    // シールドで防がれた
    Blocked,

    // スタンさせた（タックル成功）
    Stunned
}
