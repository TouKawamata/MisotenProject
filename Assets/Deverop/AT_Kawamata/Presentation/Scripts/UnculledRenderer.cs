using UnityEngine;

// カメラの周りに描くシェーダー（村山くんのDistantOceanなど）を使うRendererに付ける。
// そのシェーダーは頂点シェーダーで描く位置をカメラの周りへ動かすが、画面に入るかの判定（カリング）は
// 置いた場所のBoundsで行われるため、カメラが板から離れたり別の方向を向いたりすると、描かれずに消えてしまう。
// RendererのBoundsを大きく上書きして、どのカメラ（Player・BackMirror・演出用）からもカリングされないようにする。
[RequireComponent(typeof(Renderer))]
public class UnculledRenderer : MonoBehaviour
{
    [Tooltip("上書きするBoundsの一辺の長さ（m）。コース全体と、カメラが動く範囲を含む大きさにする")]
    [Min(1f)]
    [SerializeField] private float _boundsSize = 100000f;

    private void OnEnable()
    {
        GetComponent<Renderer>().bounds = new Bounds(Vector3.zero, Vector3.one * _boundsSize);
    }

    private void OnDisable()
    {
        GetComponent<Renderer>().ResetBounds();
    }
}
