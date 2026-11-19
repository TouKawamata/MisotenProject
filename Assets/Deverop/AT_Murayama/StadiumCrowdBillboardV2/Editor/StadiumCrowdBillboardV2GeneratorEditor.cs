using UnityEditor;
using UnityEngine;

namespace AT_Murayama.StadiumCrowdBillboardV2.Editor
{
    /// <summary>
    /// 【V2・ビルボード板ポリ版専用（完全独立）】カスタムインスペクター＆Sceneインタラクティブハンドル＋ブラシツール。
    /// ※プロジェクト規約に従い、上部メニュー（[MenuItem]）は一切使用せず、全操作を本インスペクターUIとSceneビューで完結させます。
    /// ※既存の StadiumCrowd / StadiumCrowdBillboard / StadiumCrowdV2 とは完全に独立しています。
    /// </summary>
    [CustomEditor(typeof(StadiumCrowdBillboardV2Generator))]
    public class StadiumCrowdBillboardV2GeneratorEditor : UnityEditor.Editor
    {
        private enum BrushToolMode
        {
            None = 0,
            Eraser = 1,
            Paint = 2
        }

        // 1. Billboard & Appearance
        private SerializedProperty _billboardOrientationProp;
        private SerializedProperty _verticalTiltWeightProp;
        private SerializedProperty _billboardStyleProp;
        private SerializedProperty _customSpriteTextureProp;
        private SerializedProperty _colorThemeProp;
        private SerializedProperty _outfitColorAProp;
        private SerializedProperty _outfitColorBProp;
        private SerializedProperty _outfitColorCProp;
        private SerializedProperty _outfitColorDProp;
        private SerializedProperty _pantsColorProp;
        private SerializedProperty _skinColorProp;
        private SerializedProperty _hairHatColorProp;
        private SerializedProperty _accentEmissionColorProp;
        private SerializedProperty _accentEmissionStrengthProp;
        private SerializedProperty _spectatorScaleProp;
        private SerializedProperty _heightVariationProp;

        // 2. Surface Snap & Slope Filter & Wall Avoidance
        private SerializedProperty _surfaceSnapModeProp;
        private SerializedProperty _stadiumSurfaceRootProp;
        private SerializedProperty _stadiumMeshFiltersProp;
        private SerializedProperty _minFloorNormalYProp;
        private SerializedProperty _allowDoubleSidedFloorProp;
        private SerializedProperty _excludeFlatHorizontalSurfacesProp;
        private SerializedProperty _minFloorSlopeDegProp;
        private SerializedProperty _requireInwardFacingSlopeProp;
        private SerializedProperty _autoAvoidMeshWallsProp;
        private SerializedProperty _meshWallClearanceRadiusProp;
        private SerializedProperty _wallMaxAbsNormalYProp;
        private SerializedProperty _wallDetectMinHeightAboveFloorProp;
        private SerializedProperty _checkOverheadHeadroomProp;
        private SerializedProperty _minHeadroomProp;

        // 3. Tiers
        private SerializedProperty _tiersProp;
        private SerializedProperty _randomSeedProp;

        // 4. Cutouts & Radial Walls
        private SerializedProperty _angularCutoutsProp;
        private SerializedProperty _enablePeriodicWallsProp;
        private SerializedProperty _periodicWallCountProp;
        private SerializedProperty _periodicWallOffsetDegProp;
        private SerializedProperty _periodicWallThicknessMetersProp;
        private SerializedProperty _periodicWallMarginMetersProp;
        private SerializedProperty _periodicWallTierIndexProp;
        private SerializedProperty _radialWallsProp;

        // 5. Tracking & Animation
        private SerializedProperty _trackingModeProp;
        private SerializedProperty _lookAtTargetProp;
        private SerializedProperty _autoFindPlayerAtRuntimeProp;
        private SerializedProperty _simStartHeightProp;
        private SerializedProperty _simApproachOffsetXZProp;
        private SerializedProperty _simLandingPointLocalProp;
        private SerializedProperty _simCycleDurationProp;
        private SerializedProperty _manualLandingProgressProp;
        private SerializedProperty _previewInEditModeProp;
        private SerializedProperty _showTargetMarkerMeshProp;
        private SerializedProperty _lookAtWeightProp;
        private SerializedProperty _reactionLagWeightProp;
        private SerializedProperty _reactionLagSecondsProp;
        private SerializedProperty _baseExcitementProp;
        private SerializedProperty _boostExcitementOnLandingProp;
        private SerializedProperty _animSpeedProp;
        private SerializedProperty _jumpHeightProp;
        private SerializedProperty _jumpRatioProp;
        private SerializedProperty _swayAmountProp;
        private SerializedProperty _armWaveAmountProp;
        private SerializedProperty _waveIntensityProp;
        private SerializedProperty _waveSpeedProp;

        // 6. Distant Optimization & Bake
        private SerializedProperty _animCullDistanceProp;
        private SerializedProperty _maxRenderDistanceProp;
        private SerializedProperty _castShadowsProp;
        private SerializedProperty _pauseTrackingWhenFarProp;
        private SerializedProperty _applyLayerCullDistanceProp;
        private SerializedProperty _layerCullDistanceProp;
        private SerializedProperty _isBakedAssetsProp;
        private SerializedProperty _sharedBillboardMaterialProp;
        private SerializedProperty _sharedStandMaterialProp;

        // Editor Foldout & Scene Tool States
        private bool _autoRebuildOnParamChange = true;
        private bool _showSceneHandles = true;
        private bool _showTierGuideRings = true;
        private bool _showSnapYWindows = false;

        private BrushToolMode _brushToolMode = BrushToolMode.None;
        private float _brushRadius = 3.0f;
        private float _paintSeatSpacing = 0.85f;
        private bool _showEraseStampsInScene = true;
        private bool _hasHoverHit;
        private Vector3 _hoverLocalPos;
        private Vector3 _hoverLocalNormal = Vector3.up;
        private bool _isBrushStrokeActive;
        private bool _strokeHasUnflushedChanges;
        private double _lastBrushApplyTime;
        private double _lastBrushMeshRebuildTime;

        private bool _showSurfaceSection = true;
        private bool _showTiersSection = true;
        private bool _showCutoutsSection = false;
        private bool _showAppearanceSection = true;
        private bool _showTrackingSection = false;
        private bool _showOptimizationSection = true;

        private bool _pendingRebuildAfterDrag;

        private void OnEnable()
        {
            _billboardOrientationProp = serializedObject.FindProperty("_billboardOrientation");
            _verticalTiltWeightProp = serializedObject.FindProperty("_verticalTiltWeight");
            _billboardStyleProp = serializedObject.FindProperty("_billboardStyle");
            _customSpriteTextureProp = serializedObject.FindProperty("_customSpriteTexture");
            _colorThemeProp = serializedObject.FindProperty("_colorTheme");
            _outfitColorAProp = serializedObject.FindProperty("_outfitColorA");
            _outfitColorBProp = serializedObject.FindProperty("_outfitColorB");
            _outfitColorCProp = serializedObject.FindProperty("_outfitColorC");
            _outfitColorDProp = serializedObject.FindProperty("_outfitColorD");
            _pantsColorProp = serializedObject.FindProperty("_pantsColor");
            _skinColorProp = serializedObject.FindProperty("_skinColor");
            _hairHatColorProp = serializedObject.FindProperty("_hairHatColor");
            _accentEmissionColorProp = serializedObject.FindProperty("_accentEmissionColor");
            _accentEmissionStrengthProp = serializedObject.FindProperty("_accentEmissionStrength");
            _spectatorScaleProp = serializedObject.FindProperty("_spectatorScale");
            _heightVariationProp = serializedObject.FindProperty("_heightVariation");

            _surfaceSnapModeProp = serializedObject.FindProperty("_surfaceSnapMode");
            _stadiumSurfaceRootProp = serializedObject.FindProperty("_stadiumSurfaceRoot");
            _stadiumMeshFiltersProp = serializedObject.FindProperty("_stadiumMeshFilters");
            _minFloorNormalYProp = serializedObject.FindProperty("_minFloorNormalY");
            _allowDoubleSidedFloorProp = serializedObject.FindProperty("_allowDoubleSidedFloor");
            _excludeFlatHorizontalSurfacesProp = serializedObject.FindProperty("_excludeFlatHorizontalSurfaces");
            _minFloorSlopeDegProp = serializedObject.FindProperty("_minFloorSlopeDeg");
            _requireInwardFacingSlopeProp = serializedObject.FindProperty("_requireInwardFacingSlope");
            _autoAvoidMeshWallsProp = serializedObject.FindProperty("_autoAvoidMeshWalls");
            _meshWallClearanceRadiusProp = serializedObject.FindProperty("_meshWallClearanceRadius");
            _wallMaxAbsNormalYProp = serializedObject.FindProperty("_wallMaxAbsNormalY");
            _wallDetectMinHeightAboveFloorProp = serializedObject.FindProperty("_wallDetectMinHeightAboveFloor");
            _checkOverheadHeadroomProp = serializedObject.FindProperty("_checkOverheadHeadroom");
            _minHeadroomProp = serializedObject.FindProperty("_minHeadroom");

            _tiersProp = serializedObject.FindProperty("_tiers");
            _randomSeedProp = serializedObject.FindProperty("_randomSeed");

            _angularCutoutsProp = serializedObject.FindProperty("_angularCutouts");
            _enablePeriodicWallsProp = serializedObject.FindProperty("_enablePeriodicWalls");
            _periodicWallCountProp = serializedObject.FindProperty("_periodicWallCount");
            _periodicWallOffsetDegProp = serializedObject.FindProperty("_periodicWallOffsetDeg");
            _periodicWallThicknessMetersProp = serializedObject.FindProperty("_periodicWallThicknessMeters");
            _periodicWallMarginMetersProp = serializedObject.FindProperty("_periodicWallMarginMeters");
            _periodicWallTierIndexProp = serializedObject.FindProperty("_periodicWallTierIndex");
            _radialWallsProp = serializedObject.FindProperty("_radialWalls");

            _trackingModeProp = serializedObject.FindProperty("_trackingMode");
            _lookAtTargetProp = serializedObject.FindProperty("_lookAtTarget");
            _autoFindPlayerAtRuntimeProp = serializedObject.FindProperty("_autoFindPlayerAtRuntime");
            _simStartHeightProp = serializedObject.FindProperty("_simStartHeight");
            _simApproachOffsetXZProp = serializedObject.FindProperty("_simApproachOffsetXZ");
            _simLandingPointLocalProp = serializedObject.FindProperty("_simLandingPointLocal");
            _simCycleDurationProp = serializedObject.FindProperty("_simCycleDuration");
            _manualLandingProgressProp = serializedObject.FindProperty("_manualLandingProgress");
            _previewInEditModeProp = serializedObject.FindProperty("_previewInEditMode");
            _showTargetMarkerMeshProp = serializedObject.FindProperty("_showTargetMarkerMesh");
            _lookAtWeightProp = serializedObject.FindProperty("_lookAtWeight");
            _reactionLagWeightProp = serializedObject.FindProperty("_reactionLagWeight");
            _reactionLagSecondsProp = serializedObject.FindProperty("_reactionLagSeconds");
            _baseExcitementProp = serializedObject.FindProperty("_baseExcitement");
            _boostExcitementOnLandingProp = serializedObject.FindProperty("_boostExcitementOnLanding");
            _animSpeedProp = serializedObject.FindProperty("_animSpeed");
            _jumpHeightProp = serializedObject.FindProperty("_jumpHeight");
            _jumpRatioProp = serializedObject.FindProperty("_jumpRatio");
            _swayAmountProp = serializedObject.FindProperty("_swayAmount");
            _armWaveAmountProp = serializedObject.FindProperty("_armWaveAmount");
            _waveIntensityProp = serializedObject.FindProperty("_waveIntensity");
            _waveSpeedProp = serializedObject.FindProperty("_waveSpeed");

            _animCullDistanceProp = serializedObject.FindProperty("_animCullDistance");
            _maxRenderDistanceProp = serializedObject.FindProperty("_maxRenderDistance");
            _castShadowsProp = serializedObject.FindProperty("_castShadows");
            _pauseTrackingWhenFarProp = serializedObject.FindProperty("_pauseTrackingWhenFar");
            _applyLayerCullDistanceProp = serializedObject.FindProperty("_applyLayerCullDistance");
            _layerCullDistanceProp = serializedObject.FindProperty("_layerCullDistance");
            _isBakedAssetsProp = serializedObject.FindProperty("_isBakedAssets");
            _sharedBillboardMaterialProp = serializedObject.FindProperty("_sharedBillboardMaterial");
            _sharedStandMaterialProp = serializedObject.FindProperty("_sharedStandMaterial");

            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            StadiumCrowdBillboardV2Generator gen = target as StadiumCrowdBillboardV2Generator;
            if (gen != null)
            {
                if (_strokeHasUnflushedChanges)
                {
                    gen.FlushBrushMeshRebuild();
                    _strokeHasUnflushedChanges = false;
                }
                gen.EndBrushSession();
            }
            _brushToolMode = BrushToolMode.None;
            _isBrushStrokeActive = false;
        }

