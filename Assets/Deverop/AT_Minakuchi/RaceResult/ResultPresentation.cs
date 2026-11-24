using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// 数値はResultViewが読むだけ。こちらは配置と登場演出だけを担当する。
public sealed class ResultPresentation : MonoBehaviour
{
    [SerializeField] private RawImage _backdrop;
    [SerializeField] private RectTransform _introCover;
    [SerializeField] private CanvasGroup _introGroup;
    [SerializeField] private RectTransform[] _revealItems;
    [SerializeField] private CanvasGroup _continueHint;
    [SerializeField] private EnterKeySceneLoader _sceneLoader;
    [SerializeField] private RectTransform _continueLabel;
    [SerializeField, Min(0.1f)] private float _confirmSeconds = 0.42f;
    [SerializeField] private AnimationCurve _confirmBounce = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.17f, -8f),
        new Keyframe(0.43f, 22f), new Keyframe(0.72f, -5f), new Keyframe(1f, 0f));
    [SerializeField, Min(0f)] private float _introHoldSeconds = 0.25f;
    private Texture2D _ownedBackdrop;
    private Vector2[] _positions;
    private CanvasGroup[] _groups;
    private bool _readyForReturn;
    private bool _returning;

    private void Awake()
    {
        _ownedBackdrop = ResultBackdropStore.Take();
        if (_ownedBackdrop != null)
        {
            _backdrop.texture = _ownedBackdrop;
            // 画面比率が違っても背景を引き伸ばさず、中央をトリミングする。
            float imageAspect = (float)_ownedBackdrop.width / _ownedBackdrop.height;
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float width = Mathf.Min(1f, screenAspect / imageAspect);
            float height = Mathf.Min(1f, imageAspect / screenAspect);
            _backdrop.uvRect = new Rect((1f - width) / 2, (1f - height) / 2, width, height);
        }
        else _backdrop.gameObject.SetActive(false);
        _positions = new Vector2[_revealItems.Length];
        _groups = new CanvasGroup[_revealItems.Length];
        for (int i = 0; i < _revealItems.Length; i++)
        {
            _positions[i] = _revealItems[i].anchoredPosition;
            _groups[i] = _revealItems[i].GetComponent<CanvasGroup>();
            if (_groups[i] == null) _groups[i] = _revealItems[i].gameObject.AddComponent<CanvasGroup>();
            _groups[i].alpha = 0;
        }
        _continueHint.alpha = 0;
        // 登場中のEnterで画面が一瞬で飛ばないようにする。
        _sceneLoader.enabled = false;
    }

    private IEnumerator Start()
    {
        _introCover.gameObject.SetActive(true);
        _introGroup.alpha = 1;
        yield return new WaitForSecondsRealtime(_introHoldSeconds);
        float elapsed = 0;
        const float duration = 1.6f;
        while (elapsed < duration)
        {
            float open = Mathf.Clamp01(elapsed / 0.6f);
            _introCover.anchoredPosition = new Vector2(2500 * EaseOut(open), 0);
            _introGroup.alpha = 1f - Mathf.InverseLerp(0.25f, 0.55f, elapsed);
            for (int i = 0; i < _revealItems.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - 0.2f - i * 0.13f) / 0.45f);
                _groups[i].alpha = t;
                _revealItems[i].anchoredPosition = _positions[i] + new Vector2(-220 * (1 - EaseOut(t)), 0);
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        for (int i = 0; i < _revealItems.Length; i++)
        {
            _groups[i].alpha = 1;
            _revealItems[i].anchoredPosition = _positions[i];
        }
        _introCover.gameObject.SetActive(false);
        _continueHint.alpha = 1;
        _readyForReturn = true;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            RequestReturn();
    }

    // Enterの処理をここで受け、押した反応を見せてから既存のローダーを呼ぶ。
    public void RequestReturn()
    {
        if (!_readyForReturn || _returning) return;
        _returning = true;
        StartCoroutine(ConfirmAndReturn());
    }

    private IEnumerator ConfirmAndReturn()
    {
        RectTransform label = _continueLabel != null ? _continueLabel : (RectTransform)_continueHint.transform;
        Vector2 position = label.anchoredPosition;
        Vector3 scale = label.localScale;
        float elapsed = 0f;
        while (elapsed < _confirmSeconds)
        {
            float t = Mathf.Clamp01(elapsed / _confirmSeconds);
            float bounce = _confirmBounce.Evaluate(t);
            label.anchoredPosition = position + Vector2.up * bounce;
            label.localScale = Vector3.Scale(scale, new Vector3(1f + bounce * 0.002f, 1f + bounce * 0.003f, 1f));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        label.anchoredPosition = position;
        label.localScale = scale;
        _sceneLoader.LoadNext();
        // シーン設定の不備で遷移できなかった場合は再入力できる。
        _returning = false;
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private void OnDestroy()
    {
        if (_ownedBackdrop != null) Destroy(_ownedBackdrop);
    }
}
