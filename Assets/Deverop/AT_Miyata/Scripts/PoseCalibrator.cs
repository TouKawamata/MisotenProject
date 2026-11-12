using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 直立したときの基準姿勢（肩の高さ・胴の長さ）を測り、PoseCalibrationに保存するコンポーネント。
// Entryシーンに置き、プレイヤーがまっすぐ立っている間にStartCalibration()を呼ぶ想定。
// 測定時間（_duration秒）の間に受信したフレームの平均を基準姿勢にする。
// 次のどれかに当てはまると失敗にする（保存済みの値は変えない）。
//  - 肩・腰が見えているフレームが_minSampleCount未満（カメラに映っていない、途中で見えなくなった）
//  - 測定中に肩の高さが大きく動いた（_maxShoulderDeviationRatio、胴の長さに対する比率）
public class PoseCalibrator : MonoBehaviour {
	[Header("参照")]
	[Tooltip("姿勢データの取得元。未指定ならシーン内から自動検索する")]
	[SerializeField] private PoseLandmarkReceiver _receiver;

	[Header("測定")]
	[Tooltip("測定する秒数。この間に受信したフレームの平均を基準姿勢にする")]
	[SerializeField, Min(0.1f)] private float _duration = 1.5f;
	[Tooltip("成功に必要な、肩・腰が見えていたフレームの最小数")]
	[SerializeField, Min(1)] private int _minSampleCount = 15;
	[Tooltip("この信頼度を下回る関節は見えていないものとして扱う")]
	[SerializeField, Range(0f, 1f)] private float _minVisibility = 0.5f;
	[Tooltip("測定中の肩の高さのぶれ（標準偏差）の上限（胴の長さに対する比率）。超えたら動いていたとみなして失敗にする")]
	[SerializeField, Min(0f)] private float _maxShoulderDeviationRatio = 0.1f;
	[Tooltip("ONにすると、有効になったときに自動で測定を始める")]
	[SerializeField] private bool _startOnEnable = false;

	[Header("イベント（Inspectorから演出をつなぐ用）")]
	[Tooltip("測定が成功したとき")]
	[SerializeField] private UnityEvent _onSucceeded = new UnityEvent();
	[Tooltip("測定が失敗したとき")]
	[SerializeField] private UnityEvent _onFailed = new UnityEvent();

	// 測定中に集めた肩の中点の高さ
	private readonly List<float> _shoulderHeights = new List<float>();
	// 測定中に集めた胴の長さ
	private readonly List<float> _torsoLengths = new List<float>();
	// 測定を始めてからの経過時間
	private float _elapsed;

	// 測定中か
	public bool IsCalibrating { get; private set; }

	// 測定の進み具合（0～1）。測定していないときは0
	public float Progress => IsCalibrating ? Mathf.Clamp01(_elapsed / _duration) : 0f;

	// 測定する秒数
	public float Duration => _duration;

	// 測定を始めてからの経過秒数。測定していないときは0
	public float ElapsedTime => IsCalibrating ? Mathf.Min(_elapsed, _duration) : 0f;

	// 直近の測定が失敗した理由（成功・未測定ならnull）
	public string LastFailureReason { get; private set; }

	// 測定が終わったときに呼ばれる（引数は成功したか）
	public event Action<bool> Finished;

	private void Awake() {
		// 判定：Receiverが未設定か（未設定ならシーン内から自動検索する）
		if (_receiver == null) {
			_receiver = FindFirstObjectByType<PoseLandmarkReceiver>();
		}
	}

	private void OnEnable() {
		if (_receiver != null) {
			_receiver.OnPoseUpdated += OnPoseUpdated;
		}

		// 判定：有効になったときに自動で測定を始める設定か
		if (_startOnEnable) {
			StartCalibration();
		}
	}

	private void OnDisable() {
		if (_receiver != null) {
			_receiver.OnPoseUpdated -= OnPoseUpdated;
		}

		CancelCalibration();
	}

	// 測定を始める。測定中に呼ぶと最初からやり直す。
	public void StartCalibration() {
		// 判定：Receiverが無いか（無ければ測定できないので失敗にする）
		if (_receiver == null) {
			Debug.LogError("PoseCalibrator: PoseLandmarkReceiverが見つかりません", this);
			Finish(false, "PoseLandmarkReceiverが見つかりません");
			return;
		}

		_shoulderHeights.Clear();
		_torsoLengths.Clear();
		_elapsed = 0f;
		LastFailureReason = null;
		IsCalibrating = true;
	}

