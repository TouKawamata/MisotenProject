using UnityEngine;

// RaceInputProvider（AT_Kawamata作成）に対して、起動時にどのIRaceInputを使うか設定するコンポーネント。
// RaceInputProvider本体への変更を最小限にするため、切り替えロジックはこちら側にまとめている。
// RaceInputProviderと同じGameObjectにアタッチして使用する想定。
public class TrackingRaceInputBootstrapper : MonoBehaviour {
	// 入力元を設定する対象のRaceInputProvider。未指定なら同じGameObjectから自動取得する。
	[SerializeField] private RaceInputProvider _raceInputProvider;
	// 姿勢データの取得元。未指定ならシーン内から自動検索する。
	[SerializeField] private PoseLandmarkReceiver _poseLandmarkReceiver;
	[Tooltip("判定の調整値のアセット。指定すると下の Tracking Settings より優先する（PoseTest と同じアセットを使うと、調整した値がそのまま反映される）")]
	[SerializeField] private TrackingRaceInputSettingsAsset _settingsAsset;
	// トラッキング入力の判定に使う調整用パラメータ（アセットが未指定のときだけ使う）。
	[SerializeField] private TrackingRaceInputSettings _trackingSettings = new TrackingRaceInputSettings();

	[Header("キーボード")]
	[Tooltip("ONにすると、トラッキングと一緒にキーボードでも操作できる（カメラが無い環境でのテスト用。ボタンはどちらか一方で押した扱い）")]
	[SerializeField] private bool _allowKeyboard = true;

	[Header("デバッグ")]
	[Tooltip("ONにするとトラッキングを使わず、キーボード入力だけを使う（デバッグ用）")]
	// ONの場合、トラッキングが使える状態でもキーボード入力だけを使う（動作確認用のフラグ）。
	[SerializeField] private bool _useKeyboardForDebug = false;

	// シーン開始時に、使用する入力元（キーボード／トラッキング）を決定してRaceInputProviderへ設定する。
	private void Awake() {
		// 判定：RaceInputProviderの参照が未設定か（未設定なら同じGameObjectから取得を試みる）
		if (_raceInputProvider == null) {
			_raceInputProvider = GetComponent<RaceInputProvider>();
		}

		// 判定：RaceInputProviderがそれでも見つからないか（無ければ以降の処理は行えないため中断）
		if (_raceInputProvider == null) {
			Debug.LogError("TrackingRaceInputBootstrapper: RaceInputProviderが見つかりません", this);
			return;
		}

		// 判定：PoseLandmarkReceiverの参照が未設定か（未設定ならシーン内から自動検索する）
		if (_poseLandmarkReceiver == null) {
			_poseLandmarkReceiver = FindFirstObjectByType<PoseLandmarkReceiver>();
		}

		// これから設定する入力元（トラッキング or キーボード）
		IRaceInput input;
		// 判定：デバッグ用にキーボードを使う設定になっているか（ONなら常にキーボード入力を使う）
		if (_useKeyboardForDebug) {
			input = new KeyboardRaceInput();
		}
		// 判定：PoseLandmarkReceiverが見つかっているか（見つかっていればトラッキング入力を使う）
		else if (_poseLandmarkReceiver != null) {
			TrackingRaceInputSettings settings = TrackingRaceInputSettingsAsset.Resolve(_settingsAsset, _trackingSettings);
			IRaceInput tracking = new TrackingRaceInput(_poseLandmarkReceiver, settings);
			// 判定：キーボードとの併用を許可しているか（許可していればトラッキング＋キーボード）
			if (_allowKeyboard) {
				input = new CombinedRaceInput(tracking, new KeyboardRaceInput());
			} else {
				input = tracking;
			}
		}
		// どちらにも当てはまらない場合（トラッキング機材／Python側が未起動などでReceiverが見つからない）
		else {
			Debug.LogWarning("TrackingRaceInputBootstrapper: PoseLandmarkReceiverが見つからないため、キーボード入力にフォールバックします", this);
			input = new KeyboardRaceInput();
		}

		// RaceInputProvider.Initialize()はGameLifetimeScope（DefaultExecutionOrder(-5000)）のAwake内で
		// RaceManager→PlayerManager経由で先に呼ばれ、CurrentにはKeyboardRaceInputが入っている。
		// ここでそれをトラッキング入力（またはキーボード）に差し替える。
		// PlayerManagerは毎フレームCurrentを参照し直すため、レース開始前に差し替えれば反映される。
		_raceInputProvider.SetInputSource(input);
	}
}
