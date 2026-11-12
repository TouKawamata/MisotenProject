using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// レース本体（RaceInputProvider／PlayerManager）を介さずに、TrackingRaceInputの判定結果
// （Move（左右・上下）／Boost／Shield）だけを単体でテストするためのデバッグ表示。
// PoseTestシーンのように、PoseLandmarkReceiverはあるがレース関連の仕組みは無いシーンでの動作確認用。
// Cキーを押すと_calibrationStartDelay秒後にキャリブレーション（直立姿勢の測定）を始める（しゃがみの判定に必要）。
// Xキーでキャリブレーションの結果を消す（未キャリブレーションの動きを確かめる用）。
public class TrackingRaceInputDebugViewer : MonoBehaviour
{
    // 姿勢データの取得元。未指定ならシーン内から自動検索する。
    [SerializeField] private PoseLandmarkReceiver _poseLandmarkReceiver;
    // トラッキング入力の判定に使う調整用パラメータ。
    [SerializeField] private TrackingRaceInputSettings _trackingSettings = new TrackingRaceInputSettings();

    [Header("キャリブレーション")]
    [Tooltip("Cキーで動かすキャリブレーション。未指定ならシーン内から探し、無ければこのオブジェクトに追加する")]
    [SerializeField] private PoseCalibrator _calibrator;
    [Tooltip("Cキーを押してから測定を始めるまでの秒数（まっすぐ立つまでの猶予）")]
    [SerializeField, Min(0f)] private float _calibrationStartDelay = 1f;

    [Header("見失いの警告")]
    [Tooltip("状態を表示する TrackingLossMonitor。未指定ならシーン内から探す（無ければ表示しない）")]
    [SerializeField] private TrackingLossMonitor _lossMonitor;

    [Header("デバッグ")]
    [Tooltip("ONにするとトラッキングの代わりにキーボード入力（WASD／矢印、Space、左Shift）で確認できる")]
    // ONの場合、トラッキングの代わりにキーボード入力を使う（機材なしでの画面確認用）。
    [SerializeField] private bool _useKeyboardForDebug = false;

    [Tooltip("Boost/Shieldが発火したことを表示する秒数")]
    // トリガー成立時に「!」を緑色で表示し続ける秒数。
    [SerializeField] private float _flashDuration = 0.5f;

    [Header("Moveグラフ")]
    [Tooltip("グラフに表示する履歴の秒数")]
    // グラフに表示する履歴の長さ（秒）。
    [SerializeField, Min(1f)] private float _graphSeconds = 4f;
    [Tooltip("1秒あたりに記録するサンプル数")]
    // 履歴を記録する頻度。フレームレートに依存せず一定間隔で記録する。
    [SerializeField, Min(10)] private int _graphSamplesPerSecond = 30;
    [Tooltip("ガイド線を引く値（±）。この値を超えた部分はオレンジ色で表示する")]
    // ガイド線の位置（±この値）。超えた部分は色を変えて強調する。
    [FormerlySerializedAs("_horizontalGuide")]
    [SerializeField, Range(0f, 1f)] private float _graphGuide = 0.7f;

    // 表示欄の位置と大きさ
    private const float PanelX = 10f;
    private const float PanelY = 190f;
    private const float PanelWidth = 360f;
    private const float PanelHeight = 510f;

    // 現在使用している入力元（キーボード or トラッキング）。
    private IRaceInput _input;
    // トラッキング状態の表示用（キーボードデバッグ時はnull）
    private TrackingRaceInput _trackingInput;

    // 各ジェスチャーがトリガー（成立した瞬間）した回数
    private int _boostCount;
    private int _shieldCount;
    private int _jammerCount;

    // 各ジェスチャーが最後にトリガーした時刻（Time.time基準）。表示のフラッシュ判定に使う。
    private float _lastBoostTime = -999f;
    private float _lastShieldTime = -999f;
    private float _lastJammerTime = -999f;

