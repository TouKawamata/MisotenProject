using System.Collections.Generic;
using UnityEngine;

// コースに沿って矢印の看板を置く。Inspectorの「生成」ボタンで置く（実行時は何もしない）。
//
// ・直線：トンネルの壁から少し離して、壁と平行に置く（面をコースの中心へ向ける）。背景になじませる。矢印は前を指す。
// ・カーブ：インコース側の壁ぎわに、面をレーサーの来る方へ向けて置く。矢印はカーブの方向を指す。
// ・急な坂：左右に加えて、上下にも置く。
// 直線とカーブの間は、曲がり具合（Curve T、0=直線〜1=カーブ）で、間隔・壁からの距離・向きをなめらかに変える。
//
// 矢印の角度は、プレハブにSmartArrowSignageControllerがあればその Arrow Angle に入れる。無ければ看板を前方向を軸に回して、
// プレハブの+X方向を矢印の向きに合わせる。看板の表は-Z側（プレハブの+Z方向を見たときに見える面）とする。逆なら View From Positive Z をON。
public sealed class CourseSignboardPlacer : CoursePlacerBase
{
    public enum StraightSides
    {
        [InspectorName("左右の両方")] Both,
        [InspectorName("左右交互")] Alternate,
        [InspectorName("左だけ")] LeftOnly,
        [InspectorName("右だけ")] RightOnly,
    }

    [Header("看板")]
    [Tooltip("置く看板のプレハブ（SmartArrowSignageの看板など）")]
    [SerializeField] private GameObject _signPrefab;

    [Tooltip("看板の幅（m）。SmartArrowSignageControllerが付いていればその Sign Width を使う。レーサーの方へ向けた看板が、トンネルにはみ出さないようにするのに使う")]
    [Min(0f)]
    [SerializeField] private float _signWidth = 8f;

    [Tooltip("看板の高さ（m）。プレビューの枠の大きさにだけ使う（SmartArrowSignageControllerがあればその Sign Height）")]
    [Min(0f)]
    [SerializeField] private float _signHeight = 4.5f;

    [Tooltip("ON：看板の表が+Z側（プレハブを-Z方向から見る）。OFF：表が-Z側（プレハブの+Z方向を見たときに見える面）")]
    [SerializeField] private bool _viewFromPositiveZ = false;

    [Header("配置する範囲")]
    [Tooltip("本線のスタートからの距離（m）。ここより手前には置かない。分岐は、分岐の上の距離を本線の距離に直して比べる")]
    [Min(0f)]
    [SerializeField] private float _startDistance = 30f;

    [Tooltip("ここより先には置かない（本線の距離、m）。0以下ならコースの最後まで")]
    [SerializeField] private float _endDistance = 0f;

    [Tooltip("Course Networkを使うとき、分岐にも置くか")]
    [SerializeField] private bool _placeOnBranches = true;

    [Header("カーブの判定")]
    [Tooltip("曲がり具合を測る前後の距離（m）。大きいほど細かい揺れを無視する")]
    [Min(1f)]
    [SerializeField] private float _curvatureWindow = 15f;

    [Tooltip("1mあたりにこの角度（度）以下しか曲がらないところは直線とみなす")]
    [Min(0f)]
    [SerializeField] private float _straightTurnRate = 0.3f;

    [Tooltip("1mあたりにこの角度（度）以上曲がるところはカーブとみなす。直線との間はなめらかに変える")]
    [Min(0.01f)]
    [SerializeField] private float _curveTurnRate = 1.2f;

    [Header("直線")]
    [Tooltip("直線での看板の間隔（m）")]
    [Min(1f)]
    [SerializeField] private float _straightInterval = 60f;

    [Tooltip("直線で、トンネルの壁から看板の中心までの距離（m）。少し離すと背景になじむ")]
    [Min(0f)]
    [SerializeField] private float _straightWallOffset = 6f;

    [Tooltip("直線でどちら側に置くか")]
    [SerializeField] private StraightSides _straightSides = StraightSides.Alternate;

