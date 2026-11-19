using UnityEditor;
using UnityEngine;

namespace AT_Murayama.StadiumCrowd.Editor
{
    /// <summary>
    /// StadiumCrowdGenerator 専用のカスタムインスペクター。
    /// ※プロジェクト規約に従い、上部メニュー（[MenuItem]）は一切使用せず、全操作を本インスペクターUIとSceneギズモで完結させます。
    /// </summary>
    [CustomEditor(typeof(StadiumCrowdGenerator))]
    public class StadiumCrowdGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty _spectatorStyleProp;
        private SerializedProperty _customSpectatorMeshProp;
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

        private SerializedProperty _placementModeProp;
        private SerializedProperty _innerRadiusXProp;
        private SerializedProperty _innerRadiusZProp;
        private SerializedProperty _straightTrackLengthProp;
        private SerializedProperty _startAngleDegProp;
        private SerializedProperty _arcAngleDegProp;
        private SerializedProperty _linearWidthProp;
        private SerializedProperty _pathPointsProp;
        private SerializedProperty _closePathLoopProp;
        private SerializedProperty _targetSurfaceRootProp;
        private SerializedProperty _targetMeshFiltersProp;
        private SerializedProperty _minSurfaceNormalYProp;
        private SerializedProperty _surfaceLookCenterLocalProp;

        private SerializedProperty _rowCountProp;
        private SerializedProperty _rowDepthProp;
        private SerializedProperty _rowRiseProp;
        private SerializedProperty _seatSpacingProp;
        private SerializedProperty _staggerRowsProp;
        private SerializedProperty _positionJitterProp;
        private SerializedProperty _emptySeatRatioProp;
        private SerializedProperty _sectorCountProp;
        private SerializedProperty _aisleGapMetersProp;
        private SerializedProperty _generateStandMeshProp;
        private SerializedProperty _randomSeedProp;

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

        private SerializedProperty _animCullDistanceProp;
        private SerializedProperty _maxRenderDistanceProp;
        private SerializedProperty _castShadowsProp;
        private SerializedProperty _pauseTrackingWhenFarProp;
        private SerializedProperty _applyLayerCullDistanceProp;
        private SerializedProperty _layerCullDistanceProp;
        private SerializedProperty _isBakedAssetsProp;

        private SerializedProperty _sharedCrowdMaterialProp;
        private SerializedProperty _sharedStandMaterialProp;

        private bool _autoRebuildOnParamChange = true;
        private bool _showAppearanceSection = true;
        private bool _showPlacementSection = true;
        private bool _showTrackingSection = true;
        private bool _showInternalSection = true;

