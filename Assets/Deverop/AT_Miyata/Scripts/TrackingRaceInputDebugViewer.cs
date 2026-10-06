using UnityEngine;

// レース本体（RaceInputProvider／PlayerManager）を介さずに、TrackingRaceInputの判定結果
// （Horizontal／Boost／Shield／Shortcut）だけを単体でテストするためのデバッグ表示。
// PoseTestシーンのように、PoseLandmarkReceiverはあるがレース関連の仕組みは無いシーンでの動作確認用。
public class TrackingRaceInputDebugViewer : MonoBehaviour
{
    // 姿勢データの取得元。未指定ならシーン内から自動検索する。
    [SerializeField] private PoseLandmarkReceiver _poseLandmarkReceiver;
    // トラッキング入力の判定に使う調整用パラメータ。
    [SerializeField] private TrackingRaceInputSettings _trackingSettings = new TrackingRaceInputSettings();

    [Header("デバッグ")]
    [Tooltip("ONにするとトラッキングの代わりにキーボード入力（A/D、Space、Shift、E）で確認できる")]
    // ONの場合、トラッキングの代わりにキーボード入力を使う（機材なしでの画面確認用）。
    [SerializeField] private bool _useKeyboardForDebug = false;

    [Tooltip("Boost/Shield/Shortcutが発火したことを表示する秒数")]
    // トリガー成立時に「TRIGGERED」を緑色で表示し続ける秒数。
    [SerializeField] private float _flashDuration = 0.5f;

    [Header("Horizontalグラフ（GameSceneのDebugHudと同じ表示）")]
    [Tooltip("グラフに表示する履歴の秒数")]
    // グラフに表示する履歴の長さ（秒）。
    [SerializeField, Min(1f)] private float _graphSeconds = 4f;
    [Tooltip("1秒あたりに記録するサンプル数")]
    // 履歴を記録する頻度。フレームレートに依存せず一定間隔で記録する。
    [SerializeField, Min(10)] private int _graphSamplesPerSecond = 30;
    [Tooltip("ガイド線を引く値（±）。この値を超えた部分はオレンジ色で表示する")]
    // ガイド線の位置（±この値）。超えた部分は色を変えて強調する。
    [SerializeField, Range(0f, 1f)] private float _horizontalGuide = 0.7f;

    // 現在使用している入力元（キーボード or トラッキング）。
    private IRaceInput _input;
    // ショートカットのHeld（継続状態）はIRaceInputには無いTrackingRaceInput独自のプロパティのため、
    // それを読むためだけにキャストした参照を持つ（キーボードデバッグ時はnullのまま）。
    private TrackingRaceInput _trackingInput;

    // 各ジェスチャーがトリガー（成立した瞬間）した回数
    private int _boostCount;
    private int _shieldCount;
    private int _shortcutCount;

    // 各ジェスチャーが最後にトリガーした時刻（Time.time基準）。表示のフラッシュ判定に使う。
    private float _lastBoostTime = -999f;
    private float _lastShieldTime = -999f;
    private float _lastShortcutTime = -999f;

    // 各ジェスチャーの現在の継続状態（ポーズを取り続けているか＝プレス）。
    private bool _boostHeld;
    private bool _shieldHeld;
    private bool _shortcutHeld;

    // 直近フレームで取得したHorizontalの値（表示用）。
    private float _latestHorizontal;

    // グラフ用のHorizontal履歴（リングバッファ）。
    private float[] _horizontalHistory;
    // 次に書き込む履歴の位置。
    private int _historyWriteIndex;
    // 記録済みの履歴の数（最大で_horizontalHistory.Length）。
    private int _historyCount;
    // 前回記録してからの経過時間。一定間隔で記録するために使う。
    private float _historyTimer;
    // グラフ描画用の1x1白テクスチャ（GUI.colorで色を付けて使う）。
    private Texture2D _whiteTexture;

    // 起動時に、使用する入力元（キーボード／トラッキング）を決定する。
    private void Awake()
    {
        // 判定：PoseLandmarkReceiverの参照が未設定か（未設定ならシーン内から自動検索する）
        if (_poseLandmarkReceiver == null)
        {
            _poseLandmarkReceiver = FindFirstObjectByType<PoseLandmarkReceiver>();
        }

        // 判定：デバッグ用にキーボードを使う設定になっているか（ONなら常にキーボード入力を使う）
        if (_useKeyboardForDebug)
        {
            _input = new KeyboardRaceInput();
        }
        // 判定：PoseLandmarkReceiverが見つかっているか（見つかっていればトラッキング入力を使う）
        else if (_poseLandmarkReceiver != null)
        {
            _input = new TrackingRaceInput(_poseLandmarkReceiver, _trackingSettings);
        }
        // どちらにも当てはまらない場合（Receiverが見つからない）はキーボードにフォールバック
        else
        {
            Debug.LogWarning("TrackingRaceInputDebugViewer: PoseLandmarkReceiverが見つからないため、キーボード入力にフォールバックします", this);
            _input = new KeyboardRaceInput();
        }

        // グラフ用の履歴バッファを確保
        int capacity = Mathf.Max(2, Mathf.CeilToInt(_graphSeconds * _graphSamplesPerSecond));
        _horizontalHistory = new float[capacity];

        // ショートカットのHeld（プレス）表示のために、TrackingRaceInputであればキャストして保持しておく（キーボード時はnull）
        _trackingInput = _input as TrackingRaceInput;
    }

