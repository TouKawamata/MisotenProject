using UnityEngine;
using UnityEngine.VFX;

public class EntryEffectsController : MonoBehaviour {
	public float RevealHeight = 0.0f;
	public float RevealHeightMax = 2.6f;

	[SerializeField]
	private float _revealSpeed = 1.0f;


	[SerializeField]
	private Material[] materials;

	[SerializeField]
	private VisualEffect[] visualEffects;

	[SerializeField]
	private GameObject underRing;

	[SerializeField]
	private GameObject overRing;

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
			if (RevealHeight > RevealHeightMax) {
				RevealHeight = RevealHeightMax;
			}
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