    [Tooltip("直線での看板の向き（度）。0＝壁と平行（面をコースの中心へ向ける）、90＝面をレーサーの来る方へ向ける")]
    [Range(0f, 90f)]
    [SerializeField] private float _straightFaceAngle = 0f;

    [Header("カーブ")]
    [Tooltip("カーブでの看板の間隔（m）")]
    [Min(1f)]
    [SerializeField] private float _curveInterval = 25f;

    [Tooltip("カーブのインコース側で、トンネルの壁から看板までの距離（m）。小さいほど壁ぎわ")]
    [Min(0f)]
    [SerializeField] private float _curveWallOffset = 1f;

    [Tooltip("カーブでの看板の向き（度）。0＝壁と平行、90＝面をレーサーの来る方へ向ける")]
    [Range(0f, 90f)]
    [SerializeField] private float _curveFaceAngle = 70f;

    [Tooltip("カーブでアウト側にも置くか")]
    [SerializeField] private bool _placeOutsideOnCurve = false;

    [Tooltip("アウト側に置くときの、トンネルの壁から看板までの距離（m）")]
    [Min(0f)]
    [SerializeField] private float _outsideWallOffset = 6f;

    [Header("急な坂（上下にも置く）")]
    [Tooltip("急な坂で上下にも看板を置くか")]
    [SerializeField] private bool _placeOnSlopes = true;

    [Tooltip("坂の角度（度）がこれ以上のところで上下にも置く")]
    [Range(1f, 89f)]
    [SerializeField] private float _slopeThreshold = 30f;

    [Tooltip("ON：急な下り坂にも置く。OFF：上り坂だけ")]
    [SerializeField] private bool _includeDescent = false;

    [Tooltip("上下の看板の間隔（m）")]
    [Min(1f)]
    [SerializeField] private float _slopeInterval = 40f;

    [Tooltip("上下の看板の、トンネルの壁からの距離（m）")]
    [Min(0f)]
    [SerializeField] private float _slopeWallOffset = 3f;

    [Header("矢印")]
    [Tooltip("矢印は、この距離（m）だけ先のコースの方向を指す")]
    [Min(1f)]
    [SerializeField] private float _arrowLookAhead = 30f;

    [Header("ばらつき・重なり")]
    [Tooltip("看板の位置を、壁に沿って上下（左右の看板）／左右（上下の看板）にずらす最大量（m）")]
    [Min(0f)]
    [SerializeField] private float _positionJitter = 0f;

    [Tooltip("このSplineのトンネルに入る位置には置かない。自分のコースの別の場所と、Course Networkの本線・分岐のトンネルに入る位置は、ここに入れなくても置かない")]
    [SerializeField] private CourseSpline[] _avoidSplines = new CourseSpline[0];

    [Tooltip("他のSplineのトンネルから、さらにこれだけ離す（m）")]
    [Min(0f)]
    [SerializeField] private float _avoidMargin = 1f;

    // 生成中だけ使う（今置いているコースと、重なりを調べるコース）。
    private readonly List<CourseSpline> _avoidBuffer = new List<CourseSpline>();
    private readonly List<CourseSpline> _ownBuffer = new List<CourseSpline>();
    private CourseSpline _currentSpline;
    private string _currentGroup = "";

    public GameObject SignPrefab => _signPrefab;

    private void Reset()
    {
        _containerName = "_GeneratedSigns";
    }

    public override bool CanBuild(out string reason)
    {
        if (!base.CanBuild(out reason))
        {
            return false;
        }

        if (_signPrefab == null)
        {
            reason = "Sign Prefab が未設定です";
            return false;
        }

        return true;
    }

