using System.Collections.Generic;
using UnityEngine;

// 妨害弾を撃つか・どの種類を撃つかを決める（レーサーのプレハブにJammerLauncherがあるときだけ）。考え直すTickだけ判断する。
// 標的は発射台（JammerLauncher）が決める（自分より順位が高いレーサーのうち、コース上の距離が一番近い相手）。CPUも同じ相手を見て、撃つ好機かを判断する。
//   撃つ条件：撃てる（残弾・発射間隔）、前回撃ってから個性の間隔が経った、標的が適度な距離にいる、標的がシールド中・スタン中でない
//   種類：状況に合う種類に点数を付けて選ぶ。難易度のJammerSmartnessの確率で一番の種類を、それ以外はでたらめに選ぶ
//     ・ブースト禁止：標的がブースト中なら高い
//     ・視界反転・リングの切り替え：個性のひねくれ度が高いほど高い
//     ・リングの切り替え：自分より前・標的より先に加速リングがあるときだけ（リングの標的が発射台に無ければ撃てないので、そのときはしばらく選ばない）
// 撃つときは、種類を切り替えて（SelectType）、そのTickだけJammerPressedをtrueにする。
public class CpuJammerBrain
{
    // 撃つ間隔（秒）。撃ちたがり0～1で、長い～短い。実際の間隔は、これに0.7～1.3倍のばらつきを掛ける。
    private const float FireIntervalAtLowEagerness = 12f;
    private const float FireIntervalAtHighEagerness = 2.5f;

    // 撃つ標的との距離（m）。近すぎると撃ってすぐ当たって効果を見届けにくく、遠すぎると届くまでに弾が消えやすい。
    private const float MinFireGap = 6f;
    private const float FireGapAtLowEagerness = 60f;
    private const float FireGapAtHighEagerness = 150f;

    // 種類ごとの点数。
    private const float SlowScore = 1f;
    private const float LateralSlowScore = 0.8f;
    private const float BoostLockScore = 0.5f;
    private const float BoostLockOnBoostingBonus = 1.5f;
    private const float ViewFlipScore = 0.3f;
    private const float RingSwapScore = 0.2f;
    private const float TrickBonus = 1.5f;
    private const float UnknownTypeScore = 0.5f;

    // リングの切り替えを撃つ条件：標的より先、この距離（m）以内の加速リング。
    private const float RingSwapRangeBeyondTarget = 80f;

    // リングの切り替えを撃てなかったとき（発射台にリングの標的が無い）、選ばない秒数。
    private const float RingSwapRetryDelay = 10f;

    private readonly List<int> _validTypeIndices = new List<int>();
    private readonly List<float> _typeScores = new List<float>();

    private float _cooldown;
    private float _ringSwapBlockedTimer;
    private bool _isCheckingFire;
    private bool _wasRingSwap;
    private int _ammoBeforeFire;

    // このTickで撃つか（JammerPressed）。
    public bool FirePressed { get; private set; }

    // 最後に撃った（撃とうとした）種類の表示名（頭上の表示・ログ用）。
    public string LastTypeName { get; private set; } = "";

    // 最後に撃った（撃とうとした）ときの標的（頭上の表示・Gizmos用）。まだ撃っていなければnull。
    public IReadOnlyRacerData LastTarget { get; private set; }

    // 次に撃てるようになるまでの、個性の間隔の残り（秒）。
    public float CooldownRemaining => _cooldown;

    // 発射台があるか（無ければ妨害弾は撃たない）。
    public bool HasLauncher { get; private set; }

