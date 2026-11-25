using UnityEngine;
using UnityEngine.Serialization;

// CPUの難易度＝「うまさ」。反応の速さ・操作の正確さ・成功率など、同じことをしようとしたときにどれだけ正確にできるかを決める。
// 何をしたがるか（攻撃・守備など）はCpuPersonalityProfile（個性）が決めるので、ここには入れない。
// Easy／Normal／Hardの3アセットを作って使う（Tools > CPU > Create Default Profiles で初期値のアセットを作れる）。
// CPUごとにCpuRaceInputへ入れるか、CpuDifficultyControllerで全CPUへ一括で設定する。
[CreateAssetMenu(fileName = "CpuDifficulty", menuName = "Race/CPU/Difficulty")]
public class CpuDifficultyProfile : ScriptableObject
{
    // 表示名（CPUの頭上の表示）は、アセット名からこの接頭辞を除いたもの。
    private const string AssetNamePrefix = "CpuDifficulty_";

    [Header("判断")]
    [Tooltip("狙う相手・目標の位置・ブースト・妨害弾を考え直す間隔（秒）。短いほど状況の変化にすぐ対応する")]
    [SerializeField] private float _thinkInterval = 0.15f;

    [Tooltip("脅威（後ろからのブースト・飛んでくる妨害弾・爆発しそうな爆弾）に気づいてから、シールドを張るまでの秒数")]
    [SerializeField] private float _reactionTime = 0.35f;

    [Header("操作")]
    [Tooltip("目標の位置とのずれ（m）1つあたりのMoveの大きさ。大きいほど素早く目標へ寄せる")]
    [SerializeField] private float _steeringGain = 0.5f;

    [Tooltip("上下左右の速さ（m/s）1つあたり、Moveを打ち消す量。大きいほど目標の位置で行き過ぎにくい")]
    [SerializeField] private float _steeringDamping = 0.12f;

    [Tooltip("Moveに足すぶれの大きさ（0～1）。大きいほどふらつく")]
    [Range(0f, 1f)]
    [SerializeField] private float _steeringNoise = 0.12f;

    [Tooltip("ぶれの変わる速さ（回/秒くらい）")]
    [SerializeField] private float _steeringNoiseFrequency = 0.7f;

    [Header("攻撃")]
    [Tooltip("狙う相手と断面での位置（上下左右）がこの距離（m）以内にそろったら、攻撃のブーストを始める。大きいほど、そろっていないうちに始めて外しやすい")]
    [SerializeField] private float _attackAlignTolerance = 1.5f;

    [Header("ブースト")]
    [Tooltip("この角度（度）より先のコースが曲がっていたら、加速のためのブーストを使わない・やめる。大きいほどカーブを気にしない")]
    [SerializeField] private float _boostCurveLimit = 35f;

    [Tooltip("カーブを調べる距離（m）。この先のコースの向きと今の向きを比べる")]
    [SerializeField] private float _curveCheckDistance = 60f;

    [Tooltip("ゲージ（0～1）がこれ以下になったらブーストをやめる。0だと使い切ってオーバーヒートしやすい")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _boostReserve = 0.05f;

    [Header("成功率")]
    [Tooltip("脅威（後ろからのブースト・妨害弾・爆弾）に気づいたとき、シールドを張れる確率（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _shieldSuccessRate = 0.6f;

    [Header("コースのギミック")]
    [Tooltip("前方のギミック（加速リング・減速リング・爆弾・減速エリア）に気づく確率（0～1）。ギミックごとに1回だけ決め、見落としたものは避けない・くぐらない")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("_shortcutSuccessRate")]
    [SerializeField] private float _gimmickAwareness = 0.7f;

    [Tooltip("前方のギミックを見る距離（m）。長いほど早くから避ける・くぐる位置へ寄せる")]
    [SerializeField] private float _gimmickLookAhead = 55f;

    [Header("妨害弾")]
    [Tooltip("状況に合う妨害弾の種類を選ぶ確率（0～1）。それ以外のときは、でたらめに選ぶ")]
    [Range(0f, 1f)]
    [SerializeField] private float _jammerSmartness = 0.6f;

    [Tooltip("視界反転の妨害弾を受けてから、反転した操作に慣れるまでの秒数（その間は、移動が逆になる）。効果が切れたときも同じ秒数だけ戸惑う")]
    [SerializeField] private float _viewFlipAdaptTime = 0.8f;

    [Header("追い上げ補正（判断だけを変える。速度は変えない）")]
    [Tooltip("追い上げ補正の強さ（0～1）。0で無効。Playerより後ろなら積極的に、前なら控えめにブーストを使う")]
    [Range(0f, 1f)]
    [SerializeField] private float _catchUpStrength = 0.6f;

    [Tooltip("Playerとの前後の差がこの距離（m）で、補正が最大になる")]
    [SerializeField] private float _catchUpDistance = 150f;

    [Header("Playerへの恩恵（CPUのうまさではなく、この難易度のときにPlayerが得るもの）")]
    [Tooltip("ON：Playerの妨害弾がCPUに効いたとき、Playerのブーストゲージを回復する（防がれた・無敵中だったときは回復しない）")]
    [SerializeField] private bool _playerGainsBoostOnJammerHit = false;

    [Tooltip("1発当てたときに回復する、Playerのブーストゲージの量")]
    [Min(0f)]
    [SerializeField] private float _playerBoostGainOnJammerHit = 30f;

    public string DisplayName => name.Replace(AssetNamePrefix, "");

    public float ThinkInterval => Mathf.Max(0.02f, _thinkInterval);

    public float ReactionTime => Mathf.Max(0f, _reactionTime);

    public float SteeringGain => Mathf.Max(0f, _steeringGain);

    public float SteeringDamping => Mathf.Max(0f, _steeringDamping);

    public float SteeringNoise => _steeringNoise;

    public float SteeringNoiseFrequency => Mathf.Max(0f, _steeringNoiseFrequency);

    public float AttackAlignTolerance => Mathf.Max(0f, _attackAlignTolerance);

    public float BoostCurveLimit => Mathf.Max(0f, _boostCurveLimit);

    public float CurveCheckDistance => Mathf.Max(1f, _curveCheckDistance);

    public float BoostReserve => _boostReserve;

    public float ShieldSuccessRate => _shieldSuccessRate;

    public float GimmickAwareness => _gimmickAwareness;

    public float GimmickLookAhead => Mathf.Max(0f, _gimmickLookAhead);

    public float JammerSmartness => _jammerSmartness;

    public float ViewFlipAdaptTime => Mathf.Max(0f, _viewFlipAdaptTime);

    public float CatchUpStrength => _catchUpStrength;

    public float CatchUpDistance => Mathf.Max(1f, _catchUpDistance);

    public bool PlayerGainsBoostOnJammerHit => _playerGainsBoostOnJammerHit;

    public float PlayerBoostGainOnJammerHit => Mathf.Max(0f, _playerBoostGainOnJammerHit);
}
