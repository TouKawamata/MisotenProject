using UnityEngine;

// CPUの入力（判断）。CPUManagerの「Input Source」に入れて使う（ICpuRaceInput）。
// 毎Tick、CPUManagerから呼ばれるUpdateInputで、次の順に判断して、そのTickのIRaceInputの値を決める。
//   1. CpuPerception：周りの状況・前方のギミックを、コースの向きを基準にした値にまとめる（CpuSituation）
//   2. CpuTargetSelector：狙う相手（PlayerもCPUも同じ扱い）
//      CpuShieldBrain：シールドを張りたいか（毎Tick。反応時間を秒で数えるため。後ろのブースト・妨害弾・爆弾）
//      CpuJammerBrain：妨害弾を撃つか・どの種類か（考え直すTickだけ）
//      CpuBoostBrain／CpuSteeringBrainの目標の位置：考え直すTick（難易度のThinkInterval）だけ
//      CpuSteeringBrainのMove：毎Tick
//   3. 視界反転：効果がかかると、移動の入力が反転される（RacerManagerBase）。難易度のViewFlipAdaptTimeが経ったら、反転して出し直す（慣れる）
//   4. CpuButtonLatch：「押し続けたいか」からPressed/Heldを作る
// ブーストとシールドは同時に使えないため、シールドを優先する（後ろからのブーストを防ぐ方が、損を避けられるため）。
// 難易度は、CpuDifficultyControllerがシーンにあり、Follow Global DifficultyがONならそちらを、それ以外は自分の難易度を使う。
// （2026-11-07のショートカット廃止・移動のVector2化に合わせて、上下左右の操作・妨害弾・ギミックに対応した。CpuShortcutBrainは削除）
public class CpuRaceInput : MonoBehaviour, ICpuRaceInput
{
    // コース上の距離がこれ以上（m）戻ったら、置き直し（レースのやり直し）とみなして、判断の状態を消す。
    private const float RestartDistanceThreshold = 20f;

    // 視界反転に慣れるまでの秒数に掛けるばらつき（0.8～1.2倍）。
    private const float ViewFlipAdaptJitter = 0.2f;

    [Header("難易度・個性")]
    [Tooltip("このCPUの難易度。Follow Global DifficultyがONで、シーンにCpuDifficultyController（難易度の設定あり）があれば、そちらを使う")]
    [SerializeField] private CpuDifficultyProfile _difficulty;

    [Tooltip("ONなら、CpuDifficultyControllerの難易度（全CPU一括）に従う。OFFなら、常にこのCPUの難易度を使う")]
    [SerializeField] private bool _followGlobalDifficulty = true;

    [SerializeField] private CpuPersonalityProfile _personality;

    [Tooltip("乱数のシード。0なら毎回ちがう動きになる")]
    [SerializeField] private int _randomSeed;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs;

    [Tooltip("選択中に、目標の位置（黄）・狙う相手（赤）・脅威（水色）・前方のギミック（緑＝くぐる、橙＝避ける）を表示する")]
    [SerializeField] private bool _drawGizmos = true;

    [Tooltip("頭上に、個性・難易度・今していることを表示する（Gameビュー・Sceneビュー）")]
    [SerializeField] private bool _showLabel = true;

    [Tooltip("頭上の表示の高さ（m）")]
    [SerializeField] private float _labelHeight = 2.5f;

    private readonly CpuSituation _situation = new CpuSituation();
    private readonly CpuPerception _perception = new CpuPerception();
    private readonly CpuTargetSelector _targetSelector = new CpuTargetSelector();
    private readonly CpuShieldBrain _shieldBrain = new CpuShieldBrain();
    private readonly CpuBoostBrain _boostBrain = new CpuBoostBrain();
    private readonly CpuSteeringBrain _steeringBrain = new CpuSteeringBrain();
    private readonly CpuJammerBrain _jammerBrain = new CpuJammerBrain();
    private readonly CpuButtonLatch _boostLatch = new CpuButtonLatch();
    private readonly CpuButtonLatch _shieldLatch = new CpuButtonLatch();

    private System.Random _random;
    private float _thinkTimer;
    private float _elapsedTime;
    private float _lastDistanceOnCourse;
    private bool _hasSituation;
    private bool _hasWarnedMissingProfile;
    private bool _hasWarnedMissingCourse;
    private int _loggedTargetId = -1;
    private ECpuThreatKind _loggedThreatKind = ECpuThreatKind.None;

