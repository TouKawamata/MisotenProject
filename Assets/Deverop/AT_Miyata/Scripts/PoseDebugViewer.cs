using UnityEngine;

public class PoseDebugViewer : MonoBehaviour {
	[SerializeField] private PoseLandmarkReceiver receiver;

	private int _frameCount = 0;
	private float _fps = 0f;
	private float _lastFpsUpdateTime = 0f;
	private int _receivedMessageCount = 0;
	private PoseLandmark[] _latestLandmarks;
	private string _lastReceivedTimeStr = "None";

	void Awake() {
		if (receiver == null) {
			receiver = GetComponent<PoseLandmarkReceiver>();
		}
	}

	void OnEnable() {
		if (receiver != null) {
			receiver.OnPoseUpdated += OnPoseUpdated;
		}
	}

	void OnDisable() {
		if (receiver != null) {
			receiver.OnPoseUpdated -= OnPoseUpdated;
		}
	}

	private void OnPoseUpdated(PoseLandmark[] landmarks) {
		_receivedMessageCount++;
		_frameCount++;
		_latestLandmarks = landmarks;
		_lastReceivedTimeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");

		// 最初の1回だけコンソールにログを出す（受信確認）
		if (_receivedMessageCount == 1) {
			Debug.Log("<color=green>[OSC Success]</color> Pythonからのデータ受信を確認しました！");
		}
	}

	void Update() {
		// 1秒ごとに受信FPSを計算
		if (Time.time - _lastFpsUpdateTime >= 1.0f) {
			_fps = _frameCount / (Time.time - _lastFpsUpdateTime);
			_frameCount = 0;
			_lastFpsUpdateTime = Time.time;
		}
	}

	void OnGUI() {
		// 画面左上に受信状況を表示
		GUI.Box(new Rect(10, 10, 320, 160), "OSC Pose Receiver Debug");

		if (_receivedMessageCount == 0) {
			GUI.color = Color.red;
			GUI.Label(new Rect(20, 35, 300, 20), "Status: 待機中 (データを受信していません)");
			GUI.color = Color.white;
			GUI.Label(new Rect(20, 55, 300, 40), "Pythonスクリプトを実行し、\n人物がカメラに映っているか確認してください。");
			return;
		}

		GUI.color = Color.green;
		GUI.Label(new Rect(20, 35, 300, 20), $"Status: 受信中 (FPS: {_fps:F1})");
		GUI.color = Color.white;

		GUI.Label(new Rect(20, 55, 300, 20), $"累計受信回数: {_receivedMessageCount}");
		GUI.Label(new Rect(20, 75, 300, 20), $"最新更新時刻: {_lastReceivedTimeStr}");

		if (_latestLandmarks != null && _latestLandmarks.Length > 16) {
			// 鼻 (0), 左手首 (15), 右手首 (16) の位置を表示
			Vector3 nose = _latestLandmarks[0].Position;
			Vector3 lWrist = _latestLandmarks[15].Position;
			Vector3 rWrist = _latestLandmarks[16].Position;

			GUI.Label(new Rect(20, 100, 300, 20), $"鼻 (0): {nose:F2}");
			GUI.Label(new Rect(20, 120, 300, 20), $"左手 (15): {lWrist:F2} (Vis: {_latestLandmarks[15].Visibility:F2})");
			GUI.Label(new Rect(20, 140, 300, 20), $"右手 (16): {rWrist:F2} (Vis: {_latestLandmarks[16].Visibility:F2})");
		}
	}
}