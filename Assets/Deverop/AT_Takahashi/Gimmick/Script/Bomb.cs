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

    public enum InnerBlastMode
    {
        [InspectorName("断面だけ")]
        PlanarOnly,

        [InspectorName("後ろなら後退")]
        RetreatIfBehind
    }

    [Header("爆発範囲")]

    [Tooltip("中心付近として扱う半径[m]")]
    [SerializeField, Min(0f)]
    private float _innerRadius = 10f;

    [Tooltip("爆発の効果が届く半径[m]")]
    [SerializeField, Min(0.01f)]
    private float _blastRadius = 25f;

    [Header("中心付近")]

    [Tooltip("中心付近の吹き飛ばし方")]
    [SerializeField]
    private InnerBlastMode _innerBlastMode = InnerBlastMode.PlanarOnly;

    [Tooltip("中心付近の吹き飛ばし距離[m]")]
    [SerializeField, Min(0f)]
    private float _innerPushDistance = 15f;

    [Tooltip("中心付近で行動不能にする時間[秒]")]
    [SerializeField, Min(0f)]
    private float _innerStunDuration = 0.8f;

    [Tooltip("後ろなら後退モード時の後退距離[m]")]
    [SerializeField, Min(0f)]
    private float _retreatDistance = 8f;

    [Header("爆風の外側")]

    [Tooltip("内側半径の境界での最大押し出し距離[m]")]
    [SerializeField, Min(0f)]
    private float _outerPushDistance = 6f;

    [Tooltip("外側の爆風を受けたときの最高速度倍率")]
    [SerializeField, Range(0f, 1f)]
    private float _slowMaxSpeedMultiplier = 0.65f;

    [Tooltip("減速の持続時間[秒]")]
    [SerializeField, Min(0f)]
    private float _slowDuration = 1.5f;

    [Tooltip("減速効果のフェード時間[秒]")]
    [SerializeField, Min(0f)]
    private float _slowFadeDuration = 0.25f;

    [Header("吹き飛ばし")]

    [Tooltip("吹き飛ばしにかける時間[秒]")]
    [SerializeField, Min(0.01f)]
    private float _pushDuration = 0.6f;

    [Header("防御")]

    [Tooltip("ONの場合、シールドで爆発を防げる")]
    [SerializeField]
    private bool _canBeBlockedByShield = true;

    [Header("演出")]

    [Tooltip("爆発時に呼ばれるイベント。爆弾位置のエフェクトやSEを接続する")]
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

    // 1人のレーサーが複数Colliderを持っていても、
    // 同じ爆発で複数回ヒットしないようにする。
    private readonly HashSet<IGimmickRacer> _hitRacers =
        new HashSet<IGimmickRacer>();

    private int _racerLayerMask;

    // 爆発演出中に別のレーサーがColliderへ触れても
    // 二重に爆発しないためのフラグ。
    private Collider _triggerCollider;
    private bool _hasExploded;

    /// <summary>
    /// Inspectorや演出側から参照するための読み取り専用プロパティ。
    /// </summary>
    public float BlastRadius => _blastRadius;

    public float InnerRadius => _innerRadius;

    protected override void Awake()
    {
        base.Awake();

        _triggerCollider = GetComponent<Collider>();

        _racerLayerMask = LayerMask.GetMask(RacerLayerName);

        if (_racerLayerMask == 0)
        {
            Debug.LogWarning(
                $"{nameof(Bomb)}: {RacerLayerName} レイヤーが見つかりません。",
                this);
        }
    }

    private void OnEnable()
    {
        _hasExploded = false;

        if (_triggerCollider != null)
        {
            _triggerCollider.enabled = true;
        }
    }


    public override void OnRacerEnter(in GimmickContact contact)
    {
        Debug.Log($"{nameof(Bomb)}: OnRacerEnter", this);

        Explode();
    }

    /// <summary>
    /// 爆弾を爆発させる。
    /// </summary>
    private void Explode()
    {
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;

        if (_triggerCollider != null)
        {
            _triggerCollider.enabled = false;
        }

        Debug.Log("Bomb: EXPLODE", this);

        Vector3 center = transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            _blastRadius,
            _overlaps,
            _racerLayerMask,
            QueryTriggerInteraction.Collide);

        // 以下、爆風処理
        // ...

        _onExploded?.Invoke();

        StartCoroutine(StopEffectAfterDelay());
    }

    /// <summary>
    /// 1人のレーサーに爆発効果を適用する。
    /// </summary>
    private void ApplyExplosionToRacer(
    IGimmickRacer racer,
    Vector3 explosionCenter)
    {
        Debug.Log("Bomb: ApplyExplosion START", this);

        Debug.Log(
            $"Bomb: CourseGuide = " +
            $"{(racer.CourseGuide != null ? "FOUND" : "NULL")}",
            this);

        Vector3 racerPosition = racer.Transform.position;
        Vector3 toRacer = racerPosition - explosionCenter;

        float distanceFromCenter = toRacer.magnitude;

        Vector3 direction =
            toRacer.sqrMagnitude > 0.0001f
                ? toRacer.normalized
                : Vector3.zero;

        ICourseGuide guide = racer.CourseGuide;

        if (guide == null)
        {
            Debug.LogError(
                "Bomb: CourseGuide is NULL",
                this);
            return;
        }

        Vector3 forward =
            guide.GetForward(racerPosition).normalized;

        Vector3 planar =
            Vector3.ProjectOnPlane(direction, forward);

        Debug.Log(
            $"Bomb PUSH CHECK\n" +
            $"distance = {distanceFromCenter:F2}\n" +
            $"innerRadius = {_innerRadius:F2}\n" +
            $"blastRadius = {_blastRadius:F2}\n" +
            $"direction = {direction}\n" +
            $"forward = {forward}\n" +
            $"planar = {planar}\n" +
            $"planarMag = {planar.magnitude:F3}",
            this);

        if (distanceFromCenter <= _innerRadius)
        {
            float actualPushDistance =
                _innerPushDistance * planar.magnitude;

            Debug.Log(
                $"Bomb: INNER / " +
                $"push={actualPushDistance:F2} / " +
                $"stun={_innerStunDuration:F2}",
                this);

            racer.Stun(_innerStunDuration);

            racer.ApplyPush(
                planar,
                actualPushDistance,
                _pushDuration);
        }
        else
        {
            float outerRange =
                Mathf.Max(
                    0.0001f,
                    _blastRadius - _innerRadius);

            float outerFalloff =
                1f - Mathf.Clamp01(
                    (distanceFromCenter - _innerRadius)
                    / outerRange);

            float actualPushDistance =
                _outerPushDistance
                * outerFalloff
                * planar.magnitude;

            Debug.Log(
                $"Bomb: OUTER / " +
                $"falloff={outerFalloff:F3} / " +
                $"push={actualPushDistance:F2}",
                this);

            racer.ApplyPush(
                planar,
                actualPushDistance,
                _pushDuration);

            racer.Effects.SetMaxSpeedMultiplier(
                this,
                _slowMaxSpeedMultiplier,
                _slowDuration,
                _slowFadeDuration);
        }

        NotifyActivated(racer);
    }

    /// <summary>
    /// 中心付近の強い爆発効果。
    /// </summary>
    private void ApplyInnerExplosion(
        IGimmickRacer racer,
        Vector3 direction,
        Vector3 planar,
        Vector3 forward)
    {
        racer.Stun(_innerStunDuration);

        // ApplyPush側で方向は正規化されるため、
        // コース断面成分の大きさを距離側へ掛ける。
        racer.ApplyPush(
            planar,
            30f,
            1f);

        // 「後ろなら後退」モードの場合のみ、
        // 爆弾より後方にいるレーサーをさらに後退させる。
        if (_innerBlastMode == InnerBlastMode.RetreatIfBehind &&
            forward.sqrMagnitude > 0.0001f)
        {
            float forwardAmount =
                Vector3.Dot(direction, forward);

            if (forwardAmount < 0f)
            {
                racer.ApplyForwardPush(
                    -_retreatDistance,
                    _pushDuration);
            }
        }
    }

    /// <summary>
    /// 外側の弱い爆風効果。
    /// スタンはさせず、押し出し＋減速を与える。
    /// </summary>
    private void ApplyOuterExplosion(
        IGimmickRacer racer,
        Vector3 planar,
        float distanceFromCenter)
    {
        // 内側半径 = 1
        // 爆風半径 = 0
        float outerRange =
            Mathf.Max(0.0001f, _blastRadius - _innerRadius);

        float outerFalloff =
            1f - Mathf.Clamp01(
                (distanceFromCenter - _innerRadius) / outerRange);

        float pushDistance =
            _outerPushDistance * outerFalloff;

        // ApplyPushが方向を正規化するため、
        // planar成分の大きさを距離に反映する。
        racer.ApplyPush(
            planar,
            pushDistance * planar.magnitude,
            _pushDuration);

        racer.Effects.SetMaxSpeedMultiplier(
            this,
            _slowMaxSpeedMultiplier,
            _slowDuration,
            _slowFadeDuration);
    }

    private IEnumerator StopEffectAfterDelay()
    {
        Debug.Log(
            $"Bomb: waiting {_effectDuration} sec",
            this);

        yield return new WaitForSeconds(_effectDuration);

        Debug.Log(
            "Bomb: disabling GameObject",
            this);

        _onEffectStopped?.Invoke();

        gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        _blastRadius = Mathf.Max(0.01f, _blastRadius);
        _innerRadius = Mathf.Clamp(
            _innerRadius,
            0f,
            _blastRadius);

        _innerPushDistance = Mathf.Max(0f, _innerPushDistance);
        _innerStunDuration = Mathf.Max(0f, _innerStunDuration);
        _retreatDistance = Mathf.Max(0f, _retreatDistance);

        _outerPushDistance = Mathf.Max(0f, _outerPushDistance);
        _slowMaxSpeedMultiplier =
            Mathf.Clamp01(_slowMaxSpeedMultiplier);
        _slowDuration = Mathf.Max(0f, _slowDuration);
        _slowFadeDuration = Mathf.Max(0f, _slowFadeDuration);

        _pushDuration = Mathf.Max(0.01f, _pushDuration);
        _effectDuration = Mathf.Max(0f, _effectDuration);
    }

    private void OnDrawGizmosSelected()
    {
        // 外側の爆風範囲。
        Gizmos.color =
            new Color(1f, 0.25f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(
            transform.position,
            _blastRadius);

        // 内側の強い爆発範囲。
        Gizmos.color =
            new Color(1f, 0.85f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(
            transform.position,
            _innerRadius);
    }

#endif
}