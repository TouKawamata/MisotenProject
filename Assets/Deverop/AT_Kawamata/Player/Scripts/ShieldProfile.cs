using UnityEngine;

// シールドの設定の塊。シールドは押している間ずっと張れるが、その間は最高速度が落ちる。
// ブースト接触を1回防ぐと消え、一定時間使えなくなる。順位では変わらない。
[CreateAssetMenu(fileName = "ShieldProfile", menuName = "Player/ShieldProfile")]
public class ShieldProfile : ScriptableObject
{
    [Tooltip("シールドを張っている間の最高速度の倍率（1＝そのまま）")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxSpeedMultiplier = 0.8f;
    [Tooltip("ブースト接触を防いでシールドが消えてから、再び使えるようになるまでの秒数")]
    [SerializeField] private float _brokenDuration = 5f;

    public float MaxSpeedMultiplier => _maxSpeedMultiplier;

    public float BrokenDuration => Mathf.Max(0f, _brokenDuration);
}