    // 各ジェスチャーの現在の継続状態（ポーズを取り続けているか＝プレス）。
    private bool _boostHeld;
    private bool _shieldHeld;
    // 妨害弾のポーズを取っているか（妨害弾はHeldが無いので、トラッキングのときだけ表示する）
    private bool _jammerPoseActive;

    // 直近フレームで取得したMoveの値（表示用）。
    private Vector2 _latestMove;

    // グラフ用のMove履歴（リングバッファ）。x・yで書き込み位置は共通。
    private float[] _xHistory;
    private float[] _yHistory;
    // 次に書き込む履歴の位置。
    private int _historyWriteIndex;
    // 記録済みの履歴の数（最大で履歴の長さ）。
    private int _historyCount;
    // 前回記録してからの経過時間。一定間隔で記録するために使う。
    private float _historyTimer;
    // グラフ描画用の1x1白テクスチャ（GUI.colorで色を付けて使う）。
    private Texture2D _whiteTexture;
    // Cキーを押してから測定開始までの残り秒数。待っていないときは負の値
    private float _calibrationCountdown = -1f;

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
            _trackingInput = new TrackingRaceInput(_poseLandmarkReceiver, _trackingSettings);
            _input = _trackingInput;
        }
        // どちらにも当てはまらない場合（Receiverが見つからない）はキーボードにフォールバック
        else
        {
            Debug.LogWarning("TrackingRaceInputDebugViewer: PoseLandmarkReceiverが見つからないため、キーボード入力にフォールバックします", this);
            _input = new KeyboardRaceInput();
        }

        // 判定：キャリブレーションが未設定か（シーン内に無ければこのオブジェクトに追加する）
        if (_calibrator == null)
        {
            _calibrator = FindFirstObjectByType<PoseCalibrator>();
        }

        if (_calibrator == null && _poseLandmarkReceiver != null)
        {
            _calibrator = gameObject.AddComponent<PoseCalibrator>();
        }

        // 判定：見失いの見張り役が未設定か（シーン内から探す。無くても動く）
        if (_lossMonitor == null)
        {
            _lossMonitor = FindFirstObjectByType<TrackingLossMonitor>();
        }

        // グラフ用の履歴バッファを確保
        int capacity = Mathf.Max(2, Mathf.CeilToInt(_graphSeconds * _graphSamplesPerSecond));
        _xHistory = new float[capacity];
        _yHistory = new float[capacity];
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
        UpdateCalibrationKey();

        // 判定：入力元が初期化されているか（未初期化なら何もしない）
        if (_input == null)
        {
            return;
        }

        // Moveの現在値を取得し、グラフ用に履歴へ記録
        _latestMove = _input.Move;
        SampleMove(_latestMove);

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

        // 判定：妨害弾がこのフレームで撃たれたか
        if (_input.JammerPressed)
        {
            _jammerCount++;
            _lastJammerTime = Time.time;
        }

        // ブースト・シールドの継続状態、妨害弾のポーズ
        _boostHeld = _input.BoostHeld;
        _shieldHeld = _input.ShieldHeld;
        _jammerPoseActive = _trackingInput != null && _trackingInput.JammerPoseActive;
    }

    // 画面左にデバッグ情報（入力元・トラッキング状態・キャリブレーション・Move・各ジェスチャー）を表示する。
    private void OnGUI()
    {
        GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), "Race Input Debug");
        float x = PanelX + 10f;
        float y = PanelY + 25f;
        float lineWidth = PanelWidth - 20f;

        // 判定：入力元が初期化されていないか（未初期化ならその旨だけ表示して終了）
        if (_input == null)
        {
            GUI.Label(new Rect(x, y, lineWidth, 20), "入力元が初期化されていません");
            return;
        }

        Color originalColor = GUI.color;

        // 入力元とトラッキング状態
        string sourceLabel = _trackingInput != null ? "Tracking" : "Keyboard (Debug)";
        GUI.Label(new Rect(x, y, lineWidth, 20), $"Source: {sourceLabel}");
        y += 20f;
        if (_trackingInput != null)
        {
            bool tracking = _trackingInput.IsTracking;
            string modeLabel = _poseLandmarkReceiver.Is2DMode ? "2D" : "3D";
            GUI.color = tracking ? Color.green : Color.red;
            GUI.Label(new Rect(x, y, lineWidth, 20), tracking
                ? $"Tracking: OK（{modeLabel}、縦横比 {_poseLandmarkReceiver.Aspect:F2}）"
                : "Tracking: LOST（入力なし扱い）");
            GUI.color = originalColor;
            y += 20f;
        }

        // 見失いの警告の状態
        DrawLossWarningStatus(x, y, lineWidth);
        y += 20f;

        // キャリブレーションの状態
        DrawCalibrationStatus(x, y, lineWidth);
        y += 40f;

        // Moveの数値と、上下左右のスティック表示・x/yのグラフ
        GUI.Label(new Rect(x, y, lineWidth, 20), $"Move: x {_latestMove.x:+0.000;-0.000;0.000}  y {_latestMove.y:+0.000;-0.000;0.000}  |{_latestMove.magnitude:F2}|");
        y += 22f;

        const float stickSize = 120f;
        DrawStick(new Rect(x, y, stickSize, stickSize), _latestMove);
        float graphX = x + stickSize + 10f;
        float graphWidth = lineWidth - stickSize - 10f;
        const float graphHeight = 55f;
        DrawGraph(new Rect(graphX, y, graphWidth, graphHeight), _xHistory, "x");
        DrawGraph(new Rect(graphX, y + graphHeight + 10f, graphWidth, graphHeight), _yHistory, "y");
        y += stickSize + 12f;

        // 各ジェスチャー（Boost/Shield/Jammer）のHolding状態とTrigger回数を1行ずつ表示
        DrawGestureRow(x, y, "Boost", _boostHeld, _boostCount, _lastBoostTime);
        DrawGestureRow(x, y + 25f, "Shield", _shieldHeld, _shieldCount, _lastShieldTime);
        DrawGestureRow(x, y + 50f, "Jammer", _jammerPoseActive, _jammerCount, _lastJammerTime);

        // 妨害弾の判定の途中経過（左右の腕。しきい値の調整用）
        if (_trackingInput != null)
        {
            DrawJammerArm(x, y + 72f, lineWidth, "L", _trackingInput.LeftJammerDebug);
            DrawJammerArm(x, y + 92f, lineWidth, "R", _trackingInput.RightJammerDebug);
        }
    }

    // 妨害弾の判定の途中経過（手首・肘と肩の距離、奥行き）を1行で表示する。条件を満たした値は緑、満たさない値は白。
    private void DrawJammerArm(float x, float y, float width, string label, JammerArmDebug arm)
    {
        Color originalColor = GUI.color;

        // 判定：肩・肘・手首が見えていて、距離を測れたか（測れなければその旨だけ表示）
        if (!arm.Valid)
        {
            GUI.color = Color.gray;
            GUI.Label(new Rect(x, y, width, 20), $"  {label}: 腕が見えない");
            GUI.color = originalColor;
            return;
        }

        string depth = arm.HasDepth ? $"  z {arm.DepthRatio:+0.00;-0.00;0.00}/{_trackingSettings.jammerDepthRatio:F2}" : "  z -";
        GUI.color = arm.Active ? Color.green : Color.white;
        GUI.Label(new Rect(x, y, width, 20),
            $"  {label}: 手首 {arm.WristRatio:F2}/{_trackingSettings.jammerWristRadiusRatio:F2}  肘 {arm.ElbowRatio:F2}/{_trackingSettings.jammerElbowRadiusRatio:F2}{depth}");
        GUI.color = originalColor;
    }

    // 見失いの警告（TrackingLossMonitor）の状態を1行で表示する。
    private void DrawLossWarningStatus(float x, float y, float width)
    {
        // 判定：見張り役が無いか（無ければその旨だけ表示）
        if (_lossMonitor == null)
        {
            GUI.Label(new Rect(x, y, width, 20), "Warning: Monitorなし");
            return;
        }

        Color originalColor = GUI.color;
        string label;
        // 判定：どの状態か
        if (_lossMonitor.State == TrackingLossState.Disconnected)
        {
            GUI.color = Color.red;
            label = $"Warning: 接続なし（{_lossMonitor.LostDuration:F1}秒）";
        }
        else if (_lossMonitor.State == TrackingLossState.PersonLost)
        {
            GUI.color = Color.red;
            label = $"Warning: 人物なし（{_lossMonitor.LostDuration:F1}秒）";
        }
        else if (_lossMonitor.LostDuration > 0f)
        {
            GUI.color = Color.yellow;
            label = $"Warning: なし（検出なし {_lossMonitor.LostDuration:F1}秒）";
        }
        else
        {
            label = "Warning: なし";
        }

        GUI.Label(new Rect(x, y, width, 20), label);
        GUI.color = originalColor;
    }

    // Cキーが押されたら_calibrationStartDelay秒待ってからキャリブレーションを始める（テスト用）。
    // 待っている間にもう一度押すと、待ち時間を最初からやり直す。
    // Xキーでキャリブレーションの結果を消す（開始待ち・測定中なら中止する）。
    private void UpdateCalibrationKey()
    {
        Keyboard keyboard = Keyboard.current;

        // 判定：Xキーが押されたか（押されたら開始待ち・測定を中止し、結果を消して未キャリブレーションに戻す）
        if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
        {
            _calibrationCountdown = -1f;
            // 判定：キャリブレーションがあるか（あれば測定中のものを中止する）
            if (_calibrator != null)
            {
                _calibrator.CancelCalibration();
            }
            PoseCalibration.Clear();
            return;
        }

        // 判定：キャリブレーションが無いか（無ければ何もしない）
        if (_calibrator == null)
        {
            return;
        }

        // 判定：Cキーが押されたか（押されたらカウントダウンを始める。測定中なら中止してから待ち直す）
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
        {
            _calibrator.CancelCalibration();
            _calibrationCountdown = _calibrationStartDelay;
        }

        // 判定：カウントダウン中か
        if (_calibrationCountdown < 0f)
        {
            return;
        }

        _calibrationCountdown -= Time.unscaledDeltaTime;
        // 判定：待ち時間が終わったか（終わったら測定を始める）
        if (_calibrationCountdown <= 0f)
        {
            _calibrationCountdown = -1f;
            _calibrator.StartCalibration();
        }
    }

    // キャリブレーションの状態（開始待ち・測定中・済み・未）を2行で表示する。
    private void DrawCalibrationStatus(float x, float y, float width)
    {
        Color originalColor = GUI.color;

        // 判定：開始待ち（Cキーを押した後のカウントダウン中）か
        if (_calibrationCountdown >= 0f)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x, y, width, 20), $"Calibration: 開始まで {_calibrationCountdown:F1}秒（まっすぐ立つ）");
        }
        // 判定：測定中か
        else if (_calibrator != null && _calibrator.IsCalibrating)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x, y, width, 20), $"Calibration: 測定中 {_calibrator.ElapsedTime:F1} / {_calibrator.Duration:F1}秒（動かない）");
        }
        // 判定：キャリブレーション済みか
        else if (PoseCalibration.HasValue)
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(x, y, width, 20), $"Calibration: 済（肩 {PoseCalibration.ShoulderHeight:F3}、胴 {PoseCalibration.TorsoLength:F3}）");
        }
        else
        {
            GUI.color = new Color(1f, 0.65f, 0f);
            GUI.Label(new Rect(x, y, width, 20), "Calibration: 未（しゃがみ＝下は出ません）");
        }

        GUI.color = originalColor;

        // 直近の失敗理由、またはキー操作の案内
        string sub = _calibrator != null && !string.IsNullOrEmpty(_calibrator.LastFailureReason)
            ? $"失敗：{_calibrator.LastFailureReason}"
            : $"Cキーで{_calibrationStartDelay:F1}秒後にキャリブレーション / Xキーでリセット";
        GUI.Label(new Rect(x, y + 18f, width, 20), sub);
    }

    // Moveを、中央（ニュートラル）を基準にした上下左右のスティックとして描画する。
    private void DrawStick(Rect area, Vector2 move)
    {
        DrawRect(area, new Color(0f, 0f, 0f, 0.65f));

        // 中心の十字線
        Color lineColor = new Color(1f, 1f, 1f, 0.25f);
        DrawRect(new Rect(area.center.x, area.y, 1f, area.height), lineColor);
        DrawRect(new Rect(area.x, area.center.y, area.width, 1f), lineColor);

        // 現在値の点（上が+y）
        Vector2 clamped = Vector2.ClampMagnitude(move, 1f);
        float half = area.width * 0.5f;
        Vector2 point = new Vector2(area.center.x + clamped.x * half, area.center.y - clamped.y * half);
        const float dotSize = 8f;
        // 判定：ガイドを超えているか（超えていればオレンジ、それ以外は緑）
        Color dotColor = clamped.magnitude >= _graphGuide ? new Color(1f, 0.75f, 0.1f) : Color.green;
        DrawRect(new Rect(point.x - dotSize * 0.5f, point.y - dotSize * 0.5f, dotSize, dotSize), dotColor);
    }

    // Moveの値を一定間隔で履歴（リングバッファ）に記録する。
    private void SampleMove(Vector2 value)
    {
        float interval = 1f / _graphSamplesPerSecond;
        _historyTimer += Time.unscaledDeltaTime;

        // フレームレートに依存しないよう、経過時間分だけ一定間隔で記録する
        while (_historyTimer >= interval)
        {
            _historyTimer -= interval;

            _xHistory[_historyWriteIndex] = value.x;
            _yHistory[_historyWriteIndex] = value.y;
            _historyWriteIndex = (_historyWriteIndex + 1) % _xHistory.Length;
            _historyCount = Mathf.Min(_historyCount + 1, _xHistory.Length);
        }
    }

    // 履歴を時系列グラフとして描画する（上が+1、下が-1、右端が最新）。
    private void DrawGraph(Rect rect, float[] history, string label)
    {
        // 背景
        DrawRect(rect, new Color(0f, 0f, 0f, 0.65f));

        // 0の基準線
        DrawHorizontalLine(rect, 0f, new Color(1f, 1f, 1f, 0.25f));
        // ±ガイド線
        DrawHorizontalLine(rect, _graphGuide, new Color(1f, 0.65f, 0f, 0.75f));
        DrawHorizontalLine(rect, -_graphGuide, new Color(1f, 0.65f, 0f, 0.75f));

        // 判定：線を描けるだけの履歴がたまっているか
        if (_historyCount >= 2)
        {
            float columnWidth = rect.width / Mathf.Max(1, history.Length - 1);
            // 判定：履歴が一周しているか（一周していれば書き込み位置が最古のデータ）
            int firstIndex = _historyCount == history.Length ? _historyWriteIndex : 0;

            for (int i = 0; i < _historyCount; i++)
            {
                int index = (firstIndex + i) % history.Length;
                float value = Mathf.Clamp(history[index], -1f, 1f);

                float columnX = rect.x + i * columnWidth;
                float centerY = rect.center.y;
                float valueY = centerY - value * rect.height * 0.5f;

                // 判定：ガイド線を超えているか（超えていればオレンジ、それ以外は緑）
                Color color = Mathf.Abs(value) >= _graphGuide ? new Color(1f, 0.75f, 0.1f) : Color.green;

                // 0の位置から値の位置までの縦線を1本描く
                float top = Mathf.Min(centerY, valueY);
                float height = Mathf.Max(1f, Mathf.Abs(valueY - centerY));
                DrawRect(new Rect(columnX, top, Mathf.Max(1f, columnWidth), height), color);
            }
        }

        // 軸の名前
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 40f, 20f), label);
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
    private void DrawGestureRow(float x, float y, string label, bool held, int triggerCount, float lastTriggerTime)
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
