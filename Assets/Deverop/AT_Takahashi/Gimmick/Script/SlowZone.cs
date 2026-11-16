using UnityEngine;

/// <summary>
/// エリア内にいるレーサーの最高速度を低下させるコースギミック。
///
/// 使用方法:
/// 1. このコンポーネントを減速エリアのGameObjectに追加する。
/// 2. このGameObject、または子GameObjectにColliderを追加する。
/// 3. ColliderのIs TriggerをONにする。
/// 4. Colliderが付いたGameObjectのレイヤーをObstacleにする。
/// </summary>
[DisallowMultipleComponent]
public sealed class SlowZone : CourseGimmickBase
{
    [Header("減速設定")]

    [Tooltip("エリア内での最高速度倍率。0で停止、1で変化なし。")]
    [SerializeField, Range(0f, 1f)]
    private float _speedMultiplier = 0.6f;

    [Tooltip("減速効果が目標倍率まで変化する時間。")]
    [SerializeField, Min(0f)]
    private float _fadeDuration = 0.3f;

    /// <summary>
    /// 現在設定されている最高速度倍率。
    /// 演出やデバッグ表示から参照できます。
    /// </summary>
    public float SpeedMultiplier => _speedMultiplier;

    /// <summary>
    /// 減速倍率へ移行する時間。
    /// </summary>
    public float FadeDuration => _fadeDuration;

    public override void OnRacerEnter(in GimmickContact contact)
    {
        IGimmickRacer racer = contact.Racer;

        if (racer == null || racer.Effects == null)
        {
            return;
        }

        ApplySlowEffect(racer);

        // 減速効果が発生したことをギミック演出とレーサー側へ通知する。
        NotifyActivated(racer);
    }

    public override void OnRacerExit(IGimmickRacer racer)
    {
        if (racer == null || racer.Effects == null)
        {
            return;
        }

        // UntilRemovedで設定した効果は、退出時に必ず解除する。
        racer.Effects.Remove(this);
    }

    private void ApplySlowEffect(IGimmickRacer racer)
    {
        racer.Effects.SetMaxSpeedMultiplier(
            this,
            _speedMultiplier,
            RacerEffects.UntilRemoved,
            _fadeDuration);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _speedMultiplier = Mathf.Clamp01(_speedMultiplier);
        _fadeDuration = Mathf.Max(0f, _fadeDuration);
    }
#endif
}
