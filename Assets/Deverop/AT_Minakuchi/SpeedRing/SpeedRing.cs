using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class SpeedRing : CourseGimmickBase
{
    [SerializeField, InspectorName("最初から減速リング")] private bool _startSlowing;
    [SerializeField] private string _switchTag = "Untagged";
    [SerializeField, InspectorName("加速の強さ（m/s）"), Min(0f)] private float _extraSpeed = 15f;
    [SerializeField, InspectorName("減速時の最高速度倍率"), Range(0f, 1f)] private float _slowMultiplier = 0.55f;
    [SerializeField, InspectorName("効果時間（秒）"), Min(0.01f)] private float _duration = 1f;
    [SerializeField, InspectorName("リングの見た目")] private SpeedRingVisual _visual;


    private bool _isSlowing;

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

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || _isSlowing || string.IsNullOrEmpty(_switchTag) || _switchTag == "Untagged")
            return;

        bool matches = string.Equals(other.tag, _switchTag, StringComparison.Ordinal)
            || (other.attachedRigidbody != null
                && string.Equals(other.attachedRigidbody.tag, _switchTag, StringComparison.Ordinal));
        if (!matches)
            return;

        _isSlowing = true;
        _visual?.SetSlowing(true);
    }

    public override void OnRacerEnter(in GimmickContact contact)
    {
        IGimmickRacer racer = contact.Racer;
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
