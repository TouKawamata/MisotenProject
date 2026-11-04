using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AT_Murayama.DayNightCity
{
    /// <summary>
    /// 昼（リアル白亜・高反射ガラス建築）と夜（サイバー発光・オフィス窓・スカイライン）の
    /// 両方の見た目を検証するための、仮のビル群プロシージャル生成ジェネレーター。
    /// ・本実装前のライティング、反射（Reflection Probe / Box Projection）、大気散乱、影描画のテスト用。
    /// ・単一の親オブジェクト配下に生成され、1クリックで完全に残骸ゼロで一括削除できます。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DayNightCityGenerator : MonoBehaviour
    {
        public enum LayoutPattern
        {
            [InspectorName("① 大通りキャニオン (Avenue Canyon / 反射テスト最適)")]
            AvenueCanyon,

            [InspectorName("② 都市グリッド街区 (Urban Grid / 碁盤目配置)")]
            UrbanGrid,

            [InspectorName("③ ランダムクラスター (Random Cluster / 超高層群)")]
            RandomCluster
        }

        public enum CityScalePreset
        {
            [InspectorName("① 大通りキャニオン反射テスト (40棟 / 400m / BoxProjection検証最適)")]
            AvenueCanyon_400m,

            [InspectorName("② 近未来グリッド都市 (80棟 / 800m / 街区レイアウト)")]
            FutureGrid_800m,

            [InspectorName("③ 広大メガロポリス (160棟 / 2000m / 超遠景大気散乱検証)")]
            MegaCity_2000m,

            [InspectorName("④ 高速コース両脇ウォール (60棟 / 600m / 狭小キャニオン)")]
            HighSpeedCorridor_600m
        }

        [Header("都市プリセット")]
        [SerializeField] private CityScalePreset _preset = CityScalePreset.AvenueCanyon_400m;

        [Header("1. 配置パターン & 規模設定")]
        [Tooltip("ビルの配置レイアウト")]
        [SerializeField] private LayoutPattern _layoutPattern = LayoutPattern.AvenueCanyon;

        [Tooltip("生成するビルの棟数")]
        [Range(10, 300)]
        [SerializeField] private int _buildingCount = 42;

        [Tooltip("都市の奥行き・進行方向の長さ（m）")]
        [Min(50f)]
        [SerializeField] private float _cityLength = 400f;

        [Tooltip("都市の横幅・広がり（m）")]
        [Min(30f)]
        [SerializeField] private float _cityWidth = 260f;

        [Tooltip("中央道路・コース用キャニオン幅（m）。左右のビルが向かい合って反射する間隔")]
        [Min(15f)]
        [SerializeField] private float _roadWidth = 42f;

        [Tooltip("開始オフセット距離（手前の空きスペースm）")]
        [Min(0f)]
        [SerializeField] private float _startDistance = 40f;

        [Header("2. ビルの高さ & スケール")]
        [Tooltip("ビルの最小高さ（m）")]
        [Min(20f)]
        [SerializeField] private float _minHeight = 65f;

        [Tooltip("ビルの最大高さ（m）")]
        [Min(40f)]
        [SerializeField] private float _maxHeight = 260f;

        [Tooltip("ビルの最小幅・奥行き（m）")]
        [Min(12f)]
        [SerializeField] private float _minBuildingWidth = 18f;

        [Tooltip("ビルの最大幅・奥行き（m）")]
        [Min(18f)]
        [SerializeField] private float _maxBuildingWidth = 42f;

        [Tooltip("遠景のビルほど超高層（メガタワー）にする比率")]
        [Range(0f, 1f)]
        [SerializeField] private float _distanceHeightBias = 0.45f;

        [Header("3. 建築バリエーション")]
        [Tooltip("段状セットバックやポディウム（基底複合棟）など複合建築形状を生成するか")]
        [SerializeField] private bool _enableComplexArchitectures = true;

        [Tooltip("走行テスト時に衝突しないようコライダーを自動削除するか")]
        [SerializeField] private bool _removeColliders = true;

        [Header("4. 反射テスト & 静的フラグ")]
        [Tooltip("キャニオン中央に検証用リアルタイム Reflection Probe を自動配置するか")]
        [SerializeField] private bool _generateTestReflectionProbe = true;

        [Tooltip("Box Projection（ビルのガラスに周囲ビルが正確に映り込む投影）を有効化するか")]
        [SerializeField] private bool _enableBoxProjection = true;

        [Tooltip("生成ビルに Static（Contribute GI / Batching Static / Reflection Probe）を設定するか")]
        [SerializeField] private bool _setStaticFlags = true;

        [Header("5. 昼夜プレビュー & ライティング連動")]
        [Tooltip("昼夜ブレンド係数（0.0=正午・白亜リアル, 0.5=夕暮れ, 1.0=深夜・サイバー発光）")]
        [Range(0f, 1f)]
        [SerializeField] private float _previewNightFactor = 0.0f;

        [Tooltip("シーン内の主 Directional Light の角度と照度を NightFactor に自動連動させるか")]
        [SerializeField] private bool _syncDirectionalLight = false;

        [Header("6. マテリアル・参照設定")]
        [Tooltip("外壁シェーダー（未指定時は AT_Murayama/DayNightCity/RealisticCityBuilding を自動検索）")]
        [SerializeField] private Shader _customBuildingShader;

        [Tooltip("テスト用外壁マテリアル配列（空の場合は自動生成）")]
        [SerializeField] private Material[] _presetMaterials;

        [Tooltip("配置の基準トランスフォーム（未指定時は MainCamera または このオブジェクト）")]
        [SerializeField] private Transform _referenceOrigin;

        [Tooltip("ランダムシード値")]
        [SerializeField] private int _randomSeed = 20261104;

        [Header("生成済みルートオブジェクト（管理用）")]
        [SerializeField] private GameObject _generatedCityRoot;

        private const string RootGameObjectNamePrefix = "--- Temporary DayNight City ---";

        public bool HasGeneratedCity => _generatedCityRoot != null;
        public int CurrentBuildingCount => _buildingCount;
        public float CityLength => _cityLength;
        public float PreviewNightFactor => _previewNightFactor;

        /// <summary>
        /// プリセットを適用してパラメータを一括設定します。
        /// </summary>
        public void ApplyPreset(CityScalePreset preset)
        {
            _preset = preset;
            switch (preset)
            {
                case CityScalePreset.AvenueCanyon_400m:
                    _layoutPattern = LayoutPattern.AvenueCanyon;
                    _buildingCount = 40;
                    _cityLength = 400f;
                    _cityWidth = 220f;
                    _roadWidth = 40f;
                    _startDistance = 35f;
                    _minHeight = 75f;
                    _maxHeight = 260f;
                    _minBuildingWidth = 20f;
                    _maxBuildingWidth = 42f;
                    _distanceHeightBias = 0.4f;
                    _enableComplexArchitectures = true;
                    _generateTestReflectionProbe = true;
                    _enableBoxProjection = true;
                    break;

                case CityScalePreset.FutureGrid_800m:
                    _layoutPattern = LayoutPattern.UrbanGrid;
                    _buildingCount = 80;
                    _cityLength = 800f;
                    _cityWidth = 500f;
                    _roadWidth = 50f;
                    _startDistance = 50f;
                    _minHeight = 80f;
                    _maxHeight = 350f;
                    _minBuildingWidth = 24f;
                    _maxBuildingWidth = 55f;
                    _distanceHeightBias = 0.6f;
                    _enableComplexArchitectures = true;
                    _generateTestReflectionProbe = true;
                    _enableBoxProjection = true;
                    break;

                case CityScalePreset.MegaCity_2000m:
                    _layoutPattern = LayoutPattern.RandomCluster;
                    _buildingCount = 160;
                    _cityLength = 2000f;
                    _cityWidth = 1400f;
                    _roadWidth = 65f;
                    _startDistance = 60f;
                    _minHeight = 90f;
                    _maxHeight = 520f;
                    _minBuildingWidth = 28f;
                    _maxBuildingWidth = 75f;
                    _distanceHeightBias = 0.85f;
                    _enableComplexArchitectures = true;
                    _generateTestReflectionProbe = true;
                    _enableBoxProjection = true;
                    break;

                case CityScalePreset.HighSpeedCorridor_600m:
                    _layoutPattern = LayoutPattern.AvenueCanyon;
                    _buildingCount = 60;
                    _cityLength = 600f;
                    _cityWidth = 160f;
                    _roadWidth = 28f;
                    _startDistance = 25f;
                    _minHeight = 110f;
                    _maxHeight = 300f;
                    _minBuildingWidth = 18f;
                    _maxBuildingWidth = 38f;
                    _distanceHeightBias = 0.35f;
                    _enableComplexArchitectures = true;
                    _generateTestReflectionProbe = true;
                    _enableBoxProjection = true;
                    break;
            }
        }

        /// <summary>
        /// 設定に基づいて仮のビル群をワンクリック生成します。
        /// </summary>
        public void GenerateCity()
        {
            ClearCity();

            Shader bldgShader = _customBuildingShader;
            if (bldgShader == null)
            {
                bldgShader = Shader.Find("AT_Murayama/DayNightCity/RealisticCityBuilding");
            }
#if UNITY_EDITOR
            if (bldgShader == null)
            {
                bldgShader = AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Deverop/AT_Murayama/DayNightCity/Shaders/S_RealisticCityBuilding.shader");
            }
#endif

            if (bldgShader == null)
            {
                Debug.LogError("[DayNightCityGenerator] ビル用シェーダー 'AT_Murayama/DayNightCity/RealisticCityBuilding' が見つかりません。");
                return;
            }

            // ルートオブジェクトの生成
            string rootName = $"{RootGameObjectNamePrefix} [{_layoutPattern} / {_buildingCount}Bldgs / {Mathf.RoundToInt(_cityLength)}m]";
            _generatedCityRoot = new GameObject(rootName);
            _generatedCityRoot.transform.SetParent(transform, false);
            _generatedCityRoot.transform.localPosition = Vector3.zero;
            _generatedCityRoot.transform.localRotation = Quaternion.identity;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(_generatedCityRoot, "Generate DayNight City");
            }
#endif

            Transform refTr = _referenceOrigin != null ? _referenceOrigin : (Camera.main != null ? Camera.main.transform : transform);
            Vector3 forwardFlat = Vector3.ProjectOnPlane(refTr.forward, Vector3.up).normalized;
            if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
            Quaternion basisRot = Quaternion.LookRotation(forwardFlat, Vector3.up);
            Vector3 originPos = refTr.position;
            originPos.y = Mathf.Max(0f, originPos.y - 10f); // 地上基準を少し低めに設定

            // 建築マテリアル（4系統のデザインプリセット）を準備
            Material[] mats = GetOrCreateBuildingMaterials(bldgShader);
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].HasProperty("_CityGroundY"))
                {
                    mats[i].SetFloat("_CityGroundY", originPos.y);
                }
            }

            // ビル群用サブコンテナ
            GameObject buildingsContainer = new GameObject("--- Buildings ---");
            buildingsContainer.transform.SetParent(_generatedCityRoot.transform, false);

            Random.State oldState = Random.state;
            Random.InitState(_randomSeed);

            // 配置パターンの生成
            switch (_layoutPattern)
            {
                case LayoutPattern.AvenueCanyon:
                    GenerateAvenueCanyon(buildingsContainer.transform, originPos, basisRot, mats);
                    break;
                case LayoutPattern.UrbanGrid:
                    GenerateUrbanGrid(buildingsContainer.transform, originPos, basisRot, mats);
                    break;
                case LayoutPattern.RandomCluster:
                    GenerateRandomCluster(buildingsContainer.transform, originPos, basisRot, mats);
                    break;
            }

            // 反射テスト用リアルタイム Reflection Probe の配置
            if (_generateTestReflectionProbe)
            {
                CreateTestReflectionProbe(_generatedCityRoot.transform, originPos, basisRot);
            }

            // 昼夜係数を反映
            UpdateNightFactor(_previewNightFactor);

            Random.state = oldState;