        private void OnEnable()
        {
            _spectatorStyleProp = serializedObject.FindProperty("_spectatorStyle");
            _customSpectatorMeshProp = serializedObject.FindProperty("_customSpectatorMesh");
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

            _placementModeProp = serializedObject.FindProperty("_placementMode");
            _innerRadiusXProp = serializedObject.FindProperty("_innerRadiusX");
            _innerRadiusZProp = serializedObject.FindProperty("_innerRadiusZ");
            _straightTrackLengthProp = serializedObject.FindProperty("_straightTrackLength");
            _startAngleDegProp = serializedObject.FindProperty("_startAngleDeg");
            _arcAngleDegProp = serializedObject.FindProperty("_arcAngleDeg");
            _linearWidthProp = serializedObject.FindProperty("_linearWidth");
            _pathPointsProp = serializedObject.FindProperty("_pathPoints");
            _closePathLoopProp = serializedObject.FindProperty("_closePathLoop");
            _targetSurfaceRootProp = serializedObject.FindProperty("_targetSurfaceRoot");
            _targetMeshFiltersProp = serializedObject.FindProperty("_targetMeshFilters");
            _minSurfaceNormalYProp = serializedObject.FindProperty("_minSurfaceNormalY");
            _surfaceLookCenterLocalProp = serializedObject.FindProperty("_surfaceLookCenterLocal");

            _rowCountProp = serializedObject.FindProperty("_rowCount");
            _rowDepthProp = serializedObject.FindProperty("_rowDepth");
            _rowRiseProp = serializedObject.FindProperty("_rowRise");
            _seatSpacingProp = serializedObject.FindProperty("_seatSpacing");
            _staggerRowsProp = serializedObject.FindProperty("_staggerRows");
            _positionJitterProp = serializedObject.FindProperty("_positionJitter");
            _emptySeatRatioProp = serializedObject.FindProperty("_emptySeatRatio");
            _sectorCountProp = serializedObject.FindProperty("_sectorCount");
            _aisleGapMetersProp = serializedObject.FindProperty("_aisleGapMeters");
            _generateStandMeshProp = serializedObject.FindProperty("_generateStandMesh");
            _randomSeedProp = serializedObject.FindProperty("_randomSeed");

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

            _sharedCrowdMaterialProp = serializedObject.FindProperty("_sharedCrowdMaterial");
            _sharedStandMaterialProp = serializedObject.FindProperty("_sharedStandMaterial");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StadiumCrowdGenerator gen = (StadiumCrowdGenerator)target;

            DrawHeaderStatus(gen);
            EditorGUILayout.Space(4);
            DrawMainActionButtons(gen);
            EditorGUILayout.Space(6);

            bool needMeshRebuild = false;
            bool needMaterialUpdate = false;

            // -----------------------------------------------------------------
            // Section 1: 見た目・スタイル設定
            // -----------------------------------------------------------------
            _showAppearanceSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showAppearanceSection, "1. 観客の見た目・スタイル（後からワンクリック変更可）");
            if (_showAppearanceSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_spectatorStyleProp, new GUIContent("3Dメッシュ形状"));
                if (_spectatorStyleProp.enumValueIndex == (int)SpectatorMeshFactory.SpectatorStyle.CustomMesh)
                {
                    EditorGUILayout.PropertyField(_customSpectatorMeshProp, new GUIContent("差し替えメッシュ (FBX等)"));
                }
                EditorGUILayout.PropertyField(_spectatorScaleProp, new GUIContent("観客スケール倍率"));
                if (EditorGUI.EndChangeCheck())
                {
                    needMeshRebuild = true;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_colorThemeProp, new GUIContent("カラーテーマ"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Change Crowd Color Theme");
                    gen.ApplyColorThemePreset((StadiumCrowdGenerator.CrowdColorTheme)_colorThemeProp.enumValueIndex);
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
                    _colorThemeProp.enumValueIndex = (int)StadiumCrowdGenerator.CrowdColorTheme.Custom;
                    needMaterialUpdate = true;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // -----------------------------------------------------------------
            // Section 2: 配置形状 & 客席の途切れ設定
            // -----------------------------------------------------------------
            _showPlacementSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPlacementSection, "2. 配置モード & 客席の途切れ・ブロック設定");
            if (_showPlacementSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_placementModeProp, new GUIContent("配置モード"));
                StadiumCrowdGenerator.PlacementMode mode = (StadiumCrowdGenerator.PlacementMode)_placementModeProp.enumValueIndex;

                EditorGUILayout.Space(2);
                if (mode == StadiumCrowdGenerator.PlacementMode.CircularBowl)
                {
                    EditorGUILayout.PropertyField(_innerRadiusXProp, new GUIContent("フィールド内径 X (m)"));
                    EditorGUILayout.PropertyField(_innerRadiusZProp, new GUIContent("フィールド内径 Z (m)"));
                    EditorGUILayout.PropertyField(_straightTrackLengthProp, new GUIContent("直線トラック延長 (m)"));
                    EditorGUILayout.PropertyField(_startAngleDegProp, new GUIContent("開始角度 (度)"));
                    EditorGUILayout.PropertyField(_arcAngleDegProp, new GUIContent("全体角度 (360=全周)"));
                }
                else if (mode == StadiumCrowdGenerator.PlacementMode.LinearStand)
                {
                    EditorGUILayout.PropertyField(_linearWidthProp, new GUIContent("スタンド全長幅 (m)"));
                }
                else if (mode == StadiumCrowdGenerator.PlacementMode.CustomPathStand)
                {
                    EditorGUILayout.HelpBox("Sceneビューのハンドルをドラッグして、自由な折れ線・多角形の客席ラインを編集できます。", MessageType.Info);
                    EditorGUILayout.PropertyField(_closePathLoopProp, new GUIContent("始点と終点をループ接続"));
                    EditorGUILayout.PropertyField(_pathPointsProp, new GUIContent("制御点リスト"), true);
                }
                else if (mode == StadiumCrowdGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    EditorGUILayout.HelpBox("既存のスタジアム3Dモデル（途中で途切れている客席メッシュ等）の表面に自動で観客を立たせます。", MessageType.Info);
                    EditorGUILayout.PropertyField(_targetSurfaceRootProp, new GUIContent("対象3Dモデルの親Transform"));
                    EditorGUILayout.PropertyField(_targetMeshFiltersProp, new GUIContent("または個別MeshFilter"), true);
                    EditorGUILayout.PropertyField(_minSurfaceNormalYProp, new GUIContent("最小上向き法線Y (傾斜制限)"));
                    EditorGUILayout.PropertyField(_surfaceLookCenterLocalProp, new GUIContent("視線中心ローカル座標"));
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("段数・座席間隔・途切れ設定", EditorStyles.boldLabel);
                if (mode != StadiumCrowdGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    EditorGUILayout.PropertyField(_rowCountProp, new GUIContent("客席の段数 (Rows)"));
                    EditorGUILayout.PropertyField(_rowDepthProp, new GUIContent("1段の奥行き (m)"));
                    EditorGUILayout.PropertyField(_rowRiseProp, new GUIContent("1段の高さ段差 (m)"));
                    EditorGUILayout.PropertyField(_staggerRowsProp, new GUIContent("千鳥配置 (前後の列をズラす)"));
                    EditorGUILayout.PropertyField(_generateStandMeshProp, new GUIContent("仮の客席階段メッシュも生成"));
                }

                EditorGUILayout.PropertyField(_seatSpacingProp, new GUIContent("観客の横間隔 (m)"));
                EditorGUILayout.PropertyField(_positionJitterProp, new GUIContent("立ち位置の自然な揺らぎ"));
                EditorGUILayout.PropertyField(_emptySeatRatioProp, new GUIContent("ランダム空席率 (0=満席)"));
                EditorGUILayout.PropertyField(_randomSeedProp, new GUIContent("乱数シード"));

                if (mode != StadiumCrowdGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_sectorCountProp, new GUIContent("ブロック(セクター)分割数"));
                    EditorGUILayout.PropertyField(_aisleGapMetersProp, new GUIContent("ブロック間の通路隙間 (m)"));
                }

                if (EditorGUI.EndChangeCheck())
                {
                    needMeshRebuild = true;
                }

                // Visual Sector On/Off Buttons (for easy interrupted stands!)
                if (mode != StadiumCrowdGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    DrawSectorToggleGrid(gen, ref needMeshRebuild);
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // -----------------------------------------------------------------
            // Section 3: 視線追従（上空→着地） & 歓声モーション
            // -----------------------------------------------------------------
            _showTrackingSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showTrackingSection, "3. 視線追従（上空→着地）& 歓声・個体差モーション");
            if (_showTrackingSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_trackingModeProp, new GUIContent("ターゲット追従モード"));
                StadiumCrowdGenerator.TargetTrackingMode tMode = (StadiumCrowdGenerator.TargetTrackingMode)_trackingModeProp.enumValueIndex;

                if (tMode == StadiumCrowdGenerator.TargetTrackingMode.SkyDiveSimulation)
                {
                    EditorGUILayout.PropertyField(_simStartHeightProp, new GUIContent("上空の開始高度 (m)"));
                    EditorGUILayout.PropertyField(_simApproachOffsetXZProp, new GUIContent("上空の進入オフセットXZ"));
                    EditorGUILayout.PropertyField(_simLandingPointLocalProp, new GUIContent("着地地点ローカル座標"));
                    EditorGUILayout.PropertyField(_simCycleDurationProp, new GUIContent("降下ループ周期 (秒)"));
                }
                else if (tMode == StadiumCrowdGenerator.TargetTrackingMode.ManualAltitudeScrub)
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

                if (EditorGUI.EndChangeCheck())
                {
                    needMaterialUpdate = true;
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4);

            // -----------------------------------------------------------------
            // Section 4: 遠景負荷最適化・本採用＆アセット管理
            // -----------------------------------------------------------------
            _showInternalSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showInternalSection, "4. 遠景負荷最適化・本採用＆アセット管理");
            if (_showInternalSection)
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
                if (EditorGUI.EndChangeCheck())
                {
                    needMaterialUpdate = true;
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("内部マテリアル参照", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_sharedCrowdMaterialProp, new GUIContent("観客共有マテリアル"));
                EditorGUILayout.PropertyField(_sharedStandMaterialProp, new GUIContent("客席共有マテリアル"));

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
                            $"チャンクメッシュとマテリアルを Project へ保存しました。\nこの状態でそのまま Prefab 化できます。\n\n保存先:\n{savedDir}",
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
                        "1. 生成済みメッシュ＆マテリアルを Project に永続保存(Bake)します\n2. プレビューマーカーを削除します\n3. StadiumCrowdGenerator スクリプトを取り外します（Undo可）\n\n※ CPU側の視線追従(TargetPos更新)は停止しC#負荷は完全ゼロになりますが、GPU頂点アニメと遠景距離カリングは引き続き動作します。\n\n実行しますか？",
                        "完全静的化する",
                        "キャンセル");

