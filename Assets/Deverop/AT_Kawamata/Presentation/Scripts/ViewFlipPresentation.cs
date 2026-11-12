using UnityEngine;
using UnityEngine.Rendering;

// 視界反転（妨害弾など）の画面の演出。レーサーのEffects.ViewFlipを見て、カメラの映像を左右・上下に反転する。
// カメラの向き・位置（CameraController）は変えず、このカメラの描画の直前に投影行列だけを反転し、描画が終わったらすぐ戻す。
// 毎フレーム「今の投影（ブーストのFOVの演出も含む）」から作り直すので、FOVの演出と一緒に使える。
// 1軸だけ反転すると面の表裏が逆になるので、その間だけGL.invertCullingで裏返しを直す（左右＋上下なら180度回したのと同じなので不要）。
// UI（Screen Space - Overlay）はカメラを通らないので反転しない。
// 画面全体にかかる演出なので、Playerプレハブにだけ置く（CPUは入力が反転する。RacerManagerBase）。
public class ViewFlipPresentation : MonoBehaviour
{
    [SerializeField] private RacerManagerBase _racerManager;

    [Tooltip("反転するカメラ（PlayerのMain Camera）")]
    [SerializeField] private Camera _camera;

    private bool _isFlipping;
    private bool _isCullingInverted;

    private void OnEnable()
    {
        if (_racerManager == null || _camera == null)
        {
            Debug.LogError("ViewFlipPresentation: Racer Manager / Camera が未設定です", this);
            return;
        }

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Restore();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera)
        {
            return;
        }

        EViewFlip flip = _racerManager.Effects.ViewFlip;
        if (flip == EViewFlip.None)
        {
            Restore();
            return;
        }

        bool isHorizontal = (flip & EViewFlip.Horizontal) != 0;
        bool isVertical = (flip & EViewFlip.Vertical) != 0;

        // FOVなどの今の設定から投影を作り直してから、反転を掛ける。
        _camera.ResetProjectionMatrix();
        Matrix4x4 scale = Matrix4x4.Scale(new Vector3(isHorizontal ? -1f : 1f, isVertical ? -1f : 1f, 1f));
        _camera.projectionMatrix = scale * _camera.projectionMatrix;
        _isFlipping = true;

        _isCullingInverted = isHorizontal != isVertical;
        GL.invertCulling = _isCullingInverted;
    }

    // このカメラの描画が終わったら、すぐ元に戻す。他のカメラの描画が裏返らないように、
    // また他のスクリプト（WorldToScreenPointでUIの位置を決めるものなど）が、反転していない投影を使えるようにする。
    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera)
        {
            Restore();
        }
    }

    // 投影を元に戻す（FOVなどの設定がそのまま使われるようにする）。
    private void Restore()
    {
        if (_isCullingInverted)
        {
            GL.invertCulling = false;
            _isCullingInverted = false;
        }

        if (_isFlipping && _camera != null)
        {
            _camera.ResetProjectionMatrix();
        }

        _isFlipping = false;
    }
}
