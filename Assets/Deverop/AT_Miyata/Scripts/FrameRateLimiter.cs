using UnityEngine;

// ゲームのフレームレートの上限を決める（シーンに置かなくても、起動時に自動で設定される）。
// 上限が無いと Unity が描画を繰り返して GPU を使い切り、同じPCで動くカメラの姿勢推定（pose_sender.py の YOLO を GPU で推論）が遅くなる。
// 例：Unity 再生中は YOLO が 22ms（姿勢の更新は 38fps）、GPU が空いていれば約13ms（60fps に間に合う）。2026-11-26 に計測。
// 注意：QualitySettings の V Sync Count が 0 以外だと targetFrameRate は無視される（今は全品質レベルで 0）。
public static class FrameRateLimiter {
	// フレームレートの上限（カメラの 60fps に合わせる）
	public const int TargetFrameRate = 60;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Apply() {
		Application.targetFrameRate = TargetFrameRate;
	}
}