#if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
#endif
            Debug.Log($"[DayNightCityGenerator] 昼夜検証用ビル群を生成しました（棟数: {_buildingCount}棟, 範囲: {_cityLength:F0}m × {_cityWidth:F0}m, 道路幅: {_roadWidth:F0}m）");
        }

        /// <summary>
        /// ① 大通りキャニオン（コース両脇のビル谷間。左右のガラス反射BoxProjectionテストに最適）
        /// </summary>
        private void GenerateAvenueCanyon(Transform parent, Vector3 originPos, Quaternion basisRot, Material[] mats)
        {
            int halfCount = _buildingCount / 2;
            float halfRoad = _roadWidth * 0.5f;

            for (int i = 0; i < _buildingCount; i++)
            {
                bool isRightSide = (i % 2 == 0);
                int sideIndex = i / 2;
                float progress = (float)sideIndex / Mathf.Max(1, halfCount - 1);

                // 進行方向の距離
                float dist = _startDistance + progress * (_cityLength - _startDistance) + Random.Range(-12f, 12f);
                dist = Mathf.Clamp(dist, _startDistance * 0.8f, _cityLength * 1.05f);

                // 横方向のオフセット（大通りの左右壁面）
                float sideSign = isRightSide ? 1.0f : -1.0f;
                float distNorm = Mathf.Clamp01(dist / Mathf.Max(1f, _cityLength));
                float heightScale = 1.0f + distNorm * _distanceHeightBias * 1.6f;
                float widthScale = 1.0f + distNorm * _distanceHeightBias * 0.6f;

                float bldgWidth = Random.Range(_minBuildingWidth, _maxBuildingWidth) * widthScale;
                float bldgDepth = Random.Range(_minBuildingWidth, _maxBuildingWidth) * widthScale;
                float bldgHeight = Random.Range(_minHeight, _maxHeight) * heightScale;

                // 道路端にビル正面を揃えて反射を最大化
                float lateralOffset = sideSign * (halfRoad + bldgWidth * 0.5f + Random.Range(0f, 15f));

                // 建築形状スタイル（0=標準, 1=段状セットバック, 2=低層ポディウム+高層タワー）
                int archStyle = _enableComplexArchitectures ? (i % 3) : 0;

                Vector3 bldgCenterPos = originPos + basisRot * new Vector3(lateralOffset, 0f, dist);
                Quaternion bldgRot = basisRot * Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f);

                CreateBuildingArchitecture(parent, bldgCenterPos, bldgRot, bldgWidth, bldgHeight, bldgDepth,
                    archStyle, mats[i % mats.Length], i, $"CanyonBldg_{i:000}_{(isRightSide ? "R" : "L")}");
            }
        }

        /// <summary>
        /// ② 都市グリッド街区（マンハッタン風の碁盤目ブロック配置）
        /// </summary>
        private void GenerateUrbanGrid(Transform parent, Vector3 originPos, Quaternion basisRot, Material[] mats)
        {
            float blockSize = 70f;
            float streetWidth = 24f;
            float totalPitch = blockSize + streetWidth;

            float halfRoad = _roadWidth * 0.5f;
            float halfWidth = _cityWidth * 0.5f;

            int bldgIdx = 0;
            float currentZ = _startDistance;

            while (currentZ < _cityLength && bldgIdx < _buildingCount)
            {
                for (float currentX = -halfWidth; currentX <= halfWidth && bldgIdx < _buildingCount; currentX += totalPitch)
                {
                    // 中央の大通り・コース領域は空ける
                    if (Mathf.Abs(currentX) < halfRoad) continue;

                    float distNorm = Mathf.Clamp01(currentZ / Mathf.Max(1f, _cityLength));
                    float heightScale = 1.0f + distNorm * _distanceHeightBias * 1.5f;

                    float bldgWidth = Random.Range(_minBuildingWidth, Mathf.Min(_maxBuildingWidth, blockSize * 0.9f));
                    float bldgDepth = Random.Range(_minBuildingWidth, Mathf.Min(_maxBuildingWidth, blockSize * 0.9f));
                    float bldgHeight = Random.Range(_minHeight, _maxHeight) * heightScale;

                    Vector3 centerPos = originPos + basisRot * new Vector3(currentX + Random.Range(-6f, 6f), 0f, currentZ + Random.Range(-6f, 6f));
                    Quaternion bldgRot = basisRot * Quaternion.Euler(0f, (Random.value > 0.5f ? 0f : 90f) + Random.Range(-3f, 3f), 0f);

                    int archStyle = _enableComplexArchitectures ? (bldgIdx % 3) : 0;

                    CreateBuildingArchitecture(parent, centerPos, bldgRot, bldgWidth, bldgHeight, bldgDepth,
                        archStyle, mats[bldgIdx % mats.Length], bldgIdx, $"GridBldg_{bldgIdx:000}");

                    bldgIdx++;
                }

                currentZ += totalPitch;
            }
        }

        /// <summary>
        /// ③ ランダムクラスター（自然なメガロポリス・超高層スカイライン）
        /// </summary>
        private void GenerateRandomCluster(Transform parent, Vector3 originPos, Quaternion basisRot, Material[] mats)
        {
            float halfWidth = _cityWidth * 0.5f;
            float halfRoad = _roadWidth * 0.5f;

            for (int i = 0; i < _buildingCount; i++)
            {
                float t = (float)i / Mathf.Max(1, _buildingCount - 1);
                float distFactor = Mathf.Pow(t, 0.82f);
                float dist = Mathf.Lerp(_startDistance, _cityLength, distFactor) + Random.Range(-20f, 20f);
                dist = Mathf.Clamp(dist, _startDistance * 0.8f, _cityLength * 1.05f);

                bool isRight = (i % 2 == 0);
                float sideOffset = isRight
                    ? Random.Range(halfRoad, halfWidth)
                    : Random.Range(-halfWidth, -halfRoad);

                float distNorm = Mathf.Clamp01(dist / Mathf.Max(1f, _cityLength));
                float heightScale = 1.0f + distNorm * _distanceHeightBias * 1.7f;
                float widthScale = 1.0f + distNorm * _distanceHeightBias * 0.7f;

                float bldgWidth = Random.Range(_minBuildingWidth, _maxBuildingWidth) * widthScale;
                float bldgDepth = Random.Range(_minBuildingWidth, _maxBuildingWidth) * widthScale;
                float bldgHeight = Random.Range(_minHeight, _maxHeight) * heightScale;

                Vector3 centerPos = originPos + basisRot * new Vector3(sideOffset, 0f, dist);
                Quaternion bldgRot = basisRot * Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f);

                int archStyle = _enableComplexArchitectures ? (i % 3) : 0;

                CreateBuildingArchitecture(parent, centerPos, bldgRot, bldgWidth, bldgHeight, bldgDepth,
                    archStyle, mats[i % mats.Length], i, $"ClusterBldg_{i:000}");
            }
        }

        /// <summary>
        /// 建築スタイル（標準、段状セットバック、基底ポディウム）に応じたビル構造体を生成します。
        /// </summary>
        private void CreateBuildingArchitecture(Transform parent, Vector3 basePos, Quaternion rot,
            float width, float totalHeight, float depth, int archStyle, Material mat, int seedIndex, string namePrefix)
        {
            GameObject bldgObj = new GameObject(namePrefix);
            bldgObj.transform.SetParent(parent, true);
            bldgObj.transform.position = basePos;
            bldgObj.transform.rotation = rot;

            switch (archStyle)
            {
                case 1:
                    // 段状セットバックタワー（摩天楼・上層部が絞られるシルエット）
                    CreateSteppedSetbackTower(bldgObj.transform, width, totalHeight, depth, mat, seedIndex);
                    break;
                case 2:
                    // 低層商業ポディウム ＋ 高層スレンダーメインタワー
                    CreatePodiumTower(bldgObj.transform, width, totalHeight, depth, mat, seedIndex);
                    break;
                default:
                    // 標準カーテンウォール高層タワー
                    CreateStandardTower(bldgObj.transform, width, totalHeight, depth, mat, seedIndex);
                    break;
            }

            // Static フラグの設定（Reflection Probe ベイクやバッチングテスト用）
            if (_setStaticFlags)
            {
#if UNITY_EDITOR
                SetStaticRecursively(bldgObj);
#endif
            }
        }

        private void CreateStandardTower(Transform parent, float width, float height, float depth, Material mat, int seedIndex)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Tower_Main";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            cube.transform.localScale = new Vector3(width, height, depth);

            ApplyMaterialAndCleanCollider(cube, mat, seedIndex);
        }

        private void CreateSteppedSetbackTower(Transform parent, float width, float height, float depth, Material mat, int seedIndex)
        {
            // 下層部（全体の45%高さ、フル幅）
            float h1 = height * 0.45f;
            GameObject tier1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier1.name = "Tier_Bottom";
            tier1.transform.SetParent(parent, false);
            tier1.transform.localPosition = new Vector3(0f, h1 * 0.5f, 0f);
            tier1.transform.localScale = new Vector3(width, h1, depth);
            ApplyMaterialAndCleanCollider(tier1, mat, seedIndex);

            // 中層部（全体の35%高さ、80%幅）
            float h2 = height * 0.35f;
            GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier2.name = "Tier_Mid";
            tier2.transform.SetParent(parent, false);
            tier2.transform.localPosition = new Vector3(0f, h1 + h2 * 0.5f, 0f);
            tier2.transform.localScale = new Vector3(width * 0.82f, h2, depth * 0.82f);
            ApplyMaterialAndCleanCollider(tier2, mat, seedIndex + 1);

            // 頂部クラウン（全体の20%高さ、60%幅）
            float h3 = height * 0.20f;
            GameObject tier3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier3.name = "Tier_TopCrown";
            tier3.transform.SetParent(parent, false);
            tier3.transform.localPosition = new Vector3(0f, h1 + h2 + h3 * 0.5f, 0f);
            tier3.transform.localScale = new Vector3(width * 0.62f, h3, depth * 0.62f);
            ApplyMaterialAndCleanCollider(tier3, mat, seedIndex + 2);
        }

        private void CreatePodiumTower(Transform parent, float width, float height, float depth, Material mat, int seedIndex)
        {
            // 低層商業ポディウム（高さ14m、幅広）
            float podiumHeight = Mathf.Min(16f, height * 0.2f);
            GameObject podium = GameObject.CreatePrimitive(PrimitiveType.Cube);
            podium.name = "Base_Podium";
            podium.transform.SetParent(parent, false);
            podium.transform.localPosition = new Vector3(0f, podiumHeight * 0.5f, 0f);
            podium.transform.localScale = new Vector3(width * 1.35f, podiumHeight, depth * 1.35f);
            ApplyMaterialAndCleanCollider(podium, mat, seedIndex);

            // 高層タワー（ポディウムの上に聳え立つスレンダー形状）
            float towerHeight = height - podiumHeight;
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower_Slender";
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = new Vector3(0f, podiumHeight + towerHeight * 0.5f, 0f);
            tower.transform.localScale = new Vector3(width * 0.90f, towerHeight, depth * 0.90f);
            ApplyMaterialAndCleanCollider(tower, mat, seedIndex + 1);
        }

        [Tooltip("SRP Batcher を最優先にする（マテリアル直接共有でDrawCallを最小化。ON推奨）")]
        [SerializeField] private bool _enableSrpBatching = true;

        private void ApplyMaterialAndCleanCollider(GameObject go, Material baseMat, int seed)
        {
            if (_removeColliders)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (_enableSrpBatching)
                {
                    // SRP Batcher を100%効かせるため、MaterialPropertyBlock を使わずに共有マテリアルを直接指定
                    r.sharedMaterial = baseMat;
                }
                else
                {
                    // 各棟ごとに異なるランダムシードを与えたい場合のみ MaterialPropertyBlock を適用
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetFloat("_BuildingSeed", (seed + 1) * 23.7f);
                    mpb.SetFloat("_NightFactor", _previewNightFactor);
                    r.SetPropertyBlock(mpb);
                    r.sharedMaterial = baseMat;
                }
            }
        }

        /// <summary>
        /// 検証用のリアルタイム Reflection Probe を大通り中央に配置します。
        /// </summary>
        private void CreateTestReflectionProbe(Transform parent, Vector3 originPos, Quaternion basisRot)
        {
            GameObject probeGo = new GameObject("--- Test Reflection Probe (Box Projection) ---");
            probeGo.transform.SetParent(parent, true);

            // キャニオン中央・高さ18mに配置
            Vector3 centerPos = originPos + basisRot * new Vector3(0f, 18f, _cityLength * 0.45f);
            probeGo.transform.position = centerPos;
            probeGo.transform.rotation = basisRot;

            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 256;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.boxProjection = _enableBoxProjection;

            // キャニオン全体を覆うバウンディングボックスサイズ
            float boxWidth = _cityWidth * 1.1f;
            float boxHeight = _maxHeight * 1.3f;
            float boxLength = _cityLength * 1.15f;
            probe.size = new Vector3(boxWidth, boxHeight, boxLength);
            probe.center = Vector3.zero;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                probe.RenderProbe();
            }
