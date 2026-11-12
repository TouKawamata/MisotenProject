using System;
using UnityEngine;
using UnityEngine.Serialization;
using uOSC;

// 1つのランドマーク（関節）の位置と信頼度。
public struct PoseLandmark {
	public Vector3 Position; // 座標（Landmarksはワールド座標、NormalizedLandmarksは正規化した姿勢空間の座標）
	public float Visibility; // 信頼度 (0.0 ~ 1.0)。受信していない関節は0
}

// ランドマーク配列のインデックス（MediaPipe Pose準拠の33点の並び）。
// 2Dモード（YOLO-Pose、17点）で受信した場合も、この並びに詰め替えて格納する（対応の無い関節は信頼度0）。
public static class PoseLandmarkIndex {
	public const int Count = 33; // 配列の長さ
	public const int Nose = 0; // 鼻
	public const int LeftEye = 2; // 左目
	public const int RightEye = 5; // 右目
	public const int LeftEar = 7; // 左耳
	public const int RightEar = 8; // 右耳
	public const int LeftShoulder = 11; // 左肩
	public const int RightShoulder = 12; // 右肩
	public const int LeftElbow = 13; // 左肘
	public const int RightElbow = 14; // 右肘
	public const int LeftWrist = 15; // 左手首
	public const int RightWrist = 16; // 右手首
	public const int LeftHip = 23; // 左腰
	public const int RightHip = 24; // 右腰
	public const int LeftKnee = 25; // 左膝
	public const int RightKnee = 26; // 右膝
	public const int LeftAnkle = 27; // 左足首
	public const int RightAnkle = 28; // 右足首
}

// Python（pose_sender.py）からOSCで届く姿勢データを受信する。
// 受け取るメッセージ
//  - /pose/landmarks3d : 3Dモード。MediaPipeの33点 × (x, y, z, visibility)
//  - /pose/landmarks   : 2Dモード。YOLO-Pose（COCO順）の17点 × (x, y, confidence)
//  - /pose/frame_size  : 回転補正後の映像サイズ [幅, 高さ]（int）。毎フレーム届く
// x, yは映像の幅・高さでそれぞれ0～1に正規化されて届くため、縦横比（frame_size）を掛けて縦横の比率をそろえる。
// 人物が映っていないフレームではランドマークが送られてこないため、最後の受信から一定時間たつとIsTrackingがfalseになる。
// 何のメッセージも届かなくなると（Python・カメラの停止など）、IsConnectedがfalseになる。
[RequireComponent(typeof(uOscServer))]
public class PoseLandmarkReceiver : MonoBehaviour {
	// YOLO-Pose（COCO順の17点）の各番号を、MediaPipe Poseの33点の並びに対応させる表
	private static readonly int[] CocoToMediaPipe = {
		PoseLandmarkIndex.Nose,
		PoseLandmarkIndex.LeftEye,
		PoseLandmarkIndex.RightEye,
		PoseLandmarkIndex.LeftEar,
		PoseLandmarkIndex.RightEar,
		PoseLandmarkIndex.LeftShoulder,
		PoseLandmarkIndex.RightShoulder,
		PoseLandmarkIndex.LeftElbow,
		PoseLandmarkIndex.RightElbow,
		PoseLandmarkIndex.LeftWrist,
		PoseLandmarkIndex.RightWrist,
		PoseLandmarkIndex.LeftHip,
		PoseLandmarkIndex.RightHip,
		PoseLandmarkIndex.LeftKnee,
		PoseLandmarkIndex.RightKnee,
		PoseLandmarkIndex.LeftAnkle,
		PoseLandmarkIndex.RightAnkle,
	};

	[Header("OSC Settings")]
	[Tooltip("3Dモード（MediaPipe、33点 × x,y,z,vis）のアドレス")]
	[FormerlySerializedAs("targetAddress")]
	[SerializeField] private string _address3d = "/pose/landmarks3d";
	[Tooltip("2Dモード（YOLO-Pose、17点 × x,y,conf）のアドレス")]
	[SerializeField] private string _address2d = "/pose/landmarks";
	[Tooltip("映像サイズ [幅, 高さ] のアドレス")]
	[SerializeField] private string _frameSizeAddress = "/pose/frame_size";

