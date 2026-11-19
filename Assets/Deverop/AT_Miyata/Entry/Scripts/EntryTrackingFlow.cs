using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Entry の開始の合図として使うボタン（ポーズ）
public enum EntryTriggerButton {
	// 妨害弾（片手を前に突き出す）
	Jammer,
	// ブースト（空手の構え）
	Boost,
	// シールド（片手を胸の前で水平に構える）
	Shield
}

// Entry シーンの流れ（トラッキング）。
//  1. 待機：人物が _personStableTime 秒続けて映るまで待つ。
//  2. キャリブレーション：直立姿勢の測定（PoseCalibrator）と、エントリー演出（EntryEffectsController）を同時に始める。
//     失敗したら _retryDelay 秒待って測り直す（演出は続けたまま）。
//  3. 合図待ち：キャリブレーションに成功したら、合図のポーズ（_triggerButton、初期値は妨害弾）を待つ。
//  4. 合図が来たら _nextSceneName のシーンに移る。
// 演出が始まった後に人物が _abandonTime 秒映らなかったら、プレイヤーが去ったとみなして最初（待機）に戻る。
// 案内のメッセージは画面の下に出す（仮の表示）。UIの参照が未指定なら、自分の子に Canvas を作る。
public class EntryTrackingFlow : MonoBehaviour {
	// 流れの状態
	public enum FlowState {
		// 人物が映るのを待っている
		WaitingPerson,
		// キャリブレーション中
		Calibrating,
		// キャリブレーションに失敗し、測り直すまで待っている
		RetryWait,
		// 合図のポーズを待っている
		WaitingTrigger,
		// シーンを移っている
		Loading
	}

	[Header("参照（未指定なら自動で探す）")]
	[Tooltip("姿勢データの取得元。未指定ならシーン内から探す")]
	[SerializeField] private PoseLandmarkReceiver _receiver;
	[Tooltip("直立姿勢の測定。未指定ならシーン内から探し、無ければこのオブジェクトに追加する")]
	[SerializeField] private PoseCalibrator _calibrator;
	[Tooltip("エントリー演出（3D）。キャリブレーションと同時に StartAnimation() を呼ぶ。未指定ならシーン内から探す")]
	[SerializeField] private EntryEffectsController _effectsController;

	[Header("判定の調整値（合図のポーズの判定に使う）")]
	[Tooltip("判定の調整値のアセット。指定すると下の Tracking Settings より優先する")]
	[SerializeField] private TrackingRaceInputSettingsAsset _settingsAsset;
	[Tooltip("アセットが未指定のときに使う調整値")]
	[SerializeField] private TrackingRaceInputSettings _trackingSettings = new TrackingRaceInputSettings();

	[Header("待機")]
	[Tooltip("人物がこの秒数続けて映ったら、キャリブレーションと演出を始める")]
	[SerializeField, Min(0f)] private float _personStableTime = 1f;

	[Header("キャリブレーション")]
	[Tooltip("キャリブレーションに失敗してから測り直すまでの秒数")]
	[SerializeField, Min(0f)] private float _retryDelay = 1f;

	[Header("合図")]
	[Tooltip("シーンを移る合図にするポーズ")]
	[SerializeField] private EntryTriggerButton _triggerButton = EntryTriggerButton.Jammer;
	[Tooltip("キャリブレーションに成功してから合図を受け付けるまでの秒数（成功した瞬間に誤って反応しないように）")]
	[SerializeField, Min(0f)] private float _triggerEnableDelay = 0.3f;

	[Header("やり直し")]
	[Tooltip("演出が始まった後、人物がこの秒数映らなかったら最初（待機）に戻る。0ならやり直さない")]
	[SerializeField, Min(0f)] private float _abandonTime = 5f;

	[Header("シーン遷移")]
	[Tooltip("合図が来たら移るシーンの名前（Build Settings に入っている必要がある）。空ならシーンを移らない")]
	[SerializeField] private string _nextSceneName = "GameScene";

