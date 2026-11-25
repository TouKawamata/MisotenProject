using System;
using System.Collections.Generic;
using UnityEngine;

// 加速リング。妨害弾（RingSwapJammerType）が当たる、または切り替え対象タグのものが触れると減速リングに変わる。
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class SpeedRing : CourseGimmickBase, IJammerTarget
{
    [SerializeField, InspectorName("最初から減速リング")] private bool _startSlowing;
    [Tooltip("妨害弾・タグで減速リングになってから、加速リングに戻るまでの秒数。0なら戻らない（最初から減速リングのものは戻らない）")]
    [SerializeField, InspectorName("加速リングに戻るまでの秒数"), Min(0f)] private float _revertDelay = 5f;
    [SerializeField, InspectorName("切り替え対象タグ")] private string _switchTag = "Untagged";
    [SerializeField, InspectorName("加速の強さ（m/s）"), Min(0f)] private float _extraSpeed = 15f;
    [SerializeField, InspectorName("減速時の最高速度倍率"), Range(0f, 1f)] private float _slowMultiplier = 0.55f;
    [Tooltip("ON：減速リングの効果時間中は、ブーストできない（ブースト中に通ったら止まる）。OFFだと、減速中もブーストした分だけ速くなる")]
    [SerializeField, InspectorName("減速中はブースト不可")] private bool _lockBoostWhileSlowed = true;
    [SerializeField, InspectorName("効果時間（秒）"), Min(0.01f)] private float _duration = 1f;
    [SerializeField, InspectorName("リングの見た目")] private SpeedRingVisual _visual;
    [SerializeField, Range(0.1f, 1f), InspectorName("楕円判定の大きさ")]
    private float _ellipseScale = 1f;


    private bool _isSlowing;
    private float _revertTimer;
    private readonly HashSet<IGimmickRacer> _activatedRacers = new HashSet<IGimmickRacer>();

    // 今、減速リングになっているか（CPUが読む）。
    public bool IsSlowing => _isSlowing;

    // 加速・減速の効果時間（秒）。
    public float Duration => _duration;

    private bool IsInsideEllipse(Vector3 worldPoint)
    {
        BoxCollider area = GetComponent<BoxCollider>();
        Vector3 local = transform.InverseTransformPoint(worldPoint) - area.center;
        float radiusX = Mathf.Max(area.size.x * 0.5f * _ellipseScale, 0.0001f);
        float radiusY = Mathf.Max(area.size.y * 0.5f * _ellipseScale, 0.0001f);
        float x = local.x / radiusX;
        float y = local.y / radiusY;
        return x * x + y * y <= 1f;
    }

    private void OnDrawGizmos()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        if (area == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.red;
        Vector3 radii = area.size * 0.5f;
        radii.x *= _ellipseScale;
        radii.y *= _ellipseScale;
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 start = area.center + new Vector3(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y, 0f);
            Vector3 end = area.center + new Vector3(Mathf.Cos(b) * radii.x, Mathf.Sin(b) * radii.y, 0f);
            Vector3 depth = Vector3.forward * radii.z;
            Gizmos.DrawLine(start - depth, end - depth);
            Gizmos.DrawLine(start + depth, end + depth);
            if (i % 8 == 0)
                Gizmos.DrawLine(start - depth, start + depth);
        }
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    protected override void Awake()
    {
        base.Awake();
        _isSlowing = _startSlowing;
        _visual?.SetSlowing(_isSlowing);

        // Bonus レイヤーとは別に、通常の物理レイヤーでタグ接触を受ける。
        BoxCollider passArea = GetComponent<BoxCollider>();
        var contactArea = new GameObject("Tag Contact Area");
        contactArea.transform.SetParent(transform, false);
        var trigger = contactArea.AddComponent<BoxCollider>();
        trigger.center = passArea.center;
        trigger.size = passArea.size;
        trigger.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || _isSlowing || string.IsNullOrEmpty(_switchTag) || _switchTag == "Untagged")
            return;

        bool matches = string.Equals(other.tag, _switchTag, StringComparison.Ordinal)
            || (other.attachedRigidbody != null
                && string.Equals(other.attachedRigidbody.tag, _switchTag, StringComparison.Ordinal));
        if (!matches)
            return;

        if (!IsInsideEllipse(other.ClosestPoint(transform.TransformPoint(GetComponent<BoxCollider>().center))))
            return;

        SwitchToSlowing();
    }

    private void SwitchToSlowing()
    {
        _isSlowing = true;
        _revertTimer = _revertDelay;
        _visual?.SetSlowing(true);
    }

    private void Update()
    {
        // 秒数が0（戻らない）か、最初から減速リングのものはタイマーが0のまま。
        if (!_isSlowing || _revertTimer <= 0f)
            return;

        _revertTimer -= Time.deltaTime;
        if (_revertTimer > 0f)
            return;

        _isSlowing = false;
        _visual?.SetSlowing(false);
    }

    // 妨害弾が狙う位置（リングの中心）。
    public Vector3 JammerAimPosition => transform.TransformPoint(GetComponent<BoxCollider>().center);

    // まだ加速リングのときだけ、妨害弾の標的になる。
    public bool CanReceiveJammer => isActiveAndEnabled && !_isSlowing;

    public void ReceiveJammer(JammerTypeProfile type)
    {
        if (CanReceiveJammer)
            SwitchToSlowing();
    }

    private void OnEnable()
    {
        JammerTargetRegistry.Register(this);
    }

    public override void OnRacerEnter(in GimmickContact contact)
    {
        TryActivate(contact);
    }

    public override void OnRacerStay(in GimmickContact contact, float deltaTime)
    {
        // A racer can enter through a rejected box corner and reach the ellipse later.
        TryActivate(contact);
    }

    public override void OnRacerExit(IGimmickRacer racer)
    {
        _activatedRacers.Remove(racer);
    }

    private void OnDisable()
    {
        JammerTargetRegistry.Unregister(this);
        _activatedRacers.Clear();
    }

    private void TryActivate(in GimmickContact contact)
    {
        IGimmickRacer racer = contact.Racer;
        // Use the sensor's swept contact rather than the racer's final position,
        // so crossing the whole ring within one tick still works.
        if (!IsInsideEllipse(contact.Point) || !_activatedRacers.Add(racer))
            return;

        if (_isSlowing)
        {
            racer.Effects.SetMaxSpeedMultiplier(this, _slowMultiplier, _duration);
            if (_lockBoostWhileSlowed)
            {
                racer.Effects.LockBoost(this, _duration);
            }
        }
        else
        {
            float distance = LateralPush.GetDistanceForInitialSpeed(_extraSpeed, _duration);
            racer.ApplyForwardPush(distance, _duration);
        }

        NotifyActivated(racer);
    }
}
