using UnityEngine;

// 先頭から離されているほど、ブーストのゲージの消費を遅く（＝長く使えるように）する補正の設定。Playerだけに使う（PlayerManagerに割り当てる）。
// BoostRankTable（順位ごとの消費速度。CPUと共通）の値に、ここの倍率を掛ける。倍率は発動した瞬間の距離で確定する。
[CreateAssetMenu(fileName = "BoostCatchUpProfile", menuName = "Player/BoostCatchUpProfile")]
public class BoostCatchUpProfile : ScriptableObject
{
    [Tooltip("先頭との差（m）がこれ以下なら、補正をかけない")]
    [Min(0f)]
    [SerializeField] private float _startGap = 20f;
    [Tooltip("先頭との差（m）がこれ以上なら、補正を最大にする。間は直線的に変わる")]
    [Min(0f)]
    [SerializeField] private float _fullGap = 120f;
    [Tooltip("補正が最大のときの、ゲージの消費速度の倍率。小さいほど長く使える。1なら補正なし")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _minConsumptionMultiplier = 0.6f;

    // gapToLeader：先頭との差（m）。自分が先頭なら0以下。
    public float EvaluateConsumptionMultiplier(float gapToLeader)
    {
        // 最大になる差が効き始める差以下に設定されていても、そこで切り替わるだけにする（0で割らない）。
        float range = Mathf.Max(0.0001f, _fullGap - _startGap);
        float rate = Mathf.Clamp01((gapToLeader - _startGap) / range);
        return Mathf.Lerp(1f, _minConsumptionMultiplier, rate);
    }
}
