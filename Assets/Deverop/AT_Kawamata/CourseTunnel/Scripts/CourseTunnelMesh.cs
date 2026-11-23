using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// CourseSplineのトンネル（飛行できる範囲）に沿ったチューブのメッシュを作る。見た目はマテリアル（Misoten/Course Tunnel）で付ける。
// Gizmoの代わりにエディタでコースの範囲を見るのにも、ゲーム中の演出にも使う。
//
// ・頂点は、中心＋（右・cos＋上・sin）×その場所のトンネル半径。法線は内向き（中から見る前提）。
// ・uv0 =（円周の位置0〜1, 距離m）。シェーダーの線や流れをメートル単位でそろえるため。分岐は分岐点の本線の距離から数える（流れが分岐点でつながるように）。
// ・uv1 =（本線の距離m, 両端のフェード0〜1）。区画（本線の距離で区切る）をシェーダーで分けるため。分岐は本線の距離に直す。
// ・uv2 =（その場所の半径m, 0）。光の粒の大きさをメートルでそろえるため。
// 区画（Sections）ごとに、表示する・しないと不透明度を変えられる。メッシュは作り直さず、シェーダーへ値を渡すだけなので、ゲーム中に変えても軽い。
// Course Networkを入れると、本線と分岐をまとめて1つのメッシュにする。分岐は、本線のトンネルと重なっている区間を省ける（重なると加算で明るくなりすぎるため）。
// メッシュはシーンに保存しない。エディタではコースを変えると作り直し、実行時はStartで1回だけ作る（SplineのAwakeで距離テーブルができた後）。
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class CourseTunnelMesh : MonoBehaviour
{
    // エディタで、コースが変わったかを確かめる間隔（秒）と、比べる位置の数。
    private const float EditorCheckInterval = 0.5f;
    private const int SignatureSampleCount = 16;

    // シェーダー（S_CourseTunnel）のMAX_TUNNEL_SECTIONSと同じにする。
    public const int MaxSections = 16;

    // 本線の距離で区切った区画。表示する・しないと、不透明度の倍率を持つ。
    [System.Serializable]
    public sealed class TunnelSection
    {
        [Tooltip("名前（スクリプトから名前で探すとき用）")]
        [SerializeField] private string _name = "Section";

        [Tooltip("OFFにすると、この区画のトンネルを表示しない")]
        [SerializeField] private bool _visible = true;

        [Tooltip("区画の始まり（本線のスタートからの距離、m）")]
        [Min(0f)]
        [SerializeField] private float _startDistance = 0f;

        [Tooltip("区画の終わり（本線の距離、m）")]
        [Min(0f)]
        [SerializeField] private float _endDistance = 100f;

        [Tooltip("この区画の不透明度の倍率（マテリアルの不透明度に掛ける。0で見えない、1でそのまま、1より大きいと濃く）")]
        [Range(0f, 3f)]
        [SerializeField] private float _opacity = 1f;

        public string Name => _name;
        public float StartDistance => _startDistance;
        public float EndDistance => _endDistance;

        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }

        public float Opacity
        {
            get => _opacity;
            set => _opacity = Mathf.Max(0f, value);
        }

        public void Validate()
        {
            _endDistance = Mathf.Max(_startDistance, _endDistance);
        }
    }

    private static readonly int SectionEnabledId = Shader.PropertyToID("_TunnelSectionEnabled");
    private static readonly int SectionCountId = Shader.PropertyToID("_TunnelSectionCount");
    private static readonly int OutsideOpacityId = Shader.PropertyToID("_TunnelOutsideOpacity");
    private static readonly int SectionFadeId = Shader.PropertyToID("_TunnelSectionFade");
    private static readonly int SectionsId = Shader.PropertyToID("_TunnelSections");

    [Header("コース")]
    [Tooltip("本線と分岐をまとめて見せるときに入れる。入れると Course Spline は使わない")]
    [SerializeField] private CourseNetwork _courseNetwork;

    [Tooltip("Course Networkを使わないときの、トンネルを作るコース（1本だけ）")]
    [SerializeField] private CourseSpline _courseSpline;

    [Header("形")]
    [Tooltip("輪切りにする間隔（m）。小さいほどなめらかだが頂点が増える")]
    [Min(0.25f)]
    [SerializeField] private float _sampleInterval = 2f;

    [Tooltip("円周の分割数")]
    [Range(3, 128)]
    [SerializeField] private int _radialSegments = 48;

    [Tooltip("トンネル半径に足す量（m）。＋で外へ広げる（判定より少し外に見せたいとき）")]
    [SerializeField] private float _radiusOffset = 0f;

    [Header("範囲")]
    [Tooltip("本線のスタートからの距離（m）。ここから作る。分岐は本線の距離に直して比べる")]
    [Min(0f)]
    [SerializeField] private float _startDistance = 0f;

    [Tooltip("ここまで作る（本線の距離、m）。0以下ならコースの最後まで")]
    [SerializeField] private float _endDistance = 0f;

    [Tooltip("コースの両端で薄くしていく長さ（m）。ループコースを1周作るときは端が無いので使わない")]
    [Min(0f)]
    [SerializeField] private float _endFadeLength = 20f;

    [Header("分岐")]
    [Tooltip("Course Networkを使うとき、分岐のトンネルも作るか")]
    [SerializeField] private bool _includeBranches = true;

    [Tooltip("ON：分岐のうち、本線のトンネルと重なっている区間（分岐の領域・合流の領域）は作らない")]
    [SerializeField] private bool _trimBranchOverlap = true;

    [Tooltip("分岐の両端（本線とつながるところ）で薄くしていく長さ（m）")]
    [Min(0f)]
    [SerializeField] private float _branchJoinFadeLength = 8f;

    [Header("区画（本線の距離で区切る）")]
    [Tooltip("区画ごとに、表示する・しないと不透明度を変える（最大16個）。重なっているところは、下の区画が優先")]
    [SerializeField] private TunnelSection[] _sections = new TunnelSection[0];

    [Tooltip("どの区画にも入っていないところの不透明度の倍率")]
    [Range(0f, 3f)]
    [SerializeField] private float _outsideOpacity = 1f;

    [Tooltip("0以上なら、この番号の区画だけ表示する（他の区画・区画の外は消す）。-1なら全部（各区画の設定どおり）")]
    [SerializeField] private int _soloSection = -1;

    [Tooltip("区画の境目で、なめらかに切り替える長さ（m）")]
    [Min(0f)]
    [SerializeField] private float _sectionFadeLength = 10f;

    private Mesh _mesh;
    private MaterialPropertyBlock _propertyBlock;
    private readonly Vector4[] _sectionData = new Vector4[MaxSections];
    private float _lastSignature = float.NaN;
    private float _nextCheckTime;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<Vector2> _uv0 = new List<Vector2>();
    private readonly List<Vector2> _uv1 = new List<Vector2>();
    private readonly List<Vector2> _uv2 = new List<Vector2>();
    private readonly List<int> _triangles = new List<int>();

    public CourseSpline CourseSpline
    {
        get => _courseSpline;
        set
        {
            _courseSpline = value;
            Rebuild();
        }
    }

    public CourseNetwork CourseNetwork
    {
        get => _courseNetwork;
        set
        {
            _courseNetwork = value;
            Rebuild();
        }
    }

    public int SectionCount => _sections != null ? _sections.Length : 0;

    public float OutsideOpacity
    {
        get => _outsideOpacity;
        set
        {
            _outsideOpacity = Mathf.Max(0f, value);
            ApplySectionProperties();
        }
    }

    // 名前で区画の番号を探す。無ければ-1。
    public int FindSection(string sectionName)
    {
        for (int i = 0; i < SectionCount; i++)
        {
            if (_sections[i] != null && _sections[i].Name == sectionName)
            {
                return i;
            }
        }

        return -1;
    }

    public void SetSectionVisible(int index, bool visible)
    {
        if (IsValidSection(index))
        {
            _sections[index].Visible = visible;
            ApplySectionProperties();
        }
    }

    public void SetSectionOpacity(int index, float opacity)
    {
        if (IsValidSection(index))
        {
            _sections[index].Opacity = opacity;
            ApplySectionProperties();
        }
    }

    // この区画だけ表示する（他の区画・区画の外は消す）。-1で元に戻す。
    public void SoloSection(int index)
    {
        _soloSection = IsValidSection(index) ? index : -1;
        ApplySectionProperties();
    }

    public void ShowAllSections()
    {
        SoloSection(-1);
    }

    private bool IsValidSection(int index)
    {
        return index >= 0 && index < SectionCount && _sections[index] != null;
    }

    private void Start()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        // エディタでは読み込み直したときにも作る。実行時はStartで作る（SplineのAwakeの後にするため）。
        if (!Application.isPlaying)
        {
            Rebuild();
        }
        else
        {
            ApplySectionProperties();
        }
    }

    private void OnDestroy()
    {
        DestroyMesh();
    }

    private void OnValidate()
    {
        _lastSignature = float.NaN;
        if (_sections != null)
        {
            foreach (TunnelSection section in _sections)
            {
                section?.Validate();
            }

            if (_sections.Length > MaxSections)
            {
                Debug.LogWarning($"CourseTunnelMesh: 区画は{MaxSections}個までです。{MaxSections + 1}個目からは使いません", this);
            }
        }

        ApplySectionProperties();
    }

    // 区画の設定をシェーダーへ渡す（メッシュは作り直さない）。
    public void ApplySectionProperties()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            return;
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        int count = Mathf.Min(SectionCount, MaxSections);
        bool hasSolo = IsValidSection(_soloSection);
        for (int i = 0; i < MaxSections; i++)
        {
            TunnelSection section = i < count ? _sections[i] : null;
            if (section == null)
            {
                // 空の区画は、どこにも当たらない範囲にする。
                _sectionData[i] = new Vector4(-1f, -1f, 0f, 0f);
                continue;
            }

            bool visible = hasSolo ? i == _soloSection : section.Visible;
            _sectionData[i] = new Vector4(section.StartDistance, section.EndDistance, visible ? section.Opacity : 0f, 0f);
        }

        // 他のコンポーネント（CourseTunnelHitGlow）が入れた値を消さないよう、今の値を読んでから書き足す。
        meshRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(SectionEnabledId, count > 0 || hasSolo ? 1f : 0f);
        _propertyBlock.SetFloat(SectionCountId, count);
        _propertyBlock.SetFloat(OutsideOpacityId, hasSolo ? 0f : _outsideOpacity);
        _propertyBlock.SetFloat(SectionFadeId, _sectionFadeLength);
        _propertyBlock.SetVectorArray(SectionsId, _sectionData);
        meshRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void Update()
    {
        // 実行中はコースが動かない前提なので、エディタでだけ変更を確かめる。
        if (Application.isPlaying)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now < _nextCheckTime && !float.IsNaN(_lastSignature))
        {
            return;
        }

        _nextCheckTime = now + EditorCheckInterval;
        if (ComputeSignature(CollectTargets()) != _lastSignature)
        {
            Rebuild();
        }
    }

    [ContextMenu("トンネルを作り直す")]
    public void Rebuild()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        List<CourseTarget> targets = CollectTargets();
        _lastSignature = ComputeSignature(targets);

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "CourseTunnel", hideFlags = HideFlags.DontSave };
        }

        BuildMesh(_mesh, targets);
        meshFilter.sharedMesh = _mesh;
        ApplySectionProperties();
    }

    private List<CourseTarget> CollectTargets()
    {
        return CourseTarget.Collect(_courseSpline, _courseNetwork, true, _includeBranches);
    }

    private CourseSpline GetMainSpline()
    {
        if (_courseNetwork != null && _courseNetwork.MainSpline != null)
        {
            return _courseNetwork.MainSpline;
        }

        if (_courseSpline != null && _courseSpline.TryGetComponent(out CourseBranch branch) && branch.MainSpline != null)
        {
            return branch.MainSpline;
        }

        return _courseSpline;
    }

    private void BuildMesh(Mesh mesh, List<CourseTarget> targets)
    {
        _vertices.Clear();
        _normals.Clear();
        _uv0.Clear();
        _uv1.Clear();
        _uv2.Clear();
        _triangles.Clear();

        CourseSpline mainSpline = GetMainSpline();
        foreach (CourseTarget target in targets)
        {
            if (!target.TryGetRange(mainSpline, _startDistance, _endDistance, out float start, out float end))
            {
                continue;
            }

            CourseSpline spline = target.Spline;
            if (target.IsBranch)
            {
                if (_trimBranchOverlap)
                {
                    start = Mathf.Max(start, target.Branch.ForkZoneEnd);
                    end = Mathf.Min(end, target.Branch.MergeZoneStart);
                }

                AppendTube(target, start, end, _branchJoinFadeLength, _branchJoinFadeLength, false, target.Branch.ForkDistance);
            }
            else
            {
                bool isFullLoop = spline.IsLoop && start <= 0f && end >= spline.TotalLength;
                AppendTube(target, start, end, _endFadeLength, _endFadeLength, isFullLoop, 0f);
            }
        }

        mesh.Clear();
        mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uv0);
        mesh.SetUVs(1, _uv1);
        mesh.SetUVs(2, _uv2);
        mesh.SetTriangles(_triangles, 0);
        mesh.RecalculateBounds();
    }

    // コースのstart〜endのチューブを、頂点リストの後ろに足す。alongOffsetはuv0.yに足す距離（分岐の流れを本線とつなぐため）。
    private void AppendTube(CourseTarget target, float start, float end, float fadeStartLength, float fadeEndLength, bool isFullLoop, float alongOffset)
    {
        CourseSpline spline = target.Spline;
        float total = spline.TotalLength;
        float length = end - start;
        if (total <= 0f || length <= 0.01f)
        {
            return;
        }

        int rings = Mathf.Max(2, Mathf.CeilToInt(length / _sampleInterval) + 1);
        int segments = Mathf.Max(3, _radialSegments);
        int columns = segments + 1; // 継ぎ目でUVが0→1に戻るよう、最初の列を複製する
        int baseIndex = _vertices.Count;
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        for (int ring = 0; ring < rings; ring++)
        {
            float distance = start + length * ring / (rings - 1);

            // ループコースを1周作るとき、最後の輪は最初の輪とぴったり同じ位置にする（距離0とTotalLengthで同じ点）。
            float evalDistance = isFullLoop && ring == rings - 1 ? 0f : distance;
            Vector3 center = spline.EvaluatePositionByDistance(evalDistance);
            Vector3 right = spline.EvaluateRightByDistance(evalDistance);
            Vector3 up = spline.EvaluateUpByDistance(evalDistance);
            float radius = Mathf.Max(0.01f, spline.GetTunnelRadiusByDistance(evalDistance) + _radiusOffset);

            float fade = 1f;
            if (!isFullLoop)
            {
                float fadeIn = fadeStartLength > 0f ? Mathf.Clamp01((distance - start) / fadeStartLength) : 1f;
                float fadeOut = fadeEndLength > 0f ? Mathf.Clamp01((end - distance) / fadeEndLength) : 1f;
                fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
            }

            float mainDistance = target.ToMainDistance(distance);
            for (int column = 0; column < columns; column++)
            {
                float around = (float)column / segments;
                float angle = around * Mathf.PI * 2f;
                Vector3 direction = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);

                _vertices.Add(worldToLocal.MultiplyPoint3x4(center + direction * radius));
                _normals.Add(worldToLocal.MultiplyVector(-direction).normalized);
                _uv0.Add(new Vector2(around, alongOffset + distance));
                _uv1.Add(new Vector2(mainDistance, fade));
                _uv2.Add(new Vector2(radius, 0f));
            }
        }

        for (int ring = 0; ring < rings - 1; ring++)
        {
            int current = baseIndex + ring * columns;
            int next = current + columns;
            for (int column = 0; column < segments; column++)
            {
                int a = current + column;
                int b = current + column + 1;
                int c = next + column;
                int d = next + column + 1;

                // 内側から見て表になる向き。マテリアルはCull Offなので、どちらから見ても描かれる。
                _triangles.Add(a);
                _triangles.Add(c);
                _triangles.Add(b);
                _triangles.Add(b);
                _triangles.Add(c);
                _triangles.Add(d);
            }
        }
    }

    // コースが変わったかを見分けるための値（コースの数・長さ・数か所の位置と半径・Transform）。
    private float ComputeSignature(List<CourseTarget> targets)
    {
        float signature = targets.Count * 101.3f;
        for (int t = 0; t < targets.Count; t++)
        {
            CourseSpline spline = targets[t].Spline;
            float total = spline.TotalLength;
            signature += total * (1.37f + t);
            for (int i = 0; i <= SignatureSampleCount; i++)
            {
                float distance = total * i / SignatureSampleCount;
                Vector3 position = spline.EvaluatePositionByDistance(distance);
                Vector3 up = spline.EvaluateUpByDistance(distance);
                signature += (position.x * 0.73f + position.y * 1.91f + position.z * 2.57f) * (i + 1 + t);
                signature += (up.x + up.y * 3.1f + up.z * 5.3f) * (i + 7);
                signature += spline.GetTunnelRadiusByDistance(distance) * (i + 3);
            }

            if (targets[t].IsBranch)
            {
                signature += targets[t].Branch.ForkZoneEnd * 0.31f + targets[t].Branch.MergeZoneStart * 0.17f;
            }
        }

        Matrix4x4 matrix = transform.localToWorldMatrix;
        for (int i = 0; i < 16; i++)
        {
            signature += matrix[i] * (i + 11);
        }

        return signature;
    }

    private void DestroyMesh()
    {
        if (_mesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_mesh);
        }
        else
        {
            DestroyImmediate(_mesh);
        }

        _mesh = null;
    }
}