                    if (confirmed)
                    {
                        serializedObject.ApplyModifiedProperties();
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
                gen.GenerateCrowd();
            }
            else if (needMaterialUpdate)
            {
                gen.ApplyMaterialSettingsToAllChunks();
            }
        }

        private void DrawHeaderStatus(StadiumCrowdGenerator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🏟️ スタジアム観客システム【案A：軽量3D結合メッシュ＋GPU頂点追従】", EditorStyles.boldLabel);
            string runtimeTag = Application.isPlaying
                ? (gen.IsTrackingPausedByDistance ? "  |  状態: ⏸️ 遠距離CPU追従停止中" : "  |  状態: ▶️ CPU追従稼働中")
                : (gen.IsBakedAssets ? "  |  保存: ✅ 永続ベイク済み" : "  |  保存: 未ベイク");
            EditorGUILayout.LabelField(
                $"人数: {gen.GeneratedSpectatorCount:N0} 人  |  チャンク: {gen.GeneratedChunkCount}  |  ポリゴン: {gen.GeneratedTriangleCount:N0} tris{runtimeTag}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainActionButtons(StadiumCrowdGenerator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _autoRebuildOnParamChange = EditorGUILayout.ToggleLeft("パラメータ変更時に自動で観客を再生成する (Auto Rebuild)", _autoRebuildOnParamChange);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f);
            if (GUILayout.Button("🔄 観客＆客席を生成・更新", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Generate Stadium Crowd");
                gen.GenerateCrowd();
            }

            GUI.backgroundColor = new Color(1.0f, 0.65f, 0.30f);
            if (GUILayout.Button("➕ 途切れ・除外ゾーン追加", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Add Crowd Exclusion Zone");
                CrowdExclusionZone zone = gen.AddExclusionZone();
                gen.GenerateCrowd();
                Selection.activeGameObject = zone.gameObject;
            }

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ クリア", GUILayout.Width(72), GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Clear Stadium Crowd");
                gen.ClearGeneratedCrowd();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSectorToggleGrid(StadiumCrowdGenerator gen, ref bool needMeshRebuild)
        {
            int count = Mathf.Clamp(gen.SectorCount, 1, 24);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("ブロック別 ON/OFF（クリックで客席を部分的に途切れさせられます）", EditorStyles.miniBoldLabel);

            // Quick sector pattern buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全ブロックON", EditorStyles.miniButtonLeft))
            {
                Undo.RecordObject(gen, "All Sectors ON");
                gen.SectorActiveMask = 0xFFFFFF;
                needMeshRebuild = true;
            }
            if (GUILayout.Button("コース貫通 (前後空き)", EditorStyles.miniButtonMid))
            {
                Undo.RecordObject(gen, "Course Pass-Through Sectors");
                int mask = 0;
                for (int i = 0; i < count; i++)
                {
                    float norm = (i + 0.5f) / count;
                    // Turn off sectors near 0/1 and 0.5
                    bool isGate = (norm < 0.12f || norm > 0.88f || Mathf.Abs(norm - 0.5f) < 0.12f);
                    if (!isGate) mask |= (1 << i);
                }
                gen.SectorActiveMask = mask;
                needMeshRebuild = true;
            }
            if (GUILayout.Button("馬蹄形 (一部開口)", EditorStyles.miniButtonMid))
            {
                Undo.RecordObject(gen, "Horseshoe Sectors");
                int mask = 0;
                for (int i = 0; i < count; i++)
                {
                    if (i != 0 && i != count - 1) mask |= (1 << i);
                }
                gen.SectorActiveMask = mask;
                needMeshRebuild = true;
            }
            if (GUILayout.Button("1つおき配置", EditorStyles.miniButtonRight))
            {
                Undo.RecordObject(gen, "Alternating Sectors");
                int mask = 0;
                for (int i = 0; i < count; i++)
                {
                    if (i % 2 == 0) mask |= (1 << i);
                }
                gen.SectorActiveMask = mask;
                needMeshRebuild = true;
            }
            EditorGUILayout.EndHorizontal();

            // Individual buttons
            int buttonsPerRow = 8;
            for (int i = 0; i < count; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                int end = Mathf.Min(count, i + buttonsPerRow);
                for (int s = i; s < end; s++)
                {
                    bool active = gen.IsSectorActive(s);
                    GUI.backgroundColor = active ? new Color(0.35f, 0.85f, 0.55f) : new Color(0.45f, 0.45f, 0.45f);
                    if (GUILayout.Button($"#{s + 1}", GUILayout.Height(22)))
                    {
                        Undo.RecordObject(gen, "Toggle Crowd Sector");
                        gen.SetSectorActive(s, !active);
                        needMeshRebuild = true;
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void OnSceneGUI()
        {
            StadiumCrowdGenerator gen = (StadiumCrowdGenerator)target;
            if (gen == null) return;

            // Draw draggable handles for CustomPathStand mode
            SerializedProperty modeProp = serializedObject.FindProperty("_placementMode");
            if (modeProp != null && modeProp.enumValueIndex == (int)StadiumCrowdGenerator.PlacementMode.CustomPathStand)
            {
                var pts = gen.PathPoints;
                if (pts != null && pts.Count >= 2)
                {
                    Handles.color = new Color(0.25f, 0.9f, 0.55f, 0.9f);
                    bool changed = false;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Vector3 worldPt = gen.transform.TransformPoint(pts[i]);
                        EditorGUI.BeginChangeCheck();
                        Vector3 newWorldPt = Handles.PositionHandle(worldPt, gen.transform.rotation);
                        Handles.Label(worldPt + Vector3.up * 1.5f, $"Path [{i}]");
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(gen, "Move Crowd Path Point");
                            pts[i] = gen.transform.InverseTransformPoint(newWorldPt);
                            changed = true;
                        }

                        if (i + 1 < pts.Count)
                        {
                            Handles.DrawDottedLine(worldPt, gen.transform.TransformPoint(pts[i + 1]), 4f);
                        }
                    }

                    if (changed && _autoRebuildOnParamChange)
                    {
                        gen.GenerateCrowd();
                    }
                }
            }
        }
    }
}

