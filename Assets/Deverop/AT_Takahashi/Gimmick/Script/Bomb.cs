using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 接触すると爆発し、周囲のレーサーを吹き飛ばすコースギミック。
///
/// 爆弾の中心からの距離で、効果を2段階に分ける。
///     中心付近（内側の半径以内）：短い吹き飛び＋一瞬のスタン＋減速
///     爆風の外側（内側の半径から爆風の半径まで）：軽い押し出し（機体が揺れる）＋短時間の減速
///     範囲外：影響なし
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

    // 押し出す向きが決まらないとみなす、断面のずれの長さ[m]（PlayerFlightController.ApplyPlanarPushと同じ）。
    private const float UndeterminedPlanarLength = 0.05f;

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

    [Tooltip("中心付近の吹き飛ばし距離[m]（爆弾の端をかすめたとき）")]
    [SerializeField, Min(0f)]
    private float _innerPushMinDistance = 5f;

    [Tooltip("中心付近の吹き飛ばし距離[m]（爆弾の真正面から当たったとき）")]
    [SerializeField, Min(0f)]
    private float _innerPushMaxDistance = 12f;

    [Tooltip("中心付近で行動不能にする時間[秒]。一瞬だけ止める想定")]
    [SerializeField, Min(0f)]
    private float _innerStunDuration = 0.3f;

    [Tooltip("中心付近の爆風を受けたときの最高速度倍率。0にするとその間は止まる")]
    [SerializeField, Range(0f, 1f)]
    private float _innerMaxSpeedMultiplier = 0.3f;

    [Tooltip("中心付近の減速の持続時間[秒]")]
    [SerializeField, Min(0f)]
    private float _innerSlowDuration = 1f;

    [Tooltip("中心付近の減速のフェード時間[秒]。かかるときと戻るときの両方にかかる。0ならすぐ切り替わる")]
    [SerializeField, Min(0f)]
    private float _innerSlowFadeDuration = 0.1f;

    [Tooltip("後ろなら後退モード時の後退距離[m]")]
    [SerializeField, Min(0f)]
    private float _retreatDistance = 8f;

    [Header("爆風の外側")]

    [Tooltip("内側半径の境界での最大押し出し距離[m]。爆風の半径で0になる。機体が揺れる程度の小さめの値にする")]
    [SerializeField, Min(0f)]
    private float _outerPushDistance = 3f;

    [Tooltip("外側の爆風を受けたときの最高速度倍率")]
    [SerializeField, Range(0f, 1f)]
    private float _slowMaxSpeedMultiplier = 0.65f;

    [Tooltip("外側の減速の持続時間[秒]")]
    [SerializeField, Min(0f)]
    private float _slowDuration = 1f;

    [Tooltip("外側の減速のフェード時間[秒]。かかるときと戻るときの両方にかかる")]
    [SerializeField, Min(0f)]
    private float _slowFadeDuration = 0.25f;

    [Header("吹き飛ばし")]

    [Tooltip("吹き飛ばしにかける時間[秒]")]
    [SerializeField, Min(0.01f)]
    private float _pushDuration = 0.5f;

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

    [Header("可視化（エディタのみ。Gameビューでは右上のGizmosをON）")]

    [Tooltip("ON：選択していなくても範囲を表示する。OFF：選択しているときだけ表示する")]
    [SerializeField]
    private bool _showGizmosWhenNotSelected = true;

    [Tooltip("中心付近（内側の半径）の範囲を表示する")]
    [SerializeField]
    private bool _showInnerRadius = true;

    [Tooltip("爆風の外側（爆風の半径）の範囲を表示する")]
    [SerializeField]
    private bool _showBlastRadius = true;

    [Tooltip("中心付近で当たったレーサーが、爆弾の中心から最大どこまで飛ばされうるかを表示する")]
    [SerializeField]
    private bool _showMaxPushReach = true;

    [Tooltip("範囲の名前と半径を文字で表示する")]
    [SerializeField]
    private bool _showLabels = true;

    [Tooltip("爆発したとき、当たったレーサーへの線と、吹き飛ぶ向き・距離の矢印を表示する（中心付近：赤、外側：黄、後退：青）")]
    [SerializeField]
    private bool _showHitResults = true;

    [Tooltip("当たった結果を表示しておく時間[秒]")]
    [SerializeField, Min(0.1f)]
    private float _hitResultDuration = 5f;

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

    // 中心付近で当たったレーサーが、爆弾の中心から（コースの断面で）最大どこまで離れうるか[m]。
    // 断面のずれrのレーサーは、r＋Lerp(最大, 最小, r／内側の半径)の位置まで飛ぶ（rについて直線なので、両端のどちらかが最大）。
    private float MaxInnerPushReach =>
        Mathf.Max(_innerPushMaxDistance, _innerRadius + _innerPushMinDistance);

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

        Vector3 center = transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            _blastRadius,
            _overlaps,
            _racerLayerMask,
            QueryTriggerInteraction.Collide);

        // 爆風の範囲にいるレーサーへ、1人ずつ効果を当てる。
        _hitRacers.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _overlaps[i];

            if (hitCollider == null)
            {
                continue;
            }

            // Colliderは子オブジェクトに付いていることがあるので、親までさかのぼって探す。
            IGimmickRacer racer = hitCollider.GetComponentInParent<IGimmickRacer>();

            if (racer == null)
            {
                continue;
            }

            // 1人がColliderを複数持っていても、1回だけ当てる。
            if (!_hitRacers.Add(racer))
            {
                continue;
            }

            // 無敵中は効かない。
            if (racer.IsInvincible)
            {
                continue;
            }

            // シールドで防いだら効果なし。
            if (_canBeBlockedByShield && racer.TryBlockWithShield())
            {
                continue;
            }

            ApplyExplosionToRacer(racer, center);
        }

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
        ICourseGuide guide = racer.CourseGuide;

        if (guide == null)
        {
            return;
        }

        Vector3 racerPosition = racer.Transform.position;
        Vector3 toRacer = racerPosition - explosionCenter;

        float distanceFromCenter = toRacer.magnitude;

        Vector3 direction =
            toRacer.sqrMagnitude > 0.0001f
                ? toRacer.normalized
                : Vector3.zero;

        Vector3 forward =
            guide.GetForward(racerPosition).normalized;

        // 爆弾から見たレーサーのずれのうち、コース断面（上下左右）の成分[m]。
        // 押し出す向きはこのずれの向き。強さには掛けない
        // （真後ろから踏むとほぼ0になり、飛ばなくなるため）。
        Vector3 planarOffset =
            Vector3.ProjectOnPlane(toRacer, forward);

        if (distanceFromCenter <= _innerRadius)
        {
            ApplyInnerExplosion(racer, guide, explosionCenter, direction, planarOffset, forward, distanceFromCenter);
        }
        else
        {
            ApplyOuterExplosion(racer, guide, explosionCenter, planarOffset, distanceFromCenter);
        }

        NotifyActivated(racer);
    }

    /// <summary>
    /// 中心付近の強い爆発効果。
    /// 短い吹き飛び＋一瞬のスタン＋減速。
    /// </summary>
    private void ApplyInnerExplosion(
        IGimmickRacer racer,
        ICourseGuide guide,
        Vector3 explosionCenter,
        Vector3 direction,
        Vector3 planarOffset,
        Vector3 forward,
        float distanceFromCenter)
    {
        racer.Stun(_innerStunDuration);

        // スタンは入力を止めて最高速度を下げるだけなので、
        // 中心付近の減速は最高速度の倍率で別にかける。
        racer.Effects.SetMaxSpeedMultiplier(
            this,
            _innerMaxSpeedMultiplier,
            _innerSlowDuration,
            _innerSlowFadeDuration);

        // ブースト接触と同じく、中心に近いほど強く飛ばす。
        // 断面のずれが0（真正面）で1、内側の半径で0。
        float closeness =
            _innerRadius > 0f
                ? 1f - Mathf.Clamp01(planarOffset.magnitude / _innerRadius)
                : 1f;

        float pushDistance =
            Mathf.Lerp(_innerPushMinDistance, _innerPushMaxDistance, closeness);

        // 「後ろなら後退」モードの場合のみ、
        // 爆弾より後方にいるレーサーをさらに後退させる。
        bool retreats =
            _innerBlastMode == InnerBlastMode.RetreatIfBehind &&
            forward.sqrMagnitude > 0.0001f &&
            Vector3.Dot(direction, forward) < 0f;

        Debug.Log(
            $"{nameof(Bomb)}: 爆風の内側 {racer.Transform.name} " +
            $"中心から {distanceFromCenter:F1}m（内側の半径 {_innerRadius:F1}m） " +
            $"断面のずれ {planarOffset.magnitude:F1}m 近さ {closeness:F2} " +
            $"吹き飛ばし {pushDistance:F1}m スタン {_innerStunDuration:F1}秒 " +
            $"減速 ×{_innerMaxSpeedMultiplier:F2} {_innerSlowDuration:F1}秒" +
            $"{(retreats ? $" 後退 {_retreatDistance:F1}m" : "")}",
            this);

        // 向きが決まらないとき（真正面）は、窓口側でコースの中央側へ押し出す。
        racer.ApplyPlanarPush(
            planarOffset,
            pushDistance,
            _pushDuration);

        if (retreats)
        {
            racer.ApplyForwardPush(
                -_retreatDistance,
                _pushDuration);
        }

        DrawHitResult(
            racer,
            guide,
            explosionCenter,
            planarOffset,
            pushDistance,
            retreats ? -forward * _retreatDistance : Vector3.zero,
            true);
    }

    /// <summary>
    /// 外側の弱い爆風効果。
    /// スタンはさせず、軽い押し出し（機体が揺れる）＋短時間の減速を与える。
    /// </summary>
    private void ApplyOuterExplosion(
        IGimmickRacer racer,
        ICourseGuide guide,
        Vector3 explosionCenter,
        Vector3 planarOffset,
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

        Debug.Log(
            $"{nameof(Bomb)}: 爆風の外側 {racer.Transform.name} " +
            $"中心から {distanceFromCenter:F1}m（爆風の半径 {_blastRadius:F1}m） " +
            $"弱まり {outerFalloff:F2} 押し出し {pushDistance:F1}m " +
            $"減速 ×{_slowMaxSpeedMultiplier:F2} {_slowDuration:F1}秒",
            this);

        racer.ApplyPlanarPush(
            planarOffset,
            pushDistance,
            _pushDuration);

        racer.Effects.SetMaxSpeedMultiplier(
            this,
            _slowMaxSpeedMultiplier,
            _slowDuration,
            _slowFadeDuration);

        DrawHitResult(
            racer,
            guide,
            explosionCenter,
            planarOffset,
            pushDistance,
            Vector3.zero,
            false);
    }

    private IEnumerator StopEffectAfterDelay()
    {
        yield return new WaitForSeconds(_effectDuration);

        _onEffectStopped?.Invoke();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 当たったレーサーへの線と、吹き飛ぶ向き・距離の矢印を、一定時間表示する（エディタのみ）。
    /// 爆弾は爆発の数秒後に非表示になりGizmosが描かれなくなるため、秒数付きのDebug.DrawLineで描く。
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void DrawHitResult(
        IGimmickRacer racer,
        ICourseGuide guide,
        Vector3 explosionCenter,
        Vector3 planarOffset,
        float pushDistance,
        Vector3 retreatOffset,
        bool isInner)
    {
        if (!_showHitResults)
        {
            return;
        }

        Vector3 racerPosition = racer.Transform.position;
        Color hitColor = isInner ? InnerColor : OuterColor;

        // 爆弾の中心から、当たったレーサーへの線。
        Debug.DrawLine(explosionCenter, racerPosition, hitColor, _hitResultDuration);

        // 吹き飛ぶ向きと距離。向きが決まらないときは、ApplyPlanarPushと同じくコースの中央側（それも無ければ右）。
        Vector3 pushDirection = planarOffset;

        if (pushDirection.magnitude < UndeterminedPlanarLength)
        {
            Vector3 forward = guide.GetForward(racerPosition);
            pushDirection = Vector3.ProjectOnPlane(guide.GetCenterPosition(racerPosition) - racerPosition, forward);

            if (pushDirection.magnitude < UndeterminedPlanarLength)
            {
                pushDirection = guide.GetRight(racerPosition);
            }
        }

        Vector3 landing = racerPosition + pushDirection.normalized * pushDistance;
        DrawArrow(racerPosition, landing, hitColor, _hitResultDuration);

        // 後退するときは、その分も足す。
        if (retreatOffset.sqrMagnitude > 0.0001f)
        {
            DrawArrow(landing, landing + retreatOffset, RetreatColor, _hitResultDuration);
            landing += retreatOffset;
        }

        DrawCross(landing, 1f, hitColor, _hitResultDuration);
    }

    private static readonly Color InnerColor = new Color(1f, 0.25f, 0.1f, 1f);
    private static readonly Color OuterColor = new Color(1f, 0.85f, 0.1f, 1f);
    private static readonly Color RetreatColor = new Color(0.3f, 0.6f, 1f, 1f);
    private static readonly Color PushReachColor = new Color(0.3f, 1f, 0.9f, 0.6f);

    private static void DrawArrow(Vector3 from, Vector3 to, Color color, float duration)
    {
        Vector3 line = to - from;

        if (line.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Debug.DrawLine(from, to, color, duration);

        // 矢じり。線に垂直な向きを2つ取り、先端から少し戻った位置へ引く。
        float headLength = Mathf.Min(1.5f, line.magnitude * 0.3f);
        Vector3 back = -line.normalized * headLength;
        Vector3 side = Vector3.Cross(line, Vector3.up);

        if (side.sqrMagnitude < 0.0001f)
        {
            side = Vector3.Cross(line, Vector3.right);
        }

        side = side.normalized * headLength * 0.5f;
        Vector3 up = Vector3.Cross(side, line).normalized * headLength * 0.5f;

        Debug.DrawLine(to, to + back + side, color, duration);
        Debug.DrawLine(to, to + back - side, color, duration);
        Debug.DrawLine(to, to + back + up, color, duration);
        Debug.DrawLine(to, to + back - up, color, duration);
    }

    private static void DrawCross(Vector3 position, float size, Color color, float duration)
    {
        Debug.DrawLine(position - Vector3.right * size, position + Vector3.right * size, color, duration);
        Debug.DrawLine(position - Vector3.up * size, position + Vector3.up * size, color, duration);
        Debug.DrawLine(position - Vector3.forward * size, position + Vector3.forward * size, color, duration);
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        _blastRadius = Mathf.Max(0.01f, _blastRadius);
        _innerRadius = Mathf.Clamp(
            _innerRadius,
            0f,
            _blastRadius);

        _innerPushMinDistance = Mathf.Max(0f, _innerPushMinDistance);
        _innerPushMaxDistance = Mathf.Max(_innerPushMinDistance, _innerPushMaxDistance);
        _innerStunDuration = Mathf.Max(0f, _innerStunDuration);
        _innerMaxSpeedMultiplier = Mathf.Clamp01(_innerMaxSpeedMultiplier);
        _innerSlowDuration = Mathf.Max(0f, _innerSlowDuration);
        _innerSlowFadeDuration = Mathf.Max(0f, _innerSlowFadeDuration);
        _retreatDistance = Mathf.Max(0f, _retreatDistance);

        _outerPushDistance = Mathf.Max(0f, _outerPushDistance);
        _slowMaxSpeedMultiplier =
            Mathf.Clamp01(_slowMaxSpeedMultiplier);
        _slowDuration = Mathf.Max(0f, _slowDuration);
        _slowFadeDuration = Mathf.Max(0f, _slowFadeDuration);

        _pushDuration = Mathf.Max(0.01f, _pushDuration);
        _effectDuration = Mathf.Max(0f, _effectDuration);
        _hitResultDuration = Mathf.Max(0.1f, _hitResultDuration);
    }

    private void OnDrawGizmos()
    {
        if (_showGizmosWhenNotSelected)
        {
            DrawRangeGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmosWhenNotSelected)
        {
            DrawRangeGizmos();
        }
    }

    /// <summary>
    /// 爆風の外側・中心付近・吹き飛びの最大到達の範囲を描く。
    /// </summary>
    private void DrawRangeGizmos()
    {
        Vector3 center = transform.position;

        if (_showBlastRadius)
        {
            DrawRange(center, _blastRadius, new Color(OuterColor.r, OuterColor.g, OuterColor.b, 0.5f),
                $"爆風の外側 {_blastRadius:F1}m（押し出し・減速）");
        }

        if (_showInnerRadius)
        {
            DrawRange(center, _innerRadius, new Color(InnerColor.r, InnerColor.g, InnerColor.b, 0.8f),
                $"中心付近 {_innerRadius:F1}m（吹き飛び・スタン）");
        }

        if (_showMaxPushReach)
        {
            DrawRange(center, MaxInnerPushReach, PushReachColor,
                $"吹き飛びの最大到達 {MaxInnerPushReach:F1}m");
        }
    }

    private void DrawRange(Vector3 center, float radius, Color color, string label)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, radius);

        if (_showLabels)
        {
            GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
            style.normal.textColor = color;
            UnityEditor.Handles.Label(center + Vector3.up * radius, label, style);
        }
    }

#endif
}
