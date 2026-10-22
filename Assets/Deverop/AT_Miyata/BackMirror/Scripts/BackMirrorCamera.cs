using UnityEngine;

// バックミラー用の、後ろ向きのカメラ。Playerの位置＋オフセットに置き、Playerの向きの反対を向く。
// 描画先のRenderTextureは起動時に作る（アセットを用意しなくてよいように）。
// 描画の負荷を抑えるため、カメラはBackMirrorViewが表示している間だけ有効にする（SetRendering）。
// Playerの子にしないのは、PlayerのPrefab（AT_Kawamata）を変えないため。
[RequireComponent(typeof(Camera))]
public class BackMirrorCamera : MonoBehaviour
{
    [SerializeField] private RaceManager _raceManager;

    [Header("位置")]
    [Tooltip("Playerから見たカメラの位置（x=右、y=上、z=前）。Player自身の機体が映る場合は、前・上へずらすかNear Clipを大きくする")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, 0.5f);

    [Header("描画")]
    [Tooltip("RenderTextureの大きさ（ピクセル）。表示する枠の縦横比に合わせる")]
    [SerializeField] private Vector2Int _textureSize = new Vector2Int(640, 200);

    private Camera _camera;
    private RenderTexture _texture;

    public RenderTexture Texture => _texture;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _texture = new RenderTexture(Mathf.Max(1, _textureSize.x), Mathf.Max(1, _textureSize.y), 24)
        {
            name = "BackMirrorTexture"
        };
        _camera.targetTexture = _texture;
        _camera.enabled = false;

        if (_raceManager == null)
        {
            Debug.LogError("BackMirrorCamera: Race Manager が未設定です", this);
        }
    }

    private void OnDestroy()
    {
        if (_camera != null)
        {
            _camera.targetTexture = null;
        }

        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
        }
    }

    public void SetRendering(bool isRendering)
    {
        _camera.enabled = isRendering;
    }

    // Playerが動いた後（RaceManagerのTick後）に合わせる。
    private void LateUpdate()
    {
        IReadOnlyRacerData player = _raceManager != null ? _raceManager.PlayerData : null;
        if (player == null || player.Transform == null)
        {
            return;
        }

        Transform target = player.Transform;
        transform.SetPositionAndRotation(target.TransformPoint(_offset), Quaternion.LookRotation(-target.forward, target.up));
    }
}
