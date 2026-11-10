using UnityEditor;
using UnityEngine;

namespace AT_Murayama.CharacterElectricity.Editor
{
    [CustomEditor(typeof(CharacterElectricityPreview), true)]
    public class CharacterElectricityPreviewEditor : UnityEditor.Editor
    {
        // 開発者・詳細設定用 Foldout（デフォルトはすべて閉じてUIを劇的にスリム化）
        private bool _showParticleManagement = false;
        private bool _showPlayerGuide = false;
        private bool _showDetails = false;
        private bool _showMaterialSlots = false;

        #region Pattern Metadata Definitions

        private struct ShaderPatternInfo
        {
            public CharacterElectricityPreview.ElectricityPattern pattern;
            public string shortLabel;
            public string fullTitle;
            public string description;
            public Color highlightColor;

            public ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern pattern,
                string shortLabel,
                string fullTitle,
                string description,
                Color highlightColor)
            {
                this.pattern = pattern;
                this.shortLabel = shortLabel;
                this.fullTitle = fullTitle;
                this.description = description;
                this.highlightColor = highlightColor;
            }
        }

        private static readonly ShaderPatternInfo[] ShaderPatterns = new[]
        {
            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc,
                "P1: 高密度アーク",
                "パターン1: 高密度アーク放電 (Surface Arc)",
                "装甲表面を血管・基板のように細かく走る鋭い稲妻。高密度な電撃感。",
                new Color(0.35f, 0.9f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern2_Pulse,
                "P2: 脈動オーバードライブ",
                "パターン2: 脈動オーバードライブ (Pulse Overcharge)",
                "全身を周期的に駆け巡る高圧サージウェーブ ＋ 装甲の励起グロー。",
                new Color(0.4f, 0.7f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern3_Plasma,
                "P3: 纏わりプラズマ膜",
                "パターン3: 纏わりプラズマ膜 (Plasma Cling)",
                "輪郭にまとわりつく電離プラズマの皮膜 ＋ 局所ショート火花。",
                new Color(0.7f, 0.5f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold,
                "P4: 超帯電ゴールド",
                "パターン4: 超帯電ゴールド (Golden Overload)",
                "白熱する芯を持つ高エネルギー金色の稲妻（パワーアップ・大ダメージ）。",
                new Color(1.0f, 0.85f, 0.35f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern5_CleanStreak,
                "P5: クリーンストリーク",
                "パターン5: クリーン・低密度ストリーク (Clean Streak) 【推奨】",
                "密度を落とし、太く明瞭な稲妻ラインが走る洗練された低密度表現。",
                new Color(0.35f, 1.0f, 0.85f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern6_FresnelRim,
                "P6: フレネル・エッジ",
                "パターン6: フレネル・エッジ放電 (Fresnel Rim Discharge)",
                "輪郭のみに電気が纏わりつき、正面のキャラ造形や顔・装甲を隠さない。",
                new Color(0.4f, 0.75f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern7_ShortSparks,
                "P7: ショートスパーク",
                "パターン7: 間欠的ショートスパーク (Short Sparks)",
                "バチッ…バチッ…と不規則に激しい火花が局所的に弾け飛ぶ故障・麻痺感。",
                new Color(0.9f, 0.55f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern8_CyberCircuit,
                "P8: 電子サーキット",
                "パターン8: 電子サーキット・グリッド (Cyber Circuit Surge)",
                "メッシュに沿って電子基板の直角ラインとデータサージが走るSF幾何学放電。",
                new Color(0.3f, 0.95f, 0.55f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern9_MultiChain,
                "P9: 多点連鎖スパーク",
                "パターン9: 多点連鎖クラスタースパーク (Multi-Point Chain) 【短絡連鎖】",
                "2〜3箇所の部位から時間差で連鎖的に飛び交う激しい過負荷ショート。",
                new Color(0.35f, 0.85f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern10_MicroCrackle,
                "P10: 微小チリチリ",
                "パターン10: 微小チリチリリーク (Micro Spark Jitter / Crackle) 【視界クリア】",
                "全身各所で小型の鋭い火花がチリチリ弾ける、視界を邪魔しない繊細な麻痺演出。",
                new Color(0.65f, 0.75f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern11_HeavyArcBurst,
                "P11: 高圧バースト",
                "パターン11: 高圧アーク放電バースト (Heavy High-Voltage Arc Burst) 【一撃重放電】",
                "予兆小スパークから一瞬「ドカン！バチィン！」と太い高電圧アークが奔流。",
                new Color(0.3f, 0.65f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern12_ThermalAfterglow,
                "P12: 余熱赤熱スパーク",
                "パターン12: 余熱赤熱スパーク (Thermal Afterglow / Molten Ember) 【溶融ダメージ】",
                "火花が弾けた後、装甲が赤熱・余熱でボワッとオレンジに残りゆっくり冷める。",
                new Color(1.0f, 0.6f, 0.3f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern13_AnatomicalGrounding,
                "P13: 3D骨格接地",
                "パターン13: 3D骨格・解剖学的接地アーク (Anatomical Grounding) 【次世代案1】",
                "UV継ぎ目を超越し、胸/肩の高電位コアから腰・足元のアースへ3D最短経路で貫通。",
                new Color(0.25f, 0.95f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern14_LichtenbergBranch,
                "P14: リーヒテンベルク",
                "パターン14: リーヒテンベルク樹状分岐カスケード (Lichtenberg Branch) 【次世代案2】",
                "太い主幹アークが走った直後(+18ms)、斜め前方へ鋭い小枝と孫枝がフラクタル分岐。",
                new Color(0.45f, 0.85f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern15_SteppedLeader,
                "P15: ステップトリーダー",
                "パターン15: 絶縁破壊ステップトリーダー (Stepped Leader Staccato) 【次世代案3】",
                "「チッ(30%)…パチッ(65%)…」と前進した直後、「バチィィン！(100%)」と本線が炸裂。",
                new Color(0.55f, 0.75f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern16_ImpactVector,
                "P16: 被弾ベクトル",
                "パターン16: 被弾点・タックル衝突ベクトル飛散 (Impact Vector Sparks) 【次世代案4】",
                "被弾した衝撃点を起点に、吹っ飛び方向（気流後方）へ扇状に電撃と火花が飛散。",
                new Color(0.3f, 1.0f, 0.75f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern17_ShockwaveDistortion,
                "P17: 衝撃波歪み",
                "パターン17: 衝撃波ディストーション＆爆圧スパーク (Shockwave Refraction) 【次世代案5】",
                "火花炸裂の瞬間、同心円状の衝撃波リングで周囲の装甲模様と反射光が物理的に歪む。",
                new Color(0.75f, 0.65f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern18_MagneticWhip,
                "P18: アークウィップ",
                "パターン18: 双極マグネティック・アークウィップ (Magnetic Arc Whip) 【しなり断裂】",
                "2電極間でアークが磁場で弓なりにしなり(Whip)、限界でパチン！と千切れて火花化。",
                new Color(0.85f, 0.55f, 1.0f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern19_ArmorSeamShort,
                "P19: 装甲シーム短絡",
                "パターン19: 装甲シーム・モールド沿い短絡奔流 (Armor Seam Short) 【隙間漏電】",
                "テクスチャの色境界とポリゴン角を自動検出し、装甲パーツの隙間から火花が噴出。",
                new Color(0.35f, 0.95f, 0.95f)),

            new ShaderPatternInfo(
                CharacterElectricityPreview.ElectricityPattern.Pattern20_CriticalStrobe,
                "P20: 臨界ストロボ",
                "パターン20: 臨界ストロボ連鎖 (Critical Strobe Cascade) 【★P7×P9集大成】",
                "P7の2段ダブルタップ短絡とP9の3連高速ジャンプが非周期リズムで切り替わる決定版。",
                new Color(1.0f, 0.9f, 0.35f))
        };

        private struct PurePatternInfo
        {
            public PureParticlePattern pattern;
            public string shortLabel;
            public string fullTitle;
            public string description;
            public Color highlightColor;

            public PurePatternInfo(
                PureParticlePattern pattern,
                string shortLabel,
                string fullTitle,
                string description,
                Color highlightColor)
            {
                this.pattern = pattern;
                this.shortLabel = shortLabel;
                this.fullTitle = fullTitle;
                this.description = description;
                this.highlightColor = highlightColor;
            }
        }

        private static readonly PurePatternInfo[] PurePatterns = new[]
        {
            new PurePatternInfo(
                PureParticlePattern.Pattern1_ArcRibbons,
                "P1: アークリボン",
                "パターン1: アーク・ライトニング・リボン (Arc Lightning Ribbons)",
                "体表から激しくうねる立体3Dジグザグ稲妻リボン軌跡。",
                new Color(0.35f, 0.9f, 1.0f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern2_SpasmicNeedles,
                "P2: ニードルストーム",
                "パターン2: スパスミック・ニードルストーム (Spasmic Needle Storm)",
                "全身から鋭角に高速四方放射される多数のニードルスパーク。",
                new Color(0.4f, 0.8f, 1.0f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern3_StrobeAura,
                "P3: ストロボオーラ",
                "パターン3: 高圧ストロボ・電離オーラ (High-Voltage Strobe Aura)",
                "装甲表面を包み込む高圧励起プラズマ雲とストロボ光。",
                new Color(0.85f, 0.5f, 1.0f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern4_BranchingForks,
                "P4: 樹状フォーク",
                "パターン4: 樹状分岐フォーク・カスケード (Branching Fork Arcs)",
                "関節・突起から空間へと枝分かれしながら弾け飛ぶ樹状放電。",
                new Color(0.45f, 0.75f, 1.0f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern5_CrawlingPlasmaCreep,
                "P5: プラズマ這い回り",
                "パターン5: 表面這い回りプラズマ・クリープ (Crawling Plasma Creep)",
                "装甲表面に沿って超高密度に這い回る微小チリチリ電流膜。",
                new Color(0.3f, 1.0f, 0.8f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern6_OverdriveMeltdown,
                "P6: 臨界メルトダウン",
                "パターン6: 過負荷臨界メルトダウン (Overdrive Critical Meltdown)",
                "全エミッターが最大出力で暴走・炸裂する超高圧短絡状態。",
                new Color(1.0f, 0.4f, 0.4f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern7_MagneticVortex,
                "P7: 磁場旋回",
                "パターン7: 磁場旋回バイポーラ・アーク (Magnetic Vortex Bipolar)",
                "強力な磁場に沿ってキャラクター周囲を旋回・吸着する稲妻。",
                new Color(0.6f, 0.5f, 1.0f)),

            new PurePatternInfo(
                PureParticlePattern.Pattern8_IntermittentShort,
                "P8: 間欠短絡",
                "パターン8: 間欠短絡・接触不良スパーク (Intermittent Short-Circuit)",
                "不規則な点滅・スタッカート破裂を繰り返す接触不良ショート。",
                new Color(1.0f, 0.85f, 0.3f))
        };

        #endregion

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            CharacterElectricityPreview preview = (CharacterElectricityPreview)target;

            EditorGUILayout.Space(3);
            DrawCompactHeader(preview);

            EditorGUILayout.Space(4);
            DrawModeToolbar(preview);

            EditorGUILayout.Space(4);
            // モードに応じた放電パターン選択（2列グリッド ＋ 動的説明）
            if (preview.DischargeMode == CharacterDischargeMode.ParticleOnly)
            {
                DrawPureParticlePatternGrid(preview);
            }
            else if (preview.DischargeMode == CharacterDischargeMode.ShaderOnly)
            {
                DrawShaderPatternGrid(preview);
            }
            else // Hybrid or RichHybrid
            {
                DrawShaderPatternGrid(preview);
                if (preview.UsesPureParticles)
                {
                    EditorGUILayout.Space(4);
                    DrawPureParticlePatternGrid(preview);
                }
            }

            // パラメーター＆スタン・カラー調整（1箇所に集約）
            EditorGUILayout.Space(4);
            DrawAdjustmentSection(preview);

            // 3つの Foldout セクション（デフォルトすべて false で閉じた状態）
            EditorGUILayout.Space(4);
            DrawParticleManagementFoldout(preview);

            EditorGUILayout.Space(4);
            DrawPlayerIntegrationGuide();

            EditorGUILayout.Space(4);
            DrawDetailsFoldout(preview);

            if (serializedObject.ApplyModifiedProperties())
            {
                if (preview.DischargeMode == CharacterDischargeMode.ParticleOnly)
                {
                    preview.SyncPureParticlesImmediate();
                }
                else
                {
                    preview.ApplyCurrentPattern();
                    if (preview.UsesPureParticles)
                    {
                        preview.SyncPureParticlesImmediate();
                    }
                }
                SceneView.RepaintAll();
            }
        }

        #region Compact Header (Guideline 1 & 5: 1-line header with reset)

        private void DrawCompactHeader(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("⚡ キャラクター帯電・放電コントローラー (0 GC)", titleStyle);

            // ヘッダー右端のコンパクト初期化・安全復元ボタン
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button("🔄 初期状態にリセット", GUILayout.Width(130), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog(
                    "完全初期化・安全クリーンアップ",
                    "元のデザイナー制作マテリアルへ復元し、生成された子パーティクルオブジェクトを全削除しますか？\n（キャラクターのボーンやメッシュは保護されます。Undo対応）",
                    "実行する", "キャンセル"))
                {
                    Undo.RegisterCompleteObjectUndo(preview, "Reset Character Electricity");
                    preview.ResetToOriginalAndCleanup(recordUndo: true);
                    serializedObject.Update();
                    SceneView.RepaintAll();
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            // 1行ステータスサマリー（モード、放電状態、バックアップ元マテリアル）
            string modeName = preview.DischargeMode switch
            {
                CharacterDischargeMode.ShaderOnly => "シェーダーのみ",
                CharacterDischargeMode.ParticleOnly => "パーティクルのみ",
                CharacterDischargeMode.RichHybrid => "リッチハイブリッド",
                CharacterDischargeMode.Hybrid => preview.HybridSource switch
                {
                    HybridParticleSource.PureParticles => "リッチハイブリッド",
                    HybridParticleSource.Both => "デュアルハイブリッド",
                    _ => "軽量ハイブリッド"
                },
                _ => ""
            };

            string shaderPatternStr = preview.DischargeMode == CharacterDischargeMode.ParticleOnly
                ? "未使用"
                : (preview.CurrentPattern == CharacterElectricityPreview.ElectricityPattern.Original
                    ? "停止 (元マテリアル)"
                    : preview.CurrentPattern.ToString());

            string particlePatternStr = preview.DischargeMode == CharacterDischargeMode.ShaderOnly
                ? "未使用"
                : (preview.PurePattern == PureParticlePattern.Off
                    ? "停止 (Standby)"
                    : preview.PurePattern.ToString());

            string backupName = preview.OriginalMaterialBackup != null ? preview.OriginalMaterialBackup.name : "未取得 (shiro.mat)";

            string statusSummary = $"【モード】{modeName} | 【シェーダー: {shaderPatternStr}】 | 【パーティクル: {particlePatternStr}】 | 【退避元: {backupName}】";
            EditorGUILayout.LabelField(statusSummary, EditorStyles.miniLabel);

            // シェーダー単体モード時に万一残存パーティクルが存在する場合のアラート＆クリーンアップ
            if (preview.DischargeMode == CharacterDischargeMode.ShaderOnly && preview.HasAnyParticleObjects())
            {
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUILayout.LabelField("⚠️ 子階層に残存パーティクルを検出", EditorStyles.miniBoldLabel);
                GUI.backgroundColor = new Color(1.0f, 0.72f, 0.45f);
                if (GUILayout.Button("🗑️ 残存パーティクル削除", EditorStyles.miniButton, GUILayout.Width(140)))
                {
                    Undo.RegisterCompleteObjectUndo(preview, "Delete Residual Particles");
                    preview.DeleteParticlesAndSetShaderOnly(recordUndo: true);
                    serializedObject.Update();
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Mode Selection Toolbar (Guideline 2: Unified 1-Line Mode Selection)

        private void DrawModeToolbar(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ 放電動作モード (Discharge Mode)", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;
            EditorGUILayout.BeginHorizontal();

            // Mode 1: シェーダーのみ
            bool isShader = preview.DischargeMode == CharacterDischargeMode.ShaderOnly;
            GUI.backgroundColor = isShader ? new Color(0.35f, 0.9f, 1.0f) : prevBg;
            if (GUILayout.Button("⚡ シェーダーのみ\n(Shader: P1~P20)", GUILayout.Height(32)))
            {
                SwitchModeWithUndo(preview, CharacterDischargeMode.ShaderOnly);
            }

            // Mode 2: パーティクルのみ
            bool isParticle = preview.DischargeMode == CharacterDischargeMode.ParticleOnly;
            GUI.backgroundColor = isParticle ? new Color(0.4f, 1.0f, 0.6f) : prevBg;
            if (GUILayout.Button("✨ パーティクルのみ\n(Particle: P1~P8)", GUILayout.Height(32)))
            {
                SwitchModeWithUndo(preview, CharacterDischargeMode.ParticleOnly);
            }

            // Mode 3: ハイブリッド
            bool isHybrid = preview.DischargeMode == CharacterDischargeMode.Hybrid || preview.DischargeMode == CharacterDischargeMode.RichHybrid;
            GUI.backgroundColor = isHybrid ? new Color(1.0f, 0.6f, 0.25f) : prevBg;
            if (GUILayout.Button("🌟 ハイブリッド放電\n(Shader + Particle)", GUILayout.Height(32)))
            {
                if (!isHybrid)
                {
                    SwitchModeWithUndo(preview, CharacterDischargeMode.RichHybrid);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = prevBg;

            // ハイブリッド選択時はツールバー直下にシンプルな1行ミニツールバーでパーティクル連動方式を選択
            if (isHybrid)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("連動方式:", EditorStyles.miniBoldLabel, GUILayout.Width(60));

                bool isPure = preview.DischargeMode == CharacterDischargeMode.RichHybrid ||
                              (preview.DischargeMode == CharacterDischargeMode.Hybrid && preview.HybridSource == HybridParticleSource.PureParticles);
                bool isLight = preview.DischargeMode == CharacterDischargeMode.Hybrid && preview.HybridSource == HybridParticleSource.Lightweight;
                bool isBoth = preview.DischargeMode == CharacterDischargeMode.Hybrid && preview.HybridSource == HybridParticleSource.Both;

                GUI.backgroundColor = isPure ? new Color(0.4f, 1.0f, 0.6f) : prevBg;
                if (GUILayout.Button("🌟 リッチ純粋 (P1~P8) [推奨]", EditorStyles.miniButtonLeft, GUILayout.Height(22)))
                {
                    SwitchHybridSourceWithUndo(preview, HybridParticleSource.PureParticles);
                }

                GUI.backgroundColor = isLight ? new Color(1.0f, 0.85f, 0.35f) : prevBg;
                if (GUILayout.Button("⚡ 軽量スパーク (3系統)", EditorStyles.miniButtonMid, GUILayout.Height(22)))
                {
                    SwitchHybridSourceWithUndo(preview, HybridParticleSource.Lightweight);
                }

                GUI.backgroundColor = isBoth ? new Color(1.0f, 0.5f, 0.8f) : prevBg;
                if (GUILayout.Button("💥 両方併用 (デュアル)", EditorStyles.miniButtonRight, GUILayout.Height(22)))
                {
                    SwitchHybridSourceWithUndo(preview, HybridParticleSource.Both);
                }

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = prevBg;
            }

            // 選択中モードの簡潔な1行説明
            string modeDesc = preview.DischargeMode switch
            {
                CharacterDischargeMode.ShaderOnly => "子パーティクルゼロ。メッシュ表面のURPシェーダー（P1〜P20）のみで超軽量動作。",
                CharacterDischargeMode.ParticleOnly => "元マテリアル（shiro/ao/kuro）を100%保持し、3Dパーティクル＆リボン（P1〜P8）のみで表現。",
                CharacterDischargeMode.RichHybrid => "メッシュ放電シェーダー（P1〜P20）＋ 6系統純粋パーティクル（P1〜P8）完全連動。",
                CharacterDischargeMode.Hybrid => preview.HybridSource switch
                {
                    HybridParticleSource.PureParticles => "メッシュ放電シェーダー（P1〜P20）＋ 6系統純粋パーティクル（P1〜P8）完全連動。",
                    HybridParticleSource.Both => "シェーダー放電 ＋ 純粋パーティクル ＋ 軽量スパーク全開噴出のデュアル連携。",
                    _ => "メッシュ放電シェーダー（P1〜P20）＋ 軽量3D放電スパーク（3系統）連動。"
                },
                _ => ""
            };
            EditorGUILayout.HelpBox(modeDesc, MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void SwitchModeWithUndo(CharacterElectricityPreview preview, CharacterDischargeMode newMode)
        {
            if (preview.DischargeMode == newMode) return;
            Undo.RegisterCompleteObjectUndo(preview, $"Switch Discharge Mode to {newMode}");
            if (preview.TargetRenderer != null)
            {
                Undo.RegisterCompleteObjectUndo(preview.TargetRenderer, "Switch Discharge Mode Material");
            }
            preview.SetDischargeMode(newMode, recordUndo: true);
            serializedObject.Update();
            EditorUtility.SetDirty(preview);
            if (preview.TargetRenderer != null)
            {
                EditorUtility.SetDirty(preview.TargetRenderer);
            }
            SceneView.RepaintAll();
        }

        private void SwitchHybridSourceWithUndo(CharacterElectricityPreview preview, HybridParticleSource newSource)
        {
            Undo.RegisterCompleteObjectUndo(preview, $"Switch Hybrid Particle Source to {newSource}");
            if (newSource == HybridParticleSource.PureParticles)
            {
                preview.SetDischargeMode(CharacterDischargeMode.RichHybrid, recordUndo: true);
                preview.SetHybridParticleSource(HybridParticleSource.PureParticles, recordUndo: true);
            }
            else
            {
                preview.SetDischargeMode(CharacterDischargeMode.Hybrid, recordUndo: true);
                preview.SetHybridParticleSource(newSource, recordUndo: true);
            }
            serializedObject.Update();
            EditorUtility.SetDirty(preview);
            SceneView.RepaintAll();
        }

        #endregion

        #region Pattern Selection (Guideline 3: 2-Column Grid with Categories & Contextual Description)

        private void DrawShaderPatternGrid(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 シェーダー放電パターン (全20種・2列グリッド)", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;

            // カテゴリ1: 基本放電パターン (P1〜P12)
            EditorGUILayout.LabelField("▼ 基本放電パターン (P1〜P12)", EditorStyles.miniBoldLabel);
            DrawShaderPatternGridRange(preview, 0, 12, prevBg);

            EditorGUILayout.Space(2);
            // カテゴリ2: 次世代・物理連動パターン (P13〜P17)
            EditorGUILayout.LabelField("▼ 次世代・物理連動パターン (P13〜P17)", EditorStyles.miniBoldLabel);
            DrawShaderPatternGridRange(preview, 12, 5, prevBg);

            EditorGUILayout.Space(2);
            // カテゴリ3: 究極進化・複合パターン (P18〜P20)
            EditorGUILayout.LabelField("▼ 究極進化・複合パターン (P18〜P20)", EditorStyles.miniBoldLabel);
            DrawShaderPatternGridRange(preview, 17, 3, prevBg);

            EditorGUILayout.Space(2);

            // 元マテリアルに戻す停止ボタン
            bool isOrig = preview.CurrentPattern == CharacterElectricityPreview.ElectricityPattern.Original;
            GUI.backgroundColor = isOrig ? new Color(1.0f, 0.6f, 0.6f) : prevBg;
            if (GUILayout.Button("⚪ 元のデザイナー制作マテリアルに戻す (Restore Original)", GUILayout.Height(24)))
            {
                ApplyPatternWithUndo(preview, CharacterElectricityPreview.ElectricityPattern.Original);
            }
            GUI.backgroundColor = prevBg;

            // グリッド直下に選択中パターンの詳細説明を動的表示
            EditorGUILayout.Space(2);
            DrawShaderPatternContextualBox(preview);

            EditorGUILayout.EndVertical();
        }

        private void DrawShaderPatternGridRange(CharacterElectricityPreview preview, int startIndex, int count, Color defaultBg)
        {
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawShaderPatternButton(preview, ShaderPatterns[i], defaultBg);
                if (i + 1 < endIndex)
                {
                    DrawShaderPatternButton(preview, ShaderPatterns[i + 1], defaultBg);
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawShaderPatternButton(CharacterElectricityPreview preview, ShaderPatternInfo item, Color defaultBg)
        {
            bool isSelected = preview.CurrentPattern == item.pattern;
            GUI.backgroundColor = isSelected ? item.highlightColor : defaultBg;
            if (GUILayout.Button(item.shortLabel, GUILayout.Height(26)))
            {
                ApplyPatternWithUndo(preview, item.pattern);
            }
        }

        private void DrawShaderPatternContextualBox(CharacterElectricityPreview preview)
        {
            if (preview.CurrentPattern == CharacterElectricityPreview.ElectricityPattern.Original)
            {
                EditorGUILayout.HelpBox("⚪ 【放電停止】元のデザイナー制作マテリアルに戻り、放電エフェクトは停止しています。", MessageType.None);
                return;
            }

            for (int i = 0; i < ShaderPatterns.Length; i++)
            {
                if (ShaderPatterns[i].pattern == preview.CurrentPattern)
                {
                    EditorGUILayout.HelpBox($"⚡ 【{ShaderPatterns[i].fullTitle}】\n{ShaderPatterns[i].description}", MessageType.None);
                    return;
                }
            }
        }

        private void ApplyPatternWithUndo(CharacterElectricityPreview preview, CharacterElectricityPreview.ElectricityPattern pattern)
        {
            Undo.RecordObject(preview, $"Switch Electricity Pattern to {pattern}");
            if (preview.TargetRenderer != null)
            {
                Undo.RecordObject(preview.TargetRenderer, $"Switch Electricity Material on {preview.TargetRenderer.name}");
            }

            if (pattern == CharacterElectricityPreview.ElectricityPattern.Original)
            {
                if (!preview.UsesPureParticles || preview.PurePattern == PureParticlePattern.Off)
                {
                    preview.SetStun(false, immediate: true);
                }
                preview.SwitchPattern(pattern, triggerBurst: false);
            }
            else
            {
                preview.SwitchPattern(pattern, triggerBurst: false);
                preview.SetStun(true, immediate: true);
            }

            serializedObject.Update();
            EditorUtility.SetDirty(preview);
            if (preview.TargetRenderer != null)
            {
                EditorUtility.SetDirty(preview.TargetRenderer);
            }
            SceneView.RepaintAll();
        }

        private void DrawPureParticlePatternGrid(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("✨ 純粋パーティクル放電パターン (全8種・2列グリッド)", EditorStyles.boldLabel);

            Color prevBg = GUI.backgroundColor;

            // 2列グリッドで全8ボタンを配置
            for (int i = 0; i < PurePatterns.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawPurePatternButton(preview, PurePatterns[i], prevBg);
                if (i + 1 < PurePatterns.Length)
                {
                    DrawPurePatternButton(preview, PurePatterns[i + 1], prevBg);
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(2);

            // 停止 / スタンバイ ボタン
            bool isOff = preview.PurePattern == PureParticlePattern.Off;
            GUI.backgroundColor = isOff ? new Color(1.0f, 0.6f, 0.6f) : prevBg;
            if (GUILayout.Button("⚪ 停止 / スタンバイ (Off / Standby)", GUILayout.Height(24)))
            {
                ApplyPurePatternWithUndo(preview, PureParticlePattern.Off);
            }
            GUI.backgroundColor = prevBg;

            // グリッド直下に選択中純粋パーティクルパターンの詳細説明を動的表示
            EditorGUILayout.Space(2);
            DrawPurePatternContextualBox(preview);

            EditorGUILayout.EndVertical();
        }

        private void DrawPurePatternButton(CharacterElectricityPreview preview, PurePatternInfo item, Color defaultBg)
        {
            bool isSelected = preview.PurePattern == item.pattern;
            GUI.backgroundColor = isSelected ? item.highlightColor : defaultBg;
            if (GUILayout.Button(item.shortLabel, GUILayout.Height(26)))
            {
                ApplyPurePatternWithUndo(preview, item.pattern);
            }
        }

        private void DrawPurePatternContextualBox(CharacterElectricityPreview preview)
        {
            if (preview.PurePattern == PureParticlePattern.Off)
            {
                EditorGUILayout.HelpBox("⚪ 【スタンバイ】純粋パーティクル放電は停止（待機）しています。", MessageType.None);
                return;
            }

            for (int i = 0; i < PurePatterns.Length; i++)
            {
                if (PurePatterns[i].pattern == preview.PurePattern)
                {
                    EditorGUILayout.HelpBox($"✨ 【{PurePatterns[i].fullTitle}】\n{PurePatterns[i].description}", MessageType.None);
                    return;
                }
            }
        }

        private void ApplyPurePatternWithUndo(CharacterElectricityPreview preview, PureParticlePattern pattern)
        {
            Undo.RecordObject(preview, $"Switch Pure Particle Pattern to {pattern}");
            preview.SwitchPureParticlePattern(pattern, triggerBurst: pattern != PureParticlePattern.Off);
            serializedObject.Update();
            EditorUtility.SetDirty(preview);
            SceneView.RepaintAll();
        }

        #endregion

        #region Adjustment Section (Guideline 4: Unified Parameters & Control)

        private void DrawAdjustmentSection(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎛️ パラメーター＆スタン・カラー調整 (Parameters & Control)", EditorStyles.boldLabel);

            // 1. スタン状態シミュレーション
            Color prevBg = GUI.backgroundColor;
            bool isStunned = preview.IsStunned;
            GUI.backgroundColor = isStunned ? new Color(1.0f, 0.75f, 0.2f) : new Color(0.75f, 0.75f, 0.75f);
            string btnText = isStunned ? "⚡ スタン中：放電アクティブ (クリックで停止)" : "⚪ 通常状態：放電停止 (クリックでスタン発生)";

            if (GUILayout.Button(btnText, GUILayout.Height(28)))
            {
                Undo.RecordObject(preview, "Toggle Stun State");
                preview.SetStun(!isStunned);
                EditorUtility.SetDirty(preview);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_stunTransitionSpeed"), new GUIContent("スタンフェード速度"));

            // 2. シェーダー調整（ShaderOnly または Hybrid時）
            if (preview.DischargeMode != CharacterDischargeMode.ParticleOnly)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("▼ シェーダー発光・速度調整:", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_intensityMultiplier"), new GUIContent("発光強度倍率 (Intensity)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_speedMultiplier"), new GUIContent("放電速度倍率 (Speed)"));

                // Pattern 16 専用の被弾中心UV・吹っ飛び方向ベクトル（選択時のみ条件付き表示）
                if (preview.CurrentPattern == CharacterElectricityPreview.ElectricityPattern.Pattern16_ImpactVector)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField("🎯 被弾ベクトル設定（Pattern 16 専用）", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_impactCenterUV"), new GUIContent("被弾中心UV"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_impactDirectionUV"), new GUIContent("吹っ飛び方向ベクトル"));
                    EditorGUILayout.EndVertical();
                }
            }

            // 3. 純粋パーティクル調整（PureParticles利用時）
            if (preview.UsesPureParticles)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("▼ 純粋パーティクル調整パラメーター:", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_pureRateMultiplier"), new GUIContent("放出量倍率 (Rate)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_pureSpeedMultiplier"), new GUIContent("飛散速度倍率 (Speed)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_pureSizeMultiplier"), new GUIContent("サイズ倍率 (Size)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_pureRibbonWorldSpace"), new GUIContent("ワールドリボン引きずり (Ribbon World)"));
            }

            // 4. カラー調整（プリセットボタン横1列）
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("▼ 電流カラー調整（汎用カスタム）:", EditorStyles.miniBoldLabel);
            SerializedProperty overrideProp = serializedObject.FindProperty("_overrideColor");
            EditorGUILayout.PropertyField(overrideProp, new GUIContent("カラーをカスタム指定する"));

            if (overrideProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_customElectricityColor"), new GUIContent("放電HDRカラー"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("ワンクリック色パレット:", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("シアン青", EditorStyles.miniButton))
                {
                    Undo.RecordObject(preview, "Change Electricity Color");
                    preview.SetElectricityColor(new Color(0.25f, 0.85f, 2.5f, 1.0f));
                    EditorUtility.SetDirty(preview);
                }
                if (GUILayout.Button("紫・プラズマ", EditorStyles.miniButton))
                {
                    Undo.RecordObject(preview, "Change Electricity Color");
                    preview.SetElectricityColor(new Color(1.3f, 0.35f, 2.5f, 1.0f));
                    EditorUtility.SetDirty(preview);
                }
                if (GUILayout.Button("黄金雷", EditorStyles.miniButton))
                {
                    Undo.RecordObject(preview, "Change Electricity Color");
                    preview.SetElectricityColor(new Color(2.5f, 1.6f, 0.2f, 1.0f));
                    EditorUtility.SetDirty(preview);
                }
                if (GUILayout.Button("緑・毒放電", EditorStyles.miniButton))
                {
                    Undo.RecordObject(preview, "Change Electricity Color");
                    preview.SetElectricityColor(new Color(0.3f, 2.5f, 0.6f, 1.0f));
                    EditorUtility.SetDirty(preview);
                }
                if (GUILayout.Button("過負荷・赤", EditorStyles.miniButton))
                {
                    Undo.RecordObject(preview, "Change Electricity Color");
                    preview.SetElectricityColor(new Color(2.5f, 0.3f, 0.2f, 1.0f));
                    EditorUtility.SetDirty(preview);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Foldouts (Guideline 5: Collapsible Management, Player Guide & Details)

        private void DrawParticleManagementFoldout(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showParticleManagement = EditorGUILayout.Foldout(
                _showParticleManagement,
                "🛠️ パーティクル階層管理・安全解除・アセット削減ガイド",
                true,
                EditorStyles.foldoutHeader);

            if (_showParticleManagement)
            {
                EditorGUILayout.Space(2);

                // 純粋パーティクル系統の管理
                if (preview.UsesPureParticles || preview.PureParticles != null)
                {
                    EditorGUILayout.LabelField("【純粋パーティクル系統 (Pure Particles)】", EditorStyles.miniBoldLabel);
                    CharacterPureParticleElectricity pure = preview.PureParticles;
                    if (pure != null)
                    {
                        string status = $"エミッター稼働状況: リボン({(pure.RibbonEmitter != null ? "有効" : "無")}) / ニードル({(pure.NeedleEmitter != null ? "有効" : "無")}) / オーラ({(pure.AuraEmitter != null ? "有効" : "無")}) / フォーク({(pure.ForkEmitter != null ? "有効" : "無")}) / クリープ({(pure.CrackleEmitter != null ? "有効" : "無")}) / バースト({(pure.BurstEmitter != null ? "有効" : "無")})";
                        EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("💥 純粋バースト発射 (Trigger Burst)", GUILayout.Height(24)))
                        {
                            preview.TriggerParticleBurst(30);
                            SceneView.RepaintAll();
                        }
                        if (GUILayout.Button("🔄 純粋パーティクル再同期", GUILayout.Height(24)))
                        {
                            Undo.RecordObject(preview, "Setup Pure Particles");
                            preview.EnsurePureParticles(recordUndo: true);
                            EditorUtility.SetDirty(preview);
                            SceneView.RepaintAll();
                        }
                        EditorGUILayout.EndHorizontal();

                        Color prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1.0f, 0.7f, 0.4f);
                        if (GUILayout.Button("🗑️ 純粋パーティクルを削除", GUILayout.Height(22)))
                        {
                            Undo.RegisterCompleteObjectUndo(preview, "Delete Pure Particles");
                            preview.DeletePureParticles(recordUndo: true);
                            serializedObject.Update();
                            SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = prevBg;
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("現在、純粋パーティクルコントローラーが未生成です。", MessageType.Info);
                        if (GUILayout.Button("⚡ 純粋パーティクルを自動生成・セットアップ", GUILayout.Height(26)))
                        {
                            Undo.RecordObject(preview, "Ensure Pure Particles");
                            preview.EnsurePureParticles(recordUndo: true);
                            serializedObject.Update();
                            SceneView.RepaintAll();
                        }
                    }

                    EditorGUILayout.Space(3);
                }

                // 軽量パーティクル系統の管理
                if (preview.UsesLightweightParticles || preview.Particles != null)
                {
                    EditorGUILayout.LabelField("【軽量パーティクル系統 (Lightweight Particles)】", EditorStyles.miniBoldLabel);
                    SerializedProperty enableParticlesProp = serializedObject.FindProperty("_enableParticles");
                    EditorGUILayout.PropertyField(enableParticlesProp, new GUIContent("軽量パーティクル有効"));

                    if (enableParticlesProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("_particleRateMultiplier"), new GUIContent("放出量倍率"));

                        CharacterElectricityParticles parts = preview.Particles;
                        if (parts != null)
                        {
                            string status = $"エミッター稼働状況: スパーク({(parts.SparkEmitter != null ? "有効" : "無")}) / グロー({(parts.PlasmaGlowEmitter != null ? "有効" : "無")}) / バースト({(parts.BurstEmitter != null ? "有効" : "無")})";
                            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("💥 スパークバースト発射", GUILayout.Height(24)))
                            {
                                preview.TriggerParticleBurst(25);
                                SceneView.RepaintAll();
                            }
                            if (GUILayout.Button("🔄 軽量パーティクル再同期", GUILayout.Height(24)))
                            {
                                Undo.RecordObject(preview, "Setup Particles");
                                preview.SetupParticles();
                                EditorUtility.SetDirty(preview);
                                SceneView.RepaintAll();
                            }
                            EditorGUILayout.EndHorizontal();

                            Color prevBg = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(1.0f, 0.7f, 0.4f);
                            if (GUILayout.Button("🗑️ 軽量パーティクル削除", GUILayout.Height(22)))
                            {
                                Undo.RegisterCompleteObjectUndo(preview, "Delete Lightweight Particles");
                                preview.DeleteParticlesAndSetShaderOnly(recordUndo: true);
                                serializedObject.Update();
                                SceneView.RepaintAll();
                            }
                            GUI.backgroundColor = prevBg;
                        }
                        else
                        {
                            if (GUILayout.Button("⚡ 軽量パーティクルをセットアップ", GUILayout.Height(26)))
                            {
                                Undo.RecordObject(preview, "Setup Particles");
                                preview.SetupParticles();
                                serializedObject.Update();
                                EditorUtility.SetDirty(preview);
                                SceneView.RepaintAll();
                            }
                        }
                    }

                    EditorGUILayout.Space(3);
                }

                // 完全初期化（安全解除）
                EditorGUILayout.LabelField("【安全解除・完全初期化】", EditorStyles.miniBoldLabel);
                Color prevBg2 = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
                if (GUILayout.Button("⚪ 元のマテリアルに戻して全パーティクル削除 (完全初期化)", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(
                        "完全初期化・安全クリーンアップ",
                        "元のデザイナー制作マテリアルへ復元し、生成された子パーティクルオブジェクトを全削除しますか？\n（キャラクターのボーンやメッシュは保護されます。Undo対応）",
                        "実行する", "キャンセル"))
                    {
                        Undo.RegisterCompleteObjectUndo(preview, "Reset Character Electricity");
                        preview.ResetToOriginalAndCleanup(recordUndo: true);
                        serializedObject.Update();
                        SceneView.RepaintAll();
                    }
                }
                GUI.backgroundColor = prevBg2;

                EditorGUILayout.Space(3);

                // 不要アセット削減ガイド
                EditorGUILayout.LabelField("【不要アセット削減ガイド】", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(
                    "本システムはチームの採用決定後、不要アセットをフォルダごと安全に削除できます。\n\n" +
                    "① 【パーティクルのみ】本採用時:\n" +
                    "  ・シェーダー関連（S_CharacterElectricity.shader, CharacterElectricityInput.hlsl, M_Shiro_Pattern1~20.mat）を完全削除可能。\n" +
                    "  ・スクリプトは CharacterElectricityController (Mode=ParticleOnly) または CharacterPureParticleElectricity で運用。\n\n" +
                    "② 【シェーダーのみ】本採用時:\n" +
                    "  ・パーティクル関連素材（Textures/T_Electricity_*.png, Materials/M_Electricity_*.mat, S_CharacterElectricityParticle.shader）を安全に削除可能。\n\n" +
                    "③ 【ハイブリッド】本採用時:\n" +
                    "  ・シェーダーとパーティクル両方のアセットを維持して本番運用します。",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPlayerIntegrationGuide()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showPlayerGuide = EditorGUILayout.Foldout(
                _showPlayerGuide,
                "🎮 プレイヤー担当者向け 連携コードガイド",
                true,
                EditorStyles.foldoutHeader);

            if (_showPlayerGuide)
            {
                EditorGUILayout.HelpBox(
                    "RacerManagerBase 等から1行で呼び出し可能です。Update() 毎フレームのヒープGCアロケーションは 0 バイトです。",
                    MessageType.Info);

                EditorGUILayout.LabelField("1. イベント購読による連動（推奨・1行）:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(
                    "var electricity = GetComponentInChildren<CharacterElectricityPreview>();\n" +
                    "racer.StunStarted += () => electricity.SetStun(true);\n" +
                    "racer.StunEnded   += () => electricity.SetStun(false);",
                    EditorStyles.textField);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("2. 毎フレーム同期（0 GC なので Update 内でも安心）:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(
                    "electricity.SetStun(racer.IsStunned);",
                    EditorStyles.textField);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("3. スタン残時間に応じた減衰表現（0.0〜1.0）:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(
                    "float normalized = racer.StunRemainingTime / stunDuration;\n" +
                    "electricity.SetStunNormalizedTime(normalized);",
                    EditorStyles.textField);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("4. 属性攻撃に応じた電撃色変更:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(
                    "electricity.SetElectricityColor(new Color(1.3f, 0.35f, 2.5f, 1.0f));",
                    EditorStyles.textField);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("5. 強制火花バースト（タックル衝突・被弾時）:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(
                    "electricity.TriggerParticleBurst(30);",
                    EditorStyles.textField);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDetailsFoldout(CharacterElectricityPreview preview)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showDetails = EditorGUILayout.Foldout(
                _showDetails,
                "⚙️ 内部参照・マテリアルプリセット割り当て (P1〜P20) ＆ 自動テスト",
                true,
                EditorStyles.foldoutHeader);

            if (_showDetails)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetRenderer"), new GUIContent("対象レンダラー"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetMaterialSlot"), new GUIContent("マテリアルスロット番号 (-1=自動)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_particles"), new GUIContent("軽量パーティクルコントローラー"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_pureParticles"), new GUIContent("純粋パーティクルコントローラー"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_hybridParticleSource"), new GUIContent("ハイブリッド連動方式"));

                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_originalMaterialBackup"), new GUIContent("復元用バックアップマテリアル"));

                if (GUILayout.Button("🔄 現在のマテリアルを再取得してバックアップ更新"))
                {
                    Undo.RecordObject(preview, "Refresh Original Material Backup");
                    preview.RefreshOriginalMaterialBackup();
                    EditorUtility.SetDirty(preview);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(4);

                // 20個の放電マテリアルスロットを Foldout に格納してUIを劇的にスリム化
                _showMaterialSlots = EditorGUILayout.Foldout(
                    _showMaterialSlots,
                    "📁 プリセット放電マテリアル詳細参照 (全20スロット)",
                    true);

                if (_showMaterialSlots)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern1Arc"), new GUIContent("P1 (Arc)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern2Pulse"), new GUIContent("P2 (Pulse)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern3Plasma"), new GUIContent("P3 (Plasma)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern4Gold"), new GUIContent("P4 (Gold)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern5CleanStreak"), new GUIContent("P5 (CleanStreak)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern6FresnelRim"), new GUIContent("P6 (FresnelRim)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern7ShortSparks"), new GUIContent("P7 (ShortSparks)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern8CyberCircuit"), new GUIContent("P8 (CyberCircuit)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern9MultiChain"), new GUIContent("P9 (MultiChain)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern10MicroCrackle"), new GUIContent("P10 (MicroCrackle)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern11HeavyArcBurst"), new GUIContent("P11 (HeavyArcBurst)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern12ThermalAfterglow"), new GUIContent("P12 (ThermalAfterglow)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern13Anatomical"), new GUIContent("P13 (Anatomical)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern14Lichtenberg"), new GUIContent("P14 (Lichtenberg)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern15SteppedLeader"), new GUIContent("P15 (SteppedLeader)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern16ImpactVector"), new GUIContent("P16 (ImpactVector)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern17Shockwave"), new GUIContent("P17 (Shockwave)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern18MagneticWhip"), new GUIContent("P18 (MagneticWhip)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern19ArmorSeam"), new GUIContent("P19 (ArmorSeam)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_matPattern20CriticalStrobe"), new GUIContent("P20 (CriticalStrobe)"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("🧪 回帰テスト実行 (Automated Tests)", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🧪 全20パターン シェーダーテスト", GUILayout.Height(26)))
                {
                    CharacterElectricityTest.RunTests();
                }
                if (GUILayout.Button("🧪 全8パターン 純粋パーティクルテスト", GUILayout.Height(26)))
                {
                    CharacterPureParticleElectricityTest.RunTests();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
