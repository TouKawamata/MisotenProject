using UnityEditor;
using UnityEngine;

namespace AT_Murayama.DayNightCity.Editor
{
    [CustomEditor(typeof(DayNightCityGenerator))]
    public class DayNightCityGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty _presetProp;
        private SerializedProperty _layoutPatternProp;
        private SerializedProperty _buildingCountProp;
        private SerializedProperty _cityLengthProp;
        private SerializedProperty _cityWidthProp;
        private SerializedProperty _roadWidthProp;
        private SerializedProperty _startDistanceProp;

        private SerializedProperty _minHeightProp;
        private SerializedProperty _maxHeightProp;
        private SerializedProperty _minBuildingWidthProp;
        private SerializedProperty _maxBuildingWidthProp;
        private SerializedProperty _distanceHeightBiasProp;

        private SerializedProperty _enableComplexArchitecturesProp;
        private SerializedProperty _removeCollidersProp;

        private SerializedProperty _generateTestReflectionProbeProp;
        private SerializedProperty _enableBoxProjectionProp;
        private SerializedProperty _setStaticFlagsProp;
        private SerializedProperty _enableSrpBatchingProp;

        private SerializedProperty _previewNightFactorProp;
        private SerializedProperty _syncDirectionalLightProp;

        private SerializedProperty _customBuildingShaderProp;
        private SerializedProperty _presetMaterialsProp;
        private SerializedProperty _referenceOriginProp;
        private SerializedProperty _randomSeedProp;

        private bool _showLayoutGroup = true;
        private bool _showDimensionsGroup = true;
        private bool _showReflectionGroup = true;
        private bool _showDayNightGroup = true;
        private bool _showAdvancedGroup = false;

