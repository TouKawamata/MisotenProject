using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.StadiumCrowd
{
    /// <summary>
    /// 【案A】スタジアム観客生成＆視線追従コントローラー（軽量3Dローポリ結合メッシュ ＋ 頂点シェーダー制御）。
    /// ・数千〜数万人の観客を少数のチャンクメッシュに事前結合し、CPU負荷ほぼ0ms・0 GC Allocで描画。
    /// ・上空からスタジアムに着地するキャラクター（またはシミュレーションターゲット）に合わせて、
    ///   各観客の視線（頭部・上半身・掲げた腕）が個体ごとの反応ラグを伴いながら追従します。
    /// ・円形/楕円/トラック型スタジアム、直線ブロック、折れ線パス、既存3Dモデル表面吸着の4配置モードに対応し、
    ///   セクターマスクや除外ゾーン（CrowdExclusionZone）によって「客席が途中で途切れる形状」も自在に作れます。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StadiumCrowdGenerator : MonoBehaviour
    {
        public enum PlacementMode
        {
            [InspectorName("① 円形・楕円・トラック型スタジアム (Circular / Oval / Track Bowl)")]
            CircularBowl = 0,

            [InspectorName("② 直線・分割ブロック客席 (Linear / Segmented Stand)")]
            LinearStand = 1,

            [InspectorName("③ 折れ線・自由パス客席 (Multi-Point Path Stand)")]
            CustomPathStand = 2,

            [InspectorName("④ 既存3Dモデル表面へ自動吸着 (Mesh Surface Scatter)")]
            MeshSurfaceScatter = 3
        }

        public enum CrowdColorTheme
        {
            [InspectorName("① レース観戦カジュアル (多彩な私服＆レーシングキャップ)")]
            RacingCasual = 0,

            [InspectorName("② チームカラー対抗 (ブルー＆ホワイト vs レッド＆イエロー)")]
            TeamRivalry = 1,

            [InspectorName("③ 落ち着いたシルエット重視 (背景に馴染むダークトーン)")]
            SubtleSilhouette = 2,

            [InspectorName("④ サイバーナイトスタジアム (暗色スーツ＋ネオンアクセント)")]
            CyberNight = 3,

            [InspectorName("⑤ カスタム手動設定 (インスペクターの色を維持)")]
            Custom = 4
        }

        public enum TargetTrackingMode
        {
            [InspectorName("① 上空降下→着地シミュレーション (Auto Sky-Dive Loop)")]
            SkyDiveSimulation = 0,

            [InspectorName("② 手動スライダーで降下位置を確認 (Manual Scrub 0%〜100%)")]
            ManualAltitudeScrub = 1,

            [InspectorName("③ 指定Transform / プレイヤー実機追従 (Target Transform)")]
            FollowTransform = 2
        }

        [Serializable]
        public struct SpectatorPlacementPoint
        {
            public Vector3 localPosition;
            public float baseYawRad;
            public float seedA;
            public float seedB;
            public float stadiumAngle01;
            public float rowNormalized01;
        }

        // =====================================================================
        // 1. 見た目・スタイル設定 (後からいつでもワンクリック変更可能)
        // =====================================================================
        [Header("1. 観客の見た目・スタイル設定")]
        [Tooltip("観客1人あたりの3Dメッシュ形状スタイル（後からいつでも変更・再生成可能）")]
        [SerializeField] private SpectatorMeshFactory.SpectatorStyle _spectatorStyle = SpectatorMeshFactory.SpectatorStyle.PureHuman;

        [Tooltip("カスタム外部メッシュ差し替え時の参照メッシュ")]
        [SerializeField] private Mesh _customSpectatorMesh;

        [Tooltip("観客の服装・カラーテーマ")]
        [SerializeField] private CrowdColorTheme _colorTheme = CrowdColorTheme.RacingCasual;

        [SerializeField] private Color _outfitColorA = new Color(0.16f, 0.42f, 0.85f, 1f);
        [SerializeField] private Color _outfitColorB = new Color(0.86f, 0.22f, 0.24f, 1f);
        [SerializeField] private Color _outfitColorC = new Color(0.90f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color _outfitColorD = new Color(0.95f, 0.72f, 0.16f, 1f);
        [SerializeField] private Color _pantsColor = new Color(0.14f, 0.16f, 0.20f, 1f);
        [SerializeField] private Color _skinColor = new Color(0.88f, 0.72f, 0.62f, 1f);
        [SerializeField] private Color _hairHatColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] private Color _accentEmissionColor = new Color(0.25f, 0.75f, 1.0f, 1f);
        [Range(0f, 3f)]
        [SerializeField] private float _accentEmissionStrength = 0.0f;

        [Tooltip("観客の基本スケール倍率")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float _spectatorScale = 1.0f;

        [Tooltip("体格・身長の個体差バラつき")]
        [Range(0f, 0.25f)]
        [SerializeField] private float _heightVariation = 0.10f;

        // =====================================================================
        // 2. 配置形状・客席の途切れ設定
        // =====================================================================
        [Header("2. 配置モード & スタジアム形状設定")]
        [Tooltip("スタジアムや客席の配置形状モード")]
        [SerializeField] private PlacementMode _placementMode = PlacementMode.CircularBowl;

        [Tooltip("【円形/楕円】フィールド内径 X（横半径 m）")]
        [Min(5f)]
        [SerializeField] private float _innerRadiusX = 32f;

        [Tooltip("【円形/楕円】フィールド内径 Z（縦半径 m）")]
        [Min(5f)]
        [SerializeField] private float _innerRadiusZ = 32f;

        [Tooltip("【トラック型】直線部分の延長長さ（0で真円/楕円、プラスで陸上トラック型オーバル）")]
        [Min(0f)]
        [SerializeField] private float _straightTrackLength = 0f;

        [Tooltip("【円形】配置開始角度（度）")]
        [Range(0f, 360f)]
        [SerializeField] private float _startAngleDeg = 0f;

        [Tooltip("【円形】配置する全体の弧の角度（360で全周、180で半周など）")]
        [Range(15f, 360f)]
        [SerializeField] private float _arcAngleDeg = 360f;

        [Tooltip("【直線モード】スタンド全体の横幅（m）")]
        [Min(5f)]
        [SerializeField] private float _linearWidth = 60f;

        [Tooltip("【折れ線パスモード】客席を並べるローカル制御点リスト")]
        [SerializeField] private List<Vector3> _pathPoints = new List<Vector3>
        {
            new Vector3(-30f, 0f, -20f),
            new Vector3(-15f, 0f,  15f),
            new Vector3( 15f, 0f,  15f),
            new Vector3( 30f, 0f, -20f)
        };

        [Tooltip("【折れ線パスモード】始点と終点を繋いでループさせるか")]
        [SerializeField] private bool _closePathLoop = false;

        [Tooltip("【3Dモデル表面吸着モード】観客を立たせる対象の親Transform（未指定時は下のリストを参照）")]
        [SerializeField] private Transform _targetSurfaceRoot;

        [Tooltip("【3Dモデル表面吸着モード】対象のMeshFilterリスト")]
        [SerializeField] private List<MeshFilter> _targetMeshFilters = new List<MeshFilter>();

        [Tooltip("【3Dモデル表面吸着モード】観客が立てる面の最小上向き法線Y（0.75なら傾斜約40度まで許可）")]
        [Range(0.3f, 0.99f)]
        [SerializeField] private float _minSurfaceNormalY = 0.72f;

        [Tooltip("【3Dモデル表面吸着モード】観客が中心として向く基準ローカル座標")]
        [SerializeField] private Vector3 _surfaceLookCenterLocal = Vector3.zero;

        [Header("3. 客席の段数・密度・途切れ（ブロック分割）")]
        [Tooltip("客席の段数（Rows）")]
        [Range(1, 50)]
        [SerializeField] private int _rowCount = 10;

        [Tooltip("1段あたりの奥行き（m）")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _rowDepth = 0.95f;

        [Tooltip("1段あたりの高さ・段差（m）")]
        [Range(0.0f, 2.0f)]
        [SerializeField] private float _rowRise = 0.52f;

        [Tooltip("横方向の観客同士の間隔（m）")]
        [Range(0.45f, 2.5f)]
        [SerializeField] private float _seatSpacing = 0.75f;

        [Tooltip("前後の列で観客の位置を互い違い（千鳥配置）にして見通しを良くするか")]
        [SerializeField] private bool _staggerRows = true;

        [Tooltip("立ち位置の微細なランダム揺らぎ（m）。CGっぽい完璧な整列感を和らげます")]
        [Range(0f, 0.35f)]
        [SerializeField] private float _positionJitter = 0.08f;

        [Tooltip("空席率（0で満席、0.2で20%がランダムに空席になり自然な密度になります）")]
        [Range(0f, 0.85f)]
        [SerializeField] private float _emptySeatRatio = 0.12f;

        [Tooltip("客席のブロック（セクター）分割数。ブロック間に階段通路を作ったり、特定ブロックを丸ごと途切れさせられます")]
        [Range(1, 24)]
        [SerializeField] private int _sectorCount = 8;

        [Tooltip("ブロック間の階段・通路の隙間幅（m）。途切れを強調したい場合は大きくします")]
        [Range(0f, 15f)]
        [SerializeField] private float _aisleGapMeters = 2.2f;

        [Tooltip("各セクター（最大24ブロック）のON/OFFビットマスク。途切れさせたいブロックのビットを0にします")]
        [SerializeField] private int _sectorActiveMask = 0xFFFFFF;

        [Tooltip("仮の階段状スタジアム客席（ひな壇メッシュ）も同時に生成するか（本番モデル配置時はOFFにできます）")]
        [SerializeField] private bool _generateStandMesh = true;

        [Tooltip("乱数シード値")]
        [SerializeField] private int _randomSeed = 2026;

        // =====================================================================
        // 4. 視線追従（上空→着地）＆ 歓声アニメーション設定
        // =====================================================================
        [Header("4. 視線追従（上空→着地）& 歓声モーション")]
        [Tooltip("視線追従ターゲットの動作モード")]
        [SerializeField] private TargetTrackingMode _trackingMode = TargetTrackingMode.SkyDiveSimulation;

        [Tooltip("追従対象のTransform（FollowTransformモード時）")]
        [SerializeField] private Transform _lookAtTarget;

        [Tooltip("実行時に自動でPlayer（PlayerManagerまたはPlayerタグ）を検索して追従するか")]
        [SerializeField] private bool _autoFindPlayerAtRuntime = true;

        [Tooltip("【シミュレーション用】降下開始の高度（m）")]
        [Min(5f)]
        [SerializeField] private float _simStartHeight = 85f;

        [Tooltip("【シミュレーション用】上空アプローチ時の水平オフセット（斜め上空から飛来する演出用）")]
        [SerializeField] private Vector2 _simApproachOffsetXZ = new Vector2(0f, -28f);

        [Tooltip("【シミュレーション用】着地地点のローカル座標")]
        [SerializeField] private Vector3 _simLandingPointLocal = new Vector3(0f, 0f, 0f);

        [Tooltip("【シミュレーション用】上空から着地までの1サイクル所要時間（秒）")]
        [Range(2f, 20f)]
        [SerializeField] private float _simCycleDuration = 6.5f;

        [Tooltip("【手動スライダー確認用】0% = 上空開始位置 ～ 100% = スタジアム着地完了")]
        [Range(0f, 1f)]
        [SerializeField] private float _manualLandingProgress = 0.35f;

        [Tooltip("エディタ（非再生時）でも降下シミュレーションと観客アニメーションをリアルタイムプレビューするか")]
        [SerializeField] private bool _previewInEditMode = true;

        [Tooltip("Scene/Gameビューに降下ターゲットの位置を示すプレビューマーカー球を表示するか")]
        [SerializeField] private bool _showTargetMarkerMesh = true;

        [Tooltip("視線・体幹の追従強度（0で正面固定、1でターゲットを全力追従）")]
        [Range(0f, 1f)]
        [SerializeField] private float _lookAtWeight = 1.0f;

        [Tooltip("観客ごとの振り向き反応時間のバラつき（0で全員同時、高いほど素早い人と遅れる人が混在）")]
        [Range(0f, 1f)]
        [SerializeField] private float _reactionLagWeight = 0.68f;

        [Tooltip("遅延ターゲットの追従遅れ時間（秒）")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _reactionLagSeconds = 0.45f;

        [Tooltip("基本の盛り上がり・歓声強度（0=静観 ～ 1=大歓声）")]
        [Range(0f, 1f)]
        [SerializeField] private float _baseExcitement = 0.35f;

        [Tooltip("キャラクターが着地地点に近づいた際、自動的に歓声（ジャンプ・腕振り）を最大まで盛り上げるか")]
        [SerializeField] private bool _boostExcitementOnLanding = true;

        [Tooltip("アニメーション全体の再生速度")]
        [Range(0f, 2.5f)]
        [SerializeField] private float _animSpeed = 1.0f;

        [Tooltip("ジャンプする観客の最大跳躍高（m）")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _jumpHeight = 0.22f;

        [Tooltip("ジャンプする観客の割合")]
        [Range(0f, 1f)]
        [SerializeField] private float _jumpRatio = 0.55f;

        [Tooltip("体の左右リズム揺れ幅")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _swayAmount = 0.10f;

        [Tooltip("腕振りの大きさ")]
        [Range(0f, 1.5f)]
        [SerializeField] private float _armWaveAmount = 0.85f;

        [Tooltip("スタジアムを一周するウェーブ（Mexican Wave）の強さ（0でOFF）")]
        [Range(0f, 1f)]
        [SerializeField] private float _waveIntensity = 0.18f;

        [Tooltip("スタジアムウェーブの周回速度")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float _waveSpeed = 0.22f;

        [Header("5. 内部参照 & 統計情報")]
        [SerializeField] private Shader _crowdShader;
        [SerializeField] private Material _sharedCrowdMaterial;
        [SerializeField] private Material _sharedStandMaterial;
        [SerializeField] private int _generatedSpectatorCount;
        [SerializeField] private int _generatedChunkCount;
        [SerializeField] private int _generatedTriangleCount;

        // Cached runtime state (0 GC Alloc)
        private const string CrowdRootName = "Generated_StadiumCrowd_Root";
        private const string MarkerObjectName = "Preview_SkyDiveTargetMarker";
        private const int MaxSpectatorsPerChunk = 1500;

        private readonly List<MeshRenderer> _chunkRenderers = new List<MeshRenderer>();
        private readonly List<SpectatorPlacementPoint> _lastPlacementPoints = new List<SpectatorPlacementPoint>();
        private MaterialPropertyBlock _mpb;

        private Vector3 _currentTargetWorldPos;
        private Vector3 _delayedTargetWorldPos;
        private float _currentDynamicExcitement;
        private bool _delayedPosInitialized;
        private Transform _markerTransform;
        private double _lastEditorTime;

        // Shader Property IDs
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

        private static readonly int OutfitColorAId = Shader.PropertyToID("_OutfitColorA");
        private static readonly int OutfitColorBId = Shader.PropertyToID("_OutfitColorB");
        private static readonly int OutfitColorCId = Shader.PropertyToID("_OutfitColorC");
        private static readonly int OutfitColorDId = Shader.PropertyToID("_OutfitColorD");
        private static readonly int PantsColorId = Shader.PropertyToID("_PantsColor");
        private static readonly int SkinColorId = Shader.PropertyToID("_SkinColor");
        private static readonly int HairHatColorId = Shader.PropertyToID("_HairHatColor");
        private static readonly int AccentEmissionColorId = Shader.PropertyToID("_AccentEmissionColor");
        private static readonly int AccentEmissionStrengthId = Shader.PropertyToID("_AccentEmissionStrength");

        public int GeneratedSpectatorCount => _generatedSpectatorCount;
        public int GeneratedChunkCount => _generatedChunkCount;
        public int GeneratedTriangleCount => _generatedTriangleCount;
        public int SectorCount => _sectorCount;
        public int SectorActiveMask
        {
            get => _sectorActiveMask;
            set => _sectorActiveMask = value;
        }
        public List<Vector3> PathPoints => _pathPoints;
        public IReadOnlyList<SpectatorPlacementPoint> LastPlacementPoints => _lastPlacementPoints;

        private void OnEnable()
        {
            RefreshChunkRenderersList();
            InitializeTargetPositions();

            if (_generatedSpectatorCount == 0 && _chunkRenderers.Count == 0)
            {
                GenerateCrowd();
            }
            else
            {
                ApplyMaterialSettingsToAllChunks();
            }

#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            _lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

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
                TickTrackingAndShader(Time.deltaTime, Time.time);
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

            TickTrackingAndShader(dt, (float)now);

            // Force scene/game view repaint when previewing in editor
            EditorApplication.QueuePlayerLoopUpdate();
        }
#endif

        /// <summary>
        /// 毎フレームの視線追従座標の更新とMaterialPropertyBlockの転送（0 GC Alloc）
        /// </summary>
        private void TickTrackingAndShader(float deltaTime, float currentTime)
        {
            if (_chunkRenderers.Count == 0)
            {
                RefreshChunkRenderersList();
                if (_chunkRenderers.Count == 0) return;
            }

            float landingExcitementBoost = 0f;

            switch (_trackingMode)
            {
                case TargetTrackingMode.SkyDiveSimulation:
                {
                    float cycle = Mathf.Max(_simCycleDuration, 1.0f);
                    float phase = Mathf.Repeat(currentTime, cycle) / cycle;

                    // 0.00 .. 0.65: Descending from high sky to stadium landing point
                    // 0.65 .. 0.90: Landed on stadium floor (crowd cheers wildly!)
                    // 0.90 .. 1.00: Smooth reset back to sky
                    float descentT;
                    if (phase < 0.65f)
                    {
                        float norm = phase / 0.65f;
                        // Smooth ease-out landing curve
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

            // Convert world positions into Generator local space (which matches chunk local space)
            Vector3 targetOS = transform.InverseTransformPoint(_currentTargetWorldPos);
            Vector3 delayedOS = transform.InverseTransformPoint(_delayedTargetWorldPos);

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }

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

            // Fallback: search by type name without hard-coupling to Player scripts
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
        // カラーテーマ適用
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
                    _outfitColorD = new Color(0.95f, 0.72f, 0.15f, 1f);
                    _pantsColor = new Color(0.14f, 0.16f, 0.21f, 1f);
                    _hairHatColor = new Color(0.18f, 0.20f, 0.25f, 1f);
                    _accentEmissionColor = new Color(0.95f, 0.85f, 0.25f, 1f);
                    _accentEmissionStrength = (_spectatorStyle == SpectatorMeshFactory.SpectatorStyle.CyberVisor) ? 1.2f : 0.0f;
                    break;

                case CrowdColorTheme.TeamRivalry:
                    _outfitColorA = new Color(0.10f, 0.35f, 0.92f, 1f);
                    _outfitColorB = new Color(0.92f, 0.95f, 0.98f, 1f);
                    _outfitColorC = new Color(0.90f, 0.14f, 0.18f, 1f);
                    _outfitColorD = new Color(0.98f, 0.80f, 0.10f, 1f);
                    _pantsColor = new Color(0.12f, 0.13f, 0.16f, 1f);
                    _hairHatColor = new Color(0.14f, 0.16f, 0.22f, 1f);
                    _accentEmissionColor = new Color(0.20f, 0.65f, 1.0f, 1f);
                    _accentEmissionStrength = (_spectatorStyle == SpectatorMeshFactory.SpectatorStyle.CyberVisor) ? 1.4f : 0.15f;
                    break;

                case CrowdColorTheme.SubtleSilhouette:
                    _outfitColorA = new Color(0.22f, 0.26f, 0.34f, 1f);
                    _outfitColorB = new Color(0.30f, 0.26f, 0.30f, 1f);
                    _outfitColorC = new Color(0.36f, 0.40f, 0.46f, 1f);
                    _outfitColorD = new Color(0.26f, 0.34f, 0.38f, 1f);
                    _pantsColor = new Color(0.10f, 0.11f, 0.14f, 1f);
                    _hairHatColor = new Color(0.12f, 0.13f, 0.16f, 1f);
                    _accentEmissionColor = new Color(0.4f, 0.6f, 0.8f, 1f);
                    _accentEmissionStrength = 0.0f;
                    break;

                case CrowdColorTheme.CyberNight:
                    _outfitColorA = new Color(0.12f, 0.20f, 0.36f, 1f);
                    _outfitColorB = new Color(0.24f, 0.14f, 0.36f, 1f);
                    _outfitColorC = new Color(0.14f, 0.32f, 0.38f, 1f);
                    _outfitColorD = new Color(0.28f, 0.30f, 0.38f, 1f);
                    _pantsColor = new Color(0.08f, 0.09f, 0.12f, 1f);
                    _hairHatColor = new Color(0.10f, 0.12f, 0.16f, 1f);
                    _accentEmissionColor = new Color(0.15f, 0.85f, 1.0f, 1f);
                    _accentEmissionStrength = 1.6f;
                    break;
            }

            ApplyMaterialSettingsToAllChunks();
        }

        public void ApplyMaterialSettingsToAllChunks()
        {
            EnsureMaterials();
            if (_sharedCrowdMaterial != null)
            {
                _sharedCrowdMaterial.SetColor(OutfitColorAId, _outfitColorA);
                _sharedCrowdMaterial.SetColor(OutfitColorBId, _outfitColorB);
                _sharedCrowdMaterial.SetColor(OutfitColorCId, _outfitColorC);
                _sharedCrowdMaterial.SetColor(OutfitColorDId, _outfitColorD);
                _sharedCrowdMaterial.SetColor(PantsColorId, _pantsColor);
                _sharedCrowdMaterial.SetColor(SkinColorId, _skinColor);
                _sharedCrowdMaterial.SetColor(HairHatColorId, _hairHatColor);
                _sharedCrowdMaterial.SetColor(AccentEmissionColorId, _accentEmissionColor);
                _sharedCrowdMaterial.SetFloat(AccentEmissionStrengthId, _accentEmissionStrength);

                _sharedCrowdMaterial.SetFloat(LookAtWeightId, _lookAtWeight);
                _sharedCrowdMaterial.SetFloat(ReactionLagWeightId, _reactionLagWeight);
                _sharedCrowdMaterial.SetFloat(AnimSpeedId, _animSpeed);
                _sharedCrowdMaterial.SetFloat(ExcitementId, _baseExcitement);
                _sharedCrowdMaterial.SetFloat(JumpHeightId, _jumpHeight);
                _sharedCrowdMaterial.SetFloat(JumpRatioId, _jumpRatio);
                _sharedCrowdMaterial.SetFloat(SwayAmountId, _swayAmount);
                _sharedCrowdMaterial.SetFloat(ArmWaveAmountId, _armWaveAmount);
                _sharedCrowdMaterial.SetFloat(HeightVariationId, _heightVariation);
                _sharedCrowdMaterial.SetFloat(WaveIntensityId, _waveIntensity);
                _sharedCrowdMaterial.SetFloat(WaveSpeedId, _waveSpeed);
            }

            TickTrackingAndShader(0.016f, Application.isPlaying ? Time.time : 0f);
        }

        // =====================================================================
        // メイン生成処理（配置ポイント算出 ＋ チャンク結合メッシュ生成）
        // =====================================================================
        public void GenerateCrowd()
        {
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

            // Collect active exclusion zones (child components)
            CrowdExclusionZone[] exclusionZones = GetComponentsInChildren<CrowdExclusionZone>( false);

            // 1. Calculate all spectator placement points according to current PlacementMode
            _lastPlacementPoints.Clear();
            System.Random rng = new System.Random(_randomSeed);

            switch (_placementMode)
            {
                case PlacementMode.CircularBowl:
                    GenerateCircularBowlPoints(rng, exclusionZones, _lastPlacementPoints);
                    break;
                case PlacementMode.LinearStand:
                    GenerateLinearStandPoints(rng, exclusionZones, _lastPlacementPoints);
                    break;
                case PlacementMode.CustomPathStand:
                    GenerateCustomPathPoints(rng, exclusionZones, _lastPlacementPoints);
                    break;
                case PlacementMode.MeshSurfaceScatter:
                    GenerateMeshSurfacePoints(rng, exclusionZones, _lastPlacementPoints);
                    break;
            }

            // 2. Create Root Container
            GameObject rootObj = new GameObject(CrowdRootName);
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            rootObj.transform.localScale = Vector3.one;

            // 3. Build combined crowd mesh chunks
            SpectatorMeshFactory.MeshTemplateData template = SpectatorMeshFactory.CreateTemplate(_spectatorStyle, _customSpectatorMesh);
            BuildCrowdChunks(rootObj.transform, template, _lastPlacementPoints);

            // 4. Optionally build stadium bleacher tier mesh
            if (_generateStandMesh && _placementMode != PlacementMode.MeshSurfaceScatter)
            {
                BuildStandBleacherMesh(rootObj.transform);
            }

            // 5. Update marker & tracking immediately
            UpdateTargetMarkerVisual();
            TickTrackingAndShader(0.016f, 0f);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void ClearGeneratedCrowd()
        {
            _chunkRenderers.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && (child.name == CrowdRootName || child.name == MarkerObjectName))
                {
                    DestroySafe(child.gameObject);
                }
            }

            _markerTransform = null;
            _generatedSpectatorCount = 0;
            _generatedChunkCount = 0;
            _generatedTriangleCount = 0;
        }

        /// <summary>
        /// 除外ゾーン（途切れエリア）の子オブジェクトを1つ追加します。
        /// </summary>
        public CrowdExclusionZone AddExclusionZone()
        {
            GameObject zoneObj = new GameObject($"CrowdExclusionZone_{transform.childCount + 1}");
            zoneObj.transform.SetParent(transform, false);

            // Default position: on the front edge of the stadium stand so user immediately sees it cut a gap
            float defaultZ = (_placementMode == PlacementMode.CircularBowl) ? (_innerRadiusZ + (_rowCount * _rowDepth * 0.4f)) : 0f;
            zoneObj.transform.localPosition = new Vector3(0f, _rowCount * _rowRise * 0.4f, defaultZ);

            CrowdExclusionZone zone = zoneObj.AddComponent<CrowdExclusionZone>();
            zone.size = new Vector3(14f, 18f, 16f);
            return zone;
        }

        public bool IsSectorActive(int sectorIndex)
        {
            if (sectorIndex < 0 || sectorIndex >= 24) return true;
            return (_sectorActiveMask & (1 << sectorIndex)) != 0;
        }

        public void SetSectorActive(int sectorIndex, bool active)
        {
            if (sectorIndex < 0 || sectorIndex >= 24) return;
            if (active)
                _sectorActiveMask |= (1 << sectorIndex);
            else
                _sectorActiveMask &= ~(1 << sectorIndex);
        }

        // =====================================================================
        // 配置モード別の座標算出ロジック
        // =====================================================================
        private void GenerateCircularBowlPoints(
            System.Random rng,
            CrowdExclusionZone[] exclusionZones,
            List<SpectatorPlacementPoint> outPoints)
        {
            int rows = Mathf.Max(1, _rowCount);
            int sectors = Mathf.Clamp(_sectorCount, 1, 24);
            float totalArcRad = Mathf.Clamp(_arcAngleDeg, 5f, 360f) * Mathf.Deg2Rad;
            float startRad = _startAngleDeg * Mathf.Deg2Rad;
            float halfStraight = _straightTrackLength * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                float rowNorm = rows > 1 ? r / (float)(rows - 1) : 0f;
                float rx = _innerRadiusX + r * _rowDepth;
                float rz = _innerRadiusZ + r * _rowDepth;
                float y = r * _rowRise;

                // Approximate perimeter for this row
                float avgRadius = Mathf.Max(1f, (rx + rz) * 0.5f);
                float arcPerimeter = avgRadius * totalArcRad + _straightTrackLength * (totalArcRad / Mathf.PI);
                int totalSeatsInRow = Mathf.Max(4, Mathf.RoundToInt(arcPerimeter / Mathf.Max(0.35f, _seatSpacing)));

                float staggerOffset = (_staggerRows && (r % 2 == 1)) ? 0.5f : 0f;

                for (int s = 0; s < totalSeatsInRow; s++)
                {
                    float u = (s + staggerOffset) / totalSeatsInRow;
                    if (u >= 1f) continue;

                    // Determine sector index (0 .. sectors-1)
                    float sectorFloat = u * sectors;
                    int sectorIdx = Mathf.Clamp(Mathf.FloorToInt(sectorFloat), 0, sectors - 1);
                    if (!IsSectorActive(sectorIdx))
                    {
                        continue; // This sector is turned OFF (interrupted stand!)
                    }

                    // Check aisle gap between sectors
                    if (sectors > 1 && _aisleGapMeters > 0.01f)
                    {
                        float sectorFrac = sectorFloat - sectorIdx;
                        float sectorArcLen = arcPerimeter / sectors;
                        float distFromSectorEdge = Mathf.Min(sectorFrac, 1f - sectorFrac) * sectorArcLen;
                        if (distFromSectorEdge < _aisleGapMeters * 0.5f)
                        {
                            continue;
                        }
                    }

                    // Empty seat random skip
                    if (_emptySeatRatio > 0.001f && NextFloat(rng) < _emptySeatRatio)
                    {
                        continue;
                    }

                    float angle = startRad + u * totalArcRad;
                    Vector3 basePos = EvaluateOvalStadiumPoint(angle, rx, rz, halfStraight, y, out Vector3 inwardDir);

                    // Subtle natural position jitter
                    Vector3 rightDir = new Vector3(inwardDir.z, 0f, -inwardDir.x);
                    float jitterX = (NextFloat(rng) - 0.5f) * 2f * _positionJitter;
                    float jitterZ = (NextFloat(rng) - 0.5f) * 2f * (_positionJitter * 0.6f);
                    Vector3 finalLocalPos = basePos + rightDir * jitterX + inwardDir * jitterZ;

                    if (IsInsideAnyExclusionZone(finalLocalPos, exclusionZones))
                    {
                        continue;
                    }

                    float yaw = Mathf.Atan2(inwardDir.x, inwardDir.z);

                    outPoints.Add(new SpectatorPlacementPoint
                    {
                        localPosition = finalLocalPos,
                        baseYawRad = yaw,
                        seedA = NextFloat(rng),
                        seedB = NextFloat(rng),
                        stadiumAngle01 = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f),
                        rowNormalized01 = rowNorm
                    });
                }
            }
        }

        private static Vector3 EvaluateOvalStadiumPoint(
            float angleRad,
            float rx,
            float rz,
            float halfStraight,
            float y,
            out Vector3 inwardNormalXZ)
        {
            float s = Mathf.Sin(angleRad);
            float c = Mathf.Cos(angleRad);

            float x = s * rx;
            float z = c * rz;

            // Stretch along Z if straightTrackLength > 0 (creates an oval racing stadium!)
            if (halfStraight > 0.001f)
            {
                if (c >= 0f) z += halfStraight;
                else z -= halfStraight;
            }

            // Inward direction points toward the center line (0, y, clampedZ)
            float centerZ = Mathf.Clamp(z, -halfStraight, halfStraight);
            Vector3 toCenter = new Vector3(-x / Mathf.Max(rx, 0.01f), 0f, (centerZ - z) / Mathf.Max(rz, 0.01f));
            if (toCenter.sqrMagnitude < 0.0001f)
            {
                inwardNormalXZ = new Vector3(-s, 0f, -c).normalized;
            }
            else
            {
                inwardNormalXZ = toCenter.normalized;
            }

            return new Vector3(x, y, z);
        }

        private void GenerateLinearStandPoints(
            System.Random rng,
            CrowdExclusionZone[] exclusionZones,
            List<SpectatorPlacementPoint> outPoints)
        {
            int rows = Mathf.Max(1, _rowCount);
            int sectors = Mathf.Clamp(_sectorCount, 1, 24);
            float totalWidth = Mathf.Max(2f, _linearWidth);
            int cols = Mathf.Max(2, Mathf.RoundToInt(totalWidth / Mathf.Max(0.35f, _seatSpacing)));

            for (int r = 0; r < rows; r++)
            {
                float rowNorm = rows > 1 ? r / (float)(rows - 1) : 0f;
                float z = r * _rowDepth;
                float y = r * _rowRise;
                float stagger = (_staggerRows && (r % 2 == 1)) ? 0.5f : 0f;

                for (int c = 0; c < cols; c++)
                {
                    float u = (c + stagger) / cols;
                    if (u >= 1f) continue;

                    float sectorFloat = u * sectors;
                    int sectorIdx = Mathf.Clamp(Mathf.FloorToInt(sectorFloat), 0, sectors - 1);
                    if (!IsSectorActive(sectorIdx)) continue;

                    if (sectors > 1 && _aisleGapMeters > 0.01f)
                    {
                        float sectorFrac = sectorFloat - sectorIdx;
                        float distFromEdge = Mathf.Min(sectorFrac, 1f - sectorFrac) * (totalWidth / sectors);
                        if (distFromEdge < _aisleGapMeters * 0.5f) continue;
                    }

                    if (_emptySeatRatio > 0.001f && NextFloat(rng) < _emptySeatRatio) continue;

                    float x = (u - 0.5f) * totalWidth + (NextFloat(rng) - 0.5f) * 2f * _positionJitter;
                    float finalZ = z + (NextFloat(rng) - 0.5f) * 2f * (_positionJitter * 0.6f);
                    Vector3 localPos = new Vector3(x, y, finalZ);

                    if (IsInsideAnyExclusionZone(localPos, exclusionZones)) continue;

                    // Linear stand faces -Z (toward origin/front) by default: yaw = PI
                    outPoints.Add(new SpectatorPlacementPoint
                    {
                        localPosition = localPos,
                        baseYawRad = Mathf.PI,
                        seedA = NextFloat(rng),
                        seedB = NextFloat(rng),
                        stadiumAngle01 = u,
                        rowNormalized01 = rowNorm
                    });
                }
            }
        }

        private void GenerateCustomPathPoints(
            System.Random rng,
            CrowdExclusionZone[] exclusionZones,
            List<SpectatorPlacementPoint> outPoints)
        {
            if (_pathPoints == null || _pathPoints.Count < 2) return;

            int segCount = _closePathLoop ? _pathPoints.Count : (_pathPoints.Count - 1);
            int rows = Mathf.Max(1, _rowCount);

            // Compute total path length for wave normalization
            float totalPathLen = 0f;
            for (int seg = 0; seg < segCount; seg++)
            {
                Vector3 p0 = _pathPoints[seg];
                Vector3 p1 = _pathPoints[(seg + 1) % _pathPoints.Count];
                totalPathLen += Vector3.Distance(p0, p1);
            }
            totalPathLen = Mathf.Max(totalPathLen, 0.1f);

            float accumLen = 0f;

            for (int seg = 0; seg < segCount; seg++)
            {
                Vector3 p0 = _pathPoints[seg];
                Vector3 p1 = _pathPoints[(seg + 1) % _pathPoints.Count];
                Vector3 segVec = p1 - p0;
                float segLen = segVec.magnitude;
                if (segLen < 0.2f) continue;

                // Allow toggling individual path segments via Sector Mask
                if (seg < 24 && !IsSectorActive(seg))
                {
                    accumLen += segLen;
                    continue;
                }

                Vector3 tangent = segVec / segLen;
                // Right-hand side of tangent in XZ plane is the "back" of the bleachers; inward is left (-tangent.z, 0, tangent.x)
                Vector3 inwardXZ = new Vector3(-tangent.z, 0f, tangent.x).normalized;
                if (inwardXZ.sqrMagnitude < 0.001f) inwardXZ = Vector3.back;
                Vector3 outwardXZ = -inwardXZ;

                float baseYaw = Mathf.Atan2(inwardXZ.x, inwardXZ.z);
                int cols = Mathf.Max(1, Mathf.RoundToInt(segLen / Mathf.Max(0.35f, _seatSpacing)));

                for (int r = 0; r < rows; r++)
                {
                    float rowNorm = rows > 1 ? r / (float)(rows - 1) : 0f;
                    float stagger = (_staggerRows && (r % 2 == 1)) ? 0.5f : 0f;

                    for (int c = 0; c < cols; c++)
                    {
                        float t = (c + stagger) / cols;
                        if (t >= 1f) continue;

                        if (_aisleGapMeters > 0.01f)
                        {
                            float distFromEnd = Mathf.Min(t, 1f - t) * segLen;
                            if (distFromEnd < _aisleGapMeters * 0.5f) continue;
                        }

                        if (_emptySeatRatio > 0.001f && NextFloat(rng) < _emptySeatRatio) continue;

                        Vector3 baseLinePos = Vector3.Lerp(p0, p1, t);
                        Vector3 localPos = baseLinePos
                            + outwardXZ * (r * _rowDepth + (NextFloat(rng) - 0.5f) * _positionJitter)
                            + tangent * ((NextFloat(rng) - 0.5f) * _positionJitter)
                            + Vector3.up * (r * _rowRise);

                        if (IsInsideAnyExclusionZone(localPos, exclusionZones)) continue;

                        float globalU = Mathf.Clamp01((accumLen + t * segLen) / totalPathLen);

                        outPoints.Add(new SpectatorPlacementPoint
                        {
                            localPosition = localPos,
                            baseYawRad = baseYaw,
                            seedA = NextFloat(rng),
                            seedB = NextFloat(rng),
                            stadiumAngle01 = globalU,
                            rowNormalized01 = rowNorm
                        });
                    }
                }

                accumLen += segLen;
            }
        }

        private void GenerateMeshSurfacePoints(
            System.Random rng,
            CrowdExclusionZone[] exclusionZones,
            List<SpectatorPlacementPoint> outPoints)
        {
            List<MeshFilter> filters = new List<MeshFilter>();
            if (_targetSurfaceRoot != null)
            {
                _targetSurfaceRoot.GetComponentsInChildren(false, filters);
            }
            if (_targetMeshFilters != null)
            {
                for (int i = 0; i < _targetMeshFilters.Count; i++)
                {
                    MeshFilter mf = _targetMeshFilters[i];
                    if (mf != null && !filters.Contains(mf))
                    {
                        filters.Add(mf);
                    }
                }
            }

            // Exclude our own generated crowd meshes
            for (int i = filters.Count - 1; i >= 0; i--)
            {
                if (filters[i] == null || filters[i].transform.IsChildOf(transform))
                {
                    filters.RemoveAt(i);
                }
            }

            if (filters.Count == 0) return;

            float areaPerSeat = Mathf.Max(0.25f, _seatSpacing * _seatSpacing);
            Vector3 lookCenterWorld = transform.TransformPoint(_surfaceLookCenterLocal);

            for (int f = 0; f < filters.Count; f++)
            {
                MeshFilter mf = filters[f];
                Mesh m = mf.sharedMesh;
                if (m == null) continue;

                Vector3[] verts = m.vertices;
                int[] tris = m.triangles;
                Transform meshTr = mf.transform;

                float accumArea = 0f;

                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 w0 = meshTr.TransformPoint(verts[tris[t]]);
                    Vector3 w1 = meshTr.TransformPoint(verts[tris[t + 1]]);
                    Vector3 w2 = meshTr.TransformPoint(verts[tris[t + 2]]);

                    Vector3 cross = Vector3.Cross(w1 - w0, w2 - w0);
                    float doubleArea = cross.magnitude;
                    if (doubleArea < 0.0001f) continue;

                    Vector3 worldNormal = cross / doubleArea;
                    if (worldNormal.y < _minSurfaceNormalY) continue;

                    float triArea = doubleArea * 0.5f;
                    accumArea += triArea;

                    int spawnCount = Mathf.FloorToInt(accumArea / areaPerSeat);
                    if (spawnCount <= 0) continue;
                    accumArea -= spawnCount * areaPerSeat;

                    for (int s = 0; s < spawnCount; s++)
                    {
                        if (_emptySeatRatio > 0.001f && NextFloat(rng) < _emptySeatRatio) continue;

                        // Uniform random point in triangle
                        float r1 = Mathf.Sqrt(NextFloat(rng));
                        float r2 = NextFloat(rng);
                        Vector3 worldPt = (1f - r1) * w0 + (r1 * (1f - r2)) * w1 + (r1 * r2) * w2;
                        Vector3 localPos = transform.InverseTransformPoint(worldPt);

                        if (IsInsideAnyExclusionZone(localPos, exclusionZones)) continue;

                        Vector3 toCenterLocal = _surfaceLookCenterLocal - localPos;
                        float yaw = Mathf.Atan2(toCenterLocal.x, toCenterLocal.z);
                        float angle01 = Mathf.Repeat(Mathf.Atan2(localPos.x - _surfaceLookCenterLocal.x, localPos.z - _surfaceLookCenterLocal.z) / (Mathf.PI * 2f), 1f);
                        float distNorm = Mathf.Clamp01(new Vector2(toCenterLocal.x, toCenterLocal.z).magnitude / 80f);

                        outPoints.Add(new SpectatorPlacementPoint
                        {
                            localPosition = localPos,
                            baseYawRad = yaw,
                            seedA = NextFloat(rng),
                            seedB = NextFloat(rng),
                            stadiumAngle01 = angle01,
                            rowNormalized01 = distNorm
                        });
                    }
                }
            }
        }

        private bool IsInsideAnyExclusionZone(Vector3 generatorLocalPos, CrowdExclusionZone[] zones)
        {
            if (zones == null || zones.Length == 0) return false;
            Vector3 worldPos = transform.TransformPoint(generatorLocalPos);
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsWorldPoint(worldPos))
                {
                    return true;
                }
            }
            return false;
        }

        // =====================================================================
        // チャンク結合メッシュの構築
        // =====================================================================
        private void BuildCrowdChunks(
            Transform parentRoot,
            SpectatorMeshFactory.MeshTemplateData template,
            List<SpectatorPlacementPoint> points)
        {
            _generatedSpectatorCount = points.Count;
            _generatedChunkCount = 0;
            _generatedTriangleCount = 0;

            if (points.Count == 0 || template.vertices == null || template.vertices.Length == 0)
            {
                return;
            }

            int tplVertCount = template.vertices.Length;
            int tplTriCount = template.triangles.Length;
            int chunkCapacity = Mathf.Max(64, MaxSpectatorsPerChunk);
            int totalChunks = Mathf.CeilToInt(points.Count / (float)chunkCapacity);

            for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
            {
                int startIdx = chunkIdx * chunkCapacity;
                int count = Mathf.Min(chunkCapacity, points.Count - startIdx);

                int totalVerts = count * tplVertCount;
                int totalTris = count * tplTriCount;

                Vector3[] vertices = new Vector3[totalVerts];
                Vector3[] normals = new Vector3[totalVerts];
                Color[] colors = new Color[totalVerts];
                Vector4[] uv0 = new Vector4[totalVerts];
                Vector4[] uv1 = new Vector4[totalVerts];
                int[] triangles = new int[totalTris];

                for (int i = 0; i < count; i++)
                {
                    SpectatorPlacementPoint pt = points[startIdx + i];
                    int vBase = i * tplVertCount;
                    int tBase = i * tplTriCount;

                    float s = Mathf.Sin(pt.baseYawRad);
                    float c = Mathf.Cos(pt.baseYawRad);
                    Vector2 rightXZ = new Vector2(c, -s);
                    Vector2 fwdXZ = new Vector2(s, c);

                    Vector4 packedUV0 = new Vector4(pt.localPosition.x, pt.localPosition.y, pt.localPosition.z, pt.baseYawRad);
                    Vector4 packedUV1 = new Vector4(pt.seedA, pt.seedB, pt.stadiumAngle01, pt.rowNormalized01);

                    for (int v = 0; v < tplVertCount; v++)
                    {
                        Vector3 tv = template.vertices[v] * _spectatorScale;
                        Vector3 tn = template.normals[v];

                        // Rotate template vertex from spectator local space (+X right, +Z forward) into chunk space
                        float ox = rightXZ.x * tv.x + fwdXZ.x * tv.z;
                        float oz = rightXZ.y * tv.x + fwdXZ.y * tv.z;

                        float nx = rightXZ.x * tn.x + fwdXZ.x * tn.z;
                        float nz = rightXZ.y * tn.x + fwdXZ.y * tn.z;

                        int dstV = vBase + v;
                        vertices[dstV] = new Vector3(pt.localPosition.x + ox, pt.localPosition.y + tv.y, pt.localPosition.z + oz);
                        normals[dstV] = new Vector3(nx, tn.y, nz).normalized;
                        colors[dstV] = template.colors[v];
                        uv0[dstV] = packedUV0;
                        uv1[dstV] = packedUV1;
                    }

                    for (int t = 0; t < tplTriCount; t++)
                    {
                        triangles[tBase + t] = vBase + template.triangles[t];
                    }
                }

                Mesh chunkMesh = new Mesh();
                chunkMesh.name = $"CrowdChunk_{chunkIdx}_{count}p";
                if (totalVerts > 65000)
                {
                    chunkMesh.indexFormat = IndexFormat.UInt32;
                }

                chunkMesh.vertices = vertices;
                chunkMesh.normals = normals;
                chunkMesh.colors = colors;
                chunkMesh.SetUVs(0, uv0);
                chunkMesh.SetUVs(1, uv1);
                chunkMesh.triangles = triangles;
                chunkMesh.RecalculateBounds();
                // Expand bounds slightly so vertex-animated jumping never gets frustum-culled prematurely
                Bounds b = chunkMesh.bounds;
                b.Expand(new Vector3(2.5f, 3.0f, 2.5f));
                chunkMesh.bounds = b;

                GameObject chunkObj = new GameObject($"CrowdChunk_{chunkIdx:00}");
                chunkObj.transform.SetParent(parentRoot, false);
                chunkObj.layer = gameObject.layer;

                MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
                mf.sharedMesh = chunkMesh;

                MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _sharedCrowdMaterial;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;

                _chunkRenderers.Add(mr);
                _generatedChunkCount++;
                _generatedTriangleCount += totalTris / 3;
            }
        }

        // =====================================================================
        // 仮の階段状スタジアム客席（ひな壇メッシュ）の自動生成
        // =====================================================================
        private void BuildStandBleacherMesh(Transform parentRoot)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            int rows = Mathf.Max(1, _rowCount);
            int sectors = Mathf.Clamp(_sectorCount, 1, 24);

            if (_placementMode == PlacementMode.CircularBowl)
            {
                float totalArcRad = Mathf.Clamp(_arcAngleDeg, 5f, 360f) * Mathf.Deg2Rad;
                float startRad = _startAngleDeg * Mathf.Deg2Rad;
                float halfStraight = _straightTrackLength * 0.5f;
                float avgRadius = (_innerRadiusX + _innerRadiusZ) * 0.5f;
                float arcPerimeter = avgRadius * totalArcRad + _straightTrackLength * (totalArcRad / Mathf.PI);

                for (int sec = 0; sec < sectors; sec++)
                {
                    if (!IsSectorActive(sec)) continue;

                    float secU0 = sec / (float)sectors;
                    float secU1 = (sec + 1) / (float)sectors;

                    if (sectors > 1 && _aisleGapMeters > 0.01f)
                    {
                        float gapU = (_aisleGapMeters * 0.4f) / Mathf.Max(arcPerimeter, 1f);
                        secU0 += gapU;
                        secU1 -= gapU;
                        if (secU1 <= secU0) continue;
                    }

                    int stepsPerSector = Mathf.Clamp(Mathf.CeilToInt((secU1 - secU0) * 64f), 2, 24);
                    for (int s = 0; s < stepsPerSector; s++)
                    {
                        float u0 = Mathf.Lerp(secU0, secU1, s / (float)stepsPerSector);
                        float u1 = Mathf.Lerp(secU0, secU1, (s + 1) / (float)stepsPerSector);
                        float a0 = startRad + u0 * totalArcRad;
                        float a1 = startRad + u1 * totalArcRad;

                        for (int r = 0; r < rows; r++)
                        {
                            float rx0 = _innerRadiusX + (r - 0.45f) * _rowDepth;
                            float rz0 = _innerRadiusZ + (r - 0.45f) * _rowDepth;
                            float rx1 = _innerRadiusX + (r + 0.55f) * _rowDepth;
                            float rz1 = _innerRadiusZ + (r + 0.55f) * _rowDepth;

                            float yBottom = (r == 0) ? -0.35f : (r - 1) * _rowRise;
                            float yTop = r * _rowRise;

                            Vector3 f0 = EvaluateOvalStadiumPoint(a0, rx0, rz0, halfStraight, yBottom, out _);
                            Vector3 f1 = EvaluateOvalStadiumPoint(a1, rx0, rz0, halfStraight, yBottom, out _);
                            Vector3 f0Top = new Vector3(f0.x, yTop, f0.z);
                            Vector3 f1Top = new Vector3(f1.x, yTop, f1.z);

                            Vector3 b0Top = EvaluateOvalStadiumPoint(a0, rx1, rz1, halfStraight, yTop, out _);
                            Vector3 b1Top = EvaluateOvalStadiumPoint(a1, rx1, rz1, halfStraight, yTop, out _);

                            // Front riser face
                            AddQuad(verts, norms, tris, f0, f1, f1Top, f0Top);
                            // Top tread face
                            AddQuad(verts, norms, tris, f0Top, f1Top, b1Top, b0Top);
                        }
                    }
                }
            }
            else if (_placementMode == PlacementMode.LinearStand)
            {
                float totalWidth = Mathf.Max(2f, _linearWidth);
                for (int sec = 0; sec < sectors; sec++)
                {
                    if (!IsSectorActive(sec)) continue;

                    float u0 = sec / (float)sectors;
                    float u1 = (sec + 1) / (float)sectors;
                    if (sectors > 1 && _aisleGapMeters > 0.01f)
                    {
                        float gapU = (_aisleGapMeters * 0.4f) / totalWidth;
                        u0 += gapU;
                        u1 -= gapU;
                        if (u1 <= u0) continue;
                    }

                    float x0 = (u0 - 0.5f) * totalWidth;
                    float x1 = (u1 - 0.5f) * totalWidth;

                    for (int r = 0; r < rows; r++)
                    {
                        float z0 = (r - 0.45f) * _rowDepth;
                        float z1 = (r + 0.55f) * _rowDepth;
                        float yBottom = (r == 0) ? -0.35f : (r - 1) * _rowRise;
                        float yTop = r * _rowRise;

                        AddQuad(verts, norms, tris,
                            new Vector3(x1, yBottom, z0),
                            new Vector3(x0, yBottom, z0),
                            new Vector3(x0, yTop, z0),
                            new Vector3(x1, yTop, z0));

                        AddQuad(verts, norms, tris,
                            new Vector3(x1, yTop, z0),
                            new Vector3(x0, yTop, z0),
                            new Vector3(x0, yTop, z1),
                            new Vector3(x1, yTop, z1));
                    }
                }
            }

            if (verts.Count == 0) return;

            Mesh standMesh = new Mesh();
            standMesh.name = "Generated_StadiumBleachers";
            if (verts.Count > 65000) standMesh.indexFormat = IndexFormat.UInt32;
            standMesh.SetVertices(verts);
            standMesh.SetNormals(norms);
            standMesh.SetTriangles(tris, 0);
            standMesh.RecalculateBounds();

            GameObject standObj = new GameObject("StadiumBleachers_TierMesh");
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
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3)
        {
            int b = verts.Count;
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            if (n.sqrMagnitude < 0.001f) n = Vector3.up;

            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(n);  norms.Add(n);  norms.Add(n);  norms.Add(n);

            tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
        }

        // =====================================================================
        // プレビューマーカー & マテリアル初期化
        // =====================================================================
        private void UpdateTargetMarkerVisual()
        {
            bool shouldShow = _showTargetMarkerMesh && (_trackingMode != TargetTrackingMode.FollowTransform || _lookAtTarget == null);

            if (!shouldShow)
            {
                if (_markerTransform != null)
                {
                    _markerTransform.gameObject.SetActive(false);
                }
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
                    if (mr != null && _sharedCrowdMaterial != null)
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
            if (_crowdShader == null)
            {
                _crowdShader = Shader.Find("AT_Murayama/StadiumCrowd/S_StadiumCrowd3D");
            }

            if (_sharedCrowdMaterial == null && _crowdShader != null)
            {
#if UNITY_EDITOR
                const string matDir = "Assets/Deverop/AT_Murayama/StadiumCrowd/Materials";
                const string crowdMatPath = matDir + "/M_StadiumCrowd3D_Default.mat";
                _sharedCrowdMaterial = AssetDatabase.LoadAssetAtPath<Material>(crowdMatPath);
                if (_sharedCrowdMaterial == null && !Application.isPlaying)
                {
                    if (!AssetDatabase.IsValidFolder(matDir))
                    {
                        System.IO.Directory.CreateDirectory(matDir);
                        AssetDatabase.Refresh();
                    }
                    _sharedCrowdMaterial = new Material(_crowdShader)
                    {
                        name = "M_StadiumCrowd3D_Default",
                        enableInstancing = true
                    };
                    AssetDatabase.CreateAsset(_sharedCrowdMaterial, crowdMatPath);
                }
#endif
                if (_sharedCrowdMaterial == null)
                {
                    _sharedCrowdMaterial = new Material(_crowdShader)
                    {
                        name = "M_StadiumCrowd3D_Runtime"
                    };
                }
            }

            if (_sharedStandMaterial == null)
            {
#if UNITY_EDITOR
                const string matDir = "Assets/Deverop/AT_Murayama/StadiumCrowd/Materials";
                const string standMatPath = matDir + "/M_StadiumBleacherStand.mat";
                _sharedStandMaterial = AssetDatabase.LoadAssetAtPath<Material>(standMatPath);
                if (_sharedStandMaterial == null && !Application.isPlaying)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader != null)
                    {
                        if (!AssetDatabase.IsValidFolder(matDir))
                        {
                            System.IO.Directory.CreateDirectory(matDir);
                            AssetDatabase.Refresh();
                        }
                        _sharedStandMaterial = new Material(litShader)
                        {
                            name = "M_StadiumBleacherStand",
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
                            name = "M_StadiumBleacherStand_Runtime",
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
                if (child != null && child.name.StartsWith("CrowdChunk_"))
                {
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        _chunkRenderers.Add(mr);
                    }
                }
            }
        }

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
            Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.9f);
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