    // 毎Tick呼ぶ。think：考え直すTickか（撃つかを決めるのはこのときだけ）。
    public void Update(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random, bool think)
    {
        FirePressed = false;
        float deltaTime = situation.DeltaTime;
        _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        _ringSwapBlockedTimer = Mathf.Max(0f, _ringSwapBlockedTimer - deltaTime);

        JammerLauncher launcher = situation.Launcher;
        HasLauncher = launcher != null;
        if (launcher == null)
        {
            return;
        }

        CheckLastFire(launcher);

        if (!think || _cooldown > 0f || !situation.Owner.CanFireJammer)
        {
            return;
        }

        if (!TryFindTarget(situation, out CpuRacerView target))
        {
            return;
        }

        float maxGap = Mathf.Lerp(FireGapAtLowEagerness, FireGapAtHighEagerness, personality.JammerEagerness);
        if (target.ForwardGap < MinFireGap || target.ForwardGap > maxGap || target.IsShielding || target.IsStunned)
        {
            return;
        }

        int typeIndex = ChooseType(situation, difficulty, personality, random, launcher, target);
        if (typeIndex < 0)
        {
            return;
        }

        if (typeIndex != launcher.CurrentTypeIndex)
        {
            launcher.SelectType(typeIndex);
        }

        FirePressed = true;
        LastTypeName = launcher.CurrentType != null ? launcher.CurrentType.DisplayName : "";
        LastTarget = target.Data;
        _wasRingSwap = launcher.CurrentType is RingSwapJammerType;
        _ammoBeforeFire = launcher.Ammo;
        _isCheckingFire = true;

        float interval = Mathf.Lerp(FireIntervalAtLowEagerness, FireIntervalAtHighEagerness, personality.JammerEagerness);
        _cooldown = interval * Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble());
    }

    public void Reset()
    {
        _cooldown = 0f;
        _ringSwapBlockedTimer = 0f;
        _isCheckingFire = false;
        FirePressed = false;
        LastTypeName = "";
        LastTarget = null;
    }

    // 撃った次のTickで、弾が減ったかを見る。リングの切り替えが撃てなかったら（リングの標的が無い）、しばらく選ばない。
    private void CheckLastFire(JammerLauncher launcher)
    {
        if (!_isCheckingFire)
        {
            return;
        }

        _isCheckingFire = false;
        if (_wasRingSwap && launcher.Ammo >= _ammoBeforeFire)
        {
            _ringSwapBlockedTimer = RingSwapRetryDelay;
            _cooldown = 0f;
        }
    }

    // 発射台と同じ選び方：自分より順位が高いレーサーのうち、コース上の距離が一番近い相手（ゴール済みは、Othersに入っていない）。
    private static bool TryFindTarget(CpuSituation situation, out CpuRacerView target)
    {
        target = default;
        bool found = false;
        float bestGap = float.MaxValue;
        int selfRank = situation.Self.CurrentRank;
        foreach (CpuRacerView other in situation.Others)
        {
            if (other.Data.CurrentRank >= selfRank)
            {
                continue;
            }

            float gap = Mathf.Abs(other.ForwardGap);
            if (gap < bestGap)
            {
                bestGap = gap;
                target = other;
                found = true;
            }
        }

        return found;
    }

    // 撃つ種類の番号（launcher.Types）。撃てる種類が無ければ-1。
    private int ChooseType(CpuSituation situation, CpuDifficultyProfile difficulty, CpuPersonalityProfile personality, System.Random random,
        JammerLauncher launcher, CpuRacerView target)
    {
        _validTypeIndices.Clear();
        _typeScores.Clear();
        IReadOnlyList<JammerTypeProfile> types = launcher.Types;
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] == null)
            {
                continue;
            }

            float score = ScoreType(situation, personality, types[i], target);
            if (score > 0f)
            {
                _validTypeIndices.Add(i);
                _typeScores.Add(score);
            }
        }

        if (_validTypeIndices.Count == 0)
        {
            return -1;
        }

        if (random.NextDouble() >= difficulty.JammerSmartness)
        {
            return _validTypeIndices[random.Next(_validTypeIndices.Count)];
        }

        int best = 0;
        for (int i = 1; i < _typeScores.Count; i++)
        {
            if (_typeScores[i] > _typeScores[best])
            {
                best = i;
            }
        }

        return _validTypeIndices[best];
    }

    // 0以下なら選ばない。
    private float ScoreType(CpuSituation situation, CpuPersonalityProfile personality, JammerTypeProfile type, CpuRacerView target)
    {
        float trick = personality.Trickiness * TrickBonus;
        switch (type)
        {
            case SlowJammerType _:
                return SlowScore;
            case LateralSlowJammerType _:
                return LateralSlowScore;
            case BoostLockJammerType _:
                return BoostLockScore + (target.IsBoosting ? BoostLockOnBoostingBonus : 0f);
            case ViewFlipJammerType _:
                return ViewFlipScore + trick;
            case RingSwapJammerType _:
                return _ringSwapBlockedTimer <= 0f && HasRingBeyondTarget(situation, target) ? RingSwapScore + trick : 0f;
        }

        return type.TargetsRing ? 0f : UnknownTypeScore;
    }

    // 標的より先（標的が先にくぐる）にある加速リング。発射台は自分より前の一番近いリングを狙うので、自分と標的の間にリングがあるときは撃たない。
    private static bool HasRingBeyondTarget(CpuSituation situation, CpuRacerView target)
    {
        foreach (CpuGimmickView gimmick in situation.Gimmicks)
        {
            if (gimmick.Kind != ECpuGimmickKind.SpeedRing && gimmick.Kind != ECpuGimmickKind.SlowRing)
            {
                continue;
            }

            // 近い順なので、最初のリングだけを見る。
            return gimmick.Kind == ECpuGimmickKind.SpeedRing
                && gimmick.ForwardDistance > target.ForwardGap
                && gimmick.ForwardDistance <= target.ForwardGap + RingSwapRangeBeyondTarget;
        }

        return false;
    }
}
