using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 接触すると爆発し、周囲のレーサーを吹き飛ばすコースギミック。
///
/// Collider:
///     SphereColliderなど
///     Is Trigger = ON
///
/// Layer:
///     Obstacle
/// </summary>
[DisallowMultipleComponent]
public sealed class Bomb : CourseGimmickBase
{
    private const string RacerLayerName = "Racer";

    [Header("爆発範囲")]

    [Tooltip("爆発の効果が届く半径[m]")]
    [SerializeField, Min(0f)]
    private float _blastRadius = 25f;

    [Header("吹き飛ばし")]

    [Tooltip("爆発中心で受けた場合の最大吹き飛ばし距離[m]")]
    [SerializeField, Min(0f)]
    private float _pushDistance = 15f;

    [Tooltip("吹き飛ばしにかける時間[秒]")]
    [SerializeField, Min(0.01f)]
    private float _pushDuration = 0.6f;

    [Header("行動不能")]

    [Tooltip("爆発を受けたレーサーを行動不能にする時間[秒]")]
    [SerializeField, Min(0f)]
    private float _stunDuration = 0.8f;

    [Header("防御")]

    [Tooltip("ONの場合、シールドで爆発を防げる")]
    [SerializeField]
    private bool _canBeBlockedByShield = true;

    [Header("演出")]

    [Tooltip("爆発時に呼ばれるイベント。爆発エフェクトやSEを接続する")]
    [SerializeField]
    private UnityEvent _onExploded;

    [Tooltip("爆発演出を停止するまでの時間[秒]")]
    [SerializeField, Min(0f)]
    private float _effectDuration = 3f;

    [Tooltip("爆発演出を停止するときに呼ばれるイベント")]
    [SerializeField]
    private UnityEvent _onEffectStopped;

    // NonAlloc用。
    // 1回の爆発範囲内に想定以上のColliderが存在する場合は増やす。
    private readonly Collider[] _overlaps = new Collider[32];

    // 1人のレーサーが複数Colliderを持っていても
    // 同じ爆発で複数回ヒットしないようにする。
    private readonly HashSet<IGimmickRacer> _hitRacers =
        new HashSet<IGimmickRacer>();

    private int _racerLayerMask;

    /// <summary>
    /// Inspectorや演出側から参照するための読み取り専用プロパティ。
    /// </summary>
    public float BlastRadius => _blastRadius;

    protected override void Awake()
    {
        base.Awake();

        _racerLayerMask = LayerMask.GetMask(RacerLayerName);

        if (_racerLayerMask == 0)
        {
            Debug.LogWarning(
                $"{nameof(Bomb)}: {RacerLayerName} レイヤーが見つかりません。",
                this);
        }
    }

    public override void OnRacerEnter(in GimmickContact contact)
    {
        Explode();
    }

    /// <summary>
    /// 爆弾を爆発させる。
    /// </summary>
    private void Explode()
    {
        // 複数レーサーが同じTickで触れた場合などに、
        // 爆発済みの爆弾がもう一度処理されるのを防ぐ。
        if (!gameObject.activeSelf)
        {
            return;
        }

        Vector3 center = transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            _blastRadius,
            _overlaps,
            _racerLayerMask,
            QueryTriggerInteraction.Collide);

        _hitRacers.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _overlaps[i];

            if (hitCollider == null)
            {
                continue;
            }

            IGimmickRacer racer =
                hitCollider.GetComponentInParent<IGimmickRacer>();

            if (racer == null)
            {
                continue;
            }

            // 1人が複数Colliderを持っていても1回だけ処理する。
            if (!_hitRacers.Add(racer))
            {
                continue;
            }

            // 無敵中には効かせない。
            if (racer.IsInvincible)
            {
                continue;
            }

            // シールドで防げる設定なら先に判定する。
            if (_canBeBlockedByShield &&
                racer.TryBlockWithShield())
            {
                continue;
            }

            ApplyExplosionToRacer(racer, center);
        }

        // エフェクト・SEなど。
        _onExploded?.Invoke();

        // 爆発後は無効化。
        StartCoroutine(StopEffectAfterDelay());
    }

    /// <summary>
    /// 1人のレーサーに爆発効果を適用する。
    /// </summary>
    private void ApplyExplosionToRacer(
        IGimmickRacer racer,
        Vector3 explosionCenter)
    {
        Vector3 racerPosition = racer.Transform.position;

        Vector3 toRacer =
            racerPosition - explosionCenter;

        float distanceFromCenter =
            toRacer.magnitude;

        // 爆発中心 = 1
        // 爆発半径の端 = 0
        float falloff =
            1f - Mathf.Clamp01(
                distanceFromCenter / _blastRadius);

        float pushDistance =
            _pushDistance * falloff;

        Vector3 direction =
            toRacer.sqrMagnitude > 0.0001f
                ? toRacer.normalized
                : Vector3.zero;

        // 先にスタン。
        racer.Stun(_stunDuration);

        // 横方向への吹き飛ばし。
        racer.ApplyLateralPush(
            direction,
            pushDistance,
            _pushDuration);

        // コース前後方向への吹き飛ばし。
        ICourseGuide guide = racer.CourseGuide;

        if (guide != null)
        {
            Vector3 courseForward =
                guide.GetForward(racerPosition);

            float forwardAmount =
                Vector3.Dot(direction, courseForward);

            racer.ApplyForwardPush(
                forwardAmount * pushDistance,
                _pushDuration);
        }

        // このレーサーに爆弾の効果が発生したことを通知。
        NotifyActivated(racer);
    }

    private IEnumerator StopEffectAfterDelay()
    {
        yield return new WaitForSeconds(_effectDuration);

        _onEffectStopped?.Invoke();

        gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        _blastRadius = Mathf.Max(0.01f, _blastRadius);
        _pushDistance = Mathf.Max(0f, _pushDistance);
        _pushDuration = Mathf.Max(0.01f, _pushDuration);
        _stunDuration = Mathf.Max(0f, _stunDuration);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _blastRadius);
    }

#endif
}