#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation.Editor
{
    /// <summary>
    /// RacerAnimationController 用カスタムインスペクター。
    /// ・親 Model（TimelineRaceIntro 用 Animator）への誤アタッチを自動検知＆ワンクリック修復。
    /// ・03.unity 等での単体テスト用 born model 配置＆一括セットアップ。
    /// ・Play モード中のライブ操作リモコン＆ステートリアルタイム監視。
    /// </summary>
    [CustomEditor(typeof(RacerAnimationController))]
    public sealed class RacerAnimationControllerEditor : UnityEditor.Editor
    {
        private const string DefaultProfileAssetPath = "Assets/Deverop/AT_Murayama/PlayerAnimation/Generated/RacerAnimationProfile.asset";

        private string _statusMessage = string.Empty;
        private MessageType _statusMessageType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            RacerAnimationController ctrl = (RacerAnimationController)target;

            DrawRigSafetyBanner(ctrl);
            EditorGUILayout.Space(4);
            DrawQuickSetupSection(ctrl);
            EditorGUILayout.Space(6);
            DrawRuntimePreviewPanel(ctrl);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_statusMessage, _statusMessageType);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("コンポーネント設定", EditorStyles.boldLabel);
            DrawDefaultInspector();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        // ============================================================
        // 1. 親 Model（TimelineRaceIntro 用）との競合・階層ズレ診断バナー
        // ============================================================
        private void DrawRigSafetyBanner(RacerAnimationController ctrl)
        {
            Animator anim = ctrl.CharacterAnimator;
            Transform rigRoot = ctrl.FindBestCharacterModelRoot();

            if (anim != null)
            {
                if (!RacerAnimationController.IsAnimatorOnRigRoot(anim))
                {
                    EditorGUILayout.HelpBox(
                        $"⚠ 危険：現在の CharacterAnimator ({anim.gameObject.name}) は mainRig / jnt_org の直上ではありません！\n" +
                        "Player.prefab の親「Model」の Animator は水口さんの TimelineRaceIntro 専用であり、ここに設定すると競合＋ボーンパス1段ズレで動きません。\n" +
                        "下のボタンで子「born model」側の専用 Animator へ切り替えてください。",
                        MessageType.Error);

                    if (rigRoot != null && GUILayout.Button($"🔧 子 '{rigRoot.name}' 側に専用 Animator を設定・割当する", GUILayout.Height(26)))
                    {
                        AssignAnimatorOnRigRoot(ctrl, rigRoot);
                    }
                }
                else if (anim.applyRootMotion)
                {
                    EditorGUILayout.HelpBox(
                        "⚠ CharacterAnimator の Apply Root Motion が ON です。PlayerFlightController の移動と競合しないよう OFF 推奨です。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"✅ Animator 配置正常：'{anim.gameObject.name}'（mainRig 直上・TimelineRaceIntro と完全分離）",
                        MessageType.Info);
                }
            }
            else if (rigRoot != null)
            {
                EditorGUILayout.HelpBox(
                    $"CharacterAnimator が未割当です。配下にリグ '{rigRoot.name}' が見つかりました。",
                    MessageType.Warning);
                if (GUILayout.Button($"🔧 '{rigRoot.name}' に Animator を付与して割当", GUILayout.Height(24)))
                {
                    AssignAnimatorOnRigRoot(ctrl, rigRoot);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "配下に born model（mainRig）が見つかりません。03.unity 等で単体テストする場合は下のボタンで born model を配置できます。",
                    MessageType.Warning);
                if (GUILayout.Button("👤 単体テスト用に born model.fbx を子へ配置＆自動割当", GUILayout.Height(26)))
                {
                    SpawnTestBornModelChild(ctrl);
                }
            }
        }

        // ============================================================
        // 2. クイックセットアップ＆案A/案B 切替
        // ============================================================
        private void DrawQuickSetupSection(RacerAnimationController ctrl)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛠️ クイックセットアップ（Player / CPU / 単体テスト共通）", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.55f, 0.92f, 0.65f);
            if (GUILayout.Button("🚀 1クリック全自動セットアップ（Profile作成 → FBX最適化＆鏡像生成 → Controller適用）", GUILayout.Height(32)))
            {
                RunOneClickSetup(ctrl);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            if (ctrl.Profile != null)
            {
                EditorGUILayout.BeginHorizontal();
                bool isA = ctrl.Profile.TurnMode == FlightTurnAnimationMode.DiscreteStates;
                GUI.backgroundColor = isA ? new Color(0.65f, 0.85f, 1f) : Color.white;
                if (GUILayout.Button(isA ? "● 案A：ステート遷移で再構築" : "案A：ステート遷移へ切替", GUILayout.Height(24)))
                {
                    SwitchModeAndApply(ctrl, FlightTurnAnimationMode.DiscreteStates);
                }

                bool isB = ctrl.Profile.TurnMode == FlightTurnAnimationMode.BlendTree;
                GUI.backgroundColor = isB ? new Color(0.65f, 0.85f, 1f) : Color.white;
                if (GUILayout.Button(isB ? "● 案B：BlendTreeで再構築" : "案B：BlendTreeへ切替", GUILayout.Height(24)))
                {
                    SwitchModeAndApply(ctrl, FlightTurnAnimationMode.BlendTree);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("コース自動移動の反映ソース切替（実行中でも即時反映）", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                DrawMotionSourceButton(ctrl, FlightMotionSourceMode.InputOnly, "①入力のみ");
                DrawMotionSourceButton(ctrl, FlightMotionSourceMode.InputAndCenterReturn, "②+中央戻り");
                DrawMotionSourceButton(ctrl, FlightMotionSourceMode.InputAndCourseCurve, "③+カーブ");
                DrawMotionSourceButton(ctrl, FlightMotionSourceMode.FullCombined, "④全部合成");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔗 参照を自動再検索", GUILayout.Height(22)))
            {
                Undo.RecordObject(ctrl, "Resolve RacerAnimationController References");
                ctrl.ResolveReferencesIfNeeded(allowSceneFind: true);
                EditorUtility.SetDirty(ctrl);
                _statusMessage = "参照を再検索しました。";
                _statusMessageType = MessageType.Info;
            }

            if (GUILayout.Button("🧪 自動単体テスト（全9項目）", GUILayout.Height(22)))
            {
                RacerAnimationSelfTest.TestSummary res = RacerAnimationSelfTest.RunAllTests();
                _statusMessage = res.Report;
                _statusMessageType = res.Failed == 0 ? MessageType.Info : MessageType.Error;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ============================================================
        // 3. Play モード・ライブ動作確認リモコン
        // ============================================================
        private void DrawRuntimePreviewPanel(RacerAnimationController ctrl)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 ライブ動作確認リモコン（03.unity 単体検証＆実機デバッグ用）", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Play モードに入ると、現在の再生ステート監視と手動トリガー操作が有効になります。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            // 現在の Animator 状態表示
            Animator anim = ctrl.CharacterAnimator;
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                string stateLabel = ResolveKnownStateName(stateInfo);
                string transitionNote = anim.IsInTransition(0) ? " [遷移中]" : string.Empty;
                EditorGUILayout.LabelField(
                    $"再生中: {stateLabel}{transitionNote}  (NormTime: {stateInfo.normalizedTime:F2}) | " +
                    $"Racing={ctrl.HasRaceStarted} | Move=({ctrl.CurrentSmoothedMoveX:F2}, {ctrl.CurrentSmoothedMoveY:F2}) | " +
                    $"TurnDir={ctrl.CurrentTurnDir} | VertDir={ctrl.CurrentVerticalDir}",
                    EditorStyles.miniBoldLabel);
            }

            if (!ctrl.UseManualTestOverride)
            {
                FlightMotionSourceMode srcMode = ctrl.Profile != null
                    ? ctrl.Profile.MotionSourceMode
                    : FlightMotionSourceMode.InputOnly;
                Vector2 raw = ctrl.LastRawInputMove;
                Vector2 spd = ctrl.LastSmoothedCenterReturnSpeed;
                Vector2 cc = ctrl.LastCenterReturnContribution;
                Vector2 rate = ctrl.LastSmoothedCourseCurveRate;
                Vector2 kc = ctrl.LastCourseCurveContribution;
                Vector2 comb = ctrl.LastCombinedMove;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"📡 リアルタイム移動信号モニター [{GetMotionSourceLabel(srcMode)}]", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"  入力 Raw       : ({raw.x:F2}, {raw.y:F2})", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  中央戻り       : 速度({spd.x:F2}, {spd.y:F2}) m/s → 寄与({cc.x:F2}, {cc.y:F2})", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  コースカーブ   : 角速度({rate.x:F1}, {rate.y:F1}) deg/s → 寄与({kc.x:F2}, {kc.y:F2})", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  最終合成 Target: ({comb.x:F2}, {comb.y:F2})", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(3);
            bool manual = EditorGUILayout.ToggleLeft("手動テストオーバーライド ON（RaceManager / 実入力を無視して下のボタンで操作）", ctrl.UseManualTestOverride);
            if (manual != ctrl.UseManualTestOverride)
            {
                ctrl.UseManualTestOverride = manual;
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⏮ SetUp へ戻す", GUILayout.Height(24)))
            {
                ctrl.UseManualTestOverride = true;
                ctrl.ResetToSetUp();
            }

            GUI.backgroundColor = new Color(0.6f, 0.95f, 0.65f);
            if (GUILayout.Button("🚀 GO! (TakeOff → Fly)", GUILayout.Height(24)))
            {
                ctrl.UseManualTestOverride = true;
                ctrl.TriggerStartPlay();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!ctrl.UseManualTestOverride))
            {
                EditorGUILayout.Space(3);
                Vector2 mv = ctrl.ManualMoveInput;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ 左旋回 (-1)")) { mv.x = -1f; ctrl.ManualIsRacing = true; }
                if (GUILayout.Button("● 直線 (0)")) { mv.x = 0f; ctrl.ManualIsRacing = true; }
                if (GUILayout.Button("▶ 右旋回 (+1)")) { mv.x = 1f; ctrl.ManualIsRacing = true; }
                EditorGUILayout.EndHorizontal();

                mv.x = EditorGUILayout.Slider("MoveX (Steering 左右)", mv.x, -1f, 1f);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▲ 上昇 (+1)")) { mv.y = 1f; ctrl.ManualIsRacing = true; }
                if (GUILayout.Button("● 水平 (0)")) { mv.y = 0f; ctrl.ManualIsRacing = true; }
                if (GUILayout.Button("▼ 下降 (-1)")) { mv.y = -1f; ctrl.ManualIsRacing = true; }
                EditorGUILayout.EndHorizontal();

                mv.y = EditorGUILayout.Slider("MoveY (上下 拡張用)", mv.y, -1f, 1f);
                ctrl.ManualMoveInput = mv;

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = ctrl.ManualIsStunned ? new Color(1f, 0.55f, 0.55f) : Color.white;
                if (GUILayout.Button(ctrl.ManualIsStunned ? "⚡ スタン解除" : "⚡ スタン開始"))
                {
                    ctrl.ManualIsStunned = !ctrl.ManualIsStunned;
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🔫 弾発射 (Fire)"))
                {
                    ctrl.ManualIsRacing = true;
                    ctrl.TriggerFire();
                }

                GUI.backgroundColor = ctrl.ManualIsBoosting ? new Color(1f, 0.8f, 0.45f) : Color.white;
                if (GUILayout.Button(ctrl.ManualIsBoosting ? "🔥 ブースト OFF" : "🔥 ブースト ON"))
                {
                    ctrl.ManualIsBoosting = !ctrl.ManualIsBoosting;
                }

                GUI.backgroundColor = ctrl.ManualIsShielding ? new Color(0.55f, 0.85f, 1f) : Color.white;
                if (GUILayout.Button(ctrl.ManualIsShielding ? "🛡️ シールド OFF" : "🛡️ シールド ON"))
                {
                    ctrl.ManualIsShielding = !ctrl.ManualIsShielding;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // ============================================================
        // アクション実装
        // ============================================================
        private void AssignAnimatorOnRigRoot(RacerAnimationController ctrl, Transform rigRoot)
        {
            Animator rigAnim = rigRoot.GetComponent<Animator>();
            if (rigAnim == null)
            {
                rigAnim = Undo.AddComponent<Animator>(rigRoot.gameObject);
            }

            Undo.RecordObject(rigAnim, "Configure Character Animator");
            rigAnim.applyRootMotion = false;
            if (ctrl.Profile != null && ctrl.Profile.GeneratedController != null)
            {
                rigAnim.runtimeAnimatorController = ctrl.Profile.GeneratedController;
            }

            Undo.RecordObject(ctrl, "Assign Character Animator");
            ctrl.CharacterAnimator = rigAnim;
            EditorUtility.SetDirty(rigAnim);
            EditorUtility.SetDirty(ctrl);

            _statusMessage = $"✅ '{rigRoot.name}' の Animator を設定・割当しました（ApplyRootMotion=OFF）。";
            _statusMessageType = MessageType.Info;
        }

        private void SpawnTestBornModelChild(RacerAnimationController ctrl)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RacerAnimationClipUtility.DefaultReferenceModelPath);
            if (prefab == null)
            {
                _statusMessage = $"エラー: {RacerAnimationClipUtility.DefaultReferenceModelPath} が見つかりません。";
                _statusMessageType = MessageType.Error;
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ctrl.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Test born model");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            AssignAnimatorOnRigRoot(ctrl, instance.transform);
        }

        private void RunOneClickSetup(RacerAnimationController ctrl)
        {
            // 1. Profile を確保（無ければ自動ロード／新規作成）
            RacerAnimationProfile profile = ctrl.Profile;
            if (profile == null)
            {
                profile = AssetDatabase.LoadAssetAtPath<RacerAnimationProfile>(DefaultProfileAssetPath);
                if (profile == null)
                {
                    RacerAnimationClipUtility.EnsureAssetFolderExists("Assets/Deverop/AT_Murayama/PlayerAnimation/Generated");
                    profile = ScriptableObject.CreateInstance<RacerAnimationProfile>();
                    AssetDatabase.CreateAsset(profile, DefaultProfileAssetPath);
                    AssetDatabase.SaveAssets();
                }

                Undo.RecordObject(ctrl, "Assign RacerAnimationProfile");
                ctrl.Profile = profile;
            }

            // 2. FBX 最適化・Right 補正＆鏡像 Left 生成・AnimatorController 構築
            string log1 = RacerAnimationClipUtility.OptimizeAnimationFolderFbxAndAutoAssign(profile);
            string log2 = RacerAnimationClipUtility.GenerateFixedRightAndMirroredLeftCurves(profile);
            AnimatorController controller = RacerAnimatorBuilder.BuildOrUpdateController(profile, out string log3);

            // 3. born model が配下にあれば Animator を確実に割当（無ければ単体テスト用に配置はせず案内のみ）
            Transform rigRoot = ctrl.FindBestCharacterModelRoot();
            if (rigRoot != null)
            {
                AssignAnimatorOnRigRoot(ctrl, rigRoot);
            }
            else if (ctrl.CharacterAnimator != null && controller != null)
            {
                Undo.RecordObject(ctrl.CharacterAnimator, "Assign Generated Controller");
                ctrl.CharacterAnimator.runtimeAnimatorController = controller;
                ctrl.CharacterAnimator.applyRootMotion = false;
            }

            ctrl.ResolveReferencesIfNeeded(allowSceneFind: true);
            EditorUtility.SetDirty(ctrl);

            _statusMessage = $"{log1}\n{log2}\n\n{log3}";
            _statusMessageType = (log2.StartsWith("エラー") || log3.StartsWith("エラー"))
                ? MessageType.Error
                : MessageType.Info;
            Debug.Log("[PlayerAnimation] 1クリック全自動セットアップ完了:\n" + _statusMessage);
        }

        private void SwitchModeAndApply(RacerAnimationController ctrl, FlightTurnAnimationMode mode)
        {
            RacerAnimationProfile profile = ctrl.Profile;
            if (profile == null)
            {
                return;
            }

            Undo.RecordObject(profile, "Switch Turn Mode");
            profile.TurnMode = mode;
            EditorUtility.SetDirty(profile);

            AnimatorController controller = RacerAnimatorBuilder.BuildOrUpdateController(profile, out string summary);
            if (ctrl.CharacterAnimator != null && controller != null)
            {
                ctrl.CharacterAnimator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(ctrl.CharacterAnimator);
            }

            _statusMessage = summary;
            _statusMessageType = summary.StartsWith("エラー") ? MessageType.Error : MessageType.Info;
            Debug.Log("[PlayerAnimation] " + summary);
        }

        private void DrawMotionSourceButton(RacerAnimationController ctrl, FlightMotionSourceMode mode, string label)
        {
            RacerAnimationProfile profile = ctrl.Profile;
            bool isSelected = profile != null && profile.MotionSourceMode == mode;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.65f, 0.92f, 0.78f) : Color.white;
            string caption = isSelected ? "● " + label : label;
            if (GUILayout.Button(caption, GUILayout.Height(22)) && profile != null && profile.MotionSourceMode != mode)
            {
                Undo.RecordObject(profile, "Switch Motion Source Mode");
                profile.MotionSourceMode = mode;
                EditorUtility.SetDirty(profile);
                _statusMessage = $"参照ソースを『{GetMotionSourceLabel(mode)}』へ切り替えました（即反映）。";
                _statusMessageType = MessageType.Info;
            }
            GUI.backgroundColor = prevBg;
        }

        private static string GetMotionSourceLabel(FlightMotionSourceMode mode)
        {
            switch (mode)
            {
                case FlightMotionSourceMode.InputOnly: return "①入力のみ";
                case FlightMotionSourceMode.InputAndCenterReturn: return "②入力＋中央戻り";
                case FlightMotionSourceMode.InputAndCourseCurve: return "③入力＋カーブ";
                case FlightMotionSourceMode.FullCombined: return "④全部合成";
                default: return mode.ToString();
            }
        }

        private static string ResolveKnownStateName(AnimatorStateInfo info)
        {
            if (info.IsName(RacerAnimationProfile.StateSetUp)) return RacerAnimationProfile.StateSetUp;
            if (info.IsName(RacerAnimationProfile.StateTakeOff)) return RacerAnimationProfile.StateTakeOff;
            if (info.IsName(RacerAnimationProfile.StateFly)) return RacerAnimationProfile.StateFly;
            if (info.IsName(RacerAnimationProfile.StateFlightBlend)) return RacerAnimationProfile.StateFlightBlend;
            if (info.IsName(RacerAnimationProfile.StateLeftCurve)) return RacerAnimationProfile.StateLeftCurve;
            if (info.IsName(RacerAnimationProfile.StateRightCurve)) return RacerAnimationProfile.StateRightCurve;
            if (info.IsName(RacerAnimationProfile.StateMoveUp)) return RacerAnimationProfile.StateMoveUp;
            if (info.IsName(RacerAnimationProfile.StateMoveDown)) return RacerAnimationProfile.StateMoveDown;
            if (info.IsName(RacerAnimationProfile.StateStun)) return RacerAnimationProfile.StateStun;
            if (info.IsName(RacerAnimationProfile.StateFire)) return RacerAnimationProfile.StateFire;
            if (info.IsName(RacerAnimationProfile.StateBoost)) return RacerAnimationProfile.StateBoost;
            if (info.IsName(RacerAnimationProfile.StateShield)) return RacerAnimationProfile.StateShield;
            return $"Hash({info.shortNameHash})";
        }
    }
}
#endif
