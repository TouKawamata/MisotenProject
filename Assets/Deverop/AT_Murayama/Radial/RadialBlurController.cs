using UnityEngine;

[ExecuteAlways]
public class RadialBlurController : MonoBehaviour
{
    [Header("Radial Blur Controls")]
    [Range(0f, 1f)]
    public float strength = 0f;

    // シェーダー側の変更に合わせてプロパティ名を修正
    private static readonly int StrengthId = Shader.PropertyToID("_RadialBlurStrength");

    void OnValidate()
    {
        Shader.SetGlobalFloat(StrengthId, strength);
    }

    void Update()
    {
        Shader.SetGlobalFloat(StrengthId, strength);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(StrengthId, 0f);
    }
}