#endif
        }

        /// <summary>
        /// 4系統のデザインプリセットマテリアルを準備します。
        /// </summary>
        private Material[] GetOrCreateBuildingMaterials(Shader shader)
        {
            if (_presetMaterials != null && _presetMaterials.Length >= 4 && _presetMaterials[0] != null)
            {
                return _presetMaterials;
            }

            Material[] mats = new Material[4];

            // 1. 白亜・高反射ガラスカーテンウォール（王道・現代未来都市）
            mats[0] = new Material(shader) { name = "M_DayNight_GlassWhite_Preset" };
            mats[0].SetColor("_WallColor", new Color(0.88f, 0.90f, 0.93f, 1.0f));
            mats[0].SetFloat("_WallMetallic", 0.12f);
            mats[0].SetFloat("_WallSmoothness", 0.38f);
            mats[0].SetColor("_GlassColorDay", new Color(0.04f, 0.08f, 0.12f, 1.0f));
            mats[0].SetFloat("_GlassMetallic", 0.78f);
            mats[0].SetFloat("_GlassSmoothness", 0.95f);
            mats[0].SetColor("_AccentColor", new Color(0.2f, 2.8f, 4.5f, 1.0f)); // シアン
            mats[0].SetFloat("_AccentIntensity", 2.4f);

            // 2. ダークチタン ＆ スレートブルーガラス（重厚・サイバー金融街）
            mats[1] = new Material(shader) { name = "M_DayNight_TitaniumAzure_Preset" };
            mats[1].SetColor("_WallColor", new Color(0.25f, 0.28f, 0.32f, 1.0f));
            mats[1].SetFloat("_WallMetallic", 0.65f);
            mats[1].SetFloat("_WallSmoothness", 0.55f);
            mats[1].SetColor("_GlassColorDay", new Color(0.03f, 0.06f, 0.10f, 1.0f));
            mats[1].SetFloat("_GlassMetallic", 0.85f);
            mats[1].SetFloat("_GlassSmoothness", 0.96f);
            mats[1].SetColor("_AccentColor", new Color(3.8f, 2.2f, 0.6f, 1.0f)); // ウォームアンバー
            mats[1].SetFloat("_AccentIntensity", 2.2f);

            // 3. ピュアホワイト ＆ ディープネイビー（クリーン近未来）
            mats[2] = new Material(shader) { name = "M_DayNight_FuturisticClean_Preset" };
            mats[2].SetColor("_WallColor", new Color(0.94f, 0.95f, 0.97f, 1.0f));
            mats[2].SetFloat("_WallMetallic", 0.08f);
            mats[2].SetFloat("_WallSmoothness", 0.30f);
            mats[2].SetColor("_GlassColorDay", new Color(0.02f, 0.04f, 0.08f, 1.0f));
            mats[2].SetFloat("_GlassMetallic", 0.70f);
            mats[2].SetFloat("_GlassSmoothness", 0.93f);
            mats[2].SetColor("_AccentColor", new Color(2.6f, 0.5f, 4.0f, 1.0f)); // パープルネオン
            mats[2].SetFloat("_AccentIntensity", 2.6f);

            // 4. シャンパンブロンズ ＆ 反射ブロンズガラス（高層コーポレートタワー）
            mats[3] = new Material(shader) { name = "M_DayNight_ChampagneGold_Preset" };
            mats[3].SetColor("_WallColor", new Color(0.72f, 0.68f, 0.62f, 1.0f));
            mats[3].SetFloat("_WallMetallic", 0.45f);
            mats[3].SetFloat("_WallSmoothness", 0.48f);
            mats[3].SetColor("_GlassColorDay", new Color(0.08f, 0.06f, 0.03f, 1.0f));
            mats[3].SetFloat("_GlassMetallic", 0.82f);
            mats[3].SetFloat("_GlassSmoothness", 0.94f);
            mats[3].SetColor("_AccentColor", new Color(4.0f, 1.8f, 0.4f, 1.0f)); // ゴールド
            mats[3].SetFloat("_AccentIntensity", 2.0f);

            _presetMaterials = mats;
            return mats;
        }

        /// <summary>
        /// 昼夜係数を生成済みビル群全体に反映します（0.0=昼, 1.0=夜）。
        /// </summary>
        public void UpdateNightFactor(float factor)
        {
            _previewNightFactor = Mathf.Clamp01(factor);

            // 1. プリセットマテリアル側のプロパティを一括更新（SRP Batcher 最優先時）
            if (_presetMaterials != null)
            {
                for (int i = 0; i < _presetMaterials.Length; i++)
                {
                    if (_presetMaterials[i] != null && _presetMaterials[i].HasProperty("_NightFactor"))
                    {
                        _presetMaterials[i].SetFloat("_NightFactor", _previewNightFactor);
                    }
                }
            }

            if (_generatedCityRoot == null) return;

            // 2. 個別レンダラー側への反映（MPB利用時または直接マテリアル）
            Renderer[] renderers = _generatedCityRoot.GetComponentsInChildren<Renderer>(true);
            if (!_enableSrpBatching)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetFloat("_NightFactor", _previewNightFactor);
                    r.SetPropertyBlock(mpb);
                }
            }
            else
            {
                // sharedMaterial のプロパティが更新されていれば十分
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.HasProperty("_NightFactor"))
                    {
                        r.sharedMaterial.SetFloat("_NightFactor", _previewNightFactor);
                    }
                }
            }

            // Directional Light の同期（オプション）
            if (_syncDirectionalLight)
            {
                SyncSunLight(_previewNightFactor);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && _generatedCityRoot != null)
            {
                ReflectionProbe probe = _generatedCityRoot.GetComponentInChildren<ReflectionProbe>();
                if (probe != null)
                {
                    probe.RenderProbe();
                }
            }
