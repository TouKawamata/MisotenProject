using System.IO;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.AtmosphericScattering.Editor
{
    [CustomEditor(typeof(AtmosphericScatteringController))]
    public class AtmosphericScatteringControllerEditor : UnityEditor.Editor
    {
        private const string MaterialsFolderPath = "Assets/Deverop/AT_Murayama/AtmosphericScattering/Materials";

        public override void OnInspectorGUI()
        {
            AtmosphericScatteringController controller = (AtmosphericScatteringController)target;

            EditorGUILayout.Space(4);
            DrawHeaderBox(controller);

            EditorGUILayout.Space(6);
            DrawPresetButtons(controller);

            EditorGUILayout.Space(6);
            DrawTestCityButtons(controller);

            EditorGUILayout.Space(6);
            DrawSaveMaterialButton(controller);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("詳細パラメーター調整", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        private void DrawHeaderBox(AtmosphericScatteringController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌌 深度連動・大気散乱＆遠景ボケ (Atmospheric Scattering)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "・カメラからの距離（空気の厚み）と、そこにある発光・ライトから『遠景のチリによるボケ』と『光の散乱（In-Scattering）』を計算します。\n" +
                "・Half-Res（1/2解像度）バッファにより、負荷を約1/4に抑えつつカメラ移動時の遠景の発光チラつきを防止します。\n" +
                "・Farギリギリのオブジェクトも空と誤判定しない厳密Skybox判定対応済みです。",
                MessageType.Info);

            // ON / OFF ワンクリック比較ボタン
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = controller.EnableEffect ? new Color(0.35f, 0.9f, 0.55f) : new Color(0.85f, 0.45f, 0.45f);
            string toggleLabel = controller.EnableEffect
                ? "✅ 現在：ON（クリックで一時OFFにして元画面と比較）"
                : "🚫 現在：OFF（クリックでONに戻す）";

            if (GUILayout.Button(toggleLabel, GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Toggle Atmospheric Scattering");
                controller.EnableEffect = !controller.EnableEffect;
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
                Undo.RecordObject(controller, "Toggle Atmospheric Scattering Resolution Mode");
                controller.UseHalfResolutionBuffer = !controller.UseHalfResolutionBuffer;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetButtons(AtmosphericScatteringController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 空気感プリセット（ワンクリック切り替え）", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌃 ① 画像再現：サイバー夜景\n(シアン散乱＋遠景霞み)", GUILayout.Height(36)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringController.ScatteringPreset.CyberpunkNight_Reference);
            }
            if (GUILayout.Button("🌫️ ② 濃霧・スモッグ都市\n(強い散乱＋深いボケ)", GUILayout.Height(36)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringController.ScatteringPreset.HeavySmogCity);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("✨ ③ クリアナイト\n(控えめな空気感)", GUILayout.Height(32)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringController.ScatteringPreset.ClearNightHaze);
            }
            if (GUILayout.Button("🌇 ④ 夕暮れ・暖色スモッグ\n(アンバー散乱)", GUILayout.Height(32)))
            {
                ApplyPresetWithUndo(controller, AtmosphericScatteringController.ScatteringPreset.WarmSunsetHaze);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTestCityButtons(AtmosphericScatteringController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🏙️ 動作確認用・遠景サイバービル群（ワンクリック生成／削除）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("※まだシーンに遠景ビルが無い場合でも、画像左奥のようなビル群＋地上ネオンを仮配置して散乱効果をすぐ確認できます。", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.8f, 1.0f);
            if (GUILayout.Button("🏙️ テスト用・遠景ビル群を生成", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Generate Test Cyber City");
                controller.GenerateTestCyberCity();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = controller.HasTestCity ? new Color(1.0f, 0.55f, 0.55f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!controller.HasTestCity);
            if (GUILayout.Button("🧹 テストビル群を削除", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Clear Test Cyber City");
                controller.ClearTestCyberCity();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveMaterialButton(AtmosphericScatteringController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("💾 現在の設定をマテリアルアセット (M_AtmosphericScattering.mat) として保存", GUILayout.Height(26)))
            {
                SaveMaterialAsset(controller);
            }
            EditorGUILayout.EndVertical();
        }

        private void ApplyPresetWithUndo(AtmosphericScatteringController controller, AtmosphericScatteringController.ScatteringPreset preset)
        {
            Undo.RecordObject(controller, "Apply Atmospheric Scattering Preset");
            controller.ApplyPreset(preset);
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }

        private void SaveMaterialAsset(AtmosphericScatteringController controller)
        {
            if (!Directory.Exists(MaterialsFolderPath))
            {
                Directory.CreateDirectory(MaterialsFolderPath);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{MaterialsFolderPath}/M_AtmosphericScattering.mat";
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
                    name = "M_AtmosphericScattering",
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
            Debug.Log($"[AtmosphericScattering] マテリアルを保存しました: {assetPath}");
        }
    }
}
