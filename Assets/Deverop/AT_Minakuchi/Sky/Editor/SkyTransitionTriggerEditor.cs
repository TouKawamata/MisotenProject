using UnityEditor;
using UnityEngine;

namespace Minakuchi.Sky.Editor
{
    [CustomEditor(typeof(SkyTransitionTrigger))]
    [CanEditMultipleObjects]
    public sealed class SkyTransitionTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("sky", "制御する空");
            Draw("atmosphereEnabled", "大気圏表示を有効にする");
            Draw("destinationCloudCoverage", "通過後の雲の量");
            Draw("transitionSeconds", "切り替え時間（秒）");
            Draw("overrideTimeOfDay", "到達時刻を指定");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("overrideTimeOfDay").boolValue))
                Draw("destinationHour", "到達時刻（時）");
            EditorGUILayout.Space();
            Draw("overrideCloudHeight", "このポイントで雲を上下させる");
            var cloudHeightOverride = serializedObject.FindProperty("overrideCloudHeight");
            using (new EditorGUI.DisabledScope(!cloudHeightOverride.boolValue && !cloudHeightOverride.hasMultipleDifferentValues))
                Draw("destinationCloudHeight", "移動先の雲の高さ");
            EditorGUILayout.HelpBox("雲は現在の高さから指定の高さへ、上の「切り替え時間（秒）」をかけて移動します。", MessageType.Info);
            EditorGUILayout.Space();
            Draw("targetActor", "通過対象オブジェクト");
            Draw("requiredTag", "対象タグ（対象未指定時）");
            Draw("triggerOnce", "一度だけ発動");
            EditorGUILayout.HelpBox("各ポイントで切り替え時間を個別設定できます。太陽・月・空・照明が到達時刻に合わせて変化します。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string property, string label)
        {
            var value = serializedObject.FindProperty(property);
            EditorGUILayout.PropertyField(value, new GUIContent(label, value.tooltip), true);
        }
    }
}
