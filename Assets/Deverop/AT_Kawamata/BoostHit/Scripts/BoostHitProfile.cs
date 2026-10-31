using UnityEngine;

// ブースト接触を受けたときの効果の設定の塊。PlayerとCPUで同じアセットを使う。
// ・ブースト中でない相手に当たった → 当てられた側がスタン（入力不可・減速）＋横ずれし、その後しばらく無敵
//   横ずれの距離は、当てた側の左右方向で見た中心どうしの近さ（BoostHitInfo.Closeness）で変わる
// ・お互いにブースト中 → お互いを少しはじくだけ（ブーストは解除しない）
// ・シールド中の相手の後方のshild_01に当たった → 防がれる。防いだ側は少し前へ押し出される（加速）
//   押し出しは初速で決める。検証用の切り替えで、初速をぶつかってきた速さで変えたり（ビリヤードの玉突きのイメージ）、当てた側のブーストを解除したりできる
[CreateAssetMenu(fileName = "BoostHitProfile", menuName = "Race/BoostHitProfile")]
public class BoostHitProfile : ScriptableObject
{
    [Header("スタン")]
    [Tooltip("入力を受け付けない秒数")]
    [SerializeField] private float _stunDuration = 1f;
    [Tooltip("スタン中の最高速度の倍率（1＝そのまま）")]
    [Range(0f, 1f)]
    [SerializeField] private float _stunSpeedMultiplier = 0.5f;
    [Tooltip("スタンが終わってから、次に当てられるようになるまでの無敵の秒数。シールドで防いだ直後も、この秒数だけ無敵にする")]
    [SerializeField] private float _invincibleDuration = 1f;

    [Header("横ずれ")]
    [Tooltip("端をかすめたとき（近さ0）に押し出す距離（m）")]
    [SerializeField] private float _pushMinDistance = 3f;
    [Tooltip("中心どうしが一致したとき（近さ1）に押し出す距離（m）")]
    [SerializeField] private float _pushMaxDistance = 8f;
    [Tooltip("近さに対する押し出す距離の割合。横軸＝近さ（0＝端をかすめた、1＝中心どうしが一致）、縦軸＝最小距離（0）～最大距離（1）の割合")]
    [SerializeField] private AnimationCurve _pushCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("押し出し切るまでの秒数（最初に大きく動き、だんだん止まる）")]
    [SerializeField] private float _pushDuration = 0.4f;

    [Header("お互いにブースト中")]
    [Tooltip("お互いにブースト中にぶつかったとき、はじく距離（m）。近さには関係なく一定。押し出す秒数は横ずれと同じ")]
    [SerializeField] private float _bounceDistance = 2f;
    [Tooltip("重なっている間に何度もはじかないよう、次にはじくまで空ける秒数")]
    [SerializeField] private float _bounceInterval = 0.5f;

    [Header("シールドで防いだとき")]
    [Tooltip("ブースト中の相手を後方のshild_01で防いだとき、防いだ側を前へ押し出す初速（m/s）。押し出しは最初が一番速く、Block Push Durationの秒数で0まで落ちる"
        + "（進む距離＝初速×秒数÷2）。速度ではなく位置に足すので、最高速度に関係なく押し出す")]
    [SerializeField] private float _blockPushSpeed = 12f;
    [Tooltip("前へ押し出し切るまでの秒数（最初に大きく動き、だんだん止まる）")]
    [SerializeField] private float _blockPushDuration = 0.5f;

    [Header("シールドで防いだとき（検証用）")]
    [Tooltip("ON：ぶつかってきた速さ（当てた側の前進の速さ − 防いだ側の前進の速さ）の一部を、防いだ側の押し出しの初速として受け取る（ビリヤードの玉突きのイメージ）。OFF：Block Push Speedで一定")]
    [SerializeField] private bool _useBilliardBlockPush;
    [Tooltip("ぶつかってきた速さのうち、押し出しの初速として受け取る割合（1＝速さの差をまるごと受け取る）")]
    [Range(0f, 1f)]
    [SerializeField] private float _billiardSpeedTransferRate = 0.3f;
    [Tooltip("ビリヤード方式のときの、押し出しの初速の上限（m/s）")]
    [SerializeField] private float _billiardMaxPushSpeed = 60f;
    [Tooltip("ON：防がれた側（当てた側）のブーストを解除する。OFF：解除しない")]
    [SerializeField] private bool _cancelAttackerBoostOnBlock;
    [Tooltip("ブーストを解除されてから、再び発動できるようになるまでの秒数（押し続けていても、押し直しても発動しない）")]
    [SerializeField] private float _attackerBoostLockDuration = 1f;

    public float StunDuration => Mathf.Max(0f, _stunDuration);

    public float StunSpeedMultiplier => _stunSpeedMultiplier;

    public float InvincibleDuration => Mathf.Max(0f, _invincibleDuration);

    public float PushDuration => Mathf.Max(0f, _pushDuration);

    public float BounceDistance => Mathf.Max(0f, _bounceDistance);

    public float BounceInterval => Mathf.Max(0f, _bounceInterval);

    public float BlockPushSpeed => Mathf.Max(0f, _blockPushSpeed);

    public float BlockPushDuration => Mathf.Max(0f, _blockPushDuration);

    public bool UseBilliardBlockPush => _useBilliardBlockPush;

    public bool CancelAttackerBoostOnBlock => _cancelAttackerBoostOnBlock;

    public float AttackerBoostLockDuration => Mathf.Max(0f, _attackerBoostLockDuration);

    // シールドで防いだ側を前へ押し出す初速（m/s）。relativeSpeed：当てた側の前進の速さ − 防いだ側の前進の速さ（m/s）。
    // ビリヤード方式がOFFならBlockPushSpeedで一定。
    public float EvaluateBlockPushSpeed(float relativeSpeed)
    {
        if (!_useBilliardBlockPush)
        {
            return BlockPushSpeed;
        }

        return Mathf.Clamp(relativeSpeed * _billiardSpeedTransferRate, 0f, Mathf.Max(0f, _billiardMaxPushSpeed));
    }

    // closeness：0＝端をかすめた、1＝中心どうしが一致。
    public float EvaluatePushDistance(float closeness)
    {
        float rate = _pushCurve != null && _pushCurve.length > 0
            ? Mathf.Clamp01(_pushCurve.Evaluate(Mathf.Clamp01(closeness)))
            : Mathf.Clamp01(closeness);

        return Mathf.Max(0f, Mathf.Lerp(_pushMinDistance, _pushMaxDistance, rate));
    }
}
