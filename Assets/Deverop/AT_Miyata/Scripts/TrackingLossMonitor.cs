using System;
using UnityEngine;
using UnityEngine.Events;

// トラッキングの見失いを見張った結果の状態。
public enum TrackingLossState {
	Ok,           // 検出できている（途切れていても_warningDelay未満ならここ）
	PersonLost,   // Pythonとはつながっているが、人物をしばらく検出できていない
	Disconnected, // Pythonから何も届かない（Python・カメラの停止、ネットワークの断線）
}

// トラッキングの検出がしばらくできないことを見張るコンポーネント。
// PoseLandmarkReceiver.IsTracking（0.3秒で切れる入力用の判定）とは別に、警告を出すかどうかを決める。
// 表示はしない（TrackingLossWarningViewなどが State・StateChanged を見て表示する）。
// 時間は Time.unscaledTime で数えるので、ヒットストップなどで timeScale が変わってもずれない。
public class TrackingLossMonitor : MonoBehaviour {
	[Tooltip("見張る PoseLandmarkReceiver。未指定ならシーン内から探す")]
	[SerializeField] private PoseLandmarkReceiver _poseLandmarkReceiver;

	[Header("時間（秒）")]
	[Tooltip("検出できない時間がこの秒数を過ぎたら警告する")]
	[SerializeField, Min(0f)] private float _warningDelay = 2f;
	[Tooltip("警告中に検出がこの秒数続いたら警告を消す（一瞬映っただけで点滅しないように）")]
	[SerializeField, Min(0f)] private float _recoverTime = 0.5f;
	[Tooltip("起動してから一度もPythonから受信していないとき、この秒数を過ぎたら警告する（起動直後の猶予）")]
	[SerializeField, Min(0f)] private float _startupGrace = 3f;

	[Header("イベント")]
	[Tooltip("警告が始まったとき（Ok → PersonLost / Disconnected）")]
	[SerializeField] private UnityEvent _onWarningStarted = new UnityEvent();
	[Tooltip("警告が終わったとき（PersonLost / Disconnected → Ok）")]
	[SerializeField] private UnityEvent _onWarningEnded = new UnityEvent();

	// 検出できなくなった時刻（Time.unscaledTime基準）。検出できているときは負の値
	private float _lostStartTime = -1f;
	// 警告中に検出が戻った時刻。戻っていないときは負の値
	private float _recoverStartTime = -1f;
	// Receiverが見つからない警告を出したか（1回だけ出す）
	private bool _missingReceiverLogged;

	// 現在の状態
	public TrackingLossState State { get; private set; } = TrackingLossState.Ok;

	// 警告中か（PersonLost か Disconnected）
	public bool IsWarning => State != TrackingLossState.Ok;

	// 検出できていない秒数（検出できているときは0）
	public float LostDuration => _lostStartTime < 0f ? 0f : Time.unscaledTime - _lostStartTime;

	// 状態が変わったときに呼ばれる（引数は新しい状態）。PersonLost ⇔ Disconnected の切り替わりでも呼ばれる。
	public event Action<TrackingLossState> StateChanged;

	private void Awake() {
		// 判定：Receiverが未設定か（未設定ならシーン内から探す）
		if (_poseLandmarkReceiver == null) {
			_poseLandmarkReceiver = FindFirstObjectByType<PoseLandmarkReceiver>();
		}
	}

	private void OnEnable() {
		// 起動時（有効にしたとき）は、まだ検出できていない扱いで数え始める
		_lostStartTime = Time.unscaledTime;
		_recoverStartTime = -1f;
	}

	private void Update() {
		float now = Time.unscaledTime;

		// 判定：Receiverが無いか（無ければ見張れないので、猶予の後に接続なしとして警告する）
		if (_poseLandmarkReceiver == null) {
			// 判定：まだ警告を出していないか
			if (!_missingReceiverLogged) {
				Debug.LogWarning("TrackingLossMonitor: PoseLandmarkReceiverが見つからないため、接続なしとして扱います", this);
				_missingReceiverLogged = true;
			}

			// 判定：起動直後の猶予を過ぎたか
			if (now - _lostStartTime >= _startupGrace) {
				SetState(TrackingLossState.Disconnected);
			}
			return;
		}

		bool detecting = _poseLandmarkReceiver.IsTracking;
		bool connected = _poseLandmarkReceiver.IsConnected;

		// 判定：警告していないか
		if (State == TrackingLossState.Ok) {
			// 判定：検出できているか（できていれば数え直す）
			if (detecting) {
				_lostStartTime = -1f;
				return;
			}

			// 判定：検出できなくなった瞬間か（ここから数え始める）
			if (_lostStartTime < 0f) {
				_lostStartTime = now;
			}

			// 一度もPythonから受信していないときは起動直後の猶予、それ以外は_warningDelayで警告する
			float limit = _poseLandmarkReceiver.HasConnected ? _warningDelay : Mathf.Max(_warningDelay, _startupGrace);
			// 判定：検出できない時間が限度を過ぎたか
			if (now - _lostStartTime >= limit) {
				SetState(connected ? TrackingLossState.PersonLost : TrackingLossState.Disconnected);
			}
			return;
		}

		// ここからは警告中
		// 判定：検出が戻っているか（_recoverTime続いたら警告を消す）
		if (detecting) {
			// 判定：戻った瞬間か（ここから数え始める）
			if (_recoverStartTime < 0f) {
				_recoverStartTime = now;
			}

			// 判定：戻ってから_recoverTimeたったか
			if (now - _recoverStartTime >= _recoverTime) {
				_lostStartTime = -1f;
				_recoverStartTime = -1f;
				SetState(TrackingLossState.Ok);
			}
			return;
		}

		// 検出できないまま。原因（人物がいない／接続なし）が変わったら状態を切り替える
		_recoverStartTime = -1f;
		SetState(connected ? TrackingLossState.PersonLost : TrackingLossState.Disconnected);
	}

	// 状態を変え、変わったときだけイベントを呼ぶ。
	private void SetState(TrackingLossState next) {
		// 判定：状態が変わらないか（変わらなければ何もしない）
		if (next == State) {
			return;
		}

		bool wasWarning = IsWarning;
		State = next;
		StateChanged?.Invoke(next);

		// 判定：警告が始まったか／終わったか
		if (!wasWarning && IsWarning) {
			_onWarningStarted.Invoke();
		} else if (wasWarning && !IsWarning) {
			_onWarningEnded.Invoke();
		}
	}
}
