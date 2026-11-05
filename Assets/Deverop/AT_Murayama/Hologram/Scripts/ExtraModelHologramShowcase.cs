using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 追加の汎用3Dモデル用ホログラム4種（⑧GhostRim / ⑨Dissolve / ⑩TriplanarDots / ⑪GradientEnergy）を
/// インスペクター上のボタンからワンクリックで切り替えたり、4種横並び比較サンプルを生成できるコンポーネント。
/// ・子階層の複数Renderer（MeshRenderer / SkinnedMeshRenderer）および複数マテリアルスロットにも完全対応。
/// （上部 Tools メニューには一切項目を追加せず、すべてインスペクター内で完結します）
/// </summary>
[ExecuteAlways]
public class ExtraModelHologramShowcase : MonoBehaviour
{
    public enum ExtraHologramStyle
    {
        [InspectorName("⑧ GhostRim：輪郭発光＋呼吸パルス（最軽量ベース）")]
        GhostRim = 0,

        [InspectorName("⑨ Dissolve：出現・消滅ディゾルブ＋発光エッジ")]
        Dissolve = 1,

        [InspectorName("⑩ TriplanarDots：UV不要・3軸投影ドット点群")]
        TriplanarDots = 2,

        [InspectorName("⑪ GradientEnergy：高さグラデ＋上昇エネルギー")]
        GradientEnergy = 3
    }

    [Header("単体モデル用：スタイル切り替え")]
    [Tooltip("このオブジェクト（および子階層）の Renderer に適用する汎用ホログラムスタイル")]
    [SerializeField] private ExtraHologramStyle _style = ExtraHologramStyle.GhostRim;

    [Tooltip("子階層のすべてのRenderer（SkinnedMeshRenderer / MeshRenderer）と全マテリアルスロットに一括適用するか")]
    [SerializeField] private bool _includeChildRenderers = true;

    [Tooltip("⑨ Dissolve 選択時の消滅進行度（0 = 完全表示, 1 = 完全消滅）")]
    [Range(0f, 1f)]
    [SerializeField] private float _dissolveAmount = 0.0f;

    [Tooltip("⑨ Dissolve の自動往復ループ速度（0 なら上のスライダーで手動制御）")]
    [Range(0f, 2f)]
    [SerializeField] private float _dissolveAutoLoopSpeed = 0.35f;

    [Header("カスタムマテリアル（指定時は最優先使用）")]
    [Tooltip("指定時はこのマテリアルが最優先でRendererに割り当てられます。null時は上の設定に基づき自動生成されます。")]
    [SerializeField] private Material _customMaterial;

    [Header("別名保存用の設定")]
    [Tooltip("別名保存時のファイル名（未入力の場合は M_Holo_オブジェクト名 で自動生成）")]
    [SerializeField] private string _customSaveName = "";

    [Header("比較サンプル一括生成の設定")]
    [Tooltip("一括生成する比較サンプルのスケール（m）")]
    [SerializeField] private float _sampleScale = 2.2f;

    [Tooltip("一括生成する比較サンプル同士の間隔（m）")]
    [SerializeField] private float _spacing = 3.2f;

    private readonly List<Renderer> _targetRenderers = new List<Renderer>();
    private Material _runtimeMaterial;
    private ExtraHologramStyle _lastStyle = (ExtraHologramStyle)(-1);

    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int AutoLoopSpeedId  = Shader.PropertyToID("_AutoLoopSpeed");

    public float SampleScale => _sampleScale;
    public float Spacing => _spacing;

    public string CustomSaveName
    {
        get => _customSaveName;
        set => _customSaveName = value;
    }

    public Material CustomMaterial
    {
        get => _customMaterial;
        set
        {
            _customMaterial = value;
            UpdateSingleModelMaterial();
        }
    }

    private void OnEnable()
    {
        RefreshTargetRenderers();
        UpdateSingleModelMaterial();
    }

    private void OnDisable()
    {
        CleanupRuntimeMaterial();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMaterial();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                RefreshTargetRenderers();
                UpdateSingleModelMaterial();
            }
        };
