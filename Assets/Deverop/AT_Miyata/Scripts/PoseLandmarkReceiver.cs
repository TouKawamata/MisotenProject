using UnityEngine;
using uOSC;

public struct PoseLandmark {
	public Vector3 Position; // Unity空間座標
	public float Visibility; // 信頼度 (0.0 ~ 1.0)
}

[RequireComponent(typeof(uOscServer))]
public class PoseLandmarkReceiver : MonoBehaviour {
	[Header("OSC Settings")]
	[SerializeField] private string targetAddress = "/pose/landmarks3d";

	[Header("Coordinate Mapping")]
	[Tooltip("Unity空間内でのスケール")]
	[SerializeField] private Vector3 scale = new Vector3(2.0f, 2.0f, 2.0f);
	[Tooltip("鏡像反転（鏡のように同期させたい場合はON）")]
	[SerializeField] private bool mirrorX = true;

	private uOscServer _server;

	// 外部から参照できる33個のランドマークデータ
	public PoseLandmark[] Landmarks { get; private set; } = new PoseLandmark[33];

	// 新しいフレームを受信したことを知らせるイベント
	public event System.Action<PoseLandmark[]> OnPoseUpdated;

	void Awake() {
		_server = GetComponent<uOscServer>();
	}

	void OnEnable() {
		if (_server != null) {
			_server.onDataReceived.AddListener(OnDataReceived);
		}
	}

	void OnDisable() {
		if (_server != null) {
			_server.onDataReceived.RemoveListener(OnDataReceived);
		}
	}

	private void OnDataReceived(Message message) {
		// アドレスの一致確認
		if (message.address != targetAddress) return;

		// 33点 × 4要素 (x, y, z, vis) = 132要素あるかチェック
		int elementCount = message.values.Length;
		int landmarkCount = Mathf.Min(elementCount / 4, 33);

		for (int i = 0; i < landmarkCount; i++) {
			int baseIdx = i * 4;

			// uOSC の値は object 型で届くため float へキャスト
			float rawX = (float)message.values[baseIdx];
			float rawY = (float)message.values[baseIdx + 1];
			float rawZ = (float)message.values[baseIdx + 2];
			float vis = (float)message.values[baseIdx + 3];

			// 座標変換 (中心を0に補正、Y反転、鏡像反転)
			float posX = rawX - 0.5f;
			if (mirrorX) posX = -posX;

			float posY = -(rawY - 0.5f);
			float posZ = -rawZ;

			Vector3 localPos = new Vector3(posX * scale.x, posY * scale.y, posZ * scale.z);

			Landmarks[i].Position = transform.TransformPoint(localPos);
			Landmarks[i].Visibility = vis;
		}

		// 購読先（Visualizerなど）へ通知
		OnPoseUpdated?.Invoke(Landmarks);
	}
}