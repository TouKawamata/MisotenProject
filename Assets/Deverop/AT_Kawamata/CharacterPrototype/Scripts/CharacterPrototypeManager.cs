using UnityEngine;
using UnityEngine.InputSystem;

// キャラ性能のプロトタイプ用のPlayerの窓口。処理の中身はPlayerManagerと共通で、3つのタイプの切り替えだけを足す。
// ・シールド中に減速しない：ShieldProfileを差し替えて、ShieldControllerへ渡す
// ・ブーストの当たり判定が広い：FrontShield（当たり判定）を差し替える
// ・妨害弾：Qキーで、自分より順位が高くて一番近いレーサーへ追尾する弾を撃つ
// タイプが変わるときの情報の書き換え・参照先の切り替えは、すべてChangeTypeで行う。
// Qキーはトラッキング入力に割り当てがないので、IRaceInputには足さず、KeyboardRaceInputと同じくKeyboard.currentを直接読む。
public class CharacterPrototypeManager : PlayerManager
{
    [Header("タイプ")]
    [Tooltip("レース開始時のタイプ。Play中の切り替えは、下のボタンで行う")]
    [SerializeField] private ECharacterPrototypeType _initialType = ECharacterPrototypeType.NoShieldSlowdown;

    [Header("シールド")]
    [Tooltip("通常のShieldProfile")]
    [SerializeField] private ShieldProfile _normalShieldProfile;

    [Tooltip("シールド中に減速しないShieldProfile")]
    [SerializeField] private ShieldProfile _noSlowdownShieldProfile;

    [Header("FrontShield")]
    [Tooltip("通常のFrontShield（ShieldShellHitboxとBoostHitDetectorを持つ）")]
    [SerializeField] private ShieldShellHitbox _normalFrontShield;

    [Tooltip("当たり判定が広いFrontShield（ShieldShellHitboxとBoostHitDetectorを持つ）")]
    [SerializeField] private ShieldShellHitbox _wideFrontShield;

    [Header("妨害弾")]
    [SerializeField] private JammerProfile _jammerProfile;

    private readonly JammerLauncher _jammer = new JammerLauncher();

    private bool _isTypeReady;

    public ECharacterPrototypeType CurrentType { get; private set; }

    public int JammerAmmo => _jammer.Ammo;

    public int JammerMaxAmmo => _jammer.MaxAmmo;

    public float JammerReloadRemainingTime => _jammer.ReloadRemainingTime;

    // タイプを切り替える。いつ呼んでも、その場で反映する。
    public void ChangeType(ECharacterPrototypeType type)
    {
        if (!_isTypeReady)
        {
            return;
        }

        CurrentType = type;

        SetShieldProfile(type == ECharacterPrototypeType.NoShieldSlowdown ? _noSlowdownShieldProfile : _normalShieldProfile);

        ShieldShellHitbox frontShield = type == ECharacterPrototypeType.WideBoostHit ? _wideFrontShield : _normalFrontShield;
        SetFrontShield(frontShield, frontShield.GetComponent<BoostHitDetector>());

        _jammer.Reset(_jammerProfile);

        Log($"タイプを切り替えた（{type}）");
    }

    protected override void InitializeInput()
    {
        base.InitializeInput();

        _isTypeReady = HasTypeSettings();
        if (!_isTypeReady)
        {
            Debug.LogError($"{GetType().Name}: タイプの切り替えに必要な設定が足りないため、切り替えできません（ShieldProfile／FrontShield／Jammer Profile、FrontShieldのBoostHitDetector）", this);
            return;
        }

        ChangeType(_initialType);
    }

    // GetInputはゴール前のTickごとに1回呼ばれるので、妨害弾の更新もここで行う。
    protected override IRaceInput GetInput(float deltaTime, IReadOnlyRacerData data)
    {
        if (_isTypeReady && CurrentType == ECharacterPrototypeType.Jammer)
        {
            UpdateJammer(deltaTime, data);
        }

        return base.GetInput(deltaTime, data);
    }

    private void UpdateJammer(float deltaTime, IReadOnlyRacerData data)
    {
        _jammer.Tick(deltaTime);

        Keyboard keyboard = Keyboard.current;
        if (IsStunned || keyboard == null || !keyboard.qKey.wasPressedThisFrame)
        {
            return;
        }

        Vector3 position = transform.position + transform.forward * _jammerProfile.SpawnForwardOffset;
        if (_jammer.TryFire(data, Racers, CourseGuide, position, transform.forward, out IReadOnlyRacerData target))
        {
            Log($"妨害弾を発射 標的 {target.DisplayName} 残弾 {_jammer.Ammo}/{_jammer.MaxAmmo}");
        }
        else
        {
            Log($"妨害弾を発射できない 残弾 {_jammer.Ammo}/{_jammer.MaxAmmo}（発射間隔の途中か、前に標的がいない）");
        }
    }

    private bool HasTypeSettings()
    {
        return _normalShieldProfile != null
            && _noSlowdownShieldProfile != null
            && _normalFrontShield != null
            && _wideFrontShield != null
            && _normalFrontShield.TryGetComponent(out BoostHitDetector _)
            && _wideFrontShield.TryGetComponent(out BoostHitDetector _)
            && _jammerProfile != null;
    }
}