	[Header("キーボード（テスト用）")]
	[Tooltip("ONにすると、合図をキーボードでも出せる（妨害弾：Q／ブースト：Space／シールド：左Shift）。Enter で待機とキャリブレーションを飛ばす")]
	[SerializeField] private bool _allowKeyboard = true;

	[Header("メッセージ")]
	[Tooltip("待機中のメッセージ")]
	[SerializeField, TextArea] private string _waitingMessage = "カメラの前に立ってください";
	[Tooltip("キャリブレーション中のメッセージ")]
	[SerializeField, TextArea] private string _calibratingMessage = "まっすぐ立ってください";
	[Tooltip("キャリブレーションに失敗したときのメッセージ")]
	[SerializeField, TextArea] private string _retryMessage = "もう一度、まっすぐ立ってください";
	[Tooltip("合図を待っているときのメッセージ")]
	[SerializeField, TextArea] private string _triggerMessage = "片手を前に突き出してスタート！";

	[Header("帯の色（メッセージごと。透明度を0にすると帯を出さない）")]
	[Tooltip("待機中の帯の色")]
	[SerializeField] private Color _waitingBandColor = new Color(0.6f, 0.05f, 0.05f, 0.85f);
	[Tooltip("キャリブレーション中の帯の色")]
	[SerializeField] private Color _calibratingBandColor = new Color(0.6f, 0.05f, 0.05f, 0.85f);
	[Tooltip("キャリブレーションに失敗したときの帯の色")]
	[SerializeField] private Color _retryBandColor = new Color(0.6f, 0.05f, 0.05f, 0.85f);
	[Tooltip("合図を待っているときの帯の色")]
	[SerializeField] private Color _triggerBandColor = new Color(0.6f, 0.05f, 0.05f, 0.85f);

	[Header("見た目（自動で作るUIに使う）")]
	[Tooltip("文字のフォント。日本語が入ったフォントを指定する（追加したときに NotoSerifJP-Bold SDF を自動で探して入れる）")]
	[SerializeField] private TMP_FontAsset _font;
	[Tooltip("文字の大きさ（1920x1080基準）")]
	[SerializeField, Min(1f)] private float _fontSize = 56f;
	[Tooltip("文字の色")]
	[SerializeField] private Color _textColor = Color.white;
	[Tooltip("帯の高さ（1920x1080基準）")]
	[SerializeField, Min(1f)] private float _bandHeight = 220f;
	[Tooltip("帯の画面の下からの位置（1920x1080基準）")]
	[SerializeField] private float _bottomOffset = 120f;
	[Tooltip("Canvasの描画順（大きいほど手前）。見失いの警告（1000）より奥にする")]
	[SerializeField] private int _sortingOrder = 900;

	[Header("UIの参照（未指定なら自動で作る）")]
	[Tooltip("案内のメッセージを出すテキスト")]
	[SerializeField] private TMP_Text _messageText;
	[Tooltip("メッセージの後ろの帯。未指定でもテキストがあれば帯なしで動く")]
	[SerializeField] private Image _bandImage;

	[Header("イベント（Inspectorから演出をつなぐ用）")]
	[Tooltip("キャリブレーションと演出を始めたとき")]
	[SerializeField] private UnityEvent _onEntryStarted = new UnityEvent();
	[Tooltip("キャリブレーションに成功し、合図を待ち始めたとき")]
	[SerializeField] private UnityEvent _onReady = new UnityEvent();
	[Tooltip("合図が来たとき（シーンを移る直前）")]
	[SerializeField] private UnityEvent _onTriggered = new UnityEvent();
	[Tooltip("プレイヤーが去ったとみなして最初に戻ったとき")]
	[SerializeField] private UnityEvent _onAbandoned = new UnityEvent();

