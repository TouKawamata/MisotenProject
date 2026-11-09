using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public class EntryCanvasController : MonoBehaviour {
	[Header("背景")]
	[SerializeField] private GameObject[] _backgroundImages;
	[SerializeField, Tooltip("ターゲット位置からの出発位置のずれ(px)。マイナスで左から入る")] private float _backgroundOffsetX = -2000.0f;
	[SerializeField, FormerlySerializedAs("_effectDuration"), Tooltip("秒")] private float _backgroundDuration = 0.15f;
	[SerializeField] private Ease _backgroundEase = Ease.OutQuad;

	[Header("背景 バウンド")]
	[SerializeField, Tooltip("左に戻る距離(px)")] private float _backgroundBounceDistance = 10.0f;
	[SerializeField, Tooltip("左に戻る時間(秒)")] private float _backgroundBounceBackDuration = 0.01f;
	[SerializeField] private Ease _backgroundBounceBackEase = Ease.OutQuad;
	[SerializeField, Tooltip("ターゲットへ戻る時間(秒)")] private float _backgroundBounceReturnDuration = 0.15f;
	[SerializeField] private Ease _backgroundBounceReturnEase = Ease.InOutQuad;

	[Header("縁の線と粒")]
	[SerializeField, Tooltip("背景がターゲットに着いた瞬間に再生する")] private EdgeLineParticleEmitter[] _edgeEmitters;

	[Header("テキスト 進入")]
	[SerializeField, Tooltip("[0]の初期位置をターゲットとして全要素が移動する")] private GameObject[] _textImages;
	[SerializeField, Tooltip("ターゲット位置からの出発位置のずれ(px)。マイナスで左から入る")] private float _textOffsetX = -2000.0f;
	[SerializeField, Tooltip("1要素あたりの進入時間(秒)")] private float _textDuration = 0.15f;
	[SerializeField, Tooltip("要素ごとの出発の遅れ(秒)")] private float _textStagger = 0.005f;
	[SerializeField, Tooltip("StartEffectからテキストが動き出すまでの遅延(秒)")] private float _textStartDelay = 0.05f;
	[SerializeField] private Ease _textEase = Ease.OutQuad;

	[Header("テキスト バウンド")]
	[SerializeField, Tooltip("左に戻る距離(px)")] private float _bounceDistance = 20.0f;
	[SerializeField, Tooltip("左に戻る時間(秒)")] private float _bounceBackDuration = 0.01f;
	[SerializeField] private Ease _bounceBackEase = Ease.OutQuad;
	[SerializeField, Tooltip("ターゲットへ戻る時間(秒)")] private float _bounceReturnDuration = 0.05f;
	[SerializeField] private Ease _bounceReturnEase = Ease.InOutQuad;

	private enum AnimState {
		None,
		Approach,
		BounceBack,
		BounceReturn,
		Done
	};

	// 背景
	private RectTransform[] _backgroundRects;
	private Vector2[] _backgroundStartPositions;
	private Vector2[] _backgroundTargetPositions;
	private AnimState _backgroundState = AnimState.None;
	private float _backgroundTimer = 0.0f;

	// テキスト
	private RectTransform[] _textRects;
	private Vector2 _textTarget;
	private Vector2 _textStart;
	private Vector2[] _textBouncePositions; // バウンド開始時点の各要素の位置
	private AnimState _textState = AnimState.None;
	private float _textTimer = 0.0f;


	void Awake() {
		InitBackground();
		InitText();
		ResetEffect();
	}

	void Update() {
		UpdateBackground();
		UpdateText();
	}

	public void StartEffect() {
		ResetEffect();
		_backgroundState = AnimState.Approach;
		_textState = AnimState.Approach;
	}

	public void ResetEffect() {
		_backgroundState = AnimState.None;
		_backgroundTimer = 0.0f;
		for (int i = 0; i < _backgroundImages.Length; i++) {
			_backgroundRects[i].anchoredPosition = _backgroundStartPositions[i];
		}

		foreach (var emitter in _edgeEmitters) {
			emitter.ResetEffect();
		}

		_textState = AnimState.None;
		_textTimer = 0.0f;
		for (int i = 0; i < _textImages.Length; i++) {
			_textImages[i].SetActive(true);
			_textRects[i].anchoredPosition = _textStart;
		}
	}


	// ---------- 背景 ----------

	private void InitBackground() {
		_backgroundRects = GetRects(_backgroundImages);
		_backgroundStartPositions = new Vector2[_backgroundImages.Length];
		_backgroundTargetPositions = new Vector2[_backgroundImages.Length];

		for (int i = 0; i < _backgroundImages.Length; i++) {
			_backgroundTargetPositions[i] = _backgroundRects[i].anchoredPosition;
			_backgroundStartPositions[i] = _backgroundTargetPositions[i] + new Vector2(_backgroundOffsetX, 0.0f);
		}
	}

	private void UpdateBackground() {
		switch (_backgroundState) {
			case AnimState.Approach:
				UpdateBackgroundApproach();
				break;
			case AnimState.BounceBack:
				UpdateBackgroundBounceBack();
				break;
			case AnimState.BounceReturn:
				UpdateBackgroundBounceReturn();
				break;
		}
	}

	// 全要素が同時に左から進入
	private void UpdateBackgroundApproach() {
		_backgroundTimer += Time.deltaTime;

		float easedT = Eased(Progress(_backgroundTimer, _backgroundDuration), _backgroundEase);
		for (int i = 0; i < _backgroundImages.Length; i++) {
			_backgroundRects[i].anchoredPosition = Vector2.Lerp(_backgroundStartPositions[i], _backgroundTargetPositions[i], easedT);
		}

		if (_backgroundTimer >= _backgroundDuration) {
			_backgroundState = AnimState.BounceBack;
			_backgroundTimer = 0.0f;

			foreach (var emitter in _edgeEmitters) {
				emitter.Play();
			}
		}
	}

	// 全要素が同じ距離だけ左へ戻る
	private void UpdateBackgroundBounceBack() {
		_backgroundTimer += Time.deltaTime;

		float offsetX = -_backgroundBounceDistance * Eased(Progress(_backgroundTimer, _backgroundBounceBackDuration), _backgroundBounceBackEase);
		for (int i = 0; i < _backgroundImages.Length; i++) {
			_backgroundRects[i].anchoredPosition = _backgroundTargetPositions[i] + new Vector2(offsetX, 0.0f);
		}

		if (_backgroundTimer >= _backgroundBounceBackDuration) {
			_backgroundState = AnimState.BounceReturn;
			_backgroundTimer = 0.0f;
		}
	}

	// 全要素が同時にターゲットへ戻る
	private void UpdateBackgroundBounceReturn() {
		_backgroundTimer += Time.deltaTime;

		float offsetX = -_backgroundBounceDistance * (1.0f - Eased(Progress(_backgroundTimer, _backgroundBounceReturnDuration), _backgroundBounceReturnEase));
		for (int i = 0; i < _backgroundImages.Length; i++) {
			_backgroundRects[i].anchoredPosition = _backgroundTargetPositions[i] + new Vector2(offsetX, 0.0f);
		}

		if (_backgroundTimer >= _backgroundBounceReturnDuration) {
			_backgroundState = AnimState.Done;
		}
	}


	// ---------- テキスト ----------

	private void InitText() {
		_textRects = GetRects(_textImages);
		_textBouncePositions = new Vector2[_textImages.Length];
		if (_textImages.Length == 0) return;

		_textTarget = _textRects[0].anchoredPosition;
		_textStart = _textTarget + new Vector2(_textOffsetX, 0.0f);
	}

	private void UpdateText() {
		if (_textImages.Length == 0) return;

		switch (_textState) {
			case AnimState.Approach:
				UpdateTextApproach();
				break;
			case AnimState.BounceBack:
				UpdateTextBounceBack();
				break;
			case AnimState.BounceReturn:
				UpdateTextBounceReturn();
				break;
		}
	}

	// 上から順に時間差で左から進入。[0]が着いたらバウンドへ
	private void UpdateTextApproach() {
		_textTimer += Time.deltaTime;

		for (int i = 0; i < _textImages.Length; i++) {
			float t = Progress(_textTimer - _textStartDelay - i * _textStagger, _textDuration);
			_textRects[i].anchoredPosition = Vector2.Lerp(_textStart, _textTarget, Eased(t, _textEase));
		}

		if (_textTimer - _textStartDelay >= _textDuration) {
			// この瞬間の位置関係を保ったままバウンドさせる
			for (int i = 0; i < _textImages.Length; i++) {
				_textBouncePositions[i] = _textRects[i].anchoredPosition;
			}
			_textState = AnimState.BounceBack;
			_textTimer = 0.0f;
		}
	}

	// 全要素が同じ距離だけ左へ戻る
	private void UpdateTextBounceBack() {
		_textTimer += Time.deltaTime;

		float offsetX = -_bounceDistance * Eased(Progress(_textTimer, _bounceBackDuration), _bounceBackEase);
		for (int i = 0; i < _textImages.Length; i++) {
			_textRects[i].anchoredPosition = _textBouncePositions[i] + new Vector2(offsetX, 0.0f);
		}

		if (_textTimer >= _bounceBackDuration) {
			_textState = AnimState.BounceReturn;
			_textTimer = 0.0f;
		}
	}

	// 全要素が[0]と同じタイミングでターゲットへ到着。[1]以降は到着後に非表示
	private void UpdateTextBounceReturn() {
		_textTimer += Time.deltaTime;

		float easedT = Eased(Progress(_textTimer, _bounceReturnDuration), _bounceReturnEase);
		Vector2 backOffset = new Vector2(-_bounceDistance, 0.0f);
		for (int i = 0; i < _textImages.Length; i++) {
			_textRects[i].anchoredPosition = Vector2.Lerp(_textBouncePositions[i] + backOffset, _textTarget, easedT);
		}

		if (_textTimer >= _bounceReturnDuration) {
			for (int i = 1; i < _textImages.Length; i++) {
				_textImages[i].SetActive(false);
			}
			_textState = AnimState.Done;
		}
	}


	// ---------- 共通 ----------

	private static RectTransform[] GetRects(GameObject[] objects) {
		var rects = new RectTransform[objects.Length];
		for (int i = 0; i < objects.Length; i++) {
			rects[i] = objects[i].GetComponent<RectTransform>();
		}
		return rects;
	}

	// duration が 0 以下でも割り算しないように進捗率を返す
	private static float Progress(float time, float duration) {
		return duration <= 0.0f ? 1.0f : Mathf.Clamp01(time / duration);
	}

	private static float Eased(float t, Ease ease) {
		return DOVirtual.EasedValue(0.0f, 1.0f, t, ease);
	}
}