	[Header("Coordinate Mapping")]
	[Tooltip("Unity空間内でのスケール（Landmarks（ワールド座標）にだけ使う。判定に使うNormalizedLandmarksには影響しない）")]
	[FormerlySerializedAs("scale")]
	[SerializeField] private Vector3 _scale = new Vector3(2.0f, 2.0f, 2.0f);
	[Tooltip("鏡像反転（鏡のように同期させたい場合はON）")]
	[FormerlySerializedAs("mirrorX")]
	[SerializeField] private bool _mirrorX = true;
	[Tooltip("frame_sizeを受信するまでに使う縦横比（幅÷高さ）")]
	[SerializeField, Min(0.1f)] private float _defaultAspect = 640f / 480f;

	[Header("Tracking")]
	[Tooltip("最後にランドマークを受信してからこの秒数を過ぎると、トラッキングが途切れたとみなす（IsTrackingがfalseになる）")]
	[SerializeField, Min(0.01f)] private float _lostTimeout = 0.3f;
	[Tooltip("Pythonから何も届かない時間がこの秒数を過ぎると、接続が切れたとみなす（IsConnectedがfalseになる）。frame_sizeは人物がいなくても毎フレーム届く")]
	[SerializeField, Min(0.01f)] private float _disconnectTimeout = 1f;

	private uOscServer _server;
	// 現在の映像の縦横比（幅÷高さ）
	private float _aspect;
	// 最後にランドマークを受信した時刻（Time.unscaledTime基準）。未受信なら負の値
	private float _lastReceivedTime = -1f;
	// 最後に何かのメッセージ（frame_size・ランドマーク）を受信した時刻（Time.unscaledTime基準）。未受信なら負の値
	private float _lastMessageTime = -1f;

	// 外部から参照できる33個のランドマーク（ワールド座標。表示用）
	public PoseLandmark[] Landmarks { get; private set; } = new PoseLandmark[PoseLandmarkIndex.Count];

	// 判定用の33個のランドマーク（正規化した姿勢空間の座標）。
	// 映像の中心が原点、右が+x・上が+y（鏡像反転の設定を反映済み）。yは映像の高さを1とし、xも同じ単位（縦横比を反映）。
	// このオブジェクトのTransformやscaleの影響を受けないので、シーンをまたいで値を比べられる（キャリブレーションに使う）。
	public PoseLandmark[] NormalizedLandmarks { get; private set; } = new PoseLandmark[PoseLandmarkIndex.Count];

	// 最後に受信したデータが2Dモードのものか（2Dモードではz・一部の関節が無い）
	public bool Is2DMode { get; private set; }

	// 現在の映像の縦横比（幅÷高さ）
	public float Aspect => _aspect;

	// 一度でもランドマークを受信したか
	public bool HasReceived => _lastReceivedTime >= 0f;

	// トラッキング中か（最後の受信から_lostTimeout秒以内）。人物が映らなくなるとfalseになる。
	public bool IsTracking => HasReceived && Time.unscaledTime - _lastReceivedTime <= _lostTimeout;

	// 一度でもPythonから何かのメッセージを受信したか
	public bool HasConnected => _lastMessageTime >= 0f;

	// Pythonとつながっているか（最後のメッセージから_disconnectTimeout秒以内）。
	// 人物が映っていなくてもframe_sizeは届くので、falseならPython・カメラが止まっているか、ネットワークが切れている。
	public bool IsConnected => HasConnected && Time.unscaledTime - _lastMessageTime <= _disconnectTimeout;

	// 新しいフレームを受信したことを知らせるイベント（2D・3Dどちらでも呼ばれる。引数はワールド座標のLandmarks）
	public event Action<PoseLandmark[]> OnPoseUpdated;

	void Awake() {
		_server = GetComponent<uOscServer>();
		_aspect = _defaultAspect;
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
		// 判定：どのメッセージか（アドレスで振り分ける）
		if (message.address == _frameSizeAddress) {
			_lastMessageTime = Time.unscaledTime;
			ReceiveFrameSize(message);
		} else if (message.address == _address3d) {
			_lastMessageTime = Time.unscaledTime;
			Receive3D(message);
		} else if (message.address == _address2d) {
			_lastMessageTime = Time.unscaledTime;
			Receive2D(message);
		}
	}

