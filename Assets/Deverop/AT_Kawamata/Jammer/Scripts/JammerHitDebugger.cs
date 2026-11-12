using UnityEngine;
using UnityEngine.InputSystem;

// 【デバッグ用】キーを押すと、このレーサー（Player）が妨害弾を受けたことにする。弾を当てなくても、効果を自分で確かめられる。
// 弾が当たったときと同じ入口（IJammerHitReceiver.ReceiveJammerHit）を呼ぶので、シールドで防げるか・防いだときの加速もそのまま確かめられる。
// 当てる種類は、Type Overrideが設定されていればそれ、無ければJammerLauncherで今選んでいる種類。
// 画面の左上に、最後の結果と、今かかっている効果を出す。
// Playerに付ける（Racer Manager・Jammer Launcherは未設定なら、同じGameObjectから探す）。入力はIRaceInputを通さず、Keyboard.currentを直接読む。
public class JammerHitDebugger : MonoBehaviour
{
    [Tooltip("被弾したことにするキー")]
    [SerializeField] private Key _hitKey = Key.J;

    [Tooltip("当てるレーサー。未設定なら、このGameObjectのRacerManagerBase")]
    [SerializeField] private RacerManagerBase _racerManager;

    [Tooltip("当てる種類を決めるJammerLauncher（今選んでいる種類を使う）。未設定なら、このGameObjectから探す")]
    [SerializeField] private JammerLauncher _jammerLauncher;

    [Tooltip("設定すると、JammerLauncherの選択に関係なく、この種類を当てる")]
    [SerializeField] private JammerTypeProfile _typeOverride;

    [Tooltip("画面の左上に、結果と今かかっている効果を出す")]
    [SerializeField] private bool _showStatus = true;

    private string _lastResult = "まだ当てていない";
    private GUIStyle _style;

    private JammerTypeProfile CurrentType => _typeOverride != null ? _typeOverride : _jammerLauncher != null ? _jammerLauncher.CurrentType : null;

    private void Awake()
    {
        if (_racerManager == null)
        {
            _racerManager = GetComponent<RacerManagerBase>();
        }

        if (_jammerLauncher == null)
        {
            _jammerLauncher = GetComponent<JammerLauncher>();
        }

        if (_racerManager == null)
        {
            Debug.LogError("JammerHitDebugger: Racer Manager が見つかりません", this);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (_racerManager == null || keyboard == null || _hitKey == Key.None || !keyboard[_hitKey].wasPressedThisFrame)
        {
            return;
        }

        JammerTypeProfile type = CurrentType;
        if (type == null)
        {
            _lastResult = "種類が未設定（Type Override か Jammer Launcher の種類を設定）";
            Debug.LogWarning($"JammerHitDebugger: {_lastResult}", this);
            return;
        }

        if (type.TargetsRing)
        {
            _lastResult = $"{type.DisplayName}：リング用の弾なので、レーサーには当てられない";
            Debug.Log($"JammerHitDebugger: {_lastResult}", this);
            return;
        }

        EJammerHitResult result = ((IJammerHitReceiver)_racerManager).ReceiveJammerHit(type);
        _lastResult = $"{type.DisplayName}：{GetResultLabel(result)}";
        Debug.Log($"JammerHitDebugger: 被弾したことにした {_lastResult}", this);
    }

    private void OnGUI()
    {
        if (!_showStatus || _racerManager == null)
        {
            return;
        }

        _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, wordWrap = false };

        IRacerEffects effects = _racerManager.Effects;
        JammerTypeProfile type = CurrentType;
        string text =
            $"被弾テスト [{_hitKey}]：{(type != null ? type.DisplayName : "種類が未設定")}\n" +
            $"結果：{_lastResult}\n" +
            $"最高速度 ×{effects.MaxSpeedMultiplier:F2}　左右移動 ×{effects.LateralMultiplier:F2}\n" +
            $"ブースト：{(effects.IsBoostLocked ? "不可" : "可")}　視界反転：{GetFlipLabel(effects.ViewFlip)}\n" +
            $"シールド：{(_racerManager.IsShielding ? "張っている" : "なし")}";

        GUILayout.BeginArea(new Rect(10f, 10f, 520f, 140f));
        GUILayout.Box(text, _style);
        GUILayout.EndArea();
    }

    private static string GetResultLabel(EJammerHitResult result)
    {
        switch (result)
        {
            case EJammerHitResult.Blocked:
                return "シールドで防いだ";

            case EJammerHitResult.Hit:
                return "効果を受けた";

            default:
                return "効果なし（レース前・ゴール後・無敵中）";
        }
    }

    private static string GetFlipLabel(EViewFlip flip)
    {
        switch (flip)
        {
            case EViewFlip.None:
                return "なし";

            case EViewFlip.Horizontal:
                return "左右";

            case EViewFlip.Vertical:
                return "上下";

            default:
                return "左右＋上下";
        }
    }
}
