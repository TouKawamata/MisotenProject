using System.IO;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.PlayerStatusUI.Editor
{
    [CustomEditor(typeof(PlayerPoseStatusUIController))]
    public class PlayerPoseStatusUIControllerEditor : UnityEditor.Editor
    {
        private const string PresetsFolderPath = "Assets/Deverop/AT_Murayama/PlayerStatusUI/Presets";

        public override void OnInspectorGUI()
        {
            PlayerPoseStatusUIController controller = (PlayerPoseStatusUIController)target;

            EditorGUILayout.Space(4);
            DrawHeaderAndStatusBox(controller);

            EditorGUILayout.Space(6);
            DrawMaterialAndAppearanceQuickBox(controller);

            EditorGUILayout.Space(6);
            DrawQuickTestButtonsBox(controller);

            EditorGUILayout.Space(6);
            DrawPoseAuthoringAndSaveBox(controller);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("詳細パラメーター調整", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        private void DrawHeaderAndStatusBox(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🕺 プレイヤー状態ポーズUI (3D Status Monitor)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "・PlayerManager（Stunned > Boosting > Shielding > Normal）と PlayerFlightController（Steering 左右移動）を参照し、画面前面の3Dキャラクターモデルを事前保存ポーズへ滑らかに遷移させます。\n" +
                "・通常マテリアルそのままの表示、サイバーホログラム発光、カスタムマテリアルの切り替えに対応。\n" +
                "・トラッキングや既存プレイヤースクリプトには一切手を加えず、外部からの読み取りのみで安全に動作します。",
                MessageType.Info);

            string statusMsg = controller.HasValidSetup
                ? $"✅ セットアップ完了 ｜ 現在の表示状態：【 {controller.CurrentState} 】"
                : "⚠️ ステージ未構築（下のボタンで即座にセットアップできます）";
            EditorGUILayout.LabelField(statusMsg, EditorStyles.miniBoldLabel);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.85f, 1.0f);
            if (GUILayout.Button("🔄 ステージ・UIを再構築 ＆ 6状態の初期サンプルポーズを自動セットアップ", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Rebuild Player Pose UI Stage");
                controller.RebuildStageAndUI();
                controller.GenerateDefaultSixStatePoses();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialAndAppearanceQuickBox(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 マテリアル方式 ＆ 背景・透明度クイック設定", EditorStyles.boldLabel);

            // 1. マテリアルモード選択
            EditorGUI.BeginChangeCheck();
            PlayerPoseStatusUIController.MaterialDisplayMode currentMode = controller.MaterialMode;

            EditorGUILayout.BeginHorizontal();
            Color prevBg = GUI.backgroundColor;

            // ① 通常マテリアル
            GUI.backgroundColor = (currentMode == PlayerPoseStatusUIController.MaterialDisplayMode.OriginalMaterial)
                ? new Color(0.35f, 0.95f, 0.55f)
                : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("① 通常マテリアル\n(Original Model)", GUILayout.Height(32)))
            {
                controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.OriginalMaterial;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            // ② ホログラム
            GUI.backgroundColor = (currentMode == PlayerPoseStatusUIController.MaterialDisplayMode.HologramShader)
                ? new Color(0.35f, 0.85f, 1.0f)
                : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("② ホログラム\n(Cyber Shader)", GUILayout.Height(32)))
            {
                controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.HologramShader;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            // ③ カスタム
            GUI.backgroundColor = (currentMode == PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial)
                ? new Color(1.0f, 0.8f, 0.35f)
                : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("③ カスタム\n(Custom Material)", GUILayout.Height(32)))
            {
                controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // モードに応じたワンポイント説明
            switch (controller.MaterialMode)
            {
                case PlayerPoseStatusUIController.MaterialDisplayMode.OriginalMaterial:
                    EditorGUILayout.HelpBox("モデル本来のマテリアル（テクスチャ・通常シェーダー）を全スロットにそのまま表示します。状態変化時にはほんのり状態カラーがティント発光します（下の詳細設定で強度変更可能）。", MessageType.None);
                    break;
                case PlayerPoseStatusUIController.MaterialDisplayMode.HologramShader:
                    EditorGUILayout.HelpBox($"サイバーホログラムシェーダー（現在：{controller.HologramStyle}）を全スロットに適用し、状態発光色と連動させます。", MessageType.None);
                    break;
                case PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial:
                    EditorGUILayout.HelpBox("指定されたカスタムマテリアルを全スロットに一括適用します。", MessageType.None);
                    break;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("【UI背景・透明度】", EditorStyles.miniBoldLabel);

            // 背景パネル色とアルファ
            EditorGUI.BeginChangeCheck();
            Color newBgCol = EditorGUILayout.ColorField("背景パネル色・アルファ", controller.UIBackdropColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change UI Backdrop Color");
                controller.UIBackdropColor = newBgCol;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            // 外枠ボーダーのアルファ
            EditorGUI.BeginChangeCheck();
            float newBorderAlpha = EditorGUILayout.Slider("外枠（ボーダー）透明度", controller.UIBorderAlpha, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change UI Border Alpha");
                controller.UIBorderAlpha = newBorderAlpha;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            // サブカメラ背景クリア色
            EditorGUI.BeginChangeCheck();
            Color newCamBg = EditorGUILayout.ColorField("カメラ背景クリア色 (通常は透明)", controller.CameraBackgroundColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Camera Background Color");
                controller.CameraBackgroundColor = newCamBg;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickTestButtonsBox(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 状態遷移・ワンクリック手動テスト（トラッキング無しで確認可能）", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = controller.UseManualTestMode
                ? new Color(0.35f, 0.92f, 0.55f)
                : new Color(0.75f, 0.75f, 0.75f);

            string modeLabel = controller.UseManualTestMode
                ? "✅ 手動テストモード：ON（下のボタンで自由にポーズ遷移を確認中）"
                : "🔗 現在：PlayerManager 自動連動モード（クリックで手動テストモードをON）";

            if (GUILayout.Button(modeLabel, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Toggle Manual Test Mode");
                controller.UseManualTestMode = !controller.UseManualTestMode;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // 1段目：通常・左移動・右移動
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.85f, 1.0f);
            if (GUILayout.Button("🟢 通常\n(Normal)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.Normal, 0f);
            }
            if (GUILayout.Button("⬅️ 左移動\n(MoveLeft)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.MoveLeft, -1f);
            }
            if (GUILayout.Button("➡️ 右移動\n(MoveRight)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.MoveRight, 1f);
            }
            EditorGUILayout.EndHorizontal();

            // 2段目：ブースト・シールド・スタン（最優先）
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1.0f, 0.65f, 0.3f);
            if (GUILayout.Button("🔥 ブースト\n(Boosting)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.Boosting, 0f);
                controller.TriggerEventFlash(new Color(4.5f, 2.2f, 0.5f, 1.0f), 1.0f, 0.12f);
            }

            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.65f);
            if (GUILayout.Button("🛡️ シールド\n(Shielding)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.Shielding, 0f);
                controller.TriggerEventFlash(new Color(0.5f, 4.8f, 2.5f, 1.0f), 0.9f, 0.10f);
            }

            GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
            if (GUILayout.Button("💫 スタン [最優先]\n(Stunned)", GUILayout.Height(36)))
            {
                SetTestState(controller, PlayerUIState.Stunned, 0f);
                controller.TriggerEventFlash(new Color(5.0f, 0.6f, 0.6f, 1.0f), 1.35f, 0.16f);
            }
            EditorGUILayout.EndHorizontal();

            // シールド防御瞬間（ShieldBlocked）演出テストボタン
            GUI.backgroundColor = new Color(0.55f, 1.0f, 0.85f);
            if (GUILayout.Button("⚡ シールド防御成功瞬間（ShieldBlocked フラッシュ演出）をテスト発火", GUILayout.Height(24)))
            {
                SetTestState(controller, PlayerUIState.Shielding, 0f);
                controller.TriggerEventFlash(new Color(3.5f, 6.0f, 4.5f, 1.0f), 1.5f, 0.22f);
            }

            GUI.backgroundColor = prevBg;

            // Steering スライダー
            EditorGUI.BeginChangeCheck();
            float newSteer = EditorGUILayout.Slider("手動 Steering (-1:左 〜 +1:右)", controller.ManualTestSteering, -1f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Manual Steering");
                controller.UseManualTestMode = true;
                controller.ManualTestSteering = newSteer;
                if (Mathf.Abs(newSteer) < 0.05f &&
                    (controller.ManualTestState == PlayerUIState.MoveLeft || controller.ManualTestState == PlayerUIState.MoveRight))
                {
                    controller.ManualTestState = PlayerUIState.Normal;
                }
                controller.StartEditorTransitionPreview(0.5f);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPoseAuthoringAndSaveBox(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🦴 実行前のポーズ作成・編集・保存（ボーン角度プリセット）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "【オリジナルポーズの作り方】\n" +
                "① 下の『編集・保存する状態』を選び、『👁️ この状態のポーズを呼び出す』を押します。\n" +
                "② Sceneビューでモデルの腕や背骨のボーンを回転ツール（Eキー）で好きな角度に曲げます。\n" +
                "③ 『💾 現在のボーン角度をこの状態に保存』を押すと、そのポーズが記憶されます！",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            PlayerUIState selected = (PlayerUIState)EditorGUILayout.EnumPopup("編集・保存する対象の状態", controller.SelectedEditState);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Select Edit State");
                controller.SelectedEditState = selected;
                EditorUtility.SetDirty(controller);
            }

            Color prevBg = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.55f, 0.85f, 1.0f);
            if (GUILayout.Button($"👁️ [{controller.SelectedEditState}] のポーズを呼び出す", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Preview State Pose");
                controller.UseManualTestMode = true;
                controller.ManualTestState = controller.SelectedEditState;
                controller.PreviewPoseImmediate(controller.SelectedEditState);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("🔄 基本Tポーズにリセット", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Reset Model to Bind Pose");
                controller.ResetModelToBindPose();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // シーンビューでモデルのボーンをすぐ触れるように選択するショートカットボタン
            if (GUILayout.Button("🔍 ポーズ調整用にヒエラルキー上のモデル（背骨ボーン）を選択＆フォーカス", GUILayout.Height(24)))
            {
                if (controller.ModelInstance == null)
                {
                    controller.RebuildStageAndUI();
                }
                if (controller.ModelInstance != null)
                {
                    Transform spine = FindDeepChild(controller.ModelInstance.transform, "spine_C0_1_jnt");
                    Transform targetSelect = spine != null ? spine : controller.ModelInstance.transform;
                    Selection.activeTransform = targetSelect;
                    EditorGUIUtility.PingObject(targetSelect);
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.FrameSelected();
                    }
                }
            }

            EditorGUILayout.Space(3);

            // 現在のボーン角度を選択状態に保存するメインボタン
            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.55f);
            if (GUILayout.Button($"💾 現在のモデルのボーン角度を【 {controller.SelectedEditState} 】のポーズとして保存", GUILayout.Height(32)))
            {
                Undo.RecordObject(controller, "Capture Pose To State");
                controller.CaptureCurrentModelPoseToState(controller.SelectedEditState);
                controller.ManualTestState = controller.SelectedEditState;
                EditorUtility.SetDirty(controller);
                if (controller.PosePresetAsset != null)
                {
                    EditorUtility.SetDirty(controller.PosePresetAsset);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log($"[PlayerPoseStatusUI] 現在のボーン姿勢を [{controller.SelectedEditState}] として保存しました。");
            }

            EditorGUILayout.Space(3);

            // ScriptableObject アセットとして書き出し保存
            GUI.backgroundColor = new Color(0.95f, 0.82f, 0.4f);
            if (GUILayout.Button("📁 全状態のポーズデータを ScriptableObject アセット (.asset) に書き出し保存", GUILayout.Height(26)))
            {
                SavePosesToAsset(controller);
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void SetTestState(PlayerPoseStatusUIController controller, PlayerUIState state, float steering)
        {
            Undo.RecordObject(controller, "Set Player Pose UI Test State");
            controller.UseManualTestMode = true;
            controller.ManualTestState = state;
            controller.ManualTestSteering = steering;
            controller.SelectedEditState = state;
            controller.StartEditorTransitionPreview(0.75f);
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }

        private void SavePosesToAsset(PlayerPoseStatusUIController controller)
        {
            if (!Directory.Exists(PresetsFolderPath))
            {
                Directory.CreateDirectory(PresetsFolderPath);
                AssetDatabase.Refresh();
            }

            PlayerPosePresetData asset = controller.PosePresetAsset;
            if (asset == null)
            {
                string defaultPath = $"{PresetsFolderPath}/PlayerPosePresetAsset.asset";
                asset = AssetDatabase.LoadAssetAtPath<PlayerPosePresetData>(defaultPath);
                if (asset == null)
                {
                    asset = CreateInstance<PlayerPosePresetData>();
                    AssetDatabase.CreateAsset(asset, defaultPath);
                }
                controller.PosePresetAsset = asset;
            }

            controller.SyncInlineAndAssetPoses();
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[PlayerPoseStatusUI] ポーズプリセットアセットを保存しました: {AssetDatabase.GetAssetPath(asset)}");
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
