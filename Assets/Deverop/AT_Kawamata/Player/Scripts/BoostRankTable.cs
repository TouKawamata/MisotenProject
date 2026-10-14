using System;
using UnityEngine;

// 順位（上位何%か）ごとのブーストゲージの消費速度の表。下位ほど消費を遅く（＝長く使える）するのが想定。
// 順位の割合はRacerData.RankRatio（0＝トップ、1＝最下位）で、発動した瞬間の値で消費速度を確定する。
[CreateAssetMenu(fileName = "BoostRankTable", menuName = "Player/BoostRankTable")]
public class BoostRankTable : ScriptableObject
{
    [Serializable]
    private struct Row
    {
        [Tooltip("この行を使う順位の割合の上限（0＝トップ、1＝最下位）。例：0.25なら上位25%まで")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxRankRatio;
        [Tooltip("1秒あたりのゲージの消費量")]
        [SerializeField] private float _consumptionPerSecond;

        public Row(float maxRankRatio, float consumptionPerSecond)
        {
            _maxRankRatio = maxRankRatio;
            _consumptionPerSecond = consumptionPerSecond;
        }

        public float MaxRankRatio => _maxRankRatio;

        public float ConsumptionPerSecond => Mathf.Max(0.0001f, _consumptionPerSecond);
    }

    private const float FallbackConsumptionPerSecond = 30f;

    [Tooltip("上限の小さい順に並べる。上から見て、順位の割合が上限以下になった最初の行を使う（どの行にも当てはまらなければ最後の行）")]
    [SerializeField] private Row[] _rows =
    {
        new Row(0.25f, 40f),
        new Row(0.5f, 33f),
        new Row(0.75f, 28f),
        new Row(1f, 25f),
    };

    public float EvaluateConsumptionPerSecond(float rankRatio)
    {
        if (_rows == null || _rows.Length == 0)
        {
            return FallbackConsumptionPerSecond;
        }

        foreach (Row row in _rows)
        {
            if (rankRatio <= row.MaxRankRatio)
            {
                return row.ConsumptionPerSecond;
            }
        }

        return _rows[_rows.Length - 1].ConsumptionPerSecond;
    }
}
