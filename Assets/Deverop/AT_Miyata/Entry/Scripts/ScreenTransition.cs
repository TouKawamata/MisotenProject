using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 画面トランジション（UI の上から画面全体を覆う。見た目は Entry/ScreenTransition シェーダー）。
//  1. Cover()：_coverDuration 秒かけてシェーダーの _Progress を 0→1（光の筋 → 暗くなる → 真ん中の線 → 真っ黒）。覆い切ったら IsCovered。
//  2. LoadSceneAndReveal()：覆ったままシーンを読み込み、読み込み終わったら _revealDuration 秒かけて黒から明ける（_Reveal を 0→1）。
//     シーンをまたぐため、このオブジェクトを DontDestroyOnLoad にし、明け切ったら自分を消す（GameScene 側には何も置かなくてよい）。
//     Entry のシーンの読み込みはここだけで行う（EntryTrackingFlow もこれを呼ぶ）。
// マテリアルが無くて覆えないときは、すぐに覆い切った扱いにする（見た目が出なくても遷移は止まらない）。
// キーボードは読まない（テスト用の K：覆う／C：戻す は EntryTestInput から Cover()・ResetTransition() を呼ぶ）。
// 時間は unscaledDeltaTime。読み込み直後などの長いフレームで演出が飛ばないように、1フレームで進む時間に上限（_maxDeltaTime）を付けている。
public class ScreenTransition : MonoBehaviour {
	// シェーダーのプロパティ
	private static readonly int ProgressId = Shader.PropertyToID("_Progress");
	private static readonly int RevealId = Shader.PropertyToID("_Reveal");
	private static readonly int AspectId = Shader.PropertyToID("_Aspect");

	// トランジションの状態
	public enum TransitionState {
		// 何も出していない
		Idle,
		// 覆っている途中
		Covering,
		// 覆い切った（真っ黒）
		Covered,
		// シーンを読み込んでいる（真っ黒のまま）
		Loading,
		// 読み込み後、明けるのを待っている
		WaitingReveal,
		// 明けている途中
		Revealing
	}

	[Header("見た目")]
	[Tooltip("Entry/ScreenTransition シェーダーのマテリアル。実行時に複製して使う（アセットは書き換わらない）。未指定ならシェーダーを名前で探す（エディタ用。ビルドでは指定が必要）")]
	[SerializeField] private Material _material;
	[Tooltip("Canvas の描画順（大きいほど手前）。見失いの警告（1000）より手前にする")]
	[SerializeField] private int _sortingOrder = 1100;

	[Header("時間（秒）")]
	[Tooltip("覆い始めてから真っ黒になるまで")]
	[SerializeField, Min(0f)] private float _coverDuration = 1.2f;
	[Tooltip("シーンを読み込み終わってから明け始めるまで（読み込み直後の重いフレームを避ける）")]
	[SerializeField, Min(0f)] private float _revealDelay = 0.2f;
	[Tooltip("黒から明け切るまで")]
	[SerializeField, Min(0f)] private float _revealDuration = 0.5f;
	[Tooltip("1フレームで進む時間の上限（読み込みなどで画面が止まった直後に、演出が一気に飛ばないように）")]
	[SerializeField, Min(0.001f)] private float _maxDeltaTime = 0.05f;

	// 自動で作る Canvas と Image
	private Canvas _canvas;
	private Image _image;
	// 複製したマテリアル
	private Material _materialInstance;
	// 今の状態に入ってからの秒数
	private float _stateTime;
	// シーンの読み込み
	private AsyncOperation _loadOperation;
	// 覆い切ったらシーンを移るか、移る先
	private bool _loadAfterCover;
	private string _sceneToLoad;

	// 今の状態
	public TransitionState State { get; private set; } = TransitionState.Idle;

	// 覆い切っているか（真っ黒。シーンを移ってよい）
	public bool IsCovered => State == TransitionState.Covered;

	private void Awake() {
		BuildUI();
		ApplyIdle();
	}

	private void OnDestroy() {
		// 判定：複製したマテリアルがあるか（あれば破棄する）
		if (_materialInstance != null) {
			Destroy(_materialInstance);
		}
	}

