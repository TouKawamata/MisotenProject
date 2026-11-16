using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class SpeedRing : CourseGimmickBase
{
    [SerializeField, InspectorName("最初から減速リング")] private bool _startSlowing;
    [SerializeField, InspectorName("切り替え対象タグ")] private string _switchTag = "Untagged";
    [SerializeField, InspectorName("加速の強さ（m/s）"), Min(0f)] private float _extraSpeed = 15f;
    [SerializeField, InspectorName("減速時の最高速度倍率"), Range(0f, 1f)] private float _slowMultiplier = 0.55f;
    [SerializeField, InspectorName("効果時間（秒）"), Min(0.01f)] private float _duration = 1f;
    [SerializeField, InspectorName("リングの見た目")] private SpeedRingVisual _visual;
    [SerializeField, Range(0.1f, 1f), InspectorName("楕円判定の大きさ")]
    private float _ellipseScale = 1f;


    private bool _isSlowing;
    private readonly HashSet<IGimmickRacer> _activatedRacers = new HashSet<IGimmickRacer>();

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

        _isSlowing = true;
        _visual?.SetSlowing(true);
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
        }
        else
        {
            float distance = LateralPush.GetDistanceForInitialSpeed(_extraSpeed, _duration);
            racer.ApplyForwardPush(distance, _duration);
        }

        NotifyActivated(racer);
    }
}
