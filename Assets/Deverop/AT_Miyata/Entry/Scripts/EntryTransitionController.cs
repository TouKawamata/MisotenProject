using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Entry シーンの遷移演出（合図のポーズが来てから、シーンを移るまで）。
// EntryTrackingFlow が合図で Play() を呼び、IsFinished が true になったらシーンを移る。
// 流れ（Play() からの秒数。全体の長さは _firstDelay + _secondDelay + _moveTime）：
//  1. delay1（_firstDelay）：背景は元の位置のまま。色は _Color2 = _Color1。テキストには何もしない。
//  2. delay2（_secondDelay）：入った瞬間に背景を _targetPosition へ直接移動（補間なし）。色は _Color2 = 元の _Color2。
//     テキストを表示し、開始時の位置から _textOffset1 だけずらす。
//  3. move（_moveTime）：背景が _targetPosition から元の位置へ上がる。色は _Color2 = _Color1。
//     テキストは入った瞬間に _textOffset2 へずらし、そこから「背景が着いたときのテキストの位置」へ、背景より _textMoveLead 秒早く着く。
// 演出が終わっても元の状態には戻さない（終わった時点の見た目のまま）。戻すのは F キー（_allowResetKey）か ResetEffect()。
// テキストの元の位置は Play() を呼んだ瞬間の位置（他の演出で動いているため）。
public class EntryTransitionController : MonoBehaviour {
	// マテリアルの色のプロパティ
	private static readonly int Color1Id = Shader.PropertyToID("_Color1");
	private static readonly int Color2Id = Shader.PropertyToID("_Color2");

	// 演出の区間
	private enum Phase {
		// 背景は元の位置。色は _Color2 = _Color1
		Delay1,
		// 背景は _targetPosition で止まる。色は元の _Color2。テキストを表示
		Delay2,
		// 背景が元の位置へ上がる。色は _Color2 = _Color1
		Move,
		// 終わった
		Done
	}

	[Header("参照")]
	[Tooltip("動かす背景（RectTransform）。テキストはこの子に置く")]
	[SerializeField] private GameObject _parentObject;
	[Tooltip("背景と時間をずらして動かすテキスト（RectTransform）。_parentObject の子")]
	[SerializeField] private GameObject _textObject;
	[Tooltip("色を変える Image。マテリアルの _Color1・_Color2 を使う（実行時にマテリアルを複製するので、アセットは書き換わらない）")]
	[SerializeField] private GameObject _colorChangeObject;

	[Header("時間（秒）")]
	[Tooltip("delay1：演出を始めてから背景が Target Position へ移るまで（この間、背景は元の位置。テキストには何もしない）")]
	[SerializeField, Min(0f)] private float _firstDelay = 0.1f;
	[Tooltip("delay2：背景が Target Position で止まっている時間（この間、テキストを表示して少し下にずらす）")]
	[SerializeField, Min(0f)] private float _secondDelay = 0.2f;
	[Tooltip("move：背景が Target Position から元の位置へ上がり切るまでの時間")]
	[SerializeField, Min(0f)] private float _moveTime = 0.3f;
	[Tooltip("move で、テキストが背景より早く着く時間（テキストの移動時間は Move Time − これ）")]
	[SerializeField, Min(0f)] private float _textMoveLead = 0.1f;

	[Header("動き")]
	[Tooltip("delay2 で背景を移す位置（anchoredPosition。ずらす量ではなく位置そのもの）。move でここから元の位置へ上がる")]
	[SerializeField] private Vector2 _targetPosition = new Vector2(0f, -100f);
	[Tooltip("move のイージング（背景・テキスト共通）")]
	[SerializeField] private Ease _moveEase = Ease.OutQuad;
	[Tooltip("delay2 でのテキストのずれ（Play() のときの位置から。下にずらすなら y をマイナス）")]
	[SerializeField] private Vector2 _textOffset1 = new Vector2(0f, -30f);
	[Tooltip("move に入った瞬間のテキストのずれ（Play() のときの位置から。Text Offset 1 より下にする）。ここから上がる")]
	[SerializeField] private Vector2 _textOffset2 = new Vector2(0f, -80f);

	[Header("キーボード（テスト用）")]
	[Tooltip("ONにすると、F キーで演出を止めて最初の状態に戻す（背景の位置・色。テキストは Play() のときの位置に戻して非表示）")]
	[SerializeField] private bool _allowResetKey = true;

