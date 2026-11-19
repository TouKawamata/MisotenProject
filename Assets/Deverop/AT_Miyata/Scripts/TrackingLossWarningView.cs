using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TrackingLossMonitor の状態を見て、画面中央に警告の帯とメッセージを出す（仮の表示）。
// UIの参照（_canvasGroup・_messageText）が未指定なら、起動時に専用のCanvas（Screen Space - Overlay）を自分の子に作る。
// そのため、空のGameObjectにこのコンポーネントを付けるだけで使える。見た目を差し替えるときは参照を割り当てる。
public class TrackingLossWarningView : MonoBehaviour {
	[Tooltip("見張り役。未指定なら同じオブジェクト → シーン内の順に探し、無ければこのオブジェクトに追加する")]
	[SerializeField] private TrackingLossMonitor _monitor;

	[Header("メッセージ")]
	[Tooltip("人物を検出できないときのメッセージ")]
	[SerializeField, TextArea] private string _personLostMessage = "カメラの前に立ってください";
	[Tooltip("Pythonから何も届かないときのメッセージ")]
	[SerializeField, TextArea] private string _disconnectedMessage = "カメラとの接続が切れています\n<size=60%>スタッフを呼んでください</size>";

	[Header("見た目（自動で作るUIに使う）")]
	[Tooltip("文字のフォント。日本語が入ったフォントを指定する（追加したときに NotoSerifJP-Bold SDF を自動で探して入れる）")]
	[SerializeField] private TMP_FontAsset _font;
	[Tooltip("文字の大きさ（1920x1080基準）")]
	[SerializeField, Min(1f)] private float _fontSize = 64f;
	[Tooltip("文字の色")]
	[SerializeField] private Color _textColor = Color.white;
	[Tooltip("帯の色")]
	[SerializeField] private Color _bandColor = new Color(0.6f, 0.05f, 0.05f, 0.85f);
	[Tooltip("帯の高さ（1920x1080基準）")]
	[SerializeField, Min(1f)] private float _bandHeight = 220f;
	[Tooltip("Canvasの描画順（大きいほど手前）。他のUIより手前に出す")]
	[SerializeField] private int _sortingOrder = 1000;

	[Header("アニメーション")]
	[Tooltip("出る・消えるときのフェードの秒数")]
	[SerializeField, Min(0.01f)] private float _fadeDuration = 0.2f;
	[Tooltip("警告中に文字を点滅させる周期（秒）。0なら点滅しない")]
	[SerializeField, Min(0f)] private float _blinkPeriod = 1.2f;
	[Tooltip("点滅したときの文字の一番薄い透明度")]
	[SerializeField, Range(0f, 1f)] private float _blinkMinAlpha = 0.4f;

	[Header("UIの参照（未指定なら自動で作る）")]
	[Tooltip("警告全体の表示・非表示に使う CanvasGroup")]
	[SerializeField] private CanvasGroup _canvasGroup;
	[Tooltip("メッセージを出すテキスト")]
	[SerializeField] private TMP_Text _messageText;

	private void Awake() {
		// 判定：見張り役が未設定か（同じオブジェクト → シーン内 → 追加 の順に用意する）
		if (_monitor == null) {
			_monitor = GetComponent<TrackingLossMonitor>();
		}
		if (_monitor == null) {
			_monitor = FindFirstObjectByType<TrackingLossMonitor>();
		}
		if (_monitor == null) {
			_monitor = gameObject.AddComponent<TrackingLossMonitor>();
		}

		// 判定：UIの参照が未設定か（どちらかが無ければ自動で作る）
		if (_canvasGroup == null || _messageText == null) {
			BuildUI();
		}

		_canvasGroup.alpha = 0f;
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;
	}

	private void Update() {
		bool warning = _monitor.IsWarning;

		// 判定：警告中か（警告中だけメッセージを更新する。消えるときは前のメッセージのままフェードアウト）
		if (warning) {
			_messageText.text = _monitor.State == TrackingLossState.Disconnected ? _disconnectedMessage : _personLostMessage;

			// 文字の点滅（unscaledTimeで、timeScaleの影響を受けない）
			float alpha = 1f;
			// 判定：点滅させるか
			if (_blinkPeriod > 0f) {
				float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / _blinkPeriod);
				alpha = Mathf.Lerp(_blinkMinAlpha, 1f, wave);
			}
			_messageText.alpha = alpha;
		}

		float target = warning ? 1f : 0f;
		_canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, Time.unscaledDeltaTime / _fadeDuration);
	}

	// 警告用のCanvas・帯・テキストを自分の子に作る。
	private void BuildUI() {
		// Canvas（Screen Space - Overlay。1920x1080基準で拡大縮小）
		GameObject canvasObject = new GameObject("TrackingLossWarningCanvas", typeof(RectTransform));
		canvasObject.transform.SetParent(transform, false);
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = _sortingOrder;
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		_canvasGroup = canvasObject.AddComponent<CanvasGroup>();

		// 帯（画面の横幅いっぱい、縦は中央）
		GameObject bandObject = new GameObject("Band", typeof(RectTransform));
		bandObject.transform.SetParent(canvasObject.transform, false);
		RectTransform bandRect = (RectTransform)bandObject.transform;
		bandRect.anchorMin = new Vector2(0f, 0.5f);
		bandRect.anchorMax = new Vector2(1f, 0.5f);
		bandRect.sizeDelta = new Vector2(0f, _bandHeight);
		bandRect.anchoredPosition = Vector2.zero;
		Image bandImage = bandObject.AddComponent<Image>();
		bandImage.color = _bandColor;
		bandImage.raycastTarget = false;

		// メッセージ（帯の中央）
		GameObject textObject = new GameObject("Message", typeof(RectTransform));
		textObject.transform.SetParent(bandObject.transform, false);
		RectTransform textRect = (RectTransform)textObject.transform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;
		TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
		// 判定：フォントが指定されているか（無ければTMPの既定のフォント。日本語が出ないので警告する）
		if (_font != null) {
			text.font = _font;
		} else {
			Debug.LogWarning("TrackingLossWarningView: フォントが未指定です。日本語が表示されない場合は Font に日本語フォントを指定してください", this);
		}
		text.fontSize = _fontSize;
		text.color = _textColor;
		text.alignment = TextAlignmentOptions.Center;
		text.raycastTarget = false;
		_messageText = text;
	}

#if UNITY_EDITOR
	// コンポーネントを追加したとき、日本語フォント（NotoSerifJP-Bold SDF）をプロジェクトから探して入れる。
	private void Reset() {
		string[] guids = UnityEditor.AssetDatabase.FindAssets("NotoSerifJP-Bold SDF t:TMP_FontAsset");
		// 判定：見つかったか
		if (guids.Length > 0) {
			string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
			_font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
		}
	}
#endif
}