    public override List<CoursePlacementItem> BuildPlacements()
    {
        List<CoursePlacementItem> result = new List<CoursePlacementItem>();
        if (!CanBuild(out _))
        {
            return result;
        }

        float halfWidth = GetSignWidth() * 0.5f;
        System.Random rng = new System.Random(_seed);

        // 重なりを調べるのは、置く・置かないに関係なく本線と分岐すべて。
        List<CourseSpline> allSplines = CourseTarget.SplinesOf(GetTargets());
        List<CourseTarget> targets = GetTargets(true, _placeOnBranches);
        CourseSpline mainSpline = MainSpline;

        foreach (CourseTarget target in targets)
        {
            if (!target.TryGetRange(mainSpline, _startDistance, _endDistance, out float start, out float end))
            {
                continue;
            }

            // 自分のコースは余白0で調べる（置く場所はいつも壁より外なので、その場所の断面では引っかからない。
            // 急カーブのインコースで、隣を通る自分のトンネルに入るものだけ外れる）。他のコースは余白をとって調べる。
            _currentSpline = target.Spline;
            _currentGroup = targets.Count > 1 ? target.Name : "";
            _ownBuffer.Clear();
            _ownBuffer.Add(target.Spline);
            _avoidBuffer.Clear();
            foreach (CourseSpline spline in allSplines)
            {
                if (spline != target.Spline)
                {
                    _avoidBuffer.Add(spline);
                }
            }

            foreach (CourseSpline spline in _avoidSplines)
            {
                if (spline != null && spline != target.Spline && !_avoidBuffer.Contains(spline))
                {
                    _avoidBuffer.Add(spline);
                }
            }

            BuildSideSigns(result, start, end, halfWidth, rng);
            if (_placeOnSlopes)
            {
                BuildVerticalSigns(result, start, end, rng);
            }
        }

        _currentSpline = null;
        return result;
    }

    // 左右の看板
    private void BuildSideSigns(List<CoursePlacementItem> result, float start, float end, float halfWidth, System.Random rng)
    {
        bool alternateRight = false;
        for (float distance = start; distance <= end;)
        {
            CoursePlacementUtility.CourseFrame frame = CoursePlacementUtility.GetFrame(_currentSpline, distance);
            float turnRate = CoursePlacementUtility.GetTurnRate(_currentSpline, distance, _curvatureWindow);
            float curveT = Mathf.InverseLerp(_straightTurnRate, Mathf.Max(_straightTurnRate + 0.01f, _curveTurnRate), Mathf.Abs(turnRate));
            float faceAngle = Mathf.Lerp(_straightFaceAngle, _curveFaceAngle, curveT);

            if (curveT >= 0.5f)
            {
                // ＋なら右へ曲がる＝インコースは右。
                float inside = turnRate >= 0f ? 1f : -1f;
                float insideOffset = Mathf.Lerp(_straightWallOffset, _curveWallOffset, curveT);
                TryAddSideSign(result, frame, inside, insideOffset, faceAngle, halfWidth, rng);

                if (_placeOutsideOnCurve)
                {
                    TryAddSideSign(result, frame, -inside, _outsideWallOffset, faceAngle, halfWidth, rng);
                }
            }
            else
            {
                float offset = Mathf.Lerp(_straightWallOffset, _curveWallOffset, curveT);
                switch (_straightSides)
                {
                    case StraightSides.Both:
                        TryAddSideSign(result, frame, 1f, offset, faceAngle, halfWidth, rng);
                        TryAddSideSign(result, frame, -1f, offset, faceAngle, halfWidth, rng);
                        break;
                    case StraightSides.Alternate:
                        alternateRight = !alternateRight;
                        TryAddSideSign(result, frame, alternateRight ? 1f : -1f, offset, faceAngle, halfWidth, rng);
                        break;
                    case StraightSides.LeftOnly:
                        TryAddSideSign(result, frame, -1f, offset, faceAngle, halfWidth, rng);
                        break;
                    case StraightSides.RightOnly:
                        TryAddSideSign(result, frame, 1f, offset, faceAngle, halfWidth, rng);
                        break;
                }
            }

            distance += Mathf.Max(1f, Mathf.Lerp(_straightInterval, _curveInterval, curveT));
        }
    }

    // 上下の看板（急な坂だけ）
    private void BuildVerticalSigns(List<CoursePlacementItem> result, float start, float end, System.Random rng)
    {
        for (float distance = start; distance <= end; distance += _slopeInterval)
        {
            CoursePlacementUtility.CourseFrame frame = CoursePlacementUtility.GetFrame(_currentSpline, distance);
            float slope = CoursePlacementUtility.GetSlope(frame.Forward);
            bool isSteep = slope >= _slopeThreshold || (_includeDescent && slope <= -_slopeThreshold);
            if (!isSteep)
            {
                continue;
            }

            TryAddVerticalSign(result, frame, 1f, rng);
            TryAddVerticalSign(result, frame, -1f, rng);
        }
    }

