using UnityEngine;

// 2つの入力元（トラッキングとキーボードなど）を合わせて1つの IRaceInput にする。
// カメラが無い環境でもキーボードで操作できるように、GameScene などではトラッキングとキーボードを併用する。
//  - Move：大きさが大きい方を使う（両方を足すと、斜めの向きが混ざってしまうため）。
//  - ボタン：どちらか一方でも押されていれば押した扱いにする。
public class CombinedRaceInput : IRaceInput {
	// 主に使う入力元（トラッキング）
	private readonly IRaceInput _primary;
	// 補助の入力元（キーボード）
	private readonly IRaceInput _secondary;

	public CombinedRaceInput(IRaceInput primary, IRaceInput secondary) {
		_primary = primary;
		_secondary = secondary;
	}

	// 大きさが大きい方の入力を使う。どちらも大きさは1以下なので、制限し直す必要はない。
	public Vector2 Move {
		get {
			Vector2 primary = _primary.Move;
			Vector2 secondary = _secondary.Move;
			// 判定：補助の入力の方が大きいか（キーボードを押していればキーボードが優先される）
			if (secondary.sqrMagnitude > primary.sqrMagnitude) {
				return secondary;
			}

			return primary;
		}
	}

	public bool BoostPressed => _primary.BoostPressed || _secondary.BoostPressed;

	public bool BoostHeld => _primary.BoostHeld || _secondary.BoostHeld;

	public bool ShieldPressed => _primary.ShieldPressed || _secondary.ShieldPressed;

	public bool ShieldHeld => _primary.ShieldHeld || _secondary.ShieldHeld;

	public bool JammerPressed => _primary.JammerPressed || _secondary.JammerPressed;
}