    // 視界反転：実際にかかっている向きと、CPUが慣れて打ち消している向き。
    private EViewFlip _currentViewFlip = EViewFlip.None;
    private EViewFlip _adaptedViewFlip = EViewFlip.None;
    private float _viewFlipAdaptTimer;

    // IRaceInputの移動入力（x＝右、y＝上、大きさ0～1）。
    public Vector2 Move { get; private set; }

    public bool BoostPressed => _boostLatch.Pressed;

    public bool BoostHeld => _boostLatch.Held;

    public bool ShieldPressed => _shieldLatch.Pressed;

    public bool ShieldHeld => _shieldLatch.Held;

    public bool JammerPressed { get; private set; }

    // 今使っている難易度（一括の設定を反映したもの）。
    public CpuDifficultyProfile CurrentDifficulty
    {
        get
        {
            CpuDifficultyController controller = CpuDifficultyController.Current;
            if (_followGlobalDifficulty && controller != null && controller.Difficulty != null)
            {
                return controller.Difficulty;
            }

            return _difficulty;
        }
    }

    public CpuPersonalityProfile Personality => _personality;

    // 視界反転に戸惑っている（まだ慣れていない）間か。
    public bool IsConfusedByViewFlip => _currentViewFlip != _adaptedViewFlip;

    // このCPUだけの難易度を変える（Follow Global DifficultyがONで一括の設定があれば、そちらが優先される）。
    public void SetDifficulty(CpuDifficultyProfile difficulty)
    {
        _difficulty = difficulty;
    }

    public void SetFollowGlobalDifficulty(bool follow)
    {
        _followGlobalDifficulty = follow;
    }

    public void SetPersonality(CpuPersonalityProfile personality)
    {
        _personality = personality;
    }

    // 判断の状態をすべて消す（置き直しは自動で検出するが、明示的にやり直したいとき用）。
    public void ResetState()
    {
        _perception.Reset();
        _targetSelector.Reset();
        _shieldBrain.Reset();
        _boostBrain.Reset();
        _steeringBrain.Reset();
        _jammerBrain.Reset();
        _boostLatch.Reset();
        _shieldLatch.Reset();
        _thinkTimer = 0f;
        _loggedTargetId = -1;
        _loggedThreatKind = ECpuThreatKind.None;
        _currentViewFlip = EViewFlip.None;
        _adaptedViewFlip = EViewFlip.None;
        _viewFlipAdaptTimer = 0f;
        Move = Vector2.zero;
        JammerPressed = false;
    }

    public void UpdateInput(CPUManager owner, float deltaTime, IReadOnlyRacerData data)
    {
        CpuDifficultyProfile difficulty = CurrentDifficulty;
        if (difficulty == null || _personality == null)
        {
            WarnMissingProfileOnce();
            ClearOutputs();
            return;
        }

        _random ??= _randomSeed != 0 ? new System.Random(_randomSeed) : new System.Random();
        _elapsedTime += deltaTime;

        if (data != null && data.DistanceOnCourse < _lastDistanceOnCourse - RestartDistanceThreshold)
        {
            ResetState();
        }

        _lastDistanceOnCourse = data != null ? data.DistanceOnCourse : 0f;

        _hasSituation = _perception.Update(_situation, owner, deltaTime, data, difficulty);
        if (!_hasSituation)
        {
            WarnMissingCourseOnce(owner);
            ClearOutputs();
            return;
        }

        _thinkTimer -= deltaTime;
        bool think = _thinkTimer <= 0f;
        if (think)
        {
            _thinkTimer = difficulty.ThinkInterval;
        }

        _targetSelector.Update(_situation, _personality, think);
        _shieldBrain.Update(_situation, difficulty, _personality, _random);
        _jammerBrain.Update(_situation, difficulty, _personality, _random, think);
        if (think)
        {
            _boostBrain.Think(_situation, difficulty, _personality, _targetSelector, EvaluateCatchUp(difficulty));
            _steeringBrain.Think(_situation, difficulty, _personality, _targetSelector, _shieldBrain, _random);
        }

        Vector2 move = _steeringBrain.Evaluate(_situation, difficulty, _personality, _random, _elapsedTime);
        Move = ApplyViewFlipAdaptation(owner, move, difficulty, deltaTime);

        // スタン中は入力が無視されるので、押していない扱いにする（スタン明けに押し直せるように）。
        bool canControl = !owner.IsStunned;
        bool wantsShield = canControl && _shieldBrain.WantsShield;
        bool wantsBoost = canControl && _boostBrain.WantsBoost && !wantsShield && !_shieldBrain.WantsToStopBoost;
        _shieldLatch.Update(wantsShield, owner.IsShielding);
        _boostLatch.Update(wantsBoost, owner.IsBoosting);
        JammerPressed = canControl && _jammerBrain.FirePressed;

        LogChanges();
    }

