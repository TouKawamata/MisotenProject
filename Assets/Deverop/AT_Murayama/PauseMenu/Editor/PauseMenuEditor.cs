using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.PauseMenu.Editor
{
    [CustomEditor(typeof(PauseMenu))]
    public class PauseMenuEditor : UnityEditor.Editor
    {
        private const string DefaultJapaneseFontPath = "Assets/TeamResources/Art/Fonts/NotoSerifJP-Bold SDF.asset";
        private const string PrefabDirectory = "Assets/Deverop/AT_Murayama/PauseMenu/Prefabs";
        private const string PrefabOutputPath = "Assets/Deverop/AT_Murayama/PauseMenu/Prefabs/PauseMenu.prefab";

        public override void OnInspectorGUI()
        {
            PauseMenu controller = (PauseMenu)target;

            DrawHeaderAndStatusBox(controller);
            EditorGUILayout.Space(6);
            DrawSetupAndPrefabBox(controller);
            EditorGUILayout.Space(6);
            DrawPreviewAndTestControlsBox(controller);
            EditorGUILayout.Space(8);

            DrawDefaultInspector();
        }

        private void DrawHeaderAndStatusBox(PauseMenu controller)
        {
            EditorGUILayout.BeginVertical("box");
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            EditorGUILayout.LabelField("⏸️ ポーズメニュー コントローラー（PauseMenu）", titleStyle);

            EditorGUILayout.HelpBox(
                "・Escキーで一時停止 / 再開（ゴール後・再開カウントダウン中は無効）\n" +
                "・外部（トラッキング切断等）から Open(EPauseReason.TrackingLost) でいつでも開け、Esc復帰不可＆キャリブレーション対応\n" +
                "・他シーン競合防止のため、GameScene 等には下部の「プレハブ保存」で書き出したプレハブを後から1個配置するだけで動作します。",
                MessageType.Info);

            if (Application.isPlaying)
            {
                string stateText = controller.IsCalibrating
                    ? "キャリブレーション中 (Enter待ち)"
                    : (controller.IsOpen ? $"ポーズ中 ({controller.CurrentReason})" : "非表示 (ゲーム進行中)");
                EditorGUILayout.LabelField($"実行中ステータス: {stateText}  |  Time.timeScale = {Time.timeScale:F2}");
            }
            else
            {
                EditorGUILayout.LabelField("状態: EditMode（プレビュー操作や UI 再構築・プレハブ保存が可能）");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupAndPrefabBox(PauseMenu controller)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🛠️ UI構築 ＆ 自己完結プレハブ保存", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.88f, 1.0f);
            if (GUILayout.Button("🛠️ ポーズUI階層を自動構築 / 再構築（NotoSerifJP日本語フォント適用）", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Build PauseMenu UI Hierarchy");
                TMP_FontAsset jpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultJapaneseFontPath);
                controller.BuildOrRebuildUiHierarchy(jpFont);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(0.45f, 0.95f, 0.65f);
            if (GUILayout.Button($"💾 自己完結プレハブを書き出し保存 ({PrefabOutputPath})", GUILayout.Height(28)))
            {
                SaveAsSelfContainedPrefab(controller);
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewAndTestControlsBox(PauseMenu controller)
        {
            EditorGUILayout.BeginVertical("box");

            Color prevBg = GUI.backgroundColor;
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("🎮 PlayMode 動作テスト操作", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.45f, 0.88f, 1.0f);
                if (GUILayout.Button("Open (UserRequest)", GUILayout.Height(28)))
                {
                    controller.Open(EPauseReason.UserRequest);
                }
                GUI.backgroundColor = new Color(1.0f, 0.72f, 0.35f);
                if (GUILayout.Button("Open (TrackingLost)", GUILayout.Height(28)))
                {
                    controller.Open(EPauseReason.TrackingLost);
                }
                GUI.backgroundColor = new Color(0.55f, 0.95f, 0.65f);
                if (GUILayout.Button("Close()", GUILayout.Height(28)))
                {
                    controller.Close();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.65f, 0.85f, 1.0f);
                if (GUILayout.Button("BeginCalibration()", GUILayout.Height(26)))
                {
                    controller.BeginCalibration();
                }
                if (GUILayout.Button("TryConfirmCalibration() 再開", GUILayout.Height(26)))
                {
                    controller.TryConfirmCalibration();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = prevBg;
                if (GUILayout.Button("↑ 上選択", GUILayout.Height(24)))
                {
                    controller.MoveSelection(-1);
                }
                if (GUILayout.Button("↓ 下選択", GUILayout.Height(24)))
                {
                    controller.MoveSelection(1);
                }
                if (GUILayout.Button("⏎ 選択中を実行", GUILayout.Height(24)))
                {
                    controller.ExecuteSelectedButton();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                GUI.backgroundColor = new Color(1.0f, 0.58f, 0.58f);
                if (GUILayout.Button("💥 HitStop(0.05)発火中に即ポーズして検証", GUILayout.Height(26)))
                {
                    HitStopEffect hitStop = Object.FindFirstObjectByType<HitStopEffect>();
                    if (hitStop != null)
                    {
                        hitStop.Play(1f);
                    }
                    else
                    {
                        Time.timeScale = 0.05f;
                    }

                    float scaleBeforePause = Time.timeScale;
                    controller.Open(EPauseReason.UserRequest);
                    Debug.Log($"[PauseMenuEditor] HitStopテスト: Open前 Time.timeScale={scaleBeforePause:F2} → Open後 Time.timeScale={Time.timeScale:F2}（画面の「戻る」または Esc で Time.timeScale = 1.00 に戻ればOK）");
                }
            }
            else
            {
                EditorGUILayout.LabelField("👁️ EditMode 見た目プレビュー（Gameビュー/Sceneビュー確認用）", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("非表示 (Hidden)", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Preview PauseMenu Hidden");
                    controller.PreviewStateInEditor(PauseMenu.EditorPreviewMode.Hidden);
                    EditorUtility.SetDirty(controller);
                }
                GUI.backgroundColor = new Color(0.45f, 0.88f, 1.0f);
                if (GUILayout.Button("通常ポーズ (Esc可)", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Preview UserPause");
                    controller.PreviewStateInEditor(PauseMenu.EditorPreviewMode.UserPause);
                    EditorUtility.SetDirty(controller);
                }
                GUI.backgroundColor = new Color(1.0f, 0.72f, 0.35f);
                if (GUILayout.Button("切断ポーズ (Esc不可)", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Preview TrackingLostPause");
                    controller.PreviewStateInEditor(PauseMenu.EditorPreviewMode.TrackingLostPause);
                    EditorUtility.SetDirty(controller);
                }
                GUI.backgroundColor = new Color(0.55f, 0.95f, 0.75f);
                if (GUILayout.Button("キャリブレーション中", GUILayout.Height(26)))
                {
                    Undo.RecordObject(controller, "Preview Calibrating");
                    controller.PreviewStateInEditor(PauseMenu.EditorPreviewMode.Calibrating);
                    EditorUtility.SetDirty(controller);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            GUI.backgroundColor = new Color(0.85f, 0.92f, 1.0f);
            if (GUILayout.Button("🧪 ポーズメニュー 自動テスト実行 (全8項目 / PauseMenuTest)", GUILayout.Height(26)))
            {
                PauseMenuTest.RunAllTestsAndSaveLog();
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private static void SaveAsSelfContainedPrefab(PauseMenu controller)
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("プレハブ保存不可", "PlayMode 終了後に実行してください。", "OK");
                return;
            }

            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
                AssetDatabase.Refresh();
            }

            Undo.RecordObject(controller, "Prepare and Save PauseMenu Prefab");
            controller.EnsureUiBuilt();
            // シーン配置直後に画面を塞がないよう、初期状態（Hidden）に整えてから保存
            controller.PreviewStateInEditor(PauseMenu.EditorPreviewMode.Hidden);
            EditorUtility.SetDirty(controller);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(controller.gameObject, PrefabOutputPath, InteractionMode.UserAction);
            if (savedPrefab != null)
            {
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(savedPrefab);
                Debug.Log($"[PauseMenuEditor] プレハブを保存しました: {PrefabOutputPath}");
            }
            else
            {
                Debug.LogError($"[PauseMenuEditor] プレハブ保存に失敗しました: {PrefabOutputPath}");
            }
        }
    }
}
