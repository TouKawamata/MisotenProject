using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.StadiumCrowdBillboardV2
{
    /// <summary>
    /// 【V2・ビルボード板ポリ版（完全独立）】マルチ階層（1F/2F〜）＋山なり床面3D斜面スナップ＋区画スタンプ＋Scene消しゴム/追加ブラシ対応の超軽量スタジアム観客ジェネレーター。
    /// ・1人あたりわずか 4頂点（2ポリゴン、クロスモード時は8頂点/4ポリゴン）で、最大 8,000人をわずか 1ドローコールで描画します。
    /// ・既存の StadiumCrowd / StadiumCrowdBillboard / StadiumCrowdV2 とは完全に独立しており、どれを削除してもコンパイルエラーになりません。
    /// ・ブラシ＆消しゴム編集はエディタ生成時のみ実行されるため、ゲーム実行中（Play Mode・ビルド後）の追加負荷は完全ゼロ（0.00ms / 0 GC Alloc）です。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StadiumCrowdBillboardV2Generator : MonoBehaviour
    {
        public enum BillboardOrientation
        {
            [InspectorName("① ハイブリッド俯瞰チルト (Camera + Sky Tilt / 上空見下ろし対応・推奨)")]
            HybridSkyTilt = 0,

            [InspectorName("② Y軸固定ビルボード (Classic Y-Axis / 水平視点向け)")]
            CylindricalYAxis = 1,

            [InspectorName("③ ターゲット指向クロス2枚板 (Cross-Quad 8頂点 / 立体交差)")]
            TargetCrossQuad = 2
        }

        public enum BillboardStyle
        {
            [InspectorName("① 標準レース観客・純粋人間 (Pure Spectator / 持ち物なし)")]
            PureHuman = 0,

            [InspectorName("② レース応援・キャップ＆小旗/タオル (Racing Fan with Flag)")]
            RacingFanWithFlag = 1,

            [InspectorName("③ 近未来レース・サイバーバイザー観客 (Cyber Visor Spectator)")]
            CyberVisor = 2,

            [InspectorName("④ カスタム2D画像テクスチャ使用 (Custom Sprite Texture)")]
            CustomSpriteTexture = 3
        }

        public enum SurfaceSnapMode
        {
            [InspectorName("① 数式・山なりカーブのみ (Math / Wave Only)")]
            MathAndWaveOnly = 0,

            [InspectorName("② 3Dモデル表面へ自動スナップ・床が無い場所は配置しない (Mesh Snap Required / 推奨)")]
            MeshSnapRequired = 1,

            [InspectorName("③ 3Dモデル優先スナップ＋床が無い箇所は数式高さへフォールバック (Hybrid)")]
            MeshSnapWithMathFallback = 2
        }

        public enum CrowdColorTheme
        {
            [InspectorName("① レース観戦カジュアル (多彩な私服＆レーシングキャップ)")]
            RacingCasual = 0,

            [InspectorName("② チームカラー対抗 (ブルー＆ホワイト vs レッド＆イエロー)")]
            TeamRivalry = 1,

            [InspectorName("③ 近未来サイバー・レース (ネオンシアン＆マゼンタ発光)")]
            CyberNeon = 2,

            [InspectorName("④ サンセット・フェスティバル (暖色オレンジ＆ゴールド)")]
            SunsetFestival = 3,

            [InspectorName("⑤ カスタムカラー手動設定")]
            Custom = 4
        }

        public enum TargetTrackingMode
        {
            [InspectorName("① 上空降下→着地 確認シミュレーター (SkyDive Loop)")]
            SkyDiveSimulation = 0,

            [InspectorName("② 手動スライダー降下テスト (Manual Altitude Scrub)")]
            ManualAltitudeScrub = 1,

            [InspectorName("③ 実機ターゲット追従 (Follow Target / Player)")]
            FollowTransform = 2
        }

        [Serializable]
        public class CrowdTierConfig
        {
            [Tooltip("階層の識別名（例：1階席 (1F), 2階席 (2F)）")]
            public string tierName = "1階席 (1F)";

            [Tooltip("この階層の観客生成を有効にするか")]
            public bool enabled = true;

            [Tooltip("Sceneビューのガイドリング表示色")]
            public Color gizmoColor = new Color(0.25f, 0.85f, 1.0f, 0.90f);

            [Tooltip("ONにするとSceneビューにこの階層/区画の内径・外径(奥行き)・左右角度の直接ドラッグハンドルを表示します")]
            public bool editInScene = false;

            [Header("基本円弧・段形状")]
            [Min(2f)] public float innerRadiusX = 30f;
            [Min(2f)] public float innerRadiusZ = 30f;
            [Min(0f)] public float straightTrackLength = 0f;
            [Range(0f, 360f)] public float startAngleDeg = 0f;
            [Range(5f, 360f)] public float arcAngleDeg = 360f;
            public float baseHeightY = 0f;

            [Range(1, 60)] public int rowCount = 18;
            [Range(0.45f, 4.0f)] public float rowDepth = 1.05f;
            [Range(0f, 3.0f)] public float rowRise = 0.55f;
            [Range(0.45f, 3.5f)] public float seatSpacing = 0.85f;
            public bool staggerRows = true;
            [Range(0f, 0.35f)] public float positionJitter = 0.08f;
            [Range(0f, 0.85f)] public float emptySeatRatio = 0.05f;
            public bool generateFallbackStandMesh = false;

            [Header("山なり・上下うねりオフセット")]
            public bool enableElevationWave = false;
            [Range(1, 12)] public int wavePeakCount = 3;
            [Range(-30f, 30f)] public float waveAmplitudeY = 4.0f;
            [Range(0f, 360f)] public float wavePhaseDeg = 0f;
            [Range(0.25f, 4.0f)] public float waveExponent = 1.0f;
            [Range(0f, 2.5f)] public float waveOuterRowScale = 1.0f;

            public bool useCustomElevationCurve = false;
            public AnimationCurve customElevationCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            public float customCurveScaleY = 5.0f;

            [Header("3D床スナップ高さスキャン設定")]
            public BillboardSurfaceSamplerV2.FloorSelectMode snapPolicy = BillboardSurfaceSamplerV2.FloorSelectMode.HighestInRange;
            public bool useAbsoluteYBounds = true;
            public float snapMinY = 0.3f;
            public float snapMaxY = 14f;
            [Min(0.5f)] public float snapSearchUp = 10f;
            [Min(0.5f)] public float snapSearchDown = 10f;
            public float surfaceYOffset = 0.02f;

            [NonSerialized] public int lastGeneratedCount;
            [NonSerialized] public int lastSnappedCount;
            [NonSerialized] public int lastWallRejectedCount;
            [NonSerialized] public int lastHeadroomRejectedCount;
        }

        [Serializable]
        public class AngularCutoutZone
        {
            public string label = "巨大モニター開口";
            public bool enabled = true;
            [Range(0f, 360f)] public float centerAngleDeg = 0f;
            [Range(1f, 355f)] public float arcWidthDeg = 35f;
            public int targetTierIndex = -1;
            public bool limitRadiusRange = false;
            [Min(0f)] public float minRadius = 0f;
            [Min(1f)] public float maxRadius = 200f;
            public Color gizmoColor = new Color(1.0f, 0.25f, 0.30f, 0.85f);

            public bool Contains(float angleDeg, float radius, int tierIndex)
            {
                if (!enabled) return false;
                if (targetTierIndex >= 0 && targetTierIndex != tierIndex) return false;
                if (limitRadiusRange && (radius < minRadius || radius > maxRadius)) return false;

                float delta = Mathf.Abs(Mathf.DeltaAngle(centerAngleDeg, angleDeg));
                return delta <= arcWidthDeg * 0.5f;
            }
        }

        [Serializable]
        public class RadialPartitionWall
        {
            public string label = "仕切り壁";
            public bool enabled = true;
            [Range(0f, 360f)] public float angleDeg = 0f;
            [Min(0.1f)] public float thicknessMeters = 2.6f;
            [Min(0f)] public float marginMeters = 0.45f;
            public int targetTierIndex = -1;
            public bool limitRadiusRange = false;
            [Min(0f)] public float minRadius = 0f;
            [Min(1f)] public float maxRadius = 200f;
            public Color gizmoColor = new Color(1.0f, 0.90f, 0.20f, 0.85f);
        }

        public struct SpectatorPlacementPoint
        {
            public Vector3 localPosition;
            public float baseYawRad;
            public float seedA;
            public float seedB;
            public float stadiumAngle01;
            public float rowNormalized01;
        }

        [Serializable]
        public struct BrushEraseStamp
        {
            public Vector3 localPosition;
            public float radius;
        }

        [Serializable]
        public struct ManualPaintedSeat
        {
            public Vector3 localPosition;
            public float baseYawRad;
            public float seedA;
            public float seedB;
            public float stadiumAngle01;
            public float rowNormalized01;
        }

        // =====================================================================
        // 1. ビルボード方式・見た目設定
        // =====================================================================
        [Header("1. ビルボード方式 ＆ 見た目設定")]
        [SerializeField] private BillboardOrientation _billboardOrientation = BillboardOrientation.HybridSkyTilt;
        [Range(0f, 0.85f)]
        [SerializeField] private float _verticalTiltWeight = 0.55f;
        [SerializeField] private BillboardStyle _billboardStyle = BillboardStyle.PureHuman;
        [SerializeField] private Texture2D _customSpriteTexture;
        [SerializeField] private CrowdColorTheme _colorTheme = CrowdColorTheme.RacingCasual;

        [SerializeField] private Color _outfitColorA = new Color(0.16f, 0.42f, 0.86f, 1f);
        [SerializeField] private Color _outfitColorB = new Color(0.88f, 0.22f, 0.24f, 1f);
        [SerializeField] private Color _outfitColorC = new Color(0.90f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color _outfitColorD = new Color(0.96f, 0.74f, 0.14f, 1f);
        [SerializeField] private Color _pantsColor = new Color(0.14f, 0.16f, 0.20f, 1f);
        [SerializeField] private Color _skinColor = new Color(0.88f, 0.72f, 0.62f, 1f);
        [SerializeField] private Color _hairHatColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] private Color _accentEmissionColor = new Color(0.25f, 0.75f, 1.0f, 1f);
        [Range(0f, 3f)]
        [SerializeField] private float _accentEmissionStrength = 0.20f;

        [Range(0.5f, 2.5f)]
        [SerializeField] private float _spectatorScale = 1.0f;
        [Range(0f, 0.25f)]
        [SerializeField] private float _heightVariation = 0.10f;

        // =====================================================================
        // 2. 3Dモデル床面スナップ・斜面限定フィルタ・壁回避
        // =====================================================================
        [Header("2. 3Dモデル床面スナップ ＆ 斜面限定フィルタ")]
        [SerializeField] private SurfaceSnapMode _surfaceSnapMode = SurfaceSnapMode.MeshSnapRequired;
        [SerializeField] private Transform _stadiumSurfaceRoot;
        [SerializeField] private List<MeshFilter> _stadiumMeshFilters = new List<MeshFilter>();
        [Range(0.15f, 0.98f)]
        [SerializeField] private float _minFloorNormalY = 0.45f;
        [SerializeField] private bool _allowDoubleSidedFloor = false;

        [SerializeField] private bool _excludeFlatHorizontalSurfaces = true;
        [Range(0f, 35f)]
        [SerializeField] private float _minFloorSlopeDeg = 3.5f;
        [SerializeField] private bool _requireInwardFacingSlope = true;

        [SerializeField] private bool _autoAvoidMeshWalls = true;
        [Range(0f, 2.5f)]
        [SerializeField] private float _meshWallClearanceRadius = 0.55f;
        [Range(0.05f, 0.7f)]
        [SerializeField] private float _wallMaxAbsNormalY = 0.35f;
        [Range(0.4f, 2.5f)]
        [SerializeField] private float _wallDetectMinHeightAboveFloor = 0.95f;

        [SerializeField] private bool _checkOverheadHeadroom = true;
        [Range(0.8f, 3.5f)]
        [SerializeField] private float _minHeadroom = 1.65f;

        // =====================================================================
        // 3. マルチ階層・区画スタンプ管理
        // =====================================================================
        [Header("3. マルチ階層・区画スタンプ管理")]
        [SerializeField] private List<CrowdTierConfig> _tiers = new List<CrowdTierConfig>();
        [SerializeField] private int _randomSeed = 12345;

        // =====================================================================
        // 4. 開口アーク ＆ 放射仕切り壁カッター ＆ ブラシデータ
        // =====================================================================
        [Header("4. 開口アーク ＆ 放射仕切り壁カッター")]
        [SerializeField] private List<AngularCutoutZone> _angularCutouts = new List<AngularCutoutZone>();

        [SerializeField] private bool _enablePeriodicWalls = false;
        [Range(1, 24)]
        [SerializeField] private int _periodicWallCount = 3;
        [Range(0f, 360f)]
        [SerializeField] private float _periodicWallOffsetDeg = 0f;
        [Min(0.1f)]
        [SerializeField] private float _periodicWallThicknessMeters = 2.6f;
        [Min(0f)]
        [SerializeField] private float _periodicWallMarginMeters = 0.45f;
        [SerializeField] private int _periodicWallTierIndex = -1;

        [SerializeField] private List<RadialPartitionWall> _radialWalls = new List<RadialPartitionWall>();

        // Sceneブラシ編集データ（※エディタ生成時のみ参照。ゲーム実行時の追加負荷・Update処理は完全ゼロ）
        [SerializeField] private List<BrushEraseStamp> _brushEraseStamps = new List<BrushEraseStamp>();
        [SerializeField] private List<ManualPaintedSeat> _manualPaintedSeats = new List<ManualPaintedSeat>();

        // =====================================================================
        // 5. 視線追従・降下シミュレーター・歓声アニメ
        // =====================================================================
        [Header("5. 視線追従・降下シミュレーター ＆ 歓声設定")]
        [SerializeField] private TargetTrackingMode _trackingMode = TargetTrackingMode.SkyDiveSimulation;
        [SerializeField] private Transform _lookAtTarget;
        [SerializeField] private bool _autoFindPlayerAtRuntime = true;
        [Range(10f, 300f)]
        [SerializeField] private float _simStartHeight = 95f;
        [SerializeField] private Vector2 _simApproachOffsetXZ = new Vector2(0f, -20f);
        [SerializeField] private Vector3 _simLandingPointLocal = new Vector3(0f, 1.5f, 0f);
        [Range(2f, 20f)]
        [SerializeField] private float _simCycleDuration = 7.5f;
        [Range(0f, 1f)]
        [SerializeField] private float _manualLandingProgress = 0.35f;
        [SerializeField] private bool _previewInEditMode = true;
        [SerializeField] private bool _showTargetMarkerMesh = true;

        [Range(0f, 1f)] [SerializeField] private float _lookAtWeight = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _reactionLagWeight = 0.68f;
        [Range(0.05f, 1.5f)] [SerializeField] private float _reactionLagSeconds = 0.40f;
        [Range(0f, 1f)] [SerializeField] private float _baseExcitement = 0.45f;
        [SerializeField] private bool _boostExcitementOnLanding = true;
        [Range(0f, 3f)] [SerializeField] private float _animSpeed = 1.0f;
        [Range(0f, 0.55f)] [SerializeField] private float _jumpHeight = 0.22f;
        [Range(0f, 1f)] [SerializeField] private float _jumpRatio = 0.55f;
        [Range(0f, 0.3f)] [SerializeField] private float _swayAmount = 0.10f;
        [Range(0f, 1.5f)] [SerializeField] private float _armWaveAmount = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float _waveIntensity = 0.18f;
        [Range(0.05f, 1.0f)] [SerializeField] private float _waveSpeed = 0.22f;

        // =====================================================================
        // 6. 遠景負荷最適化・本採用設定
        // =====================================================================
        [Header("6. 遠景負荷最適化・本採用設定")]
        [Min(0f)] [SerializeField] private float _animCullDistance = 250f;
        [Min(0f)] [SerializeField] private float _maxRenderDistance = 600f;
        [SerializeField] private bool _castShadows = true;
        [SerializeField] private bool _pauseTrackingWhenFar = true;
        [SerializeField] private bool _applyLayerCullDistance = false;
        [Min(10f)] [SerializeField] private float _layerCullDistance = 500f;

        [Header("7. 内部参照 & 統計情報")]
        [SerializeField] private Shader _billboardShader;
        [SerializeField] private Material _sharedBillboardMaterial;
        [SerializeField] private Material _sharedStandMaterial;
        [SerializeField] private int _generatedSpectatorCount;
        [SerializeField] private int _generatedChunkCount;
        [SerializeField] private int _generatedTriangleCount;
        [SerializeField] private bool _isBakedAssets;
        [SerializeField] private Vector3 _crowdBoundsCenterLocal;
        [SerializeField] private float _crowdBoundingRadius = 150f;

        // Runtime state (0 GC Alloc)
        private const string CrowdRootName = "Generated_BillboardCrowdV2_Root";
        private const string MarkerObjectName = "Preview_SkyDiveTargetMarker_BBV2";
        private const int MaxSpectatorsPerChunk = 8000;
        private const float SpatialChunkCellSize = 24f;

        private readonly List<MeshRenderer> _chunkRenderers = new List<MeshRenderer>();
        private readonly List<SpectatorPlacementPoint> _lastPlacementPoints = new List<SpectatorPlacementPoint>();
        private MaterialPropertyBlock _mpb;

        private Vector3 _currentTargetWorldPos;
        private Vector3 _delayedTargetWorldPos;
        private float _currentDynamicExcitement;
        private bool _delayedPosInitialized;
        private Transform _markerTransform;
        private double _lastEditorTime;

        private Camera _cachedMainCamera;
        private float _nextCameraLookupTime;
        private Camera _appliedCullCamera;
        private int _appliedCullLayer = -1;
        private float _appliedCullDistanceValue = -1f;
        private float _savedPrevLayerCullDistance;
        private bool _hasSavedLayerCullState;
        private bool _trackingPausedByDistance;

        // Surface sampler cache (Editor / Generation only)
        private BillboardSurfaceSamplerV2 _cachedSampler;
        private Transform _cachedSurfaceRoot;
        private int _cachedFilterHash;
        private Matrix4x4 _cachedGenMatrix;
        private float _cachedMinFloorNormalY;
        private float _cachedWallNormalY;
        private bool _cachedDoubleSided;
        private bool _cachedExcludeFlat;
        private float _cachedMinSlopeDeg;
        private bool _cachedRequireInward;
        private bool _keepSamplerAliveForBrush;

        private int _lastSampledMeshCount;
        private int _lastNonReadableFallbackCount;
        private int _lastSkippedUnreadableCount;
        private int _lastTotalSnappedCount;
        private int _lastTotalWallRejectedCount;
        private int _lastTotalHeadroomRejectedCount;

        // Shader Property IDs
        private static readonly int UseCustomTextureId = Shader.PropertyToID("_UseCustomTexture");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BillboardOrientationModeId = Shader.PropertyToID("_BillboardOrientationMode");
        private static readonly int VerticalTiltWeightId = Shader.PropertyToID("_VerticalTiltWeight");

        private static readonly int TargetPosOSId = Shader.PropertyToID("_TargetPosOS");
        private static readonly int DelayedTargetPosOSId = Shader.PropertyToID("_DelayedTargetPosOS");
        private static readonly int LookAtWeightId = Shader.PropertyToID("_LookAtWeight");
        private static readonly int ReactionLagWeightId = Shader.PropertyToID("_ReactionLagWeight");
        private static readonly int ExcitementId = Shader.PropertyToID("_Excitement");
        private static readonly int AnimSpeedId = Shader.PropertyToID("_AnimSpeed");
        private static readonly int JumpHeightId = Shader.PropertyToID("_JumpHeight");
        private static readonly int JumpRatioId = Shader.PropertyToID("_JumpRatio");
        private static readonly int SwayAmountId = Shader.PropertyToID("_SwayAmount");
        private static readonly int ArmWaveAmountId = Shader.PropertyToID("_ArmWaveAmount");
        private static readonly int HeightVariationId = Shader.PropertyToID("_HeightVariation");
        private static readonly int WaveIntensityId = Shader.PropertyToID("_WaveIntensity");
        private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
        private static readonly int AnimCullDistanceId = Shader.PropertyToID("_AnimCullDistance");
        private static readonly int MaxRenderDistanceId = Shader.PropertyToID("_MaxRenderDistance");

        private static readonly int OutfitColorAId = Shader.PropertyToID("_OutfitColorA");
        private static readonly int OutfitColorBId = Shader.PropertyToID("_OutfitColorB");
        private static readonly int OutfitColorCId = Shader.PropertyToID("_OutfitColorC");
        private static readonly int OutfitColorDId = Shader.PropertyToID("_OutfitColorD");
        private static readonly int PantsColorId = Shader.PropertyToID("_PantsColor");
        private static readonly int SkinColorId = Shader.PropertyToID("_SkinColor");
        private static readonly int HairHatColorId = Shader.PropertyToID("_HairHatColor");
        private static readonly int AccentEmissionColorId = Shader.PropertyToID("_AccentEmissionColor");
        private static readonly int AccentEmissionStrengthId = Shader.PropertyToID("_AccentEmissionStrength");

        // Public Accessors
        public int GeneratedSpectatorCount => _generatedSpectatorCount;
        public int GeneratedChunkCount => _generatedChunkCount;
        public int GeneratedTriangleCount => _generatedTriangleCount;
        public bool IsBakedAssets => _isBakedAssets;
        public bool IsLayerSafeForCullDistance => IsSafeLayerForCull(gameObject.layer);
        public bool IsTrackingPausedByDistance => _trackingPausedByDistance;

        public SurfaceSnapMode CurrentSurfaceSnapMode => _surfaceSnapMode;
        public bool HasAssignedSurfaceTarget => _stadiumSurfaceRoot != null || (_stadiumMeshFilters != null && _stadiumMeshFilters.Count > 0);
        public int LastSampledMeshCount => _lastSampledMeshCount;
        public int LastNonReadableFallbackCount => _lastNonReadableFallbackCount;
        public int LastSkippedUnreadableCount => _lastSkippedUnreadableCount;
        public int LastTotalSnappedCount => _lastTotalSnappedCount;
        public int LastTotalWallRejectedCount => _lastTotalWallRejectedCount;
        public int LastTotalHeadroomRejectedCount => _lastTotalHeadroomRejectedCount;

        public List<CrowdTierConfig> Tiers => _tiers;
        public List<AngularCutoutZone> AngularCutouts => _angularCutouts;
        public List<RadialPartitionWall> RadialWalls => _radialWalls;
        public bool EnablePeriodicWalls => _enablePeriodicWalls;
        public int PeriodicWallCount => _periodicWallCount;
        public float PeriodicWallOffsetDeg => _periodicWallOffsetDeg;
        public float PeriodicWallThicknessMeters => _periodicWallThicknessMeters;
        public float PeriodicWallMarginMeters => _periodicWallMarginMeters;
        public int PeriodicWallTierIndex => _periodicWallTierIndex;

        public int BrushEraseStampCount => _brushEraseStamps != null ? _brushEraseStamps.Count : 0;
        public int ManualPaintedSeatCount => _manualPaintedSeats != null ? _manualPaintedSeats.Count : 0;
        public IReadOnlyList<BrushEraseStamp> BrushEraseStamps => _brushEraseStamps;
        public IReadOnlyList<ManualPaintedSeat> ManualPaintedSeats => _manualPaintedSeats;

        private void Reset()
        {
            EnsureDefaultTiers();
        }

        private void OnEnable()
        {
            EnsureDefaultTiers();
            RefreshChunkRenderersList();
            InitializeTargetPositions();

            if (_generatedSpectatorCount == 0 && _chunkRenderers.Count == 0)
            {
                GenerateCrowd(false);
            }
            else if (!_isBakedAssets && HasMissingChunkMeshes())
            {
                GenerateCrowd(false);
            }
            else
            {
                if (_isBakedAssets && HasMissingChunkMeshes())
                {
                    Debug.LogWarning("[StadiumCrowdBillboardV2] ベイク済みメッシュ参照の一部が欠損しています。インスペクターから再生成または再ベイクしてください。", this);
                }
                ApplyMaterialSettingsToAllChunks();
            }

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            _lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        }

        private void OnDisable()
        {
            _keepSamplerAliveForBrush = false;
            RestoreLayerCullDistance();
            DisposeSamplerCache();
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            _keepSamplerAliveForBrush = false;
            RestoreLayerCullDistance();
            DisposeSamplerCache();
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
        }

#if UNITY_EDITOR
        private void OnBeforeAssemblyReload()
        {
            _keepSamplerAliveForBrush = false;
            RestoreLayerCullDistance();
            DisposeSamplerCache();
        }
#endif

        private void Start()
        {
            if (Application.isPlaying && _autoFindPlayerAtRuntime && _lookAtTarget == null)
            {
                TryAutoFindPlayerTarget();
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                TickTrackingAndShader(Time.deltaTime, Time.time, false);
            }
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (Application.isPlaying || !_previewInEditMode || !isActiveAndEnabled)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0.001f, 0.1f);
            _lastEditorTime = now;

            TickTrackingAndShader(dt, (float)now, false);
            EditorApplication.QueuePlayerLoopUpdate();
        }
#endif

        public void EnsureDefaultTiers()
        {
            if (_tiers == null) _tiers = new List<CrowdTierConfig>();

            if (_tiers.Count == 0)
            {
                _tiers.Add(new CrowdTierConfig
                {
                    tierName = "1階席 (1F)",
                    enabled = true,
                    gizmoColor = new Color(0.25f, 0.85f, 1.0f, 0.90f),
                    editInScene = true,
                    innerRadiusX = 30f,
                    innerRadiusZ = 30f,
                    baseHeightY = 0f,
                    rowCount = 18,
                    rowDepth = 1.05f,
                    rowRise = 0.55f,
                    seatSpacing = 0.85f,
                    snapPolicy = BillboardSurfaceSamplerV2.FloorSelectMode.HighestInRange,
                    useAbsoluteYBounds = true,
                    snapMinY = 0.3f,
                    snapMaxY = 14f,
                    snapSearchUp = 10f,
                    snapSearchDown = 10f
                });

                _tiers.Add(new CrowdTierConfig
                {
                    tierName = "2階席 (2F)",
                    enabled = true,
                    gizmoColor = new Color(1.0f, 0.72f, 0.25f, 0.90f),
                    editInScene = false,
                    innerRadiusX = 38f,
                    innerRadiusZ = 38f,
                    baseHeightY = 15f,
                    rowCount = 16,
                    rowDepth = 1.10f,
                    rowRise = 0.65f,
                    seatSpacing = 0.88f,
                    snapPolicy = BillboardSurfaceSamplerV2.FloorSelectMode.HighestInRange,
                    useAbsoluteYBounds = true,
                    snapMinY = 12f,
                    snapMaxY = 48f,
                    snapSearchUp = 12f,
                    snapSearchDown = 12f
                });
            }
        }

        // =====================================================================
        // 視線追従・MaterialPropertyBlock 更新（0 GC Alloc）
        // =====================================================================
        private void TickTrackingAndShader(float deltaTime, float currentTime, bool forceUpdate)
        {
            if (_chunkRenderers.Count == 0)
            {
                RefreshChunkRenderersList();
                if (_chunkRenderers.Count == 0) return;
            }

            if (Application.isPlaying && (_applyLayerCullDistance || _hasSavedLayerCullState))
            {
                UpdateLayerCullDistance();
            }

            if (_pauseTrackingWhenFar && !forceUpdate && Application.isPlaying)
            {
                float effectiveCullDist = _maxRenderDistance > 0f
                    ? _maxRenderDistance
                    : (_applyLayerCullDistance && IsSafeLayerForCull(gameObject.layer) ? _layerCullDistance : 0f);

                if (effectiveCullDist > 0f)
                {
                    Camera cam = GetMainCameraCached();
                    if (cam != null)
                    {
                        Vector3 lossy = transform.lossyScale;
                        float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
                        float pauseThreshold = _crowdBoundingRadius * Mathf.Max(maxScale, 0.01f) + effectiveCullDist + 25f;
                        Vector3 worldCenter = transform.TransformPoint(_crowdBoundsCenterLocal);
                        if ((cam.transform.position - worldCenter).sqrMagnitude > pauseThreshold * pauseThreshold)
                        {
                            _trackingPausedByDistance = true;
                            return;
                        }
                    }
                }
            }
            _trackingPausedByDistance = false;

            float landingExcitementBoost = 0f;

            switch (_trackingMode)
            {
                case TargetTrackingMode.SkyDiveSimulation:
                {
                    float cycle = Mathf.Max(_simCycleDuration, 1.0f);
                    float phase = Mathf.Repeat(currentTime, cycle) / cycle;

                    float descentT;
                    if (phase < 0.65f)
                    {
                        float norm = phase / 0.65f;
                        descentT = 1f - Mathf.Pow(1f - norm, 2.2f);
                        landingExcitementBoost = Mathf.SmoothStep(0f, 0.65f, norm);
                    }
                    else if (phase < 0.90f)
                    {
                        descentT = 1.0f;
                        landingExcitementBoost = 0.65f;
                    }
                    else
                    {
                        float resetNorm = (phase - 0.90f) / 0.10f;
                        descentT = 1.0f - resetNorm;
                        landingExcitementBoost = (1.0f - resetNorm) * 0.65f;
                    }

                    Vector3 skyStartLocal = _simLandingPointLocal + new Vector3(_simApproachOffsetXZ.x, _simStartHeight, _simApproachOffsetXZ.y);
                    Vector3 currentLocal = Vector3.Lerp(skyStartLocal, _simLandingPointLocal, descentT);
                    _currentTargetWorldPos = transform.TransformPoint(currentLocal);
                    break;
                }

                case TargetTrackingMode.ManualAltitudeScrub:
                {
                    Vector3 skyStartLocal = _simLandingPointLocal + new Vector3(_simApproachOffsetXZ.x, _simStartHeight, _simApproachOffsetXZ.y);
                    Vector3 currentLocal = Vector3.Lerp(skyStartLocal, _simLandingPointLocal, _manualLandingProgress);
                    _currentTargetWorldPos = transform.TransformPoint(currentLocal);
                    landingExcitementBoost = Mathf.SmoothStep(0.3f, 1.0f, _manualLandingProgress) * 0.65f;
                    break;
                }

                case TargetTrackingMode.FollowTransform:
                {
                    if (_lookAtTarget != null)
                    {
                        _currentTargetWorldPos = _lookAtTarget.position;
                        Vector3 localTarget = transform.InverseTransformPoint(_currentTargetWorldPos);
                        float distToLanding = Vector3.Distance(localTarget, _simLandingPointLocal);
                        landingExcitementBoost = Mathf.SmoothStep(45f, 5f, distToLanding) * 0.65f;
                    }
                    else
                    {
                        _currentTargetWorldPos = transform.TransformPoint(_simLandingPointLocal + Vector3.up * 20f);
                    }
                    break;
                }
            }

            if (!_delayedPosInitialized)
            {
                _delayedTargetWorldPos = _currentTargetWorldPos;
                _delayedPosInitialized = true;
            }
            else
            {
                float smoothSpeed = 1f / Mathf.Max(_reactionLagSeconds, 0.05f);
                _delayedTargetWorldPos = Vector3.Lerp(_delayedTargetWorldPos, _currentTargetWorldPos, Mathf.Clamp01(deltaTime * smoothSpeed));
            }

            UpdateTargetMarkerVisual();

            float targetExcitement = _baseExcitement + (_boostExcitementOnLanding ? landingExcitementBoost : 0f);
            _currentDynamicExcitement = Mathf.Clamp01(targetExcitement);

            Vector3 targetOS = transform.InverseTransformPoint(_currentTargetWorldPos);
            Vector3 delayedOS = transform.InverseTransformPoint(_delayedTargetWorldPos);

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _chunkRenderers.Count; i++)
            {
                MeshRenderer mr = _chunkRenderers[i];
                if (mr == null) continue;

                mr.GetPropertyBlock(_mpb);
                _mpb.SetVector(TargetPosOSId, new Vector4(targetOS.x, targetOS.y, targetOS.z, 1f));
                _mpb.SetVector(DelayedTargetPosOSId, new Vector4(delayedOS.x, delayedOS.y, delayedOS.z, 1f));
                _mpb.SetFloat(ExcitementId, _currentDynamicExcitement);
                _mpb.SetFloat(LookAtWeightId, _lookAtWeight);
                _mpb.SetFloat(ReactionLagWeightId, _reactionLagWeight);
                mr.SetPropertyBlock(_mpb);
            }
        }

        private void InitializeTargetPositions()
        {
            Vector3 skyStartLocal = _simLandingPointLocal + new Vector3(_simApproachOffsetXZ.x, _simStartHeight * 0.5f, _simApproachOffsetXZ.y);
            _currentTargetWorldPos = transform.TransformPoint(skyStartLocal);
            _delayedTargetWorldPos = _currentTargetWorldPos;
            _delayedPosInitialized = true;
        }

        private void TryAutoFindPlayerTarget()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _lookAtTarget = playerObj.transform;
                return;
            }

            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                MonoBehaviour mb = allBehaviours[i];
                if (mb != null && mb.GetType().Name == "PlayerManager")
                {
                    _lookAtTarget = mb.transform;
                    return;
                }
            }
        }

        // =====================================================================
        // カラーテーマ・ビルボードマテリアル同期
        // =====================================================================
        public void ApplyColorThemePreset(CrowdColorTheme theme)
        {
            _colorTheme = theme;
            switch (theme)
            {
                case CrowdColorTheme.RacingCasual:
                    _outfitColorA = new Color(0.16f, 0.42f, 0.86f, 1f);
                    _outfitColorB = new Color(0.88f, 0.22f, 0.24f, 1f);
                    _outfitColorC = new Color(0.90f, 0.92f, 0.95f, 1f);
                    _outfitColorD = new Color(0.96f, 0.74f, 0.14f, 1f);
                    _pantsColor = new Color(0.14f, 0.16f, 0.20f, 1f);
                    _accentEmissionColor = new Color(0.25f, 0.80f, 1.0f, 1f);
                    _accentEmissionStrength = 0.20f;
                    break;

                case CrowdColorTheme.TeamRivalry:
                    _outfitColorA = new Color(0.08f, 0.36f, 0.95f, 1f);
                    _outfitColorB = new Color(0.95f, 0.16f, 0.16f, 1f);
                    _outfitColorC = new Color(0.95f, 0.97f, 1.00f, 1f);
                    _outfitColorD = new Color(1.00f, 0.82f, 0.10f, 1f);
                    _pantsColor = new Color(0.12f, 0.14f, 0.18f, 1f);
                    _accentEmissionColor = new Color(1.0f, 0.88f, 0.2f, 1f);
                    _accentEmissionStrength = 0.45f;
                    break;

                case CrowdColorTheme.CyberNeon:
                    _outfitColorA = new Color(0.10f, 0.20f, 0.36f, 1f);
                    _outfitColorB = new Color(0.22f, 0.12f, 0.34f, 1f);
                    _outfitColorC = new Color(0.16f, 0.72f, 0.85f, 1f);
                    _outfitColorD = new Color(0.92f, 0.24f, 0.65f, 1f);
                    _pantsColor = new Color(0.08f, 0.09f, 0.14f, 1f);
                    _accentEmissionColor = new Color(0.0f, 1.4f, 2.0f, 1f);
                    _accentEmissionStrength = 1.6f;
                    break;

                case CrowdColorTheme.SunsetFestival:
                    _outfitColorA = new Color(0.95f, 0.42f, 0.14f, 1f);
                    _outfitColorB = new Color(0.98f, 0.76f, 0.18f, 1f);
                    _outfitColorC = new Color(0.95f, 0.90f, 0.82f, 1f);
                    _outfitColorD = new Color(0.76f, 0.18f, 0.28f, 1f);
                    _pantsColor = new Color(0.18f, 0.14f, 0.14f, 1f);
                    _accentEmissionColor = new Color(1.6f, 0.85f, 0.25f, 1f);
                    _accentEmissionStrength = 0.65f;
                    break;
            }

            ApplyMaterialSettingsToAllChunks();
        }

        public void ApplyMaterialSettingsToAllChunks()
        {
            EnsureMaterials();
            if (_sharedBillboardMaterial != null)
            {
                bool useTex = (_billboardStyle == BillboardStyle.CustomSpriteTexture && _customSpriteTexture != null);
                _sharedBillboardMaterial.SetFloat(UseCustomTextureId, useTex ? 1f : 0f);
                if (useTex)
                {
                    _sharedBillboardMaterial.SetTexture(MainTexId, _customSpriteTexture);
                }
                _sharedBillboardMaterial.SetFloat(BillboardOrientationModeId, (float)_billboardOrientation);
                _sharedBillboardMaterial.SetFloat(VerticalTiltWeightId, _verticalTiltWeight);

                _sharedBillboardMaterial.SetColor(OutfitColorAId, _outfitColorA);
                _sharedBillboardMaterial.SetColor(OutfitColorBId, _outfitColorB);
                _sharedBillboardMaterial.SetColor(OutfitColorCId, _outfitColorC);
                _sharedBillboardMaterial.SetColor(OutfitColorDId, _outfitColorD);
                _sharedBillboardMaterial.SetColor(PantsColorId, _pantsColor);
                _sharedBillboardMaterial.SetColor(SkinColorId, _skinColor);
                _sharedBillboardMaterial.SetColor(HairHatColorId, _hairHatColor);
                _sharedBillboardMaterial.SetColor(AccentEmissionColorId, _accentEmissionColor);
                _sharedBillboardMaterial.SetFloat(AccentEmissionStrengthId, _accentEmissionStrength);

                _sharedBillboardMaterial.SetFloat(LookAtWeightId, _lookAtWeight);
                _sharedBillboardMaterial.SetFloat(ReactionLagWeightId, _reactionLagWeight);
                _sharedBillboardMaterial.SetFloat(AnimSpeedId, _animSpeed);
                _sharedBillboardMaterial.SetFloat(ExcitementId, _baseExcitement);
                _sharedBillboardMaterial.SetFloat(JumpHeightId, _jumpHeight);
                _sharedBillboardMaterial.SetFloat(JumpRatioId, _jumpRatio);
                _sharedBillboardMaterial.SetFloat(SwayAmountId, _swayAmount);
                _sharedBillboardMaterial.SetFloat(ArmWaveAmountId, _armWaveAmount);
                _sharedBillboardMaterial.SetFloat(HeightVariationId, _heightVariation);
                _sharedBillboardMaterial.SetFloat(WaveIntensityId, _waveIntensity);
                _sharedBillboardMaterial.SetFloat(WaveSpeedId, _waveSpeed);
                _sharedBillboardMaterial.SetFloat(AnimCullDistanceId, Mathf.Max(0f, _animCullDistance));
                _sharedBillboardMaterial.SetFloat(MaxRenderDistanceId, Mathf.Max(0f, _maxRenderDistance));
            }

            if (_chunkRenderers.Count == 0)
            {
                RefreshChunkRenderersList();
            }

            ShadowCastingMode castMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            for (int i = 0; i < _chunkRenderers.Count; i++)
            {
                MeshRenderer mr = _chunkRenderers[i];
                if (mr == null) continue;
                if (mr.gameObject.layer != gameObject.layer)
                {
                    mr.gameObject.layer = gameObject.layer;
                }
                if (mr.shadowCastingMode != castMode)
                {
                    mr.shadowCastingMode = castMode;
                }
                if (_sharedBillboardMaterial != null && mr.sharedMaterial != _sharedBillboardMaterial)
                {
                    mr.sharedMaterial = _sharedBillboardMaterial;
                }
            }

            Transform root = transform.Find(CrowdRootName);
            if (root != null)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child != null && child.name.StartsWith("FallbackStandBBV2_") && child.gameObject.layer != gameObject.layer)
                    {
                        child.gameObject.layer = gameObject.layer;
                    }
                }
            }

            UpdateLayerCullDistance();
            TickTrackingAndShader(0.016f, Application.isPlaying ? Time.time : 0f, true);
        }

        // =====================================================================
        // メイン生成処理（マルチ階層＋床スナップ＋極座標開口＆放射壁＋ブラシ合流）
        // =====================================================================
        public void GenerateCrowd(bool forceResampleSurface = false)
        {
            EnsureDefaultTiers();
            ClearGeneratedCrowd();
            EnsureMaterials();

            if (_colorTheme != CrowdColorTheme.Custom)
            {
                ApplyColorThemePreset(_colorTheme);
            }
            else
            {
                ApplyMaterialSettingsToAllChunks();
            }

            bool wantSurfaceSnap = _surfaceSnapMode != SurfaceSnapMode.MathAndWaveOnly && HasAssignedSurfaceTarget;
            BillboardSurfaceSamplerV2 sampler = null;
            bool shouldDisposeSamplerAfterBuild = false;

            if (wantSurfaceSnap)
            {
                sampler = GetOrBuildSampler(forceResampleSurface);
                if (sampler != null)
                {
                    _lastSampledMeshCount = sampler.IndexedMeshCount;
                    _lastNonReadableFallbackCount = sampler.NonReadableFallbackMeshCount;
                    _lastSkippedUnreadableCount = sampler.SkippedUnreadableMeshCount;
                    shouldDisposeSamplerAfterBuild = !_keepSamplerAliveForBrush && sampler.HasTemporaryProxies;
                }
            }
            else
            {
                _lastSampledMeshCount = 0;
                _lastNonReadableFallbackCount = 0;
                _lastSkippedUnreadableCount = 0;
            }

            _lastTotalSnappedCount = 0;
            _lastTotalWallRejectedCount = 0;
            _lastTotalHeadroomRejectedCount = 0;

            BillboardExclusionZoneV2[] exclusionZones = GetComponentsInChildren<BillboardExclusionZoneV2>(false);

            _lastPlacementPoints.Clear();
            System.Random rng = new System.Random(_randomSeed);

            try
            {
                for (int tierIdx = 0; tierIdx < _tiers.Count; tierIdx++)
                {
                    CrowdTierConfig tier = _tiers[tierIdx];
                    if (tier == null) continue;

                    tier.lastGeneratedCount = 0;
                    tier.lastSnappedCount = 0;
                    tier.lastWallRejectedCount = 0;
                    tier.lastHeadroomRejectedCount = 0;

                    if (!tier.enabled) continue;

                    GenerateTierPoints(tier, tierIdx, rng, sampler, exclusionZones, _lastPlacementPoints);

                    _lastTotalSnappedCount += tier.lastSnappedCount;
                    _lastTotalWallRejectedCount += tier.lastWallRejectedCount;
                    _lastTotalHeadroomRejectedCount += tier.lastHeadroomRejectedCount;
                }
            }
            finally
            {
                if (shouldDisposeSamplerAfterBuild)
                {
                    DisposeSamplerCache();
                }
            }

            AppendManualPaintedSeatsToPoints(_lastPlacementPoints);
            RebuildCrowdChunksAndStandsFromCurrentPoints();
        }

        private void AppendManualPaintedSeatsToPoints(List<SpectatorPlacementPoint> targetPoints)
        {
            if (_manualPaintedSeats == null || _manualPaintedSeats.Count == 0) return;

            for (int i = 0; i < _manualPaintedSeats.Count; i++)
            {
                ManualPaintedSeat seat = _manualPaintedSeats[i];
                targetPoints.Add(new SpectatorPlacementPoint
                {
                    localPosition = seat.localPosition,
                    baseYawRad = seat.baseYawRad,
                    seedA = seat.seedA,
                    seedB = seat.seedB,
                    stadiumAngle01 = seat.stadiumAngle01,
                    rowNormalized01 = seat.rowNormalized01
                });
            }
        }

        private void RebuildCrowdChunksAndStandsFromCurrentPoints()
        {
            ClearGeneratedCrowd();
            EnsureMaterials();

            GameObject rootObj = new GameObject(CrowdRootName);
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            rootObj.transform.localScale = Vector3.one;

            BuildBillboardChunks(rootObj.transform, _lastPlacementPoints);

            for (int tierIdx = 0; tierIdx < _tiers.Count; tierIdx++)
            {
                CrowdTierConfig tier = _tiers[tierIdx];
                if (tier != null && tier.enabled && tier.generateFallbackStandMesh)
                {
                    BuildTierFallbackStandMesh(rootObj.transform, tier, tierIdx);
                }
            }

            UpdateTargetMarkerVisual();
            TickTrackingAndShader(0.016f, 0f, true);
            UpdateLayerCullDistance();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private BillboardSurfaceSamplerV2 GetOrBuildSampler(bool forceRebuild)
        {
            int filterHash = ComputeMeshFilterListHash();
            Matrix4x4 currentMat = transform.localToWorldMatrix;

            bool needsRebuild = forceRebuild
                || _cachedSampler == null
                || _cachedSurfaceRoot != _stadiumSurfaceRoot
                || _cachedFilterHash != filterHash
                || _cachedGenMatrix != currentMat
                || !Mathf.Approximately(_cachedMinFloorNormalY, _minFloorNormalY)
                || !Mathf.Approximately(_cachedWallNormalY, _wallMaxAbsNormalY)
                || _cachedDoubleSided != _allowDoubleSidedFloor
                || _cachedExcludeFlat != _excludeFlatHorizontalSurfaces
                || !Mathf.Approximately(_cachedMinSlopeDeg, _minFloorSlopeDeg)
                || _cachedRequireInward != _requireInwardFacingSlope;

            if (needsRebuild)
            {
                if (_cachedSampler == null) _cachedSampler = new BillboardSurfaceSamplerV2();
                _cachedSampler.Build(
                    transform,
                    _stadiumSurfaceRoot,
                    _stadiumMeshFilters,
                    _minFloorNormalY,
                    _wallMaxAbsNormalY,
                    _allowDoubleSidedFloor,
                    _excludeFlatHorizontalSurfaces,
                    _minFloorSlopeDeg,
                    _requireInwardFacingSlope);

                _cachedSurfaceRoot = _stadiumSurfaceRoot;
                _cachedFilterHash = filterHash;
                _cachedGenMatrix = currentMat;
                _cachedMinFloorNormalY = _minFloorNormalY;
                _cachedWallNormalY = _wallMaxAbsNormalY;
                _cachedDoubleSided = _allowDoubleSidedFloor;
                _cachedExcludeFlat = _excludeFlatHorizontalSurfaces;
                _cachedMinSlopeDeg = _minFloorSlopeDeg;
                _cachedRequireInward = _requireInwardFacingSlope;
            }

            return _cachedSampler;
        }

        public void DisposeSamplerCache()
        {
            if (_cachedSampler != null)
            {
                _cachedSampler.Dispose();
                _cachedSampler = null;
            }
        }

        private int ComputeMeshFilterListHash()
        {
            int h = 17;
            if (_stadiumMeshFilters != null)
            {
                h = h * 31 + _stadiumMeshFilters.Count;
                for (int i = 0; i < _stadiumMeshFilters.Count; i++)
                {
                    MeshFilter mf = _stadiumMeshFilters[i];
                    h = h * 31 + (mf != null ? mf.GetInstanceID() : 0);
                }
            }
            return h;
        }

        private void GenerateTierPoints(
            CrowdTierConfig tier,
            int tierIndex,
            System.Random rng,
            BillboardSurfaceSamplerV2 sampler,
            BillboardExclusionZoneV2[] exclusionZones,
            List<SpectatorPlacementPoint> outPoints)
        {
            int rows = Mathf.Max(1, tier.rowCount);
            float totalArcDeg = Mathf.Clamp(tier.arcAngleDeg, 5f, 360f);
            float totalArcRad = totalArcDeg * Mathf.Deg2Rad;
            float startRad = tier.startAngleDeg * Mathf.Deg2Rad;
            float halfStraight = Mathf.Max(0f, tier.straightTrackLength) * 0.5f;
            float seatSpacing = Mathf.Max(0.35f, tier.seatSpacing);

            bool hasActiveSurface = sampler != null && sampler.HasAnySurface && _surfaceSnapMode != SurfaceSnapMode.MathAndWaveOnly;

            for (int r = 0; r < rows; r++)
            {
                float rowNorm = rows > 1 ? (r / (float)(rows - 1)) : 0f;
                float rx = Mathf.Max(1f, tier.innerRadiusX + r * tier.rowDepth);
                float rz = Mathf.Max(1f, tier.innerRadiusZ + r * tier.rowDepth);
                float baseRowY = tier.baseHeightY + r * tier.rowRise;

                float avgRadius = (rx + rz) * 0.5f;
                float perimeter = avgRadius * totalArcRad + tier.straightTrackLength * (totalArcRad / Mathf.PI);
                int seatCount = Mathf.Max(1, Mathf.FloorToInt(perimeter / seatSpacing));

                float staggerOffset = (tier.staggerRows && (r % 2 == 1)) ? (0.5f / seatCount) : 0f;

                for (int s = 0; s < seatCount; s++)
                {
                    if (tier.emptySeatRatio > 0.001f && NextFloat(rng) < tier.emptySeatRatio) continue;

                    float u = (s + 0.5f) / seatCount + staggerOffset;
                    if (u > 1f)
                    {
                        if (totalArcDeg >= 359.5f) u -= 1f;
                        else continue;
                    }

                    float angleRad = startRad + u * totalArcRad;
                    float waveOffsetY = EvaluateTierElevationOffset(tier, angleRad * Mathf.Rad2Deg, rowNorm);
                    float expectedY = baseRowY + waveOffsetY;

                    Vector3 localPos = EvaluateOvalPoint(angleRad, rx, rz, halfStraight, expectedY, out Vector3 inwardDir);

                    if (tier.positionJitter > 0.001f)
                    {
                        Vector3 tangent = new Vector3(-inwardDir.z, 0f, inwardDir.x);
                        float jT = (NextFloat(rng) * 2f - 1f) * tier.positionJitter;
                        float jR = (NextFloat(rng) * 2f - 1f) * (tier.positionJitter * 0.45f);
                        localPos += tangent * jT + inwardDir * jR;
                    }

                    float actualAngleDeg = Mathf.Repeat(Mathf.Atan2(localPos.x, localPos.z) * Mathf.Rad2Deg, 360f);
                    float radialDist = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

                    if (IsInsideAnyAngularCutout(actualAngleDeg, radialDist, tierIndex)) continue;
                    if (IsInsideAnyRadialWall(localPos, tierIndex)) continue;

                    if (hasActiveSurface)
                    {
                        float searchMinY = expectedY - Mathf.Max(0.5f, tier.snapSearchDown);
                        float searchMaxY = expectedY + Mathf.Max(0.5f, tier.snapSearchUp);

                        if (tier.useAbsoluteYBounds)
                        {
                            float absMin = Mathf.Min(tier.snapMinY, tier.snapMaxY);
                            float absMax = Mathf.Max(tier.snapMinY, tier.snapMaxY);

                            if (tier.snapPolicy == BillboardSurfaceSamplerV2.FloorSelectMode.NearestToExpectedY)
                            {
                                searchMinY = Mathf.Max(searchMinY, absMin);
                                searchMaxY = Mathf.Min(searchMaxY, absMax);
                            }
                            else
                            {
                                searchMinY = absMin;
                                searchMaxY = absMax;
                            }
                        }

                        if (searchMinY > searchMaxY)
                        {
                            if (_surfaceSnapMode == SurfaceSnapMode.MeshSnapRequired) continue;
                        }
                        else if (sampler.TrySampleFloorY(
                            localPos.x,
                            localPos.z,
                            expectedY,
                            searchMinY,
                            searchMaxY,
                            tier.snapPolicy,
                            inwardDir,
                            out float hitY,
                            out _))
                        {
                            if (_checkOverheadHeadroom && sampler.HasOverheadObstruction(localPos.x, hitY, localPos.z, _minHeadroom))
                            {
                                tier.lastHeadroomRejectedCount++;
                                continue;
                            }

                            if (_autoAvoidMeshWalls && _meshWallClearanceRadius > 0.01f &&
                                sampler.IsNearTallWall(localPos.x, hitY, localPos.z, _meshWallClearanceRadius, _wallDetectMinHeightAboveFloor))
                            {
                                tier.lastWallRejectedCount++;
                                continue;
                            }

                            localPos.y = hitY + tier.surfaceYOffset;
                            tier.lastSnappedCount++;
                        }
                        else
                        {
                            if (_surfaceSnapMode == SurfaceSnapMode.MeshSnapRequired) continue;
                        }
                    }

                    if (IsInsideAnyExclusionZone(localPos, tierIndex, exclusionZones)) continue;
                    if (IsInsideAnyBrushEraseStamp(localPos)) continue;

                    float yawRad = Mathf.Atan2(inwardDir.x, inwardDir.z) + (NextFloat(rng) * 2f - 1f) * 0.14f;

                    outPoints.Add(new SpectatorPlacementPoint
                    {
                        localPosition = localPos,
                        baseYawRad = yawRad,
                        seedA = NextFloat(rng),
                        seedB = NextFloat(rng),
                        stadiumAngle01 = actualAngleDeg / 360f,
                        rowNormalized01 = rowNorm
                    });
                    tier.lastGeneratedCount++;
                }
            }
        }

        public static float EvaluateTierElevationOffset(CrowdTierConfig tier, float angleDeg, float rowNorm01)
        {
            if (tier == null) return 0f;

            float offset = 0f;
            if (tier.enableElevationWave)
            {
                float rad = (angleDeg - tier.wavePhaseDeg) * Mathf.Max(1, tier.wavePeakCount) * Mathf.Deg2Rad;
                float raw01 = 0.5f + 0.5f * Mathf.Cos(rad);
                float shaped = Mathf.Pow(Mathf.Clamp01(raw01), Mathf.Clamp(tier.waveExponent, 0.25f, 4.0f));
                float rowScale = Mathf.Lerp(1f, tier.waveOuterRowScale, Mathf.Clamp01(rowNorm01));
                offset += shaped * tier.waveAmplitudeY * rowScale;
            }

            if (tier.useCustomElevationCurve && tier.customElevationCurve != null && tier.customElevationCurve.length > 0)
            {
                float u = Mathf.Repeat(angleDeg, 360f) / 360f;
                offset += tier.customElevationCurve.Evaluate(u) * tier.customCurveScaleY;
            }

            return offset;
        }

        public static Vector3 EvaluateOvalPoint(
            float angleRad,
            float rx,
            float rz,
            float halfStraight,
            float y,
            out Vector3 inwardDir)
        {
            float s = Mathf.Sin(angleRad);
            float c = Mathf.Cos(angleRad);

            float x = s * rx;
            float z = c * rz;

            if (halfStraight > 0.001f)
            {
                z += (c >= 0f ? halfStraight : -halfStraight);
                float focusZ = (c >= 0f ? halfStraight : -halfStraight);
                Vector3 toFocus = new Vector3(-x, 0f, focusZ - z);
                inwardDir = toFocus.sqrMagnitude > 0.0001f ? toFocus.normalized : new Vector3(-s, 0f, -c);
            }
            else
            {
                Vector3 toCenter = new Vector3(-x, 0f, -z);
                inwardDir = toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : new Vector3(-s, 0f, -c);
            }

            return new Vector3(x, y, z);
        }

        private bool IsInsideAnyAngularCutout(float angleDeg, float radialDist, int tierIndex)
        {
            if (_angularCutouts == null) return false;
            for (int i = 0; i < _angularCutouts.Count; i++)
            {
                AngularCutoutZone zone = _angularCutouts[i];
                if (zone != null && zone.Contains(angleDeg, radialDist, tierIndex)) return true;
            }
            return false;
        }

        private bool IsInsideAnyRadialWall(Vector3 localPos, int tierIndex)
        {
            if (_enablePeriodicWalls && _periodicWallCount > 0 && (_periodicWallTierIndex < 0 || _periodicWallTierIndex == tierIndex))
            {
                float halfWidth = _periodicWallThicknessMeters * 0.5f + _periodicWallMarginMeters;
                float step = 360f / _periodicWallCount;
                for (int i = 0; i < _periodicWallCount; i++)
                {
                    float wallAngle = _periodicWallOffsetDeg + i * step;
                    if (IsInsideRadialWallRay(localPos, wallAngle, halfWidth, false, 0f, 0f)) return true;
                }
            }

            if (_radialWalls != null)
            {
                for (int i = 0; i < _radialWalls.Count; i++)
                {
                    RadialPartitionWall wall = _radialWalls[i];
                    if (wall == null || !wall.enabled) continue;
                    if (wall.targetTierIndex >= 0 && wall.targetTierIndex != tierIndex) continue;

                    float halfWidth = wall.thicknessMeters * 0.5f + wall.marginMeters;
                    if (IsInsideRadialWallRay(localPos, wall.angleDeg, halfWidth, wall.limitRadiusRange, wall.minRadius, wall.maxRadius)) return true;
                }
            }

            return false;
        }

        public static bool IsInsideRadialWallRay(
            Vector3 localPos,
            float wallAngleDeg,
            float halfWidthMeters,
            bool limitRadius,
            float minRadius,
            float maxRadius)
        {
            float rad = wallAngleDeg * Mathf.Deg2Rad;
            float dirX = Mathf.Sin(rad);
            float dirZ = Mathf.Cos(rad);

            float along = localPos.x * dirX + localPos.z * dirZ;
            if (along < 0f) return false;
            if (limitRadius && (along < minRadius || along > maxRadius)) return false;

            float perp = Mathf.Abs(localPos.x * dirZ - localPos.z * dirX);
            return perp <= halfWidthMeters;
        }

        private bool IsInsideAnyExclusionZone(Vector3 generatorLocalPos, int tierIndex, BillboardExclusionZoneV2[] zones)
        {
            if (zones == null || zones.Length == 0) return false;
            Vector3 worldPos = transform.TransformPoint(generatorLocalPos);
            for (int i = 0; i < zones.Length; i++)
            {
                BillboardExclusionZoneV2 z = zones[i];
                if (z != null && z.AppliesToTier(tierIndex) && z.ContainsWorldPoint(worldPos)) return true;
            }
            return false;
        }

        // =====================================================================
        // ビルボードQuadチャンク結合メッシュ構築（1人=4頂点/2ポリゴン、クロス8頂点）
        // =====================================================================
        private void BuildBillboardChunks(Transform parentRoot, List<SpectatorPlacementPoint> points)
        {
            _generatedSpectatorCount = points.Count;
            _generatedChunkCount = 0;
            _generatedTriangleCount = 0;

            if (points.Count == 0)
            {
                _crowdBoundsCenterLocal = Vector3.zero;
                _crowdBoundingRadius = 50f;
                return;
            }

            points.Sort(SpatialSortComparison);

            Vector3 minPt = points[0].localPosition;
            Vector3 maxPt = points[0].localPosition;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 p = points[i].localPosition;
                minPt = Vector3.Min(minPt, p);
                maxPt = Vector3.Max(maxPt, p);
            }
            _crowdBoundsCenterLocal = (minPt + maxPt) * 0.5f;
            _crowdBoundingRadius = Mathf.Max(10f, Vector3.Distance(minPt, maxPt) * 0.5f);

            ShadowCastingMode shadowMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            bool isCrossQuad = (_billboardOrientation == BillboardOrientation.TargetCrossQuad);
            int quadsPerPerson = isCrossQuad ? 2 : 1;
            int vertsPerPerson = 4 * quadsPerPerson;
            int trisPerPerson = 6 * quadsPerPerson;

            float styleEncoded = 0.0f;
            if (_billboardStyle == BillboardStyle.RacingFanWithFlag) styleEncoded = 0.33f;
            else if (_billboardStyle == BillboardStyle.CyberVisor) styleEncoded = 0.66f;

            int chunkCapacity = Mathf.Max(64, MaxSpectatorsPerChunk / quadsPerPerson);
            int totalChunks = Mathf.CeilToInt(points.Count / (float)chunkCapacity);

            for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
            {
                int startIdx = chunkIdx * chunkCapacity;
                int count = Mathf.Min(chunkCapacity, points.Count - startIdx);

                int totalVerts = count * vertsPerPerson;
                int totalTris = count * trisPerPerson;

                Vector3[] vertices = new Vector3[totalVerts];
                Vector3[] normals = new Vector3[totalVerts];
                Color[] colors = new Color[totalVerts];
                Vector4[] uv0 = new Vector4[totalVerts];
                Vector4[] uv1 = new Vector4[totalVerts];
                int[] triangles = new int[totalTris];

                Vector3 scaleEncodedNormal = new Vector3(0f, _spectatorScale, 0f);

                for (int i = 0; i < count; i++)
                {
                    SpectatorPlacementPoint pt = points[startIdx + i];
                    int vBase = i * vertsPerPerson;
                    int tBase = i * trisPerPerson;

                    Vector4 packedUV0 = new Vector4(pt.localPosition.x, pt.localPosition.y, pt.localPosition.z, pt.baseYawRad);
                    Vector4 packedUV1 = new Vector4(pt.seedA, pt.seedB, pt.stadiumAngle01, pt.rowNormalized01);

                    for (int q = 0; q < quadsPerPerson; q++)
                    {
                        int qv = vBase + q * 4;
                        int qt = tBase + q * 6;
                        float planeId = (q == 0) ? 0.0f : 1.0f;

                        vertices[qv + 0] = pt.localPosition + new Vector3(-0.4f, 0.0f, 0f);
                        vertices[qv + 1] = pt.localPosition + new Vector3( 0.4f, 0.0f, 0f);
                        vertices[qv + 2] = pt.localPosition + new Vector3( 0.4f, 1.8f, 0f);
                        vertices[qv + 3] = pt.localPosition + new Vector3(-0.4f, 1.8f, 0f);

                        normals[qv + 0] = scaleEncodedNormal;
                        normals[qv + 1] = scaleEncodedNormal;
                        normals[qv + 2] = scaleEncodedNormal;
                        normals[qv + 3] = scaleEncodedNormal;

                        colors[qv + 0] = new Color(0f, 0f, planeId, styleEncoded);
                        colors[qv + 1] = new Color(1f, 0f, planeId, styleEncoded);
                        colors[qv + 2] = new Color(1f, 1f, planeId, styleEncoded);
                        colors[qv + 3] = new Color(0f, 1f, planeId, styleEncoded);

                        uv0[qv + 0] = packedUV0;
                        uv0[qv + 1] = packedUV0;
                        uv0[qv + 2] = packedUV0;
                        uv0[qv + 3] = packedUV0;

                        uv1[qv + 0] = packedUV1;
                        uv1[qv + 1] = packedUV1;
                        uv1[qv + 2] = packedUV1;
                        uv1[qv + 3] = packedUV1;

                        triangles[qt + 0] = qv + 0;
                        triangles[qt + 1] = qv + 2;
                        triangles[qt + 2] = qv + 1;
                        triangles[qt + 3] = qv + 0;
                        triangles[qt + 4] = qv + 3;
                        triangles[qt + 5] = qv + 2;
                    }
                }

                Mesh chunkMesh = new Mesh();
                chunkMesh.name = $"BillboardV2Chunk_{chunkIdx}_{count}p";
                if (totalVerts > 65000) chunkMesh.indexFormat = IndexFormat.UInt32;

                chunkMesh.vertices = vertices;
                chunkMesh.normals = normals;
                chunkMesh.colors = colors;
                chunkMesh.SetUVs(0, uv0);
                chunkMesh.SetUVs(1, uv1);
                chunkMesh.triangles = triangles;
                chunkMesh.RecalculateBounds();
                Bounds b = chunkMesh.bounds;
                b.Expand(new Vector3(3.0f, 3.5f, 3.0f));
                chunkMesh.bounds = b;

                GameObject chunkObj = new GameObject($"BillboardV2Chunk_{chunkIdx:00}");
                chunkObj.transform.SetParent(parentRoot, false);
                chunkObj.layer = gameObject.layer;

                MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
                mf.sharedMesh = chunkMesh;

                MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _sharedBillboardMaterial;
                mr.shadowCastingMode = shadowMode;
                mr.receiveShadows = true;

                _chunkRenderers.Add(mr);
                _generatedChunkCount++;
                _generatedTriangleCount += totalTris / 3;
            }
        }

        private void BuildTierFallbackStandMesh(Transform parentRoot, CrowdTierConfig tier, int tierIndex)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            int rows = Mathf.Max(1, tier.rowCount);
            float totalArcRad = Mathf.Clamp(tier.arcAngleDeg, 5f, 360f) * Mathf.Deg2Rad;
            float startRad = tier.startAngleDeg * Mathf.Deg2Rad;
            float halfStraight = Mathf.Max(0f, tier.straightTrackLength) * 0.5f;

            int steps = Mathf.Clamp(Mathf.CeilToInt(tier.arcAngleDeg / 5f), 8, 96);

            for (int s = 0; s < steps; s++)
            {
                float u0 = s / (float)steps;
                float u1 = (s + 1) / (float)steps;
                float a0 = startRad + u0 * totalArcRad;
                float a1 = startRad + u1 * totalArcRad;
                float midAngleDeg = Mathf.Repeat(((a0 + a1) * 0.5f) * Mathf.Rad2Deg, 360f);

                for (int r = 0; r < rows; r++)
                {
                    float rowNorm = rows > 1 ? (r / (float)(rows - 1)) : 0f;
                    float rxMid = tier.innerRadiusX + r * tier.rowDepth;
                    if (IsInsideAnyAngularCutout(midAngleDeg, rxMid, tierIndex)) continue;

                    float w0 = EvaluateTierElevationOffset(tier, a0 * Mathf.Rad2Deg, rowNorm);
                    float w1 = EvaluateTierElevationOffset(tier, a1 * Mathf.Rad2Deg, rowNorm);

                    float rx0 = tier.innerRadiusX + (r - 0.45f) * tier.rowDepth;
                    float rz0 = tier.innerRadiusZ + (r - 0.45f) * tier.rowDepth;
                    float rx1 = tier.innerRadiusX + (r + 0.55f) * tier.rowDepth;
                    float rz1 = tier.innerRadiusZ + (r + 0.55f) * tier.rowDepth;

                    float yTop0 = tier.baseHeightY + r * tier.rowRise + w0;
                    float yTop1 = tier.baseHeightY + r * tier.rowRise + w1;
                    float yBot0 = yTop0 - Mathf.Max(0.35f, tier.rowRise);
                    float yBot1 = yTop1 - Mathf.Max(0.35f, tier.rowRise);

                    Vector3 f0 = EvaluateOvalPoint(a0, rx0, rz0, halfStraight, yBot0, out _);
                    Vector3 f1 = EvaluateOvalPoint(a1, rx0, rz0, halfStraight, yBot1, out _);
                    Vector3 f0Top = new Vector3(f0.x, yTop0, f0.z);
                    Vector3 f1Top = new Vector3(f1.x, yTop1, f1.z);
                    Vector3 b0Top = EvaluateOvalPoint(a0, rx1, rz1, halfStraight, yTop0, out _);
                    Vector3 b1Top = EvaluateOvalPoint(a1, rx1, rz1, halfStraight, yTop1, out _);

                    AddQuad(verts, norms, tris, f0, f1, f1Top, f0Top);
                    AddQuad(verts, norms, tris, f0Top, f1Top, b1Top, b0Top);
                }
            }

            if (verts.Count == 0) return;

            Mesh standMesh = new Mesh();
            standMesh.name = $"FallbackStandBBV2_Tier{tierIndex}";
            if (verts.Count > 65000) standMesh.indexFormat = IndexFormat.UInt32;
            standMesh.SetVertices(verts);
            standMesh.SetNormals(norms);
            standMesh.SetTriangles(tris, 0);
            standMesh.RecalculateBounds();

            GameObject standObj = new GameObject($"FallbackStandBBV2_Tier{tierIndex}");
            standObj.transform.SetParent(parentRoot, false);
            standObj.layer = gameObject.layer;

            MeshFilter mf = standObj.AddComponent<MeshFilter>();
            mf.sharedMesh = standMesh;

            MeshRenderer mr = standObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedStandMaterial;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static void AddQuad(
            List<Vector3> verts,
            List<Vector3> norms,
            List<int> tris,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int b = verts.Count;
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            if (n.sqrMagnitude < 0.001f) n = Vector3.up;

            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);

            tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
        }

        // =====================================================================
        // クリア（未ベイクMeshのネイティブメモリ解放対応）・階層操作・ブラシ編集
        // =====================================================================
        public void ClearGeneratedCrowd()
        {
            _chunkRenderers.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && (child.name == CrowdRootName || child.name == MarkerObjectName))
                {
                    if (child.name == CrowdRootName)
                    {
                        DestroyUnbakedProceduralMeshesUnder(child);
                    }
                    DestroySafe(child.gameObject);
                }
            }

            _markerTransform = null;
            _generatedSpectatorCount = 0;
            _generatedChunkCount = 0;
            _generatedTriangleCount = 0;
            _isBakedAssets = false;
            _crowdBoundsCenterLocal = Vector3.zero;
            _crowdBoundingRadius = 150f;
            _trackingPausedByDistance = false;
        }

        private void DestroyUnbakedProceduralMeshesUnder(Transform root)
        {
            if (root == null) return;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null) continue;
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;

                bool isPersistentAsset = false;