#endif
    }

    public void SetStyle(ExtraHologramStyle style)
    {
        _style = style;
        UpdateSingleModelMaterial();
    }

    public ExtraHologramStyle GetStyle() => _style;

    private void RefreshTargetRenderers()
    {
        _targetRenderers.Clear();

        if (_includeChildRenderers)
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];
                if (r == null) continue;

                // 比較サンプル一括生成で作られた子オブジェクト（M_Model_08〜11）は個別マテリアルを維持するため除外
                if (r.gameObject != gameObject && r.gameObject.name.StartsWith("M_Model_"))
                {
                    continue;
                }

                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    _targetRenderers.Add(r);
                }
            }
        }
        else
        {
            Renderer selfRenderer = GetComponent<Renderer>();
            if (selfRenderer != null)
            {
                _targetRenderers.Add(selfRenderer);
            }
        }
    }

    /// <summary>
    /// 現在のインスペクター設定（スタイル・各種パラメータ）を反映した独立マテリアルインスタンスを新規生成
    /// （別名保存用）
    /// </summary>
    public Material CreateConfiguredMaterial()
    {
        string shaderName = GetShaderName(_style);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[ExtraModelHologramShowcase] シェーダー '{shaderName}' が見つかりません。");
            return null;
        }

        Material mat = new Material(shader);
        if (mat.HasProperty(DissolveAmountId))
        {
            mat.SetFloat(DissolveAmountId, _dissolveAmount);
        }
        if (mat.HasProperty(AutoLoopSpeedId))
        {
            mat.SetFloat(AutoLoopSpeedId, _dissolveAutoLoopSpeed);
        }
        return mat;
    }

    /// <summary>
    /// カスタムマテリアルの割り当てを解除し、インスペクター設定によるリアルタイム調整モードに戻す
    /// </summary>
    public void ClearCustomMaterial()
    {
        _customMaterial = null;
        UpdateSingleModelMaterial();
    }

    public void UpdateSingleModelMaterial()
    {
        if (_targetRenderers.Count == 0)
        {
            RefreshTargetRenderers();
        }
        if (_targetRenderers.Count == 0) return;

        if (_customMaterial != null)
        {
            AssignMaterialToAllRenderers(_customMaterial);
            return;
        }

        string shaderName = GetShaderName(_style);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) return;

        if (_runtimeMaterial == null || _lastStyle != _style || _runtimeMaterial.shader != shader)
        {
            CleanupRuntimeMaterial();
            _runtimeMaterial = new Material(shader)
            {
                name = $"M_Runtime_Extra_{_style}",
                hideFlags = HideFlags.DontSave
            };
            _lastStyle = _style;
        }

        if (_runtimeMaterial.HasProperty(DissolveAmountId))
        {
            _runtimeMaterial.SetFloat(DissolveAmountId, _dissolveAmount);
        }
        if (_runtimeMaterial.HasProperty(AutoLoopSpeedId))
        {
            _runtimeMaterial.SetFloat(AutoLoopSpeedId, _dissolveAutoLoopSpeed);
        }

        AssignMaterialToAllRenderers(_runtimeMaterial);
    }

    private void AssignMaterialToAllRenderers(Material mat)
    {
        if (mat == null) return;

        for (int i = 0; i < _targetRenderers.Count; i++)
        {
            Renderer r = _targetRenderers[i];
            if (r == null) continue;

            Material[] currentMats = r.sharedMaterials;
            int slotCount = (currentMats != null && currentMats.Length > 0) ? currentMats.Length : 1;

            if (slotCount == 1)
            {
                r.sharedMaterial = mat;
            }
            else
            {
                bool allMatch = true;
                for (int s = 0; s < slotCount; s++)
                {
                    if (currentMats[s] != mat)
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (!allMatch)
                {
                    Material[] newMats = new Material[slotCount];
                    for (int s = 0; s < slotCount; s++)
                    {
                        newMats[s] = mat;
                    }
                    r.sharedMaterials = newMats;
                }
            }

            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    public static string GetShaderName(ExtraHologramStyle style)
    {
        switch (style)
        {
            case ExtraHologramStyle.GhostRim:
                return "Custom/Hologram/ModelHologram_GhostRim";
            case ExtraHologramStyle.Dissolve:
                return "Custom/Hologram/ModelHologram_Dissolve";
            case ExtraHologramStyle.TriplanarDots:
                return "Custom/Hologram/ModelHologram_TriplanarDots";
            case ExtraHologramStyle.GradientEnergy:
                return "Custom/Hologram/ModelHologram_GradientEnergy";
            default:
                return "Custom/Hologram/ModelHologram_GhostRim";
        }
    }

    private void CleanupRuntimeMaterial()
    {
        if (_runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }
}
