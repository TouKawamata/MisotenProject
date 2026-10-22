using UnityEngine;

// ショートカットを発動するかを決める。発動エリア（CPUManager.CurrentShortcutZone）に入った瞬間に、
// 難易度の成功率×個性のショートカット志向で、発動するかを1回だけ決める。反応時間が経ったら、エリアのトリガー方式で入力する。
//   KeyPress：ShortcutPressedを1Tickだけtrueにする。
//   HorizontalThreshold：Horizontalが閾値を「超えた瞬間」が必要なので、1Tick目は0、2Tick目は±1でHorizontalを上書きする。
// 発動しなかった（吸着にならなかった）ときは、少し待って数回までやり直す。
public class CpuShortcutBrain
{
    // ショートカット志向0～1で、成功率に掛ける倍率。
    private const float PreferenceMultiplierAtLow = 0.4f;
    private const float PreferenceMultiplierAtHigh = 1f;

    private const int MaxAttempts = 3;
    private const float RetryInterval = 0.3f;

    private ShortcutZone _zone;
    private bool _willTrigger;
    private float _delayTimer;
    private int _attempts;
    private bool _isWaitingForThresholdEdge;

    public bool ShortcutPressed { get; private set; }

    public bool OverridesHorizontal { get; private set; }

    public float HorizontalOverride { get; private set; }

    // steeringHorizontal：今のTickの操作。HorizontalThresholdのときに、左右どちらへ振るかに使う。
    public void Update(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random, float steeringHorizontal)
    {
        ShortcutPressed = false;
        OverridesHorizontal = false;
        HorizontalOverride = 0f;

        CPUManager owner = situation.Owner;
        ShortcutZone zone = owner.CurrentShortcutZone;
        if (zone != _zone)
        {
            EnterZone(zone, difficulty, personality, random);
        }

        if (zone == null || !_willTrigger || owner.IsShortcutProfileActive || _attempts >= MaxAttempts)
        {
            return;
        }

        _delayTimer -= situation.DeltaTime;
        if (_delayTimer > 0f)
        {
            return;
        }

        if (zone.TriggerMethod == EShortcutTriggerMethod.HorizontalThreshold)
        {
            OverridesHorizontal = true;
            if (!_isWaitingForThresholdEdge)
            {
                HorizontalOverride = 0f;
                _isWaitingForThresholdEdge = true;
                return;
            }

            HorizontalOverride = steeringHorizontal < 0f ? -1f : 1f;
            _isWaitingForThresholdEdge = false;
        }
        else
        {
            ShortcutPressed = true;
        }

        _attempts++;
        _delayTimer = RetryInterval;
    }

    public void Reset()
    {
        _zone = null;
        _willTrigger = false;
        _attempts = 0;
        _isWaitingForThresholdEdge = false;
        ShortcutPressed = false;
        OverridesHorizontal = false;
    }

    private void EnterZone(ShortcutZone zone, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random)
    {
        _zone = zone;
        _attempts = 0;
        _isWaitingForThresholdEdge = false;
        _delayTimer = difficulty.ReactionTime;

        float preference = Mathf.Lerp(PreferenceMultiplierAtLow, PreferenceMultiplierAtHigh, personality.ShortcutPreference);
        _willTrigger = zone != null && random.NextDouble() < difficulty.ShortcutSuccessRate * preference;
    }
}