	// 合図のポーズの判定に使う入力（トラッキング＋キーボード）
	private IRaceInput _input;
	// 人物が続けて映っている秒数（待機中）
	private float _personTime;
	// 人物が映っていない秒数（演出が始まった後）
	private float _lostTime;
	// 今の状態に入ってからの秒数
	private float _stateTime;
	// キャリブレーションの直近の結果（Finished で受け取り、Update で処理する）
	private bool? _calibrationResult;

	// 今の状態
	public FlowState State { get; private set; } = FlowState.WaitingPerson;

	private void Awake() {
		// 判定：Receiverが未設定か（未設定ならシーン内から探す）
		if (_receiver == null) {
			_receiver = FindFirstObjectByType<PoseLandmarkReceiver>();
		}

		// 判定：演出が未設定か（未設定ならシーン内から探す）
		if (_effectsController == null) {
			_effectsController = FindFirstObjectByType<EntryEffectsController>();
		}

		// 判定：キャリブレーションが未設定か（シーン内 → 追加 の順に用意する）
		if (_calibrator == null) {
			_calibrator = FindFirstObjectByType<PoseCalibrator>();
		}
		if (_calibrator == null) {
			_calibrator = gameObject.AddComponent<PoseCalibrator>();
		}

		_input = CreateInput();

		// 判定：UIの参照が未設定か（無ければ自動で作る）
		if (_messageText == null) {
			BuildUI();
		}
	}

	private void OnEnable() {
		_calibrator.Finished += OnCalibrationFinished;
	}

	private void OnDisable() {
		_calibrator.Finished -= OnCalibrationFinished;
	}

	private void Start() {
		SetState(FlowState.WaitingPerson);
	}

	private void Update() {
		float deltaTime = Time.unscaledDeltaTime;
		_stateTime += deltaTime;

		// 合図のポーズは「腕を戻すまで次は出ない」仕組みのため、どの状態でも毎フレーム評価しておく
		bool triggered = IsTriggerPressed();
		bool tracking = _receiver != null && _receiver.IsTracking;

		// 判定：プレイヤーが去ったか（演出が始まった後、人物が映らない時間が続いたら最初に戻る）
		if (State != FlowState.WaitingPerson && State != FlowState.Loading) {
			_lostTime = tracking ? 0f : _lostTime + deltaTime;
			if (_abandonTime > 0f && _lostTime >= _abandonTime) {
				Abandon();
				return;
			}
		}

		switch (State) {
			case FlowState.WaitingPerson:
				UpdateWaitingPerson(tracking, deltaTime);
				break;
			case FlowState.Calibrating:
				UpdateCalibrating();
				break;
			case FlowState.RetryWait:
				UpdateRetryWait(tracking);
				break;
			case FlowState.WaitingTrigger:
				UpdateWaitingTrigger(triggered);
				break;
			default:
				break;
		}
	}

	// 待機：人物が続けて映ったら始める。
	private void UpdateWaitingPerson(bool tracking, float deltaTime) {
		_personTime = tracking ? _personTime + deltaTime : 0f;

		// 判定：テスト用に Enter で飛ばすか（キャリブレーションをせずに合図待ちへ）
		if (_allowKeyboard && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) {
			StartEntryEffect();
			EnterReady();
			return;
		}

		// 判定：人物が _personStableTime 秒続けて映ったか
		if (_personTime >= _personStableTime) {
			StartEntryEffect();
			StartCalibration();
		}
	}

	// キャリブレーション中：結果を待つ。
	private void UpdateCalibrating() {
		// 判定：結果が届いたか
		if (!_calibrationResult.HasValue) {
			UpdateMessage();
			return;
		}

		bool succeeded = _calibrationResult.Value;
		_calibrationResult = null;
		// 判定：成功したか（成功なら合図待ち、失敗なら測り直し）
		if (succeeded) {
			EnterReady();
		} else {
			SetState(FlowState.RetryWait);
		}
	}

	// 測り直しの待ち：時間がたち、人物が映っていれば測り直す。
	private void UpdateRetryWait(bool tracking) {
		// 判定：待ち時間が過ぎ、人物が映っているか
		if (_stateTime >= _retryDelay && tracking) {
			StartCalibration();
		}
	}

