using UnityEngine;

// CPUの個性＝「好み」。何をしたがるかを0～1の重みで決める（うまさはCpuDifficultyProfileが決める）。
// 難易度とはどの組み合わせでも使える（例：Easyのアタッカーは、よく狙うがよく外す）。
// 初期の5種類（アタッカー・ガーディアン・スピードスター・トリックスター・バランス）は、
// Tools > CPU > Create Default Profiles で作れる。重みを実際の距離・閾値にする換算は、各Brainの定数で行う。
[CreateAssetMenu(fileName = "CpuPersonality", menuName = "Race/CPU/Personality")]
public class CpuPersonalityProfile : ScriptableObject
{
    // 表示名が未設定のとき、アセット名からこの接頭辞を除いたものを表示名にする。
    private const string AssetNamePrefix = "CpuPersonality_";

    [Header("表示")]
    [Tooltip("CPUの頭上・Gizmosに出す名前。空ならアセット名から作る")]
    [SerializeField] private string _displayName;

    [Tooltip("CPUの頭上の名前・Gizmosの色")]
    [SerializeField] private Color _labelColor = Color.white;

    [Header("好み")]
    [Tooltip("攻撃性。高いほど遠くの相手まで狙い、少ないゲージでも攻撃のブーストを使う")]
    [Range(0f, 1f)]
    [SerializeField] private float _aggression = 0.5f;

    [Tooltip("慎重さ。高いほど後ろのブーストに早く気づき、シールドを長めに張る")]
    [Range(0f, 1f)]
    [SerializeField] private float _caution = 0.5f;

    [Tooltip("ブーストの使い方。0＝ため込む（満タン近くまで待ち、早めにやめて攻撃用に残す）、1＝すぐ使う（使い切るまで使う）")]
    [Range(0f, 1f)]
    [SerializeField] private float _boostEagerness = 0.5f;

    [Tooltip("走行ライン。0＝コース中央寄り、1＝外寄り（トンネルの壁の近く）")]
    [Range(0f, 1f)]
    [SerializeField] private float _preferredLine = 0.4f;

    [Tooltip("ライン変更の頻度。高いほどよく左右のラインを変える")]
    [Range(0f, 1f)]
    [SerializeField] private float _lineChangeFrequency = 0.5f;

    [Tooltip("執念（やり返し）。高いほど、自分にブーストを当てた相手を長く・優先して狙う")]
    [Range(0f, 1f)]
    [SerializeField] private float _vengefulness = 0.4f;

    [Tooltip("ショートカット志向。高いほどショートカットを狙う（難易度のショートカット成功率に掛かる）")]
    [Range(0f, 1f)]
    [SerializeField] private float _shortcutPreference = 0.6f;

    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name.Replace(AssetNamePrefix, "") : _displayName;

    public Color LabelColor => _labelColor;

    public float Aggression => _aggression;

    public float Caution => _caution;

    public float BoostEagerness => _boostEagerness;

    public float PreferredLine => _preferredLine;

    public float LineChangeFrequency => _lineChangeFrequency;

    public float Vengefulness => _vengefulness;

    public float ShortcutPreference => _shortcutPreference;
}
