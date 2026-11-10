using UnityEngine;

public class EntryTestInput : MonoBehaviour {
	[SerializeField]
	private EntryEffectsController _entryEffectsController;

	[SerializeField]
	private EntryCanvasController _entryCanvasController;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {

	}

	// Update is called once per frame
	void Update() {
		// テスト用の入力(新しいInputSystemを使用)
		if (UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame) {
			_entryEffectsController.StartAnimation();
		}
		if (UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame) {
			_entryCanvasController.StartEffect();
		}

		// リセット
		if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) {
			_entryEffectsController.ResetAnimation();

		}
		if (UnityEngine.InputSystem.Keyboard.current.tKey.wasPressedThisFrame) {
			_entryCanvasController.ResetEffect();
		}

	}
}
