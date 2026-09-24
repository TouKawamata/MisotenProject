using UnityEngine;

// Playerに関する機能の窓口。入力を1回だけ読み、Boost/Shield/Shortcutの入力をハンドリングし、
// 現在有効なFlightTuningProfile（通常／ショートカット吸着）を選んでPlayerFlightControllerへ渡す。
// レースの参加者（IRacer）として、Initialize/PlaceAt/TickはRaceManagerから呼び出される。
// ゴール後は入力を無視して直進させる（Tickは呼ばれ続ける）。
public class PlayerManager : MonoBehaviour, IShortcutZoneReceiver, IRacer
{
    [SerializeField] private PlayerFlightController _flightController;
    [SerializeField] private BoostController _boost;
    [SerializeField] private RaceInputProvider _inputProvider;

    [Header("慣性設定")]
    [SerializeField] private FlightTuningProfile _defaultProfile;
    [Tooltip("ShortcutZoneの発動エリア内でトリガーが成立したときに切り替えるProfile（Splineの高さへ強く吸着する設定）")]
    [SerializeField] private FlightTuningProfile _shortcutProfile;

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private ShortcutZone _currentShortcutZone;
    private bool _isShortcutProfileActive;
    private bool _wasHorizontalAboveThreshold;
    private bool _isInitialized;

    public bool IsInShortcutZone => _currentShortcutZone != null;

    public bool IsShortcutProfileActive => _isShortcutProfileActive;

    // 直近のTickでPlayerFlightControllerへ渡したProfile。Initialize前はnull。
    public FlightTuningProfile CurrentProfile { get; private set; }

    public Transform Transform => transform;

    public void Initialize()
    {
        if (_defaultProfile == null || _shortcutProfile == null)
        {
            Debug.LogError("PlayerManager: Default Profile / Shortcut Profile が未設定です", this);
            return;
        }

        _inputProvider.Initialize();
        _boost.Initialize();
        _flightController.Initialize(_defaultProfile);
        _isInitialized = true;
    }

    // 配置してから、ブースト・ショートカット・飛行の内部状態をリセットする（飛行の向きはtransformから取るため、この順番）。
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (!_isInitialized)
        {
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
        _boost.Initialize();
        // 発動エリアに居るかどうか（_currentShortcutZone）はShortcutZone側のトリガー通知に任せ、吸着状態だけ解除する。
        _isShortcutProfileActive = false;
        _wasHorizontalAboveThreshold = false;
        CurrentProfile = _defaultProfile;
        _flightController.ResetState(CurrentProfile);
    }

    public void Tick(float deltaTime, IReadOnlyRacerData data)
    {
        if (!_isInitialized)
        {
            return;
        }

        _boost.Tick(deltaTime);

        // ゴール後は入力を無視し、通常のProfileでまっすぐ飛ばす。
        if (data.IsFinished)
        {
            CurrentProfile = _defaultProfile;
            _flightController.Tick(0f, _boost.CurrentMaxSpeed, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, CurrentProfile, deltaTime);
            return;
        }

        IRaceInput input = _inputProvider.Current;
        if (input == null)
        {
            return;
        }

        float horizontal = input.Horizontal;

        if (input.BoostPressed)
        {
            _boost.TryActivate();
        }

        // シールドは今回デバッグログ出力のみ。状態管理は一切持たない。
        if (input.ShieldPressed && _showDebugLogs)
        {
            Debug.Log("シールド発動した");
        }

        // ショートカットキー入力自体は常にログを出す（デバッグ用。トラッキング入力側の動作確認にも使う）。
        if (input.ShortcutPressed && _showDebugLogs)
        {
            Debug.Log("ショートカットした");
        }

        UpdateShortcutTrigger(input, horizontal);

        CurrentProfile = _isShortcutProfileActive ? _shortcutProfile : _defaultProfile;
        _flightController.Tick(horizontal, _boost.CurrentMaxSpeed, _boost.CurrentForwardAcceleration, _boost.CurrentSteeringPower, CurrentProfile, deltaTime);
    }

    // ShortcutZone（発動エリア）からの通知。エリアを離れたら吸着も強制的に解除する。
    public void SetInShortcutZone(ShortcutZone zone)
    {
        _currentShortcutZone = zone;
        if (zone == null)
        {
            _isShortcutProfileActive = false;
        }
    }

    // 実際の吸着発動は「発動エリア内であること」＋「そのエリアが指定するトリガー方式で入力があったこと」が条件。
    // どちらを使うか（Eキー／Horizontal閾値）はエリアごとにShortcutZone側で設定する。
    private void UpdateShortcutTrigger(IRaceInput input, float horizontal)
    {
        if (_currentShortcutZone == null)
        {
            return;
        }

        bool zoneTriggered;
        if (_currentShortcutZone.TriggerMethod == EShortcutTriggerMethod.HorizontalThreshold)
        {
            bool isAboveThreshold = Mathf.Abs(horizontal) >= _currentShortcutZone.HorizontalThreshold;
            zoneTriggered = isAboveThreshold && !_wasHorizontalAboveThreshold;
            _wasHorizontalAboveThreshold = isAboveThreshold;
        }
        else
        {
            zoneTriggered = input.ShortcutPressed;
        }

        if (zoneTriggered)
        {
            _isShortcutProfileActive = true;
        }
    }
}