    // 視界反転がかかると、RacerManagerBaseが移動の入力を反転する。慣れるまで（難易度のViewFlipAdaptTime）はそのまま出して逆に動き、
    // 慣れたら、同じ軸を先に反転して出す（反転されて元の向きになる）。効果が切れたときも、同じだけ戸惑う。
    private Vector2 ApplyViewFlipAdaptation(CPUManager owner, Vector2 move, CpuDifficultyProfile difficulty, float deltaTime)
    {
        EViewFlip flip = owner.Effects != null ? owner.Effects.ViewFlip : EViewFlip.None;
        if (flip != _currentViewFlip)
        {
            _currentViewFlip = flip;
            float jitter = 1f + ((float)_random.NextDouble() * 2f - 1f) * ViewFlipAdaptJitter;
            _viewFlipAdaptTimer = difficulty.ViewFlipAdaptTime * jitter;
        }

        if (_adaptedViewFlip != _currentViewFlip)
        {
            _viewFlipAdaptTimer -= deltaTime;
            if (_viewFlipAdaptTimer <= 0f)
            {
                _adaptedViewFlip = _currentViewFlip;
            }
        }

        if ((_adaptedViewFlip & EViewFlip.Horizontal) != 0)
        {
            move.x = -move.x;
        }

        if ((_adaptedViewFlip & EViewFlip.Vertical) != 0)
        {
            move.y = -move.y;
        }

        return move;
    }

