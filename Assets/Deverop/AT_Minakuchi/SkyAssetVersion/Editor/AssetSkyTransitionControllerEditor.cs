using UnityEditor;
using UnityEngine;

namespace Minakuchi.AssetSky.Editor
{
    [CustomEditor(typeof(AssetSkyTransitionController)), CanEditMultipleObjects]
    public sealed class AssetSkyTransitionControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("アセットの背景画像", EditorStyles.boldLabel);
            Draw("useAssetBackground", "アセットの背景を使用", "OFFなら元の手続き型の空に戻します。");
            Draw("cloudySky", "曇りの背景", "曇りの状態で使用するキューブマップ。");
            Draw("partlyCloudySky", "薄曇りの背景", "雲が少ない状態で使用するキューブマップ。");
            Draw("nightSky", "夜空の背景", "時刻に合わせて夜空へ滑らかに切り替えます。");
            Draw("atmosphereSky", "大気圏付近の背景", "大気圏への移行度に合わせて切り替えます。");
            Draw("backgroundExposure", "背景の明るさ", "画像全体の明るさの倍率。");
            Draw("backgroundRotation", "背景の向き（度）", "背景画像全体を水平に回転します。");
            Draw("dynamicCloudOpacity", "動く雲の重ね具合", "流れと上下移動ができる追加の雲の濃さ。0なら背景画像だけになります。");
            Draw("additionalCelestials", "動く太陽と月を重ねる", "画像に太陽や月が描かれている場合、二重表示を避けたいときはOFFにしてください。");
            EditorGUILayout.HelpBox("画像に描かれた雲・太陽・月は背景の一部です。個別に動くのは追加で重ねる雲・太陽・月です。", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("空と照明", EditorStyles.boldLabel);
            Draw("enableSunsetTint", "夕焼けの色づけを有効にする", "16〜20時に夕焼けを表示。18時付近で最も強くなります。");
            Draw("sunsetColor", "夕焼けの色", "オレンジや赤など、夕方の空と雲に重ねる色。");
            Draw("sunsetIntensity", "夕焼けの強さ", "大きいほど夕焼けの色を強く表示。0で色づけなし。");
            Draw("sunsetSpread", "夕焼けの広がり", "大きいほど地平線から上空まで広くオレンジ色になります。");
            Draw("skyTemplate", "空のマテリアル", "空の描画に使う元のマテリアル。再生中は複製して使用します。");
            Draw("sun", "太陽のライト", "時刻に合わせて方向・色・明るさを変えるライト。");
            Draw("moon", "月のライト", "夜の明るさを制御するライト。");
            Draw("initialState", "開始時の空", "再生開始時の空の状態。");
            Draw("transitionSeconds", "標準の切り替え時間（秒）", "テストボタンから切り替えるときの秒数。通過ポイントでは各ポイントの秒数を使います。");
            Draw("cloudCoverage", "雲の量", "0で雲なし、1で雲が多い状態。");
            using (new EditorGUI.DisabledScope(true))
                Draw("night", "夜の度合い（自動）", "時刻と太陽の高さから自動計算されます。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("時刻と天体", EditorStyles.boldLabel);
            Draw("timeOfDay", "時刻（0〜24時）", "12が正午、0が真夜中。太陽・月・照明が連動します。");
            Draw("advanceTime", "時間を自動で進める", "空の切り替えが終わった後も時刻を進めます。");
            Draw("dayLengthSeconds", "1日の長さ（秒）", "ゲーム内の24時間が経過するまでの実時間。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("雲と大気圏", EditorStyles.boldLabel);
            Draw("upperAtmosphere", "大気圏への移行度", "0で地上の空、1で大気圏付近の空。");
            Draw("cloudHeight", "雲の高さ", "雲の層を描く高さ。通過ポイントの「このポイントで雲を上下させる」で移動先を指定できます。負の値も使用できます。");
            Draw("enableCloudHaze", "雲の近くの霞を表示", "雲の層の近くで画面全体を白く霞ませる演出。OFFにすると霞だけを消します。再生中も変更できます。");
            Draw("cloudScale", "雲の模様の大きさ", "大きくすると雲の模様が広がります。");
            Draw("changeCloudPosition", "高度による雲の上下変化も使う", "空の見た目の高度に合わせた相対移動。各ポイントで雲の高さだけを指定したい場合はOFFにしてください。ポイントの雲の上下移動と横方向の流れはOFFでも使えます。");
            Draw("enableCloudFlow", "雲を流す", "横方向へ雲を流します。OFFで現在位置に停止し、ONでその位置から再開します。");
            Draw("wind", "雲の流れる方向と速さ", "横方向2軸の移動速度。両方0にすると流れません。");
            Draw("cloudFlowSpeed", "雲の流れる速さの倍率", "初期値は4倍。動きを速くしたい場合は大きくしてください。");
            Draw("followCameraAltitude", "実際のカメラ高度に連動", "カメラの高さを読み取って空を変えます。カメラ自体は動かしません。");
            Draw("observerCamera", "空を観測するカメラ", "省略時はメインカメラを使用。位置の読み取りに使います。");
            Draw("atmosphereStartHeight", "大気圏へ変わり始める高さ", "実際のカメラ高度に連動させた場合の開始高度。");
            Draw("atmosphereEndHeight", "大気圏への変化が完了する高さ", "実高度連動時の完了高度と、見た目の高度の目標値。");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト用の操作", EditorStyles.boldLabel);
            Draw("showDemoControls", "テスト操作画面を表示", "空を切り替えるボタンとスライダーを表示します。");
            Draw("enableDemoCameraControl", "テストカメラ制御を有効にする", "テスト操作画面が有効なときだけ使用。OFFならテストカメラを生成せず、ゲームカメラの位置・回転も変更しません。");
            EditorGUILayout.HelpBox("カメラ制御をOFFにしても、空の切り替えとテスト操作画面は使えます。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string name, string label, string tooltip)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip), true);
        }
    }
}
