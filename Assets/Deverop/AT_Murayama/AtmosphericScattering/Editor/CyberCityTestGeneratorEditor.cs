using UnityEditor;
using UnityEngine;

namespace AT_Murayama.AtmosphericScattering.Editor
{
    [CustomEditor(typeof(CyberCityTestGenerator))]
    public class CyberCityTestGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CyberCityTestGenerator generator = (CyberCityTestGenerator)target;

            EditorGUILayout.Space(4);
            DrawHeaderBox(generator);

            EditorGUILayout.Space(6);
            DrawPresetButtons(generator);

            EditorGUILayout.Space(6);
            DrawActionButtons(generator);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("詳細パラメーター調整", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        private void DrawHeaderBox(CyberCityTestGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🏙️ 動作確認用・サイバーパンク都市ジェネレーター", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "大気散乱（Atmospheric Scattering）や遠景フォグの効果をテストするための高層ビル群＋地上ネオン街灯をワンクリック生成・一括削除できます。\n" +
                "・近景〜超遠景（数百m〜数千m）の広大なシーンを瞬時に構築できます。\n" +
                "・不要になった際は赤色ボタン『ビル群を一括削除』を押せば、シーンや本番アセットを一切汚さずに綺麗に消去できます。",
                MessageType.Info);

            string statusText = generator.HasGeneratedCity
                ? $"✅ 生成済み：現在 {generator.BuildingCount}棟（最遠 {generator.MaxDistance:F0}m まで展開中）"
                : "⚪ 未生成（下の青色ボタンを押すとビル群が生成されます）";

            EditorGUILayout.LabelField(statusText, EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetButtons(CyberCityTestGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 都市規模プリセット（ワンクリック適用＆生成）", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🏙️ ① 標準街並み\n(42棟 / 400m)", GUILayout.Height(38)))
            {
                ApplyPresetAndRebuild(generator, CyberCityTestGenerator.CityScalePreset.StandardMedium_400m);
            }
            if (GUILayout.Button("🌆 ② 巨大都市・遠景\n(120棟 / 1200m)", GUILayout.Height(38)))
            {
                ApplyPresetAndRebuild(generator, CyberCityTestGenerator.CityScalePreset.LargeCity_1200m);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌌 ③ 超広大メガロポリス\n(240棟 / 2500m)", GUILayout.Height(38)))
            {
                ApplyPresetAndRebuild(generator, CyberCityTestGenerator.CityScalePreset.MegaCity_2500m);
            }
            if (GUILayout.Button("🏎️ ④ 沿道ビルキャニオン\n(70棟 / 800m / コース両脇)", GUILayout.Height(38)))
            {
                ApplyPresetAndRebuild(generator, CyberCityTestGenerator.CityScalePreset.RoadsideCanyon_800m);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons(CyberCityTestGenerator generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.85f, 1.0f);
            string genLabel = generator.HasGeneratedCity ? "🔄 ビル群を再生成（Rebuild）" : "🏙️ ビル群を生成（Generate）";
            if (GUILayout.Button(genLabel, GUILayout.Height(34)))
            {
                Undo.RecordObject(generator, "Generate Cyber City");
                generator.GenerateCity();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = generator.HasGeneratedCity ? new Color(1.0f, 0.5f, 0.5f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!generator.HasGeneratedCity);
            if (GUILayout.Button("🧹 ビル群を一括削除（Clear）", GUILayout.Height(34)))
            {
                Undo.RecordObject(generator, "Clear Cyber City");
                generator.ClearCity();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ApplyPresetAndRebuild(CyberCityTestGenerator generator, CyberCityTestGenerator.CityScalePreset preset)
        {
            Undo.RecordObject(generator, "Apply City Scale Preset");
            generator.ApplyPreset(preset);
            generator.GenerateCity();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }
    }
}

