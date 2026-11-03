using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CourseSpline に沿って【左右 ＋ 上下】のコース境界を示す半透明の帯を生成する検証用コンポーネント。
/// ※既存の CourseLine / CourseLineTrueUV には一切影響を与えない完全独立版。
/// ※チェックボックスで「左右のみ」「上下のみ」「4面すべて」を瞬時に切り替え可能（説得・比較用）。
/// </summary>
[ExecuteAlways]
public class CourseLine4Sides : MonoBehaviour
{
    public enum SideType
    {
        Left,
        Right,
        Top,
        Bottom
    }

    // ──────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────

    [Header("コース参照")]
    [Tooltip("コースの Spline。Inspector で割り当てる")]
    [SerializeField] private CourseSpline _courseSpline;

    [Header("メッシュ設定")]
    [Tooltip("サンプリング間隔（m）。小さいほど滑らかだが頂点数が増える")]
    [SerializeField] private float _sampleInterval = 2f;

    [Tooltip("帯の幅（左右の壁の高さ / 上下の帯の横幅）")]
    [SerializeField] private float _height = 3f;

    [Tooltip("コース境界（TunnelRadius）からの追加ずらし量")]
    [SerializeField] private float _offset = 0f;

    [Header("表示切り替え（比較・説得用）")]
    [Tooltip("左右の壁（Left / Right）を生成するか")]
    [SerializeField] private bool _enableLeftRight = true;

    [Tooltip("上下の帯（Top: 天井 / Bottom: 床面）を生成するか")]
    [SerializeField] private bool _enableTopBottom = true;

    [Tooltip("上下の帯の幅を左右と別で設定するか")]
    [SerializeField] private bool _useSeparateHeightForTopBottom = false;

    [Tooltip("上下の帯の幅（有効時のみ）")]
    [SerializeField] private float _topBottomHeight = 3f;

    [Header("見た目")]
    [Tooltip("帯に使うマテリアル（ドット版 / 矢印版のマテリアルを割り当て）")]
    [SerializeField] private Material _material;

    [Header("発光設定（最大8スロット）")]
    [Tooltip("発光が届く範囲（m）")]
    [SerializeField] private float _glowRadius = 20f;

    [Tooltip("発光の強さ倍率")]
    [SerializeField] private float _glowIntensity = 2f;

    [Tooltip("確認用：Transform を割り当てると1枠目の座標として常時発光します（未割り当てならスクリプトから制御）")]
    [SerializeField] private Transform _glowTarget;

    [Tooltip("複数人確認用：割り当てた枠（0〜7）の Transform 位置を常時発光させます")]
    [SerializeField] private Transform[] _glowTargets = new Transform[MaxHitSlots];

    // ──────────────────────────────────────────────
    // 内部
    // ──────────────────────────────────────────────

    public const int MaxHitSlots = 8;

    private readonly List<MeshRenderer> _sideRenderers = new List<MeshRenderer>();
    private readonly List<MeshRenderer> _topBottomRenderers = new List<MeshRenderer>();
    private readonly Vector4[] _hitSlots = new Vector4[MaxHitSlots];

    private MaterialPropertyBlock _mpbSide;
    private MaterialPropertyBlock _mpbTopBottom;

    private static readonly int[] HitDataIds = new int[MaxHitSlots]
    {
        Shader.PropertyToID("_HitData1"),
        Shader.PropertyToID("_HitData2"),
        Shader.PropertyToID("_HitData3"),
        Shader.PropertyToID("_HitData4"),
        Shader.PropertyToID("_HitData5"),
        Shader.PropertyToID("_HitData6"),
        Shader.PropertyToID("_HitData7"),
        Shader.PropertyToID("_HitData8")
    };
    private static readonly int HitGlowRadiusId    = Shader.PropertyToID("_HitGlowRadius");
    private static readonly int HitGlowIntensityId = Shader.PropertyToID("_HitGlowIntensity");
    private static readonly int BandHeightId       = Shader.PropertyToID("_BandHeight");

    // ──────────────────────────────────────────────
    // ライフサイクル
    // ──────────────────────────────────────────────

    private void OnValidate()
    {
        _sampleInterval = Mathf.Max(0.1f, _sampleInterval);
        _height = Mathf.Max(0.01f, _height);
        _topBottomHeight = Mathf.Max(0.01f, _topBottomHeight);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                Rebuild();
            }
        };
