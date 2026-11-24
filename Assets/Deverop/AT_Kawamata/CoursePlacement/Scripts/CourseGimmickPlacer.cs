using System;
using System.Collections.Generic;
using UnityEngine;

// コースギミック（CourseGimmickBaseを継承したもののプレハブ）を、種類ごとの設定でコースに沿ってまとめて置く。
// Inspectorの「生成」ボタンで置く（実行時は何もしない）。種類ごとに Container Name/<名前> の子にまとめる。
// リストの上のギミックから順に置き、ギミックどうしが Min Spacing より近くなるものは置かない（上のものが優先）。
public sealed class CourseGimmickPlacer : CoursePlacerBase
{
    public enum CountMode
    {
        [InspectorName("個数")] Count,
        [InspectorName("間隔（m）")] Interval,
        [InspectorName("密度（100mあたりの個数）")] Density,
    }

    public enum TargetCourse
    {
        [InspectorName("本線と分岐")] MainAndBranches,
        [InspectorName("本線だけ")] MainOnly,
        [InspectorName("分岐だけ")] BranchesOnly,
    }

    public enum LateralMode
    {
        [InspectorName("コースの中心")] Center,
        [InspectorName("断面の円の中にランダム")] RandomInCircle,
        [InspectorName("角度の範囲で指定")] AngleRange,
    }

    [Serializable]
    public sealed class GimmickEntry
    {
        [Tooltip("名前。生成したものはこの名前の子オブジェクトにまとめる")]
        [SerializeField] private string _name = "Gimmick";

        [Tooltip("OFFにすると置かない（生成し直すと、前に置いたものも消える）")]
        [SerializeField, InspectorName("配置する")] private bool _enabled = true;

        [Tooltip("置くギミックのプレハブ。CourseGimmickBaseを継承したコンポーネントが付いているもの")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Course Networkを使うとき、どのコースに置くか")]
        [SerializeField] private TargetCourse _targetCourse = TargetCourse.MainAndBranches;

        [Header("数")]
        [Tooltip("数の決め方。個数・密度・間隔は、コース（本線・分岐）ごとに数える")]
        [SerializeField] private CountMode _countMode = CountMode.Count;

        [Tooltip("個数のとき：置く数。範囲を等分した区間ごとに1つずつ、区間の中のランダムな位置に置く")]
        [Min(0)]
        [SerializeField] private int _count = 5;

        [Tooltip("間隔のとき：置く間隔（m）")]
        [Min(1f)]
        [SerializeField] private float _interval = 100f;

        [Tooltip("密度のとき：100mあたりの個数")]
        [Min(0f)]
        [SerializeField] private float _densityPer100m = 1f;

        [Tooltip("間隔のとき：前後へずらす最大量（m）")]
        [Min(0f)]
        [SerializeField] private float _distanceJitter = 0f;

        [Header("範囲")]
        [Tooltip("本線のスタートからの距離（m）。ここより手前には置かない（全体の Avoid Start Distance より手前にも置かない）。分岐は本線の距離に直して比べる")]
        [Min(0f)]
        [SerializeField] private float _startDistance = 0f;

        [Tooltip("ここより先には置かない（本線の距離、m）。0以下ならコースの最後まで")]
        [SerializeField] private float _endDistance = 0f;

        [Header("断面のどこに置くか")]
        [SerializeField] private LateralMode _lateralMode = LateralMode.RandomInCircle;

        [Tooltip("中心からの距離を、その場所のトンネル半径の割合で（0=中心、1=壁）。この範囲でランダム")]
        [SerializeField] private Vector2 _radiusRatio = new Vector2(0f, 0.6f);

        [Tooltip("角度の範囲で指定するとき：0=右、90=上、180=左、270=下（度）。この範囲でランダム")]
        [SerializeField] private Vector2 _angleRange = new Vector2(0f, 360f);

        [Header("向き・大きさ")]
        [Tooltip("ON：プレハブの+Zをコースの進行方向、+Yをコースの上に合わせる。OFF：プレハブの向きのまま")]
        [SerializeField] private bool _alignToCourse = true;

        [Tooltip("進行方向を軸にランダムに回す")]
        [SerializeField] private bool _randomRoll = false;

        [Tooltip("大きさの倍率の範囲（プレハブの大きさに掛ける）。この範囲でランダム")]
        [SerializeField] private Vector2 _scaleRange = Vector2.one;

        public string Name => string.IsNullOrEmpty(_name) ? (_prefab != null ? _prefab.name : "Gimmick") : _name;
        public bool Enabled => _enabled;
        public GameObject Prefab => _prefab;
        public TargetCourse Target => _targetCourse;
        public CountMode Mode => _countMode;
        public int Count => _count;
        public float Interval => _interval;
        public float DensityPer100m => _densityPer100m;
        public float DistanceJitter => _distanceJitter;
        public float StartDistance => _startDistance;
        public float EndDistance => _endDistance;
        public LateralMode Lateral => _lateralMode;
        public Vector2 RadiusRatio => _radiusRatio;
        public Vector2 AngleRange => _angleRange;
        public bool AlignToCourse => _alignToCourse;
        public bool RandomRoll => _randomRoll;
        public Vector2 ScaleRange => _scaleRange;

        // プレハブにCourseGimmickBaseが付いているか（エディタの警告用）。
        public bool HasGimmickComponent => _prefab != null && _prefab.GetComponentInChildren<CourseGimmickBase>(true) != null;

        public void Validate()
        {
            _radiusRatio.x = Mathf.Clamp01(_radiusRatio.x);
            _radiusRatio.y = Mathf.Clamp(_radiusRatio.y, _radiusRatio.x, 1f);
            _angleRange.y = Mathf.Max(_angleRange.x, _angleRange.y);
            _scaleRange.x = Mathf.Max(0.01f, _scaleRange.x);
            _scaleRange.y = Mathf.Max(_scaleRange.x, _scaleRange.y);
        }
    }