	// 覆い始める（シーンは移らない。覆い切ったら IsCovered）。シーンを移っている最中は何もしない。
	public void Cover() {
		StartCover(false, null);
	}

	// 覆い切ったらシーンを読み込み、読み込み終わったら明ける。覆い切っていればすぐ読み込む。
	// 読めないシーン（空・Build Settings に無い）ならエラーを出して false を返す（覆っている状態はそのまま）。
	public bool LoadSceneAndReveal(string sceneName) {
		// 判定：移るシーンが指定されているか
		if (string.IsNullOrEmpty(sceneName)) {
			Debug.LogError("ScreenTransition: 移るシーンが空です", this);
			return false;
		}

		// 判定：Build Settings に入っているか（入っていないと読み込みがエラーになる）
		if (!Application.CanStreamedLevelBeLoaded(sceneName)) {
			Debug.LogError($"ScreenTransition: シーン「{sceneName}」が Build Settings にありません", this);
			return false;
		}

		// 判定：シーンを移っている最中か（もう移っているので、受け付けたことにする）
		if (IsLoadingOrRevealing()) {
			return true;
		}

		// 判定：覆い切っているか（覆い切っていればすぐ読み込む。まだなら覆ってから）
		if (State == TransitionState.Covered) {
			StartLoading(sceneName);
		} else if (State == TransitionState.Covering) {
			_loadAfterCover = true;
			_sceneToLoad = sceneName;
		} else {
			StartCover(true, sceneName);
		}
		return true;
	}

	// 最初の状態に戻す（何も出さない）。シーンを移っている最中は何もしない。
	public void ResetTransition() {
		// 判定：シーンを移っている最中か
		if (IsLoadingOrRevealing()) {
			return;
		}

		_loadAfterCover = false;
		_sceneToLoad = null;
		ApplyIdle();
	}

	private void Update() {
		float deltaTime = Mathf.Min(Time.unscaledDeltaTime, _maxDeltaTime);
		_stateTime += deltaTime;

		// 判定：何か出しているか（出していれば画面の縦横比を毎フレーム入れる。解像度が変わっても歪まないように）
		if (State != TransitionState.Idle && _materialInstance != null) {
			_materialInstance.SetFloat(AspectId, Screen.height > 0 ? (float)Screen.width / Screen.height : 1f);
		}

		switch (State) {
			case TransitionState.Covering:
				UpdateCovering();
				break;
			case TransitionState.Loading:
				UpdateLoading();
				break;
			case TransitionState.WaitingReveal:
				UpdateWaitingReveal();
				break;
			case TransitionState.Revealing:
				UpdateRevealing();
				break;
			default:
				break;
		}
	}

	// 覆っている途中：_Progress を 0→1。覆い切ったら、移る指定があればシーンを読み込む。
	private void UpdateCovering() {
		float progress = _coverDuration > 0f ? Mathf.Clamp01(_stateTime / _coverDuration) : 1f;
		SetProgress(progress);

		// 判定：覆い切ったか
		if (progress < 1f) {
			return;
		}

		SetState(TransitionState.Covered);
		// 判定：覆い切ったらシーンを移る指定か
		if (_loadAfterCover) {
			StartLoading(_sceneToLoad);
		}
	}

	// シーンを読み込んでいる：終わったら明けるのを待つ。
	private void UpdateLoading() {
		// 判定：読み込みが終わったか
		if (_loadOperation == null || _loadOperation.isDone) {
			_loadOperation = null;
			SetState(TransitionState.WaitingReveal);
		}
	}

	// 明けるのを待っている：_revealDelay 秒たったら明け始める。
	private void UpdateWaitingReveal() {
		// 判定：待ち時間が過ぎたか
		if (_stateTime >= _revealDelay) {
			SetState(TransitionState.Revealing);
		}
	}

	// 明けている途中：_Reveal を 0→1。明け切ったら自分を消す。
	private void UpdateRevealing() {
		float reveal = _revealDuration > 0f ? Mathf.Clamp01(_stateTime / _revealDuration) : 1f;
		// 判定：マテリアルがあるか
		if (_materialInstance != null) {
			_materialInstance.SetFloat(RevealId, reveal);
		}

		// 判定：明け切ったか
		if (reveal >= 1f) {
			Destroy(gameObject);
		}
	}

