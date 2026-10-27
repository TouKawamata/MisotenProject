using UnityEngine;

public class EntryEffectsController : MonoBehaviour {
	public float Progress = 0.0f;

	[SerializeField]
	private Material[] materials;

	private readonly int progressId = Shader.PropertyToID("_RevealHeight");

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {
	}

	// Update is called once per frame
	void Update() {
		foreach (var item in materials) {
			item.SetFloat(progressId, Progress);
		}
	}
}
