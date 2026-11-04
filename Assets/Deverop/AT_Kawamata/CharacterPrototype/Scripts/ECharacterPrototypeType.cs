using UnityEngine;

// キャラ性能のプロトタイプのタイプ。CharacterPrototypeManagerが切り替える。
public enum ECharacterPrototypeType
{
    [InspectorName("シールド中に減速しない")]
    NoShieldSlowdown,

    [InspectorName("ブーストの当たり判定が広い")]
    WideBoostHit,

    [InspectorName("妨害弾")]
    Jammer
}