	// 覆い始める。loadAfterCover が true なら、覆い切ったら sceneName を読み込む。
	private void StartCover(bool loadAfterCover, string sceneName) {
		// 判定：シーンを移っている最中か
		if (IsLoadingOrRevealing()) {
			return;
		}

		_loadAfterCover = loadAfterCover;
		_sceneToLoad = sceneName;

		// 判定：マテリアルが用意できていないか（シェーダーが無いときは見た目を出さずに、すぐ覆い切った扱いにする）
		if (_materialInstance == null) {
			Debug.LogWarning("ScreenTransition: マテリアルが無いため、見た目を出さずに進めます", this);
			SetState(TransitionState.Covered);
			// 判定：覆い切ったらシーンを移る指定か
			if (_loadAfterCover) {
				StartLoading(_sceneToLoad);
			}
			return;
		}

		_canvas.enabled = true;
		_materialInstance.SetFloat(RevealId, 0f);
		SetProgress(0f);
		SetState(TransitionState.Covering);
	}

	// シーンを読み込み始める（シーンをまたいで残るように DontDestroyOnLoad にする）。シーン名は LoadSceneAndReveal で確認済み。
	private void StartLoading(string sceneName) {
		_loadAfterCover = false;

		// DontDestroyOnLoad はルートのオブジェクトにしか効かないので、親から外す
		transform.SetParent(null, true);
		DontDestroyOnLoad(gameObject);
		_loadOperation = SceneManager.LoadSceneAsync(sceneName);
		SetState(TransitionState.Loading);
	}

	// 最初の状態（何も出さない）にする。
	private void ApplyIdle() {
		SetState(TransitionState.Idle);
		// 判定：Canvas があるか（出していないときは描画しない）
		if (_canvas != null) {
			_canvas.enabled = false;
		}

		// 判定：マテリアルがあるか
		if (_materialInstance != null) {
			_materialInstance.SetFloat(ProgressId, 0f);
			_materialInstance.SetFloat(RevealId, 0f);
		}
	}

	private void SetProgress(float progress) {
		// 判定：マテリアルがあるか
		if (_materialInstance == null) {
			return;
		}
		_materialInstance.SetFloat(ProgressId, progress);
	}

	private void SetState(TransitionState state) {
		State = state;
		_stateTime = 0f;
	}

	private bool IsLoadingOrRevealing() {
		return State == TransitionState.Loading || State == TransitionState.WaitingReveal || State == TransitionState.Revealing;
	}

	// 画面いっぱいの Image を持つ Canvas を自分の子に作る。
	private void BuildUI() {
		// Canvas（Screen Space - Overlay）
		GameObject canvasObject = new GameObject("ScreenTransitionCanvas", typeof(RectTransform));
		canvasObject.transform.SetParent(transform, false);
		_canvas = canvasObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = _sortingOrder;

		// 画面いっぱいの Image
		GameObject imageObject = new GameObject("Screen", typeof(RectTransform));
		imageObject.transform.SetParent(canvasObject.transform, false);
		RectTransform imageRect = (RectTransform)imageObject.transform;
		imageRect.anchorMin = Vector2.zero;
		imageRect.anchorMax = Vector2.one;
		imageRect.offsetMin = Vector2.zero;
		imageRect.offsetMax = Vector2.zero;
		_image = imageObject.AddComponent<Image>();
		_image.raycastTarget = false;

		// マテリアル（指定が無ければシェーダーを名前で探す）
		Material source = _material;
		// 判定：マテリアルが指定されているか
		if (source == null) {
			Shader shader = Shader.Find("Entry/ScreenTransition");
			// 判定：シェーダーが見つかったか
			if (shader != null) {
				source = new Material(shader);
			} else {
				Debug.LogError("ScreenTransition: Material が未指定で、Entry/ScreenTransition シェーダーも見つかりません", this);
				return;
			}
		}

		_materialInstance = new Material(source);
		_image.material = _materialInstance;

		// 判定：名前で探して作った仮のマテリアルか（複製したので要らない）
		if (source != _material) {
			Destroy(source);
		}
	}
}
