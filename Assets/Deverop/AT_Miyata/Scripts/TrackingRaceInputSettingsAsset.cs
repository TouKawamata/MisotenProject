using UnityEngine;

// TrackingRaceInputSettings（判定の調整値）をアセットとして保存するためのScriptableObject。
// Bootstrapper（GameScene など）・DebugViewer（PoseTest）・EntryTrackingFlow（Entry）が同じアセットを参照することで、
// PoseTest で調整した値がそのままレースにも反映されるようにする。
// 再生中に Inspector で値を変えると、アセットなので再生を止めても値が残る（調整した値を残すため）。
[CreateAssetMenu(fileName = "TrackingRaceInputSettings", menuName = "Tracking/Race Input Settings")]
public class TrackingRaceInputSettingsAsset : ScriptableObject {
	[Tooltip("トラッキング入力の判定に使う調整用パラメータ")]
	[SerializeField] private TrackingRaceInputSettings _settings = new TrackingRaceInputSettings();

	// 判定に使う調整値。参照を渡すので、再生中に変えた値はすぐ判定に反映される
	public TrackingRaceInputSettings Settings => _settings;

	// アセットが指定されていればその値、無ければ fallback を返す（各コンポーネントで共通の選び方）。
	public static TrackingRaceInputSettings Resolve(TrackingRaceInputSettingsAsset asset, TrackingRaceInputSettings fallback) {
		// 判定：アセットが指定されているか（指定されていればアセットの値を優先する）
		if (asset != null) {
			return asset.Settings;
		}

		return fallback;
	}
}
