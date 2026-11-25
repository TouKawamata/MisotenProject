using UnityEngine;
using UnityEngine.InputSystem;

// Entry シーンのテスト用のキーボード入力（Entry のキーボード入力はすべてここに集めている）。
// 各スクリプトはキーボードを読まず、窓口のメソッドを持つだけ。ここがキーを読んで呼ぶ。
// _enableKeyboard で全部のキーを一括でオン・オフする。本番のシーンには置かない（置かなければキーボードでは何も起きない）。
//  P：3D 演出を始める        O：UI 演出を始める        R：3D 演出を戻す       T：UI 演出を戻す
//  Q／Space／左Shift：合図（妨害弾／ブースト／シールド。EntryTrackingFlow の Trigger Button と同じボタンだけ効く）
//  Enter：待機とキャリブレーションを飛ばす（待機中だけ）    L：どの状態からでも遷移演出を出す
//  M：案内と見失いの警告の表示を切り替える
//  F：遷移演出（EntryTransitionController）を戻す
//  K：画面トランジションで覆う（シーンは移らない）    C：画面トランジションを戻す
public class EntryTestInput : MonoBehaviour {
	[Header("キーボード")]
	[Tooltip("ONにすると、下のキーをすべて受け付ける。OFFならどのキーにも反応しない")]
	[SerializeField] private bool _enableKeyboard = true;

	[Header("参照（未指定ならシーン内から探す）")]
	[Tooltip("エントリー演出（3D）。P で始め、R で戻す")]
	[SerializeField] private EntryEffectsController _entryEffectsController;
	[Tooltip("エントリー演出（UI）。O で始め、T で戻す")]
	[SerializeField] private EntryCanvasController _entryCanvasController;
	[Tooltip("Entry の流れ。Q／Space／左Shift：合図、Enter：飛ばす、L：遷移演出、M：案内の表示")]
	[SerializeField] private EntryTrackingFlow _trackingFlow;
	[Tooltip("遷移演出。F で戻す")]
	[SerializeField] private EntryTransitionController _transitionController;
	[Tooltip("画面トランジション。K で覆い、C で戻す")]
	[SerializeField] private ScreenTransition _screenTransition;

	private void Awake() {
		// 判定：3D 演出が未設定か
		if (_entryEffectsController == null) {
			_entryEffectsController = FindFirstObjectByType<EntryEffectsController>();
		}

		// 判定：UI 演出が未設定か
		if (_entryCanvasController == null) {
			_entryCanvasController = FindFirstObjectByType<EntryCanvasController>();
		}

		// 判定：流れが未設定か
		if (_trackingFlow == null) {
			_trackingFlow = FindFirstObjectByType<EntryTrackingFlow>();
		}

		// 判定：遷移演出が未設定か
		if (_transitionController == null) {
			_transitionController = FindFirstObjectByType<EntryTransitionController>();
		}
	}

	private void Update() {
		Keyboard keyboard = Keyboard.current;
		// 判定：キーボードを使うか（OFF・キーボードが無いときは何もしない）
		if (!_enableKeyboard || keyboard == null) {
			return;
		}

		UpdateEntryEffects(keyboard);
		UpdateTrackingFlow(keyboard);
		UpdateTransition(keyboard);
	}

	// エントリー演出（P・O で始める、R・T で戻す）。
	private void UpdateEntryEffects(Keyboard keyboard) {
		// 判定：3D 演出があるか
		if (_entryEffectsController != null) {
			// 判定：P が押されたか
			if (keyboard.pKey.wasPressedThisFrame) {
				_entryEffectsController.StartAnimation();
			}
			// 判定：R が押されたか
			if (keyboard.rKey.wasPressedThisFrame) {
				_entryEffectsController.ResetAnimation();
			}
		}

		// 判定：UI 演出があるか
		if (_entryCanvasController != null) {
			// 判定：O が押されたか
			if (keyboard.oKey.wasPressedThisFrame) {
				_entryCanvasController.StartEffect();
			}
			// 判定：T が押されたか
			if (keyboard.tKey.wasPressedThisFrame) {
				_entryCanvasController.ResetEffect();
			}
		}
	}

	// Entry の流れ（合図・飛ばす・遷移演出・案内の表示）。
	private void UpdateTrackingFlow(Keyboard keyboard) {
		// 判定：流れがあるか
		if (_trackingFlow == null) {
			return;
		}

		// 判定：合図のキーが押されたか（流れの側で、合図にしているボタンのときだけ効く）
		if (keyboard.qKey.wasPressedThisFrame) {
			_trackingFlow.PressTrigger(EntryTriggerButton.Jammer);
		}
		if (keyboard.spaceKey.wasPressedThisFrame) {
			_trackingFlow.PressTrigger(EntryTriggerButton.Boost);
		}
		if (keyboard.leftShiftKey.wasPressedThisFrame) {
			_trackingFlow.PressTrigger(EntryTriggerButton.Shield);
		}

		// 判定：Enter が押されたか
		if (keyboard.enterKey.wasPressedThisFrame) {
			_trackingFlow.SkipCalibration();
		}

		// 判定：L が押されたか
		if (keyboard.lKey.wasPressedThisFrame) {
			_trackingFlow.StartTransitionForTest();
		}

		// 判定：M が押されたか
		if (keyboard.mKey.wasPressedThisFrame) {
			_trackingFlow.ToggleMessages();
		}
	}

	// 遷移演出と画面トランジション（F：遷移演出を戻す、K：覆う、C：戻す）。
	private void UpdateTransition(Keyboard keyboard) {
		// 判定：F が押されたか
		if (keyboard.fKey.wasPressedThisFrame && _transitionController != null) {
			_transitionController.ResetEffect();
		}

		bool coverPressed = keyboard.kKey.wasPressedThisFrame;
		bool resetPressed = keyboard.cKey.wasPressedThisFrame;
		// 判定：K・C のどちらも押されていないか
		if (!coverPressed && !resetPressed) {
			return;
		}

		// 判定：画面トランジションが見つかっていないか（流れが自動で作ることもあるので、押されたときに探し直す）
		if (_screenTransition == null) {
			_screenTransition = FindFirstObjectByType<ScreenTransition>();
		}
		if (_screenTransition == null) {
			return;
		}

		// 判定：K が押されたか
		if (coverPressed) {
			_screenTransition.Cover();
		}
		// 判定：C が押されたか
		if (resetPressed) {
			_screenTransition.ResetTransition();
		}
	}
}
