using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// CourseSpline から立体的な3Dミニマップ用のコースメッシュ（帯リボン または 円柱トンネル）と機体アイコンを自動生成し、
/// 【方式A：専用レイヤー＋斜め見下ろしサブカメラ（RenderTexture UI）】と
/// 【方式B：メインカメラ前 1/1000スケール 3Dミニチュアホログラム】、
/// 【自機周辺ズーム】と【コース全体マップ】、
/// さらに【平らな帯（Ribbon）】と【円柱トンネル（CylinderTube）】をリアルタイムに切り替えて比較・検証できるコンポーネント。
/// </summary>
[ExecuteAlways]
public class CourseMinimapController : MonoBehaviour
{
    public enum MinimapDisplayMode
    {
        [InspectorName("方式A：サブカメラ ＋ RenderTexture (UI表示)")]
        ModeA_RenderTexture = 0,

        [InspectorName("方式B：メインカメラ前 3Dミニチュアホログラム")]
        ModeB_3DMiniature = 1
    }

    public enum MinimapScopeMode
    {
        [InspectorName("周辺ズーム（自機中心・周囲切り抜き）")]
        AroundPlayer = 0,

        [InspectorName("コース全体マップ（全域表示）")]
        WholeCourse = 1
    }

    public enum MinimapMeshShape
    {
        [InspectorName("平らな帯（フラットリボン）")]
        FlatRibbon = 0,

        [InspectorName("円柱トンネル（CourseSplineギズモ風チューブ）")]
        CylinderTube = 1
    }

    // ──────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────

    [Header("表示モードの切り替え（実行中もリアルタイム変更可）")]
    [Tooltip("方式A（サブカメラ＋UI）と 方式B（カメラ前3Dミニチュア）を切り替えます")]
    [SerializeField] private MinimapDisplayMode _displayMode = MinimapDisplayMode.ModeA_RenderTexture;

    [Tooltip("自機周辺だけをズーム表示するか、コース全体を俯瞰表示するかを切り替えます")]
    [SerializeField] private MinimapScopeMode _scopeMode = MinimapScopeMode.AroundPlayer;

    [Tooltip("コースの形状を「平らな帯」にするか「円柱トンネル（スプラインギズモ風）」にするかを切り替えます")]
    [SerializeField] private MinimapMeshShape _meshShape = MinimapMeshShape.CylinderTube;

    [Header("コース・機体参照")]
    [Tooltip("コースの Spline（未指定ならシーン内から自動検索）")]
    [SerializeField] private CourseSpline _courseSpline;

    [Tooltip("自機の Transform（中心基準＆進行方向基準）")]
    [SerializeField] private Transform _playerTarget;

    [Tooltip("他機（ライバル機）の Transform リスト（任意・最大8機など）")]
    [SerializeField] private List<Transform> _racerTargets = new List<Transform>();

    [Header("コースメッシュ生成設定")]
    [Tooltip("サンプリング間隔（m）。ミニマップなので 3〜5m でも十分滑らかで軽量です")]
    [SerializeField] private float _sampleInterval = 4f;

    [Tooltip("実際のコース幅・トンネル半径（TunnelRadius）に対するミニマップの太さ倍率")]
    [SerializeField] private float _roadWidthScale = 1.0f;

    [Tooltip("【円柱トンネル時】円周の分割数（8〜16推奨。12なら綺麗な十二角柱チューブになります）")]
    [Range(4, 32)]
    [SerializeField] private int _cylinderRadialSegments = 12;

    [Tooltip("【円柱トンネル時】円周方向に走らせる縦ワイヤーラインの本数（ギズモ風の筋の数）")]
    [Range(0, 16)]
    [SerializeField] private int _cylinderWireCount = 8;

    [Tooltip("コースのバンク（傾き）をミニマップのメッシュにも反映するか")]
    [SerializeField] private bool _applyBankToMesh = true;

    [Header("立体表現・アイコン設定（コースメートル単位）")]
    [Tooltip("【周辺ズーム時】自機を中心に切り抜いて表示する球体半径（m）")]
    [SerializeField] private float _visibleRadius = 260f;

    [Tooltip("高低差の色分けが最大になる高さの差（m）")]
    [SerializeField] private float _heightColorRange = 35f;

    [Tooltip("自機アイコンの大きさ（コース上のメートル換算）")]
    [SerializeField] private float _playerIconSize = 14f;

    [Tooltip("他機アイコンの大きさ（コース上のメートル換算）")]
    [SerializeField] private float _racerIconSize = 10f;

    [Tooltip("【コース全体マップ時】全体が縮小されるためアイコンを見やすく拡大する倍率")]
    [SerializeField] private float _wholeCourseIconScaleMultiplier = 2.5f;

    [Tooltip("【平らな帯モード時】アイコンを道から少し浮かせる高さ（m）。円柱モード時はトンネル中心（0m）に配置されます")]
    [SerializeField] private float _iconHeightOffset = 3f;

    [Header("共通アングル・回転設定")]
    [Tooltip("斜め上から見下ろすピッチ角度（度）。35〜55度が立体交差を見やすい")]
    [Range(10f, 85f)]
    [SerializeField] private float _pitchAngle = 42f;

    [Tooltip("【周辺ズーム時】自機の向きに合わせてミニマップを回転させるか（ONで進行方向が常に奥＝上）")]
    [SerializeField] private bool _rotateWithPlayer = true;

