using UnityEngine;
using UnityEngine.UI;

// バックミラーの表示（画面上部のRawImage）。BackMirrorTriggerを見てフェードし、見えている間だけカメラに描かせる。
public class BackMirrorView : MonoBehaviour
{
    [SerializeField] private BackMirrorTrigger _trigger;
    [SerializeField] private BackMirrorCamera _mirrorCamera;
    [SerializeField] private RawImage _rawImage;

    [Tooltip("ミラー全体（枠を含む）の透明度を変える")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("表示")]
    [Tooltip("出す／消すのにかける時間（秒）")]
    [SerializeField] private float _fadeDuration = 0.15f;

    [Tooltip("ONなら左右を反転する（本物の鏡と同じ向き。後ろの右側にいる相手が、ミラーの右側に映る）")]
    [SerializeField] private bool _flipHorizontal = true;

    private void Start()
    {
        if (_trigger == null || _mirrorCamera == null || _rawImage == null || _canvasGroup == null)
        {
            Debug.LogError("BackMirrorView: Trigger / Mirror Camera / Raw Image / Canvas Group が未設定です", this);
            enabled = false;
            return;
        }

        // RenderTextureはBackMirrorCameraのAwakeで作られる。
        _rawImage.texture = _mirrorCamera.Texture;
        _rawImage.uvRect = _flipHorizontal ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
        _canvasGroup.alpha = 0f;
        _mirrorCamera.SetRendering(false);
    }

    private void Update()
    {
        float target = _trigger.ShouldShow ? 1f : 0f;
        float step = _fadeDuration > 0f ? Time.deltaTime / _fadeDuration : 1f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, step);

        // 消えている間は描かない（負荷を無くす）。
        _mirrorCamera.SetRendering(_trigger.ShouldShow || _canvasGroup.alpha > 0f);
    }
}
