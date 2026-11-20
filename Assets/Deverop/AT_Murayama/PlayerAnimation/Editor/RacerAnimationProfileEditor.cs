#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation.Editor
{
    /// <summary>
    /// RacerAnimationProfile 用カスタムインスペクター。
    /// [MenuItem] を一切使わず、インスペクターUI上のボタンだけで
    /// 「FBX最適化 → Right補正＆鏡像Left生成 → AnimatorController構築（案A/案B切替）→ 診断＆テスト」まで完結させます。
    /// </summary>
    [CustomEditor(typeof(RacerAnimationProfile))]
    public sealed class RacerAnimationProfileEditor : UnityEditor.Editor
    {
        private bool _showDiagnostics = true;
        private string _lastOperationLog = string.Empty;
        private MessageType _lastOperationLogType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            RacerAnimationProfile profile = (RacerAnimationProfile)target;

            DrawQuickPipelineSection(profile);
            EditorGUILayout.Space(6);
            DrawDiagnosticsSection(profile);
            EditorGUILayout.Space(6);

            if (!string.IsNullOrEmpty(_lastOperationLog))
            {
                EditorGUILayout.HelpBox(_lastOperationLog, _lastOperationLogType);
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.LabelField("詳細設定（クリップ・遷移チューニング・拡張スロット）", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        private void DrawQuickPipelineSection(RacerAnimationProfile profile)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛠️ アニメーション自動構築パイプライン", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.55f, 0.92f, 0.65f);
            if (GUILayout.Button("🚀 一括自動セットアップ（FBX最適化 → Right補正＆鏡像Left生成 → Controller構築）", GUILayout.Height(34)))
            {
                RunFullPipeline(profile);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("① Animation FBX 最適化＆自動読込", GUILayout.Height(24)))
            {
                Undo.RecordObject(profile, "Optimize Animation FBX & Auto Assign");
                _lastOperationLog = RacerAnimationClipUtility.OptimizeAnimationFolderFbxAndAutoAssign(profile);
                _lastOperationLogType = MessageType.Info;
                Debug.Log("[PlayerAnimation] " + _lastOperationLog);
            }

            if (GUILayout.Button("② Right補正 ＋ 鏡像Left自動生成", GUILayout.Height(24)))
            {
                Undo.RecordObject(profile, "Generate Fixed Right & Mirrored Left Curve");
                _lastOperationLog = RacerAnimationClipUtility.GenerateFixedRightAndMirroredLeftCurves(profile);
                _lastOperationLogType = _lastOperationLog.StartsWith("エラー") ? MessageType.Error : MessageType.Info;
                Debug.Log("[PlayerAnimation] " + _lastOperationLog);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("③ 旋回方式を切り替えて AnimatorController を再構築（GUID維持・参照切れゼロ）", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            bool isModeA = profile.TurnMode == FlightTurnAnimationMode.DiscreteStates;
            GUI.backgroundColor = isModeA ? new Color(0.65f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button(isModeA ? "● 案A：ステート遷移で構築" : "案A：ステート遷移へ切替＆構築", GUILayout.Height(26)))
            {
                RebuildWithMode(profile, FlightTurnAnimationMode.DiscreteStates);
            }

            bool isModeB = profile.TurnMode == FlightTurnAnimationMode.BlendTree;
            GUI.backgroundColor = isModeB ? new Color(0.65f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button(isModeB ? "● 案B：BlendTreeで構築" : "案B：BlendTreeへ切替＆構築", GUILayout.Height(26)))
            {
                RebuildWithMode(profile, FlightTurnAnimationMode.BlendTree);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🧪 自動単体テスト実行（全9項目・鏡像精度実測）", GUILayout.Height(24)))
            {
                RacerAnimationSelfTest.TestSummary res = RacerAnimationSelfTest.RunAllTests();
                _lastOperationLog = res.Report;
                _lastOperationLogType = res.Failed == 0 ? MessageType.Info : MessageType.Error;
            }

            using (new EditorGUI.DisabledScope(profile.GeneratedController == null))
            {
                if (GUILayout.Button("🎯 生成Controllerを選択", GUILayout.Width(160), GUILayout.Height(24)))
                {
                    EditorGUIUtility.PingObject(profile.GeneratedController);
                    Selection.activeObject = profile.GeneratedController;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDiagnosticsSection(RacerAnimationProfile profile)
        {
            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "🔍 クリップ診断（長さ・Loop・mainRig階層チェック）", true);
            if (!_showDiagnostics)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (profile.ReferenceModelPrefab == null)
            {
                EditorGUILayout.HelpBox("ReferenceModelPrefab（born model.fbx）が未設定です。上の『①』または『🚀 一括自動セットアップ』で自動設定されます。", MessageType.Warning);
            }

            List<RacerAnimationClipUtility.ClipDiagnostic> diags = RacerAnimationClipUtility.DiagnoseProfileClips(profile);
            bool anyMainRigWarning = false;

            for (int i = 0; i < diags.Count; i++)
            {
                RacerAnimationClipUtility.ClipDiagnostic d = diags[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(d.SlotName, GUILayout.Width(165));

                if (d.Clip == null)
                {
                    GUI.color = new Color(0.75f, 0.75f, 0.75f);
                    EditorGUILayout.LabelField("未設定（待機枠 / スキップ）");
                    GUI.color = Color.white;
                }
                else
                {
                    string status = $"{d.Clip.name} | {d.LengthSeconds:F3}s | Loop={(d.IsLooping ? "ON" : "OFF")}";
                    if (d.NeedsMainRigPrefix)
                    {
                        anyMainRigWarning = true;
                        GUI.color = new Color(1f, 0.55f, 0.55f);
                        status += "  ⚠ mainRig階層欠落!";
                    }
                    else if (d.IsVeryShortSingleFrame)
                    {
                        GUI.color = new Color(1f, 0.88f, 0.45f);
                        status += "  ⚠ 約1フレーム長";
                    }
                    else
                    {
                        GUI.color = new Color(0.75f, 1f, 0.78f);
                    }

                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (anyMainRigWarning)
            {
                EditorGUILayout.HelpBox(
                    "⚠ mainRig 階層欠落があるクリップが割り当てられています。そのまま再生するとボーンパス不一致で動きません。\n" +
                    "上の『② Right補正 ＋ 鏡像Left自動生成』を押すと、mainRig/ 補完済みの .anim に自動差し替えされます。",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void RunFullPipeline(RacerAnimationProfile profile)
        {
            Undo.RecordObject(profile, "Run Full PlayerAnimation Pipeline");
            string step1 = RacerAnimationClipUtility.OptimizeAnimationFolderFbxAndAutoAssign(profile);
            string step2 = RacerAnimationClipUtility.GenerateFixedRightAndMirroredLeftCurves(profile);
            RacerAnimatorBuilder.BuildOrUpdateController(profile, out string step3);

            _lastOperationLog = $"{step1}\n{step2}\n\n{step3}";
            _lastOperationLogType = (step2.StartsWith("エラー") || step3.StartsWith("エラー"))
                ? MessageType.Error
                : MessageType.Info;
            Debug.Log("[PlayerAnimation] 一括自動セットアップ完了:\n" + _lastOperationLog);
        }

        private void RebuildWithMode(RacerAnimationProfile profile, FlightTurnAnimationMode mode)
        {
            Undo.RecordObject(profile, "Switch Mode & Rebuild AnimatorController");
            profile.TurnMode = mode;
            EditorUtility.SetDirty(profile);
            RacerAnimatorBuilder.BuildOrUpdateController(profile, out string summary);
            _lastOperationLog = summary;
            _lastOperationLogType = summary.StartsWith("エラー") ? MessageType.Error : MessageType.Info;
            Debug.Log("[PlayerAnimation] " + summary);
        }
    }
}
#endif
