using UnityEngine;

// CPUの入力（判断）。CPUManagerの「Input Source」に入れて使う（ICpuRaceInput）。
// 毎Tick、CPUManagerから呼ばれるUpdateInputで、次の順に判断して、そのTickのIRaceInputの値を決める。
//   1. CpuPerception：周りの状況をコースの向きを基準にした値にまとめる（CpuSituation）
//   2. CpuTargetSelector：狙う相手（PlayerもCPUも同じ扱い）
//      CpuShieldBrain：シールドを張りたいか（毎Tick。反応時間を秒で数えるため）
//      CpuBoostBrain／CpuSteeringBrainの目標：考え直すTick（難易度のThinkInterval）だけ
//      CpuSteeringBrainのHorizontal・CpuShortcutBrain：毎Tick
//   3. CpuButtonLatch：「押し続けたいか」からPressed/Heldを作る
// ブーストとシールドは同時に使えないため、シールドを優先する（後ろからのブーストを防ぐ方が、損を避けられるため）。
// 難易度は、CpuDifficultyControllerがシーンにあり、Follow Global DifficultyがONならそちらを、それ以外は自分の難易度を使う。
public class CpuRaceInput : MonoBehaviour, ICpuRaceInput
{
    // コース上の距離がこれ以上（m）戻ったら、置き直し（レースのやり直し）とみなして、判断の状態を消す。
    private const float RestartDistanceThreshold = 20f;

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

    [Tooltip("選択中に、目標の横位置（黄）・狙う相手（赤）・脅威（青）を表示する")]
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
    private readonly CpuShortcutBrain _shortcutBrain = new CpuShortcutBrain();
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

    public float Horizontal { get; private set; }

    public bool BoostPressed => _boostLatch.Pressed;

    public bool BoostHeld => _boostLatch.Held;

    public bool ShieldPressed => _shieldLatch.Pressed;

    public bool ShieldHeld => _shieldLatch.Held;

    public bool ShortcutPressed { get; private set; }

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
        _shortcutBrain.Reset();
        _boostLatch.Reset();
        _shieldLatch.Reset();
        _thinkTimer = 0f;
        _loggedTargetId = -1;
        Horizontal = 0f;
        ShortcutPressed = false;
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
        if (think)
        {
            _boostBrain.Think(_situation, difficulty, _personality, _targetSelector, EvaluateCatchUp(difficulty));
            _steeringBrain.Think(_situation, _personality, _targetSelector, _shieldBrain, _random);
        }

        float horizontal = _steeringBrain.Evaluate(_situation, difficulty, _personality, _random, _elapsedTime);
        _shortcutBrain.Update(_situation, difficulty, _personality, _random, horizontal);
        Horizontal = _shortcutBrain.OverridesHorizontal ? _shortcutBrain.HorizontalOverride : horizontal;
        ShortcutPressed = _shortcutBrain.ShortcutPressed;

        // スタン中は入力が無視されるので、押していない扱いにする（スタン明けに押し直せるように）。
        bool canControl = !owner.IsStunned;
        bool wantsShield = canControl && _shieldBrain.WantsShield;
        bool wantsBoost = canControl && _boostBrain.WantsBoost && !wantsShield;
        _shieldLatch.Update(wantsShield, owner.IsShielding);
        _boostLatch.Update(wantsBoost, owner.IsBoosting);

        LogChanges();
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
        Horizontal = 0f;
        ShortcutPressed = false;
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

        if (_boostLatch.Pressed)
        {
            Debug.Log($"CpuRaceInput: {name} がブーストを押した（{(_boostBrain.IsAttacking ? "攻撃" : "加速")}）", this);
        }

        if (_shieldLatch.Pressed)
        {
            Debug.Log($"CpuRaceInput: {name} がシールドを押した（脅威：{(_shieldBrain.HasThreat ? _shieldBrain.Threat.Data.DisplayName : "なし")}）", this);
        }

        if (ShortcutPressed || _shortcutBrain.OverridesHorizontal && _shortcutBrain.HorizontalOverride != 0f)
        {
            Debug.Log($"CpuRaceInput: {name} がショートカットの入力をした", this);
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

        if (owner.IsShielding)
        {
            return "シールド";
        }

        string target = _targetSelector.HasTarget ? $"→{_targetSelector.Target.Data.DisplayName}" : "";
        if (owner.IsBoosting)
        {
            return _boostBrain.IsAttacking ? $"攻撃ブースト{target}" : "加速ブースト";
        }

        return string.IsNullOrEmpty(target) ? "" : $"狙い{target}";
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos || !_hasSituation || !Application.isPlaying)
        {
            return;
        }

        Vector3 position = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_situation.CourseCenter + _situation.CourseRight * _steeringBrain.TargetLateral, 0.5f);

        if (_targetSelector.HasTarget && _targetSelector.Target.Data.Transform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, _targetSelector.Target.Data.Transform.position);
        }

        if (_shieldBrain.HasThreat && _shieldBrain.Threat.Data.Transform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(position, _shieldBrain.Threat.Data.Transform.position);
        }
    }
}