	// 背景
	private RectTransform _parentRectTransform;
	private Vector2 _originalPosition;
	// テキスト（元の位置は Play() のときに覚え直す）
	private RectTransform _textRectTransform;
	private Vector2 _textOriginalPosition;
	// 色
	private Material _colorMaterial;
	private Color _originalColor1;
	private Color _originalColor2;
	// 初期化したか（Awake の前に ResetEffect() が呼ばれても動くように、遅延初期化にしている）
	private bool _isInitialized;

	// 演出を始めてからの秒数
	private float _time;

	// 演出を出しているか
	public bool IsPlaying { get; private set; }

	// 演出が終わり、シーンを移ってよいか
	public bool IsFinished { get; private set; }

	// 演出全体の長さ（秒）
	public float TotalDuration => _firstDelay + _secondDelay + _moveTime;

	private void Awake() {
		EnsureInitialized();
	}

	private void OnDestroy() {
		// 判定：複製したマテリアルがあるか（あれば破棄する）
		if (_colorMaterial != null) {
			Destroy(_colorMaterial);
		}
	}

	// 遷移演出を始める（出している途中で呼ばれたら最初から）。テキストの表示状態には触らない（delay1 では何もしない）。
	public void Play() {
		EnsureInitialized();

		// 判定：テキストがあるか（あれば今の位置を元の位置として覚える）
		if (_textRectTransform != null) {
			// 判定：出している途中か（途中ならずらした位置になっているので、前の元の位置を使い続ける）
			if (IsPlaying) {
				_textRectTransform.anchoredPosition = _textOriginalPosition;
			}
			_textOriginalPosition = _textRectTransform.anchoredPosition;
		}

		RestoreBackground();
		_time = 0f;
		IsPlaying = true;
		IsFinished = false;
		ApplyEffect();

		// 判定：長さが0か（0ならすぐ終わる）
		if (_time >= TotalDuration) {
			Finish();
		}
	}

	// 遷移演出を止めて、最初の状態に戻す（背景の位置・色。テキストは Play() のときの位置に戻して非表示）。
	public void ResetEffect() {
		EnsureInitialized();
		_time = 0f;
		IsPlaying = false;
		IsFinished = false;
		RestoreBackground();

		// 判定：テキストがあるか
		if (_textRectTransform != null) {
			_textRectTransform.anchoredPosition = _textOriginalPosition;
			_textObject.SetActive(false);
		}
	}

	private void Update() {
		// 判定：テスト用に F でリセットするか
		if (_allowResetKey && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) {
			ResetEffect();
			return;
		}

		// 判定：演出を出しているか
		if (!IsPlaying) {
			return;
		}

		_time += Time.deltaTime;
		ApplyEffect();

		// 判定：演出の長さが過ぎたか
		if (_time >= TotalDuration) {
			Finish();
		}
	}

	// 演出を終える（位置・色は終わった時点のまま。戻すのは ResetEffect()）。
	private void Finish() {
		IsPlaying = false;
		IsFinished = true;
	}

	// 今の時間の区間。
	private Phase GetPhase() {
		// 判定：delay1 の間か
		if (_time < _firstDelay) {
			return Phase.Delay1;
		}

		// 判定：delay2 の間か
		if (_time < _firstDelay + _secondDelay) {
			return Phase.Delay2;
		}

		// 判定：move の間か
		if (_time < TotalDuration) {
			return Phase.Move;
		}

		return Phase.Done;
	}

	// 今の時間に合わせて、背景・色・テキストを更新する。
	private void ApplyEffect() {
		Phase phase = GetPhase();
		// move の進み具合（背景・テキスト。イージング後。move より前は0）
		float moveStart = _firstDelay + _secondDelay;
		float parentMove = EvaluateMove(moveStart, _moveTime);
		float textMove = EvaluateMove(moveStart, _moveTime - _textMoveLead);

		UpdateBackground(phase, parentMove);
		UpdateColor(phase);
		UpdateText(phase, parentMove, textMove);
	}

	// moveStart から duration 秒かけて 0→1 になる進み具合（イージング後）。
	private float EvaluateMove(float moveStart, float duration) {
		// 判定：始まる前か
		if (_time < moveStart) {
			return 0f;
		}

		// 判定：時間があるか（あればその割合、無ければすぐ着く）
		float t = duration > 0f ? Mathf.Clamp01((_time - moveStart) / duration) : 1f;
		return DOVirtual.EasedValue(0f, 1f, t, _moveEase);
	}