    [Tooltip("【コース全体マップ時】コース全体マップでも自機の向きに合わせて回すか（OFFなら方角固定で安定）")]
    [SerializeField] private bool _rotateInWholeCourse = false;

    [Tooltip("進行方向の向き補正角度（度）。もし機体モデルの正面軸が逆向き等の場合は 180 などを指定")]
    [SerializeField] private float _yawOffset = 0f;

    [Tooltip("コース全体マップ固定時の水平回転角度（度。コース全体が見やすい向きに調整可能）")]
    [SerializeField] private float _wholeCourseFixedYaw = 0f;

    [Header("【方式A】サブカメラ ＋ RenderTexture 設定")]
    [Tooltip("ミニマップ専用に使うレイヤー番号（デフォルト 30 = 空きレイヤー）")]
    [Range(8, 31)]
    [SerializeField] private int _minimapLayer = 30;

    [Tooltip("【周辺ズーム時】サブカメラの自機からの引き距離（m）")]
    [SerializeField] private float _cameraDistance = 320f;

    [Tooltip("【周辺ズーム時】サブカメラの注視点の前方オフセット（m。少し先を映す）")]
    [SerializeField] private float _lookAheadOffset = 45f;

    [Tooltip("【コース全体マップ時】自動計算された全体カメラ距離に対するズーム倍率（小さいほど寄る）")]
    [Range(0.3f, 2.5f)]
    [SerializeField] private float _wholeCourseCameraDistanceMultiplier = 1.15f;

    [Tooltip("UI上のミニマップ表示サイズ（ピクセル）")]
    [SerializeField] private Vector2 _uiSize = new Vector2(280f, 280f);

    [Tooltip("UI上の画面左下からの余白（ピクセル）")]
    [SerializeField] private Vector2 _uiMargin = new Vector2(32f, 32f);

    [Header("【方式B】メインカメラ前 3Dミニチュア設定")]
    [Tooltip("メインカメラから見たミニチュア中心の配置オフセット（X:左右, Y:上下, Z:前方距離）")]
    [SerializeField] private Vector3 _miniatureScreenOffset = new Vector3(-0.42f, -0.22f, 0.85f);

    [Tooltip("【周辺ズーム時】ミニチュア全体の縮小スケール（例: 0.0012 = 1/833スケール）")]
    [SerializeField] private float _miniatureScale = 0.0012f;

    [Tooltip("【コース全体マップ時】カメラ前での全体ミニチュアの見かけサイズ（m。0.35なら画面上で約35cm幅に自動縮小）")]
    [SerializeField] private float _wholeCourseMiniatureApparentSize = 0.36f;

    [Tooltip("方式Bでコースの壁等にめり込んでも常に最前面に描画するか（ZTest Always）")]
    [SerializeField] private bool _alwaysOnTopInModeB = true;

    [Header("マテリアル（未割当なら自動生成）")]
    [SerializeField] private Material _roadMaterial;

    // ──────────────────────────────────────────────
    // 内部キャッシュ・生成オブジェクト
    // ──────────────────────────────────────────────

    private Transform _hologramPivot;
    private Transform _meshRoot;
    private MeshRenderer _roadRenderer;
    private MeshRenderer _playerIconRenderer;
    private readonly List<MeshRenderer> _racerIconRenderers = new List<MeshRenderer>();

    private Camera _subCamera;
    private RenderTexture _renderTexture;
    private Canvas _uiCanvas;
    private RawImage _uiRawImage;

    private Material _runtimeRoadMat;
    private Material _runtimePlayerIconMat;
    private Material _runtimeRacerIconMat;
    private MaterialPropertyBlock _mpb;

    private Bounds _courseWorldBounds;
    private float _courseBoundingRadius = 300f;

    private float _currentSmoothYaw;
    private bool _yawInitialized;
    private MinimapMeshShape _lastBuiltShape = (MinimapMeshShape)(-1);
    private int _lastBuiltSegments = -1;

    private static readonly int PlayerLocalPosId    = Shader.PropertyToID("_PlayerLocalPos");
    private static readonly int VisibleRadiusId     = Shader.PropertyToID("_VisibleRadius");
    private static readonly int HeightColorRangeId  = Shader.PropertyToID("_HeightColorRange");
    private static readonly int IsCylinderId        = Shader.PropertyToID("_IsCylinder");
    private static readonly int CylinderWireCountId = Shader.PropertyToID("_CylinderWireCount");
    private static readonly int IsIconId            = Shader.PropertyToID("_IsIcon");
    private static readonly int IconColorId         = Shader.PropertyToID("_IconColor");
    private static readonly int ZTestId             = Shader.PropertyToID("_ZTest");

    private Camera _modifiedMainCamera;
    private int _lastExcludedLayer = -1;

    // ──────────────────────────────────────────────
    // ライフサイクル
    // ──────────────────────────────────────────────

    private void OnEnable()
    {
        AutoFindReferences();
        EnsureHierarchyAndRebuild();
    }

    private void OnDisable()
    {
        RestoreMainCameraCullingMask();
        ReleaseRenderTextureSafely();
    }

    private void OnDestroy()
    {
        RestoreMainCameraCullingMask();
        ReleaseRenderTextureSafely();
    }

