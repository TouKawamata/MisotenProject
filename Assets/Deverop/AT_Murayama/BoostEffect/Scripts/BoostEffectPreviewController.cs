using UnityEngine;

/// <summary>
/// 【デバッグ・見た目確認用】
/// ラジアルブラーと集中線の見た目を、ゲーム非実行時（エディタ上）でも
/// スライダーで即座にプレビュー・微調整するためのコントローラー。
/// （本番のゲームロジックには影響しません）
/// </summary>
[ExecuteAlways]
public class BoostEffectPreviewController : MonoBehaviour
{
    [Header("プレビュー操作（スライダーを動かすと即反映）")]
    [Tooltip("ラジアルブラーの強さ（0〜1）")]
    [Range(0f, 1f)]
    public float radialBlurStrength = 0f;

    [Tooltip("集中線の強さ（0〜1）")]
    [Range(0f, 1f)]
    public float speedLineStrength = 0f;

    [Header("テスト連動（任意）")]
    [Tooltip("Play/Stop のアニメーションをテストしたい場合、ここに割り当ててください")]
    [SerializeField] private RadialBlurEffect _radialBlurEffect;
    [SerializeField] private SpeedLineEffect _speedLineEffect;

    private static readonly int RadialBlurStrengthId = Shader.PropertyToID("_RadialBlurStrength");
    private static readonly int SpeedLineStrengthId  = Shader.PropertyToID("_SpeedLineStrength");

    private void OnEnable()
    {
        // スクリプトのチェックをONにした時、即座にスライダー値を反映（ゲーム実行中・エディタ非実行中問わず）
        ApplyStrength();
    }

    private void OnDisable()
    {
        // スクリプトのチェックをOFFにした時、またはオブジェクト非アクティブ時は即座に0に戻す
        ResetGlobals();
    }

    private void OnValidate()
    {
        // スクリプトが有効な時だけスライダー値を反映し、無効時は0に保つ
        if (isActiveAndEnabled)
        {
            ApplyStrength();
        }
        else
        {
            ResetGlobals();
        }
    }

    private void Update()
    {
        // スクリプトが有効な間は、スライダーの値を常にリアルタイム反映（ゲーム実行中もスライダーで確認可能）
        if (isActiveAndEnabled)
        {
            ApplyStrength();
        }
    }

    public void ApplyStrength()
    {
        Shader.SetGlobalFloat(RadialBlurStrengthId, radialBlurStrength);
        Shader.SetGlobalFloat(SpeedLineStrengthId, speedLineStrength);
    }

    public void ResetGlobals()
    {
        Shader.SetGlobalFloat(RadialBlurStrengthId, 0f);
        Shader.SetGlobalFloat(SpeedLineStrengthId, 0f);
    }

    // --- 右クリックメニューまたはInspectorから呼べるテストボタン ---
    [ContextMenu("テスト: Play（ブースト開始演出）")]
    public void TestPlay()
    {
        if (_radialBlurEffect != null) _radialBlurEffect.Play();
        if (_speedLineEffect != null) _speedLineEffect.Play();
    }

    [ContextMenu("テスト: Stop（ブースト終了演出）")]
    public void TestStop()
    {
        if (_radialBlurEffect != null) _radialBlurEffect.Stop();
        if (_speedLineEffect != null) _speedLineEffect.Stop();
    }
}