    // 作成した白テクスチャを破棄する。
    private void OnDestroy()
    {
        if (_whiteTexture != null)
        {
            Destroy(_whiteTexture);
        }
    }

    // 毎フレーム、入力値を取得して表示用の状態を更新する。
    private void Update()
    {
        // 判定：入力元が初期化されているか（未初期化なら何もしない）
        if (_input == null)
        {
            return;
        }

        // Horizontalの現在値を取得
        _latestHorizontal = _input.Horizontal;
        // グラフ用に履歴へ記録
        SampleHorizontal(_latestHorizontal);

        // 判定：ブーストがこのフレームでトリガーしたか（成立した瞬間だけtrue）
        if (_input.BoostPressed)
        {
            _boostCount++;
            _lastBoostTime = Time.time;
        }

        // 判定：シールドがこのフレームでトリガーしたか
        if (_input.ShieldPressed)
        {
            _shieldCount++;
            _lastShieldTime = Time.time;
        }

        // 判定：ショートカットがこのフレームでトリガーしたか
        if (_input.ShortcutPressed)
        {
            _shortcutCount++;
            _lastShortcutTime = Time.time;
        }

        // ブースト・シールドの継続状態はIRaceInputから取得する。
        _boostHeld = _input.BoostHeld;
        _shieldHeld = _input.ShieldHeld;
        // ショートカットの継続状態はTrackingRaceInput独自のため、キーボードモード（_trackingInputがnull）のときは常にfalse
        _shortcutHeld = _trackingInput != null && _trackingInput.IsShortcutPoseActive;
    }

    // 画面左上にデバッグ情報（Source／Horizontal／各ジェスチャーの状態）を表示する。
    private void OnGUI()
    {
        // デバッグ表示欄のサイズ
        const int width = 340;
        const int height = 300;
        GUI.Box(new Rect(10, 190, width, height), "Race Input Debug");

        // 判定：入力元が初期化されていないか（未初期化ならその旨だけ表示して終了）
        if (_input == null)
        {
            GUI.Label(new Rect(20, 215, width - 20, 20), "入力元が初期化されていません");
            return;
        }

        // 現在の入力元（トラッキング or キーボード）を表示
        string sourceLabel = _useKeyboardForDebug ? "Keyboard (Debug)" : "Tracking";
        GUI.Label(new Rect(20, 215, width - 20, 20), $"Source: {sourceLabel}");
        // 判定：キーボードモードかどうか（キーボードモードではショートカットのHeld表示が非対応であることを注記する）
        if (_useKeyboardForDebug)
        {
            GUI.Label(new Rect(20, 233, width - 20, 20), "※ShortcutのHeldはキーボードデバッグ時は非対応");
        }

        // Horizontalの数値とバー表示
        GUI.Label(new Rect(20, 253, width - 20, 20), $"Horizontal: {_latestHorizontal:+0.000;-0.000;0.000}");
        DrawHorizontalBar(new Rect(20, 273, width - 40, 16), _latestHorizontal);
        // 直近の推移を時系列グラフで表示（GameSceneのDebugHudと同じ表示）
        DrawHorizontalGraph(new Rect(20, 295, width - 40, 100));

        // 各ジェスチャー（Boost/Shield/Shortcut）のHolding状態とTrigger回数を1行ずつ表示
        DrawGestureRow(20, 405, "Boost", _boostHeld, _boostCount, _lastBoostTime);
        DrawGestureRow(20, 430, "Shield", _shieldHeld, _shieldCount, _lastShieldTime);
        DrawGestureRow(20, 455, "Shortcut (Banzai)", _shortcutHeld, _shortcutCount, _lastShortcutTime);
    }

    // Horizontalの値を、中央（直立＝0）を基準にした横棒グラフとして描画する。
    private void DrawHorizontalBar(Rect area, float value)
    {
        // バーの背景
        GUI.Box(area, GUIContent.none);

        // 表示上も-1～1にクランプしておく（念のため）
        float clamped = Mathf.Clamp(value, -1f, 1f);
        // バー領域の中心X座標（Horizontal=0の位置）
        float centerX = area.x + area.width * 0.5f;
        // 中心から端までの幅
        float halfWidth = area.width * 0.5f;
        // 現在値に応じたマーカーのX座標
        float markerX = centerX + halfWidth * clamped;

        // 中央線（直立の基準位置）
        GUI.Box(new Rect(centerX - 1, area.y, 2, area.height), GUIContent.none);

        // 現在値のマーカー
        float markerWidth = 6f;
        GUI.Box(new Rect(markerX - markerWidth * 0.5f, area.y, markerWidth, area.height), GUIContent.none);
    }

