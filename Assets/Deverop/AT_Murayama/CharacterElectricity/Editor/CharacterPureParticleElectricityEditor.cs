#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.CharacterElectricity.Editor
{
    [CustomEditor(typeof(CharacterPureParticleElectricity))]
    public class CharacterPureParticleElectricityEditor : UnityEditor.Editor
    {
        private bool _showGuide = false;
        private bool _showAdvanced = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            CharacterPureParticleElectricity controller = (CharacterPureParticleElectricity)target;

            EditorGUILayout.Space(4);
            DrawHeaderBox(controller);

            EditorGUILayout.Space(6);
            DrawSafeDetachSection(controller);

            EditorGUILayout.Space(6);
            DrawPatternSelectionButtons(controller);

            EditorGUILayout.Space(6);
            DrawStunSection(controller);

            EditorGUILayout.Space(6);
            DrawParametersSection(controller);

            EditorGUILayout.Space(6);
            DrawColorSection(controller);

            EditorGUILayout.Space(6);
            DrawSetupSection(controller);

            EditorGUILayout.Space(6);
            DrawIntegrationGuide();

            EditorGUILayout.Space(6);
            DrawAdvancedProperties();

            if (serializedObject.ApplyModifiedProperties())
            {
                controller.ApplyPatternImmediate();
                SceneView.RepaintAll();
            }
        }

        private void DrawHeaderBox(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("⚡ 完全パーティクル放電コントローラー (100% Pure Particle / 0 GC)", titleStyle);

            string targetName = controller.TargetRenderer != null ? controller.TargetRenderer.name : "未設定 (自動検出中)";
            EditorGUILayout.HelpBox(
                "『born model』等のキャラクターに対し、メッシュマテリアル（shiro/ao/kuro）を一切変更せず、" +
                "100% パーティクル（3D稲妻リボン・高速ニードル・電離オーラ・樹状フォーク・這い回りチリチリ）のみで放電演出を行います。\n" +
                "・元のマテリアル・テクスチャ・Litシェーダーを完全に保護。\n" +
                "・全8パターンの多彩な電流表現をワンクリックで切り替え可能。\n" +
                "・Update() 内でのヒープGCアロケーションは完全 0 バイトです。",
                MessageType.Info);

            string statusText = $"【現在の放電】: {controller.CurrentPattern}  |  【対象】: {targetName}";
            EditorGUILayout.LabelField(statusText, EditorStyles.miniBoldLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSafeDetachSection(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛡️ スクリプト安全解除・初期化 (Safe Detach & Reset)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "💡 【スクリプトの外し方 (Remove Component)】\n" +
                "スクリプトを外す前やパーティクルを全撤去したいときは、下のボタンを1クリックしてください。\n" +
                "1. 全6系統の子パーティクル階層（PS_Pure_*）を完全に安全消去（Undo対応）\n" +
                "2. 放電を完全停止し、キャラクター階層を初期状態（純粋な素体状態）に復元\n" +
                "※押し忘れて直接 Remove Component した場合でも、OnDestroy 安全ガードにより自動で全エミッターが安全消去されます。\n" +
                "キャラクター本来のボーン・メッシュ構成を一切壊さず、ゼロ残留で安全に外せます。",
                MessageType.None);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.55f, 0.55f);
            if (GUILayout.Button("⚪ 完全初期化・安全クリーンアップ (Cleanup All Child Particles & Reset)", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog(
                    "完全初期化・安全クリーンアップ",
                    "生成された全6系統の子パーティクルオブジェクト（PS_Pure_*）を安全に消去し、初期状態に戻しますか？\n（キャラクターのボーンやメッシュは保護されます。Undoで元に戻せます）",
                    "消去して初期化", "キャンセル"))
                {
                    Undo.RegisterCompleteObjectUndo(controller, "Cleanup All Pure Particles");
                    controller.CleanupAllParticles(recordUndo: true);
                    serializedObject.Update();
                    SceneView.RepaintAll();
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();
        }

        private void DrawPatternSelectionButtons(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 純粋パーティクル放電パターン（ワンクリック検証）", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;

            // パターン1: アーク・ライトニング・リボン
            DrawPatternButton(controller, PureParticlePattern.Pattern1_ArcRibbons,
                "⚡ パターン1: アーク・ライトニング・リボン (Arc Lightning Ribbons)\n3Dリボン軌跡が激しくのたうち回り、全身を巡る王道稲妻",
                new Color(0.35f, 0.9f, 1.0f));

            // パターン2: スパスミック・ニードルストーム
            DrawPatternButton(controller, PureParticlePattern.Pattern2_SpasmicNeedles,
                "⚡ パターン2: スパスミック・ニードルストーム (Spasmic Needle Storm)\n超高速の鋭い針状放電が全身からハリネズミ状に全方位放射",
                new Color(0.45f, 0.85f, 1.0f));

            // パターン3: 高圧ストロボ・電離オーラ
            DrawPatternButton(controller, PureParticlePattern.Pattern3_StrobeAura,
                "⚡ パターン3: 高圧ストロボ・電離オーラ (High-Voltage Strobe Aura)\n紫色に励起された高圧電離ガスが激しく脈動・ストロボ明滅",
                new Color(0.85f, 0.45f, 1.0f));

            // パターン4: 樹状分岐フォーク・カスケード
            DrawPatternButton(controller, PureParticlePattern.Pattern4_BranchingForks,
                "⚡ パターン4: 樹状分岐フォーク・カスケード (Branching Fork Arcs)\n関節や装甲端からリーヒテンベルク図形のように枝分かれ瞬発放電",
                new Color(0.6f, 0.8f, 1.0f));

            // パターン5: 表面這い回りプラズマ・クリープ
            DrawPatternButton(controller, PureParticlePattern.Pattern5_CrawlingPlasmaCreep,
                "⚡ パターン5: 表面這い回りプラズマ・クリープ (Crawling Plasma Creep)\n装甲表面にまとわりつく高密度エレクトリック流体・球電這い回り",
                new Color(0.3f, 1.0f, 0.7f));

            // パターン6: 過負荷臨界メルトダウン
            DrawPatternButton(controller, PureParticlePattern.Pattern6_OverdriveMeltdown,
                "⚡ パターン6: 過負荷臨界メルトダウン (Overdrive Critical Meltdown)\n金色に輝く超高圧サージ。全エミッターが全開で高密度炸裂",
                new Color(1.0f, 0.75f, 0.2f));

            // パターン7: 磁場旋回バイポーラ・アーク
            DrawPatternButton(controller, PureParticlePattern.Pattern7_MagneticVortex,
                "⚡ パターン7: 磁場旋回バイポーラ・アーク (Magnetic Vortex Bipolar)\n電磁場拘束コイルのようにキャラクター周囲を旋回するアーク流",
                new Color(1.0f, 0.35f, 0.85f));

            // パターン8: 間欠短絡・接触不良スパーク
            DrawPatternButton(controller, PureParticlePattern.Pattern8_IntermittentShort,
                "⚡ パターン8: 間欠短絡・接触不良スパーク (Intermittent Short-Circuit)\nバチバチッと激しく断続する接触不良・短絡ショート放電",
                new Color(1.0f, 0.95f, 0.4f));

            EditorGUILayout.Space(2);

            // 停止・消去ボタン
            EditorGUILayout.BeginHorizontal();
            bool isOff = controller.CurrentPattern == PureParticlePattern.Off;
            GUI.backgroundColor = isOff ? new Color(1.0f, 0.4f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("⚪ 放電停止 (Off / Standby)", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Set Pure Particle Pattern Off");
                controller.CurrentPattern = PureParticlePattern.Off;
                controller.StopEmissionGradually();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.7f);
            if (GUILayout.Button("🧹 即時消去 (Clear & Stop)", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Clear and Stop Pure Electricity");
                controller.CurrentPattern = PureParticlePattern.Off;
                controller.ClearAndStop();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPatternButton(CharacterPureParticleElectricity controller, PureParticlePattern pattern, string label, Color highlightColor)
        {
            Color prevBg = GUI.backgroundColor;
            bool isActive = controller.CurrentPattern == pattern && controller.IsStunned;
            GUI.backgroundColor = isActive ? highlightColor : prevBg;

            if (GUILayout.Button(label, GUILayout.Height(36)))
            {
                Undo.RecordObject(controller, $"Set Pure Particle Pattern {pattern}");
                controller.SetStun(true, immediate: true);
                controller.CurrentPattern = pattern;
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawStunSection(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ スタン・被弾シミュレーション", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = controller.IsStunned ? new Color(1.0f, 0.5f, 0.3f) : prevBg;
            string stunBtnText = controller.IsStunned ? "⚡ スタン解除 (SetStun: false)" : "⚡ スタン開始 (SetStun: true)";
            if (GUILayout.Button(stunBtnText, GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Toggle Stun");
                controller.SetStun(!controller.IsStunned, immediate: false);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
            if (GUILayout.Button("💥 即時バースト (TriggerBurst)", GUILayout.Height(28)))
            {
                controller.TriggerBurst(30, 1.2f);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_currentStunWeight"), new GUIContent("スタン進行度 (Weight)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_stunFadeSpeed"), new GUIContent("フェード速度 (Fade Speed)"));

            EditorGUILayout.EndVertical();
        }

        private void DrawParametersSection(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎛️ パラメーター微調整", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_particlesEnabled"), new GUIContent("パーティクル有効化"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_ribbonWorldSpace"), new GUIContent("リボン軌跡ワールド空間化 (World Space Trails)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rateMultiplier"), new GUIContent("放出量倍率 (Rate)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_speedMultiplier"), new GUIContent("飛散速度倍率 (Speed)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sizeMultiplier"), new GUIContent("サイズ倍率 (Size)"));

            EditorGUILayout.EndVertical();
        }

        private void DrawColorSection(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 放電カラー・カスタム", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_overrideColor"), new GUIContent("カラー上書き"));
            if (controller.OverrideColor)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_customColor"), new GUIContent("カスタム放電色 (HDR)"));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("シアン (青)")) { controller.CustomColor = new Color(0.25f, 0.85f, 2.5f, 1f); }
                if (GUILayout.Button("マゼンタ (紫)")) { controller.CustomColor = new Color(2.2f, 0.4f, 1.9f, 1f); }
                if (GUILayout.Button("ゴールド (金)")) { controller.CustomColor = new Color(2.8f, 1.7f, 0.25f, 1f); }
                if (GUILayout.Button("エメラルド (緑)")) { controller.CustomColor = new Color(0.2f, 2.2f, 1.6f, 1f); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupSection(CharacterPureParticleElectricity controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔨 自動セットアップ＆エミッター状態", EditorStyles.boldLabel);

            bool allPresent = controller.RibbonEmitter != null &&
                              controller.NeedleEmitter != null &&
                              controller.AuraEmitter != null &&
                              controller.ForkEmitter != null &&
                              controller.CrackleEmitter != null &&
                              controller.BurstEmitter != null;
            bool hasAny = controller.HasAnyEmitters();

            if (allPresent)
            {
                EditorGUILayout.HelpBox("✅ 全6系統のパーティクルエミッターが正常に接続されています。", MessageType.None);
            }
            else if (hasAny)
            {
                EditorGUILayout.HelpBox("⚠️ 一部のパーティクルエミッターが未生成または参照切れです。「自動構築」を押して補完するか、「全エミッター削除」でクリアできます。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("⚪ パーティクルエミッターは未生成です。「完全自動生成」ボタンで即座に構築できます。", MessageType.Info);
            }

            if (hasAny)
            {
                EditorGUILayout.Space(2);
                Color prevBg2 = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.7f, 0.4f);
                if (GUILayout.Button("🗑️ 全エミッターを削除・階層クリーンアップ (Cleanup All Emitters)", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog(
                        "全エミッター削除・階層クリーンアップ",
                        "生成された全6系統の子パーティクルオブジェクト（PS_Pure_*）を安全に削除しますか？\n（Undo対応）",
                        "削除する", "キャンセル"))
                    {
                        Undo.RegisterCompleteObjectUndo(controller, "Cleanup All Pure Particles");
                        controller.CleanupAllParticles(recordUndo: true);
                        serializedObject.Update();
                        SceneView.RepaintAll();
                    }
                }
                GUI.backgroundColor = prevBg2;
            }

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = allPresent ? prevBg : new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("🔨 パーティクル階層を完全自動生成・再構築 (Setup Hierarchy)", GUILayout.Height(32)))
            {
                Undo.RecordObject(controller, "Setup Pure Particle Hierarchy");
                controller.SetupParticlesHierarchy();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("🧪 純粋パーティクル放電テストを実行 (Run Automated Tests)", GUILayout.Height(28)))
            {
                CharacterPureParticleElectricityTest.RunTests();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawIntegrationGuide()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showGuide = EditorGUILayout.Foldout(_showGuide, "📖 プレイヤー連携コード例 (RacerManagerBase等)", true);
            if (_showGuide)
            {
                EditorGUILayout.TextArea(
                    "// 1. 取得\n" +
                    "var electricity = GetComponentInChildren<CharacterPureParticleElectricity>();\n\n" +
                    "// 2. スタン開始 / 終了\n" +
                    "electricity.SetStun(true);\n" +
                    "electricity.SetStun(false);\n\n" +
                    "// 3. パターン指定\n" +
                    "electricity.SetPattern(PureParticlePattern.Pattern1_ArcRibbons);\n\n" +
                    "// 4. 被弾バースト（衝突ベクトル付き）\n" +
                    "electricity.TriggerBurst(30, 1.2f, null, impactVector);\n\n" +
                    "// 5. 放電カラー変更\n" +
                    "electricity.SetCustomColor(new Color(2.8f, 1.7f, 0.25f, 1.0f));",
                    GUILayout.Height(180));
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedProperties()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "⚙️ 内部プロパティ参照 (Advanced)", true);
            if (_showAdvanced)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetRenderer"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_ribbonEmitter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_needleEmitter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_auraEmitter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_forkEmitter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_crackleEmitter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_burstEmitter"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_matTrail"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_matNeedle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPlasma"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_matFork"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_matCrackle"));
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