	// /pose/frame_size：[幅, 高さ]から縦横比を更新する。
	private void ReceiveFrameSize(Message message) {
		// 判定：幅・高さの2つがそろっているか
		if (message.values.Length < 2) {
			return;
		}

		float width = ToFloat(message.values[0]);
		float height = ToFloat(message.values[1]);
		// 判定：高さが0以下（不正な値）でないか
		if (width > 0f && height > 0f) {
			_aspect = width / height;
		}
	}

	// /pose/landmarks3d：33点 × (x, y, z, visibility)
	private void Receive3D(Message message) {
		int landmarkCount = Mathf.Min(message.values.Length / 4, PoseLandmarkIndex.Count);

		for (int i = 0; i < landmarkCount; i++) {
			int baseIdx = i * 4;
			float rawX = ToFloat(message.values[baseIdx]);
			float rawY = ToFloat(message.values[baseIdx + 1]);
			float rawZ = ToFloat(message.values[baseIdx + 2]);
			float vis = ToFloat(message.values[baseIdx + 3]);
			SetLandmark(i, rawX, rawY, rawZ, vis);
		}

		// 届かなかった関節は未検出扱いにする
		for (int i = landmarkCount; i < PoseLandmarkIndex.Count; i++) {
			ClearLandmark(i);
		}

		Is2DMode = false;
		NotifyUpdated();
	}

	// /pose/landmarks：COCO順の17点 × (x, y, confidence)。MediaPipeの並びに詰め替える。
	private void Receive2D(Message message) {
		// 対応の無い関節（指先など）を未検出にするため、先に全部クリアする
		for (int i = 0; i < PoseLandmarkIndex.Count; i++) {
			ClearLandmark(i);
		}

		int landmarkCount = Mathf.Min(message.values.Length / 3, CocoToMediaPipe.Length);
		for (int i = 0; i < landmarkCount; i++) {
			int baseIdx = i * 3;
			float rawX = ToFloat(message.values[baseIdx]);
			float rawY = ToFloat(message.values[baseIdx + 1]);
			float conf = ToFloat(message.values[baseIdx + 2]);
			SetLandmark(CocoToMediaPipe[i], rawX, rawY, 0f, conf);
		}

		Is2DMode = true;
		NotifyUpdated();
	}

	// 0～1に正規化された座標を、姿勢空間（NormalizedLandmarks）とワールド座標（Landmarks）に変換して格納する。
	private void SetLandmark(int index, float rawX, float rawY, float rawZ, float visibility) {
		// 中心を0に補正し、xに縦横比を掛けて縦と同じ単位にそろえる（y方向は映像の高さが1）
		float posX = (rawX - 0.5f) * _aspect;
		// 判定：鏡像反転するか
		if (_mirrorX) {
			posX = -posX;
		}

		// 映像のyは下向きが正なので反転して上を正にする
		float posY = -(rawY - 0.5f);
		// MediaPipeのzは手前が負なので反転して手前を正にする（2Dモードでは0）
		float posZ = -rawZ;

		Vector3 normalized = new Vector3(posX, posY, posZ);
		NormalizedLandmarks[index].Position = normalized;
		NormalizedLandmarks[index].Visibility = visibility;

		Vector3 localPos = Vector3.Scale(normalized, _scale);
		Landmarks[index].Position = transform.TransformPoint(localPos);
		Landmarks[index].Visibility = visibility;
	}

	// 指定した関節を未検出（信頼度0）にする。位置は前の値のまま残す。
	private void ClearLandmark(int index) {
		NormalizedLandmarks[index].Visibility = 0f;
		Landmarks[index].Visibility = 0f;
	}

	// 受信時刻を記録し、購読先（Visualizer・キャリブレーションなど）へ通知する。
	private void NotifyUpdated() {
		_lastReceivedTime = Time.unscaledTime;
		OnPoseUpdated?.Invoke(Landmarks);
	}

	// uOSCの値はobject型（float・intなど）で届くため、型によらずfloatに変換する。
	private static float ToFloat(object value) {
		return Convert.ToSingle(value);
	}
}