    // ＋＝Playerより後ろ（積極的に）、－＝Playerより前（控えめに）。-1～1に難易度の強さを掛けた値。
    private float EvaluateCatchUp(CpuDifficultyProfile difficulty)
    {
        if (!_situation.HasPlayer || difficulty.CatchUpStrength <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp(_situation.GapToPlayer / difficulty.CatchUpDistance, -1f, 1f) * difficulty.CatchUpStrength;
    }

    private void ClearOutputs()
    {
        Move = Vector2.zero;
        JammerPressed = false;
        _boostLatch.Reset();
        _shieldLatch.Reset();
    }

    private void WarnMissingProfileOnce()
    {
        if (_hasWarnedMissingProfile)
        {
            return;
        }

        _hasWarnedMissingProfile = true;
        Debug.LogWarning($"CpuRaceInput: {name} の難易度（Difficulty）または個性（Personality）が未設定のため、入力なしで走ります", this);
    }

    // コースが取れないと、CPUの判断だけでなく飛行の計算も止まる（PlayerFlightControllerはコースが無いと動かさない）。
    // Course SplineはシーンのオブジェクトなのでPrefabには保存できず、シーンに置いたCPUごとに設定が要る。
    private void WarnMissingCourseOnce(CPUManager owner)
    {
        if (_hasWarnedMissingCourse || owner.CourseGuide != null)
        {
            return;
        }

        _hasWarnedMissingCourse = true;
        Debug.LogWarning($"CpuRaceInput: {name} のコース（PlayerFlightControllerのCourse Spline）が未設定のため、動けません。シーン上のCPUに、シーンのCourseSplineを設定してください", this);
    }

    private void LogChanges()
    {
        if (!_showDebugLogs)
        {
            return;
        }

        int targetId = _targetSelector.HasTarget ? _targetSelector.Target.RacerId : -1;
        if (targetId != _loggedTargetId)
        {
            _loggedTargetId = targetId;
            Debug.Log(targetId >= 0
                ? $"CpuRaceInput: {name} が {_targetSelector.Target.Data.DisplayName} を狙う"
                : $"CpuRaceInput: {name} が狙うのをやめた", this);
        }

        if (_shieldBrain.ThreatKind != _loggedThreatKind)
        {
            _loggedThreatKind = _shieldBrain.ThreatKind;
            if (_loggedThreatKind != ECpuThreatKind.None)
            {
                Debug.Log($"CpuRaceInput: {name} が脅威に気づいた（{GetThreatName(_loggedThreatKind)}）", this);
            }
        }

        if (_boostLatch.Pressed)
        {
            Debug.Log($"CpuRaceInput: {name} がブーストを押した（{(_boostBrain.IsAttacking ? "攻撃" : "加速")}）", this);
        }

        if (_shieldLatch.Pressed)
        {
            Debug.Log($"CpuRaceInput: {name} がシールドを押した（脅威：{GetThreatName(_shieldBrain.ThreatKind)}）", this);
        }

        if (JammerPressed)
        {
            Debug.Log($"CpuRaceInput: {name} が妨害弾を撃った（{_jammerBrain.LastTypeName}）", this);
        }
    }

    private static string GetThreatName(ECpuThreatKind kind)
    {
        switch (kind)
        {
            case ECpuThreatKind.BoostRacer:
                return "後ろからのブースト";
            case ECpuThreatKind.Jammer:
                return "妨害弾";
            case ECpuThreatKind.Bomb:
                return "爆弾";
            default:
                return "なし";
        }
    }

    private void OnGUI()
    {
        if (_showLabel && Application.isPlaying)
        {
            CpuLabelDrawer.DrawGameView(GetLabelPosition(), BuildLabelText(), GetLabelColor());
        }
    }

    // 頭上の表示は選択していなくても出す（どのCPUがどの個性か見分けるため）。
    private void OnDrawGizmos()
    {
        if (_showLabel)
        {
            CpuLabelDrawer.DrawSceneView(GetLabelPosition(), BuildLabelText(), GetLabelColor());
        }
    }

    private Vector3 GetLabelPosition()
    {
        return transform.position + transform.up * _labelHeight;
    }

    private Color GetLabelColor()
    {
        return _personality != null ? _personality.LabelColor : Color.white;
    }

    // 1行目：個性 / 難易度、2行目：今していること（再生中だけ）。
    private string BuildLabelText()
    {
        CpuDifficultyProfile difficulty = CurrentDifficulty;
        string personalityName = _personality != null ? _personality.DisplayName : "個性なし";
        string difficultyName = difficulty != null ? difficulty.DisplayName : "難易度なし";
        string header = $"{personalityName} / {difficultyName}";

        string status = Application.isPlaying && _hasSituation ? BuildStatusText() : "";
        return string.IsNullOrEmpty(status) ? header : $"{header}\n{status}";
    }

    private string BuildStatusText()
    {
        CPUManager owner = _situation.Owner;
        if (owner == null)
        {
            return "";
        }

        if (owner.IsStunned)
        {
            return "スタン";
        }

        string confused = IsConfusedByViewFlip ? "（反転で混乱）" : "";
        if (owner.IsShielding)
        {
            return $"シールド：{GetThreatName(_shieldBrain.ThreatKind)}{confused}";
        }

        string target = _targetSelector.HasTarget ? $"→{_targetSelector.Target.Data.DisplayName}" : "";
        if (owner.IsBoosting)
        {
            return (_boostBrain.IsAttacking ? $"攻撃ブースト{target}" : "加速ブースト") + confused;
        }

        if (JammerPressed)
        {
            return $"妨害弾：{_jammerBrain.LastTypeName}{confused}";
        }

        if (_steeringBrain.IsSeekingRing)
        {
            return $"リングへ{confused}";
        }

        if (_steeringBrain.IsAvoidingGimmick)
        {
            return $"ギミック回避{confused}";
        }

        return string.IsNullOrEmpty(target) ? confused : $"狙い{target}{confused}";
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos || !_hasSituation || !Application.isPlaying)
        {
            return;
        }

        Vector3 position = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_situation.ToWorld(_steeringBrain.TargetOffset), 0.5f);

        if (_targetSelector.HasTarget && _targetSelector.Target.Data.Transform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, _targetSelector.Target.Data.Transform.position);
        }

        if (_shieldBrain.ThreatKind != ECpuThreatKind.None)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(position, _shieldBrain.ThreatPosition);
        }

        foreach (CpuGimmickView gimmick in _situation.Gimmicks)
        {
            Gizmos.color = gimmick.Kind == ECpuGimmickKind.SpeedRing ? Color.green : new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(position, gimmick.Gimmick.transform.position);
        }
    }
}
