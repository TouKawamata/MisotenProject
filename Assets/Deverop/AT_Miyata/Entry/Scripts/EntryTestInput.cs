using UnityEngine;

public class EntryTestInput : MonoBehaviour {
	[SerializeField]
	private EntryEffectsController _entryEffectsController;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {

	}

	// Update is called once per frame
	void Update() {
		// テスト用の入力(新しいInputSystemを使用)
		if (UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame) {
			_entryEffectsController.StartReveal(true);
		}

		// リセット
		if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) {
			_entryEffectsController.StartReveal(false);
			_entryEffectsController.RevealHeight = -3.0f;

		}

	}
}