        private void OnUndoRedoPerformed()
        {
            StadiumCrowdBillboardV2Generator gen = target as StadiumCrowdBillboardV2Generator;
            if (gen != null && !Application.isPlaying)
            {
                gen.GenerateCrowd(false);
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void SetBrushMode(StadiumCrowdBillboardV2Generator gen, BrushToolMode nextMode)
        {
            if (_brushToolMode == nextMode) return;
            _brushToolMode = nextMode;
            _isBrushStrokeActive = false;

            if (gen != null)
            {
                if (_brushToolMode != BrushToolMode.None)
                {
                    gen.BeginBrushSession();
                }
                else
                {
                    if (_strokeHasUnflushedChanges)
                    {
                        gen.FlushBrushMeshRebuild();
                        _strokeHasUnflushedChanges = false;
                    }
                    gen.EndBrushSession();
                }
            }
            SceneView.RepaintAll();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StadiumCrowdBillboardV2Generator gen = (StadiumCrowdBillboardV2Generator)target;
            gen.EnsureDefaultTiers();

            DrawHeaderStatus(gen);
            EditorGUILayout.Space(4);
            DrawMainActionBar(gen);
            EditorGUILayout.Space(4);
            DrawBrushToolPanel(gen);
            EditorGUILayout.Space(6);

            bool needMeshRebuild = false;
            bool needSurfaceResample = false;
            bool needMaterialUpdate = false;

            string[] tierPopupNames = BuildTierScopePopupNames(gen);

            // =================================================================
            // Section 1: 3Dモデル床面スナップ・斜面限定フィルタ・自動壁回避
            // =================================================================
            _showSurfaceSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showSurfaceSection, "1. 山なり床スナップ ＆ 斜面限定フィルタ（水平面除外）");
            if (_showSurfaceSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_surfaceSnapModeProp, new GUIContent("床面フィッティング方式"));
                EditorGUILayout.PropertyField(_stadiumSurfaceRootProp, new GUIContent("スタジアム3Dモデル (親Transform)"));
                EditorGUILayout.PropertyField(_stadiumMeshFiltersProp, new GUIContent("追加スナップMesh (任意)"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    needMeshRebuild = true;
                    needSurfaceResample = true;
                }

                StadiumCrowdBillboardV2Generator.SurfaceSnapMode snapMode = (StadiumCrowdBillboardV2Generator.SurfaceSnapMode)_surfaceSnapModeProp.enumValueIndex;
                if (snapMode != StadiumCrowdBillboardV2Generator.SurfaceSnapMode.MathAndWaveOnly)
                {
                    if (!gen.HasAssignedSurfaceTarget)
                    {
                        EditorGUILayout.HelpBox(
                            "スタジアム3Dモデルが未指定のため、現在は数式高さで仮表示中です。\n上の「スタジアム3Dモデル (親Transform)」にスタジアムオブジェクトをドラッグ＆ドロップすると、客席の傾斜に自動着地します。",
                            MessageType.Info);
                    }
                    else
                    {
                        string snapStats = $"床スナップ結果: {gen.LastTotalSnappedCount:N0}人着地 / 読込メッシュ {gen.LastSampledMeshCount}個 (壁回避: {gen.LastTotalWallRejectedCount}席, 天井回避: {gen.LastTotalHeadroomRejectedCount}席)";
                        EditorGUILayout.HelpBox(snapStats, MessageType.Info);

                        if (gen.LastNonReadableFallbackCount > 0)
                        {
                            EditorGUILayout.HelpBox(
                                $"補足: {gen.LastNonReadableFallbackCount}個のメッシュは Read/Write=OFF のため一時Colliderフォールバックで吸着しました（元オブジェクト無改変）。",
                                MessageType.None);
                        }
                        if (gen.LastSkippedUnreadableCount > 0)
                        {
                            EditorGUILayout.HelpBox(
                                $"注意: {gen.LastSkippedUnreadableCount}個のメッシュは読み取りも一時Collider化もできずスキップされました。FBXの Read/Write Enabled を確認してください。",
                                MessageType.Warning);
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("★ 誤配置防止フィルタ（地面・柱の上・外壁屋根・モニター枠を除外）", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(_excludeFlatHorizontalSurfacesProp, new GUIContent("水平面を除外し客席スロープ(斜面)のみ配置"));
                    if (_excludeFlatHorizontalSurfacesProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_minFloorSlopeDegProp, new GUIContent("最小傾斜角 (° / 既定3.5°で水平面除外)"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(_requireInwardFacingSlopeProp, new GUIContent("中心向きの斜面のみ許可 (外向き外壁を除外)"));

                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_minFloorNormalYProp, new GUIContent("床面の法線Y下限 (急斜面・壁の除外)"));
                    EditorGUILayout.PropertyField(_allowDoubleSidedFloorProp, new GUIContent("両面床メッシュを許可 (通常OFF)"));

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("3Dモデル壁・天井の自動回避", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(_autoAvoidMeshWallsProp, new GUIContent("縦壁(仕切り壁等)を自動回避"));
                    if (_autoAvoidMeshWallsProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_meshWallClearanceRadiusProp, new GUIContent("壁からの自動回避半径 (m)"));
                        EditorGUILayout.PropertyField(_wallDetectMinHeightAboveFloorProp, new GUIContent("壁とみなす床からの最低高さ (m)"));
                        EditorGUILayout.PropertyField(_wallMaxAbsNormalYProp, new GUIContent("縦壁判定の法線|Y|上限"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.PropertyField(_checkOverheadHeadroomProp, new GUIContent("頭上クリアランス判定 (2階裏側回避)"));
                    if (_checkOverheadHeadroomProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_minHeadroomProp, new GUIContent("必要頭上高さ (m)"));
                        EditorGUI.indentLevel--;
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        needMeshRebuild = true;
                        needSurfaceResample = true;
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // =================================================================
            // Section 2: マルチ階層 ＆ 区画スタンプ管理
            // =================================================================
            _showTiersSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showTiersSection, $"2. 階層・区画スタンプ管理（現在 {_tiersProp.arraySize} 個）");
            if (_showTiersSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.HelpBox(
                    "各階層の「🎯 編集」をONにすると、Sceneビューで【シアン球=前列内径】【緑球=奥行き(段数)】【白キューブ=区画の左右端】を直接ドラッグして客席の奥端まで広げられます。",
                    MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.70f);
                if (GUILayout.Button("➕ 全周階層を追加 (3F〜)", GUILayout.Height(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Add Billboard Crowd Tier");
                    gen.AddNewTier();
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                    needMeshRebuild = true;
                }

                GUI.backgroundColor = new Color(0.50f, 0.80f, 1.0f);
                if (GUILayout.Button("➕ 区画ブロック追加 (扇形スタンプ)", GUILayout.Height(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Add Billboard Sector Block");
                    gen.AddSectorBlockTier();
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                    needMeshRebuild = true;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_randomSeedProp, new GUIContent("ランダム配置シード"));
                if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;

                EditorGUILayout.Space(4);

                int tierToRemove = -1;
                int tierToDuplicate = -1;

                for (int i = 0; i < _tiersProp.arraySize; i++)
                {
                    SerializedProperty tierProp = _tiersProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = tierProp.FindPropertyRelative("tierName");
                    SerializedProperty enabledProp = tierProp.FindPropertyRelative("enabled");
                    SerializedProperty colorProp = tierProp.FindPropertyRelative("gizmoColor");
                    SerializedProperty editSceneProp = tierProp.FindPropertyRelative("editInScene");

                    int tierGenCount = (i < gen.Tiers.Count && gen.Tiers[i] != null) ? gen.Tiers[i].lastGeneratedCount : 0;
                    int tierSnapCount = (i < gen.Tiers.Count && gen.Tiers[i] != null) ? gen.Tiers[i].lastSnappedCount : 0;

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(18));
                    if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;

                    tierProp.isExpanded = EditorGUILayout.Foldout(
                        tierProp.isExpanded,
                        $"#{i + 1} {nameProp.stringValue} ({tierGenCount:N0}人 / 吸着:{tierSnapCount:N0})",
                        true,
                        EditorStyles.foldoutHeader);

                    GUI.backgroundColor = editSceneProp.boolValue ? new Color(0.35f, 0.90f, 1.0f) : Color.white;
                    if (GUILayout.Button("🎯 編集", GUILayout.Width(54)))
                    {
                        editSceneProp.boolValue = !editSceneProp.boolValue;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = Color.white;

                    if (GUILayout.Button(new GUIContent("📋", "この階層/区画を複製"), GUILayout.Width(28)))
                    {
                        tierToDuplicate = i;
                    }

                    colorProp.colorValue = EditorGUILayout.ColorField(GUIContent.none, colorProp.colorValue, false, false, false, GUILayout.Width(24));

                    if (_tiersProp.arraySize > 1 && GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        tierToRemove = i;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (tierProp.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();

                        EditorGUILayout.PropertyField(nameProp, new GUIContent("階層 / 区画名"));

                        SerializedProperty rxProp = tierProp.FindPropertyRelative("innerRadiusX");
                        SerializedProperty rzProp = tierProp.FindPropertyRelative("innerRadiusZ");
                        SerializedProperty rowCountProp = tierProp.FindPropertyRelative("rowCount");
                        SerializedProperty rowDepthProp = tierProp.FindPropertyRelative("rowDepth");

                        EditorGUILayout.LabelField("円弧・奥行き形状", EditorStyles.miniBoldLabel);
                        EditorGUILayout.PropertyField(rxProp, new GUIContent("内径 X (m)"));
                        EditorGUILayout.PropertyField(rzProp, new GUIContent("内径 Z (m)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("straightTrackLength"), new GUIContent("直線延長 (m)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("startAngleDeg"), new GUIContent("開始角度 (°)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("arcAngleDeg"), new GUIContent("区画の角度幅 (360=全周)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("baseHeightY"), new GUIContent("基準高さ Y (m)"));
                        EditorGUILayout.PropertyField(rowCountProp, new GUIContent("段数 (Row Count / 奥行き)"));
                        EditorGUILayout.PropertyField(rowDepthProp, new GUIContent("1段の奥行き (m)"));

                        float outerEstX = rxProp.floatValue + Mathf.Max(0, rowCountProp.intValue - 1) * rowDepthProp.floatValue;
                        EditorGUILayout.LabelField($"現在の外周端(奥) 半径目安: 約 {outerEstX:0.1} m", EditorStyles.miniLabel);

                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("rowRise"), new GUIContent("1段の高さ上昇 (m)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("seatSpacing"), new GUIContent("座席の横間隔 (m)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("staggerRows"), new GUIContent("互い違い配置 (千鳥配列)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("positionJitter"), new GUIContent("立ち位置ランダム揺らぎ"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("emptySeatRatio"), new GUIContent("空席率 (0=満員)"));
                        EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("generateFallbackStandMesh"), new GUIContent("仮ひな壇メッシュを生成"));

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("山なり・上下うねりオフセット (数式 / カーブ)", EditorStyles.miniBoldLabel);
                        SerializedProperty waveEnableProp = tierProp.FindPropertyRelative("enableElevationWave");
                        EditorGUILayout.PropertyField(waveEnableProp, new GUIContent("山なりサイン波を有効にする"));
                        if (waveEnableProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("wavePeakCount"), new GUIContent("山の数 (例: 3山)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("waveAmplitudeY"), new GUIContent("山の高低差 Y (m)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("wavePhaseDeg"), new GUIContent("山の頂点角度位相 (°)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("waveExponent"), new GUIContent("山形のカーブ鋭さ"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("waveOuterRowScale"), new GUIContent("後列へのうねり倍率"));
                            EditorGUI.indentLevel--;
                        }

                        SerializedProperty curveEnableProp = tierProp.FindPropertyRelative("useCustomElevationCurve");
                        EditorGUILayout.PropertyField(curveEnableProp, new GUIContent("自由カーブ (0〜360°) を加算"));
                        if (curveEnableProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("customElevationCurve"), new GUIContent("周回高さカーブ (0..1)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("customCurveScaleY"), new GUIContent("カーブ高さ倍率 (m)"));
                            EditorGUI.indentLevel--;
                        }

                        if (_surfaceSnapModeProp.enumValueIndex != (int)StadiumCrowdBillboardV2Generator.SurfaceSnapMode.MathAndWaveOnly)
                        {
                            EditorGUILayout.Space(4);
                            EditorGUILayout.LabelField("3D床スナップ高さスキャン範囲 (1F / 2F 分離用)", EditorStyles.miniBoldLabel);
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("snapPolicy"), new GUIContent("複数床ヒット時の選び方"));
                            SerializedProperty useAbsProp = tierProp.FindPropertyRelative("useAbsoluteYBounds");
                            EditorGUILayout.PropertyField(useAbsProp, new GUIContent("絶対Y範囲で厳格制限 (推奨)"));
                            if (useAbsProp.boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("snapMinY"), new GUIContent("スキャン下限 Y (m)"));
                                EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("snapMaxY"), new GUIContent("スキャン上限 Y (m)"));
                                EditorGUI.indentLevel--;
                            }
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("snapSearchUp"), new GUIContent("予測Yからの上探索幅 (m)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("snapSearchDown"), new GUIContent("予測Yからの下探索幅 (m)"));
                            EditorGUILayout.PropertyField(tierProp.FindPropertyRelative("surfaceYOffset"), new GUIContent("足元Y微調整オフセット (m)"));
                        }

                        if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                if (tierToDuplicate >= 0)
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Duplicate Billboard Crowd Tier");
                    gen.DuplicateTier(tierToDuplicate);
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                    needMeshRebuild = true;
                }
                else if (tierToRemove >= 0)
                {
                    _tiersProp.DeleteArrayElementAtIndex(tierToRemove);
                    needMeshRebuild = true;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // =================================================================
            // Section 3: 開口アーク ＆ 放射仕切り壁カッター
            // =================================================================
            _showCutoutsSection = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showCutoutsSection,
                $"3. 開口アーク ({_angularCutoutsProp.arraySize}) ＆ 仕切り壁カッター ({_radialWallsProp.arraySize})");
            if (_showCutoutsSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(1.0f, 0.50f, 0.50f);
                if (GUILayout.Button("➕ モニター開口追加", GUILayout.Height(24)))
                {
                    AddCutoutEntry("巨大モニター開口", 0f, 40f, new Color(1.0f, 0.22f, 0.32f, 0.85f));
                    needMeshRebuild = true;
                }
                GUI.backgroundColor = new Color(1.0f, 0.65f, 0.35f);
                if (GUILayout.Button("➕ 発射口開口追加", GUILayout.Height(24)))
                {
                    AddCutoutEntry("発射口ゲート開口", 180f, 30f, new Color(1.0f, 0.50f, 0.15f, 0.85f));
                    needMeshRebuild = true;
                }
                GUI.backgroundColor = new Color(1.0f, 0.90f, 0.40f);
                if (GUILayout.Button("➕ 仕切り壁追加", GUILayout.Height(24)))
                {
                    float nextAngle = (_radialWallsProp.arraySize * 60f) % 360f;
                    AddRadialWallEntry($"仕切り壁 #{_radialWallsProp.arraySize + 1}", nextAngle, 2.6f, 0.5f);
                    needMeshRebuild = true;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("■ 角度開口アークリスト（巨大モニター / 発射口 / 大開口）", EditorStyles.boldLabel);

                int cutoutRemoveIdx = -1;
                for (int i = 0; i < _angularCutoutsProp.arraySize; i++)
                {
                    SerializedProperty item = _angularCutoutsProp.GetArrayElementAtIndex(i);
                    SerializedProperty labelProp = item.FindPropertyRelative("label");
                    SerializedProperty enabledProp = item.FindPropertyRelative("enabled");
                    SerializedProperty centerProp = item.FindPropertyRelative("centerAngleDeg");
                    SerializedProperty widthProp = item.FindPropertyRelative("arcWidthDeg");
                    SerializedProperty scopeProp = item.FindPropertyRelative("targetTierIndex");
                    SerializedProperty limitRProp = item.FindPropertyRelative("limitRadiusRange");
                    SerializedProperty minRProp = item.FindPropertyRelative("minRadius");
                    SerializedProperty maxRProp = item.FindPropertyRelative("maxRadius");
                    SerializedProperty colProp = item.FindPropertyRelative("gizmoColor");

                    EditorGUILayout.BeginVertical("box");
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(18));
                    labelProp.stringValue = EditorGUILayout.TextField(labelProp.stringValue);
                    colProp.colorValue = EditorGUILayout.ColorField(GUIContent.none, colProp.colorValue, false, false, false, GUILayout.Width(28));
                    if (GUILayout.Button("✕", GUILayout.Width(22))) cutoutRemoveIdx = i;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Slider(centerProp, 0f, 360f, new GUIContent("中心角度 (°)"));
                    EditorGUILayout.Slider(widthProp, 1f, 355f, new GUIContent("開口角度幅 (°)"));
                    scopeProp.intValue = DrawTierScopePopup("対象階層", scopeProp.intValue, tierPopupNames);

                    EditorGUILayout.PropertyField(limitRProp, new GUIContent("半径範囲を限定する"));
                    if (limitRProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(minRProp, new GUIContent("最小半径 (m)"));
                        EditorGUILayout.PropertyField(maxRProp, new GUIContent("最大半径 (m)"));
                        EditorGUI.indentLevel--;
                    }
                    if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;
                    EditorGUILayout.EndVertical();
                }
                if (cutoutRemoveIdx >= 0)
                {
                    _angularCutoutsProp.DeleteArrayElementAtIndex(cutoutRemoveIdx);
                    needMeshRebuild = true;
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("■ 放射状の仕切り壁カッター（メートル平行幅カット）", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_enablePeriodicWallsProp, new GUIContent("均等分割の仕切り壁を有効化 (例: 3分割=120°毎)"));
                if (_enablePeriodicWallsProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_periodicWallCountProp, new GUIContent("均等壁の本数"));
                    EditorGUILayout.PropertyField(_periodicWallOffsetDegProp, new GUIContent("基準角度オフセット (°)"));
                    EditorGUILayout.PropertyField(_periodicWallThicknessMetersProp, new GUIContent("壁の厚み (m)"));
                    EditorGUILayout.PropertyField(_periodicWallMarginMetersProp, new GUIContent("壁の左右余白 (m)"));
                    _periodicWallTierIndexProp.intValue = DrawTierScopePopup("対象階層", _periodicWallTierIndexProp.intValue, tierPopupNames);
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;

                EditorGUILayout.Space(4);
                int wallRemoveIdx = -1;
                for (int i = 0; i < _radialWallsProp.arraySize; i++)
                {
                    SerializedProperty item = _radialWallsProp.GetArrayElementAtIndex(i);
                    SerializedProperty labelProp = item.FindPropertyRelative("label");
                    SerializedProperty enabledProp = item.FindPropertyRelative("enabled");
                    SerializedProperty angleProp = item.FindPropertyRelative("angleDeg");
                    SerializedProperty thickProp = item.FindPropertyRelative("thicknessMeters");
                    SerializedProperty marginProp = item.FindPropertyRelative("marginMeters");
                    SerializedProperty scopeProp = item.FindPropertyRelative("targetTierIndex");
                    SerializedProperty limitRProp = item.FindPropertyRelative("limitRadiusRange");
                    SerializedProperty minRProp = item.FindPropertyRelative("minRadius");
                    SerializedProperty maxRProp = item.FindPropertyRelative("maxRadius");
                    SerializedProperty colProp = item.FindPropertyRelative("gizmoColor");

                    EditorGUILayout.BeginVertical("box");
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(18));
                    labelProp.stringValue = EditorGUILayout.TextField(labelProp.stringValue);
                    colProp.colorValue = EditorGUILayout.ColorField(GUIContent.none, colProp.colorValue, false, false, false, GUILayout.Width(28));
                    if (GUILayout.Button("✕", GUILayout.Width(22))) wallRemoveIdx = i;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Slider(angleProp, 0f, 360f, new GUIContent("壁の角度 (°)"));
                    EditorGUILayout.PropertyField(thickProp, new GUIContent("壁本体の厚み (m)"));
                    EditorGUILayout.PropertyField(marginProp, new GUIContent("壁の左右安全余白 (m)"));
                    scopeProp.intValue = DrawTierScopePopup("対象階層", scopeProp.intValue, tierPopupNames);

                    EditorGUILayout.PropertyField(limitRProp, new GUIContent("半径範囲を限定する"));
                    if (limitRProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(minRProp, new GUIContent("最小半径 (m)"));
                        EditorGUILayout.PropertyField(maxRProp, new GUIContent("最大半径 (m)"));
                        EditorGUI.indentLevel--;
                    }
                    if (EditorGUI.EndChangeCheck()) needMeshRebuild = true;
                    EditorGUILayout.EndVertical();
                }
                if (wallRemoveIdx >= 0)
                {
                    _radialWallsProp.DeleteArrayElementAtIndex(wallRemoveIdx);
                    needMeshRebuild = true;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // =================================================================
            // Section 4: ビルボード方式・見た目・カラーテーマ
            // =================================================================
            _showAppearanceSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showAppearanceSection, "4. ビルボード方式・スタイル・カラーテーマ");
            if (_showAppearanceSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                int prevOrientation = _billboardOrientationProp.enumValueIndex;
                EditorGUILayout.PropertyField(_billboardOrientationProp, new GUIContent("ビルボード向き制御モード"));
                if (_billboardOrientationProp.enumValueIndex == (int)StadiumCrowdBillboardV2Generator.BillboardOrientation.HybridSkyTilt)
                {
                    EditorGUILayout.PropertyField(_verticalTiltWeightProp, new GUIContent("上空俯瞰時のチルト補正強度"));
                }
                if (EditorGUI.EndChangeCheck())
                {
                    // CrossQuad (8頂点) と通常 (4頂点) を切り替えた場合はメッシュ再構築
                    bool wasCross = prevOrientation == (int)StadiumCrowdBillboardV2Generator.BillboardOrientation.TargetCrossQuad;
                    bool isCross = _billboardOrientationProp.enumValueIndex == (int)StadiumCrowdBillboardV2Generator.BillboardOrientation.TargetCrossQuad;
                    if (wasCross != isCross) needMeshRebuild = true;
                    else needMaterialUpdate = true;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_billboardStyleProp, new GUIContent("ビルボード見た目スタイル"));
                if (_billboardStyleProp.enumValueIndex == (int)StadiumCrowdBillboardV2Generator.BillboardStyle.CustomSpriteTexture)
                {
                    EditorGUILayout.PropertyField(_customSpriteTextureProp, new GUIContent("差し替え2D画像 (RGBA)"));
                }
                EditorGUILayout.PropertyField(_spectatorScaleProp, new GUIContent("観客スケール倍率"));
                if (EditorGUI.EndChangeCheck())
                {
                    needMeshRebuild = true;
                    needMaterialUpdate = true;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_colorThemeProp, new GUIContent("カラーテーマ"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Change BillboardCrowdV2 Color Theme");
                    gen.ApplyColorThemePreset((StadiumCrowdBillboardV2Generator.CrowdColorTheme)_colorThemeProp.enumValueIndex);
                    serializedObject.Update();
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_outfitColorAProp, new GUIContent("服カラー 1 (メイン)"));
                EditorGUILayout.PropertyField(_outfitColorBProp, new GUIContent("服カラー 2 (サブ)"));
                EditorGUILayout.PropertyField(_outfitColorCProp, new GUIContent("服カラー 3 (明色)"));
                EditorGUILayout.PropertyField(_outfitColorDProp, new GUIContent("服カラー 4 (アクセント)"));
                EditorGUILayout.PropertyField(_pantsColorProp, new GUIContent("ボトムス / パンツ色"));
                EditorGUILayout.PropertyField(_skinColorProp, new GUIContent("肌の色"));
                EditorGUILayout.PropertyField(_hairHatColorProp, new GUIContent("髪 / キャップ基本色"));
                EditorGUILayout.PropertyField(_accentEmissionColorProp, new GUIContent("バイザー/旗 発光色"));
                EditorGUILayout.PropertyField(_accentEmissionStrengthProp, new GUIContent("発光アクセント強度"));
                EditorGUILayout.PropertyField(_heightVariationProp, new GUIContent("身長・体格バラつき"));
                if (EditorGUI.EndChangeCheck())
                {
                    _colorThemeProp.enumValueIndex = (int)StadiumCrowdBillboardV2Generator.CrowdColorTheme.Custom;
                    needMaterialUpdate = true;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // =================================================================
            // Section 5: 視線追従・降下シミュレーター・歓声設定
            // =================================================================
            _showTrackingSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showTrackingSection, "5. 視線追従・降下シミュレーター・歓声設定");
            if (_showTrackingSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_trackingModeProp, new GUIContent("追従テスト / 本番モード"));
                StadiumCrowdBillboardV2Generator.TargetTrackingMode tMode = (StadiumCrowdBillboardV2Generator.TargetTrackingMode)_trackingModeProp.enumValueIndex;

                if (tMode == StadiumCrowdBillboardV2Generator.TargetTrackingMode.SkyDiveSimulation)
                {
                    EditorGUILayout.PropertyField(_simStartHeightProp, new GUIContent("上空の開始高度 (m)"));
                    EditorGUILayout.PropertyField(_simApproachOffsetXZProp, new GUIContent("上空の進入オフセットXZ"));
                    EditorGUILayout.PropertyField(_simLandingPointLocalProp, new GUIContent("着地地点ローカル座標"));
                    EditorGUILayout.PropertyField(_simCycleDurationProp, new GUIContent("降下ループ周期 (秒)"));
                }
                else if (tMode == StadiumCrowdBillboardV2Generator.TargetTrackingMode.ManualAltitudeScrub)
                {
                    EditorGUILayout.PropertyField(_manualLandingProgressProp, new GUIContent("降下進行度 (0=上空 → 1=着地)"));
                    EditorGUILayout.PropertyField(_simStartHeightProp, new GUIContent("上空の開始高度 (m)"));
                    EditorGUILayout.PropertyField(_simApproachOffsetXZProp, new GUIContent("上空の進入オフセットXZ"));
                    EditorGUILayout.PropertyField(_simLandingPointLocalProp, new GUIContent("着地地点ローカル座標"));
                }
                else
                {
                    EditorGUILayout.PropertyField(_lookAtTargetProp, new GUIContent("追従ターゲットTransform"));
                    EditorGUILayout.PropertyField(_autoFindPlayerAtRuntimeProp, new GUIContent("実行時にPlayerを自動検索"));
                    EditorGUILayout.PropertyField(_simLandingPointLocalProp, new GUIContent("着地判定の中心座標"));
                }

                EditorGUILayout.PropertyField(_previewInEditModeProp, new GUIContent("エディタ非再生時も常時プレビュー"));
                EditorGUILayout.PropertyField(_showTargetMarkerMeshProp, new GUIContent("ターゲット位置マーカー球を表示"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("視線追従・歓声パラメータ", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_lookAtWeightProp, new GUIContent("視線・体幹の追従強度"));
                EditorGUILayout.PropertyField(_reactionLagWeightProp, new GUIContent("振り向き反応ラグの個体差"));
                EditorGUILayout.PropertyField(_reactionLagSecondsProp, new GUIContent("遅れ追従の遅延時間 (秒)"));
                EditorGUILayout.PropertyField(_baseExcitementProp, new GUIContent("基本の歓声・盛り上がり度"));
                EditorGUILayout.PropertyField(_boostExcitementOnLandingProp, new GUIContent("着地時に大歓声バースト"));
                EditorGUILayout.PropertyField(_animSpeedProp, new GUIContent("モーション全体速度"));
                EditorGUILayout.PropertyField(_jumpHeightProp, new GUIContent("ジャンプ最大高さ (m)"));
                EditorGUILayout.PropertyField(_jumpRatioProp, new GUIContent("ジャンプする人の割合"));
                EditorGUILayout.PropertyField(_swayAmountProp, new GUIContent("体の左右リズム揺れ幅"));
                EditorGUILayout.PropertyField(_armWaveAmountProp, new GUIContent("腕振りの大きさ"));
                EditorGUILayout.PropertyField(_waveIntensityProp, new GUIContent("スタジアムウェーブ強度"));
                EditorGUILayout.PropertyField(_waveSpeedProp, new GUIContent("スタジアムウェーブ速度"));

                if (EditorGUI.EndChangeCheck()) needMaterialUpdate = true;

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // =================================================================
            // Section 6: 遠景負荷最適化・本採用＆アセット管理
            // =================================================================
            _showOptimizationSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showOptimizationSection, "6. 遠景負荷最適化・本採用＆アセット管理");
            if (_showOptimizationSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("遠景負荷最適化 (GPUカリング / 影 / CPU更新スキップ)", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_animCullDistanceProp, new GUIContent("GPUアニメ省略距離 (m, 0=無制限)"));
                EditorGUILayout.PropertyField(_maxRenderDistanceProp, new GUIContent("GPU最大描画距離 (m, 0=無制限)"));
                EditorGUILayout.PropertyField(_castShadowsProp, new GUIContent("観客の影キャスト (ShadowCaster)"));
                EditorGUILayout.PropertyField(_pauseTrackingWhenFarProp, new GUIContent("遠距離時のCPU視線追従停止"));
                EditorGUILayout.PropertyField(_applyLayerCullDistanceProp, new GUIContent("Cameraレイヤー距離カリングを適用"));
                if (_applyLayerCullDistanceProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_layerCullDistanceProp, new GUIContent("レイヤーカリング距離 (m)"));
                    EditorGUI.indentLevel--;

                    if (!gen.IsLayerSafeForCullDistance)
                    {
                        string layerName = LayerMask.LayerToName(gen.gameObject.layer);
                        EditorGUILayout.HelpBox(
                            $"安全ガード作動中: 現在の Layer {gen.gameObject.layer} ({(string.IsNullOrEmpty(layerName) ? "名称未設定" : layerName)}) には Camera.layerCullDistances を適用しません。\nDefault(0)・ゲーム共有レイヤー(1〜10)・予約レイヤー(29, 30)を保護しています。使用する場合は専用の観客レイヤー(Layer 11〜28, 31)を割り当ててください。",
                            MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            $"Play Mode 中のみ、Layer {gen.gameObject.layer} ({LayerMask.LayerToName(gen.gameObject.layer)}) にカリング距離 {_layerCullDistanceProp.floatValue:0}m が適用されます（終了・リロード時自動復元）。",
                            MessageType.Info);
                    }
                }
                if (EditorGUI.EndChangeCheck()) needMaterialUpdate = true;

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("内部マテリアル参照", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_sharedBillboardMaterialProp, new GUIContent("ビルボード共有マテリアル"));
                EditorGUILayout.PropertyField(_sharedStandMaterialProp, new GUIContent("仮客席共有マテリアル"));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("本採用・プレハブ化・完全静的化ツール", EditorStyles.boldLabel);
                string bakeStatus = _isBakedAssetsProp != null && _isBakedAssetsProp.boolValue
                    ? "保存状態: ✅ .asset / .mat 永続ベイク済み（プレハブ化OK・起動再生成なし）"
                    : "保存状態: ⚠️ ランタイム生成メッシュ（プレハブ化する場合は下の保存ボタンを押してください）";
                EditorGUILayout.LabelField(bakeStatus, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.45f, 0.80f, 1.0f);
                if (GUILayout.Button("💾 メッシュ＆マテリアル永続保存", GUILayout.Height(26)))
                {
                    serializedObject.ApplyModifiedProperties();
                    if (gen.BakeCrowdAssetsToProject(out string savedDir))
                    {
                        EditorUtility.DisplayDialog("アセット永続保存完了",
                            $"ビルボードメッシュとマテリアルを Project へ保存しました。\nこの状態でそのまま Prefab 化できます。\n\n保存先:\n{savedDir}",
                            "OK");
                    }
                    serializedObject.Update();
                }

                GUI.backgroundColor = new Color(0.55f, 0.95f, 0.65f);
                if (GUILayout.Button("🚀 本番ステージ用設定に切替", GUILayout.Height(26)))
                {
                    serializedObject.ApplyModifiedProperties();
                    gen.ApplyProductionPreset();
                    serializedObject.Update();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(1.0f, 0.78f, 0.40f);
                if (GUILayout.Button("🧹 コントローラーを外して完全静的化 (GPUのみ)", GUILayout.Height(24)))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "コントローラー取り外し確認",
                        "1. 生成済みメッシュ＆マテリアルを Project に永続保存(Bake)します\n2. プレビューマーカーを削除します\n3. StadiumCrowdBillboardV2Generator スクリプトを取り外します（Undo可）\n\n※ CPU側の視線追従(TargetPos更新)は停止しC#負荷は完全ゼロになりますが、GPU頂点アニメと遠景距離カリングは引き続き動作します。\n\n実行しますか？",
                        "完全静的化する",
                        "キャンセル");

                    if (confirmed)
                    {
                        serializedObject.ApplyModifiedProperties();
                        SetBrushMode(gen, BrushToolMode.None);
                        if (gen.BakeCrowdAssetsToProject(out _))
                        {
                            gen.DestroyPreviewMarkerImmediate();
                            gen.RestoreLayerCullDistance();
                            Undo.DestroyObjectImmediate(gen);
                            GUIUtility.ExitGUI();
                            return;
                        }
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            if (needMeshRebuild && _autoRebuildOnParamChange)
            {
                gen.GenerateCrowd(needSurfaceResample);
                SceneView.RepaintAll();
            }
            else if (needMaterialUpdate)
            {
                gen.ApplyMaterialSettingsToAllChunks();
                SceneView.RepaintAll();
            }
        }

        private void DrawHeaderStatus(StadiumCrowdBillboardV2Generator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🪧 スタジアム観客システム V2【ビルボード板ポリ版：斜面スナップ・区画ハンドル・ブラシ対応】", EditorStyles.boldLabel);
            string runtimeTag = Application.isPlaying
                ? (gen.IsTrackingPausedByDistance ? "  |  状態: ⏸️ 遠距離CPU追従停止中" : "  |  状態: ▶️ CPU追従稼働中")
                : (gen.IsBakedAssets ? "  |  保存: ✅ 永続ベイク済み" : "  |  保存: 未ベイク");
            EditorGUILayout.LabelField(
                $"総人数: {gen.GeneratedSpectatorCount:N0} 人  |  チャンク: {gen.GeneratedChunkCount}  |  ポリゴン: {gen.GeneratedTriangleCount:N0} tris{runtimeTag}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainActionBar(StadiumCrowdBillboardV2Generator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _autoRebuildOnParamChange = EditorGUILayout.ToggleLeft("変更時に自動再生成", _autoRebuildOnParamChange, GUILayout.Width(135));
            EditorGUI.BeginChangeCheck();
            _showSceneHandles = EditorGUILayout.ToggleLeft("Sceneハンドル", _showSceneHandles, GUILayout.Width(105));
            _showTierGuideRings = EditorGUILayout.ToggleLeft("階層リング", _showTierGuideRings, GUILayout.Width(82));
            _showSnapYWindows = EditorGUILayout.ToggleLeft("Y範囲", _showSnapYWindows, GUILayout.Width(58));
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.80f, 0.95f);
            if (GUILayout.Button("🔄 ビルボード＆床スナップ再生成", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Generate Billboard Stadium Crowd V2");
                gen.GenerateCrowd(true);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(1.0f, 0.72f, 0.32f);
            if (GUILayout.Button("🧱 モニター/発射口/壁テンプレ", GUILayout.Height(28)))
            {
                Undo.RecordObject(gen, "Apply 3-Wing Billboard Template");
                gen.ApplyThreeWingStadiumTemplate();
                gen.GenerateCrowd(false);
                EditorUtility.SetDirty(gen);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.35f);
            if (GUILayout.Button("➕ 3D除外ボックス追加 (任意形状用)", GUILayout.Height(22)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Add BillboardExclusionZoneV2");
                BillboardExclusionZoneV2 zone = gen.AddExclusionZone();
                gen.GenerateCrowd(false);
                Selection.activeGameObject = zone.gameObject;
            }

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ クリア", GUILayout.Width(76), GUILayout.Height(22)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Clear Billboard Stadium Crowd V2");
                gen.ClearGeneratedCrowd();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawBrushToolPanel(StadiumCrowdBillboardV2Generator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🖌️ Sceneビュー直接編集ブラシ（ゲーム実行時の処理負荷は完全ゼロ）", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (_brushToolMode == BrushToolMode.None) ? new Color(0.65f, 0.85f, 1.0f) : Color.white;
            if (GUILayout.Button("✋ 通常 (ハンドル)", GUILayout.Height(26)))
            {
                SetBrushMode(gen, BrushToolMode.None);
            }

            GUI.backgroundColor = (_brushToolMode == BrushToolMode.Eraser) ? new Color(1.0f, 0.42f, 0.42f) : Color.white;
            if (GUILayout.Button("🧹 消しゴムブラシ", GUILayout.Height(26)))
            {
                SetBrushMode(gen, _brushToolMode == BrushToolMode.Eraser ? BrushToolMode.None : BrushToolMode.Eraser);
            }

            GUI.backgroundColor = (_brushToolMode == BrushToolMode.Paint) ? new Color(0.42f, 0.92f, 0.55f) : Color.white;
            if (GUILayout.Button("🖌️ 追加ペイント", GUILayout.Height(26)))
            {
                SetBrushMode(gen, _brushToolMode == BrushToolMode.Paint ? BrushToolMode.None : BrushToolMode.Paint);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🎨 自動階層を全OFFにして「ブラシ＆消しゴム専用」で0から塗る", GUILayout.Height(22)))
            {
                Undo.RecordObject(gen, "Switch Billboard V2 to Manual Brush Only Mode");
                gen.SwitchToManualBrushOnlyMode(false);
                SetBrushMode(gen, BrushToolMode.Paint);
                EditorUtility.SetDirty(gen);
                SceneView.RepaintAll();
            }

            if (_brushToolMode != BrushToolMode.None)
            {
                _brushRadius = EditorGUILayout.Slider("ブラシ半径 (m)", _brushRadius, 0.5f, 15f);
                if (_brushToolMode == BrushToolMode.Paint)
                {
                    _paintSeatSpacing = EditorGUILayout.Slider("追加時の座席間隔 (m)", _paintSeatSpacing, 0.45f, 2.5f);
                }
                else
                {
                    _showEraseStampsInScene = EditorGUILayout.Toggle("消しゴム適用範囲を赤丸表示", _showEraseStampsInScene);
                }

                EditorGUILayout.HelpBox(
                    "Sceneビュー操作: 左ドラッグ＝消す/追加 ｜ [ / ] キー・Shift+ホイール＝半径変更 ｜ Alt+左ドラッグ＝カメラ回転 ｜ Esc＝終了",
                    MessageType.Info);
            }

            if (gen.BrushEraseStampCount > 0 || gen.ManualPaintedSeatCount > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"消しゴム: {gen.BrushEraseStampCount}箇所  |  手動追加: {gen.ManualPaintedSeatCount}人", EditorStyles.miniLabel);

                if (gen.BrushEraseStampCount > 0)
                {
                    if (GUILayout.Button("↩️ 1つ戻す", GUILayout.Width(68)))
                    {
                        Undo.RecordObject(gen, "Undo Last Erase Stamp");
                        gen.UndoLastBrushEraseStamp();
                        gen.GenerateCrowd(false);
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("消去リセット", GUILayout.Width(76)))
                    {
                        Undo.RecordObject(gen, "Clear Erase Stamps");
                        gen.ClearBrushErases();
                        gen.GenerateCrowd(false);
                        SceneView.RepaintAll();
                    }
                }
                if (gen.ManualPaintedSeatCount > 0)
                {
                    if (GUILayout.Button("追加全消去", GUILayout.Width(72)))
                    {
                        Undo.RecordObject(gen, "Clear Painted Seats");
                        gen.ClearManualPaintedSeats();
                        gen.GenerateCrowd(false);
                        SceneView.RepaintAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void AddCutoutEntry(string label, float centerDeg, float widthDeg, Color color)
        {
            int idx = _angularCutoutsProp.arraySize;
            _angularCutoutsProp.InsertArrayElementAtIndex(idx);
            SerializedProperty item = _angularCutoutsProp.GetArrayElementAtIndex(idx);
            item.FindPropertyRelative("label").stringValue = label;
            item.FindPropertyRelative("enabled").boolValue = true;
            item.FindPropertyRelative("centerAngleDeg").floatValue = centerDeg;
            item.FindPropertyRelative("arcWidthDeg").floatValue = widthDeg;
            item.FindPropertyRelative("targetTierIndex").intValue = -1;
            item.FindPropertyRelative("limitRadiusRange").boolValue = false;
            item.FindPropertyRelative("minRadius").floatValue = 0f;
            item.FindPropertyRelative("maxRadius").floatValue = 200f;
            item.FindPropertyRelative("gizmoColor").colorValue = color;
        }

        private void AddRadialWallEntry(string label, float angleDeg, float thicknessM, float marginM)
        {
            int idx = _radialWallsProp.arraySize;
            _radialWallsProp.InsertArrayElementAtIndex(idx);
            SerializedProperty item = _radialWallsProp.GetArrayElementAtIndex(idx);
            item.FindPropertyRelative("label").stringValue = label;
            item.FindPropertyRelative("enabled").boolValue = true;
            item.FindPropertyRelative("angleDeg").floatValue = angleDeg;
            item.FindPropertyRelative("thicknessMeters").floatValue = thicknessM;
            item.FindPropertyRelative("marginMeters").floatValue = marginM;
            item.FindPropertyRelative("targetTierIndex").intValue = -1;
            item.FindPropertyRelative("limitRadiusRange").boolValue = false;
            item.FindPropertyRelative("minRadius").floatValue = 0f;
            item.FindPropertyRelative("maxRadius").floatValue = 200f;
            item.FindPropertyRelative("gizmoColor").colorValue = new Color(1.0f, 0.90f, 0.20f, 0.85f);
        }

        private static string[] BuildTierScopePopupNames(StadiumCrowdBillboardV2Generator gen)
        {
            int count = gen.Tiers != null ? gen.Tiers.Count : 0;
            string[] names = new string[count + 1];
            names[0] = "全階層 (All Tiers)";
            for (int i = 0; i < count; i++)
            {
                string tName = gen.Tiers[i] != null ? gen.Tiers[i].tierName : $"Tier {i + 1}";
                names[i + 1] = $"#{i + 1} のみ: {tName}";
            }
            return names;
        }

        private static int DrawTierScopePopup(string label, int currentTargetTierIndex, string[] popupNames)
        {
            int popupIdx = Mathf.Clamp(currentTargetTierIndex + 1, 0, popupNames.Length - 1);
            int nextPopupIdx = EditorGUILayout.Popup(label, popupIdx, popupNames);
            return nextPopupIdx - 1;
        }

        // =====================================================================
        // Scene ビュー可視化・階層4辺ハンドル・開口/壁ハンドル・ブラシ操作
        // =====================================================================
        private void OnSceneGUI()
        {
            StadiumCrowdBillboardV2Generator gen = (StadiumCrowdBillboardV2Generator)target;
            if (gen == null || gen.Tiers == null || gen.Tiers.Count == 0) return;

            Transform tr = gen.transform;
            float maxOuterR = 35f;
            float minInnerR = 10000f;
            float maxGuideY = 0f;
            float activeTierFallbackY = 0f;

            for (int t = 0; t < gen.Tiers.Count; t++)
            {
                var tier = gen.Tiers[t];
                if (tier == null || !tier.enabled) continue;

                float outerR = Mathf.Max(tier.innerRadiusX, tier.innerRadiusZ) + Mathf.Max(0, tier.rowCount - 1) * tier.rowDepth;
                float innerR = Mathf.Min(tier.innerRadiusX, tier.innerRadiusZ);
                if (outerR > maxOuterR) maxOuterR = outerR;
                if (innerR < minInnerR) minInnerR = innerR;
                float topY = tier.baseHeightY + tier.rowCount * tier.rowRise;
                if (topY > maxGuideY) maxGuideY = topY;
                if (tier.editInScene) activeTierFallbackY = tier.baseHeightY;

                if (_showTierGuideRings)
                {
                    DrawTierGuideRing(tr, tier, _showSnapYWindows);
                }
            }

            if (minInnerR > maxOuterR) minInnerR = Mathf.Max(4f, maxOuterR * 0.45f);

            DrawSceneOverlayToolbar(gen);

            if (_brushToolMode != BrushToolMode.None)
            {
                HandleSceneBrushMode(gen, activeTierFallbackY);
                return;
            }

            if (!_showSceneHandles) return;

            bool handleModified = false;

            for (int t = 0; t < gen.Tiers.Count; t++)
            {
                var tier = gen.Tiers[t];
                if (tier == null || !tier.enabled || !tier.editInScene) continue;

                if (DrawTierInteractiveHandles(gen, tier, _showSnapYWindows))
                {
                    handleModified = true;
                }
            }

            if (gen.AngularCutouts != null)
            {
                for (int i = 0; i < gen.AngularCutouts.Count; i++)
                {
                    var zone = gen.AngularCutouts[i];
                    if (zone == null || !zone.enabled) continue;

                    GetDisplayRangeForScope(gen, zone.targetTierIndex, minInnerR, maxOuterR, maxGuideY,
                        out float dispInnerR, out float dispOuterR, out float dispY);

                    if (zone.limitRadiusRange)
                    {
                        dispInnerR = Mathf.Max(2f, zone.minRadius);
                        dispOuterR = Mathf.Max(dispInnerR + 2f, zone.maxRadius);
                    }

                    if (DrawAngularCutoutHandle(gen, zone, dispInnerR, dispOuterR, dispY))
                    {
                        handleModified = true;
                    }
                }
            }

            if (gen.EnablePeriodicWalls && gen.PeriodicWallCount > 0)
            {
                GetDisplayRangeForScope(gen, gen.PeriodicWallTierIndex, minInnerR, maxOuterR, maxGuideY,
                    out float dispInnerR, out float dispOuterR, out float dispY);

                float halfW = gen.PeriodicWallThicknessMeters * 0.5f + gen.PeriodicWallMarginMeters;
                float step = 360f / gen.PeriodicWallCount;
                Color wallCol = new Color(1.0f, 0.85f, 0.25f, 0.70f);
                for (int i = 0; i < gen.PeriodicWallCount; i++)
                {
                    float deg = gen.PeriodicWallOffsetDeg + i * step;
                    DrawRadialWallCorridor(tr, deg, halfW, dispInnerR, dispOuterR, dispY, wallCol, $"均等壁 #{i + 1}");
                }
            }

            if (gen.RadialWalls != null)
            {
                for (int i = 0; i < gen.RadialWalls.Count; i++)
                {
                    var wall = gen.RadialWalls[i];
                    if (wall == null || !wall.enabled) continue;

                    GetDisplayRangeForScope(gen, wall.targetTierIndex, minInnerR, maxOuterR, maxGuideY,
                        out float dispInnerR, out float dispOuterR, out float dispY);

                    if (wall.limitRadiusRange)
                    {
                        dispInnerR = Mathf.Max(2f, wall.minRadius);
                        dispOuterR = Mathf.Max(dispInnerR + 2f, wall.maxRadius);
                    }

                    if (DrawRadialWallHandle(gen, wall, dispInnerR, dispOuterR, dispY))
                    {
                        handleModified = true;
                    }
                }
            }

            if (handleModified)
            {
                EditorUtility.SetDirty(gen);
                if (_autoRebuildOnParamChange)
                {
                    if (gen.LastNonReadableFallbackCount > 0)
                    {
                        _pendingRebuildAfterDrag = true;
                    }
                    else
                    {
                        gen.GenerateCrowd(false);
                    }
                }
                SceneView.RepaintAll();
            }

            if (_pendingRebuildAfterDrag && Event.current.type == EventType.MouseUp)
            {
                _pendingRebuildAfterDrag = false;
                gen.GenerateCrowd(false);
                SceneView.RepaintAll();
            }
        }

        private void DrawSceneOverlayToolbar(StadiumCrowdBillboardV2Generator gen)
        {
            Handles.BeginGUI();
            Rect barRect = new Rect(12, 12, _brushToolMode == BrushToolMode.None ? 295 : 445, 32);
            GUILayout.BeginArea(barRect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = (_brushToolMode == BrushToolMode.None) ? new Color(0.65f, 0.85f, 1.0f) : Color.white;
            if (GUILayout.Button("✋ 通常", EditorStyles.toolbarButton, GUILayout.Width(68)))
            {
                SetBrushMode(gen, BrushToolMode.None);
            }

            GUI.backgroundColor = (_brushToolMode == BrushToolMode.Eraser) ? new Color(1.0f, 0.45f, 0.45f) : Color.white;
            if (GUILayout.Button("🧹 消しゴム", EditorStyles.toolbarButton, GUILayout.Width(86)))
            {
                SetBrushMode(gen, _brushToolMode == BrushToolMode.Eraser ? BrushToolMode.None : BrushToolMode.Eraser);
            }

            GUI.backgroundColor = (_brushToolMode == BrushToolMode.Paint) ? new Color(0.45f, 0.92f, 0.55f) : Color.white;
            if (GUILayout.Button("🖌️ 追加ブラシ", EditorStyles.toolbarButton, GUILayout.Width(94)))
            {
                SetBrushMode(gen, _brushToolMode == BrushToolMode.Paint ? BrushToolMode.None : BrushToolMode.Paint);
            }
            GUI.backgroundColor = Color.white;

            if (_brushToolMode != BrushToolMode.None)
            {
                GUILayout.Label($"半径:{_brushRadius:0.0}m", EditorStyles.miniLabel, GUILayout.Width(62));
                if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    _brushRadius = Mathf.Clamp(_brushRadius * 0.8f, 0.5f, 15f);
                }
                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    _brushRadius = Mathf.Clamp(_brushRadius * 1.25f, 0.5f, 15f);
                }
                GUILayout.Label("(Esc解除)", EditorStyles.miniLabel, GUILayout.Width(58));
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void HandleSceneBrushMode(StadiumCrowdBillboardV2Generator gen, float fallbackY)
        {
            Event e = Event.current;
            Transform tr = gen.transform;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    SetBrushMode(gen, BrushToolMode.None);
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.LeftBracket)
                {
                    _brushRadius = Mathf.Clamp(_brushRadius * 0.85f, 0.5f, 15f);
                    e.Use();
                    SceneView.RepaintAll();
                    Repaint();
                    return;
                }
                if (e.keyCode == KeyCode.RightBracket)
                {
                    _brushRadius = Mathf.Clamp(_brushRadius * 1.18f, 0.5f, 15f);
                    e.Use();
                    SceneView.RepaintAll();
                    Repaint();
                    return;
                }
            }

            if (e.type == EventType.ScrollWheel && e.shift)
            {
                float factor = e.delta.y > 0f ? 0.9f : 1.12f;
                _brushRadius = Mathf.Clamp(_brushRadius * factor, 0.5f, 15f);
                e.Use();
                SceneView.RepaintAll();
                Repaint();
                return;
            }

            if (_brushToolMode == BrushToolMode.Eraser && _showEraseStampsInScene && gen.BrushEraseStamps != null)
            {
                float scale = GetHorizontalLossyScale(tr);
                Handles.color = new Color(1.0f, 0.25f, 0.25f, 0.28f);
                for (int i = 0; i < gen.BrushEraseStamps.Count; i++)
                {
                    var st = gen.BrushEraseStamps[i];
                    Vector3 wp = tr.TransformPoint(st.localPosition);
                    Handles.DrawWireDisc(wp, tr.up, st.radius * scale);
                }
            }

            if (e.alt || (e.isMouse && e.button != 0))
            {
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                Ray worldRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                _hasHoverHit = gen.TryRaycastBrushTarget(
                    worldRay,
                    _brushToolMode == BrushToolMode.Eraser,
                    fallbackY,
                    out _hoverLocalPos,
                    out _hoverLocalNormal);
                SceneView.RepaintAll();
            }

            if (_hasHoverHit)
            {
                Vector3 hitWS = tr.TransformPoint(_hoverLocalPos);
                Vector3 nrmWS = tr.TransformDirection(_hoverLocalNormal).normalized;
                if (nrmWS.sqrMagnitude < 0.01f) nrmWS = Vector3.up;
                float worldR = _brushRadius * GetHorizontalLossyScale(tr);

                Color brushCol = (_brushToolMode == BrushToolMode.Eraser)
                    ? new Color(1.0f, 0.25f, 0.25f, 0.92f)
                    : new Color(0.20f, 0.95f, 0.45f, 0.92f);

                Handles.color = new Color(brushCol.r, brushCol.g, brushCol.b, 0.18f);
                Handles.DrawSolidDisc(hitWS + nrmWS * 0.05f, nrmWS, worldR);
                Handles.color = brushCol;
                Handles.DrawWireDisc(hitWS + nrmWS * 0.05f, nrmWS, worldR);
                Handles.DrawWireDisc(hitWS + nrmWS * 0.05f, Vector3.up, worldR);

                string tag = (_brushToolMode == BrushToolMode.Eraser) ? "🧹 消しゴム" : "🖌️ 追加";
                Handles.Label(hitWS + Vector3.up * 1.4f, $"{tag} (半径 {_brushRadius:0.1}m)");
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GUIUtility.hotControl = controlId;
                _isBrushStrokeActive = true;
                _strokeHasUnflushedChanges = false;
                double now = EditorApplication.timeSinceStartup;
                _lastBrushApplyTime = now;
                _lastBrushMeshRebuildTime = now;

                Undo.RecordObject(gen, _brushToolMode == BrushToolMode.Eraser ? "Erase Billboard Crowd Stroke" : "Paint Billboard Crowd Stroke");

                if (_hasHoverHit)
                {
                    ApplyBrushAtHover(gen, true);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && _isBrushStrokeActive)
            {
                double now = EditorApplication.timeSinceStartup;
                if (_hasHoverHit && (now - _lastBrushApplyTime) >= 0.025)
                {
                    _lastBrushApplyTime = now;

                    // ビルボードは頂点数が圧倒的に少ないためさらに高速だが、2万人超でも滑らかなよう自動調整
                    double rebuildInterval = gen.GeneratedSpectatorCount < 6000 ? 0.05 : (gen.GeneratedSpectatorCount < 15000 ? 0.10 : 0.16);
                    bool shouldRebuildNow = (now - _lastBrushMeshRebuildTime) >= rebuildInterval;

                    if (ApplyBrushAtHover(gen, shouldRebuildNow))
                    {
                        if (shouldRebuildNow)
                        {
                            _lastBrushMeshRebuildTime = now;
                            _strokeHasUnflushedChanges = false;
                        }
                        else
                        {
                            _strokeHasUnflushedChanges = true;
                        }
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _isBrushStrokeActive)
            {
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                }
                _isBrushStrokeActive = false;
                if (_strokeHasUnflushedChanges)
                {
                    gen.FlushBrushMeshRebuild();
                    _strokeHasUnflushedChanges = false;
                }
                EditorUtility.SetDirty(gen);
                e.Use();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private bool ApplyBrushAtHover(StadiumCrowdBillboardV2Generator gen, bool rebuildNow)
        {
            bool changed = false;
            if (_brushToolMode == BrushToolMode.Eraser)
            {
                changed = gen.FastApplyBrushErase(_hoverLocalPos, _brushRadius, rebuildNow);
            }
            else if (_brushToolMode == BrushToolMode.Paint)
            {
                changed = gen.FastApplyBrushPaint(_hoverLocalPos, _brushRadius, _paintSeatSpacing, rebuildNow);
            }
            if (changed)
            {
                SceneView.RepaintAll();
                if (rebuildNow) Repaint();
            }
            return changed;
        }

        private static bool DrawTierInteractiveHandles(
            StadiumCrowdBillboardV2Generator gen,
            StadiumCrowdBillboardV2Generator.CrowdTierConfig tier,
            bool showSnapYHandles)
        {
            Transform tr = gen.transform;
            bool modified = false;

            bool isSector = tier.arcAngleDeg < 359.5f;
            float startDeg = Mathf.Repeat(tier.startAngleDeg, 360f);
            float endDeg = Mathf.Repeat(tier.startAngleDeg + tier.arcAngleDeg, 360f);
            float midDeg = isSector ? Mathf.Repeat(tier.startAngleDeg + tier.arcAngleDeg * 0.5f, 360f) : startDeg;
            float midRad = midDeg * Mathf.Deg2Rad;
            float halfStraight = Mathf.Max(0f, tier.straightTrackLength) * 0.5f;

            float outerRx = tier.innerRadiusX + Mathf.Max(0, tier.rowCount - 1) * tier.rowDepth;
            float outerRz = tier.innerRadiusZ + Mathf.Max(0, tier.rowCount - 1) * tier.rowDepth;
            float waveIn = StadiumCrowdBillboardV2Generator.EvaluateTierElevationOffset(tier, midDeg, 0f);
            float waveOut = StadiumCrowdBillboardV2Generator.EvaluateTierElevationOffset(tier, midDeg, 1f);
            float yIn = tier.baseHeightY + waveIn;
            float yOut = tier.baseHeightY + Mathf.Max(0, tier.rowCount - 1) * tier.rowRise + waveOut;

            Vector3 innerPtL = StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(midRad, tier.innerRadiusX, tier.innerRadiusZ, halfStraight, yIn, out _);
            Vector3 outerPtL = StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(midRad, outerRx, outerRz, halfStraight, yOut, out _);
            Vector3 innerWS = tr.TransformPoint(innerPtL);
            Vector3 outerWS = tr.TransformPoint(outerPtL);

            Handles.color = tier.gizmoColor;
            Handles.DrawAAPolyLine(3.0f, innerWS, outerWS);

            float capSizeIn = HandleUtility.GetHandleSize(innerWS) * 0.14f;
            float capSizeOut = HandleUtility.GetHandleSize(outerWS) * 0.15f;

            Handles.color = new Color(0.15f, 0.92f, 1.0f, 1.0f);
            Handles.Label(innerWS + Vector3.up * 1.4f, $"[{tier.tierName}] 前列内径 {tier.innerRadiusX:0.1}m");
            EditorGUI.BeginChangeCheck();
            Vector3 movedInnerWS = Handles.FreeMoveHandle(innerWS, capSizeIn, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localM = tr.InverseTransformPoint(movedInnerWS);
                float newR = Mathf.Max(2f, Mathf.Sqrt(localM.x * localM.x + localM.z * localM.z));
                float ratio = tier.innerRadiusX > 0.1f ? (tier.innerRadiusZ / tier.innerRadiusX) : 1f;

                Undo.RecordObject(gen, "Resize Billboard Tier Inner Radius");
                tier.innerRadiusX = newR;
                tier.innerRadiusZ = Mathf.Max(2f, newR * ratio);
                modified = true;
            }

            Handles.color = new Color(0.25f, 1.0f, 0.45f, 1.0f);
            Handles.Label(outerWS + Vector3.up * 1.6f, $"[{tier.tierName}] 奥行き端 {outerRx:0.1}m ({tier.rowCount}段)");
            EditorGUI.BeginChangeCheck();
            Vector3 movedOuterWS = Handles.FreeMoveHandle(outerWS, capSizeOut, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localM = tr.InverseTransformPoint(movedOuterWS);
                float dragOuterR = Mathf.Sqrt(localM.x * localM.x + localM.z * localM.z);
                float innerAvg = (tier.innerRadiusX + tier.innerRadiusZ) * 0.5f;
                float depthSpan = Mathf.Max(0f, dragOuterR - innerAvg);
                int newRows = Mathf.Clamp(Mathf.RoundToInt(depthSpan / Mathf.Max(0.45f, tier.rowDepth)) + 1, 1, 60);

                if (newRows != tier.rowCount)
                {
                    Undo.RecordObject(gen, "Resize Billboard Tier Depth (Row Count)");
                    tier.rowCount = newRows;
                    modified = true;
                }
            }

            if (isSector)
            {
                float handleR = Mathf.Max(outerRx, outerRz) + 2.5f;
                Vector3 leftOutWS = tr.TransformPoint(AngleToDirXZ(startDeg) * handleR + Vector3.up * yOut);
                Vector3 rightOutWS = tr.TransformPoint(AngleToDirXZ(endDeg) * handleR + Vector3.up * yOut);
                Vector3 midRotWS = tr.TransformPoint(AngleToDirXZ(midDeg) * (handleR + 3.0f) + Vector3.up * yOut);

                Vector3 leftInWS = tr.TransformPoint(AngleToDirXZ(startDeg) * tier.innerRadiusX + Vector3.up * yIn);
                Vector3 rightInWS = tr.TransformPoint(AngleToDirXZ(endDeg) * tier.innerRadiusX + Vector3.up * yIn);

                Handles.color = tier.gizmoColor;
                Handles.DrawAAPolyLine(3.0f, leftInWS, leftOutWS);
                Handles.DrawAAPolyLine(3.0f, rightInWS, rightOutWS);

                EditorGUI.BeginChangeCheck();
                Vector3 movedRotWS = Handles.FreeMoveHandle(midRotWS, capSizeOut * 1.05f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 lm = tr.InverseTransformPoint(movedRotWS);
                    if (lm.x * lm.x + lm.z * lm.z > 1f)
                    {
                        float newMid = Mathf.Repeat(Mathf.Atan2(lm.x, lm.z) * Mathf.Rad2Deg, 360f);
                        Undo.RecordObject(gen, "Rotate Billboard Sector Block");
                        tier.startAngleDeg = Mathf.Repeat(newMid - tier.arcAngleDeg * 0.5f, 360f);
                        modified = true;
                    }
                }

                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                Vector3 movedLeftWS = Handles.FreeMoveHandle(leftOutWS, capSizeIn * 0.95f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 lm = tr.InverseTransformPoint(movedLeftWS);
                    if (lm.x * lm.x + lm.z * lm.z > 1f)
                    {
                        float newStart = Mathf.Repeat(Mathf.Atan2(lm.x, lm.z) * Mathf.Rad2Deg, 360f);
                        float delta = Mathf.DeltaAngle(startDeg, newStart);
                        float nextArc = Mathf.Clamp(tier.arcAngleDeg - delta, 5f, 360f);
                        Undo.RecordObject(gen, "Resize Billboard Sector Block Start Angle");
                        tier.arcAngleDeg = nextArc;
                        tier.startAngleDeg = Mathf.Repeat(endDeg - nextArc, 360f);
                        modified = true;
                    }
                }

                EditorGUI.BeginChangeCheck();
                Vector3 movedRightWS = Handles.FreeMoveHandle(rightOutWS, capSizeIn * 0.95f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 lm = tr.InverseTransformPoint(movedRightWS);
                    if (lm.x * lm.x + lm.z * lm.z > 1f)
                    {
                        float newEnd = Mathf.Repeat(Mathf.Atan2(lm.x, lm.z) * Mathf.Rad2Deg, 360f);
                        float delta = Mathf.DeltaAngle(endDeg, newEnd);
                        float nextArc = Mathf.Clamp(tier.arcAngleDeg + delta, 5f, 360f);
                        Undo.RecordObject(gen, "Resize Billboard Sector Block End Angle");
                        tier.arcAngleDeg = nextArc;
                        modified = true;
                    }
                }
            }

            if (showSnapYHandles && tier.useAbsoluteYBounds)
            {
                Vector3 dir = AngleToDirXZ(midDeg);
                float rY = Mathf.Max(outerRx, outerRz) + 1.5f;
                Vector3 minYWS = tr.TransformPoint(dir * rY + Vector3.up * tier.snapMinY);
                Vector3 maxYWS = tr.TransformPoint(dir * rY + Vector3.up * tier.snapMaxY);

                Handles.color = new Color(1.0f, 0.85f, 0.25f, 0.9f);
                Handles.DrawDottedLine(minYWS, maxYWS, 3f);
                Handles.Label(minYWS, $"Y下限 {tier.snapMinY:0.1}m");
                Handles.Label(maxYWS, $"Y上限 {tier.snapMaxY:0.1}m");

                EditorGUI.BeginChangeCheck();
                Vector3 movedMinY = Handles.Slider(minYWS, tr.up, capSizeIn * 0.85f, Handles.ConeHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gen, "Adjust Billboard Tier Snap Min Y");
                    tier.snapMinY = tr.InverseTransformPoint(movedMinY).y;
                    modified = true;
                }

                EditorGUI.BeginChangeCheck();
                Vector3 movedMaxY = Handles.Slider(maxYWS, tr.up, capSizeIn * 0.85f, Handles.ConeHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gen, "Adjust Billboard Tier Snap Max Y");
                    tier.snapMaxY = tr.InverseTransformPoint(movedMaxY).y;
                    modified = true;
                }
            }

            return modified;
        }

        private static void GetDisplayRangeForScope(
            StadiumCrowdBillboardV2Generator gen,
            int tierScope,
            float fallbackInnerR,
            float fallbackOuterR,
            float fallbackY,
            out float innerR,
            out float outerR,
            out float displayY)
        {
            if (tierScope >= 0 && tierScope < gen.Tiers.Count && gen.Tiers[tierScope] != null)
            {
                var tier = gen.Tiers[tierScope];
                innerR = Mathf.Min(tier.innerRadiusX, tier.innerRadiusZ);
                outerR = Mathf.Max(tier.innerRadiusX, tier.innerRadiusZ) + tier.rowCount * tier.rowDepth;
                displayY = tier.baseHeightY + tier.rowCount * tier.rowRise * 0.5f;
            }
            else
            {
                innerR = fallbackInnerR;
                outerR = fallbackOuterR;
                displayY = fallbackY + 1.0f;
            }
        }

        private static void DrawTierGuideRing(Transform tr, StadiumCrowdBillboardV2Generator.CrowdTierConfig tier, bool showSnapYWindow)
        {
            const int segs = 72;
            float startRad = tier.startAngleDeg * Mathf.Deg2Rad;
            float totalArcRad = Mathf.Clamp(tier.arcAngleDeg, 5f, 360f) * Mathf.Deg2Rad;
            float halfStraight = Mathf.Max(0f, tier.straightTrackLength) * 0.5f;

            float outerRx = tier.innerRadiusX + Mathf.Max(0, tier.rowCount - 1) * tier.rowDepth;
            float outerRz = tier.innerRadiusZ + Mathf.Max(0, tier.rowCount - 1) * tier.rowDepth;
            float outerBaseY = tier.baseHeightY + Mathf.Max(0, tier.rowCount - 1) * tier.rowRise;

            Vector3[] innerPts = new Vector3[segs + 1];
            Vector3[] outerPts = new Vector3[segs + 1];

            for (int i = 0; i <= segs; i++)
            {
                float a = startRad + (i / (float)segs) * totalArcRad;
                float deg = a * Mathf.Rad2Deg;
                float wIn = StadiumCrowdBillboardV2Generator.EvaluateTierElevationOffset(tier, deg, 0f);
                float wOut = StadiumCrowdBillboardV2Generator.EvaluateTierElevationOffset(tier, deg, 1f);

                innerPts[i] = tr.TransformPoint(StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(a, tier.innerRadiusX, tier.innerRadiusZ, halfStraight, tier.baseHeightY + wIn, out _));
                outerPts[i] = tr.TransformPoint(StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(a, outerRx, outerRz, halfStraight, outerBaseY + wOut, out _));
            }

            Color c = tier.gizmoColor;
            float lineW = tier.editInScene ? 3.2f : 1.8f;
            Handles.color = new Color(c.r, c.g, c.b, tier.editInScene ? 0.92f : 0.55f);
            Handles.DrawAAPolyLine(lineW, innerPts);
            Handles.color = new Color(c.r, c.g, c.b, tier.editInScene ? 0.72f : 0.35f);
            Handles.DrawAAPolyLine(lineW * 0.85f, outerPts);

            if (showSnapYWindow && tier.useAbsoluteYBounds)
            {
                Handles.color = new Color(c.r, c.g, c.b, 0.35f);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f;
                    Vector3 low = tr.TransformPoint(StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(a, outerRx, outerRz, halfStraight, tier.snapMinY, out _));
                    Vector3 high = tr.TransformPoint(StadiumCrowdBillboardV2Generator.EvaluateOvalPoint(a, outerRx, outerRz, halfStraight, tier.snapMaxY, out _));
                    Handles.DrawDottedLine(low, high, 3f);
                }
            }
        }

        private static bool DrawAngularCutoutHandle(
            StadiumCrowdBillboardV2Generator gen,
            StadiumCrowdBillboardV2Generator.AngularCutoutZone zone,
            float innerR,
            float outerR,
            float yLocal)
        {
            Transform tr = gen.transform;
            float halfW = zone.arcWidthDeg * 0.5f;
            float startDeg = Mathf.Repeat(zone.centerAngleDeg - halfW, 360f);
            float endDeg = Mathf.Repeat(zone.centerAngleDeg + halfW, 360f);
            float handleR = outerR + 3.0f;

            Vector3 centerWS = tr.TransformPoint(new Vector3(0f, yLocal, 0f));
            Vector3 upWS = tr.TransformDirection(Vector3.up).normalized;
            Vector3 fromDirWS = tr.TransformDirection(AngleToDirXZ(startDeg)).normalized;

            Color c = zone.gizmoColor;
            Handles.color = new Color(c.r, c.g, c.b, 0.14f);
            Handles.DrawSolidArc(centerWS, upWS, fromDirWS, zone.arcWidthDeg, outerR * GetHorizontalLossyScale(tr));

            Handles.color = new Color(c.r, c.g, c.b, 0.92f);
            Handles.DrawWireArc(centerWS, upWS, fromDirWS, zone.arcWidthDeg, outerR * GetHorizontalLossyScale(tr));
            Handles.DrawWireArc(centerWS, upWS, fromDirWS, zone.arcWidthDeg, innerR * GetHorizontalLossyScale(tr));

            Vector3 startInWS = tr.TransformPoint(AngleToDirXZ(startDeg) * innerR + Vector3.up * yLocal);
            Vector3 startOutWS = tr.TransformPoint(AngleToDirXZ(startDeg) * handleR + Vector3.up * yLocal);
            Vector3 endInWS = tr.TransformPoint(AngleToDirXZ(endDeg) * innerR + Vector3.up * yLocal);
            Vector3 endOutWS = tr.TransformPoint(AngleToDirXZ(endDeg) * handleR + Vector3.up * yLocal);
            Vector3 midOutWS = tr.TransformPoint(AngleToDirXZ(zone.centerAngleDeg) * (handleR + 2.0f) + Vector3.up * yLocal);

            Handles.DrawAAPolyLine(3f, startInWS, startOutWS);
            Handles.DrawAAPolyLine(3f, endInWS, endOutWS);

            Handles.Label(midOutWS + upWS * 1.8f, $"[開口] {zone.label}\n中心 {zone.centerAngleDeg:0.0}° / 幅 {zone.arcWidthDeg:0.0}°");

            bool modified = false;
            float capSize = HandleUtility.GetHandleSize(midOutWS) * 0.13f;

            Handles.color = new Color(c.r, c.g, c.b, 1f);
            EditorGUI.BeginChangeCheck();
            Vector3 movedMidWS = Handles.FreeMoveHandle(midOutWS, capSize * 1.15f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localMoved = tr.InverseTransformPoint(movedMidWS);
                if (localMoved.x * localMoved.x + localMoved.z * localMoved.z > 1f)
                {
                    Undo.RecordObject(gen, "Rotate Billboard Angular Cutout");
                    zone.centerAngleDeg = Mathf.Repeat(Mathf.Atan2(localMoved.x, localMoved.z) * Mathf.Rad2Deg, 360f);
                    modified = true;
                }
            }

            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 movedStartWS = Handles.FreeMoveHandle(startOutWS, capSize, Vector3.zero, Handles.CubeHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localMoved = tr.InverseTransformPoint(movedStartWS);
                if (localMoved.x * localMoved.x + localMoved.z * localMoved.z > 1f)
                {
                    float newStartDeg = Mathf.Repeat(Mathf.Atan2(localMoved.x, localMoved.z) * Mathf.Rad2Deg, 360f);
                    float delta = Mathf.DeltaAngle(startDeg, newStartDeg);
                    float nextWidth = Mathf.Clamp(zone.arcWidthDeg - delta, 1f, 355f);
                    float nextCenter = Mathf.Repeat(endDeg - nextWidth * 0.5f, 360f);

                    Undo.RecordObject(gen, "Resize Billboard Angular Cutout Start Edge");
                    zone.arcWidthDeg = nextWidth;
                    zone.centerAngleDeg = nextCenter;
                    modified = true;
                }
            }

            EditorGUI.BeginChangeCheck();
            Vector3 movedEndWS = Handles.FreeMoveHandle(endOutWS, capSize, Vector3.zero, Handles.CubeHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localMoved = tr.InverseTransformPoint(movedEndWS);
                if (localMoved.x * localMoved.x + localMoved.z * localMoved.z > 1f)
                {
                    float newEndDeg = Mathf.Repeat(Mathf.Atan2(localMoved.x, localMoved.z) * Mathf.Rad2Deg, 360f);
                    float delta = Mathf.DeltaAngle(endDeg, newEndDeg);
                    float nextWidth = Mathf.Clamp(zone.arcWidthDeg + delta, 1f, 355f);
                    float nextCenter = Mathf.Repeat(startDeg + nextWidth * 0.5f, 360f);

                    Undo.RecordObject(gen, "Resize Billboard Angular Cutout End Edge");
                    zone.arcWidthDeg = nextWidth;
                    zone.centerAngleDeg = nextCenter;
                    modified = true;
                }
            }

            return modified;
        }

        private static bool DrawRadialWallHandle(
            StadiumCrowdBillboardV2Generator gen,
            StadiumCrowdBillboardV2Generator.RadialPartitionWall wall,
            float innerR,
            float outerR,
            float yLocal)
        {
            Transform tr = gen.transform;
            float halfW = wall.thicknessMeters * 0.5f + wall.marginMeters;
            DrawRadialWallCorridor(tr, wall.angleDeg, halfW, innerR, outerR, yLocal, wall.gizmoColor,
                $"[壁] {wall.label}\n{wall.angleDeg:0.0}° (幅 {halfW * 2f:0.1}m)");

            Vector3 dir = AngleToDirXZ(wall.angleDeg);
            Vector3 handleWS = tr.TransformPoint(dir * (outerR + 3.5f) + Vector3.up * yLocal);
            float capSize = HandleUtility.GetHandleSize(handleWS) * 0.13f;

            Handles.color = wall.gizmoColor;
            EditorGUI.BeginChangeCheck();
            Vector3 movedWS = Handles.FreeMoveHandle(handleWS, capSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 localMoved = tr.InverseTransformPoint(movedWS);
                if (localMoved.x * localMoved.x + localMoved.z * localMoved.z > 1f)
                {
                    Undo.RecordObject(gen, "Rotate Billboard Radial Wall");
                    wall.angleDeg = Mathf.Repeat(Mathf.Atan2(localMoved.x, localMoved.z) * Mathf.Rad2Deg, 360f);
                    return true;
                }
            }
            return false;
        }

        private static void DrawRadialWallCorridor(
            Transform tr,
            float angleDeg,
            float halfWidthMeters,
            float innerR,
            float outerR,
            float yLocal,
            Color color,
            string label)
        {
            Vector3 dir = AngleToDirXZ(angleDeg);
            Vector3 perp = new Vector3(dir.z, 0f, -dir.x);

            Vector3 p0 = tr.TransformPoint(dir * innerR - perp * halfWidthMeters + Vector3.up * yLocal);
            Vector3 p1 = tr.TransformPoint(dir * outerR - perp * halfWidthMeters + Vector3.up * yLocal);
            Vector3 p2 = tr.TransformPoint(dir * outerR + perp * halfWidthMeters + Vector3.up * yLocal);
            Vector3 p3 = tr.TransformPoint(dir * innerR + perp * halfWidthMeters + Vector3.up * yLocal);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { p0, p1, p2, p3 },
                new Color(color.r, color.g, color.b, 0.22f),
                new Color(color.r, color.g, color.b, 0.95f));

            Vector3 labelPos = tr.TransformPoint(dir * (outerR + 3.5f) + Vector3.up * (yLocal + 1.8f));
            Handles.Label(labelPos, label);
        }

        private static Vector3 AngleToDirXZ(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        private static float GetHorizontalLossyScale(Transform tr)
        {
            Vector3 s = tr.lossyScale;
            return Mathf.Max(0.01f, (Mathf.Abs(s.x) + Mathf.Abs(s.z)) * 0.5f);
        }
    }
}
