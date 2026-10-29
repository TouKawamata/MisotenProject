using UnityEngine;
using UnityEngine.VFX;

public class EntryEffectsController : MonoBehaviour {
	public float RevealHeight = 0.0f;

	[SerializeField]
	private float _revealSpeed = 1.0f;



	[SerializeField]
	private Material[] materials;

	[SerializeField]
	private VisualEffect[] visualEffects;

	private readonly int revealHeightId = Shader.PropertyToID("_RevealHeight");
	private readonly int revealHeightIdVFX = Shader.PropertyToID("RevealHeight");


	private bool _isRevealing = false;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {
	}

	// Update is called once per frame
	void Update() {
		if (_isRevealing) {
			RevealHeight += _revealSpeed * Time.deltaTime;
		}

		foreach (var item in materials) {
			item.SetFloat(revealHeightId, RevealHeight);
		}
		foreach (var item in visualEffects) {
			item.SetFloat(revealHeightIdVFX, RevealHeight);
		}
	}

	public void StartReveal(bool isRevealing) {
		_isRevealing = isRevealing;
	}

}
