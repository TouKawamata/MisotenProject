using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CourseSpline に沿って左右のコース境界を示す半透明の帯（ガードレール）を生成する。
/// Player が境界に衝突した際、周辺が発光して衝突フィードバックを強化する。
/// </summary>
[ExecuteAlways]
public class CourseLine : MonoBehaviour
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

    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private readonly Vector4[] _hitSlots = new Vector4[MaxHitSlots];
    private MaterialPropertyBlock _mpb;

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

        // ExecuteAlways 環境で OnValidate から直接 Rebuild すると
        // DestroyImmediate が呼べないフレームがあるため、遅延実行する。
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
        _mpb = new MaterialPropertyBlock();
        EnsureRenderersCached();
    }

    private void Update()
    {
        EnsureRenderersCached();
        if (_renderers.Count == 0)
        {
            return;
        }

        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }

        UpdatePreviewTargets();
        ApplyPropertyBlock();
    }

    // ──────────────────────────────────────────────
    // 衝突発光 設定用関数（衝突側から値を直接送る）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 指定スロット（0〜7）の発光座標と発光強度（0〜1）を設定する。
    /// </summary>
    /// <param name="slotIndex">スロット番号（0〜7）</param>
    /// <param name="worldPosition">発光させるワールド座標</param>
    /// <param name="intensity">発光強度（0 = 消灯、1 = 最大）</param>
    public void SetHitData(int slotIndex, Vector3 worldPosition, float intensity)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots)
        {
            return;
        }

        _hitSlots[slotIndex] = new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, Mathf.Clamp01(intensity));
    }

    /// <summary>
    /// 指定スロット（0〜7）の発光強度（w成分：0〜1）のみを変更する。
    /// </summary>
    public void SetHitIntensity(int slotIndex, float intensity)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots)
        {
            return;
        }

        Vector4 current = _hitSlots[slotIndex];
        current.w = Mathf.Clamp01(intensity);
        _hitSlots[slotIndex] = current;
    }

    /// <summary>
    /// 指定スロット（0〜7）の発光を消灯（0にリセット）する。
    /// </summary>
    public void ClearHitData(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxHitSlots)
        {
            return;
        }

        _hitSlots[slotIndex] = Vector4.zero;
    }

    /// <summary>
    /// 全8スロットの発光を消灯（0にリセット）する。
    /// </summary>
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
        for (int i = 0; i < MaxHitSlots; i++)
        {
            _mpb.SetVector(HitDataIds[i], _hitSlots[i]);
        }
        _mpb.SetFloat(HitGlowRadiusId, _glowRadius);
        _mpb.SetFloat(HitGlowIntensityId, _glowIntensity);
        _mpb.SetFloat(BandHeightId, _height);

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }
    }

    private void EnsureRenderersCached()
    {
        if (_renderers.Count > 0)
        {
            return;
        }

        GetComponentsInChildren(true, _renderers);
        _renderers.RemoveAll(r => r == null || r.gameObject == gameObject);
    }

    // ──────────────────────────────────────────────
    // メッシュ生成
    // ──────────────────────────────────────────────

    /// <summary>
    /// 帯メッシュを再生成する。Editor のボタンおよび OnValidate から呼ばれる。
    /// </summary>
    public void Rebuild()
    {
        ClearChildren();
        _renderers.Clear();

        if (_courseSpline == null || _courseSpline.TotalLength <= 0f)
        {
            return;
        }

        // 左右それぞれの帯を生成
        BuildSide("CourseLine_Left",  -1f);
        BuildSide("CourseLine_Right", +1f);
    }

    /// <summary>
    /// 片側の帯メッシュを生成する。
    /// </summary>
    /// <param name="childName">子 GameObject の名前</param>
    /// <param name="sign">-1 = 左側, +1 = 右側</param>
    private void BuildSide(string childName, float sign)
    {
        float totalLength = _courseSpline.TotalLength;
        bool isLoop = _courseSpline.IsLoop;

        // ── サンプリング ──
        List<Vector3> bottomVerts = new List<Vector3>();
        List<Vector3> topVerts = new List<Vector3>();
        List<float> distances = new List<float>();

        float interval = Mathf.Max(0.1f, _sampleInterval);
        int sampleCount = Mathf.FloorToInt(totalLength / interval) + 1;

        for (int i = 0; i < sampleCount; i++)
        {
            float d = i * interval;
            if (d > totalLength)
            {
                d = totalLength;
            }

            SamplePoint(d, sign, out Vector3 bottom, out Vector3 top);
            bottomVerts.Add(bottom);
            topVerts.Add(top);
            distances.Add(d);
        }

        // 最後のサンプルが totalLength ちょうどでなければ追加
        float lastDistance = distances[distances.Count - 1];
        if (lastDistance < totalLength - 0.001f)
        {
            SamplePoint(totalLength, sign, out Vector3 bottom, out Vector3 top);
            bottomVerts.Add(bottom);
            topVerts.Add(top);
            distances.Add(totalLength);
        }

        // ループなら先頭のサンプルを末尾に追加して閉じる
        if (isLoop)
        {
            SamplePoint(0f, sign, out Vector3 bottom, out Vector3 top);
            bottomVerts.Add(bottom);
            topVerts.Add(top);
            distances.Add(totalLength);
        }

        int vertexCount = bottomVerts.Count;
        if (vertexCount < 2)
        {
            return;
        }

        // ── メッシュ構築 ──
        int quadCount = vertexCount - 1;
        Vector3[] vertices = new Vector3[vertexCount * 2];
        Vector2[] uvs = new Vector2[vertexCount * 2];
        int[] triangles = new int[quadCount * 6];

        for (int i = 0; i < vertexCount; i++)
        {
            // ワールド座標をローカル座標に変換（親の Transform 基準）
            vertices[i * 2]     = transform.InverseTransformPoint(bottomVerts[i]);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(topVerts[i]);

            // U は 0〜1 への正規化ではなく、仕様通り「コース上の距離（m）」をそのまま入れる。
            float u = distances[i];
            uvs[i * 2]     = new Vector2(u, 0f); // 下端
            uvs[i * 2 + 1] = new Vector2(u, 1f); // 上端
        }

        for (int i = 0; i < quadCount; i++)
        {
            int baseVertex = i * 2;
            int triBase = i * 6;

            // 表面（片面はシェーダーの Both で対応するが、
            // 外から見える向きを表にしておく）
            triangles[triBase]     = baseVertex;
            triangles[triBase + 1] = baseVertex + 1;
            triangles[triBase + 2] = baseVertex + 2;

            triangles[triBase + 3] = baseVertex + 1;
            triangles[triBase + 4] = baseVertex + 3;
            triangles[triBase + 5] = baseVertex + 2;
        }

        Mesh mesh = new Mesh
        {
            name = childName
        };
        // 頂点数が65535を超える巨大なコースにも対応できるように32bitインデックスフォーマットを指定
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // ── 子 GameObject 作成 ──
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

    /// <summary>
    /// 指定距離地点の上端・下端のワールド座標を計算する。
    /// コース幅は区間ごとに変わるため、その距離での半径（Radius Scale 反映済み）を使う。
    /// </summary>
    private void SamplePoint(float distance, float sign, out Vector3 bottom, out Vector3 top)
    {
        Vector3 center = _courseSpline.EvaluatePositionByDistance(distance);
        Vector3 right  = _courseSpline.EvaluateRightByDistance(distance);
        Vector3 up     = _courseSpline.EvaluateUpByDistance(distance);
        float tunnelRadius = _courseSpline.GetTunnelRadiusByDistance(distance);

        Vector3 basePos = center + right * sign * (tunnelRadius + _offset);
        bottom = basePos - up * (_height * 0.5f);
        top    = basePos + up * (_height * 0.5f);
    }

    /// <summary>
    /// 生成済みの子オブジェクトをすべて削除する。
    /// </summary>
    private void ClearChildren()
    {
        // 逆順で削除（子のインデックスがずれないように）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