    [Header("全体の設定")]
    [Tooltip("スタートからこの距離（m）までは、どのギミックも置かない")]
    [Min(0f)]
    [SerializeField] private float _avoidStartDistance = 50f;

    [Tooltip("ギミックどうし（種類を問わない）をこれより近くに置かない（m）。0なら調べない")]
    [Min(0f)]
    [SerializeField] private float _minSpacing = 10f;

    [Header("ギミックの種類")]
    [SerializeField] private GimmickEntry[] _entries = new GimmickEntry[0];

    public IReadOnlyList<GimmickEntry> Entries => _entries;

    private void Reset()
    {
        _containerName = "_GeneratedGimmicks";
    }

    private void OnValidate()
    {
        if (_entries == null)
        {
            return;
        }

        foreach (GimmickEntry entry in _entries)
        {
            entry?.Validate();
        }
    }

    public override List<CoursePlacementItem> BuildPlacements()
    {
        List<CoursePlacementItem> result = new List<CoursePlacementItem>();
        if (!CanBuild(out _) || _entries == null)
        {
            return result;
        }

        List<CourseTarget> targets = GetTargets();
        CourseSpline mainSpline = MainSpline;

        for (int i = 0; i < _entries.Length; i++)
        {
            GimmickEntry entry = _entries[i];
            if (entry == null || !entry.Enabled || entry.Prefab == null)
            {
                continue;
            }

            for (int t = 0; t < targets.Count; t++)
            {
                CourseTarget target = targets[t];
                if ((entry.Target == TargetCourse.MainOnly && target.IsBranch) ||
                    (entry.Target == TargetCourse.BranchesOnly && !target.IsBranch))
                {
                    continue;
                }

                // 種類・コースごとに乱数を分ける（他の種類やコースの設定を変えても、この配置は変わらない）。
                int seed = unchecked(_seed * 486187739 + (i + 1) * 7919 + StableHash(target.Name) * 31);
                BuildEntry(entry, target, mainSpline, new System.Random(seed), result);
            }
        }

        return result;
    }