    // Horizontalの値を一定間隔で履歴（リングバッファ）に記録する。
    private void SampleHorizontal(float value)
    {
        float interval = 1f / _graphSamplesPerSecond;
        _historyTimer += Time.unscaledDeltaTime;

        // フレームレートに依存しないよう、経過時間分だけ一定間隔で記録する
        while (_historyTimer >= interval)
        {
            _historyTimer -= interval;

            _horizontalHistory[_historyWriteIndex] = value;
            _historyWriteIndex = (_historyWriteIndex + 1) % _horizontalHistory.Length;
            _historyCount = Mathf.Min(_historyCount + 1, _horizontalHistory.Length);
        }
    }

    // Horizontalの履歴を時系列グラフとして描画する（上が+1、下が-1、右端が最新）。
    private void DrawHorizontalGraph(Rect rect)
    {
        // 背景
        DrawRect(rect, new Color(0f, 0f, 0f, 0.65f));

        // 0の基準線
        DrawHorizontalLine(rect, 0f, new Color(1f, 1f, 1f, 0.25f));
        // ±ガイド線
        DrawHorizontalLine(rect, _horizontalGuide, new Color(1f, 0.65f, 0f, 0.75f));
        DrawHorizontalLine(rect, -_horizontalGuide, new Color(1f, 0.65f, 0f, 0.75f));

        // 判定：線を描けるだけの履歴がたまっているか
        if (_historyCount >= 2)
        {
            float columnWidth = rect.width / Mathf.Max(1, _horizontalHistory.Length - 1);
            // 判定：履歴が一周しているか（一周していれば書き込み位置が最古のデータ）
            int firstIndex = _historyCount == _horizontalHistory.Length ? _historyWriteIndex : 0;

            for (int i = 0; i < _historyCount; i++)
            {
                int index = (firstIndex + i) % _horizontalHistory.Length;
                float value = Mathf.Clamp(_horizontalHistory[index], -1f, 1f);

                float x = rect.x + i * columnWidth;
                float centerY = rect.center.y;
                float valueY = centerY - value * rect.height * 0.5f;

                // 判定：ガイド線を超えているか（超えていればオレンジ、それ以外は緑）
                Color color = Mathf.Abs(value) >= _horizontalGuide ? new Color(1f, 0.75f, 0.1f) : Color.green;

                // 0の位置から値の位置までの縦線を1本描く
                float top = Mathf.Min(centerY, valueY);
                float height = Mathf.Max(1f, Mathf.Abs(valueY - centerY));
                DrawRect(new Rect(x, top, Mathf.Max(1f, columnWidth), height), color);
            }
        }

        // 上端・下端の目盛りラベル
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 80f, 20f), "+1.0");
        GUI.Label(new Rect(rect.x + 4f, rect.yMax - 20f, 80f, 20f), "-1.0");
    }

    // グラフ内の指定した値の高さに横線を引く。
    private void DrawHorizontalLine(Rect rect, float value, Color color)
    {
        value = Mathf.Clamp(value, -1f, 1f);
        float y = rect.center.y - value * rect.height * 0.5f;
        DrawRect(new Rect(rect.x, y, rect.width, 1f), color);
    }

    // 指定した色で矩形を塗りつぶす。
    private void DrawRect(Rect rect, Color color)
    {
        // 判定：白テクスチャが未作成か（初回のみ作成する）
        if (_whiteTexture == null)
        {
            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        Color originalColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _whiteTexture);
        GUI.color = originalColor;
    }

    // 1つのジェスチャーについて、Holding（プレス中かどうか）とTrigger回数を1行で表示する。
    // held: ポーズを取り続けている間ずっとtrue（プレス）。lastTriggerTime: ポーズが成立した瞬間（トリガー）の時刻。
    private void DrawGestureRow(int x, int y, string label, bool held, int triggerCount, float lastTriggerTime)
    {
        // 判定：直近のトリガーから_flashDuration秒以内か（以内ならTRIGGERED表示を点灯させる）
        bool isFlashing = Time.time - lastTriggerTime <= _flashDuration;

        Color originalColor = GUI.color;

        // プレス（継続状態）の表示：held中は水色でHOLDINGと表示
        GUI.color = held ? Color.cyan : Color.white;
        GUI.Label(new Rect(x, y, 150, 20), $"{label}: {(held ? "HOLDING" : "-")}");

        // トリガー（成立した瞬間）の表示：フラッシュ中は緑色で強調表示
        GUI.color = isFlashing ? Color.green : Color.white;
        GUI.Label(new Rect(x + 150, y, 170, 20), $"Trigger x{triggerCount}{(isFlashing ? "  !" : string.Empty)}");

        GUI.color = originalColor;
    }
}