#endif
    }

    private void Start()
    {
        _mpbSide = new MaterialPropertyBlock();
        _mpbTopBottom = new MaterialPropertyBlock();
        EnsureRenderersCached();
    }

    private void Update()
    {
        EnsureRenderersCached();
        if (_sideRenderers.Count == 0 && _topBottomRenderers.Count == 0) return;

        if (_mpbSide == null) _mpbSide = new MaterialPropertyBlock();
        if (_mpbTopBottom == null) _mpbTopBottom = new MaterialPropertyBlock();

        UpdatePreviewTargets();
        ApplyPropertyBlock();
    }

    // ──────────────────────────────────────────────
    // 衝突発光 設定用API（衝突側から値を直接送る）
    // ──────────────────────────────────────────────

    public void SetHitData(int slotIndex, Vector3 worldPosition, float intensity)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots) return;
        _hitSlots[slotIndex] = new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, Mathf.Clamp01(intensity));
    }

    public void SetHitIntensity(int slotIndex, float intensity)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots) return;
        Vector4 current = _hitSlots[slotIndex];
        current.w = Mathf.Clamp01(intensity);
        _hitSlots[slotIndex] = current;
    }

    public void ClearHitData(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots) return;
        _hitSlots[slotIndex] = Vector4.zero;
    }

    public void ClearAllHitData()
    {
        for (int i = 0; i < MaxHitSlots; i++)
        {
            _hitSlots[i] = Vector4.zero;
        }
    }

    private void UpdatePreviewTargets()
    {
        for (int i = 0; i < MaxHitSlots; i++)
        {
            Transform target = (i == 0 && _glowTarget != null)
                ? _glowTarget
                : (_glowTargets != null && i < _glowTargets.Length ? _glowTargets[i] : null);

            if (target != null)
            {
                Vector3 pos = target.position;
                _hitSlots[i] = new Vector4(pos.x, pos.y, pos.z, 1f);
            }
        }
    }

    private void ApplyPropertyBlock()
    {
        // 左右用 MPB
        PopulateMPB(_mpbSide, _height);
        for (int i = 0; i < _sideRenderers.Count; i++)
        {
            if (_sideRenderers[i] != null)
            {
                _sideRenderers[i].SetPropertyBlock(_mpbSide);
            }
        }

        // 上下用 MPB
        float tbH = _useSeparateHeightForTopBottom ? _topBottomHeight : _height;
        PopulateMPB(_mpbTopBottom, tbH);
        for (int i = 0; i < _topBottomRenderers.Count; i++)
        {
            if (_topBottomRenderers[i] != null)
            {
                _topBottomRenderers[i].SetPropertyBlock(_mpbTopBottom);
            }
        }
    }

    private void PopulateMPB(MaterialPropertyBlock mpb, float bandHeight)
    {
        for (int i = 0; i < MaxHitSlots; i++)
        {
            mpb.SetVector(HitDataIds[i], _hitSlots[i]);
        }
        mpb.SetFloat(HitGlowRadiusId, _glowRadius);
        mpb.SetFloat(HitGlowIntensityId, _glowIntensity);
        mpb.SetFloat(BandHeightId, bandHeight);
    }

    private void EnsureRenderersCached()
    {
        if (_sideRenderers.Count > 0 || _topBottomRenderers.Count > 0) return;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            if (child.name.Contains("Left") || child.name.Contains("Right"))
            {
                _sideRenderers.Add(mr);
            }
            else if (child.name.Contains("Top") || child.name.Contains("Bottom"))
            {
                _topBottomRenderers.Add(mr);
            }
        }
    }

    // ──────────────────────────────────────────────
    // メッシュ生成
    // ──────────────────────────────────────────────

    public void Rebuild()
    {
        ClearChildren();
        _sideRenderers.Clear();
        _topBottomRenderers.Clear();

        if (_courseSpline == null || _courseSpline.TotalLength <= 0f) return;

        if (_enableLeftRight)
        {
            BuildPanel("CourseLine_Left",  SideType.Left,  _height, _sideRenderers);
            BuildPanel("CourseLine_Right", SideType.Right, _height, _sideRenderers);
        }

        if (_enableTopBottom)
        {
            float tbH = _useSeparateHeightForTopBottom ? _topBottomHeight : _height;
            BuildPanel("CourseLine_Top",    SideType.Top,    tbH, _topBottomRenderers);
            BuildPanel("CourseLine_Bottom", SideType.Bottom, tbH, _topBottomRenderers);
        }
    }

    private void BuildPanel(string childName, SideType side, float panelHeight, List<MeshRenderer> targetList)
    {
        float totalLength = _courseSpline.TotalLength;
        bool isLoop = _courseSpline.IsLoop;

        List<Vector3> edge0Verts = new List<Vector3>();
        List<Vector3> edge1Verts = new List<Vector3>();
        List<float> trueDistances = new List<float>();

        float interval = Mathf.Max(0.1f, _sampleInterval);
        int sampleCount = Mathf.FloorToInt(totalLength / interval) + 1;
        float accumulatedDistance = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float d = i * interval;
            if (d > totalLength) d = totalLength;

            SamplePoint(d, side, panelHeight, out Vector3 edge0, out Vector3 edge1);

            if (edge0Verts.Count > 0)
            {
                accumulatedDistance += Vector3.Distance(edge0, edge0Verts[edge0Verts.Count - 1]);
            }

            edge0Verts.Add(edge0);
            edge1Verts.Add(edge1);
            trueDistances.Add(accumulatedDistance);
        }

        float lastSplineDistance = (sampleCount - 1) * interval;
        if (lastSplineDistance < totalLength - 0.001f)
        {
            SamplePoint(totalLength, side, panelHeight, out Vector3 edge0, out Vector3 edge1);
            accumulatedDistance += Vector3.Distance(edge0, edge0Verts[edge0Verts.Count - 1]);

            edge0Verts.Add(edge0);
            edge1Verts.Add(edge1);
            trueDistances.Add(accumulatedDistance);
        }

        if (isLoop)
        {
            SamplePoint(0f, side, panelHeight, out Vector3 edge0, out Vector3 edge1);
            accumulatedDistance += Vector3.Distance(edge0, edge0Verts[edge0Verts.Count - 1]);

            edge0Verts.Add(edge0);
            edge1Verts.Add(edge1);
            trueDistances.Add(accumulatedDistance);
        }

        int vertexCount = edge0Verts.Count;
        if (vertexCount < 2) return;

        // メッシュ構築
        int quadCount = vertexCount - 1;
        Vector3[] vertices = new Vector3[vertexCount * 2];
        Vector2[] uvs = new Vector2[vertexCount * 2];
        int[] triangles = new int[quadCount * 6];

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i * 2]     = transform.InverseTransformPoint(edge0Verts[i]);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(edge1Verts[i]);

            float u = trueDistances[i];
            uvs[i * 2]     = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
        }

        for (int i = 0; i < quadCount; i++)
        {
            int baseVertex = i * 2;
            int triBase = i * 6;

            triangles[triBase]     = baseVertex;
            triangles[triBase + 1] = baseVertex + 1;
            triangles[triBase + 2] = baseVertex + 2;

            triangles[triBase + 3] = baseVertex + 1;
            triangles[triBase + 4] = baseVertex + 3;
            triangles[triBase + 5] = baseVertex + 2;
        }

        Mesh mesh = new Mesh { name = childName };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        targetList.Add(renderer);
    }

    // 4面（Left, Right, Top, Bottom）の位置とリボンの幅方向を計算
    private void SamplePoint(float distance, SideType side, float panelHeight, out Vector3 edge0, out Vector3 edge1)
    {
        Vector3 center = _courseSpline.EvaluatePositionByDistance(distance);
        Vector3 right  = _courseSpline.EvaluateRightByDistance(distance);
        Vector3 up     = _courseSpline.EvaluateUpByDistance(distance);
        float radius   = _courseSpline.GetTunnelRadiusByDistance(distance) + _offset;

        float halfH = panelHeight * 0.5f;

        switch (side)
        {
            case SideType.Left:
            {
                // 左壁（内壁に沿って縦に立つ）
                Vector3 basePos = center - right * radius;
                edge0 = basePos - up * halfH; // 下端 (UV.y = 0)
                edge1 = basePos + up * halfH; // 上端 (UV.y = 1)
                break;
            }
            case SideType.Right:
            {
                // 右壁（内壁に沿って縦に立つ）
                Vector3 basePos = center + right * radius;
                edge0 = basePos - up * halfH; // 下端 (UV.y = 0)
                edge1 = basePos + up * halfH; // 上端 (UV.y = 1)
                break;
            }
            case SideType.Top:
            {
                // 天井（内壁に沿って横に寝かせる）
                Vector3 basePos = center + up * radius;
                edge0 = basePos - right * halfH; // 左端 (UV.y = 0)
                edge1 = basePos + right * halfH; // 右端 (UV.y = 1)
                break;
            }
            case SideType.Bottom:
            {
                // 床面（内壁に沿って横に寝かせる）
                Vector3 basePos = center - up * radius;
                edge0 = basePos - right * halfH; // 左端 (UV.y = 0)
                edge1 = basePos + right * halfH; // 右端 (UV.y = 1)
                break;
            }
            default:
                edge0 = center;
                edge1 = center;
                break;
        }
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}