	// 合図待ち：合図が来たらシーンを移る。
	private void UpdateWaitingTrigger(bool triggered) {
		// 判定：合図を受け付ける時間になっていて、合図が来たか
		if (_stateTime >= _triggerEnableDelay && triggered) {
			_onTriggered.Invoke();
			LoadNextScene();
		}
	}

	// エントリー演出を始める（キャンバスの演出は EntryEffectsController が続けて出す）。
	private void StartEntryEffect() {
		// 判定：演出が見つかっているか
		if (_effectsController != null) {
			_effectsController.StartAnimation();
		} else {
			Debug.LogWarning("EntryTrackingFlow: EntryEffectsController が見つからないため、演出を出せません", this);
		}

		_lostTime = 0f;
		_onEntryStarted.Invoke();
	}

	private void StartCalibration() {
		_calibrationResult = null;
		SetState(FlowState.Calibrating);
		_calibrator.StartCalibration();
	}

	private void EnterReady() {
		SetState(FlowState.WaitingTrigger);
		_onReady.Invoke();
	}

	// プレイヤーが去ったので、測定・演出・キャリブレーションの結果を消して最初に戻る。
	private void Abandon() {
		_calibrator.CancelCalibration();
		_calibrationResult = null;
		PoseCalibration.Clear();
		// 判定：演出が見つかっているか（あれば最初の状態に戻す。キャンバスも一緒に戻る）
		if (_effectsController != null) {
			_effectsController.ResetAnimation();
		}

		_onAbandoned.Invoke();
		SetState(FlowState.WaitingPerson);
	}

	private void LoadNextScene() {
		// 判定：移るシーンが指定されているか（空なら移らない）
		if (string.IsNullOrEmpty(_nextSceneName)) {
			Debug.Log("EntryTrackingFlow: 合図が来ました（Next Scene Name が空なので、シーンは移りません）", this);
			SetState(FlowState.WaitingTrigger);
			return;
		}

		// 判定：Build Settings に入っているか（入っていないと LoadScene がエラーになる）
		if (!Application.CanStreamedLevelBeLoaded(_nextSceneName)) {
			Debug.LogError($"EntryTrackingFlow: シーン「{_nextSceneName}」が Build Settings にありません", this);
			SetState(FlowState.WaitingTrigger);
			return;
		}

		SetState(FlowState.Loading);
		SceneManager.LoadScene(_nextSceneName);
	}

	private void SetState(FlowState state) {
		State = state;
		_stateTime = 0f;
		// 判定：待機に入るか（続けて映っている時間を数え直す）
		if (state == FlowState.WaitingPerson) {
			_personTime = 0f;
		}

		UpdateMessage();
	}

	// 合図のボタン（ポーズ）が、このフレームで押されたか。
	private bool IsTriggerPressed() {
		switch (_triggerButton) {
			case EntryTriggerButton.Boost:
				return _input.BoostPressed;
			case EntryTriggerButton.Shield:
				return _input.ShieldPressed;
			default:
				return _input.JammerPressed;
		}
	}

	// 合図の判定に使う入力を作る（Receiver が無ければキーボードだけ）。
	private IRaceInput CreateInput() {
		// 判定：Receiverが無いか（無ければトラッキングは使えない）
		if (_receiver == null) {
			Debug.LogWarning("EntryTrackingFlow: PoseLandmarkReceiver が見つかりません。キーボードだけで動かします（Enter で開始、合図はキーボード）", this);
			return new KeyboardRaceInput();
		}

		TrackingRaceInputSettings settings = TrackingRaceInputSettingsAsset.Resolve(_settingsAsset, _trackingSettings);
		IRaceInput tracking = new TrackingRaceInput(_receiver, settings);
		// 判定：キーボードとの併用を許可しているか
		if (_allowKeyboard) {
			return new CombinedRaceInput(tracking, new KeyboardRaceInput());
		}

		return tracking;
	}

