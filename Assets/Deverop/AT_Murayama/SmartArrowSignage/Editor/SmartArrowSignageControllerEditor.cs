using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SmartArrowSignageController 用のインスペクター拡張。
/// ・チーム規約遵守：Toolsメニュー（[MenuItem]）は一切追加せず、全操作はインスペクターGUIで完結。
/// ・8方向クイックコンパス（0°, 45°, 90°, 135°, 180°, -135°, -90°, -45°）
/// ・8種矢印スタイル切替（鋭角レーシング、太めサイン、二重シェブロン、三重シェブロン、ネオン管、クアンタム、ハザード警戒、カーブ案内）
/// ・6種設置スタイル切替（スクリーン単体、壁面マウント、天井吊り下げ、フロアスタンド、エアロフレーム、反重力パイロン）
/// ・3種背景モード切替（フロストガラス、純粋空中ホログラム、ミニマルガラス）
/// ・7種HDR発光カラープリセット（シアン、ライム、アンバー、クリムゾン、ホワイト、ゴールド、バイオレット）
/// ・6種アスペクト比プリセット（16:9, 21:9, 32:9, 4:3, 1:1, 9:16）
/// ・マテリアル別名保存（Save As）
/// ・6種プレハブ・8種マテリアル・6種メッシュの一括自動生成
/// ・シーン内展示ショーケース（--- SmartArrowSignage Showcase ---）のワンクリック配置
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(SmartArrowSignageController))]
public class SmartArrowSignageControllerEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/SmartArrowSignage";
    private const string ShadersFolder   = BaseFolder + "/Shaders";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    private static bool _showAssetTools = false;

    static SmartArrowSignageControllerEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh screenMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/SmartArrow_Screen_RoundedRect.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || screenMesh == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        SmartArrowSignageController comp = (SmartArrowSignageController)target;

        EditorGUILayout.Space(4f);

        // ─────────────────────────────────────────────────────────────
        // 1. ワンタッチ 8方向クイックコンパス
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🧭【ワンタッチ方向指定】 8方向クイックコンパス", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAngleButton(comp, "↖ 135°\n(左上)", 135f);
                DrawAngleButton(comp, "↑ 90°\n(真上)", 90f);
                DrawAngleButton(comp, "↗ 45°\n(右上)", 45f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAngleButton(comp, "← 180°\n(左)", 180f);
                DrawAngleButton(comp, "🎯 リセット\n(0°)", 0f, true);
                DrawAngleButton(comp, "→ 0°\n(右)", 0f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAngleButton(comp, "↙ -135°\n(左下)", -135f);
                DrawAngleButton(comp, "↓ -90°\n(真下)", -90f);
                DrawAngleButton(comp, "↘ -45°\n(右下)", -45f);
            }

            EditorGUILayout.Space(2f);
            EditorGUI.BeginChangeCheck();
            float newAngle = EditorGUILayout.Slider("微調整 角度 (Slider)", comp.ArrowAngle, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Adjust Arrow Angle");
                comp.SetArrowAngle(newAngle);
                EditorUtility.SetDirty(comp);
            }
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 2. 矢印スタイル・パターン切替（8種）
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("📐【矢印スタイル・幾何学パターン】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPatternButton(comp, "⚡ 鋭角レーシング", SmartArrowSignageController.ArrowPattern.SharpRacing);
                DrawPatternButton(comp, "🛑 太めサイン", SmartArrowSignageController.ArrowPattern.BoldSign);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPatternButton(comp, "⏩ 二重シェブロン", SmartArrowSignageController.ArrowPattern.DoubleChevron);
                DrawPatternButton(comp, "⏭️ 三重シェブロン", SmartArrowSignageController.ArrowPattern.TripleChevron);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPatternButton(comp, "💡 ネオン管中空", SmartArrowSignageController.ArrowPattern.NeonTube);
                DrawPatternButton(comp, "💠 クアンタム", SmartArrowSignageController.ArrowPattern.QuantumLattice);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPatternButton(comp, "⚠️ ハザード警戒", SmartArrowSignageController.ArrowPattern.HazardStripes);
                DrawPatternButton(comp, "↩️ カーブ案内", SmartArrowSignageController.ArrowPattern.CurvedSpeed);
            }
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 3. アニメーション方式 ＆ リピート数
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🎬【アニメーション方式】", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAnimButton(comp, "🌊 フロー", SmartArrowSignageController.AnimationMode.Flowing);
            DrawAnimButton(comp, "⚡ 点滅", SmartArrowSignageController.AnimationMode.Blinking);
            DrawAnimButton(comp, "💓 パルス", SmartArrowSignageController.AnimationMode.Pulse);
            DrawAnimButton(comp, "📏 スキャン", SmartArrowSignageController.AnimationMode.ScanlineFlow);
            DrawAnimButton(comp, "⏸️ 静止", SmartArrowSignageController.AnimationMode.Static);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("🔢【連続個数（シェブロンリピート）】", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawRepeatButton(comp, "1個", 1);
            DrawRepeatButton(comp, "2連", 2);
            DrawRepeatButton(comp, "3連", 3);
            DrawRepeatButton(comp, "4連", 4);
            DrawRepeatButton(comp, "5連", 5);
            DrawRepeatButton(comp, "6連", 6);
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 4. 背景モード切替（全9種：空中浮遊・金具無し対応拡張）
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🔮【背景表現モード】(空中浮遊・金具なしスタイル対応 全9種)", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("── 🪟 ガラス・透過系 ──", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSignModeButton(comp, "🪟 フロストガラス\n(物理透過・光沢)", SmartArrowSignageController.SignMode.FrostedGlass, new Color(0.75f, 0.95f, 1f));
                DrawSignModeButton(comp, "💎 ミニマルガラス\n(軽量アクリル)", SmartArrowSignageController.SignMode.MinimalGlass, new Color(0.9f, 0.95f, 0.95f));
                DrawSignModeButton(comp, "🕶️ ダークエアロ\n(直射日光・高対比)", SmartArrowSignageController.SignMode.DarkAeroVisor, new Color(0.65f, 0.70f, 0.80f));
            }
            EditorGUILayout.LabelField("── ✨ 空中純浮遊ホログラム系 ──", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSignModeButton(comp, "✨ 空中ホログラム\n(ガラスなし純浮遊)", SmartArrowSignageController.SignMode.PureHologram, new Color(0.85f, 0.85f, 1f));
                DrawSignModeButton(comp, "🌐 サイバーグリッド\n(走査線・方眼メッシュ)", SmartArrowSignageController.SignMode.CyberGridHolo, new Color(0.4f, 0.9f, 1f));
                DrawSignModeButton(comp, "✨ ネオンエッジ\n(外周発光・クリア)", SmartArrowSignageController.SignMode.NeonWireframeHolo, new Color(0.35f, 0.95f, 0.85f));
            }
            EditorGUILayout.LabelField("── 🛡️ 反重力エネルギー・フィールド系 ──", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSignModeButton(comp, "🛡️ シールド境界\n(反重力ハニカム波)", SmartArrowSignageController.SignMode.EnergyShield, new Color(0.4f, 1f, 0.8f));
                DrawSignModeButton(comp, "✨ スターダスト\n(空中浮遊光粒子)", SmartArrowSignageController.SignMode.StarDustMatrix, new Color(0.8f, 0.7f, 1f));
                DrawSignModeButton(comp, "🌈 オーロラプリズム\n(虹色偏光クリスタル)", SmartArrowSignageController.SignMode.AuroraPrism, new Color(1f, 0.75f, 0.95f));
            }

            EditorGUILayout.Space(2f);
            string modeDesc = comp.Mode switch
            {
                SmartArrowSignageController.SignMode.FrostedGlass => "🪟 物理透過フロストガラス: 13タップScene Colorブラー、光学的屈折、太陽光鏡面光沢、動的反射光スイープ、面取りベベル発光を完全搭載した高級ガラス板。",
                SmartArrowSignageController.SignMode.MinimalGlass => "💎 ミニマル半透明ガラス: 背景ブラーを排した超軽量クリアアクリル・ポリカーボネート調。シンプルでクリアな視界を確保。",
                SmartArrowSignageController.SignMode.DarkAeroVisor => "🕶️ ダークエアロ・バイザー: 偏光ダークスモーク・直射日光耐性。白雲や青空、強烈な太陽光下でも矢印サインが100%クッキリ映える極限高コントラスト。",
                SmartArrowSignageController.SignMode.PureHologram => "✨ 純粋空中ホログラム: 背景ガラス板が一切存在せず、空気中に純粋な光エネルギー矢印のみが浮かび上がる完全空中浮遊サイン。",
                SmartArrowSignageController.SignMode.CyberGridHolo => "🌐 サイバーグリッド・ホロ: 空中マトリクス板。微細な走査線とデジタル方眼メッシュが浮かび、金具が無くても空間に看板領域がクッキリ映えます。",
                SmartArrowSignageController.SignMode.NeonWireframeHolo => "✨ ネオンエッジ・ホロフレーム: 外周境界のみが多重ネオンパルスで鮮やかに発光し、中央は完全クリア。背景景色を遮らずに看板領域を美しく明示。",
                SmartArrowSignageController.SignMode.EnergyShield => "🛡️ 反重力エネルギーシールド: 六角形ハニカム格子と脈動プラズマ波境界によるフォースフィールド。空中に反重力で浮遊している設定を視覚化。",
                SmartArrowSignageController.SignMode.StarDustMatrix => "✨ スターダスト・微粒子マトリクス: 空間内部に微細な光のナノダスト・データ微粒子がキラキラと漂う高密度SF浮遊フィールド。",
                SmartArrowSignageController.SignMode.AuroraPrism => "🌈 オーロラプリズム・光彩ガラス: 見る角度や視線ベクトルによって虹色に揺らめく未来型偏光クリスタルガラス。",
                _ => ""
            };
            EditorGUILayout.HelpBox(modeDesc, MessageType.None);
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 5. 設置スタイル切替（6種）
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🏗️【設置スタイル・金具マウント】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStyleButton(comp, "① スクリーン単体\n(純浮遊)", SmartArrowSignageController.MountingStyle.ScreenOnly);
                DrawStyleButton(comp, "② 壁面マウント\n(ビル・トンネル金具)", SmartArrowSignageController.MountingStyle.WallMount);
                DrawStyleButton(comp, "③ 天井吊り下げ\n(高架・トラス懸架)", SmartArrowSignageController.MountingStyle.CeilingHanger);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStyleButton(comp, "④ フロアスタンド\n(地面・路面支柱)", SmartArrowSignageController.MountingStyle.FloorStand);
                DrawStyleButton(comp, "⑤ エアロフレーム\n(白亜ベゼル枠)", SmartArrowSignageController.MountingStyle.AeroFramed);
                DrawStyleButton(comp, "⑥ 反重力パイロン\n(上下エネルギーポッド)", SmartArrowSignageController.MountingStyle.FloatingPylons);
            }
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 6. カラーパレット（7種 HDR Bloom連動）
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🎨【カラーパレット】(HDR Bloom連動)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawColorButton(comp, "Cyan", SmartArrowSignageController.ArrowColorPreset.NeonCyan, new Color(0.2f, 0.92f, 1f));
            DrawColorButton(comp, "Lime", SmartArrowSignageController.ArrowColorPreset.SpeedLime, new Color(0.35f, 1f, 0.6f));
            DrawColorButton(comp, "Amber", SmartArrowSignageController.ArrowColorPreset.CautionAmber, new Color(1f, 0.65f, 0.1f));
            DrawColorButton(comp, "Crimson", SmartArrowSignageController.ArrowColorPreset.DangerCrimson, new Color(1f, 0.2f, 0.3f));
            DrawColorButton(comp, "White", SmartArrowSignageController.ArrowColorPreset.PureWhite, Color.white);
            DrawColorButton(comp, "Gold", SmartArrowSignageController.ArrowColorPreset.CyberGold, new Color(1f, 0.85f, 0.3f));
            DrawColorButton(comp, "Violet", SmartArrowSignageController.ArrowColorPreset.VioletQuantum, new Color(0.85f, 0.4f, 1f));
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 7. アスペクト比プリセット
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("📐【アスペクト比プリセット】", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAspectButton(comp, "16:9\n(ワイド)", SmartArrowSignageController.AspectRatioPreset.Ratio_16_9);
            DrawAspectButton(comp, "21:9\n(超ワイド)", SmartArrowSignageController.AspectRatioPreset.Ratio_21_9);
            DrawAspectButton(comp, "32:9\n(ロング帯)", SmartArrowSignageController.AspectRatioPreset.Ratio_32_9);
            DrawAspectButton(comp, "4:3\n(HUD)", SmartArrowSignageController.AspectRatioPreset.Ratio_4_3);
            DrawAspectButton(comp, "1:1\n(スクエア)", SmartArrowSignageController.AspectRatioPreset.Ratio_1_1);
            DrawAspectButton(comp, "9:16\n(縦サイネージ)", SmartArrowSignageController.AspectRatioPreset.Ratio_9_16);
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 8. 外枠・金具 ＆ HUD装飾の表示切替
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("🔩【外枠・金具マウント ＆ HUD装飾表示切替】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("── 🏗️ 3D金具・外枠 (Physical Mount) ──", EditorStyles.miniLabel);
            if (comp.Style == SmartArrowSignageController.MountingStyle.ScreenOnly)
            {
                EditorGUILayout.HelpBox("💡 現在「① スクリーン単体 (純浮遊)」設定のため、3D金具・外枠は完全非表示（金具無し）です。\n空中に金具無しで浮かべる配置パターンに最適化されています。", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                    if (GUILayout.Button("🏗️ 壁面マウント金具を装着", GUILayout.Height(26f)))
                    {
                        Undo.RecordObject(comp, "Switch to Wall Mount");
                        comp.SetMountingStyle(SmartArrowSignageController.MountingStyle.WallMount);
                        EditorUtility.SetDirty(comp);
                    }
                    if (GUILayout.Button("🔲 エアロフレーム枠を装着", GUILayout.Height(26f)))
                    {
                        Undo.RecordObject(comp, "Switch to Aero Framed");
                        comp.SetMountingStyle(SmartArrowSignageController.MountingStyle.AeroFramed);
                        EditorUtility.SetDirty(comp);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = comp.ShowFrame ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.8f, 0.8f);
                    string frameText = comp.ShowFrame ? "3D枠・金具・パイロン: [ ON ] (クリックで非表示)" : "3D枠・金具・パイロン: [ OFF ] (クリックで表示)";
                    if (GUILayout.Button(frameText, GUILayout.Height(28f)))
                    {
                        Undo.RecordObject(comp, "Toggle Sign Frame");
                        comp.SetShowFrame(!comp.ShowFrame);
                        EditorUtility.SetDirty(comp);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("── 🔲 2D HUD装飾・四隅コーナー金具 (Screen Graphics) ──", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = comp.ShowHUDDecorations ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.85f, 0.7f);
                string hudText = comp.ShowHUDDecorations
                    ? "四隅コーナー留め金具・HUD装飾: [ ON ] (クリックで非表示)"
                    : "四隅コーナー留め金具・HUD装飾: [ OFF ] (金具なし純粋スクリーン)";
                if (GUILayout.Button(hudText, GUILayout.Height(28f)))
                {
                    Undo.RecordObject(comp, "Toggle HUD Corner Decorations");
                    comp.SetShowHUDDecorations(!comp.ShowHUDDecorations);
                    EditorUtility.SetDirty(comp);
                }
                GUI.backgroundColor = Color.white;
            }
            if (comp.Style == SmartArrowSignageController.MountingStyle.ScreenOnly && comp.ShowHUDDecorations)
            {
                EditorGUILayout.HelpBox("💡 空中に金具無しで純浮遊させる場合、四隅コーナー金具装飾を [ OFF ] にすることで、画面内の留め金具風グラフィックも消去され、完全な純浮遊空中スクリーンになります。", MessageType.None);
            }
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 光の反射・スイープ演出のタイミング
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("✨【光の反射・スイープ演出のタイミング】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUI.BeginChangeCheck();
            float newPhase = EditorGUILayout.Slider("反射タイミング位相 (0〜1)", comp.SweepPhaseOffset, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Change Sweep Phase Offset");
                comp.SweepPhaseOffset = newPhase;
                EditorUtility.SetDirty(comp);
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            bool newAutoStagger = EditorGUILayout.ToggleLeft("📍 設置位置に応じた自動タイミング分散 (Auto Stagger by Position)", comp.AutoStaggerPhaseByPosition);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Toggle Auto Stagger Phase");
                comp.AutoStaggerPhaseByPosition = newAutoStagger;
                EditorUtility.SetDirty(comp);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🎲 ランダムな位相を設定", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(comp, "Randomize Sweep Phase");
                    comp.RandomizeSweepPhase();
                    EditorUtility.SetDirty(comp);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("🔄 0にリセット", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(comp, "Reset Sweep Phase");
                    comp.SweepPhaseOffset = 0f;
                    EditorUtility.SetDirty(comp);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.HelpBox("複数看板を配置した際に光の反射が同時に流れないよう、看板ごとにタイミングをずらせます（位置連動ONなら配置するだけで自動分散）。", MessageType.None);
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 9. マテリアル別名保存 (Save As)
        // ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("💾【マテリアル別名保存 (Save As)】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (comp.CustomMaterial != null)
            {
                EditorGUILayout.HelpBox($"現在、別名保存したカスタムマテリアルが割り当てられています:\n{comp.CustomMaterial.name}", MessageType.Info);
                GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
                if (GUILayout.Button("🔄 カスタムマテリアル解除（インスペクター調整に戻る）", GUILayout.Height(24f)))
                {
                    Undo.RecordObject(comp, "Clear Custom Material");
                    comp.ClearCustomMaterial();
                    EditorUtility.SetDirty(comp);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(4f);
            }
            else
            {
                EditorGUILayout.HelpBox("インスペクターで調整した矢印スタイル、角度、発光色、フロスト質感を新規マテリアル（.mat）として保存し、この看板に割り当てます。", MessageType.None);
            }

            string defaultPlaceholder = $"M_SmartArrow_{comp.gameObject.name}";
            comp.CustomSaveName = EditorGUILayout.TextField("保存ファイル名", comp.CustomSaveName);
            if (string.IsNullOrWhiteSpace(comp.CustomSaveName))
            {
                EditorGUILayout.LabelField($"※空欄の場合は「{defaultPlaceholder}.mat」で保存されます", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.35f, 0.95f, 0.6f);
                if (GUILayout.Button("💾 別名で新規保存＆適用", GUILayout.Height(28f)))
                {
                    SaveAsCustomMaterial(comp, false);
                }

                GUI.backgroundColor = new Color(0.85f, 0.95f, 1f);
                if (GUILayout.Button("📂 保存先を選んで別名保存...", GUILayout.Height(28f)))
                {
                    SaveAsCustomMaterial(comp, true);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6f);

        // ─────────────────────────────────────────────────────────────
        // 10. アセット一括生成 ＆ 展示ショーケースセクション
        // ─────────────────────────────────────────────────────────────
        _showAssetTools = EditorGUILayout.Foldout(_showAssetTools, "🛠️【セットアップ・アセット生成 & 展示ショーケース】", true);
        if (_showAssetTools)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.backgroundColor = new Color(0.75f, 0.95f, 1f);
                if (GUILayout.Button("✨ シーン内に展示ショーケース（--- SmartArrowSignage Showcase ---）を配置", GUILayout.Height(30f)))
                {
                    SpawnSmartArrowShowcase(comp.transform.position);
                }

                EditorGUILayout.Space(2f);
                GUI.backgroundColor = new Color(0.85f, 1f, 0.8f);
                if (GUILayout.Button("📁 6種プレハブ・8種マテリアル・6種メッシュを一括再生成 / 保存", GUILayout.Height(26f)))
                {
                    GenerateAllAssets(ping: true);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            comp.UpdateSignboard();
            EditorUtility.SetDirty(comp);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("マテリアル・表示を再適用", GUILayout.Height(26f)))
        {
            comp.UpdateSignboard();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawAngleButton(SmartArrowSignageController comp, string label, float angle, bool isCenter = false)
    {
        bool isSelected = !isCenter && Mathf.Approximately(comp.ArrowAngle, angle);
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.9f, 1f) : (isCenter ? new Color(0.92f, 0.94f, 0.98f) : Color.white);
        if (GUILayout.Button(label, GUILayout.Height(32f)))
        {
            Undo.RecordObject(comp, "Change Arrow Angle");
            comp.SetArrowAngle(angle);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawPatternButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.ArrowPattern pattern)
    {
        bool isSelected = comp.Pattern == pattern;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.95f, 0.7f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Undo.RecordObject(comp, "Change Arrow Pattern");
            comp.SetArrowPattern(pattern);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawAnimButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.AnimationMode anim)
    {
        bool isSelected = comp.Anim == anim;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(26f)))
        {
            Undo.RecordObject(comp, "Change Animation Mode");
            comp.SetAnimationMode(anim);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawRepeatButton(SmartArrowSignageController comp, string label, int count)
    {
        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.FindProperty("_arrowRepeat");
        bool isSelected = prop != null && prop.intValue == count;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
        {
            Undo.RecordObject(comp, "Change Arrow Repeat Count");
            comp.SetArrowRepeat(count);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawSignModeButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.SignMode mode, Color btnColor)
    {
        bool isSelected = comp.Mode == mode;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.95f, 0.7f) : btnColor;
        if (GUILayout.Button(label, GUILayout.Height(34f)))
        {
            Undo.RecordObject(comp, "Change Sign Mode");
            comp.SetSignMode(mode);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawStyleButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.MountingStyle style)
    {
        bool isSelected = comp.Style == style;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(34f)))
        {
            Undo.RecordObject(comp, "Change Mounting Style");
            comp.SetMountingStyle(style);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawColorButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.ArrowColorPreset preset, Color btnColor)
    {
        GUI.backgroundColor = btnColor;
        if (GUILayout.Button(label, GUILayout.Height(26f)))
        {
            Undo.RecordObject(comp, "Apply Arrow Color Preset");
            comp.ApplyColorPreset(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawAspectButton(SmartArrowSignageController comp, string label, SmartArrowSignageController.AspectRatioPreset preset)
    {
        bool isSelected = comp.AspectRatio == preset;
        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(30f)))
        {
            Undo.RecordObject(comp, "Change Aspect Ratio");
            comp.SetAspectRatio(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SaveAsCustomMaterial(SmartArrowSignageController comp, bool useSavePanel)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        string savePath = "";
        string fileWithoutExt = string.IsNullOrWhiteSpace(comp.CustomSaveName)
            ? $"M_SmartArrow_{comp.gameObject.name}"
            : comp.CustomSaveName.Trim();

        if (fileWithoutExt.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
        {
            fileWithoutExt = Path.GetFileNameWithoutExtension(fileWithoutExt);
        }

        if (useSavePanel)
        {
            savePath = EditorUtility.SaveFilePanelInProject(
                "スマート方向サインマテリアルを別名保存",
                $"{fileWithoutExt}.mat",
                "mat",
                "マテリアルの保存先を指定してください",
                MaterialsFolder);

            if (string.IsNullOrEmpty(savePath)) return;
            fileWithoutExt = Path.GetFileNameWithoutExtension(savePath);
        }
        else
        {
            savePath = $"{MaterialsFolder}/{fileWithoutExt}.mat";
        }

        if (!useSavePanel && File.Exists(savePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "マテリアル上書き確認",
                $"'{fileWithoutExt}.mat' は既に存在します。\n上書き保存してもよろしいですか？",
                "上書きする",
                "キャンセル");
            if (!overwrite) return;
        }

        Material newMat = comp.CreateConfiguredMaterial();
        if (newMat == null)
        {
            EditorUtility.DisplayDialog("エラー", "マテリアルの生成に失敗しました。シェーダーが見つかりません。", "OK");
            return;
        }

        AssetDatabase.CreateAsset(newMat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material loadedMat = AssetDatabase.LoadAssetAtPath<Material>(savePath);
        if (loadedMat != null)
        {
            Undo.RecordObject(comp, "Assign Custom SmartArrow Material");
            comp.CustomMaterial = loadedMat;
            comp.CustomSaveName = fileWithoutExt;
            comp.UpdateSignboard();
            EditorUtility.SetDirty(comp);
            EditorGUIUtility.PingObject(loadedMat);
            Debug.Log($"<color=#70D0FF>[SmartArrowSignage]</color> マテリアルを保存し適用しました: {savePath}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // アセット一括自動生成（プレハブ・マテリアル・メッシュ）
    // ─────────────────────────────────────────────────────────────

    public static void GenerateAllAssets(bool ping = true)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(ShadersFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        EnsureFolder(PrefabsFolder);

        // 1. メッシュの生成・保存（全6種）
        Mesh screenMesh  = SmartArrowSignageController.CreateRoundedScreenMesh(1.0f, 1.0f, 0.08f, 0.02f);
        Mesh frameMesh   = SmartArrowSignageController.CreateAeroFrameMesh(1.0f, 1.0f, 0.08f, 0.04f, 0.035f);
        Mesh mountMesh   = SmartArrowSignageController.CreateWallMountMesh(1.0f, 1.0f);
        Mesh hangerMesh  = SmartArrowSignageController.CreateCeilingHangerMesh(1.0f, 1.0f);
        Mesh standMesh   = SmartArrowSignageController.CreateFloorStandMesh(1.0f, 1.0f);
        Mesh pylonsMesh  = SmartArrowSignageController.CreateFloatingPylonsMesh(1.0f, 1.0f);

        SaveOrUpdateMesh(screenMesh,  $"{MeshesFolder}/SmartArrow_Screen_RoundedRect.asset");
        SaveOrUpdateMesh(frameMesh,   $"{MeshesFolder}/SmartArrow_AeroFrame.asset");
        SaveOrUpdateMesh(mountMesh,   $"{MeshesFolder}/SmartArrow_WallMount.asset");
        SaveOrUpdateMesh(hangerMesh,  $"{MeshesFolder}/SmartArrow_CeilingHanger.asset");
        SaveOrUpdateMesh(standMesh,   $"{MeshesFolder}/SmartArrow_FloorStand.asset");
        SaveOrUpdateMesh(pylonsMesh,  $"{MeshesFolder}/SmartArrow_FloatingPylons.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh persistentScreen = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_Screen_RoundedRect.asset");
        Mesh persistentFrame  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_AeroFrame.asset");
        Mesh persistentMount  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_WallMount.asset");
        Mesh persistentHanger = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_CeilingHanger.asset");
        Mesh persistentStand  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_FloorStand.asset");
        Mesh persistentPylons = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartArrow_FloatingPylons.asset");

        // 2. マテリアルの生成・保存（全8種）
        Shader arrowShader = Shader.Find("Custom/SmartArrowSignage/S_SmartArrowSignage");
        Shader litShader   = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material matNeonCyan     = CreateOrUpdateArrowMaterial("M_SmartArrow_NeonCyan", arrowShader, 0, 0, new Color(0.20f, 0.92f, 1.0f), new Color(1.0f, 0.45f, 0.1f), 2.8f, 0f);
        Material matSpeedLime    = CreateOrUpdateArrowMaterial("M_SmartArrow_SpeedLime", arrowShader, 1, 0, new Color(0.35f, 1.0f, 0.60f), new Color(1.0f, 0.90f, 0.20f), 2.6f, 0f);
        Material matCautionAmber = CreateOrUpdateArrowMaterial("M_SmartArrow_CautionAmber", arrowShader, 6, 0, new Color(1.0f, 0.65f, 0.05f), new Color(1.0f, 0.20f, 0.05f), 3.0f, 0f);
        Material matHoloGold     = CreateOrUpdateArrowMaterial("M_SmartArrow_HoloGold", arrowShader, 7, 1, new Color(1.0f, 0.82f, 0.25f), new Color(1.0f, 0.40f, 0.05f), 3.2f, 0f);
        Material matTripleViolet = CreateOrUpdateArrowMaterial("M_SmartArrow_TripleViolet", arrowShader, 3, 0, new Color(0.85f, 0.35f, 1.0f), new Color(0.15f, 0.90f, 1.0f), 2.8f, 0f);
        Material matNeonTube     = CreateOrUpdateArrowMaterial("M_SmartArrow_NeonTube", arrowShader, 4, 2, Color.white, new Color(0.60f, 0.85f, 1.0f), 2.5f, 0f);

        Material matFrameWhite   = CreateOrUpdateFrameMaterial("M_SmartArrow_Frame_White", litShader, new Color(0.95f, 0.97f, 1.0f), 0.1f, 0.9f);
        Material matFrameDark    = CreateOrUpdateFrameMaterial("M_SmartArrow_Frame_Dark", litShader, new Color(0.15f, 0.17f, 0.20f), 0.7f, 0.75f);

        // 3. プレハブの生成・保存（全6種・光反射スイープの位相差をデフォで分散）
        CreateArrowPrefab(
            "Prefab_SmartArrow_WallMount.prefab",
            SmartArrowSignageController.MountingStyle.WallMount,
            SmartArrowSignageController.SignMode.FrostedGlass,
            SmartArrowSignageController.ArrowPattern.SharpRacing,
            SmartArrowSignageController.AspectRatioPreset.Ratio_16_9,
            1,
            SmartArrowSignageController.ArrowColorPreset.NeonCyan,
            persistentScreen, persistentMount, matNeonCyan, matFrameDark, 8.0f, 4.5f,
            0.00f);

        CreateArrowPrefab(
            "Prefab_SmartArrow_CeilingHanger.prefab",
            SmartArrowSignageController.MountingStyle.CeilingHanger,
            SmartArrowSignageController.SignMode.FrostedGlass,
            SmartArrowSignageController.ArrowPattern.BoldSign,
            SmartArrowSignageController.AspectRatioPreset.Ratio_21_9,
            1,
            SmartArrowSignageController.ArrowColorPreset.SpeedLime,
            persistentScreen, persistentHanger, matSpeedLime, matFrameDark, 10.5f, 4.5f,
            0.17f);

        CreateArrowPrefab(
            "Prefab_SmartArrow_FloorStand.prefab",
            SmartArrowSignageController.MountingStyle.FloorStand,
            SmartArrowSignageController.SignMode.FrostedGlass,
            SmartArrowSignageController.ArrowPattern.HazardStripes,
            SmartArrowSignageController.AspectRatioPreset.Ratio_16_9,
            1,
            SmartArrowSignageController.ArrowColorPreset.CautionAmber,
            persistentScreen, persistentStand, matCautionAmber, matFrameDark, 8.0f, 4.5f,
            0.34f);

        CreateArrowPrefab(
            "Prefab_SmartArrow_Framed.prefab",
            SmartArrowSignageController.MountingStyle.AeroFramed,
            SmartArrowSignageController.SignMode.FrostedGlass,
            SmartArrowSignageController.ArrowPattern.TripleChevron,
            SmartArrowSignageController.AspectRatioPreset.Ratio_32_9,
            3,
            SmartArrowSignageController.ArrowColorPreset.VioletQuantum,
            persistentScreen, persistentFrame, matTripleViolet, matFrameWhite, 16.0f, 4.5f,
            0.51f);

        CreateArrowPrefab(
            "Prefab_SmartArrow_FloatingHolo.prefab",
            SmartArrowSignageController.MountingStyle.FloatingPylons,
            SmartArrowSignageController.SignMode.PureHologram,
            SmartArrowSignageController.ArrowPattern.CurvedSpeed,
            SmartArrowSignageController.AspectRatioPreset.Ratio_1_1,
            1,
            SmartArrowSignageController.ArrowColorPreset.CyberGold,
            persistentScreen, persistentPylons, matHoloGold, matFrameWhite, 5.0f, 5.0f,
            0.68f);

        CreateArrowPrefab(
            "Prefab_SmartArrow_ScreenOnly.prefab",
            SmartArrowSignageController.MountingStyle.ScreenOnly,
            SmartArrowSignageController.SignMode.FrostedGlass,
            SmartArrowSignageController.ArrowPattern.SharpRacing,
            SmartArrowSignageController.AspectRatioPreset.Ratio_16_9,
            1,
            SmartArrowSignageController.ArrowColorPreset.NeonCyan,
            persistentScreen, null, matNeonCyan, null, 8.0f, 4.5f,
            0.85f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject samplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_SmartArrow_WallMount.prefab");
            if (samplePrefab != null) EditorGUIUtility.PingObject(samplePrefab);
        }
        Debug.Log("<color=#70D0FF>[SmartArrowSignage]</color> 全アセット（プレハブ6種・マテリアル8種・メッシュ6種）を正常に生成・保存しました。");
    }

    private static void SaveOrUpdateMesh(Mesh newMesh, string assetPath)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(newMesh, assetPath);
        }
        else
        {
            existing.Clear();
            existing.SetVertices(newMesh.vertices);
            existing.SetNormals(newMesh.normals);
            existing.SetUVs(0, newMesh.uv);
            existing.SetTriangles(newMesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
        }
    }

    private static Material CreateOrUpdateArrowMaterial(string matName, Shader shader, int pattern, int signMode, Color colPrimary, Color colSecondary, float emission, float angle)
    {
        string path = $"{MaterialsFolder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetFloat("_SignMode", (float)signMode);
        mat.SetFloat("_ArrowPattern", (float)pattern);
        mat.SetFloat("_ArrowAngle", angle);
        mat.SetFloat("_ArrowScale", 1.0f);
        mat.SetFloat("_ArrowAspect", 1.0f);
        mat.SetFloat("_ArrowRepeat", (pattern == 3) ? 3f : ((pattern == 2) ? 2f : 1f));
        mat.SetFloat("_ArrowSpacing", 0.35f);
        mat.SetVector("_ArrowOffset", Vector4.zero);
        mat.SetFloat("_AnimMode", 0f);
        mat.SetFloat("_AnimSpeed", 2.5f);

        mat.SetColor("_ArrowColor", colPrimary);
        mat.SetColor("_SecondaryColor", colSecondary);
        mat.SetFloat("_EmissionMultiplier", emission);
        mat.SetColor("_CoreColor", Color.white);
        mat.SetFloat("_CoreIntensity", 2.0f);
        mat.SetFloat("_GlowWidth", 0.06f);
        mat.SetFloat("_GlowIntensity", 1.2f);
        mat.SetFloat("_OutlineWidth", 0.04f);
        mat.SetFloat("_StripeDensity", 22.0f);

        mat.SetFloat("_ShowHUDDecorations", 1.0f);
        mat.SetColor("_HUDColor", new Color(colPrimary.r, colPrimary.g, colPrimary.b, 0.7f));

        mat.SetFloat("_UseSceneBlur", 1.0f);
        mat.SetFloat("_BlurRadius", 0.015f);
        mat.SetFloat("_RefractionAmount", 0.008f);
        mat.SetFloat("_RoughnessNoise", 0.35f);
        mat.SetColor("_GlassTint", new Color(0.92f, 0.97f, 1.0f, 0.22f));

        mat.SetFloat("_SpecularIntensity", 1.3f);
        mat.SetFloat("_SpecularSmoothness", 64.0f);
        mat.SetColor("_FresnelColor", new Color(0.75f, 0.95f, 1.0f, 0.85f));
        mat.SetFloat("_FresnelPower", 3.2f);
        mat.SetColor("_EdgeGlowColor", new Color(0.85f, 0.97f, 1.0f, 0.9f));
        mat.SetFloat("_EdgeGlowIntensity", 1.4f);
        mat.SetFloat("_SweepSpeed", 0.4f);
        mat.SetFloat("_SweepIntensity", 0.35f);
        mat.SetFloat("_AspectRatio", 1.7777f);

        // レンダーステート
        if (signMode == 1) // PureHologram
        {
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        else
        {
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        mat.SetFloat("_Cull", 2f); // 2 = Cull Back
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_ZTest", 4f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateOrUpdateFrameMaterial(string matName, Shader shader, Color baseColor, float metallic, float smoothness)
    {
        string path = $"{MaterialsFolder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CreateArrowPrefab(
        string fileName,
        SmartArrowSignageController.MountingStyle style,
        SmartArrowSignageController.SignMode signMode,
        SmartArrowSignageController.ArrowPattern pattern,
        SmartArrowSignageController.AspectRatioPreset aspectPreset,
        int repeatCount,
        SmartArrowSignageController.ArrowColorPreset colorPreset,
        Mesh screenMesh,
        Mesh frameMesh,
        Material signMat,
        Material frameMat,
        float width,
        float height,
        float sweepPhaseOffset = 0f)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SmartArrowSignageController ctrl = root.AddComponent<SmartArrowSignageController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject frameObj = null;
        if (frameMesh != null)
        {
            string frameObjName = "Frame";
            if (style == SmartArrowSignageController.MountingStyle.WallMount) frameObjName = "MountArm";
            else if (style == SmartArrowSignageController.MountingStyle.CeilingHanger) frameObjName = "CeilingHanger";
            else if (style == SmartArrowSignageController.MountingStyle.FloorStand) frameObjName = "FloorStand";
            else if (style == SmartArrowSignageController.MountingStyle.FloatingPylons) frameObjName = "Pylons";

            frameObj = new GameObject(frameObjName);
            frameObj.transform.SetParent(root.transform, false);
            MeshFilter fmf = frameObj.AddComponent<MeshFilter>();
            fmf.sharedMesh = frameMesh;
            MeshRenderer fmr = frameObj.AddComponent<MeshRenderer>();
            fmr.sharedMaterial = frameMat;
            fmr.shadowCastingMode = ShadowCastingMode.On;
            fmr.receiveShadows = true;
        }

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_mountingStyle").enumValueIndex = (int)style;
        so.FindProperty("_signMode").enumValueIndex = (int)signMode;
        so.FindProperty("_arrowPattern").enumValueIndex = (int)pattern;
        so.FindProperty("_aspectRatio").enumValueIndex = (int)aspectPreset;
        so.FindProperty("_arrowRepeat").intValue = repeatCount;
        so.FindProperty("_showFrame").boolValue = (style != SmartArrowSignageController.MountingStyle.ScreenOnly);
        so.FindProperty("_signWidth").floatValue = width;
        so.FindProperty("_signHeight").floatValue = height;
        so.FindProperty("_sweepPhaseOffset").floatValue = sweepPhaseOffset;
        so.FindProperty("_autoStaggerPhaseByPosition").boolValue = true;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.ApplyColorPreset(colorPreset);
        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    // ─────────────────────────────────────────────────────────────
    // シーン内への展示用ショーケース一括配置
    // ─────────────────────────────────────────────────────────────

    public static void SpawnSmartArrowShowcase(Vector3 originPos)
    {
        const string ShowcaseName = "--- SmartArrowSignage Showcase ---";
        GameObject existing = GameObject.Find(ShowcaseName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(ShowcaseName);
        Undo.RegisterCreatedObjectUndo(root, "Spawn SmartArrow Showcase");
        root.transform.position = originPos;

        string[] prefabNames = new string[]
        {
            "Prefab_SmartArrow_WallMount.prefab",
            "Prefab_SmartArrow_CeilingHanger.prefab",
            "Prefab_SmartArrow_FloorStand.prefab",
            "Prefab_SmartArrow_FloatingHolo.prefab",
            "Prefab_SmartArrow_Framed.prefab",
            "Prefab_SmartArrow_ScreenOnly.prefab"
        };

        string[] labels = new string[]
        {
            "Showcase_01_RacingWallMount",
            "Showcase_02_CeilingHangerGuide",
            "Showcase_03_FloorStandCaution",
            "Showcase_04_FloatingHoloCurve",
            "Showcase_05_FramedTripleSpeed",
            "Showcase_06_ScreenOnlyNeonTube"
        };

        float spacing = 12.0f;
        float startX = -30.0f;

        for (int i = 0; i < prefabNames.Length; i++)
        {
            string pPath = $"{PrefabsFolder}/{prefabNames[i]}";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
            if (prefabAsset == null) continue;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, root.transform);
            instance.name = labels[i];
            instance.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);
            instance.transform.localRotation = Quaternion.identity;

            SmartArrowSignageController ctrl = instance.GetComponent<SmartArrowSignageController>();
            if (ctrl != null)
            {
                ctrl.SweepPhaseOffset = (float)i / prefabNames.Length;
                if (i == 1) ctrl.SetArrowAngle(45f);
                else if (i == 2) ctrl.SetArrowAngle(90f);
                else if (i == 5)
                {
                    ctrl.SetArrowPattern(SmartArrowSignageController.ArrowPattern.NeonTube);
                    ctrl.SetSignMode(SmartArrowSignageController.SignMode.CyberGridHolo);
                    ctrl.SetArrowAngle(-45f);
                    ctrl.SetShowHUDDecorations(false); // 空中金具無し純浮遊のため四隅留め金具グラフィックをOFF
                }
                ctrl.UpdateSignboard();
            }
        }

        Selection.activeGameObject = root;
        Debug.Log($"<color=#70D0FF>[SmartArrowSignage]</color> 展示ショーケースを配置しました: {ShowcaseName}");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
