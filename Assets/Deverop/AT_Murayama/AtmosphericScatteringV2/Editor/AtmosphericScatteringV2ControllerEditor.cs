using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.AtmosphericScatteringV2.Editor
{
    [CustomEditor(typeof(AtmosphericScatteringV2Controller))]
    public class AtmosphericScatteringV2ControllerEditor : UnityEditor.Editor
    {
        private const string MaterialsFolderPath = "Assets/Deverop/AT_Murayama/AtmosphericScatteringV2/Materials";

        private bool _activeProfileFoldout = true;
        private bool _dayProfileFoldout = true;
        private bool _sunsetProfileFoldout = true;
        private bool _nightProfileFoldout = true;
        private bool _advancedSettingsFoldout = false;

        public override void OnInspectorGUI()
        {
            AtmosphericScatteringV2Controller controller = (AtmosphericScatteringV2Controller)target;
            serializedObject.Update();

            EditorGUILayout.Space(4);
            DrawHeaderBox(controller);

            EditorGUILayout.Space(6);
            CheckAndDrawV1Warning();
            CheckAndDrawFarClipWarning(controller);

            EditorGUILayout.Space(4);
            DrawMasterPresetButtons(controller);

            EditorGUILayout.Space(6);
            DrawTimeOfDaySection(controller);

            EditorGUILayout.Space(6);
            DrawExtensionTogglesSection(controller);

            EditorGUILayout.Space(6);
            DrawProfileSettings(controller);

            EditorGUILayout.Space(6);
            DrawAdvancedSettings(controller);

            EditorGUILayout.Space(6);
            DrawSaveMaterialButton(controller);

            if (serializedObject.ApplyModifiedProperties())
            {
                controller.UpdateMaterialProperties();
                SceneView.RepaintAll();
            }
        }

        private void DrawHeaderBox(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌌 大気散乱 V2 (Atmospheric Scattering V2)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "・昼夜時刻補間（Time of Day）/ 太陽方向 Mie 散乱 / 指数オーバーフロー防止・高度フォグ / 2000m超 Dual-Range 対応\n" +
                "・Half-Res 3パス＋バイラテラル深度保護合成により、低負荷かつエッジの破綻・チラつきを防止します。\n" +
                "・Pass 2 / Pass 3 で共通関数を用いて差分逆算を整合し、Half-Res/Full-Res間の色味・フォグのズレをゼロ化しています。",
                MessageType.Info);

            // ON / OFF ワンクリック比較ボタン
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = controller.EnableEffect ? new Color(0.35f, 0.9f, 0.55f) : new Color(0.85f, 0.45f, 0.45f);
            string toggleLabel = controller.EnableEffect
                ? "✅ 現在：ON（クリックで一時OFFにして元画面と比較）"
                : "🚫 現在：OFF（クリックでONに戻す）";

            if (GUILayout.Button(toggleLabel, GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Toggle Atmospheric Scattering V2");
                controller.EnableEffect = !controller.EnableEffect;
                serializedObject.Update();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(3);

            // Half-Res / Full-Res ワンクリック切り替え比較ボタン
            GUI.backgroundColor = controller.UseHalfResolutionBuffer ? new Color(0.45f, 0.82f, 1.0f) : new Color(0.95f, 0.8f, 0.4f);
            string resLabel = controller.UseHalfResolutionBuffer
                ? "🚀 解像度モード：Half-Res 1/2バッファ（推奨：低負荷＆遠景チラつき防止） [クリックでFull-Resと比較]"
                : "🔍 解像度モード：Full-Res 等倍1パス [クリックで推奨のHalf-Resに戻す]";

            if (GUILayout.Button(resLabel, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Toggle Atmospheric Scattering V2 Resolution Mode");
                controller.UseHalfResolutionBuffer = !controller.UseHalfResolutionBuffer;
                serializedObject.Update();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void CheckAndDrawV1Warning()
        {
            // 他フォルダの型参照・using は100%禁止のため、文字列判定とリフレクションで安全に検出・制御
            MonoBehaviour[] allBehaviors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            List<MonoBehaviour> activeV1List = new List<MonoBehaviour>();

            foreach (MonoBehaviour mb in allBehaviors)
            {
                if (mb != null && mb.enabled && mb.gameObject.activeInHierarchy && mb.GetType().Name == "AtmosphericScatteringController")
                {
                    var prop = mb.GetType().GetProperty("EnableEffect");
                    bool isEffectOn = (prop != null && prop.PropertyType == typeof(bool))
                        ? (bool)prop.GetValue(mb)
                        : true;
                    if (isEffectOn)
                    {
                        activeV1List.Add(mb);
                    }
                }
            }

            if (activeV1List.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox(
                    "⚠️ 同一シーン内に有効な旧バージョン（V1: AtmosphericScatteringController）が存在します！\n" +
                    "V1 と V2 が同時に有効だと、大気散乱パスが二重に実行され画面が極端に白濁・暗転する原因になります。",
                    MessageType.Warning);

                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
                if (GUILayout.Button("🛑 同一シーンの旧 V1 コントローラーを一時 OFF にする", GUILayout.Height(28)))
                {
                    foreach (MonoBehaviour v1 in activeV1List)
                    {
                        Undo.RecordObject(v1, "Disable V1 Atmospheric Scattering");
                        var prop = v1.GetType().GetProperty("EnableEffect");
                        if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(v1, false);
                        }
                        else
                        {
                            v1.enabled = false;
                        }
                        EditorUtility.SetDirty(v1);
                    }
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = prevColor;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        private void CheckAndDrawFarClipWarning(AtmosphericScatteringV2Controller controller)
        {
            if (controller.EnableDualRange)
            {
                Camera cam = Camera.main;
                if (cam != null && cam.farClipPlane < controller.EffectiveFarDistance)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.HelpBox(
                        $"⚠️ Main Camera の farClipPlane ({cam.farClipPlane:F0}m) が、Dual-Range の最大距離 ({controller.EffectiveFarDistance:F0}m) より短いため、遠景フォグがクリッピングされる可能性があります。\n" +
                        $"Camera の Far Clip Plane を {controller.EffectiveFarDistance:F0}m 以上に広げることを推奨します（※自動変更は行いません）。",
                        MessageType.Warning);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(4);
                }
            }
        }

        private void DrawMasterPresetButtons(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 ワンクリック・マスタープリセット", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌃 ① V1互換サイバー夜\n(CyberNight_V1Compatible)\n全拡張OFF・V1と完全同一", GUILayout.Height(46)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringV2Controller.MasterPresetV2.CyberNight_V1Compatible);
            }
            if (GUILayout.Button("☀️ ② 快晴の昼\n(RealisticDay_Clear)\nDual-Range 2000m＋Mie＋高度フォグ", GUILayout.Height(46)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringV2Controller.MasterPresetV2.RealisticDay_Clear);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌇 ③ ゴールデンアワー夕暮れ\n(GoldenHour_Sunset)\n暖色大気＋強Mie＋低層スモッグ", GUILayout.Height(46)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringV2Controller.MasterPresetV2.GoldenHour_Sunset);
            }
            if (GUILayout.Button("🌆 ④ メガシティ昼夜自動補間\n(MegaCity_DayNightAuto)\n昼/夕/夜 3プロファイル自動ブレンド", GUILayout.Height(46)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringV2Controller.MasterPresetV2.MegaCity_DayNightAuto);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTimeOfDaySection(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌓 昼夜・時刻コントローラー (Time of Day)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool todEnabled = EditorGUILayout.Toggle("昼夜補間を有効にする", controller.EnableTimeOfDay);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle Time of Day");
                controller.EnableTimeOfDay = todEnabled;
                serializedObject.Update();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            if (controller.EnableTimeOfDay)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                AtmosphericScatteringV2Controller.TimeSourceMode source =
                    (AtmosphericScatteringV2Controller.TimeSourceMode)EditorGUILayout.EnumPopup("時刻ソース", controller.TimeSource);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change Time Source");
                    controller.TimeSource = source;
                    serializedObject.Update();
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }

                if (controller.TimeSource == AtmosphericScatteringV2Controller.TimeSourceMode.Manual)
                {
                    EditorGUI.BeginChangeCheck();
                    float newTod = EditorGUILayout.Slider("時刻 (0:昼, 0.5:夕, 1:夜)", controller.TimeOfDay, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(controller, "Change Time of Day");
                        controller.TimeOfDay = newTod;
                        serializedObject.Update();
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("☀️ 昼 (0.0)", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(controller, "Set TimeOfDay to Day");
                        controller.TimeOfDay = 0.0f;
                        serializedObject.Update();
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("🌇 夕 (0.5)", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(controller, "Set TimeOfDay to Sunset");
                        controller.TimeOfDay = 0.5f;
                        serializedObject.Update();
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("🌙 夜 (1.0)", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(controller, "Set TimeOfDay to Night");
                        controller.TimeOfDay = 1.0f;
                        serializedObject.Update();
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    Light sun = (Light)EditorGUILayout.ObjectField("太陽光 (Directional Light)", controller.SunLight, typeof(Light), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(controller, "Change Sun Light");
                        controller.SunLight = sun;
                        serializedObject.Update();
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }

                    float autoTod = controller.ComputeTimeFromSunElevation();
                    string phaseStr = autoTod < 0.25f ? "☀️ 昼" : (autoTod < 0.75f ? "🌇 夕暮れ" : "🌙 夜");
                    EditorGUILayout.HelpBox($"自動算出時刻: {autoTod:F2} ({phaseStr})  ※太陽光の向きに応じて自動補間されます", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawExtensionTogglesSection(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚙️ 拡張機能個別トグル（プロファイル値を壊さずに即時ON/OFF比較可能）", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool dual = EditorGUILayout.ToggleLeft("📏 2段距離カーブ（Dual-Range 2000〜3000mスケール対応）", controller.EnableDualRange);
            bool height = EditorGUILayout.ToggleLeft("☁️ 高度フォグ（Height Fog）", controller.EnableHeightFog);
            bool mie = EditorGUILayout.ToggleLeft("☀️ 太陽方向 Mie 散乱（前方散乱グロー）", controller.EnableMieScattering);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle V2 Extension Feature");
                controller.EnableDualRange = dual;
                controller.EnableHeightFog = height;
                controller.EnableMieScattering = mie;
                serializedObject.Update();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProfileSettings(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📋 プロファイル詳細パラメータ調整", EditorStyles.boldLabel);

            if (!controller.EnableTimeOfDay)
            {
                SerializedProperty activeProp = serializedObject.FindProperty("_activeProfile");
                DrawProfileProperty(activeProp, "単一アクティブプロファイル", ref _activeProfileFoldout);
            }
            else
            {
                SerializedProperty dayProp = serializedObject.FindProperty("_dayProfile");
                DrawProfileProperty(dayProp, "☀️ 昼プロファイル (TimeOfDay = 0.0)", ref _dayProfileFoldout);

                EditorGUILayout.Space(4);
                SerializedProperty sunsetProp = serializedObject.FindProperty("_sunsetProfile");
                DrawProfileProperty(sunsetProp, "🌇 夕暮れプロファイル (TimeOfDay = 0.5)", ref _sunsetProfileFoldout);

                EditorGUILayout.Space(4);
                SerializedProperty nightProp = serializedObject.FindProperty("_nightProfile");
                DrawProfileProperty(nightProp, "🌙 夜プロファイル (TimeOfDay = 1.0)", ref _nightProfileFoldout);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProfileProperty(SerializedProperty profileProp, string title, ref bool foldout)
        {
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (!foldout || profileProp == null) return;

            EditorGUI.indentLevel++;

            // 1. Optical Depth
            EditorGUILayout.LabelField("1. 空気の厚み（Optical Depth）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("startDistance"), new GUIContent("開始距離 (m)"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("maxDistance"), new GUIContent("最大距離 (m)"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("densityCurve"), new GUIContent("距離増大カーブ"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("skyboxInfluence"), new GUIContent("Skyboxベースフォグ比率"));

            // 2. Dual-Range
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("2. 2段距離カーブ（Dual-Range 2000m〜3000m）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("nearEndDistance"), new GUIContent("近景保護帯終端 (m)"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("farEndDistance"), new GUIContent("遠景帯終端 (m)"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("nearMaxAirMass"), new GUIContent("近景帯上限AirMass"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("farDensityCurve"), new GUIContent("遠景帯密度カーブ"));

            // 3. Blur
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("3. 遠景の像のボケ（Atmospheric Blur）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("blurRadius"), new GUIContent("ボケ半径"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("depthEdgeSharpness"), new GUIContent("手前輪郭保護強度"));

            // 4. Light In-Scattering
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("4. 発光・ライトの散乱（Light In-Scattering）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("scatterIntensity"), new GUIContent("散乱光強度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("scatterRadius"), new GUIContent("散乱光拡散半径"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("scatterThreshold"), new GUIContent("光源抽出しきい値"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("scatterSoftKnee"), new GUIContent("しきい値ソフトネス"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("scatterTint"), new GUIContent("散乱光チリ色"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("lightColorPreservation"), new GUIContent("元光源色残存比率"));

            // 5. Anti-Flicker
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("5. 遠景発光チラつき防止（Anti-Flicker）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("maxScatterLuminance"), new GUIContent("最大輝度ソフトクランプ"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("antiFlickerStrength"), new GUIContent("Karis重み強度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("noiseJitter"), new GUIContent("ノイズジッター"));

            // 6. Aerial Perspective
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("6. 大気の環境散乱・暗部シルエット化（Aerial Perspective）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("atmosphereColor"), new GUIContent("大気ベース色"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("atmosphereDensity"), new GUIContent("大気ベース濃度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("contrastReduction"), new GUIContent("遠景コントラスト減衰"));

            // 7. Height Fog
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("7. 高度フォグ（Height Fog）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("heightFogStrength"), new GUIContent("高度フォグ強度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("fogBaseHeight"), new GUIContent("フォグ基準高度 Y (m)"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("heightFalloff"), new GUIContent("上空減衰率"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("skyHeightClear"), new GUIContent("地平線・空整合係数"));

            // 8. Mie Scattering
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("8. 太陽方向 Mie 散乱（前方散乱）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("mieIntensity"), new GUIContent("Mie散乱強度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("mieAnisotropy"), new GUIContent("前方異方性 g"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("skyboxMieInfluence"), new GUIContent("Skybox Mie影響度"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("mieMaxLuminance"), new GUIContent("Mie輝度ソフトクランプ"));
            EditorGUILayout.PropertyField(profileProp.FindPropertyRelative("mieTint"), new GUIContent("Mie色味調整"));

            EditorGUI.indentLevel--;
        }

        private void DrawAdvancedSettings(AtmosphericScatteringV2Controller controller)
        {
            _advancedSettingsFoldout = EditorGUILayout.Foldout(_advancedSettingsFoldout, "🔧 高度な設定・パイプライン注入", true);
            if (!_advancedSettingsFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_overallStrength"), new GUIContent("全体強度 (Overall Strength)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_applyToSceneView"), new GUIContent("Sceneビュー適用"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_injectionPoint"), new GUIContent("RenderPass注入タイミング"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sunLight"), new GUIContent("太陽光ライト参照"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_scatteringMaterial"), new GUIContent("参照マテリアル"));
            EditorGUI.indentLevel--;
        }

        private void DrawSaveMaterialButton(AtmosphericScatteringV2Controller controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("💾 現在の設定をマテリアルアセット (M_AtmosphericScatteringV2.mat) として保存", GUILayout.Height(26)))
            {
                SaveMaterialAsset(controller);
            }
            EditorGUILayout.EndVertical();
        }

        private void ApplyPresetWithUndo(AtmosphericScatteringV2Controller controller, AtmosphericScatteringV2Controller.MasterPresetV2 preset)
        {
            Undo.RecordObject(controller, $"Apply Atmospheric Scattering V2 Preset {preset}");
            controller.ApplyMasterPreset(preset);
            serializedObject.Update();
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }

        private void SaveMaterialAsset(AtmosphericScatteringV2Controller controller)
        {
            if (!Directory.Exists(MaterialsFolderPath))
            {
                Directory.CreateDirectory(MaterialsFolderPath);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{MaterialsFolderPath}/M_AtmosphericScatteringV2.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            controller.UpdateMaterialProperties();
            Material runtimeMat = controller.GetActiveMaterial();

            if (runtimeMat == null)
            {
                Debug.LogWarning("保存元のマテリアルが見つかりません。");
                return;
            }

            if (existing == null)
            {
                Material newMat = new Material(runtimeMat)
                {
                    name = "M_AtmosphericScatteringV2",
                    hideFlags = HideFlags.None
                };
                AssetDatabase.CreateAsset(newMat, assetPath);
                existing = newMat;
            }
            else
            {
                existing.CopyPropertiesFromMaterial(runtimeMat);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            controller.SetMaterialAsset(existing);
            EditorUtility.SetDirty(controller);
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"[AtmosphericScatteringV2] マテリアルを保存しました: {assetPath}");
        }
    }
}