    // 左右の看板。side：＋1=右、−1=左。faceAngle：0=壁と平行、90=レーサーの方へ向ける。
    private void TryAddSideSign(List<CoursePlacementItem> result, CoursePlacementUtility.CourseFrame frame, float side, float wallOffset,
                                float faceAngle, float halfWidth, System.Random rng)
    {
        // 乱数の消費回数を、置く・置かないに関係なく一定にする（設定を少し変えても、他の看板の位置が変わりにくいように）。
        float jitter = CoursePlacementUtility.RandomRange(rng, -_positionJitter, _positionJitter);

        Vector3 outward = frame.Right * side;
        float faceRad = faceAngle * Mathf.Deg2Rad;

        // 看板の見る方向（レーサー／中心から看板を見る向き）。壁と平行なら外向き、レーサーの方へ向けるなら進行方向。
        Vector3 viewDirection = (outward * Mathf.Cos(faceRad) + frame.Forward * Mathf.Sin(faceRad)).normalized;

        // 斜めに向けた看板は、幅の分だけトンネル側へはみ出すので、その分だけ外へ出す。
        float clearance = frame.Radius + wallOffset + halfWidth * Mathf.Sin(faceRad);
        Vector3 position = frame.Center + outward * clearance + frame.Up * jitter;

        AddSign(result, frame, position, viewDirection, frame.Up);
    }

    // 上下の看板。side：＋1=上、−1=下。面をコースの中心へ向け、看板の上を進行方向にそろえる。
    private void TryAddVerticalSign(List<CoursePlacementItem> result, CoursePlacementUtility.CourseFrame frame, float side, System.Random rng)
    {
        float jitter = CoursePlacementUtility.RandomRange(rng, -_positionJitter, _positionJitter);

        Vector3 outward = frame.Up * side;
        Vector3 position = frame.Center + outward * (frame.Radius + _slopeWallOffset) + frame.Right * jitter;
        AddSign(result, frame, position, outward, frame.Forward);
    }

    private void AddSign(List<CoursePlacementItem> result, CoursePlacementUtility.CourseFrame frame, Vector3 position,
                         Vector3 viewDirection, Vector3 signUp)
    {
        if (CoursePlacementUtility.IsInsideAnyTunnel(position, _ownBuffer, 0f) ||
            CoursePlacementUtility.IsInsideAnyTunnel(position, _avoidBuffer, _avoidMargin))
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(_viewFromPositiveZ ? -viewDirection : viewDirection, signUp);

        // 矢印：少し先のコースの中心へ向かう方向を、看板の面に投影した向き。
        float aheadDistance = CoursePlacementUtility.ClampOrWrap(_currentSpline, frame.Distance + _arrowLookAhead);
        Vector3 travel = _currentSpline.EvaluatePositionByDistance(aheadDistance) - frame.Center;
        if (travel.sqrMagnitude < 0.0001f)
        {
            travel = frame.Forward;
        }

        float arrowAngle = CoursePlacementUtility.GetArrowAngleOnSign(rotation, travel.normalized);
        result.Add(new CoursePlacementItem(_signPrefab, position, rotation, 1f, arrowAngle, _currentGroup));
    }

    private float GetSignWidth()
    {
        if (_signPrefab != null && _signPrefab.TryGetComponent(out SmartArrowSignageController controller))
        {
            return controller.SignWidth;
        }

        return _signWidth;
    }

    private float GetSignHeight()
    {
        if (_signPrefab != null && _signPrefab.TryGetComponent(out SmartArrowSignageController controller))
        {
            return controller.SignHeight;
        }

        return _signHeight;
    }

#if UNITY_EDITOR
    protected override Color PreviewColor => new Color(0.3f, 0.9f, 1f, 0.9f);

    protected override Vector3 PreviewSize => new Vector3(GetSignWidth(), GetSignHeight(), 0.2f);
#endif
}