	// 測定を中止する（結果は保存せず、イベントも呼ばない）。
	public void CancelCalibration() {
		IsCalibrating = false;
		_elapsed = 0f;
	}

	private void Update() {
		// 判定：測定中か
		if (!IsCalibrating) {
			return;
		}

		// 時間で区切る（ゲームの一時停止の影響を受けないようunscaledDeltaTimeを使う）
		_elapsed += Time.unscaledDeltaTime;
		// 判定：測定時間が過ぎたか
		if (_elapsed >= _duration) {
			Complete();
		}
	}

	// 新しいフレームを受信するたびに、肩・腰が見えていれば記録する。
	private void OnPoseUpdated(PoseLandmark[] _) {
		// 判定：測定中か
		if (!IsCalibrating) {
			return;
		}

		PoseLandmark[] landmarks = _receiver.NormalizedLandmarks;
		// 判定：肩・腰の4点が見えているか（見えていないフレームは使わない）
		if (!IsVisible(landmarks, PoseLandmarkIndex.LeftShoulder) || !IsVisible(landmarks, PoseLandmarkIndex.RightShoulder)
			|| !IsVisible(landmarks, PoseLandmarkIndex.LeftHip) || !IsVisible(landmarks, PoseLandmarkIndex.RightHip)) {
			return;
		}

		Vector3 midShoulder = (landmarks[PoseLandmarkIndex.LeftShoulder].Position + landmarks[PoseLandmarkIndex.RightShoulder].Position) * 0.5f;
		Vector3 midHip = (landmarks[PoseLandmarkIndex.LeftHip].Position + landmarks[PoseLandmarkIndex.RightHip].Position) * 0.5f;
		// 2Dモードではzが無く、3Dモードのzは精度が低いため、画面上（x, y）の距離で胴の長さを測る
		float torsoLength = Vector2.Distance(midShoulder, midHip);

		_shoulderHeights.Add(midShoulder.y);
		_torsoLengths.Add(torsoLength);
	}

	// 測定時間が終わったら、集めた値を確認して平均を保存する。
	private void Complete() {
		IsCalibrating = false;

		// 判定：十分な数のフレームが集まったか
		if (_shoulderHeights.Count < _minSampleCount) {
			Finish(false, $"肩・腰が見えていたフレームが足りません（{_shoulderHeights.Count} / {_minSampleCount}）");
			return;
		}

		float shoulderHeight = Average(_shoulderHeights);
		float torsoLength = Average(_torsoLengths);
		// 判定：胴の長さが極端に小さい（検出が不安定）か
		if (torsoLength <= 0.0001f) {
			Finish(false, "胴の長さを測れませんでした");
			return;
		}

		// 判定：測定中に肩の高さが大きく動いていないか（しゃがんだり歩いたりしていたら失敗）
		float deviation = StandardDeviation(_shoulderHeights, shoulderHeight);
		if (deviation > torsoLength * _maxShoulderDeviationRatio) {
			Finish(false, $"測定中に体が動きました（肩の高さのぶれ {deviation / torsoLength:F2} > {_maxShoulderDeviationRatio:F2}）");
			return;
		}

		PoseCalibration.Set(shoulderHeight, torsoLength);
		Finish(true, null);
	}

	// 結果を記録し、イベントを呼ぶ。
	private void Finish(bool succeeded, string failureReason) {
		IsCalibrating = false;
		LastFailureReason = failureReason;

		// 判定：成功したか（成功・失敗でイベントとログを分ける）
		if (succeeded) {
			Debug.Log($"PoseCalibrator: キャリブレーション成功（肩の高さ {PoseCalibration.ShoulderHeight:F3}、胴の長さ {PoseCalibration.TorsoLength:F3}）", this);
			_onSucceeded.Invoke();
		} else {
			Debug.LogWarning($"PoseCalibrator: キャリブレーション失敗：{failureReason}", this);
			_onFailed.Invoke();
		}

		Finished?.Invoke(succeeded);
	}

	// 指定した関節が、設定した信頼度以上で見えているか。
	private bool IsVisible(PoseLandmark[] landmarks, int index) {
		return landmarks != null && index < landmarks.Length && landmarks[index].Visibility >= _minVisibility;
	}

	private static float Average(List<float> values) {
		float sum = 0f;
		foreach (float value in values) {
			sum += value;
		}

		return sum / values.Count;
	}

	private static float StandardDeviation(List<float> values, float average) {
		float sum = 0f;
		foreach (float value in values) {
			float diff = value - average;
			sum += diff * diff;
		}

		return Mathf.Sqrt(sum / values.Count);
	}
}
