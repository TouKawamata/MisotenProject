using UnityEditor;
using UnityEngine;

namespace Minakuchi.Sky.Editor
{
    [CustomEditor(typeof(SkyTransitionController)), CanEditMultipleObjects]
    public sealed class SkyTransitionControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("空と照明", EditorStyles.boldLabel);
            Draw("enableSunsetTint", "夕焼けの色づけを有効にする", "16時から夕焼けが強まり、18時以降は地平線へ沈んで消えます。");
            Draw("sunsetColor", "夕焼けの色", "夕方の空と雲に重ねる色。");
            Draw("sunsetIntensity", "夕焼けの強さ", "大きいほど夕焼けの色が強くなります。0で色づけなし。");
            Draw("sunsetSpread", "夕焼けの広がり", "大きいほど地平線から上空までオレンジ色が広がります。");
            Draw("sunsetEndHour", "夕焼けが消える時刻", "18時からこの時刻まで夕焼けの範囲が地平線へ沈みます。色の設定は変わりません。");
            Draw("nightStartHour", "夜色が上空から下り始める時刻", "初期値17時。夕焼けと並行して上空から夜色が下ります。開始を早めても、夜色が地平線まで届く時刻は変わりません。");
            Draw("nightCompleteHour", "夜色が地平線まで届く時刻", "この時刻まで青さが地平線付近に残り、最後に空全体が夜になります。");
            Draw("skyTemplate", "空のマテリアル", "空の描画に使う元のマテリアル。再生中は複製して使用します。");
            Draw("sun", "太陽のライト", "時刻に合わせて方向・色・明るさを変えるライト。");
            Draw("moon", "月のライト", "夜の明るさを制御するライト。");
            Draw("atmosphereEnabled", "大気圏表示を有効にする", "ONで大気圏付近の空、OFFで通常の空。再生中は標準の切り替え時間で移行します。時刻とは独立しています。");
            Draw("transitionSeconds", "標準の切り替え時間（秒）", "メインの大気圏ON／OFFを切り替える秒数。通過ポイントでは各ポイントの秒数を使います。");
            Draw("cloudCoverage", "雲の量", "0で雲なし、1で雲が多い状態。");
            using (new EditorGUI.DisabledScope(true))
                Draw("night", "夜の度合い（自動）", "時刻と太陽の高さから自動計算されます。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("時刻と天体", EditorStyles.boldLabel);
            Draw("timeOfDay", "時刻（0〜24時）", "12が正午、0が真夜中。太陽・月・照明が連動します。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("雲と大気圏", EditorStyles.boldLabel);
            Draw("upperAtmosphere", "大気圏への移行度", "0で地上の空、1で大気圏付近の空。");
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                Draw("cloudHeight", Application.isPlaying ? "現在の雲の高さ" : "開始時の雲の高さ", "再生中の上下移動はTriggerの「移動先の雲の高さ」で指定します。");
            Draw("cloudScale", "雲の模様の大きさ", "大きくすると雲の模様が広がります。");
            Draw("enableCloudFlow", "雲を流す", "横方向へ雲を流します。OFFで現在位置に停止し、ONでその位置から再開します。");
            Draw("wind", "雲の流れる方向と速さ", "横方向2軸の移動速度。両方0にすると流れません。");
            Draw("cloudFlowSpeed", "雲の流れる速さの倍率", "初期値は4倍。動きを速くしたい場合は大きくしてください。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト用の操作", EditorStyles.boldLabel);
            Draw("showDemoControls", "テスト操作画面を表示", "空を切り替えるボタンとスライダーを表示します。");
            EditorGUILayout.HelpBox("既存のゲームカメラを使用します。この空の制御はカメラを生成・移動・回転させません。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string name, string label, string tooltip)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip), true);
        }
    }
}
