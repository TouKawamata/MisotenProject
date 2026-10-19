using UnityEngine;

// shild_01の見た目。Playerの子のFrontShell／BackShellの中の、shild_01（伊藤さんのプレハブ）に付ける。
// BoostPresentation／ShieldPresentationからShow／Hide／Shatterを呼ばれ、表示を切り替えるだけ（当たり判定はShieldShellHitbox）。
// 砕ける演出（Shatter）は、シェーダーの頂点の揺れを使う。シェーダーでは
//   頂点の位置 ＝ 元の位置 ＋ sin(_VertexFurency × 経過時間) × (_VectorAmount × 法線)  （オブジェクト空間。全頂点が同じタイミングで膨らむ・縮む）
// なので、揺れの速さ（_VertexFurency）は砕け始めた瞬間に一気に切り替え（少しずつ変えると、経過時間が掛かっているせいで揺れが飛ぶ）、
// 揺れの大きさ（_VectorAmount）だけを一定時間かけて上げてから隠す。マテリアルのアセットは書き換えず、MaterialPropertyBlockで上書きする。
// _VectorAmountはオブジェクト空間の値なので、プレハブでのshild_01の拡大率（今は約180倍）が掛かる（0.01で約1.8m）。
public class ShieldShellView : MonoBehaviour
{
    private static readonly int VertexFrequencyId = Shader.PropertyToID("_VertexFurency");
    private static readonly int VectorAmountId = Shader.PropertyToID("_VectorAmount");

    [Tooltip("表示を切り替えるRenderer。未設定なら、自分と子のRendererをすべて使う")]
    [SerializeField] private Renderer[] _renderers;

    [Header("砕ける演出")]
    [Tooltip("揺れを上げてから消えるまでの秒数")]
    [SerializeField] private float _shatterDuration = 0.3f;
    [Tooltip("砕けるときの頂点の揺れの速さ（シェーダーの_VertexFurency。ラジアン毎秒）。砕け始めた瞬間にこの値へ切り替える。60で1秒に約10回。約190を超えると60fpsでは追いつかず、ちらつきに見える")]
    [SerializeField] private float _shatterVertexFrequency = 60f;
    [Tooltip("砕けるときの頂点の揺れの大きさ（シェーダーの_VectorAmount。x・y・z別々）。この値まで上げる。shild_01の拡大率が掛かるので、0.002～0.005くらいから試す")]
    [SerializeField] private Vector4 _shatterVectorAmount = new Vector4(0.003f, 0.003f, 0.003f, 0f);

    private MaterialPropertyBlock _propertyBlock;
    private float _baseVertexFrequency;
    private Vector4 _baseVectorAmount;
    private bool _isShattering;
    private float _shatterElapsed;

    private void Awake()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (_renderers.Length == 0)
        {
            Debug.LogError($"ShieldShellView: {name} にRendererがありません", this);
        }

        _propertyBlock = new MaterialPropertyBlock();
        CacheBaseValues();
        SetVisible(false);
    }

    private void Update()
    {
        if (!_isShattering)
        {
            return;
        }

        _shatterElapsed += Time.deltaTime;
        float t = _shatterDuration > 0f ? Mathf.Clamp01(_shatterElapsed / _shatterDuration) : 1f;
        ApplyShake(_shatterVertexFrequency, Vector4.Lerp(GetShatterStartAmount(), _shatterVectorAmount, t));

        if (t >= 1f)
        {
            _isShattering = false;
            ClearShake();
            SetVisible(false);
        }
    }

    // 張った（ブースト・シールドを始めた）とき。砕けている途中なら打ち切って、元の見た目で出す。
    public void Show()
    {
        _isShattering = false;
        ClearShake();
        SetVisible(true);
    }

    // 解除したとき。砕けている途中なら、最後まで見せてから隠す（ShieldBlockedの直後にShieldEndedも来るため）。
    public void Hide()
    {
        if (_isShattering)
        {
            return;
        }

        SetVisible(false);
    }

    // 防いで砕けるとき。揺れを上げてから隠す。
    public void Shatter()
    {
        SetVisible(true);
        _isShattering = true;
        _shatterElapsed = 0f;
    }

    // 調整用。Play中にInspectorのメニュー（⋮）から、防いだときと同じ演出を再生する（ブーストを当ててもらわなくても見られる）。
    [ContextMenu("砕ける演出を再生")]
    private void PlayShatterForTuning()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ShieldShellView: 砕ける演出はPlay中だけ再生できます", this);
            return;
        }

        Shatter();
    }

    // 揺れの大きさを上げ始める値。マテリアルの揺れの速さが0（普段は揺れていない）なら、マテリアルの大きさは見た目に出ていないので0から上げる。
    private Vector4 GetShatterStartAmount()
    {
        return Mathf.Approximately(_baseVertexFrequency, 0f) ? Vector4.zero : _baseVectorAmount;
    }

    // マテリアルに設定されている揺れの値を、砕ける演出の始まりの値として覚える。
    private void CacheBaseValues()
    {
        foreach (Renderer shellRenderer in _renderers)
        {
            Material material = shellRenderer != null ? shellRenderer.sharedMaterial : null;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(VertexFrequencyId))
            {
                _baseVertexFrequency = material.GetFloat(VertexFrequencyId);
            }

            if (material.HasProperty(VectorAmountId))
            {
                _baseVectorAmount = material.GetVector(VectorAmountId);
            }

            return;
        }
    }

    private void ApplyShake(float vertexFrequency, Vector4 vectorAmount)
    {
        _propertyBlock.Clear();
        _propertyBlock.SetFloat(VertexFrequencyId, vertexFrequency);
        _propertyBlock.SetVector(VectorAmountId, vectorAmount);
        foreach (Renderer shellRenderer in _renderers)
        {
            if (shellRenderer != null)
            {
                shellRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }

    private void ClearShake()
    {
        foreach (Renderer shellRenderer in _renderers)
        {
            if (shellRenderer != null)
            {
                shellRenderer.SetPropertyBlock(null);
            }
        }
    }

    private void SetVisible(bool isVisible)
    {
        foreach (Renderer shellRenderer in _renderers)
        {
            if (shellRenderer != null)
            {
                shellRenderer.enabled = isVisible;
            }
        }
    }
}
