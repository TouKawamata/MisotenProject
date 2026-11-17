using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// コピーしたGameSceneにだけ配置する。ゴール演出後のEnterで呼ばれる。
public sealed class RaceResultTransition : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(0.1f)] private float _closeSeconds = 0.65f;
    [SerializeField, Min(0f)] private float _titleHoldSeconds = 0.85f;
    private GameObject _overlay;

    public IEnumerator PlayExit()
    {
        // UIを消したゴール演出の背景を、黒帯で隠す前に保存する。
        yield return new WaitForEndOfFrame();
        ResultBackdropStore.Set(ScreenCapture.CaptureScreenshotAsTexture());
        _overlay = new GameObject("Result Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = _overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = _overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform top = Panel("Upper shutter", new Vector2(0, 1150), new Vector2(3000, 1400), Color.black);
        RectTransform bottom = Panel("Lower shutter", new Vector2(0, -1150), new Vector2(3000, 1400), Color.black);
        top.localRotation = bottom.localRotation = Quaternion.Euler(0, 0, 5);
        var title = Label("RACE CLEARED", new Vector2(0, 0), 100, Color.white);
        var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0;
        float elapsed = 0;
        while (elapsed < _closeSeconds)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / _closeSeconds);
            top.anchoredPosition = new Vector2(0, Mathf.Lerp(1450, 620, t));
            bottom.anchoredPosition = new Vector2(0, Mathf.Lerp(-1450, -620, t));
            titleGroup.alpha = Mathf.InverseLerp(0.3f, 0.8f, t);
            title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        top.anchoredPosition = new Vector2(0, 620);
        bottom.anchoredPosition = new Vector2(0, -620);
        titleGroup.alpha = 1;
        title.rectTransform.localScale = Vector3.one;
        title.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
        RectTransform stripe = Panel("Yellow slash", new Vector2(-2400, -140), new Vector2(3000, 110), new Color(1, 0.94f, 0));
        stripe.localRotation = Quaternion.Euler(0, 0, 5);
        var subtitle = Label("RACE RESULT", new Vector2(0, -140), 36, Color.black);
        subtitle.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
        subtitle.gameObject.SetActive(false);
        elapsed = 0;
        while (elapsed < 0.3f)
        {
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.3f), 3f);
            stripe.anchoredPosition = new Vector2(Mathf.Lerp(-3000, 0, t), -140);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        stripe.anchoredPosition = new Vector2(0, -140);
        subtitle.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(_titleHoldSeconds);
    }

    private RectTransform Panel(string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_overlay.transform, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI Label(string text, Vector2 position, float size, Color color)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_overlay.transform, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) label.font = _font;
        label.text = text; label.fontSize = size; label.color = color;
        label.fontStyle = FontStyles.Bold | FontStyles.Italic;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(1800, 200);
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    private void OnDisable()
    {
        if (_overlay != null) Destroy(_overlay);
        _overlay = null;
    }
}
