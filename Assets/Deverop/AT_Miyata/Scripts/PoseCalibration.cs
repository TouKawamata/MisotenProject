using System;

// トラッキングのキャリブレーション結果（直立したときの基準姿勢）。
// Entryシーンで取った値をレースのシーンでも使うため、staticに持つ（シーンを読み込み直しても消えない）。
// 値はPoseLandmarkReceiver.NormalizedLandmarks（姿勢空間）の単位。
// 値を入れるのはPoseCalibrator、読むのはTrackingRaceInput（しゃがみの判定）。
public static class PoseCalibration {
	// キャリブレーション済みか
	public static bool HasValue { get; private set; }

	// 直立時の肩の中点の高さ（y）
	public static float ShoulderHeight { get; private set; }

	// 直立時の胴の長さ（肩の中点〜腰の中点の距離）。しゃがみの量をこの長さに対する比率で扱う
	public static float TorsoLength { get; private set; }

	// 値が変わったとき（設定・消去）に呼ばれる
	public static event Action Changed;

	// 基準姿勢を設定する。
	public static void Set(float shoulderHeight, float torsoLength) {
		ShoulderHeight = shoulderHeight;
		TorsoLength = torsoLength;
		HasValue = true;
		Changed?.Invoke();
	}

	// 基準姿勢を消す（プレイヤーが交代したときなど）。
	public static void Clear() {
		HasValue = false;
		ShoulderHeight = 0f;
		TorsoLength = 0f;
		Changed?.Invoke();
	}
}