	// 背景：delay1 は元の位置、delay2 は _targetPosition（直接移動）、move は _targetPosition → 元の位置。
	private void UpdateBackground(Phase phase, float parentMove) {
		// 判定：背景があるか
		if (_parentRectTransform == null) {
			return;
		}

		// 判定：delay1 の間か（まだ元の位置）
		if (phase == Phase.Delay1) {
			_parentRectTransform.anchoredPosition = _originalPosition;
			return;
		}

		_parentRectTransform.anchoredPosition = Vector2.LerpUnclamped(_targetPosition, _originalPosition, parentMove);
	}

	// 色：delay2 の間だけ _Color2 を元の色にし、それ以外（delay1・move・終わった後）は _Color1 と同じにする。
	private void UpdateColor(Phase phase) {
		// 判定：色を変えるマテリアルがあるか
		if (_colorMaterial == null) {
			return;
		}

		_colorMaterial.SetColor(Color2Id, phase == Phase.Delay2 ? _originalColor2 : _originalColor1);
	}

	// テキスト：delay1 は何もしない。delay2 は表示して _textOffset1、move は _textOffset2 から「背景が着いたときの位置」へ上がる。
	private void UpdateText(Phase phase, float parentMove, float textMove) {
		// 判定：テキストがあり、delay1 を過ぎたか（delay1 の間は何もしない）
		if (_textRectTransform == null || phase == Phase.Delay1) {
			return;
		}

		// 判定：表示されていないか（delay2 に入ったら表示する）
		if (!_textObject.activeSelf) {
			_textObject.SetActive(true);
		}

		// 判定：delay2 の間か（背景は止まっているので、ずらすだけ）
		if (phase == Phase.Delay2) {
			_textRectTransform.anchoredPosition = _textOriginalPosition + _textOffset1;
			return;
		}

		// move：背景が _targetPosition にいるときの見え方を基準に、テキストは _textOffset2 から
		// 「背景が着いたときの位置」（上がる量 riseOffset の分だけ上）へ textMove で動く。
		// テキストは背景の子なので、背景が上がった分（riseOffset × parentMove）を引いてローカル位置にする（背景の拡大・回転は無い前提）。
		Vector2 riseOffset = _originalPosition - _targetPosition;
		Vector2 textOffset = Vector2.LerpUnclamped(_textOffset2, riseOffset, textMove);
		_textRectTransform.anchoredPosition = _textOriginalPosition + textOffset - riseOffset * parentMove;
	}

	// 背景の位置と色を元に戻す。
	private void RestoreBackground() {
		// 判定：背景があるか
		if (_parentRectTransform != null) {
			_parentRectTransform.anchoredPosition = _originalPosition;
		}

		// 判定：色を変えるマテリアルがあるか
		if (_colorMaterial != null) {
			_colorMaterial.SetColor(Color1Id, _originalColor1);
			_colorMaterial.SetColor(Color2Id, _originalColor2);
		}
	}

	// 参照から RectTransform・マテリアルを取り出し、元の位置と色を覚える（1回だけ）。
	private void EnsureInitialized() {
		// 判定：初期化済みか
		if (_isInitialized) {
			return;
		}
		_isInitialized = true;

		// 判定：背景が指定されているか
		if (_parentObject != null) {
			_parentRectTransform = _parentObject.GetComponent<RectTransform>();
			_originalPosition = _parentRectTransform.anchoredPosition;
		} else {
			Debug.LogWarning("EntryTransitionController: Parent Object が未指定のため、背景を動かせません", this);
		}

		// 判定：テキストが指定されているか（元の位置は Play() で覚え直す。ここでは F キーで戻す先の仮の値）
		if (_textObject != null) {
			_textRectTransform = _textObject.GetComponent<RectTransform>();
			_textOriginalPosition = _textRectTransform.anchoredPosition;
		}

		// 判定：色を変える Image があるか（マテリアルを複製して、アセットを書き換えないようにする）
		Image colorImage = _colorChangeObject != null ? _colorChangeObject.GetComponent<Image>() : null;
		if (colorImage != null && colorImage.material != null) {
			_colorMaterial = new Material(colorImage.material);
			colorImage.material = _colorMaterial;
			_originalColor1 = _colorMaterial.GetColor(Color1Id);
			_originalColor2 = _colorMaterial.GetColor(Color2Id);
		}
	}
}