    /// <summary>
    /// 方式Aでメインカメラから除外したミニマップ専用レイヤー（CullingMask）を安全に元通り復元する。
    /// </summary>
    private void RestoreMainCameraCullingMask()
    {
        if (_modifiedMainCamera != null && _lastExcludedLayer >= 0)
        {
            _modifiedMainCamera.cullingMask |= (1 << _lastExcludedLayer);
            _lastExcludedLayer = -1;
            _modifiedMainCamera = null;
        }
    }

    /// <summary>
    /// Camera.targetTexture にセットされたまま RenderTexture を Release/Destroy すると
    /// Unity が "Releasing render texture that is set as Camera.targetTexture!" エラーを出すため、
    /// 必ず先に Camera と RawImage から参照を外してから破棄する。
    /// </summary>
    private void ReleaseRenderTextureSafely()
    {
        if (_subCamera != null)
        {
            _subCamera.targetTexture = null;
        }
        else
        {
            Transform camTrans = transform.Find("Minimap_SubCamera");
            if (camTrans != null)
            {
                Camera cam = camTrans.GetComponent<Camera>();
                if (cam != null) cam.targetTexture = null;
            }
        }

        if (_uiRawImage != null)
        {
            _uiRawImage.texture = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            if (Application.isPlaying) Destroy(_renderTexture);
            else DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
    }

    private void OnValidate()
    {
        _sampleInterval = Mathf.Max(0.5f, _sampleInterval);
        _roadWidthScale = Mathf.Max(0.1f, _roadWidthScale);
        _cylinderRadialSegments = Mathf.Clamp(_cylinderRadialSegments, 4, 32);
        _cylinderWireCount = Mathf.Clamp(_cylinderWireCount, 0, 16);
        _visibleRadius = Mathf.Max(10f, _visibleRadius);
        _heightColorRange = Mathf.Max(1f, _heightColorRange);
        _cameraDistance = Mathf.Max(20f, _cameraDistance);
        _miniatureScale = Mathf.Clamp(_miniatureScale, 0.00005f, 0.05f);
        _wholeCourseMiniatureApparentSize = Mathf.Clamp(_wholeCourseMiniatureApparentSize, 0.05f, 1.5f);
        _wholeCourseIconScaleMultiplier = Mathf.Max(0.5f, _wholeCourseIconScaleMultiplier);
    }

    private void LateUpdate()
    {
        AutoFindReferences();
        if (_courseSpline == null) return;

        bool shapeChanged = (_lastBuiltShape != _meshShape) ||
                            (_meshShape == MinimapMeshShape.CylinderTube && _lastBuiltSegments != _cylinderRadialSegments);

        if (_meshRoot == null || _roadRenderer == null || shapeChanged)
        {
            EnsureHierarchyAndRebuild();
            if (_meshRoot == null) return;
        }

        UpdateModeStateAndTransforms();
        UpdateIconsAndShaderProperties();
    }

    // ──────────────────────────────────────────────
    // 公開メソッド（インスペクターボタン等から呼び出し）
    // ──────────────────────────────────────────────

    public void SetDisplayMode(MinimapDisplayMode mode)
    {
        _displayMode = mode;
        UpdateModeStateAndTransforms();
        UpdateIconsAndShaderProperties();
    }

    public MinimapDisplayMode GetDisplayMode() => _displayMode;

    public void SetScopeMode(MinimapScopeMode scope)
    {
        _scopeMode = scope;
        _yawInitialized = false;
        UpdateModeStateAndTransforms();
        UpdateIconsAndShaderProperties();
    }

    public MinimapScopeMode GetScopeMode() => _scopeMode;

    public void SetMeshShape(MinimapMeshShape shape)
    {
        if (_meshShape != shape)
        {
            _meshShape = shape;
            BuildRoadMesh();
        }
        UpdateModeStateAndTransforms();
        UpdateIconsAndShaderProperties();
    }

    public MinimapMeshShape GetMeshShape() => _meshShape;

    public void Rebuild()
    {
        AutoFindReferences();
        ClearGeneratedChildren();
        EnsureHierarchyAndRebuild();
        UpdateModeStateAndTransforms();
        UpdateIconsAndShaderProperties();
    }

    // ──────────────────────────────────────────────
    // 自動参照検索・階層構築
    // ──────────────────────────────────────────────

    private void AutoFindReferences()
    {
        if (_courseSpline == null)
        {
            _courseSpline = FindFirstObjectByType<CourseSpline>();
        }

        if (_playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTarget = playerObj.transform;
            }
            else if (Camera.main != null)
            {
                _playerTarget = Camera.main.transform;
            }
        }
    }

    private void EnsureHierarchyAndRebuild()
    {
        if (_courseSpline == null || _courseSpline.TotalLength <= 0f) return;

        EnsureMaterials();

        // 1. ピボットとメッシュルートの準備
        Transform existingPivot = transform.Find("Minimap_HologramPivot");
        if (existingPivot != null)
        {
            _hologramPivot = existingPivot;
        }
        else
        {
            GameObject pivotObj = new GameObject("Minimap_HologramPivot");
            pivotObj.transform.SetParent(transform, false);
            _hologramPivot = pivotObj.transform;
        }

        Transform existingMeshRoot = _hologramPivot.Find("Minimap_MeshRoot");
        if (existingMeshRoot != null)
        {
            _meshRoot = existingMeshRoot;
        }
        else
        {
            GameObject rootObj = new GameObject("Minimap_MeshRoot");
            rootObj.transform.SetParent(_hologramPivot, false);
            _meshRoot = rootObj.transform;
        }

        // 2. コースメッシュ（帯 または 円柱トンネル）生成 ＆ コース全体Bounds計測
        BuildRoadMesh();

        // 3. 自機＆他機アイコン生成
        BuildIcons();

        // 4. 方式A用のサブカメラ＆UI生成
        EnsureSubCameraAndUI();
    }

