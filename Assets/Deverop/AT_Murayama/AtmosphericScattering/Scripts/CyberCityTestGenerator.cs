using UnityEngine;
using UnityEngine.Rendering;

namespace AT_Murayama.AtmosphericScattering
{
    /// <summary>
    /// 大気散乱（Atmospheric Scattering）や遠景フォグの見た目を検証するための
    /// 「サイバーパンク風高層ビル群＋地上発光ネオン」生成ジェネレーター。
    /// ・近景〜超遠景（数百m〜数千m）の広大なメガロポリス都市をワンクリックで生成・再構築・一括削除できます。
    /// ・専用シェーダー（S_CyberCityTestBuilding）により、各ビルの窓明かりや地上ネオンがプロシージャルに発光します。
    /// ・不要になった際は一括削除でき、本番アセットやシーンデータを汚しません。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CyberCityTestGenerator : MonoBehaviour
    {
        public enum CityScalePreset
        {
            [InspectorName("① 標準（中規模・近〜中景：42棟 / 400m）")]
            StandardMedium_400m,

            [InspectorName("② 巨大都市（大規模・遠景：120棟 / 1200m）")]
            LargeCity_1200m,

            [InspectorName("③ 超広大メガロポリス（特大規模・超遠景：240棟 / 2500m）")]
            MegaCity_2500m,

            [InspectorName("④ コース沿道ビルキャニオン（両脇高層群：70棟 / 800m）")]
            RoadsideCanyon_800m
        }

        [Header("配置プリセット")]
        [SerializeField] private CityScalePreset _preset = CityScalePreset.StandardMedium_400m;

        [Header("1. 生成規模・距離・広がり設定")]
        [Tooltip("生成するビルの棟数（40〜300棟推奨）")]
        [Range(10, 500)]
        [SerializeField] private int _buildingCount = 42;

        [Tooltip("最も手前のビルまでの距離（m）")]
        [Min(10f)]
        [SerializeField] private float _minDistance = 60f;

        [Tooltip("最も奥のビルまでの距離（m）。大気散乱の Max Distance に合わせて広げられます")]
        [Min(50f)]
        [SerializeField] private float _maxDistance = 400f;

        [Tooltip("左右方向の広がり幅（m）。コース全体や広角カメラの視界をカバーできます")]
        [Min(30f)]
        [SerializeField] private float _citySpreadWidth = 260f;

        [Tooltip("道路・コース用の空きスペース（中央付近にビルが被らないようにする横幅）")]
        [Min(0f)]
        [SerializeField] private float _centerClearWidth = 25f;

        [Header("2. ビルのサイズ・スケール")]
        [Tooltip("ビルの最小高さ（m）")]
        [Min(20f)]
        [SerializeField] private float _minHeight = 70f;

        [Tooltip("ビルの最大高さ（m）")]
        [Min(40f)]
        [SerializeField] private float _maxHeight = 240f;

        [Tooltip("ビルの最小幅・奥行き（m）")]
        [Min(10f)]
        [SerializeField] private float _minWidth = 16f;

        [Tooltip("ビルの最大幅・奥行き（m）")]
        [Min(15f)]
        [SerializeField] private float _maxWidth = 40f;

        [Tooltip("遠くのビルほど超高層（メガタワー）にする比率（0=均一, 1=遠景ほど巨大化）")]
        [Range(0f, 1f)]
        [SerializeField] private float _distanceHeightBias = 0.45f;

        [Header("3. 地上ネオン・街灯発光")]
        [Tooltip("ビルの低層部に色鮮やかな地上ネオン発光ボックスを生成するか")]
        [SerializeField] private bool _generateStreetNeons = true;

        [Tooltip("ビル1棟あたりの地上ネオン数")]
        [Range(1, 8)]
        [SerializeField] private int _neonsPerBuilding = 3;

        [Header("4. 手前オブジェクト・基準トランスフォーム")]
        [Tooltip("手前の距離対比用オーバーパス（高架橋）を生成するか")]
        [SerializeField] private bool _generateForegroundBridge = true;

        [Tooltip("都市生成の基準位置・向き（未指定時は MainCamera またはこの Transform）")]
        [SerializeField] private Transform _referenceOrigin;

        [Tooltip("ビルのランダム配置シード値")]
        [SerializeField] private int _randomSeed = 20261027;

        [Tooltip("ビルの影落としをOFFにしてパフォーマンスを最優先にするか（遠景テスト時はON推奨）")]
        [SerializeField] private bool _disableBuildingShadows = true;

        [Header("生成済みルートオブジェクト（管理用）")]
        [SerializeField] private GameObject _generatedCityRoot;

        public bool HasGeneratedCity => _generatedCityRoot != null;
        public int BuildingCount => _buildingCount;
        public float MaxDistance => _maxDistance;

        private void OnDisable()
        {
            // プレイモード終了時などに不完全な参照が残らないようチェック
        }

        /// <summary>
        /// プリセットを選択してパラメータを一括設定します。
        /// </summary>
        public void ApplyPreset(CityScalePreset preset)
        {
            _preset = preset;
            switch (preset)
            {
                case CityScalePreset.StandardMedium_400m:
                    _buildingCount = 42;
                    _minDistance = 65f;
                    _maxDistance = 400f;
                    _citySpreadWidth = 260f;
                    _centerClearWidth = 25f;
                    _minHeight = 75f;
                    _maxHeight = 240f;
                    _minWidth = 16f;
                    _maxWidth = 38f;
                    _distanceHeightBias = 0.45f;
                    _generateStreetNeons = true;
                    _neonsPerBuilding = 3;
                    _generateForegroundBridge = true;
                    break;

                case CityScalePreset.LargeCity_1200m:
                    _buildingCount = 120;
                    _minDistance = 75f;
                    _maxDistance = 1200f;
                    _citySpreadWidth = 650f;
                    _centerClearWidth = 35f;
                    _minHeight = 85f;
                    _maxHeight = 380f;
                    _minWidth = 22f;
                    _maxWidth = 55f;
                    _distanceHeightBias = 0.65f;
                    _generateStreetNeons = true;
                    _neonsPerBuilding = 3;
                    _generateForegroundBridge = true;
                    break;

                case CityScalePreset.MegaCity_2500m:
                    _buildingCount = 240;
                    _minDistance = 90f;
                    _maxDistance = 2500f;
                    _citySpreadWidth = 1400f;
                    _centerClearWidth = 50f;
                    _minHeight = 100f;
                    _maxHeight = 550f;
                    _minWidth = 28f;
                    _maxWidth = 80f;
                    _distanceHeightBias = 0.85f;
                    _generateStreetNeons = true;
                    _neonsPerBuilding = 2;
                    _generateForegroundBridge = true;
                    break;

                case CityScalePreset.RoadsideCanyon_800m:
                    _buildingCount = 70;
                    _minDistance = 45f;
                    _maxDistance = 800f;
                    _citySpreadWidth = 180f;
                    _centerClearWidth = 40f; // コース幅を広めに確保
                    _minHeight = 110f;
                    _maxHeight = 320f;
                    _minWidth = 20f;
                    _maxWidth = 42f;
                    _distanceHeightBias = 0.5f;
                    _generateStreetNeons = true;
                    _neonsPerBuilding = 4;
                    _generateForegroundBridge = false;
                    break;
            }
        }

        /// <summary>
        /// 設定値に基づいてサイバーパンクビル群をワンクリック生成します。
        /// </summary>
        public void GenerateCity()
        {
            ClearCity();

            Shader buildingShader = Shader.Find("AT_Murayama/AtmosphericScattering/CyberCityTestBuilding");
            if (buildingShader == null)
            {
#if UNITY_EDITOR
                buildingShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Deverop/AT_Murayama/AtmosphericScattering/Shaders/S_CyberCityTestBuilding.shader");
#endif
            }

            if (buildingShader == null)
            {
                Debug.LogError("[CyberCityTestGenerator] ビル用シェーダー 'AT_Murayama/AtmosphericScattering/CyberCityTestBuilding' が見つかりません。");
                return;
            }

            _generatedCityRoot = new GameObject($"TestCyberCity_Scale{Mathf.RoundToInt(_maxDistance)}m_{_buildingCount}Bldgs (ワンクリック削除可能)");
            _generatedCityRoot.transform.SetParent(transform, false);
            _generatedCityRoot.transform.localPosition = Vector3.zero;
            _generatedCityRoot.transform.localRotation = Quaternion.identity;

            Transform refTr = _referenceOrigin != null ? _referenceOrigin : (Camera.main != null ? Camera.main.transform : transform);
            Vector3 forwardFlat = Vector3.ProjectOnPlane(refTr.forward, Vector3.up).normalized;
            if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
            Quaternion basisRot = Quaternion.LookRotation(forwardFlat, Vector3.up);
            Vector3 originPos = refTr.position;
            originPos.y -= 12f; // 地上基準を少し低めに配置

            // 窓明かり・ビルパターンの異なる3種類のマテリアルを準備
            Material[] buildingMats = new Material[4];
            for (int i = 0; i < buildingMats.Length; i++)
            {
                Material m = new Material(buildingShader)
                {
                    name = $"M_CityBldg_{i}"
                };
                m.SetFloat("_BuildingSeed", (i + 1) * 19.7f);
                buildingMats[i] = m;
            }

            // 地上ネオン用マテリアル（Unlit）
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Material neonCyan = null, neonWarm = null, neonRed = null, neonPurple = null;
            if (unlitShader != null)
            {
                neonCyan = new Material(unlitShader) { name = "M_Neon_Cyan" };
                neonCyan.SetColor("_BaseColor", new Color(0.3f, 2.8f, 4.5f, 1f));

                neonWarm = new Material(unlitShader) { name = "M_Neon_Warm" };
                neonWarm.SetColor("_BaseColor", new Color(3.5f, 2.2f, 0.8f, 1f));

                neonRed = new Material(unlitShader) { name = "M_Neon_Red" };
                neonRed.SetColor("_BaseColor", new Color(3.8f, 0.4f, 0.6f, 1f));

                neonPurple = new Material(unlitShader) { name = "M_Neon_Purple" };
                neonPurple.SetColor("_BaseColor", new Color(2.6f, 0.5f, 4.0f, 1f));
            }

            Random.State oldState = Random.state;
            Random.InitState(_randomSeed);

            float halfSpread = _citySpreadWidth * 0.5f;
            float halfClear = _centerClearWidth * 0.5f;

            for (int i = 0; i < _buildingCount; i++)
            {
                // 距離：近景から遠景へ指数的・均一に配分
                float t = (float)i / Mathf.Max(1, _buildingCount - 1);
                // 遠景ほどビル密度が高くなるよう非線形補間
                float distFactor = Mathf.Pow(t, 0.85f);
                float dist = Mathf.Lerp(_minDistance, _maxDistance, distFactor) + Random.Range(-15f, 15f);
                dist = Mathf.Clamp(dist, _minDistance * 0.8f, _maxDistance * 1.05f);

                // 左右位置：中央（コースやカメラ正面）を空けて左右に均等配置
                bool isRightSide = (i % 2 == 0);
                float sideOffset;
                if (isRightSide)
                {
                    sideOffset = Random.Range(halfClear, halfSpread);
                }
                else
                {
                    sideOffset = Random.Range(-halfSpread, -halfClear);
                }

                // 遠景ほど超高層ビルにするバイアス
                float distNorm = Mathf.Clamp01((dist - _minDistance) / Mathf.Max(1f, _maxDistance - _minDistance));
                float heightScale = 1.0f + distNorm * _distanceHeightBias * 1.5f;
                float widthScale = 1.0f + distNorm * _distanceHeightBias * 0.8f;

                float height = Random.Range(_minHeight, _maxHeight) * heightScale;
                float width = Random.Range(_minWidth, _maxWidth) * widthScale;
                float depth = Random.Range(_minWidth, _maxWidth) * widthScale;

                GameObject bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bldg.name = $"Building_{i:000}_Dist{Mathf.RoundToInt(dist)}m";
                bldg.transform.SetParent(_generatedCityRoot.transform, true);

                // 走行テスト時に衝突しないよう Collider を即座に削除
                Collider col = bldg.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);

                Vector3 worldPos = originPos + basisRot * new Vector3(sideOffset, height * 0.5f, dist);
                bldg.transform.position = worldPos;
                bldg.transform.rotation = basisRot * Quaternion.Euler(0f, Random.Range(-10f, 10f), 0f);
                bldg.transform.localScale = new Vector3(width, height, depth);

                Renderer r = bldg.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = buildingMats[i % buildingMats.Length];
                    if (_disableBuildingShadows)
                    {
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                }

                // 地上ネオン・街灯の生成
                if (_generateStreetNeons && neonCyan != null)
                {
                    int neonCount = Random.Range(1, _neonsPerBuilding + 1);
                    for (int s = 0; s < neonCount; s++)
                    {
                        GameObject lightBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        lightBox.name = $"Neon_{s}";
                        lightBox.transform.SetParent(bldg.transform, false);

                        Collider lCol = lightBox.GetComponent<Collider>();
                        if (lCol != null) DestroyImmediate(lCol);

                        float lx = Random.Range(-0.52f, 0.52f);
                        float ly = Random.Range(-0.48f, -0.25f);
                        float lz = -0.52f;
                        lightBox.transform.localPosition = new Vector3(lx, ly, lz);
                        lightBox.transform.localScale = new Vector3(Random.Range(0.08f, 0.28f), Random.Range(0.02f, 0.08f), 0.05f);

                        Renderer lr = lightBox.GetComponent<Renderer>();
                        if (lr != null)
                        {
                            int colorPick = (i + s) % 6;
                            Material chosenNeon = neonCyan;
                            if (colorPick == 0) chosenNeon = neonRed;
                            else if (colorPick == 1) chosenNeon = neonWarm;
                            else if (colorPick == 2) chosenNeon = neonPurple;

                            lr.sharedMaterial = chosenNeon;
                            lr.shadowCastingMode = ShadowCastingMode.Off;
                            lr.receiveShadows = false;
                        }
                    }
                }
            }

            // 手前の距離対比用オーバーパス（高架橋）
            if (_generateForegroundBridge)
            {
                GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridge.name = "Foreground_Overpass_Dist35m";
                bridge.transform.SetParent(_generatedCityRoot.transform, true);
                Collider fbCol = bridge.GetComponent<Collider>();
                if (fbCol != null) DestroyImmediate(fbCol);

                bridge.transform.position = originPos + basisRot * new Vector3(10f, 18f, 42f);
                bridge.transform.rotation = basisRot * Quaternion.Euler(0f, -25f, 0f);
                bridge.transform.localScale = new Vector3(8f, 5f, 95f);
                Renderer fbRenderer = bridge.GetComponent<Renderer>();
                if (fbRenderer != null)
                {
                    fbRenderer.sharedMaterial = buildingMats[0];
                    if (_disableBuildingShadows)
                    {
                        fbRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            Random.state = oldState;
            Debug.Log($"[CyberCityTestGenerator] ビル群を生成しました（棟数: {_buildingCount}棟, 範囲: {_minDistance:F0}m〜{_maxDistance:F0}m, 広がり: {_citySpreadWidth:F0}m）");
        }

        /// <summary>
        /// 生成したビル群を一括削除します。
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
                    DestroyImmediate(_generatedCityRoot);
                }
                _generatedCityRoot = null;
            }

            // 念のため名前プレフィックスで残骸があればクリーンアップ
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("TestCyberCity"))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
    }
}

