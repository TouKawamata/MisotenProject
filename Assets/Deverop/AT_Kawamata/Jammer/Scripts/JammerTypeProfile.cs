using UnityEngine;

// 妨害弾の種類（当たったときの効果）の継承元。種類ごとに派生クラスを作り、アセットをJammerLauncherのTypesに入れる。
// 効果はIGimmickRacer（コースギミックと同じ窓口）で反映する。効果の出どころ（source）はこのアセット自身にするので、
// 同じ種類が当たり直すと上書き（秒数が最初から）になり、重ねがけしない。別の種類なら両方の効果がかかる。
// シールドで防げるか・防いだときに加速するかは、受ける側（RacerManagerBase）がこの設定を見て決める。
public abstract class JammerTypeProfile : ScriptableObject
{
    [Tooltip("Inspectorのボタンやログに出す名前")]
    [SerializeField] private string _displayName = "妨害弾";

    [Header("シールド")]
    [Tooltip("ON：シールド中なら防げる（シールドは消える）。OFF：シールド中でも効果を受ける（シールドは残る）")]
    [SerializeField] private bool _canBeBlockedByShield = true;
    [Tooltip("ON：シールドで防いだとき、前へ押し出して加速する（ブースト接触を防いだときと同じ演出が出る）")]
    [SerializeField] private bool _pushOnBlock = true;
    [Tooltip("防いだときの押し出しの初速（m/s）。Block Push Durationの秒数で0まで落ちる")]
    [SerializeField] private float _blockPushSpeed = 12f;
    [Tooltip("防いだときに押し出し切るまでの秒数")]
    [SerializeField] private float _blockPushDuration = 1f;

    [Header("効果")]
    [Tooltip("効果が続く秒数")]
    [SerializeField] private float _effectDuration = 3f;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

    public bool CanBeBlockedByShield => _canBeBlockedByShield;

    public bool PushOnBlock => _pushOnBlock;

    public float BlockPushSpeed => Mathf.Max(0f, _blockPushSpeed);

    public float BlockPushDuration => Mathf.Max(0f, _blockPushDuration);

    public float EffectDuration => Mathf.Max(0f, _effectDuration);

    // true：レーサーではなく、JammerLauncherのRing Targetsのうち一番近いものを狙う。
    public virtual bool TargetsRing => false;

    // レーサーが効果を受けるとき（シールドで防がれなかったとき）。
    public virtual void ApplyToRacer(IGimmickRacer racer)
    {
    }

    // レーサー以外の標的（リングなど）に当たったとき。
    public virtual void ApplyToTarget(GameObject target)
    {
    }
}
