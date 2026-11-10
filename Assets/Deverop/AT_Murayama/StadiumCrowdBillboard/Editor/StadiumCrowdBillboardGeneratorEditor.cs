using UnityEditor;
using UnityEngine;

namespace AT_Murayama.StadiumCrowdBillboard.Editor
{
    /// <summary>
    /// 【案B：ビルボード観客システム専用】カスタムインスペクター。
    /// ※上部メニュー（[MenuItem]）は一切使用せず、インスペクターUIとSceneギズモのみで完結。
    /// ※案A（StadiumCrowd）とは完全に独立しているため、将来どちらか片方を削除してもエラーになりません。
    /// </summary>
    [CustomEditor(typeof(StadiumCrowdBillboardGenerator))]
    public class StadiumCrowdBillboardGeneratorEditor : UnityEditor.Editor
    {
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

        private SerializedProperty _sharedBillboardMaterialProp;
        private SerializedProperty _sharedStandMaterialProp;

        private bool _autoRebuildOnParamChange = true;
        private bool _showAppearanceSection = true;
        private bool _showPlacementSection = true;
        private bool _showTrackingSection = true;
        private bool _showInternalSection = false;

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

            _sharedBillboardMaterialProp = serializedObject.FindProperty("_sharedBillboardMaterial");
            _sharedStandMaterialProp = serializedObject.FindProperty("_sharedStandMaterial");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StadiumCrowdBillboardGenerator gen = (StadiumCrowdBillboardGenerator)target;

            DrawHeaderStatus(gen);
            EditorGUILayout.Space(4);
            DrawMainActionButtons(gen);
            EditorGUILayout.Space(6);

            bool needMeshRebuild = false;
            bool needMaterialUpdate = false;

            // -----------------------------------------------------------------
            // Section 1: ビルボード方式 & スタイル設定
            // -----------------------------------------------------------------
            _showAppearanceSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showAppearanceSection, "1. ビルボード方式 & 観客スタイル（案Aとの比較用）");
            if (_showAppearanceSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_billboardOrientationProp, new GUIContent("ビルボード回転方式"));
                if (_billboardOrientationProp.enumValueIndex == (int)StadiumCrowdBillboardGenerator.BillboardOrientation.HybridSkyTilt)
                {
                    EditorGUILayout.PropertyField(_verticalTiltWeightProp, new GUIContent("上空俯瞰チルト補正強度"));
                }
                EditorGUILayout.PropertyField(_billboardStyleProp, new GUIContent("観客シルエット形状"));
                if (_billboardStyleProp.enumValueIndex == (int)StadiumCrowdBillboardGenerator.BillboardStyle.CustomSpriteTexture)
                {
                    EditorGUILayout.PropertyField(_customSpriteTextureProp, new GUIContent("カスタム2Dスプライト"));
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
                    Undo.RecordObject(gen, "Change Billboard Crowd Color Theme");
                    gen.ApplyColorThemePreset((StadiumCrowdBillboardGenerator.CrowdColorTheme)_colorThemeProp.enumValueIndex);
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
                    _colorThemeProp.enumValueIndex = (int)StadiumCrowdBillboardGenerator.CrowdColorTheme.Custom;
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
                StadiumCrowdBillboardGenerator.PlacementMode mode = (StadiumCrowdBillboardGenerator.PlacementMode)_placementModeProp.enumValueIndex;

                EditorGUILayout.Space(2);
                if (mode == StadiumCrowdBillboardGenerator.PlacementMode.CircularBowl)
                {
                    EditorGUILayout.PropertyField(_innerRadiusXProp, new GUIContent("フィールド内径 X (m)"));
                    EditorGUILayout.PropertyField(_innerRadiusZProp, new GUIContent("フィールド内径 Z (m)"));
                    EditorGUILayout.PropertyField(_straightTrackLengthProp, new GUIContent("直線トラック延長 (m)"));
                    EditorGUILayout.PropertyField(_startAngleDegProp, new GUIContent("開始角度 (度)"));
                    EditorGUILayout.PropertyField(_arcAngleDegProp, new GUIContent("全体角度 (360=全周)"));
                }
                else if (mode == StadiumCrowdBillboardGenerator.PlacementMode.LinearStand)
                {
                    EditorGUILayout.PropertyField(_linearWidthProp, new GUIContent("スタンド全長幅 (m)"));
                }
                else if (mode == StadiumCrowdBillboardGenerator.PlacementMode.CustomPathStand)
                {
                    EditorGUILayout.HelpBox("Sceneビューのハンドルをドラッグして、自由な折れ線・多角形の客席ラインを編集できます。", MessageType.Info);
                    EditorGUILayout.PropertyField(_closePathLoopProp, new GUIContent("始点と終点をループ接続"));
                    EditorGUILayout.PropertyField(_pathPointsProp, new GUIContent("制御点リスト"), true);
                }
                else if (mode == StadiumCrowdBillboardGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    EditorGUILayout.HelpBox("既存のスタジアム3Dモデルの表面に自動でビルボード観客を立たせます。", MessageType.Info);
                    EditorGUILayout.PropertyField(_targetSurfaceRootProp, new GUIContent("対象3Dモデルの親Transform"));
                    EditorGUILayout.PropertyField(_targetMeshFiltersProp, new GUIContent("または個別MeshFilter"), true);
                    EditorGUILayout.PropertyField(_minSurfaceNormalYProp, new GUIContent("最小上向き法線Y (傾斜制限)"));
                    EditorGUILayout.PropertyField(_surfaceLookCenterLocalProp, new GUIContent("視線中心ローカル座標"));
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("段数・座席間隔・途切れ設定", EditorStyles.boldLabel);
                if (mode != StadiumCrowdBillboardGenerator.PlacementMode.MeshSurfaceScatter)
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

                if (mode != StadiumCrowdBillboardGenerator.PlacementMode.MeshSurfaceScatter)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_sectorCountProp, new GUIContent("ブロック(セクター)分割数"));
                    EditorGUILayout.PropertyField(_aisleGapMetersProp, new GUIContent("ブロック間の通路隙間 (m)"));
                }