#endif
        }

        private void SyncSunLight(float nightFactor)
        {
            Light sunLight = RenderSettings.sun;
            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        sunLight = lights[i];
                        break;
                    }
                }
            }

            if (sunLight != null)
            {
                // 昼: 太陽高度50度, 1.2lux -> 夕暮れ: 5度, 0.8lux -> 夜: -15度, 0.05lux
                float sunPitch = Mathf.Lerp(50f, -15f, nightFactor);
                sunLight.transform.rotation = Quaternion.Euler(sunPitch, -30f, 0f);
                sunLight.intensity = Mathf.Lerp(1.2f, 0.05f, nightFactor);
                sunLight.color = Color.Lerp(new Color(1f, 0.98f, 0.95f), new Color(0.2f, 0.35f, 0.65f), nightFactor);
            }
        }

#if UNITY_EDITOR
        private void SetStaticRecursively(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);

            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetStaticRecursively(go.transform.GetChild(i).gameObject);
            }
        }
#endif

        /// <summary>
        /// 生成したビル群および検証用オブジェクトを完全に一括削除します。
        /// </summary>
        public void ClearCity()
        {
            if (_generatedCityRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_generatedCityRoot);
                }
                else
                {
#if UNITY_EDITOR
                    Undo.DestroyObjectImmediate(_generatedCityRoot);
#else
                    DestroyImmediate(_generatedCityRoot);
#endif
                }
                _generatedCityRoot = null;
            }

            // 残骸のクリーンアップ（名前にプレフィックスが含まれるものを全て消去）
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith(RootGameObjectNamePrefix))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
#if UNITY_EDITOR
                        Undo.DestroyObjectImmediate(child.gameObject);
#else
                        DestroyImmediate(child.gameObject);
#endif
                    }
                }
            }

#if UNITY_EDITOR
            // ルート直下に誤ってデタッチされた残骸オブジェクトも完全走査して消去
            if (!Application.isPlaying && gameObject.scene.isLoaded)
            {
                GameObject[] rootObjs = gameObject.scene.GetRootGameObjects();
                for (int i = 0; i < rootObjs.Length; i++)
                {
                    if (rootObjs[i] != null && rootObjs[i] != gameObject && rootObjs[i].name.StartsWith(RootGameObjectNamePrefix))
                    {
                        Undo.DestroyObjectImmediate(rootObjs[i]);
                    }
                }
            }
#endif

            Debug.Log("[DayNightCityGenerator] 検証用ビル群を一括削除しました（残骸ゼロ）");
        }
    }
}