	private void OnCalibrationFinished(bool succeeded) {
		// 判定：自分が始めた測定か（キャリブレーション中以外の結果は使わない）
		if (State == FlowState.Calibrating) {
			_calibrationResult = succeeded;
		}
	}

	// 状態に合わせてメッセージを変える。
	private void UpdateMessage() {
		// 判定：テキストがあるか
		if (_messageText == null) {
			return;
		}

		// 帯の色（メッセージが無い状態では帯も消す）
		Color bandColor = Color.clear;
		switch (State) {
			case FlowState.WaitingPerson:
				_messageText.text = _waitingMessage;
				bandColor = _waitingBandColor;
				break;
			case FlowState.Calibrating:
				int percent = Mathf.RoundToInt(_calibrator.Progress * 100f);
				_messageText.text = $"{_calibratingMessage}\n<size=60%>{percent}%</size>";
				bandColor = _calibratingBandColor;
				break;
			case FlowState.RetryWait:
				_messageText.text = _retryMessage;
				bandColor = _retryBandColor;
				break;
			case FlowState.WaitingTrigger:
				_messageText.text = _triggerMessage;
				bandColor = _triggerBandColor;
				break;
			default:
				_messageText.text = string.Empty;
				break;
		}

		// 判定：帯があるか（透明度0なら非表示にして、余計な描画をしない）
		if (_bandImage != null) {
			_bandImage.color = bandColor;
			_bandImage.enabled = bandColor.a > 0f;
		}
	}

	// 案内のメッセージ用の Canvas とテキストを自分の子に作る。
	private void BuildUI() {
		// Canvas（Screen Space - Overlay。1920x1080基準で拡大縮小）
		GameObject canvasObject = new GameObject("EntryTrackingFlowCanvas", typeof(RectTransform));
		canvasObject.transform.SetParent(transform, false);
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = _sortingOrder;
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		// 帯（画面の横幅いっぱい、画面の下から _bottomOffset の位置）
		GameObject bandObject = new GameObject("Band", typeof(RectTransform));
		bandObject.transform.SetParent(canvasObject.transform, false);
		RectTransform bandRect = (RectTransform)bandObject.transform;
		bandRect.anchorMin = new Vector2(0f, 0f);
		bandRect.anchorMax = new Vector2(1f, 0f);
		bandRect.pivot = new Vector2(0.5f, 0f);
		bandRect.sizeDelta = new Vector2(0f, _bandHeight);
		bandRect.anchoredPosition = new Vector2(0f, _bottomOffset);
		Image bandImage = bandObject.AddComponent<Image>();
		bandImage.color = _waitingBandColor;
		bandImage.raycastTarget = false;
		_bandImage = bandImage;

		// メッセージ（帯の中央）
		GameObject textObject = new GameObject("Message", typeof(RectTransform));
		textObject.transform.SetParent(bandObject.transform, false);
		RectTransform textRect = (RectTransform)textObject.transform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;
		TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
		// 判定：フォントが指定されているか（無ければTMPの既定のフォント。日本語が出ないので警告する）
		if (_font != null) {
			text.font = _font;
		} else {
			Debug.LogWarning("EntryTrackingFlow: フォントが未指定です。日本語が表示されない場合は Font に日本語フォントを指定してください", this);
		}
		text.fontSize = _fontSize;
		text.color = _textColor;
		text.alignment = TextAlignmentOptions.Center;
		text.raycastTarget = false;
		_messageText = text;
	}

#if UNITY_EDITOR
	// コンポーネントを追加したとき、日本語フォント（NotoSerifJP-Bold SDF）をプロジェクトから探して入れる。
	private void Reset() {
		string[] guids = UnityEditor.AssetDatabase.FindAssets("NotoSerifJP-Bold SDF t:TMP_FontAsset");
		// 判定：見つかったか
		if (guids.Length > 0) {
			string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
			_font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
		}
	}
#endif
}