                if (EditorGUI.EndChangeCheck())
                {
                    needMeshRebuild = true;
                }

                if (mode != StadiumCrowdBillboardGenerator.PlacementMode.MeshSurfaceScatter)
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
                StadiumCrowdBillboardGenerator.TargetTrackingMode tMode = (StadiumCrowdBillboardGenerator.TargetTrackingMode)_trackingModeProp.enumValueIndex;

                if (tMode == StadiumCrowdBillboardGenerator.TargetTrackingMode.SkyDiveSimulation)
                {
                    EditorGUILayout.PropertyField(_simStartHeightProp, new GUIContent("上空の開始高度 (m)"));
                    EditorGUILayout.PropertyField(_simApproachOffsetXZProp, new GUIContent("上空の進入オフセットXZ"));
                    EditorGUILayout.PropertyField(_simLandingPointLocalProp, new GUIContent("着地地点ローカル座標"));
                    EditorGUILayout.PropertyField(_simCycleDurationProp, new GUIContent("降下ループ周期 (秒)"));
                }
                else if (tMode == StadiumCrowdBillboardGenerator.TargetTrackingMode.ManualAltitudeScrub)
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
            // Section 4: 内部マテリアル参照
            // -----------------------------------------------------------------
            _showInternalSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showInternalSection, "4. マテリアル参照 & 最適化ツール");
            if (_showInternalSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(_sharedBillboardMaterialProp, new GUIContent("ビルボード共有マテリアル"));
                EditorGUILayout.PropertyField(_sharedStandMaterialProp, new GUIContent("客席共有マテリアル"));
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

        private void DrawHeaderStatus(StadiumCrowdBillboardGenerator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🪧 スタジアム観客システム【案B：ビルボード板ポリ＋2.5Dシェーダー追従】", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"現在の配置人数: {gen.GeneratedSpectatorCount:N0} 人  |  描画チャンク数(DrawCall): {gen.GeneratedChunkCount}  |  総ポリゴン: {gen.GeneratedTriangleCount:N0} tris",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainActionButtons(StadiumCrowdBillboardGenerator gen)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _autoRebuildOnParamChange = EditorGUILayout.ToggleLeft("パラメータ変更時に自動で観客を再生成する (Auto Rebuild)", _autoRebuildOnParamChange);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.80f, 0.95f);
            if (GUILayout.Button("🔄 ビルボード観客を生成・更新", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Generate Billboard Stadium Crowd");
                gen.GenerateCrowd();
            }

            GUI.backgroundColor = new Color(1.0f, 0.65f, 0.30f);
            if (GUILayout.Button("➕ 途切れ・除外ゾーン追加", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Add Billboard Exclusion Zone");
                BillboardExclusionZone zone = gen.AddExclusionZone();
                gen.GenerateCrowd();
                Selection.activeGameObject = zone.gameObject;
            }

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ クリア", GUILayout.Width(72), GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gen.gameObject, "Clear Billboard Stadium Crowd");
                gen.ClearGeneratedCrowd();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSectorToggleGrid(StadiumCrowdBillboardGenerator gen, ref bool needMeshRebuild)
        {
            int count = Mathf.Clamp(gen.SectorCount, 1, 24);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("ブロック別 ON/OFF（クリックで客席を部分的に途切れさせられます）", EditorStyles.miniBoldLabel);

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

            int buttonsPerRow = 8;
            for (int i = 0; i < count; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                int end = Mathf.Min(count, i + buttonsPerRow);
                for (int s = i; s < end; s++)
                {
                    bool active = gen.IsSectorActive(s);
                    GUI.backgroundColor = active ? new Color(0.35f, 0.80f, 0.95f) : new Color(0.45f, 0.45f, 0.45f);
                    if (GUILayout.Button($"#{s + 1}", GUILayout.Height(22)))
                    {
                        Undo.RecordObject(gen, "Toggle Billboard Crowd Sector");
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
            StadiumCrowdBillboardGenerator gen = (StadiumCrowdBillboardGenerator)target;
            if (gen == null) return;

            SerializedProperty modeProp = serializedObject.FindProperty("_placementMode");
            if (modeProp != null && modeProp.enumValueIndex == (int)StadiumCrowdBillboardGenerator.PlacementMode.CustomPathStand)
            {
                var pts = gen.PathPoints;
                if (pts != null && pts.Count >= 2)
                {
                    Handles.color = new Color(0.35f, 0.85f, 1.0f, 0.9f);
                    bool changed = false;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Vector3 worldPt = gen.transform.TransformPoint(pts[i]);
                        EditorGUI.BeginChangeCheck();
                        Vector3 newWorldPt = Handles.PositionHandle(worldPt, gen.transform.rotation);
                        Handles.Label(worldPt + Vector3.up * 1.5f, $"Path [{i}]");
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(gen, "Move Billboard Path Point");
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

