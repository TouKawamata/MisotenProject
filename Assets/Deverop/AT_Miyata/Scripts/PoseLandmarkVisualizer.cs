using UnityEngine;

public class PoseLandmarkVisualizer : MonoBehaviour {
	[Header("References")]
	[SerializeField] private PoseLandmarkReceiver receiver;

	[Header("Visual Settings")]
	[Tooltip("マーカー用のプレハブ（未指定の場合は球体を自動生成）")]
	[SerializeField] private GameObject markerPrefab;
	[SerializeField] private float markerScale = 0.05f;
	[Tooltip("表示する最小信頼度")]
	[SerializeField] private float minVisibility = 0.5f;

	private readonly Transform[] _markerTransforms = new Transform[33];

	// Startより前のAwakeで生成を完了させることで、受信イベントとの競合を防ぐ
	void Awake() {
		if (receiver == null) {
			receiver = GetComponent<PoseLandmarkReceiver>();
		}

		// 33個のマーカーオブジェクトを生成
		for (int i = 0; i < 33; i++) {
			GameObject marker;
			if (markerPrefab != null) {
				marker = Instantiate(markerPrefab, transform);
			} else {
				marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				marker.transform.localScale = Vector3.one * markerScale;
				marker.transform.SetParent(transform);

				// 物理演算不要ならColliderを破棄
				Collider col = marker.GetComponent<Collider>();
				if (col != null) Destroy(col);
			}

			marker.name = $"Marker_{i}";
			_markerTransforms[i] = marker.transform;

			// 初回受信までは非アクティブにしておく
			marker.SetActive(false);
		}
	}

	void OnEnable() {
		if (receiver != null) {
			receiver.OnPoseUpdated += UpdateMarkers;
		}
	}

	void OnDisable() {
		if (receiver != null) {
			receiver.OnPoseUpdated -= UpdateMarkers;
		}
	}

	private void UpdateMarkers(PoseLandmark[] landmarks) {
		for (int i = 0; i < landmarks.Length; i++) {
			if (_markerTransforms[i] == null) continue;

			// 信頼度が閾値未満なら非表示
			bool isVisible = landmarks[i].Visibility >= minVisibility;
			_markerTransforms[i].gameObject.SetActive(isVisible);

			// 座標更新
			if (isVisible) {
				_markerTransforms[i].position = landmarks[i].Position;
			}
		}
	}
}