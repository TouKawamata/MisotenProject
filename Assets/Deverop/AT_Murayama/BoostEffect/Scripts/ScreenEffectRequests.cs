using System.Collections.Generic;
using UnityEngine;

// 画面全体の演出（ラジアルブラー・集中線）を、今どこ（ブースト・シールドの押し出し・加速リングなど）が、どの強さで使っているかを覚える。
// 同時に使われているときは、一番強いものに合わせる。1つが終わっても、ほかが使っている間は演出が消えない。
public class ScreenEffectRequests
{
    private readonly Dictionary<object, float> _strengthByOwner = new Dictionary<object, float>();

    // 使っている中で一番強いもの（0～1）。どこも使っていなければ0。
    public float MaxStrength { get; private set; }

    // ownerが使い始める。もう使っていれば、強さを変える。
    public void Set(object owner, float strength)
    {
        _strengthByOwner[owner] = Mathf.Clamp01(strength);
        UpdateMaxStrength();
    }

    // ownerが使い終わる。使っていなければ何もしない。
    public void Remove(object owner)
    {
        if (_strengthByOwner.Remove(owner))
        {
            UpdateMaxStrength();
        }
    }

    public void Clear()
    {
        _strengthByOwner.Clear();
        MaxStrength = 0f;
    }

    private void UpdateMaxStrength()
    {
        MaxStrength = 0f;
        foreach (float strength in _strengthByOwner.Values)
        {
            MaxStrength = Mathf.Max(MaxStrength, strength);
        }
    }
}