    private void BuildEntry(GimmickEntry entry, CourseTarget target, CourseSpline mainSpline, System.Random rng, List<CoursePlacementItem> result)
    {
        if (!target.TryGetRange(mainSpline, Mathf.Max(entry.StartDistance, _avoidStartDistance), entry.EndDistance, out float start, out float end))
        {
            return;
        }

        CourseSpline spline = target.Spline;
        foreach (float distance in GetDistances(entry, start, end, rng))
        {
            // 乱数の消費回数を一定にするため、置かないときも先に全部引いておく。
            float u = (float)rng.NextDouble();
            float angle01 = (float)rng.NextDouble();
            float roll = CoursePlacementUtility.RandomRange(rng, 0f, 360f);
            float scale = CoursePlacementUtility.RandomRange(rng, entry.ScaleRange.x, entry.ScaleRange.y);

            CoursePlacementUtility.CourseFrame frame = CoursePlacementUtility.GetFrame(spline, distance);
            Vector3 position = frame.Center;

            switch (entry.Lateral)
            {
                case LateralMode.RandomInCircle:
                {
                    // 面積あたりの密度がそろうように、半径は2乗で補間する。
                    float minSq = entry.RadiusRatio.x * entry.RadiusRatio.x;
                    float maxSq = entry.RadiusRatio.y * entry.RadiusRatio.y;
                    float radius = Mathf.Sqrt(Mathf.Lerp(minSq, maxSq, u)) * frame.Radius;
                    position = frame.PointAround(angle01 * 360f, radius);
                    break;
                }
                case LateralMode.AngleRange:
                {
                    float radius = Mathf.Lerp(entry.RadiusRatio.x, entry.RadiusRatio.y, u) * frame.Radius;
                    position = frame.PointAround(Mathf.Lerp(entry.AngleRange.x, entry.AngleRange.y, angle01), radius);
                    break;
                }
            }

            if (_minSpacing > 0f && IsTooClose(position, result))
            {
                continue;
            }

            Quaternion rotation = entry.AlignToCourse ? frame.Rotation : entry.Prefab.transform.rotation;
            if (entry.RandomRoll)
            {
                rotation = Quaternion.AngleAxis(roll, entry.AlignToCourse ? frame.Forward : rotation * Vector3.forward) * rotation;
            }

            result.Add(new CoursePlacementItem(entry.Prefab, position, rotation, scale, float.NaN, entry.Name));
        }
    }

    // 置く距離の一覧。個数・密度は範囲を等分した区間ごとに1つ（区間の中でランダム）、間隔は等間隔＋ばらつき。
    private static IEnumerable<float> GetDistances(GimmickEntry entry, float start, float end, System.Random rng)
    {
        float length = end - start;

        int count;
        switch (entry.Mode)
        {
            case CountMode.Interval:
            {
                int intervalCount = Mathf.FloorToInt(length / entry.Interval);
                float offset = (length - intervalCount * entry.Interval) * 0.5f + entry.Interval * 0.5f;
                for (int i = 0; i < intervalCount; i++)
                {
                    float jitter = CoursePlacementUtility.RandomRange(rng, -entry.DistanceJitter, entry.DistanceJitter);
                    yield return Mathf.Clamp(start + offset + i * entry.Interval + jitter, start, end);
                }

                yield break;
            }
            case CountMode.Density:
                count = Mathf.RoundToInt(length / 100f * entry.DensityPer100m);
                break;
            default:
                count = entry.Count;
                break;
        }

        if (count <= 0)
        {
            yield break;
        }

        float slot = length / count;
        for (int i = 0; i < count; i++)
        {
            yield return start + (i + (float)rng.NextDouble()) * slot;
        }
    }

    // 実行ごとに変わらない文字列のハッシュ（string.GetHashCodeは環境によって変わることがあるため）。
    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in text)
            {
                hash = hash * 31 + c;
            }

            return hash;
        }
    }

    private bool IsTooClose(Vector3 position, List<CoursePlacementItem> placed)
    {
        float minSq = _minSpacing * _minSpacing;
        for (int i = 0; i < placed.Count; i++)
        {
            if ((placed[i].Position - position).sqrMagnitude < minSq)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    protected override Color PreviewColor => new Color(1f, 0.4f, 0.3f, 0.9f);
#endif
}
