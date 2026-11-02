using UnityEngine;
using UnityEngine.VFX;
using DG.Tweening;

public class EntryEffectsController : MonoBehaviour {
	[Header("参照")]
	[SerializeField] private Material[] materials;
	[SerializeField] private VisualEffect[] visualEffects;
	[SerializeField, Tooltip("下側のリング")] private GameObject underRing;
	[SerializeField, Tooltip("上側のリング")] private GameObject overRing;

	[Header("展開")]
	[SerializeField, Tooltip("Off: リングとエフェクトを出した状態で待機し、上昇のみ行う")] private bool _useSpread = true;
	[SerializeField] private Ease _spreadEase = Ease.InOutSine;
	[SerializeField, Tooltip("秒")] private float _spreadDuration = 0.5f;
	[SerializeField, Tooltip("展開後のサイズ")] private Vector3 _spreadAfterSize = new Vector3(5.0f, 1.0f, 5.0f);

	[Header("上昇")]
	[SerializeField, Tooltip("上昇前の遅延(秒)")] private float _delayBeforeRise = 0.5f;
	[SerializeField] private float _revealSpeed = 1.0f;
	[SerializeField] private float _revealHeightMax = 2.6f;
	[SerializeField] private float _revealHeightMin = -2.6f;

	[Header("上側リングの移動範囲")]
	[SerializeField] private float _overRingRevealHeightMax = 2.3f;
	[SerializeField] private float _overRingRevealMin = -2.2f;

	private float _revealHeight = 0.0f;

	private enum State {
		None,
		Spread,
		DelayBeforeRise,
		Rise
	};

	private State _state = State.None;


	private float _time = 0.0f;


	private readonly int _revealHeightId = Shader.PropertyToID("_RevealHeight");
	private readonly int _revealHeightIdVFX = Shader.PropertyToID("RevealHeight");


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {
		ResetAnimation();
	}

	// Update is called once per frame
	void Update() {
		switch (_state) {
			case State.Spread:
				Spread();
				break;
			case State.DelayBeforeRise:
				DelayBeforeRise();
				break;
			case State.Rise:
				Rise();
				break;
		}
	}

	public void StartAnimation() {
		_state = _useSpread ? State.Spread : State.Rise;
	}

	public void ResetAnimation() {
		_state = State.None;
		_revealHeight = _revealHeightMin;
		_time = 0.0f;
		SetRingScale(_useSpread ? Vector3.zero : _spreadAfterSize);

		EffectsVisible(!_useSpread);
		ApplyHeight();
	}


	private void Spread() {
		_time += Time.deltaTime;

		float t = Mathf.Clamp01(_time / _spreadDuration);
		float easeT = DOVirtual.EasedValue(0, 1, t, _spreadEase);

		SetRingScale(Vector3.Lerp(Vector3.zero, _spreadAfterSize, easeT));

		if (_time > _spreadDuration) {
			FinishSpread();
		}
	}

	private void FinishSpread() {
		_state = State.DelayBeforeRise;
		_time = 0.0f;

		EffectsVisible(true);
	}

	private void SetRingScale(Vector3 scale) {
		underRing.transform.localScale = scale;
		overRing.transform.localScale = scale;
	}

	private void DelayBeforeRise() {
		_time += Time.deltaTime;
		if (_time > _delayBeforeRise) {
			_state = State.Rise;
		}
	}

	private void Rise() {
		_revealHeight += _revealSpeed * Time.deltaTime;
		if (_revealHeight > _revealHeightMax) {
			_revealHeight = _revealHeightMax;
			_state = State.None;
		}

		ApplyHeight();
	}

	private void ApplyHeight() {
		foreach (var item in materials) {
			item.SetFloat(_revealHeightId, _revealHeight);
		}
		foreach (var item in visualEffects) {
			item.SetFloat(_revealHeightIdVFX, _revealHeight);
		}

		overRing.transform.localPosition = new Vector3(0, Mathf.Clamp(_revealHeight, _overRingRevealMin, _overRingRevealHeightMax), 0);
	}

	private void EffectsVisible(bool isVisible) {
		foreach (var item in visualEffects) {
			if (isVisible) {
				item.Reinit();
				item.Play();
			} else {
				item.Stop();
			}
		}
	}

}