    private void EnsureMaterials()
    {
        Shader minimapShader = Shader.Find("Custom/CourseMinimap");
        if (minimapShader == null) return;

        if (_roadMaterial != null)
        {
            _runtimeRoadMat = _roadMaterial;
        }
        else if (_runtimeRoadMat == null)
        {
            _runtimeRoadMat = new Material(minimapShader)
            {
                name = "M_CourseMinimap_Road_Runtime",
                hideFlags = HideFlags.DontSave
            };
        }

        if (_runtimePlayerIconMat == null)
        {
            _runtimePlayerIconMat = new Material(minimapShader)
            {
                name = "M_CourseMinimap_PlayerIcon_Runtime",
                hideFlags = HideFlags.DontSave
            };
            _runtimePlayerIconMat.SetFloat(IsIconId, 1f);
            _runtimePlayerIconMat.SetColor(IconColorId, new Color(3.5f, 2.8f, 0.2f, 1f)); // 明るいイエローゴールド
        }

        if (_runtimeRacerIconMat == null)
        {
            _runtimeRacerIconMat = new Material(minimapShader)
            {
                name = "M_CourseMinimap_RacerIcon_Runtime",
                hideFlags = HideFlags.DontSave
            };
            _runtimeRacerIconMat.SetFloat(IsIconId, 1f);
            _runtimeRacerIconMat.SetColor(IconColorId, new Color(3.2f, 0.4f, 0.8f, 0.95f)); // マゼンタレッド
        }

        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }
    }

    // ──────────────────────────────────────────────
    // メッシュ生成（コース本来のワールド座標系＝メートル単位で頂点を格納）
    // ──────────────────────────────────────────────

    private void BuildRoadMesh()
    {
        if (_meshRoot == null || _courseSpline == null || _courseSpline.TotalLength <= 0f) return;

        Transform oldRoad = _meshRoot.Find("Minimap_Road");
        if (oldRoad != null)
        {
            if (Application.isPlaying) Destroy(oldRoad.gameObject);
            else DestroyImmediate(oldRoad.gameObject);
        }

        float totalLength = _courseSpline.TotalLength;
        bool isLoop = _courseSpline.IsLoop;
        float interval = Mathf.Max(0.5f, _sampleInterval);
        int sampleCount = Mathf.FloorToInt(totalLength / interval) + 1;

        List<float> sampleDistances = new List<float>(sampleCount + 2);
        for (int i = 0; i < sampleCount; i++)
        {
            sampleDistances.Add(Mathf.Min(i * interval, totalLength));
        }
        if (sampleDistances[sampleDistances.Count - 1] < totalLength - 0.01f)
        {
            sampleDistances.Add(totalLength);
        }
        if (isLoop)
        {
            sampleDistances.Add(0f);
        }

        int ringCount = sampleDistances.Count;
        if (ringCount < 2) return;

        Mesh roadMesh = (_meshShape == MinimapMeshShape.CylinderTube)
            ? GenerateCylinderMesh(sampleDistances)
            : GenerateRibbonMesh(sampleDistances);

        _lastBuiltShape = _meshShape;
        _lastBuiltSegments = _cylinderRadialSegments;

        GameObject roadObj = new GameObject("Minimap_Road");
        roadObj.transform.SetParent(_meshRoot, false);

        MeshFilter mf = roadObj.AddComponent<MeshFilter>();
        mf.sharedMesh = roadMesh;

        _roadRenderer = roadObj.AddComponent<MeshRenderer>();
        _roadRenderer.sharedMaterial = _runtimeRoadMat;
        _roadRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _roadRenderer.receiveShadows = false;
    }

    /// <summary>
    /// 【平らな帯（FlatRibbon）】メッシュを生成する
    /// </summary>
    private Mesh GenerateRibbonMesh(List<float> sampleDistances)
    {
        int ringCount = sampleDistances.Count;
        int quadCount = ringCount - 1;

        Vector3[] vertices = new Vector3[ringCount * 2];
        Vector3[] normals = new Vector3[ringCount * 2];
        Vector2[] uvs = new Vector2[ringCount * 2];
        int[] triangles = new int[quadCount * 6];

        float accumulatedDist = 0f;
        Vector3 prevCenter = Vector3.zero;
        Bounds calcBounds = new Bounds();
        bool boundsInit = false;

        for (int i = 0; i < ringCount; i++)
        {
            float d = sampleDistances[i];
            SampleSplineFrame(d, out Vector3 center, out Vector3 right, out Vector3 up, out float radius);

            Vector3 leftEdge = center - right * radius;
            Vector3 rightEdge = center + right * radius;

            if (!boundsInit)
            {
                calcBounds = new Bounds(center, Vector3.zero);
                boundsInit = true;
            }
            calcBounds.Encapsulate(leftEdge);
            calcBounds.Encapsulate(rightEdge);

            if (i > 0) accumulatedDist += Vector3.Distance(center, prevCenter);
            prevCenter = center;

            vertices[i * 2]     = leftEdge;
            vertices[i * 2 + 1] = rightEdge;
            normals[i * 2]      = up;
            normals[i * 2 + 1]  = up;
            uvs[i * 2]          = new Vector2(accumulatedDist, 0f);
            uvs[i * 2 + 1]      = new Vector2(accumulatedDist, 1f);
        }

        _courseWorldBounds = calcBounds;
        _courseBoundingRadius = Mathf.Max(50f, calcBounds.extents.magnitude);

        for (int i = 0; i < quadCount; i++)
        {
            int b = i * 2;
            int t = i * 6;

            triangles[t]     = b;
            triangles[t + 1] = b + 2;
            triangles[t + 2] = b + 1;

            triangles[t + 3] = b + 1;
            triangles[t + 4] = b + 2;
            triangles[t + 5] = b + 3;
        }

        Mesh mesh = new Mesh
        {
            name = "MinimapRibbonMesh",
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return mesh;
    }

    /// <summary>
    /// 【円柱トンネル（CylinderTube）】CourseSplineのトンネルギズモのように断面円を繋いだチューブメッシュを生成する
    /// </summary>
    private Mesh GenerateCylinderMesh(List<float> sampleDistances)
    {
        int ringCount = sampleDistances.Count;
        int segs = Mathf.Clamp(_cylinderRadialSegments, 4, 32);
        int vertsPerRing = segs + 1; // UV.y = 0〜1 の継ぎ目のため +1

        Vector3[] vertices = new Vector3[ringCount * vertsPerRing];
        Vector3[] normals = new Vector3[ringCount * vertsPerRing];
        Vector2[] uvs = new Vector2[ringCount * vertsPerRing];
        int[] triangles = new int[(ringCount - 1) * segs * 6];

        float accumulatedDist = 0f;
        Vector3 prevCenter = Vector3.zero;
        Bounds calcBounds = new Bounds();
        bool boundsInit = false;

        for (int i = 0; i < ringCount; i++)
        {
            float d = sampleDistances[i];
            SampleSplineFrame(d, out Vector3 center, out Vector3 right, out Vector3 up, out float radius);

            if (!boundsInit)
            {
                calcBounds = new Bounds(center, Vector3.zero);
                boundsInit = true;
            }

            if (i > 0) accumulatedDist += Vector3.Distance(center, prevCenter);
            prevCenter = center;

            int ringBase = i * vertsPerRing;
            for (int s = 0; s <= segs; s++)
            {
                float vRatio = (float)s / segs;
                float angleRad = vRatio * Mathf.PI * 2f;
                float cosA = Mathf.Cos(angleRad);
                float sinA = Mathf.Sin(angleRad);

                // 断面円周上の外向き法線と頂点座標
                Vector3 radialDir = (right * cosA + up * sinA).normalized;
                Vector3 pos = center + radialDir * radius;

                vertices[ringBase + s] = pos;
                normals[ringBase + s]  = radialDir;
                uvs[ringBase + s]      = new Vector2(accumulatedDist, vRatio);

                calcBounds.Encapsulate(pos);
            }
        }

        _courseWorldBounds = calcBounds;
        _courseBoundingRadius = Mathf.Max(50f, calcBounds.extents.magnitude);

        int t = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            int currRing = i * vertsPerRing;
            int nextRing = (i + 1) * vertsPerRing;

            for (int s = 0; s < segs; s++)
            {
                int v00 = currRing + s;
                int v01 = currRing + s + 1;
                int v10 = nextRing + s;
                int v11 = nextRing + s + 1;

                triangles[t++] = v00;
                triangles[t++] = v10;
                triangles[t++] = v01;

                triangles[t++] = v01;
                triangles[t++] = v10;
                triangles[t++] = v11;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "MinimapCylinderTubeMesh",
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return mesh;
    }

    private void SampleSplineFrame(float distance, out Vector3 center, out Vector3 right, out Vector3 up, out float radius)
    {
        center = _courseSpline.EvaluatePositionByDistance(distance);
        right = _courseSpline.EvaluateRightByDistance(distance);
        up = _courseSpline.EvaluateUpByDistance(distance);

        if (!_applyBankToMesh)
        {
            Vector3 forward = Vector3.Cross(right, up).normalized;
            Vector3 flatRight = Vector3.Cross(Vector3.up, forward);
            if (flatRight.sqrMagnitude > 0.001f)
            {
                right = flatRight.normalized;
                up = Vector3.up;
            }
        }

        radius = _courseSpline.GetTunnelRadiusByDistance(distance) * _roadWidthScale;
    }

    // ──────────────────────────────────────────────
    // 自機・他機アイコンの3Dメッシュ生成（プレハブ不要）
    // ──────────────────────────────────────────────

    private void BuildIcons()
    {
        for (int i = _meshRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _meshRoot.GetChild(i);
            if (child.name.StartsWith("Minimap_PlayerIcon") || child.name.StartsWith("Minimap_RacerIcon"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // 自機矢印アイコン
        GameObject playerIconObj = new GameObject("Minimap_PlayerIcon");
        playerIconObj.transform.SetParent(_meshRoot, false);
        MeshFilter pmf = playerIconObj.AddComponent<MeshFilter>();
        pmf.sharedMesh = CreateArrowIconMesh();
        _playerIconRenderer = playerIconObj.AddComponent<MeshRenderer>();
        _playerIconRenderer.sharedMaterial = _runtimePlayerIconMat;
        _playerIconRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _playerIconRenderer.receiveShadows = false;

        // 他機ダイヤモンドアイコン
        _racerIconRenderers.Clear();
        Mesh diamondMesh = CreateDiamondIconMesh();
        for (int i = 0; i < _racerTargets.Count; i++)
        {
            GameObject racerObj = new GameObject($"Minimap_RacerIcon_{i}");
            racerObj.transform.SetParent(_meshRoot, false);
            MeshFilter rmf = racerObj.AddComponent<MeshFilter>();
            rmf.sharedMesh = diamondMesh;
            MeshRenderer rmr = racerObj.AddComponent<MeshRenderer>();
            rmr.sharedMaterial = _runtimeRacerIconMat;
            rmr.shadowCastingMode = ShadowCastingMode.Off;
            rmr.receiveShadows = false;
            _racerIconRenderers.Add(rmr);
        }
    }

    private static Mesh CreateArrowIconMesh()
    {
        Mesh m = new Mesh { name = "MinimapArrowIcon" };
        Vector3[] v = new Vector3[]
        {
            new Vector3( 0.0f,  0.0f,  1.2f), // 0: 先端 (+Z)
            new Vector3(-0.8f,  0.0f, -0.9f), // 1: 左後翼
            new Vector3( 0.8f,  0.0f, -0.9f), // 2: 右後翼
            new Vector3( 0.0f,  0.4f, -0.2f), // 3: 上部中央稜線
            new Vector3( 0.0f,  0.0f, -0.45f) // 4: 後部くびれ
        };
        int[] tris = new int[]
        {
            0, 3, 1,
            0, 2, 3,
            3, 4, 1,
            3, 2, 4,
            0, 1, 4,
            0, 4, 2
        };
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        return m;
    }

    private static Mesh CreateDiamondIconMesh()
    {
        Mesh m = new Mesh { name = "MinimapDiamondIcon" };
        Vector3[] v = new Vector3[]
        {
            new Vector3( 0.0f,  0.8f,  0.0f), // 0: 上
            new Vector3( 0.0f, -0.8f,  0.0f), // 1: 下
            new Vector3( 0.0f,  0.0f,  0.7f), // 2: 前
            new Vector3( 0.7f,  0.0f,  0.0f), // 3: 右
            new Vector3( 0.0f,  0.0f, -0.7f), // 4: 後
            new Vector3(-0.7f,  0.0f,  0.0f)  // 5: 左
        };
        int[] tris = new int[]
        {
            0, 2, 3,  0, 3, 4,  0, 4, 5,  0, 5, 2,
            1, 3, 2,  1, 4, 3,  1, 5, 4,  1, 2, 5
        };
        m.SetVertices(v);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        return m;
    }

    // ──────────────────────────────────────────────
    // 方式A用：サブカメラ ＋ RenderTexture ＋ UI Canvas 自動構築
    // ──────────────────────────────────────────────

    private void EnsureSubCameraAndUI()
    {
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_CourseMinimap_Runtime",
                antiAliasing = 2
            };
            _renderTexture.Create();
        }

        Transform camTrans = transform.Find("Minimap_SubCamera");
        if (camTrans == null)
        {
            GameObject camObj = new GameObject("Minimap_SubCamera");
            camObj.transform.SetParent(transform, false);
            _subCamera = camObj.AddComponent<Camera>();
        }
        else
        {
            _subCamera = camTrans.GetComponent<Camera>();
            if (_subCamera == null) _subCamera = camTrans.gameObject.AddComponent<Camera>();
        }

        _subCamera.clearFlags = CameraClearFlags.SolidColor;
        _subCamera.backgroundColor = new Color(0.01f, 0.04f, 0.08f, 0.55f);
        _subCamera.cullingMask = 1 << _minimapLayer;
        _subCamera.targetTexture = _renderTexture;
        _subCamera.nearClipPlane = 1f;
        _subCamera.farClipPlane = 10000f;
        _subCamera.fieldOfView = 45f;

        UniversalAdditionalCameraData urpCamData = _subCamera.GetUniversalAdditionalCameraData();
        if (urpCamData != null)
        {
            urpCamData.renderPostProcessing = false;
            urpCamData.renderShadows = false;
        }

        Transform canvasTrans = transform.Find("Minimap_UICanvas");
        if (canvasTrans == null)
        {
            GameObject canvasObj = new GameObject("Minimap_UICanvas");
            canvasObj.transform.SetParent(transform, false);
            _uiCanvas = canvasObj.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject rawImgObj = new GameObject("Minimap_RawImage");
            rawImgObj.transform.SetParent(canvasObj.transform, false);
            _uiRawImage = rawImgObj.AddComponent<RawImage>();
            _uiRawImage.raycastTarget = false;
        }
        else
        {
            _uiCanvas = canvasTrans.GetComponent<Canvas>();
            _uiRawImage = canvasTrans.GetComponentInChildren<RawImage>(true);
        }

        if (_uiRawImage != null)
        {
            _uiRawImage.texture = _renderTexture;
            RectTransform rt = _uiRawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = _uiMargin;
            rt.sizeDelta = _uiSize;
        }
    }

    // ──────────────────────────────────────────────
    // 毎フレームの座標・回転・モード切り替え更新
    // ──────────────────────────────────────────────

    private void UpdateModeStateAndTransforms()
    {
        Vector3 playerPos = _playerTarget != null ? _playerTarget.position : _courseSpline.EvaluatePositionByDistance(0f);
        bool isWholeCourse = (_scopeMode == MinimapScopeMode.WholeCourse);
        float targetYaw = GetTargetMapYaw(isWholeCourse);

        if (!_yawInitialized || !Application.isPlaying)
        {
            _currentSmoothYaw = targetYaw;
            _yawInitialized = true;
        }
        else
        {
            _currentSmoothYaw = Mathf.LerpAngle(_currentSmoothYaw, targetYaw, Time.deltaTime * 12f);
        }

        bool isModeA = (_displayMode == MinimapDisplayMode.ModeA_RenderTexture);

        if (_subCamera != null && _subCamera.gameObject.activeSelf != isModeA)
        {
            _subCamera.gameObject.SetActive(isModeA);
        }
        if (_uiCanvas != null && _uiCanvas.gameObject.activeSelf != isModeA)
        {
            _uiCanvas.gameObject.SetActive(isModeA);
        }

        int targetLayer = isModeA ? _minimapLayer : 0;
        SetLayerRecursively(_hologramPivot.gameObject, targetLayer);

        Camera mainCam = Camera.main;
        if (isModeA)
        {
            if (mainCam != null)
            {
                // レイヤー番号が変更された場合は前のレイヤーを復元してから新しいレイヤーを除外
                if (_lastExcludedLayer >= 0 && _lastExcludedLayer != _minimapLayer && _modifiedMainCamera != null)
                {
                    _modifiedMainCamera.cullingMask |= (1 << _lastExcludedLayer);
                }

                mainCam.cullingMask &= ~(1 << _minimapLayer);
                _modifiedMainCamera = mainCam;
                _lastExcludedLayer = _minimapLayer;
            }
        }
        else
        {
            // 方式B（3Dミニチュア）になったらメインカメラのCullingMaskを元通り復元
            RestoreMainCameraCullingMask();
        }

        if (isModeA)
        {
            // ── 【方式A】メッシュはワールド原点（原寸大）、サブカメラが斜め上から映す ──
            _hologramPivot.position = Vector3.zero;
            _hologramPivot.rotation = Quaternion.identity;
            _hologramPivot.localScale = Vector3.one;

            _meshRoot.localPosition = Vector3.zero;
            _meshRoot.localRotation = Quaternion.identity;
            _meshRoot.localScale = Vector3.one;

            if (_subCamera != null)
            {
                _subCamera.cullingMask = 1 << _minimapLayer;
                Quaternion yawRot = Quaternion.Euler(0f, _currentSmoothYaw, 0f);
                Quaternion camRot = yawRot * Quaternion.Euler(_pitchAngle, 0f, 0f);

                if (isWholeCourse)
                {
                    float halfFovRad = _subCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    float autoFitDist = (_courseBoundingRadius / Mathf.Max(0.1f, Mathf.Tan(halfFovRad))) * _wholeCourseCameraDistanceMultiplier;
                    Vector3 focusPoint = _courseWorldBounds.center;
                    _subCamera.transform.position = focusPoint - (camRot * Vector3.forward * autoFitDist);
                    _subCamera.transform.rotation = camRot;
                }
                else
                {
                    Vector3 focusPoint = playerPos + (yawRot * Vector3.forward * _lookAheadOffset);
                    _subCamera.transform.position = focusPoint - (camRot * Vector3.forward * _cameraDistance);
                    _subCamera.transform.rotation = camRot;
                }
            }

            if (_uiRawImage != null)
            {
                RectTransform rt = _uiRawImage.rectTransform;
                rt.anchoredPosition = _uiMargin;
                rt.sizeDelta = _uiSize;
            }
        }
        else
        {
            // ── 【方式B】メインカメラの画面左下（コックピット前）に3Dミニチュアを直接浮かべる ──
            Transform camTrans = mainCam != null ? mainCam.transform : (_playerTarget != null ? _playerTarget : transform);

            // ※重要：Unity左手系において、カメラの目の前にある模型を「手前が下、奥（進行方向）が上」に見下ろすように傾けるには、
            // カメラ回転(+Pitch)の逆変換である「-_pitchAngle」をX軸に掛ける必要がある！
            Quaternion pitchRot = Quaternion.Euler(-_pitchAngle, 0f, 0f);
            Quaternion invYawRot = Quaternion.Euler(0f, -_currentSmoothYaw, 0f);

            _hologramPivot.position = camTrans.TransformPoint(_miniatureScreenOffset);
            _hologramPivot.rotation = camTrans.rotation * pitchRot * invYawRot;

            if (isWholeCourse)
            {
                float autoScale = _wholeCourseMiniatureApparentSize / Mathf.Max(10f, _courseBoundingRadius * 2f);
                _hologramPivot.localScale = Vector3.one * autoScale;
                _meshRoot.localPosition = -_courseWorldBounds.center;
            }
            else
            {
                _hologramPivot.localScale = Vector3.one * _miniatureScale;
                _meshRoot.localPosition = -playerPos;
            }

            _meshRoot.localRotation = Quaternion.identity;
            _meshRoot.localScale = Vector3.one;
        }
    }

    private void UpdateIconsAndShaderProperties()
    {
        Vector3 playerPos = _playerTarget != null ? _playerTarget.position : _courseSpline.EvaluatePositionByDistance(0f);
        bool isWholeCourse = (_scopeMode == MinimapScopeMode.WholeCourse);
        bool isCylinder = (_meshShape == MinimapMeshShape.CylinderTube);
        float iconScaleMul = isWholeCourse ? _wholeCourseIconScaleMultiplier : 1f;

        // 円柱モードのときはチューブのど真ん中（オフセット0）にアイコンを配置し、平らな帯のときだけ少し上に浮かせる
        Vector3 upOffset = isCylinder ? Vector3.zero : Vector3.up * (_iconHeightOffset * iconScaleMul);

        // 1. 自機アイコンの位置・向き・サイズ更新
        if (_playerIconRenderer != null)
        {
            Transform pt = _playerIconRenderer.transform;
            pt.localPosition = playerPos + upOffset;
            float iconYaw = (_playerTarget != null ? _playerTarget.eulerAngles.y : 0f) + _yawOffset;
            pt.localRotation = Quaternion.Euler(0f, iconYaw, 0f);
            pt.localScale = Vector3.one * (_playerIconSize * iconScaleMul);
        }

        // 2. 他機アイコンの位置・サイズ更新
        for (int i = 0; i < _racerIconRenderers.Count; i++)
        {
            MeshRenderer rmr = _racerIconRenderers[i];
            if (rmr == null) continue;

            Transform target = (i < _racerTargets.Count) ? _racerTargets[i] : null;
            if (target != null && target.gameObject.activeInHierarchy)
            {
                rmr.gameObject.SetActive(true);
                Transform rt = rmr.transform;
                rt.localPosition = target.position + upOffset;
                rt.localRotation = Quaternion.Euler(0f, target.eulerAngles.y + _yawOffset, 0f);
                rt.localScale = Vector3.one * (_racerIconSize * iconScaleMul);
            }
            else
            {
                rmr.gameObject.SetActive(false);
            }
        }

        // 3. ZTest 切り替え
        bool useAlwaysZ = (_displayMode == MinimapDisplayMode.ModeB_3DMiniature && _alwaysOnTopInModeB);
        float zTestValue = useAlwaysZ ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual;

        if (_runtimeRoadMat != null) _runtimeRoadMat.SetFloat(ZTestId, zTestValue);
        if (_runtimePlayerIconMat != null) _runtimePlayerIconMat.SetFloat(ZTestId, (float)CompareFunction.Always);
        if (_runtimeRacerIconMat != null) _runtimeRacerIconMat.SetFloat(ZTestId, (float)CompareFunction.Always);

        // 4. シェーダーへ自機のコース座標・表示半径・円柱モードフラグを送信
        float effectiveVisibleRadius = isWholeCourse ? 100000f : _visibleRadius;
        float isCylinderFloat = isCylinder ? 1f : 0f;
        Vector4 playerPosVec = new Vector4(playerPos.x, playerPos.y, playerPos.z, 1f);

        if (_runtimeRoadMat != null && _roadMaterial == null)
        {
            _runtimeRoadMat.SetVector(PlayerLocalPosId, playerPosVec);
            _runtimeRoadMat.SetFloat(VisibleRadiusId, effectiveVisibleRadius);
            _runtimeRoadMat.SetFloat(HeightColorRangeId, _heightColorRange);
            _runtimeRoadMat.SetFloat(IsCylinderId, isCylinderFloat);
            _runtimeRoadMat.SetFloat(CylinderWireCountId, _cylinderWireCount);
        }
        if (_roadRenderer != null)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetVector(PlayerLocalPosId, playerPosVec);
            _mpb.SetFloat(VisibleRadiusId, effectiveVisibleRadius);
            _mpb.SetFloat(HeightColorRangeId, _heightColorRange);
            _mpb.SetFloat(IsCylinderId, isCylinderFloat);
            _mpb.SetFloat(CylinderWireCountId, _cylinderWireCount);
            _roadRenderer.SetPropertyBlock(_mpb);
        }
    }

    private float GetTargetMapYaw(bool isWholeCourse)
    {
        if (isWholeCourse)
        {
            if (!_rotateInWholeCourse) return _wholeCourseFixedYaw;
        }
        else
        {
            if (!_rotateWithPlayer) return 0f;
        }

        if (_playerTarget == null) return 0f;

        Vector3 flatForward = Vector3.ProjectOnPlane(_playerTarget.forward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f)
        {
            return Quaternion.LookRotation(flatForward, Vector3.up).eulerAngles.y + _yawOffset;
        }
        return _playerTarget.eulerAngles.y + _yawOffset;
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        if (obj.layer != layer) obj.layer = layer;
        Transform t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }

    private void ClearGeneratedChildren()
    {
        // 子オブジェクト（SubCamera含む）を破棄する前に、RenderTexture参照を安全に外す
        if (_subCamera != null) _subCamera.targetTexture = null;
        if (_uiRawImage != null) _uiRawImage.texture = null;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        _hologramPivot = null;
        _meshRoot = null;
        _roadRenderer = null;
        _playerIconRenderer = null;
        _racerIconRenderers.Clear();
        _subCamera = null;
        _uiCanvas = null;
        _uiRawImage = null;
    }
}
