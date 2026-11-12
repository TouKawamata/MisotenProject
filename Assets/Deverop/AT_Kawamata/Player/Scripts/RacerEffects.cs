using System.Collections.Generic;
using UnityEngine;

// レーサー1人分の、時間付きの効果を覚えて、毎Tickの倍率・状態を求める（IRacerEffectsの約束の通り）。
// RacerManagerBaseが持ち、Tickで秒数を進め、最高速度・左右の移動・ブーストの発動・視界反転に反映する。
// 種類を足すときは、EKindに足して、Tickで出力を求める処理と、窓口のSet〇〇を足す。
public class RacerEffects : IRacerEffects
{
    // durationに渡すと、Removeするまで続く。
    public const float UntilRemoved = float.PositiveInfinity;

    private enum EKind
    {
        MaxSpeed,
        Lateral,
        BoostLock,
        ViewFlip
    }

    private class Entry
    {
        public object Source;
        public EKind Kind;
        public float Value;
        public float Remaining;
        public float FadeDuration;
    }

    // 倍率1つ分の出力。一番強い効果の倍率へ、その効果のFadeDurationの秒数をかけて近づける（効果が外れたら、最後に使った秒数で1へ戻す）。
    private class FadedMultiplier
    {
        private float _fadeDuration;

        public float Value { get; private set; } = 1f;

        public void Tick(float target, float fadeDuration, bool hasEntry, float deltaTime)
        {
            if (hasEntry)
            {
                _fadeDuration = fadeDuration;
            }

            Value = _fadeDuration > 0f ? Mathf.MoveTowards(Value, target, deltaTime / _fadeDuration) : target;
        }

        public void Reset()
        {
            _fadeDuration = 0f;
            Value = 1f;
        }
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly FadedMultiplier _maxSpeed = new FadedMultiplier();
    private readonly FadedMultiplier _lateral = new FadedMultiplier();

    public float MaxSpeedMultiplier => _maxSpeed.Value;

    public float LateralMultiplier => _lateral.Value;

    public bool IsBoostLocked { get; private set; }

    public EViewFlip ViewFlip { get; private set; }

    public void SetMaxSpeedMultiplier(object source, float multiplier, float duration, float fadeDuration = 0f)
    {
        Set(source, EKind.MaxSpeed, Mathf.Clamp01(multiplier), duration, fadeDuration);
    }

    public void SetLateralMultiplier(object source, float multiplier, float duration, float fadeDuration = 0f)
    {
        Set(source, EKind.Lateral, Mathf.Clamp01(multiplier), duration, fadeDuration);
    }

    // ブースト禁止は、かけた瞬間から効く（このTickのうちに発動させない）。
    public void LockBoost(object source, float duration)
    {
        Set(source, EKind.BoostLock, 0f, duration, 0f);
        IsBoostLocked = true;
    }

    // 視界反転も、かけた瞬間から効く。向きはValueに入れておく（Noneなら何もしない）。
    public void SetViewFlip(object source, EViewFlip flip, float duration)
    {
        if (flip == EViewFlip.None)
        {
            return;
        }

        Set(source, EKind.ViewFlip, (float)flip, duration, 0f);
        ViewFlip |= flip;
    }

    public void Remove(object source)
    {
        _entries.RemoveAll(entry => Equals(entry.Source, source));
    }

    public void Tick(float deltaTime)
    {
        float maxSpeedTarget = 1f;
        float maxSpeedFade = 0f;
        bool hasMaxSpeed = false;
        float lateralTarget = 1f;
        float lateralFade = 0f;
        bool hasLateral = false;
        bool isBoostLocked = false;
        EViewFlip viewFlip = EViewFlip.None;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            entry.Remaining -= deltaTime;

            // かけた側が壊されたら（Removeが呼ばれないまま）、ここで外す。
            if (entry.Remaining <= 0f || IsDestroyed(entry.Source))
            {
                _entries.RemoveAt(i);
                continue;
            }

            switch (entry.Kind)
            {
                case EKind.MaxSpeed:
                    if (!hasMaxSpeed || entry.Value <= maxSpeedTarget)
                    {
                        maxSpeedTarget = entry.Value;
                        maxSpeedFade = entry.FadeDuration;
                        hasMaxSpeed = true;
                    }

                    break;

                case EKind.Lateral:
                    if (!hasLateral || entry.Value <= lateralTarget)
                    {
                        lateralTarget = entry.Value;
                        lateralFade = entry.FadeDuration;
                        hasLateral = true;
                    }

                    break;

                case EKind.BoostLock:
                    isBoostLocked = true;
                    break;

                case EKind.ViewFlip:
                    viewFlip |= (EViewFlip)(int)entry.Value;
                    break;
            }
        }

        _maxSpeed.Tick(maxSpeedTarget, maxSpeedFade, hasMaxSpeed, deltaTime);
        _lateral.Tick(lateralTarget, lateralFade, hasLateral, deltaTime);
        IsBoostLocked = isBoostLocked;
        ViewFlip = viewFlip;
    }

    // スタートグリッドへの配置・ゴールのとき。すべての効果を外し、倍率もすぐ1に戻す。
    public void Reset()
    {
        _entries.Clear();
        _maxSpeed.Reset();
        _lateral.Reset();
        IsBoostLocked = false;
        ViewFlip = EViewFlip.None;
    }

    private void Set(object source, EKind kind, float value, float duration, float fadeDuration)
    {
        if (source == null || duration <= 0f)
        {
            return;
        }

        Entry entry = _entries.Find(e => e.Kind == kind && Equals(e.Source, source));
        if (entry == null)
        {
            entry = new Entry { Source = source, Kind = kind };
            _entries.Add(entry);
        }

        entry.Value = value;
        entry.Remaining = duration;
        entry.FadeDuration = Mathf.Max(0f, fadeDuration);
    }

    // UnityのObjectは、Destroyされると == null になる（参照そのものはnullにならない）。
    private static bool IsDestroyed(object source)
    {
        return source is Object unityObject && unityObject == null;
    }
}