        private void OnEnable()
        {
            _presetProp = serializedObject.FindProperty("_preset");
            _layoutPatternProp = serializedObject.FindProperty("_layoutPattern");
            _buildingCountProp = serializedObject.FindProperty("_buildingCount");
            _cityLengthProp = serializedObject.FindProperty("_cityLength");
            _cityWidthProp = serializedObject.FindProperty("_cityWidth");
            _roadWidthProp = serializedObject.FindProperty("_roadWidth");
            _startDistanceProp = serializedObject.FindProperty("_startDistance");

            _minHeightProp = serializedObject.FindProperty("_minHeight");
            _maxHeightProp = serializedObject.FindProperty("_maxHeight");
            _minBuildingWidthProp = serializedObject.FindProperty("_minBuildingWidth");
            _maxBuildingWidthProp = serializedObject.FindProperty("_maxBuildingWidth");
            _distanceHeightBiasProp = serializedObject.FindProperty("_distanceHeightBias");

            _enableComplexArchitecturesProp = serializedObject.FindProperty("_enableComplexArchitectures");
            _removeCollidersProp = serializedObject.FindProperty("_removeColliders");

            _generateTestReflectionProbeProp = serializedObject.FindProperty("_generateTestReflectionProbe");
            _enableBoxProjectionProp = serializedObject.FindProperty("_enableBoxProjection");
            _setStaticFlagsProp = serializedObject.FindProperty("_setStaticFlags");
            _enableSrpBatchingProp = serializedObject.FindProperty("_enableSrpBatching");

            _previewNightFactorProp = serializedObject.FindProperty("_previewNightFactor");
            _syncDirectionalLightProp = serializedObject.FindProperty("_syncDirectionalLight");

            _customBuildingShaderProp = serializedObject.FindProperty("_customBuildingShader");
            _presetMaterialsProp = serializedObject.FindProperty("_presetMaterials");
            _referenceOriginProp = serializedObject.FindProperty("_referenceOrigin");
            _randomSeedProp = serializedObject.FindProperty("_randomSeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DayNightCityGenerator generator = (DayNightCityGenerator)target;

            EditorGUILayout.Space(4);
            DrawHeaderBox(generator);

            EditorGUILayout.Space(6);
            DrawPresetSection(generator);

            EditorGUILayout.Space(6);
            DrawDayNightPreviewSection(generator);

            EditorGUILayout.Space(6);
            DrawActionButtons(generator);

            EditorGUILayout.Space(8);
            DrawParameterGroups(generator);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeaderBox(DayNightCityGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("🏙️ 昼夜両対応・仮ビル群ジェネレーター", titleStyle);

            EditorGUILayout.HelpBox(
                "「昼は現実的な白亜・高反射ガラス建築」「夜はサイバー発光・オフィス窓明かり」の両方を検証するための仮設ビル群生成ツールです。\n" +
                "・大通りキャニオンにより、左右のビルの窓ガラス反射（Reflection Probe / Box Projection）を即座に確認できます。\n" +
                "・検証完了後は赤色ボタン『ビル群を一括削除』を押せば、全ビル・反射Probeを含め完全に残骸ゼロで消去されます。",
                MessageType.Info);

            string statusText = generator.HasGeneratedCity
                ? $"✅ 生成済み：{generator.CurrentBuildingCount}棟（最遠 {generator.CityLength:F0}m / 昼夜係数 {generator.PreviewNightFactor:F2}）"
                : "⚪ 未生成（下の青色ボタンを押すとビル群が生成されます）";

            EditorGUILayout.LabelField(statusText, EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetSection(DayNightCityGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 都市規模プリセット（ワンクリック適用＆生成）", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🏙️ ① キャニオン反射テスト\n(40棟 / 400m / 反射最適)", GUILayout.Height(40)))
            {
                ApplyPresetAndRebuild(generator, DayNightCityGenerator.CityScalePreset.AvenueCanyon_400m);
            }
            if (GUILayout.Button("🌆 ② 近未来グリッド街区\n(80棟 / 800m / 碁盤目)", GUILayout.Height(40)))
            {
                ApplyPresetAndRebuild(generator, DayNightCityGenerator.CityScalePreset.FutureGrid_800m);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌌 ③ 広大メガロポリス\n(160棟 / 2000m / 超遠景)", GUILayout.Height(40)))
            {
                ApplyPresetAndRebuild(generator, DayNightCityGenerator.CityScalePreset.MegaCity_2000m);
            }
            if (GUILayout.Button("🏎️ ④ 高速コース両脇\n(60棟 / 600m / 狭壁面)", GUILayout.Height(40)))
            {
                ApplyPresetAndRebuild(generator, DayNightCityGenerator.CityScalePreset.HighSpeedCorridor_600m);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDayNightPreviewSection(DayNightCityGenerator generator)
        {
            _showDayNightGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showDayNightGroup, "🌓 昼夜プレビュー・コントローラー");
            if (_showDayNightGroup)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                float newNight = EditorGUILayout.Slider("昼夜係数 (NightFactor)", _previewNightFactorProp.floatValue, 0.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    _previewNightFactorProp.floatValue = newNight;
                    serializedObject.ApplyModifiedProperties();
                    generator.UpdateNightFactor(newNight);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("☀️ 昼 (Day: 0.0)", GUILayout.Height(26)))
                {
                    SetNightFactorAndRepaint(generator, 0.0f);
                }
                if (GUILayout.Button("🌆 夕暮れ (Sunset: 0.5)", GUILayout.Height(26)))
                {
                    SetNightFactorAndRepaint(generator, 0.5f);
                }
                if (GUILayout.Button("🌙 深夜 (Night: 1.0)", GUILayout.Height(26)))
                {
                    SetNightFactorAndRepaint(generator, 1.0f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_syncDirectionalLightProp, new GUIContent("☀️ 太陽光（DirectionalLight）を昼夜に連動"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    generator.UpdateNightFactor(_previewNightFactorProp.floatValue);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionButtons(DayNightCityGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            Color prevBg = GUI.backgroundColor;

            // 生成 / 再構築ボタン
            GUI.backgroundColor = new Color(0.35f, 0.85f, 1.0f);
            string genLabel = generator.HasGeneratedCity ? "🔄 ビル群を再生成（Rebuild）" : "🏙️ ビル群を生成（Generate）";
            if (GUILayout.Button(genLabel, GUILayout.Height(36)))
            {
                Undo.RecordObject(generator, "Generate DayNight City");
                generator.GenerateCity();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            // 一括削除ボタン
            GUI.backgroundColor = generator.HasGeneratedCity ? new Color(1.0f, 0.45f, 0.45f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!generator.HasGeneratedCity);
            if (GUILayout.Button("🧹 ビル群を一括削除（Clear）", GUILayout.Height(36)))
            {
                Undo.RecordObject(generator, "Clear DayNight City");
                generator.ClearCity();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawParameterGroups(DayNightCityGenerator generator)
        {
            // 1. 配置パターン & 規模
            _showLayoutGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showLayoutGroup, "1. 配置パターン & 規模設定");
            if (_showLayoutGroup)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_presetProp, new GUIContent("適用中プリセット"));
                EditorGUILayout.PropertyField(_layoutPatternProp, new GUIContent("配置レイアウト"));
                EditorGUILayout.PropertyField(_buildingCountProp, new GUIContent("生成棟数"));
                EditorGUILayout.PropertyField(_cityLengthProp, new GUIContent("都市奥行き (m)"));
                EditorGUILayout.PropertyField(_cityWidthProp, new GUIContent("都市横幅 (m)"));
                EditorGUILayout.PropertyField(_roadWidthProp, new GUIContent("道路・キャニオン幅 (m)"));
                EditorGUILayout.PropertyField(_startDistanceProp, new GUIContent("手前開始オフセット (m)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // 2. ビル寸法 & 建築スタイル
            _showDimensionsGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showDimensionsGroup, "2. ビルの高さ・スケール・建築形状");
            if (_showDimensionsGroup)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_minHeightProp, new GUIContent("最小ビル高さ (m)"));
                EditorGUILayout.PropertyField(_maxHeightProp, new GUIContent("最大ビル高さ (m)"));
                EditorGUILayout.PropertyField(_minBuildingWidthProp, new GUIContent("最小ビル幅 (m)"));
                EditorGUILayout.PropertyField(_maxBuildingWidthProp, new GUIContent("最大ビル幅 (m)"));
                EditorGUILayout.PropertyField(_distanceHeightBiasProp, new GUIContent("遠景メガタワー比率"));
                EditorGUILayout.PropertyField(_enableComplexArchitecturesProp, new GUIContent("段状・ポディウム複合建築"));
                EditorGUILayout.PropertyField(_removeCollidersProp, new GUIContent("コライダーを自動削除"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // 3. 反射テスト & 静的フラグ
            _showReflectionGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showReflectionGroup, "3. 反射テスト & 静的フラグ");
            if (_showReflectionGroup)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_generateTestReflectionProbeProp, new GUIContent("検証用 Reflection Probe 配置"));
                if (_generateTestReflectionProbeProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_enableBoxProjectionProp, new GUIContent("Box Projection 有効化"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(_setStaticFlagsProp, new GUIContent("Static フラグ（GI / Batching）"));
                EditorGUILayout.PropertyField(_enableSrpBatchingProp, new GUIContent("SRP Batcher 最適化（DrawCall最小化）"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(2);

            // 4. マテリアル・参照設定（高度な設定）
            _showAdvancedGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvancedGroup, "4. マテリアル・詳細参照設定");
            if (_showAdvancedGroup)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_customBuildingShaderProp, new GUIContent("カスタム外壁シェーダー"));
                EditorGUILayout.PropertyField(_presetMaterialsProp, new GUIContent("外壁マテリアル配列"), true);
                EditorGUILayout.PropertyField(_referenceOriginProp, new GUIContent("基準 Transform"));
                EditorGUILayout.PropertyField(_randomSeedProp, new GUIContent("ランダムシード"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SetNightFactorAndRepaint(DayNightCityGenerator generator, float factor)
        {
            _previewNightFactorProp.floatValue = factor;
            serializedObject.ApplyModifiedProperties();
            generator.UpdateNightFactor(factor);
            SceneView.RepaintAll();
        }

        private void ApplyPresetAndRebuild(DayNightCityGenerator generator, DayNightCityGenerator.CityScalePreset preset)
        {
            Undo.RecordObject(generator, "Apply City Scale Preset");
            generator.ApplyPreset(preset);
            generator.GenerateCity();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }
    }
}
