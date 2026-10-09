using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CourseSpline に沿って左右のコース境界を示す半透明の帯を生成する。
/// ※UV計算において「実際の壁の長さ（True Length）」を測ることで、カーブ時のテクスチャ歪みをなくしたバージョン。
/// </summary>
[ExecuteAlways]
public class CourseLineTrueUV : MonoBehaviour
{
    // ──────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────

    [Header("コース参照")]
    [Tooltip("コースの Spline。Inspector で割り当てる")]
    [SerializeField] private CourseSpline _courseSpline;

    [Header("メッシュ設定")]
    [Tooltip("サンプリング間隔（m）。小さいほど滑らかだが頂点数が増える")]
    [SerializeField] private float _sampleInterval = 2f;

    [Tooltip("帯の高さ")]
    [SerializeField] private float _height = 3f;

    [Tooltip("コース境界（TunnelRadius）からの追加ずらし量")]
    [SerializeField] private float _offset = 0f;

    [Header("見た目")]
    [Tooltip("帯に使うマテリアル（Shader Graph で作成したものを割り当て）")]
    [SerializeField] private Material _material;

    [Tooltip("テクスチャ1枚が何メートルでリピートするか（※ループ調整なしの純粋な長さ）")]
    [SerializeField] private float _textureRepeatLength = 10f;

    [Header("発光設定")]
    [Tooltip("発光が届く範囲")]
    [SerializeField] private float _glowRadius = 20f;

    [Tooltip("発光の強さ")]
    [SerializeField] private float _glowIntensity = 2f;

    [Tooltip("発光させる対象の Transform（Player を割り当て）")]
    [SerializeField] private Transform _glowTarget;

    // ──────────────────────────────────────────────
    // 内部
    // ──────────────────────────────────────────────

    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private MaterialPropertyBlock _mpb;

    private static readonly int PlayerPositionId = Shader.PropertyToID("_PlayerPosition");
    private static readonly int GlowRadiusId     = Shader.PropertyToID("_GlowRadius");
    private static readonly int GlowIntensityId  = Shader.PropertyToID("_GlowIntensity");
    private static readonly int TilingId         = Shader.PropertyToID("_Tilling");

    // ──────────────────────────────────────────────
    // ライフサイクル
    // ──────────────────────────────────────────────

    private void OnValidate()
    {
        _sampleInterval = Mathf.Max(0.1f, _sampleInterval);
        _height = Mathf.Max(0.01f, _height);

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

    private Vector2 _cachedTiling = Vector2.one;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        RecalculateTiling();
    }

    private void Update()
    {
        if (_glowTarget == null || _renderers.Count == 0) return;

        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
            RecalculateTiling();
        }

        _mpb.SetVector(PlayerPositionId, _glowTarget.position);
        _mpb.SetFloat(GlowRadiusId, _glowRadius);
        _mpb.SetFloat(GlowIntensityId, _glowIntensity);
        _mpb.SetVector(TilingId, _cachedTiling);

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }
    }

    /// <summary>
    /// 最適なタイリング値を事前計算する。
    /// ※このバージョンは左右の壁の長さが異なるため、ループ時の整数丸めをしません。
    /// </summary>
    private void RecalculateTiling()
    {
        float tiling = 1f;
        if (_textureRepeatLength > 0.001f)
        {
            // 単純な割り算のみ（物理的な距離に対して正確にリピートする）
            tiling = 1f / _textureRepeatLength;
        }
        
        _cachedTiling = new Vector2(tiling, 1f);
    }

    // ──────────────────────────────────────────────
    // メッシュ生成
    // ──────────────────────────────────────────────

    public void Rebuild()
    {
        ClearChildren();
        _renderers.Clear();

        if (_courseSpline == null || _courseSpline.TotalLength <= 0f) return;

        BuildSide("CourseLine_Left",  -1f);
        BuildSide("CourseLine_Right", +1f);

        RecalculateTiling();
    }

    private void BuildSide(string childName, float sign)
    {
        float totalLength = _courseSpline.TotalLength;
        float tunnelRadius = _courseSpline.TunnelRadius;
        bool isLoop = _courseSpline.IsLoop;

        List<Vector3> bottomVerts = new List<Vector3>();
        List<Vector3> topVerts = new List<Vector3>();
        
        // 実際の物理的な長さを記録するリスト
        List<float> trueDistances = new List<float>();

        float interval = Mathf.Max(0.1f, _sampleInterval);
        int sampleCount = Mathf.FloorToInt(totalLength / interval) + 1;

        float accumulatedDistance = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float d = i * interval;
            if (d > totalLength) d = totalLength;

            SamplePoint(d, tunnelRadius, sign, out Vector3 bottom, out Vector3 top);

            if (bottomVerts.Count > 0)
            {
                // 直前の頂点からの実際の距離（物理距離）を足し合わせる
                accumulatedDistance += Vector3.Distance(bottom, bottomVerts[bottomVerts.Count - 1]);
            }

            bottomVerts.Add(bottom);
            topVerts.Add(top);
            trueDistances.Add(accumulatedDistance);
        }

        // 最後のサンプルが totalLength ちょうどでなければ追加
        float lastSplineDistance = (sampleCount - 1) * interval;
        if (lastSplineDistance < totalLength - 0.001f)
        {
            SamplePoint(totalLength, tunnelRadius, sign, out Vector3 bottom, out Vector3 top);
            accumulatedDistance += Vector3.Distance(bottom, bottomVerts[bottomVerts.Count - 1]);
            
            bottomVerts.Add(bottom);
            topVerts.Add(top);
            trueDistances.Add(accumulatedDistance);
        }

        // ループなら先頭のサンプルを末尾に追加して閉じる
        if (isLoop)
        {
            SamplePoint(0f, tunnelRadius, sign, out Vector3 bottom, out Vector3 top);
            accumulatedDistance += Vector3.Distance(bottom, bottomVerts[bottomVerts.Count - 1]);

            bottomVerts.Add(bottom);
            topVerts.Add(top);
            trueDistances.Add(accumulatedDistance);
        }

        int vertexCount = bottomVerts.Count;
        if (vertexCount < 2) return;

        // ── メッシュ構築 ──
        int quadCount = vertexCount - 1;
        Vector3[] vertices = new Vector3[vertexCount * 2];
        Vector2[] uvs = new Vector2[vertexCount * 2];
        int[] triangles = new int[quadCount * 6];

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i * 2]     = transform.InverseTransformPoint(bottomVerts[i]);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(topVerts[i]);

            // U座標に「実際の長さ（物理距離）」を入れることで、カーブでの歪みをなくす
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

        _renderers.Add(renderer);
    }

    private void SamplePoint(float distance, float tunnelRadius, float sign, out Vector3 bottom, out Vector3 top)
    {
        Vector3 center = _courseSpline.EvaluatePositionByDistance(distance);
        Vector3 right  = _courseSpline.EvaluateRightByDistance(distance);
        Vector3 up     = _courseSpline.EvaluateUpByDistance(distance);

        Vector3 basePos = center + right * sign * (tunnelRadius + _offset);
        bottom = basePos - up * (_height * 0.5f);
        top    = basePos + up * (_height * 0.5f);
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