#if UNITY_EDITOR
                isPersistentAsset = AssetDatabase.Contains(mesh);
#else
                isPersistentAsset = _isBakedAssets;
#endif
                if (!isPersistentAsset)
                {
                    mf.sharedMesh = null;
                    DestroySafe(mesh);
                }
            }
        }

        public BillboardExclusionZoneV2 AddExclusionZone()
        {
            GameObject zoneObj = new GameObject($"BillboardExclusionZoneV2_{transform.childCount + 1}");
            zoneObj.transform.SetParent(transform, false);

            float defaultZ = (_tiers != null && _tiers.Count > 0) ? _tiers[0].innerRadiusZ + 6f : 35f;
            zoneObj.transform.localPosition = new Vector3(0f, 6f, defaultZ);

            BillboardExclusionZoneV2 zone = zoneObj.AddComponent<BillboardExclusionZoneV2>();
            zone.size = new Vector3(14f, 18f, 16f);
            return zone;
        }

        public void AddNewTier()
        {
            EnsureDefaultTiers();
            CrowdTierConfig prev = _tiers[_tiers.Count - 1];
            int num = _tiers.Count + 1;
            Color col = Color.HSVToRGB((num * 0.27f) % 1f, 0.7f, 0.95f);
            _tiers.Add(new CrowdTierConfig
            {
                tierName = $"{num}階席 ({num}F)",
                enabled = true,
                gizmoColor = col,
                editInScene = true,
                innerRadiusX = prev.innerRadiusX + 8f,
                innerRadiusZ = prev.innerRadiusZ + 8f,
                straightTrackLength = prev.straightTrackLength,
                startAngleDeg = prev.startAngleDeg,
                arcAngleDeg = prev.arcAngleDeg,
                baseHeightY = prev.baseHeightY + 14f,
                rowCount = Mathf.Max(4, prev.rowCount),
                rowDepth = prev.rowDepth,
                rowRise = prev.rowRise,
                seatSpacing = prev.seatSpacing,
                staggerRows = prev.staggerRows,
                positionJitter = prev.positionJitter,
                emptySeatRatio = prev.emptySeatRatio,
                enableElevationWave = prev.enableElevationWave,
                wavePeakCount = prev.wavePeakCount,
                waveAmplitudeY = prev.waveAmplitudeY,
                wavePhaseDeg = prev.wavePhaseDeg,
                snapPolicy = BillboardSurfaceSamplerV2.FloorSelectMode.HighestInRange,
                useAbsoluteYBounds = true,
                snapMinY = prev.snapMaxY - 2f,
                snapMaxY = prev.snapMaxY + 28f,
                snapSearchUp = 12f,
                snapSearchDown = 12f
            });
        }

        public void AddSectorBlockTier()
        {
            EnsureDefaultTiers();
            CrowdTierConfig src = _tiers[0];
            int num = _tiers.Count + 1;
            Color col = Color.HSVToRGB((num * 0.19f + 0.12f) % 1f, 0.78f, 0.98f);

            for (int i = 0; i < _tiers.Count; i++)
            {
                if (_tiers[i] != null) _tiers[i].editInScene = false;
            }

            _tiers.Add(new CrowdTierConfig
            {
                tierName = $"区画ブロック #{num}",
                enabled = true,
                gizmoColor = col,
                editInScene = true,
                innerRadiusX = src != null ? src.innerRadiusX : 30f,
                innerRadiusZ = src != null ? src.innerRadiusZ : 30f,
                straightTrackLength = 0f,
                startAngleDeg = Mathf.Repeat((num - 1) * 70f, 360f),
                arcAngleDeg = 65f,
                baseHeightY = src != null ? src.baseHeightY : 0f,
                rowCount = src != null ? src.rowCount : 18,
                rowDepth = src != null ? src.rowDepth : 1.05f,
                rowRise = src != null ? src.rowRise : 0.55f,
                seatSpacing = src != null ? src.seatSpacing : 0.85f,
                staggerRows = true,
                positionJitter = 0.08f,
                emptySeatRatio = 0.05f,
                snapPolicy = BillboardSurfaceSamplerV2.FloorSelectMode.HighestInRange,
                useAbsoluteYBounds = true,
                snapMinY = src != null ? src.snapMinY : 0.3f,
                snapMaxY = src != null ? src.snapMaxY : 14f,
                snapSearchUp = 10f,
                snapSearchDown = 10f
            });
        }

        public void DuplicateTier(int index)
        {
            if (_tiers == null || index < 0 || index >= _tiers.Count || _tiers[index] == null) return;
            CrowdTierConfig src = _tiers[index];

            for (int i = 0; i < _tiers.Count; i++)
            {
                if (_tiers[i] != null) _tiers[i].editInScene = false;
            }

            bool isSectorBlock = src.arcAngleDeg < 359.5f;
            float nextStartAngle = isSectorBlock
                ? Mathf.Repeat(src.startAngleDeg + src.arcAngleDeg + 8f, 360f)
                : src.startAngleDeg;

            AnimationCurve copiedCurve = src.customElevationCurve != null
                ? new AnimationCurve(src.customElevationCurve.keys)
                : AnimationCurve.Linear(0f, 0f, 1f, 0f);

            Color nextCol = Color.HSVToRGB(((_tiers.Count + 1) * 0.21f) % 1f, 0.75f, 0.96f);

            _tiers.Insert(index + 1, new CrowdTierConfig
            {
                tierName = src.tierName + " (複製)",
                enabled = true,
                gizmoColor = nextCol,
                editInScene = true,
                innerRadiusX = src.innerRadiusX,
                innerRadiusZ = src.innerRadiusZ,
                straightTrackLength = src.straightTrackLength,
                startAngleDeg = nextStartAngle,
                arcAngleDeg = src.arcAngleDeg,
                baseHeightY = src.baseHeightY,
                rowCount = src.rowCount,
                rowDepth = src.rowDepth,
                rowRise = src.rowRise,
                seatSpacing = src.seatSpacing,
                staggerRows = src.staggerRows,
                positionJitter = src.positionJitter,
                emptySeatRatio = src.emptySeatRatio,
                generateFallbackStandMesh = src.generateFallbackStandMesh,
                enableElevationWave = src.enableElevationWave,
                wavePeakCount = src.wavePeakCount,
                waveAmplitudeY = src.waveAmplitudeY,
                wavePhaseDeg = src.wavePhaseDeg,
                waveExponent = src.waveExponent,
                waveOuterRowScale = src.waveOuterRowScale,
                useCustomElevationCurve = src.useCustomElevationCurve,
                customElevationCurve = copiedCurve,
                customCurveScaleY = src.customCurveScaleY,
                snapPolicy = src.snapPolicy,
                useAbsoluteYBounds = src.useAbsoluteYBounds,
                snapMinY = src.snapMinY,
                snapMaxY = src.snapMaxY,
                snapSearchUp = src.snapSearchUp,
                snapSearchDown = src.snapSearchDown,
                surfaceYOffset = src.surfaceYOffset
            });
        }

        private bool HasAnyEnabledTier()
        {
            if (_tiers == null) return false;
            for (int i = 0; i < _tiers.Count; i++)
            {
                if (_tiers[i] != null && _tiers[i].enabled) return true;
            }
            return false;
        }

        public void SwitchToManualBrushOnlyMode(bool keepExistingManualSeats = true)
        {
            if (_tiers != null)
            {
                for (int i = 0; i < _tiers.Count; i++)
                {
                    if (_tiers[i] != null)
                    {
                        _tiers[i].enabled = false;
                        _tiers[i].editInScene = false;
                    }
                }
            }

            if (_brushEraseStamps != null) _brushEraseStamps.Clear();
            if (!keepExistingManualSeats && _manualPaintedSeats != null)
            {
                _manualPaintedSeats.Clear();
            }

            GenerateCrowd(false);
        }

        public void BeginBrushSession()
        {
            _keepSamplerAliveForBrush = true;
        }

        public void EndBrushSession()
        {
            _keepSamplerAliveForBrush = false;
            if (_cachedSampler != null && _cachedSampler.HasTemporaryProxies)
            {
                DisposeSamplerCache();
            }
        }

        public void FlushBrushMeshRebuild()
        {
            RebuildCrowdChunksAndStandsFromCurrentPoints();
        }

        public bool IsInsideAnyBrushEraseStamp(Vector3 localPos)
        {
            if (_brushEraseStamps == null || _brushEraseStamps.Count == 0) return false;
            for (int i = 0; i < _brushEraseStamps.Count; i++)
            {
                BrushEraseStamp st = _brushEraseStamps[i];
                float r = Mathf.Max(0.1f, st.radius);
                if ((localPos - st.localPosition).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        public bool TryRaycastBrushTarget(
            Ray worldRay,
            bool allowFloatingSeatHit,
            float fallbackPlaneY,
            out Vector3 outLocalPos,
            out Vector3 outLocalNormal)
        {
            outLocalPos = Vector3.zero;
            outLocalNormal = Vector3.up;

            if (HasAssignedSurfaceTarget)
            {
                BillboardSurfaceSamplerV2 sampler = GetOrBuildSampler(false);
                if (sampler != null && sampler.TryRaycastSurface(worldRay, out outLocalPos, out outLocalNormal))
                {
                    return true;
                }
            }

            if (allowFloatingSeatHit && _lastPlacementPoints.Count > 0)
            {
                Vector3 originL = transform.InverseTransformPoint(worldRay.origin);
                Vector3 dirL = transform.InverseTransformDirection(worldRay.direction).normalized;
                float bestDistSqr = 4.0f * 4.0f;
                int bestIdx = -1;

                for (int i = 0; i < _lastPlacementPoints.Count; i++)
                {
                    Vector3 p = _lastPlacementPoints[i].localPosition + Vector3.up * 0.85f;
                    Vector3 toP = p - originL;
                    float proj = Vector3.Dot(toP, dirL);
                    if (proj < 0.1f) continue;
                    Vector3 closest = originL + dirL * proj;
                    float dSqr = (p - closest).sqrMagnitude;
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    outLocalPos = _lastPlacementPoints[bestIdx].localPosition;
                    outLocalNormal = Vector3.up;
                    return true;
                }
            }

            Plane fallbackPlane = new Plane(transform.up, transform.TransformPoint(new Vector3(0f, fallbackPlaneY, 0f)));
            if (fallbackPlane.Raycast(worldRay, out float enter) && enter > 0.1f)
            {
                outLocalPos = transform.InverseTransformPoint(worldRay.GetPoint(enter));
                outLocalNormal = Vector3.up;
                return true;
            }

            return false;
        }

        public bool FastApplyBrushErase(Vector3 localCenter, float radius, bool rebuildNow = true)
        {
            if (_brushEraseStamps == null) _brushEraseStamps = new List<BrushEraseStamp>();
            if (_manualPaintedSeats == null) _manualPaintedSeats = new List<ManualPaintedSeat>();

            float r = Mathf.Max(0.25f, radius);
            float rSqr = r * r;

            bool addStamp = HasAnyEnabledTier();
            if (addStamp && _brushEraseStamps.Count > 0)
            {
                BrushEraseStamp last = _brushEraseStamps[_brushEraseStamps.Count - 1];
                float minStep = r * 0.28f;
                if ((last.localPosition - localCenter).sqrMagnitude < minStep * minStep && last.radius >= r * 0.95f)
                {
                    addStamp = false;
                }
            }

            if (addStamp)
            {
                _brushEraseStamps.Add(new BrushEraseStamp
                {
                    localPosition = localCenter,
                    radius = r
                });
            }

            bool removedAny = false;
            for (int i = _manualPaintedSeats.Count - 1; i >= 0; i--)
            {
                if ((_manualPaintedSeats[i].localPosition - localCenter).sqrMagnitude <= rSqr)
                {
                    _manualPaintedSeats.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (_lastPlacementPoints.Count == 0 && _generatedSpectatorCount > 0)
            {
                GenerateCrowd(false);
                return true;
            }

            for (int i = _lastPlacementPoints.Count - 1; i >= 0; i--)
            {
                if ((_lastPlacementPoints[i].localPosition - localCenter).sqrMagnitude <= rSqr)
                {
                    _lastPlacementPoints.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny && rebuildNow)
            {
                RebuildCrowdChunksAndStandsFromCurrentPoints();
            }

            return removedAny || addStamp;
        }

        public bool FastApplyBrushPaint(Vector3 localCenter, float radius, float seatSpacing, bool rebuildNow = true)
        {
            if (_manualPaintedSeats == null) _manualPaintedSeats = new List<ManualPaintedSeat>();

            if (_lastPlacementPoints.Count == 0 && _generatedSpectatorCount > 0)
            {
                GenerateCrowd(false);
            }

            float r = Mathf.Max(0.3f, radius);
            float rSqr = r * r;

            if (_brushEraseStamps != null && _brushEraseStamps.Count > 0)
            {
                for (int i = _brushEraseStamps.Count - 1; i >= 0; i--)
                {
                    if ((_brushEraseStamps[i].localPosition - localCenter).sqrMagnitude <= rSqr)
                    {
                        _brushEraseStamps.RemoveAt(i);
                    }
                }
            }

            float spacing = Mathf.Clamp(seatSpacing, 0.45f, 3.0f);
            float minDistSqr = (spacing * 0.78f) * (spacing * 0.78f);

            BillboardSurfaceSamplerV2 sampler = HasAssignedSurfaceTarget ? GetOrBuildSampler(false) : null;
            bool hasSurface = sampler != null && sampler.HasAnySurface;

            int gridSteps = Mathf.Clamp(Mathf.CeilToInt(r / spacing), 0, 12);
            int addedCount = 0;

            Vector2 radialDir = new Vector2(localCenter.x, localCenter.z);
            if (radialDir.sqrMagnitude < 0.01f) radialDir = Vector2.up;
            else radialDir.Normalize();
            Vector2 tangentDir = new Vector2(-radialDir.y, radialDir.x);

            for (int gx = -gridSteps; gx <= gridSteps; gx++)
            {
                for (int gz = -gridSteps; gz <= gridSteps; gz++)
                {
                    float stagger = ((gz & 1) != 0) ? (spacing * 0.5f) : 0f;
                    float dx = gx * spacing + stagger;
                    float dz = gz * spacing;
                    if (dx * dx + dz * dz > rSqr) continue;

                    Vector2 xz = new Vector2(localCenter.x, localCenter.z) + tangentDir * dx + radialDir * dz;
                    float candidateY = localCenter.y;

                    if (hasSurface)
                    {
                        Vector3 inward = new Vector3(-xz.x, 0f, -xz.y);
                        if (inward.sqrMagnitude > 0.001f) inward.Normalize();

                        if (!sampler.TrySampleFloorY(
                            xz.x,
                            xz.y,
                            localCenter.y,
                            localCenter.y - 3.0f,
                            localCenter.y + 3.0f,
                            BillboardSurfaceSamplerV2.FloorSelectMode.NearestToExpectedY,
                            inward,
                            out float hitY,
                            out _,
                            true))
                        {
                            continue;
                        }
                        candidateY = hitY + 0.02f;
                    }

                    Vector3 candidatePos = new Vector3(xz.x, candidateY, xz.y);

                    bool tooClose = false;
                    for (int i = 0; i < _lastPlacementPoints.Count; i++)
                    {
                        if ((_lastPlacementPoints[i].localPosition - candidatePos).sqrMagnitude < minDistSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    float hashA = Mathf.Repeat(Mathf.Sin(candidatePos.x * 12.9898f + candidatePos.z * 78.233f + candidatePos.y * 37.719f) * 43758.5453f, 1f);
                    float hashB = Mathf.Repeat(Mathf.Sin(candidatePos.x * 39.346f + candidatePos.z * 11.135f + candidatePos.y * 83.155f) * 24634.6345f, 1f);

                    Vector3 toCenter = new Vector3(-candidatePos.x, 0f, -candidatePos.z);
                    float yawRad = (toCenter.sqrMagnitude > 0.001f ? Mathf.Atan2(toCenter.x, toCenter.z) : 0f) + (hashA * 2f - 1f) * 0.14f;
                    float angle01 = Mathf.Repeat(Mathf.Atan2(candidatePos.x, candidatePos.z) * Mathf.Rad2Deg, 360f) / 360f;

                    ManualPaintedSeat newSeat = new ManualPaintedSeat
                    {
                        localPosition = candidatePos,
                        baseYawRad = yawRad,
                        seedA = hashA,
                        seedB = hashB,
                        stadiumAngle01 = angle01,
                        rowNormalized01 = 0.5f
                    };

                    _manualPaintedSeats.Add(newSeat);
                    _lastPlacementPoints.Add(new SpectatorPlacementPoint
                    {
                        localPosition = newSeat.localPosition,
                        baseYawRad = newSeat.baseYawRad,
                        seedA = newSeat.seedA,
                        seedB = newSeat.seedB,
                        stadiumAngle01 = newSeat.stadiumAngle01,
                        rowNormalized01 = newSeat.rowNormalized01
                    });
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                if (rebuildNow)
                {
                    RebuildCrowdChunksAndStandsFromCurrentPoints();
                }
                return true;
            }
            return false;
        }

        public void ClearBrushErases()
        {
            if (_brushEraseStamps != null) _brushEraseStamps.Clear();
        }

        public void ClearManualPaintedSeats()
        {
            if (_manualPaintedSeats != null) _manualPaintedSeats.Clear();
        }

        public void UndoLastBrushEraseStamp()
        {
            if (_brushEraseStamps != null && _brushEraseStamps.Count > 0)
            {
                _brushEraseStamps.RemoveAt(_brushEraseStamps.Count - 1);
            }
        }

        public void ApplyThreeWingStadiumTemplate()
        {
            if (_angularCutouts == null) _angularCutouts = new List<AngularCutoutZone>();
            if (_radialWalls == null) _radialWalls = new List<RadialPartitionWall>();

            _angularCutouts.Clear();
            _angularCutouts.Add(new AngularCutoutZone
            {
                label = "巨大モニター開口 (上側)",
                enabled = true,
                centerAngleDeg = 0f,
                arcWidthDeg = 42f,
                targetTierIndex = -1,
                gizmoColor = new Color(1.0f, 0.22f, 0.32f, 0.85f)
            });
            _angularCutouts.Add(new AngularCutoutZone
            {
                label = "発射口ゲート開口 (下側)",
                enabled = true,
                centerAngleDeg = 180f,
                arcWidthDeg = 32f,
                targetTierIndex = -1,
                gizmoColor = new Color(1.0f, 0.50f, 0.15f, 0.85f)
            });

            _enablePeriodicWalls = false;
            _radialWalls.Clear();
            float[] sampleWallAngles = { 60f, 120f, 240f, 300f };
            for (int i = 0; i < sampleWallAngles.Length; i++)
            {
                _radialWalls.Add(new RadialPartitionWall
                {
                    label = $"仕切り壁 #{i + 1}",
                    enabled = true,
                    angleDeg = sampleWallAngles[i],
                    thicknessMeters = 2.6f,
                    marginMeters = 0.55f,
                    targetTierIndex = -1,
                    gizmoColor = new Color(1.0f, 0.90f, 0.20f, 0.85f)
                });
            }
        }

        // =====================================================================
        // マーカー・マテリアル初期化
        // =====================================================================
        private void UpdateTargetMarkerVisual()
        {
            bool shouldShow = _showTargetMarkerMesh && (_trackingMode != TargetTrackingMode.FollowTransform || _lookAtTarget == null);

            if (!shouldShow)
            {
                if (_markerTransform != null) _markerTransform.gameObject.SetActive(false);
                return;
            }

            if (_markerTransform == null)
            {
                Transform existing = transform.Find(MarkerObjectName);
                if (existing != null)
                {
                    _markerTransform = existing;
                }
                else
                {
                    GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    markerObj.name = MarkerObjectName;
                    markerObj.transform.SetParent(transform, false);
                    markerObj.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);

                    Collider col = markerObj.GetComponent<Collider>();
                    if (col != null) DestroySafe(col);

                    MeshRenderer mr = markerObj.GetComponent<MeshRenderer>();
                    if (mr != null && _sharedStandMaterial != null)
                    {
                        mr.sharedMaterial = _sharedStandMaterial;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    _markerTransform = markerObj.transform;
                }
            }

            if (_markerTransform != null)
            {
                _markerTransform.gameObject.SetActive(true);
                _markerTransform.position = _currentTargetWorldPos;
            }
        }

        private void EnsureMaterials()
        {
            if (_billboardShader == null)
            {
                _billboardShader = Shader.Find("AT_Murayama/StadiumCrowdBillboardV2/S_StadiumCrowdBillboardV2");
            }

            if (_sharedBillboardMaterial == null && _billboardShader != null)
            {
#if UNITY_EDITOR
                const string matDir = "Assets/Deverop/AT_Murayama/StadiumCrowdBillboardV2/Materials";
                const string matPath = matDir + "/M_StadiumCrowdBillboardV2_Default.mat";
                _sharedBillboardMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (_sharedBillboardMaterial == null && !Application.isPlaying)
                {
                    EnsureAssetFolder(matDir);
                    _sharedBillboardMaterial = new Material(_billboardShader)
                    {
                        name = "M_StadiumCrowdBillboardV2_Default",
                        enableInstancing = true
                    };
                    AssetDatabase.CreateAsset(_sharedBillboardMaterial, matPath);
                }
#endif
                if (_sharedBillboardMaterial == null)
                {
                    _sharedBillboardMaterial = new Material(_billboardShader)
                    {
                        name = "M_StadiumCrowdBillboardV2_Runtime"
                    };
                }
            }

            if (_sharedStandMaterial == null)
            {
#if UNITY_EDITOR
                const string matDir = "Assets/Deverop/AT_Murayama/StadiumCrowdBillboardV2/Materials";
                const string standMatPath = matDir + "/M_StadiumBleacherBBV2_Default.mat";
                _sharedStandMaterial = AssetDatabase.LoadAssetAtPath<Material>(standMatPath);
                if (_sharedStandMaterial == null && !Application.isPlaying)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader != null)
                    {
                        EnsureAssetFolder(matDir);
                        _sharedStandMaterial = new Material(litShader)
                        {
                            name = "M_StadiumBleacherBBV2_Default",
                            color = new Color(0.18f, 0.20f, 0.24f, 1f)
                        };
                        _sharedStandMaterial.SetFloat("_Smoothness", 0.25f);
                        AssetDatabase.CreateAsset(_sharedStandMaterial, standMatPath);
                    }
                }
#endif
                if (_sharedStandMaterial == null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader != null)
                    {
                        _sharedStandMaterial = new Material(litShader)
                        {
                            name = "M_StadiumBleacherBBV2_Runtime",
                            color = new Color(0.18f, 0.20f, 0.24f, 1f)
                        };
                    }
                }
            }
        }

        private void RefreshChunkRenderersList()
        {
            _chunkRenderers.Clear();
            Transform root = transform.Find(CrowdRootName);
            if (root == null) return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name.StartsWith("BillboardV2Chunk_"))
                {
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null) _chunkRenderers.Add(mr);
                }
            }
        }

        private static int SpatialSortComparison(SpectatorPlacementPoint a, SpectatorPlacementPoint b)
        {
            int ax = Mathf.FloorToInt(a.localPosition.x / SpatialChunkCellSize);
            int bx = Mathf.FloorToInt(b.localPosition.x / SpatialChunkCellSize);
            int cmpX = ax.CompareTo(bx);
            if (cmpX != 0) return cmpX;

            int az = Mathf.FloorToInt(a.localPosition.z / SpatialChunkCellSize);
            int bz = Mathf.FloorToInt(b.localPosition.z / SpatialChunkCellSize);
            int cmpZ = ((ax & 1) == 0) ? az.CompareTo(bz) : bz.CompareTo(az);
            if (cmpZ != 0) return cmpZ;

            int ay = Mathf.FloorToInt(a.localPosition.y / 8f);
            int by = Mathf.FloorToInt(b.localPosition.y / 8f);
            int cmpY = ay.CompareTo(by);
            if (cmpY != 0) return cmpY;

            return a.stadiumAngle01.CompareTo(b.stadiumAngle01);
        }

        private bool HasMissingChunkMeshes()
        {
            for (int i = 0; i < _chunkRenderers.Count; i++)
            {
                MeshRenderer mr = _chunkRenderers[i];
                if (mr == null) return true;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return true;
            }
            return false;
        }

        private Camera GetMainCameraCached()
        {
            if (_cachedMainCamera != null && _cachedMainCamera.isActiveAndEnabled && _cachedMainCamera.CompareTag("MainCamera"))
            {
                return _cachedMainCamera;
            }

            if (Application.isPlaying)
            {
                if (Time.unscaledTime < _nextCameraLookupTime && _cachedMainCamera == null)
                {
                    return null;
                }
                _nextCameraLookupTime = Time.unscaledTime + 1.0f;
            }

            _cachedMainCamera = Camera.main;
            return _cachedMainCamera;
        }

        public static bool IsSafeLayerForCull(int layer)
        {
            return layer >= 11 && layer <= 31 && layer != 29 && layer != 30;
        }

        public void UpdateLayerCullDistance()
        {
            if (!Application.isPlaying || !_applyLayerCullDistance || !IsSafeLayerForCull(gameObject.layer))
            {
                RestoreLayerCullDistance();
                return;
            }

            Camera cam = GetMainCameraCached();
            if (cam == null) return;

            int layer = gameObject.layer;
            float targetDist = Mathf.Max(10f, _layerCullDistance);

            if (_appliedCullCamera == cam && _appliedCullLayer == layer && Mathf.Approximately(_appliedCullDistanceValue, targetDist))
            {
                return;
            }

            RestoreLayerCullDistance();

            float[] dists = cam.layerCullDistances;
            if (dists != null && layer >= 0 && layer < dists.Length)
            {
                _savedPrevLayerCullDistance = dists[layer];
                _hasSavedLayerCullState = true;
                dists[layer] = targetDist;
                cam.layerCullDistances = dists;
                _appliedCullCamera = cam;
                _appliedCullLayer = layer;
                _appliedCullDistanceValue = targetDist;
            }
        }

        public void RestoreLayerCullDistance()
        {
            if (!_hasSavedLayerCullState) return;

            if (_appliedCullCamera != null && _appliedCullLayer >= 0 && _appliedCullLayer < 32)
            {
                float[] dists = _appliedCullCamera.layerCullDistances;
                if (dists != null && _appliedCullLayer < dists.Length)
                {
                    dists[_appliedCullLayer] = _savedPrevLayerCullDistance;
                    _appliedCullCamera.layerCullDistances = dists;
                }
            }

            _hasSavedLayerCullState = false;
            _appliedCullCamera = null;
            _appliedCullLayer = -1;
            _appliedCullDistanceValue = -1f;
            _savedPrevLayerCullDistance = 0f;
        }

#if UNITY_EDITOR
        public bool BakeCrowdAssetsToProject(out string savedFolderPath)
        {
            savedFolderPath = string.Empty;
            if (Application.isPlaying)
            {
                Debug.LogWarning("[StadiumCrowdBillboardV2] Play Mode 中のベイクは未対応です。編集モードで実行してください。", this);
                return false;
            }

            RefreshChunkRenderersList();
            if (_chunkRenderers.Count == 0 || HasMissingChunkMeshes())
            {
                GenerateCrowd(false);
                RefreshChunkRenderersList();
            }

            Transform root = transform.Find(CrowdRootName);
            if (root == null || _chunkRenderers.Count == 0)
            {
                Debug.LogWarning("[StadiumCrowdBillboardV2] ベイク対象の生成済みチャンクが存在しません。", this);
                return false;
            }

            ApplyMaterialSettingsToAllChunks();

            string safeName = MakeSafeFileName(gameObject.name);
            int idToken = Mathf.Abs(GetInstanceID());
            savedFolderPath = $"Assets/Deverop/AT_Murayama/StadiumCrowdBillboardV2/Baked/{safeName}_{idToken}";
            EnsureAssetFolder(savedFolderPath);

            if (_sharedBillboardMaterial != null)
            {
                string bbMatPath = $"{savedFolderPath}/M_{safeName}_BillboardV2Baked.mat";
                string currentMatPath = AssetDatabase.GetAssetPath(_sharedBillboardMaterial);
                if (currentMatPath == bbMatPath)
                {
                    EditorUtility.SetDirty(_sharedBillboardMaterial);
                }
                else
                {
                    Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(bbMatPath);
                    if (existingMat != null)
                    {
                        EditorUtility.CopySerialized(_sharedBillboardMaterial, existingMat);
                        _sharedBillboardMaterial = existingMat;
                        EditorUtility.SetDirty(existingMat);
                    }
                    else
                    {
                        Material bakedMat = new Material(_sharedBillboardMaterial)
                        {
                            name = $"M_{safeName}_BillboardV2Baked"
                        };
                        AssetDatabase.CreateAsset(bakedMat, bbMatPath);
                        _sharedBillboardMaterial = bakedMat;
                    }
                }
            }

            bool hasStandMesh = false;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name.StartsWith("FallbackStandBBV2_"))
                {
                    hasStandMesh = true;
                    break;
                }
            }

            if (hasStandMesh && _sharedStandMaterial != null)
            {
                string standMatPath = $"{savedFolderPath}/M_{safeName}_StandBBV2Baked.mat";
                string currentStandMatPath = AssetDatabase.GetAssetPath(_sharedStandMaterial);
                if (currentStandMatPath == standMatPath)
                {
                    EditorUtility.SetDirty(_sharedStandMaterial);
                }
                else
                {
                    Material existingStandMat = AssetDatabase.LoadAssetAtPath<Material>(standMatPath);
                    if (existingStandMat != null)
                    {
                        EditorUtility.CopySerialized(_sharedStandMaterial, existingStandMat);
                        _sharedStandMaterial = existingStandMat;
                        EditorUtility.SetDirty(existingStandMat);
                    }
                    else
                    {
                        Material bakedStandMat = new Material(_sharedStandMaterial)
                        {
                            name = $"M_{safeName}_StandBBV2Baked"
                        };
                        AssetDatabase.CreateAsset(bakedStandMat, standMatPath);
                        _sharedStandMaterial = bakedStandMat;
                    }
                }
            }

            int savedMeshCount = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;

                MeshFilter mf = child.GetComponent<MeshFilter>();
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null) continue;

                string meshPath = $"{savedFolderPath}/{child.name}.asset";
                string currentMeshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (currentMeshPath == meshPath)
                {
                    EditorUtility.SetDirty(mf.sharedMesh);
                }
                else
                {
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                    {
                        AssetDatabase.DeleteAsset(meshPath);
                    }
                    Mesh meshCopy = Instantiate(mf.sharedMesh);
                    meshCopy.name = mf.sharedMesh.name;
                    AssetDatabase.CreateAsset(meshCopy, meshPath);
                    mf.sharedMesh = meshCopy;
                }

                if (mr != null)
                {
                    bool isStand = child.name.StartsWith("FallbackStandBBV2_");
                    mr.sharedMaterial = isStand ? _sharedStandMaterial : _sharedBillboardMaterial;
                    if (!isStand)
                    {
                        mr.shadowCastingMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    }
                    EditorUtility.SetDirty(mr);
                }
                child.gameObject.layer = gameObject.layer;
                EditorUtility.SetDirty(mf);
                savedMeshCount++;
            }

            _isBakedAssets = savedMeshCount > 0;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return _isBakedAssets;
        }

        public void ApplyProductionPreset()
        {
            Undo.RecordObject(this, "Apply Production BillboardCrowdV2 Settings");

            _showTargetMarkerMesh = false;
            _previewInEditMode = false;
            _trackingMode = TargetTrackingMode.FollowTransform;
            _autoFindPlayerAtRuntime = true;

            if (_lookAtTarget == null)
            {
                TryAutoFindPlayerTarget();
            }

            DestroyPreviewMarkerImmediate();
            ApplyMaterialSettingsToAllChunks();
            EditorUtility.SetDirty(this);
        }

        public void DestroyPreviewMarkerImmediate()
        {
            if (_markerTransform != null)
            {
                Undo.DestroyObjectImmediate(_markerTransform.gameObject);
                _markerTransform = null;
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == MarkerObjectName)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void EnsureAssetFolder(string targetFolder)
        {
            string normalized = targetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static string MakeSafeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "BillboardCrowdV2";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-') sb.Append(ch);
                else sb.Append('_');
            }
            return sb.Length > 0 ? sb.ToString() : "BillboardCrowdV2";
        }
#endif

        private static float NextFloat(System.Random rng)
        {
            return (float)rng.NextDouble();
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(obj);
                return;
            }
#endif
            Destroy(obj);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1.0f, 0.6f, 0.9f);
            Vector3 landingWorld = transform.TransformPoint(_simLandingPointLocal);
            Vector3 skyStartWorld = transform.TransformPoint(_simLandingPointLocal + new Vector3(_simApproachOffsetXZ.x, _simStartHeight, _simApproachOffsetXZ.y));

            Gizmos.DrawWireSphere(landingWorld, 2.0f);
            Gizmos.DrawLine(skyStartWorld, landingWorld);

            Gizmos.color = new Color(1.0f, 0.85f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(_currentTargetWorldPos, 1.5f);

            Gizmos.color = new Color(1.0f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(_delayedTargetWorldPos, 1.1f);
        }
#endif
    }
}
