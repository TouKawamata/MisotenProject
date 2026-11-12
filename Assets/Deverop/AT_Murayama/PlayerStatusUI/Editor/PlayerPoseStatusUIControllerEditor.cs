using System.IO;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.PlayerStatusUI.Editor
{
    [CustomEditor(typeof(PlayerPoseStatusUIController))]
    public class PlayerPoseStatusUIControllerEditor : UnityEditor.Editor
    {
        private const string PresetsFolderPath = "Assets/Deverop/AT_Murayama/PlayerStatusUI/Presets";
        private const string MaterialsFolderPath = "Assets/Deverop/AT_Murayama/PlayerStatusUI/Materials";
        private const string ExistingHoloMaterialsFolderPath = "Assets/Deverop/AT_Murayama/Hologram/Materials";
        private bool _showHoloPatternsFoldout = true;
        private bool _showCustomMaterialsQuickList = false;

        public override void OnInspectorGUI()
        {
            PlayerPoseStatusUIController controller = (PlayerPoseStatusUIController)target;

            EditorGUILayout.Space(4);
            DrawHeaderAndStatusBox(controller);

            EditorGUILayout.Space(6);
            DrawScreenPositionAndLayoutSection(controller);

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
                "・通常マテリアルそのままの表示、サイバーホログラム発光（全16パターン）、カスタムマテリアルの切り替えに対応。\n" +
                "・03シーン設定に準拠し、背景完全透過・枠線なしがデフォルトに設定されています。",
                MessageType.Info);

            string statusMsg = controller.HasValidSetup
                ? $"✅ セットアップ完了 ｜ 現在の表示状態：【 {controller.CurrentState} 】 ｜ マテリアル：【 {controller.MaterialMode} 】"
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

        private void DrawScreenPositionAndLayoutSection(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 画面配置・レイアウト直感調整 (Screen Placement)", EditorStyles.boldLabel);

            // 1. 表示方式切替（方式A vs 方式B）
            EditorGUILayout.BeginHorizontal();
            bool isModeA = (controller.DisplayMode == PlayerPoseStatusUIController.UIDisplayMode.ModeA_RenderTextureUI);
            Color origBg = GUI.backgroundColor;

            GUI.backgroundColor = isModeA ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("方式A：サブカメラ ＋ UI (RenderTexture・推奨)", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Switch to Mode A");
                controller.DisplayMode = PlayerPoseStatusUIController.UIDisplayMode.ModeA_RenderTextureUI;
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = !isModeA ? new Color(0.4f, 1.0f, 0.6f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("方式B：カメラ前 3Dミニチュア直接配置", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Switch to Mode B");
                controller.DisplayMode = PlayerPoseStatusUIController.UIDisplayMode.ModeB_CameraForeground3D;
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = origBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 2. 画面レイアウトプレビュー (16:9 Screen Guide)
            DrawPlayerStatusUIScreenPreview(controller, isModeA);

            EditorGUILayout.Space(4);

            // 3. 画面配置プリセット
            PlayerPoseStatusUIController.ScreenCorner currentCorner = controller.ScreenCornerPosition;
            EditorGUILayout.LabelField($"【画面上の配置アンカー】 現在：【 {currentCorner} 】", EditorStyles.miniBoldLabel);

            // ⭐ 画面下中央 一発配置おすすめボタン（ユーザー要望）
            bool isBottomCenter = (currentCorner == PlayerPoseStatusUIController.ScreenCorner.BottomCenter);
            GUI.backgroundColor = isBottomCenter ? new Color(1.0f, 0.85f, 0.35f) : new Color(0.45f, 0.9f, 1.0f);
            string bcBtnText = isBottomCenter
                ? "⭐ 現在【画面下中央 (Bottom Center)】に配置中（おすすめレイアウト）"
                : "⭐【画面下中央 (Bottom Center)】にワンクリック配置する";
            if (GUILayout.Button(bcBtnText, GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Set Screen Corner to Bottom Center");
                controller.SetScreenCornerPreset(PlayerPoseStatusUIController.ScreenCorner.BottomCenter, true);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = origBg;

            EditorGUILayout.Space(3);

            // 3行アンカーグリッド:
            // 上段: 左上 | 上中央 | 右上
            EditorGUILayout.BeginHorizontal();
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.TopLeft, "↖ 左上\n(Top-Left)");
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.TopCenter, "⬆ 上中央\n(Top-Center)");
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.TopRight, "↗ 右上\n(Top-Right)");
            EditorGUILayout.EndHorizontal();

            // 中段: 左下 | 下中央 | 右下
            EditorGUILayout.BeginHorizontal();
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.BottomLeft, "↙ 左下\n(Bottom-Left)");
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.BottomCenter, "⭐⬇ 画面下中央\n(Bottom-Center)");
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.BottomRight, "↘ 右下 (従来標準)\n(Bottom-Right)");
            EditorGUILayout.EndHorizontal();

            // 下段: 画面中央 | カスタム自由配置
            EditorGUILayout.BeginHorizontal();
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.Center, "🎯 画面中央\n(Center)");
            DrawCornerButton(controller, PlayerPoseStatusUIController.ScreenCorner.Custom, "⚙️ カスタム自由配置\n(Custom)");
            EditorGUILayout.EndHorizontal();

            if (currentCorner == PlayerPoseStatusUIController.ScreenCorner.Custom)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("【カスタム自由配置 (Normalized Sliders)】\n画面上の正規化座標（0.0〜1.0）でアンカーとピボットを自由に調整できます。", MessageType.None);
                EditorGUI.BeginChangeCheck();
                float ax = EditorGUILayout.Slider("アンカー X (0=左 〜 1=右)", controller.CustomAnchor.x, 0f, 1f);
                float ay = EditorGUILayout.Slider("アンカー Y (0=下 〜 1=上)", controller.CustomAnchor.y, 0f, 1f);
                float px = EditorGUILayout.Slider("ピボット X (0=左 〜 1=右)", controller.CustomPivot.x, 0f, 1f);
                float py = EditorGUILayout.Slider("ピボット Y (0=下 〜 1=上)", controller.CustomPivot.y, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change Custom Anchor/Pivot");
                    controller.CustomAnchor = new Vector2(ax, ay);
                    controller.CustomPivot = new Vector2(px, py);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(4);

            if (isModeA)
            {
                // 4. UI表示サイズ
                EditorGUILayout.LabelField("【UI表示サイズ (Size)】", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                Vector2 newSize = EditorGUILayout.Vector2Field("サイズ (幅 x 高さ px)", controller.UISize);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change UI Size");
                    controller.UISize = newSize;
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("コンパクト (320×380)", GUILayout.Height(22)))
                {
                    Undo.RecordObject(controller, "Set UI Size Compact");
                    controller.UISize = new Vector2(320f, 380f);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("標準 (390×450)", GUILayout.Height(22)))
                {
                    Undo.RecordObject(controller, "Set UI Size Standard");
                    controller.UISize = new Vector2(390f, 450f);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("ワイド (450×480)", GUILayout.Height(22)))
                {
                    Undo.RecordObject(controller, "Set UI Size Wide");
                    controller.UISize = new Vector2(450f, 480f);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // 5. マージン・オフセット微調整
                bool isCenterBased = (currentCorner == PlayerPoseStatusUIController.ScreenCorner.BottomCenter ||
                                      currentCorner == PlayerPoseStatusUIController.ScreenCorner.TopCenter ||
                                      currentCorner == PlayerPoseStatusUIController.ScreenCorner.Center);

                string labelX = isCenterBased
                    ? "↔️ 水平微調整オフセット (X: 0で完全中央)"
                    : "↔️ 画面横端からのマージン (X)";
                string labelY = isCenterBased
                    ? "↕️ 画面端からの縦マージン (Y)"
                    : "↕️ 画面縦端からのマージン (Y)";

                EditorGUILayout.LabelField("【マージン ＆ オフセット調整】", EditorStyles.miniBoldLabel);
                Vector2 curMargin = controller.UIMargin;

                EditorGUI.BeginChangeCheck();
                float newX = EditorGUILayout.Slider(labelX, curMargin.x, isCenterBased ? -400f : 0f, isCenterBased ? 400f : 200f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change UI Margin X");
                    controller.UIMargin = new Vector2(newX, curMargin.y);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                if (isCenterBased && Mathf.Abs(curMargin.x) > 0.01f)
                {
                    if (GUILayout.Button("🎯 横オフセットを 0 にリセット (完全中央揃え)", GUILayout.Height(20)))
                    {
                        Undo.RecordObject(controller, "Reset UI Margin X to Center");
                        controller.UIMargin = new Vector2(0f, curMargin.y);
                        EditorUtility.SetDirty(controller);
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        SceneView.RepaintAll();
                    }
                }

                EditorGUI.BeginChangeCheck();
                float newY = EditorGUILayout.Slider(labelY, curMargin.y, 0f, 250f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change UI Margin Y");
                    controller.UIMargin = new Vector2(controller.UIMargin.x, newY);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(4);

                // 6. カメラ視点・画角微調整（UI内でのキャラクターの映り方）
                EditorGUILayout.LabelField("【UI内キャラクター視点・カメラアングル】", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                float newYaw = EditorGUILayout.Slider("向き回転 Yaw (度: 160〜200=正面斜め)", controller.ModelViewYaw, 0f, 360f);
                float newPitch = EditorGUILayout.Slider("上下ピッチ Pitch (度)", controller.CameraPitch, -25f, 35f);
                float newDist = EditorGUILayout.Slider("カメラ距離倍率 (小さい=上半身寄り)", controller.CameraDistanceMultiplier, 0.45f, 2.0f);
                float newHeight = EditorGUILayout.Slider("カメラ高さオフセット", controller.CameraHeightOffset, -0.5f, 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change Camera Angle/Distance");
                    controller.ModelViewYaw = newYaw;
                    controller.CameraPitch = newPitch;
                    controller.CameraDistanceMultiplier = newDist;
                    controller.CameraHeightOffset = newHeight;
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
            }
            else
            {
                // ── 方式B：カメラ前 3Dミニチュア設定 ──
                EditorGUILayout.LabelField("【方式B 3Dミニチュア詳細調整】", EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();
                float bDist = EditorGUILayout.Slider("🔍 前方距離 Forward Distance (m)", controller.ModeBForwardDistance, 0.4f, 3.0f);
                float bScale = EditorGUILayout.Slider("🔬 ミニチュア縮小スケール Scale", controller.ModeBMiniatureScale, 0.02f, 0.3f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change Mode B Distance/Scale");
                    controller.ModeBForwardDistance = bDist;
                    controller.ModeBMiniatureScale = bScale;
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(3);

                // マージン・オフセット
                bool isCenterBased = (currentCorner == PlayerPoseStatusUIController.ScreenCorner.BottomCenter ||
                                      currentCorner == PlayerPoseStatusUIController.ScreenCorner.TopCenter ||
                                      currentCorner == PlayerPoseStatusUIController.ScreenCorner.Center);

                string labelX = isCenterBased
                    ? "↔️ 水平微調整オフセット (X: 0で完全中央)"
                    : "↔️ 画面横端からのマージン (X)";
                string labelY = isCenterBased
                    ? "↕️ 画面端からの縦マージン (Y)"
                    : "↕️ 画面縦端からのマージン (Y)";

                EditorGUILayout.LabelField("【画面端マージン ＆ オフセット調整】", EditorStyles.miniBoldLabel);
                Vector2 curMargin = controller.UIMargin;

                EditorGUI.BeginChangeCheck();
                float newX = EditorGUILayout.Slider(labelX, curMargin.x, isCenterBased ? -400f : 0f, isCenterBased ? 400f : 200f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change UI Margin X");
                    controller.UIMargin = new Vector2(newX, curMargin.y);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                if (isCenterBased && Mathf.Abs(curMargin.x) > 0.01f)
                {
                    if (GUILayout.Button("🎯 横オフセットを 0 にリセット (完全中央揃え)", GUILayout.Height(20)))
                    {
                        Undo.RecordObject(controller, "Reset UI Margin X to Center");
                        controller.UIMargin = new Vector2(0f, curMargin.y);
                        EditorUtility.SetDirty(controller);
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        SceneView.RepaintAll();
                    }
                }

                EditorGUI.BeginChangeCheck();
                float newY = EditorGUILayout.Slider(labelY, curMargin.y, 0f, 250f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change UI Margin Y");
                    controller.UIMargin = new Vector2(controller.UIMargin.x, newY);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(3);

                // キャラクターモデル向き回転
                EditorGUI.BeginChangeCheck();
                float newYaw = EditorGUILayout.Slider("向き回転 Yaw (度: 160〜200=正面斜め)", controller.ModelViewYaw, 0f, 360f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Change Model Yaw");
                    controller.ModelViewYaw = newYaw;
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawPlayerStatusUIScreenPreview(PlayerPoseStatusUIController controller, bool isModeA)
        {
            EditorGUILayout.LabelField("🖥️ 画面レイアウトプレビュー (16:9 Screen Guide)", EditorStyles.miniBoldLabel);

            float screenW = 200f;
            float screenH = 112.5f; // 16:9
            Rect totalRect = GUILayoutUtility.GetRect(0f, screenH + 8f, GUILayout.ExpandWidth(true));
            float startX = totalRect.x + (totalRect.width - screenW) * 0.5f;
            float startY = totalRect.y + 4f;
            Rect screenRect = new Rect(startX, startY, screenW, screenH);

            // Screen background
            EditorGUI.DrawRect(screenRect, new Color(0.08f, 0.10f, 0.14f, 1f));

            // Center crosshairs
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawLine(new Vector3(screenRect.x, screenRect.y + screenH * 0.5f, 0), new Vector3(screenRect.x + screenW, screenRect.y + screenH * 0.5f, 0));
            Handles.DrawLine(new Vector3(screenRect.x + screenW * 0.5f, screenRect.y, 0), new Vector3(screenRect.x + screenW * 0.5f, screenRect.y + screenH, 0));

            // Screen outline
            Handles.color = new Color(0.35f, 0.45f, 0.55f, 0.8f);
            Handles.DrawPolyLine(
                new Vector3(screenRect.x, screenRect.y, 0),
                new Vector3(screenRect.x + screenW, screenRect.y, 0),
                new Vector3(screenRect.x + screenW, screenRect.y + screenH, 0),
                new Vector3(screenRect.x, screenRect.y + screenH, 0),
                new Vector3(screenRect.x, screenRect.y, 0)
            );

            // Minimap placeholder hint (bottom-left) to help developer see relative layout
            Rect minimapHint = new Rect(screenRect.x + 3f, screenRect.y + screenH - 35f, 32f, 32f);
            EditorGUI.DrawRect(minimapHint, new Color(0.2f, 0.8f, 1f, 0.12f));
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Handles.DrawPolyLine(
                new Vector3(minimapHint.x, minimapHint.y, 0),
                new Vector3(minimapHint.x + minimapHint.width, minimapHint.y, 0),
                new Vector3(minimapHint.x + minimapHint.width, minimapHint.y + minimapHint.height, 0),
                new Vector3(minimapHint.x, minimapHint.y + minimapHint.height, 0),
                new Vector3(minimapHint.x, minimapHint.y, 0)
            );
            GUIStyle mapStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.8f, 1f, 0.6f) },
                fontSize = 8
            };
            GUI.Label(minimapHint, "Minimap\n(左下標準)", mapStyle);

            // Player Status UI rect
            PlayerPoseStatusUIController.ScreenCorner corner = controller.ScreenCornerPosition;
            Vector2 normAnchor;
            Vector2 pivot;

            switch (corner)
            {
                case PlayerPoseStatusUIController.ScreenCorner.BottomLeft:
                    normAnchor = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f); break;
                case PlayerPoseStatusUIController.ScreenCorner.TopRight:
                    normAnchor = new Vector2(1f, 1f); pivot = new Vector2(1f, 1f); break;
                case PlayerPoseStatusUIController.ScreenCorner.TopLeft:
                    normAnchor = new Vector2(0f, 1f); pivot = new Vector2(0f, 1f); break;
                case PlayerPoseStatusUIController.ScreenCorner.BottomCenter:
                    normAnchor = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 0f); break;
                case PlayerPoseStatusUIController.ScreenCorner.TopCenter:
                    normAnchor = new Vector2(0.5f, 1f); pivot = new Vector2(0.5f, 1f); break;
                case PlayerPoseStatusUIController.ScreenCorner.Center:
                    normAnchor = new Vector2(0.5f, 0.5f); pivot = new Vector2(0.5f, 0.5f); break;
                case PlayerPoseStatusUIController.ScreenCorner.Custom:
                    normAnchor = controller.CustomAnchor; pivot = controller.CustomPivot; break;
                default: // BottomRight
                    normAnchor = new Vector2(1f, 0f); pivot = new Vector2(1f, 0f); break;
            }

            float scale = screenW / 1920f;
            float elemW = Mathf.Clamp(controller.UISize.x * scale, 24f, 65f);
            float elemH = Mathf.Clamp(controller.UISize.y * scale, 26f, 70f);

            float ax = screenRect.x + normAnchor.x * screenRect.width;
            float ay = screenRect.y + (1f - normAnchor.y) * screenRect.height;

            float marginX = controller.UIMargin.x * scale;
            float marginY = controller.UIMargin.y * scale;

            float offsetX = 0f;
            if (Mathf.Abs(normAnchor.x) < 0.01f) offsetX = marginX;
            else if (Mathf.Abs(normAnchor.x - 1f) < 0.01f) offsetX = -marginX;
            else offsetX = marginX;

            float offsetY = 0f;
            if (Mathf.Abs(normAnchor.y) < 0.01f) offsetY = -marginY;
            else if (Mathf.Abs(normAnchor.y - 1f) < 0.01f) offsetY = marginY;
            else offsetY = -marginY;

            float px = (ax + offsetX) - pivot.x * elemW;
            float py = (ay + offsetY) - (1f - pivot.y) * elemH;

            Rect elemRect = new Rect(px, py, elemW, elemH);

            // Draw Player UI box (Warm Gold glow for bottom-center recommendation)
            bool isBC = (corner == PlayerPoseStatusUIController.ScreenCorner.BottomCenter);
            Color fillCol = isBC ? new Color(1f, 0.85f, 0.25f, 0.5f) : new Color(0.35f, 0.85f, 1f, 0.45f);
            Color borderCol = isBC ? new Color(1f, 0.92f, 0.4f, 1f) : new Color(0.45f, 0.95f, 1f, 1f);

            EditorGUI.DrawRect(elemRect, fillCol);
            Handles.color = borderCol;
            Handles.DrawPolyLine(
                new Vector3(elemRect.x, elemRect.y, 0),
                new Vector3(elemRect.x + elemRect.width, elemRect.y, 0),
                new Vector3(elemRect.x + elemRect.width, elemRect.y + elemRect.height, 0),
                new Vector3(elemRect.x, elemRect.y + elemRect.height, 0),
                new Vector3(elemRect.x, elemRect.y, 0)
            );

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = isBC ? 10 : 9
            };
            string labelTxt = isBC ? "⭐ 画面下中央\n(Status UI)" : "🕺 状態UI";
            GUI.Label(elemRect, labelTxt, labelStyle);
        }

        private void DrawCornerButton(PlayerPoseStatusUIController controller, PlayerPoseStatusUIController.ScreenCorner corner, string label)
        {
            bool isSelected = controller.ScreenCornerPosition == corner;
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.88f, 0.88f, 0.88f);
            if (GUILayout.Button(label, GUILayout.Height(32)))
            {
                Undo.RecordObject(controller, $"Set Screen Corner to {corner}");
                controller.SetScreenCornerPreset(corner, true);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = origBg;
        }

        private void DrawMaterialAndAppearanceQuickBox(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 マテリアル方式 ＆ ホログラム選定・背景透過設定", EditorStyles.boldLabel);

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
            if (GUILayout.Button("② ホログラム (全16種)\n(Cyber Hologram)", GUILayout.Height(32)))
            {
                controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.HologramShader;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            // ③ カスタム
            GUI.backgroundColor = (currentMode == PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial)
                ? new Color(1.0f, 0.8f, 0.35f)
                : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("③ カスタムマテリアル\n(Custom Material)", GUILayout.Height(32)))
            {
                controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 状態による強制カラー変更の全体トグル
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = controller.EnableStateColorOverride ? new Color(0.4f, 0.95f, 0.6f) : new Color(1.0f, 0.75f, 0.75f);
            string toggleLabel = controller.EnableStateColorOverride
                ? "🎨 状態カラー変化 (スタン/ガード/ブーストの色変更): [ 有効 (ON) ]"
                : "🎨 状態カラー変化 (スタン/ガード/ブーストの色変更): [ 無し (OFF) - 本来の色を維持 ]";
            if (GUILayout.Button(toggleLabel, GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Toggle State Color Override");
                controller.EnableStateColorOverride = !controller.EnableStateColorOverride;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBgColor;
            if (!controller.EnableStateColorOverride)
            {
                EditorGUILayout.HelpBox("💡 状態カラー変化が【無し】に設定されています。スタン時やシールド/ガード時でも勝手な色変化が入らず、モデル本来のマテリアル色（または選択中ホログラム固有の配色）が100%維持されます。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // モードに応じた詳細設定パネル
            switch (controller.MaterialMode)
            {
                case PlayerPoseStatusUIController.MaterialDisplayMode.OriginalMaterial:
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.HelpBox("モデル本来のマテリアル（テクスチャ・通常シェーダー）を全スロットにそのまま表示します。", MessageType.None);
                    EditorGUI.BeginChangeCheck();
                    bool newTint = EditorGUILayout.ToggleLeft("🌈 状態変化時の発光ティント（スタン/ブースト/シールド等）", controller.TintOriginalMaterialOnState);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(controller, "Toggle Tint Original Material");
                        controller.TintOriginalMaterialOnState = newTint;
                        EditorUtility.SetDirty(controller);
                        SceneView.RepaintAll();
                    }
                    if (controller.TintOriginalMaterialOnState && controller.EnableStateColorOverride)
                    {
                        EditorGUI.BeginChangeCheck();
                        float newIntensity = EditorGUILayout.Slider("ティント強度 (Tint Intensity)", controller.OriginalMaterialTintIntensity, 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(controller, "Change Tint Intensity");
                            controller.OriginalMaterialTintIntensity = newIntensity;
                            EditorUtility.SetDirty(controller);
                            SceneView.RepaintAll();
                        }
                    }
                    EditorGUILayout.EndVertical();
                    break;

                case PlayerPoseStatusUIController.MaterialDisplayMode.HologramShader:
                    DrawHologramPatternSelector(controller);
                    break;

                case PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial:
                    DrawCustomMaterialSelector(controller);
                    break;
            }

            EditorGUILayout.Space(6);
            DrawBackgroundAndTransparencySection(controller);

            EditorGUILayout.EndVertical();
        }

        private void DrawHologramPatternSelector(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"🔮 ホログラムパターン選択（現在：【 {controller.HologramPattern} 】）", EditorStyles.boldLabel);

            // 発光色連動 ＆ Premultiplied Alpha トグル
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newSync = EditorGUILayout.ToggleLeft("🌈 状態発光色（通常/ブースト/シールド/スタン）に連動", controller.SyncHologramColorsWithState, GUILayout.Width(340));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle Sync Hologram Colors");
                controller.SyncHologramColorsWithState = newSync;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            if (!controller.EnableStateColorOverride)
            {
                EditorGUILayout.HelpBox("※現在「状態カラー変化」が全体で【無し (OFF)】に設定されているため、マテリアル固有の配色が維持されます。", MessageType.None);
            }

            EditorGUILayout.Space(4);
            _showHoloPatternsFoldout = EditorGUILayout.Foldout(_showHoloPatternsFoldout, "📋 全16パターン一覧（クリックで即座に切り替え）", true, EditorStyles.foldoutHeader);
            if (_showHoloPatternsFoldout)
            {
                Color prevBg = GUI.backgroundColor;

                // グループ1：新設・UI特化ホログラム 5種（最重要・新機能）
                EditorGUILayout.LabelField("【✨ UI特化・高視認性ホログラム 5種（新設・推奨）】", EditorStyles.miniBoldLabel);
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.UI_CyberNeon, "12. ✨ サイバーネオン"),
                    (PlayerPoseStatusUIController.UIHologramPattern.UI_QuantumLattice, "13. ✨ クアンタム格子"));
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.UI_TacticalGlitch, "14. ✨ 戦術グリッチ"),
                    (PlayerPoseStatusUIController.UIHologramPattern.UI_AuroraPlasma, "15. ✨ オーロラプラズマ"));
                DrawHologramButtonSingle(controller,
                    PlayerPoseStatusUIController.UIHologramPattern.UI_GlintPulseRadar, "16. ✨ レーダースキャン・パルス");

                EditorGUILayout.Space(4);

                // グループ2：定番 3Dモデルホログラム 7種
                EditorGUILayout.LabelField("【🔷 定番 3Dモデルホログラム 7種】", EditorStyles.miniBoldLabel);
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.ClassicScanline, "01. スキャンライン"),
                    (PlayerPoseStatusUIController.UIHologramPattern.HexShield, "02. ヘックスシールド"));
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.WireframeGrid, "03. CADワイヤー"),
                    (PlayerPoseStatusUIController.UIHologramPattern.CyberGlitch, "04. サイバーグリッチ"));
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.LEDMatrix, "05. LEDドット"),
                    (PlayerPoseStatusUIController.UIHologramPattern.PatternBanner, "06. パターン矢印"));
                DrawHologramButtonSingle(controller,
                    PlayerPoseStatusUIController.UIHologramPattern.ProjectorBeam, "07. 台座プロジェクター照射光");

                EditorGUILayout.Space(4);

                // グループ3：拡張・汎用ホログラム 4種
                EditorGUILayout.LabelField("【🔶 拡張・汎用ホログラム 4種】", EditorStyles.miniBoldLabel);
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.GhostRim, "08. 最軽量ゴースト輪郭"),
                    (PlayerPoseStatusUIController.UIHologramPattern.Dissolve, "09. ディゾルブ消滅"));
                DrawHologramButtonRow(controller,
                    (PlayerPoseStatusUIController.UIHologramPattern.TriplanarDots, "10. 3軸均等ドット"),
                    (PlayerPoseStatusUIController.UIHologramPattern.GradientEnergy, "11. 高さグラデエネルギー"));

                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.Space(4);

            // 選択中パターンの動的説明ボックス
            string patternDesc = GetPatternDescription(controller.HologramPattern);
            EditorGUILayout.HelpBox(patternDesc, MessageType.None);

            if (controller.HologramPattern == PlayerPoseStatusUIController.UIHologramPattern.GhostRim)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("【👻 08. 最軽量ゴースト輪郭：パルス強度制御】", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                bool disablePulse = EditorGUILayout.ToggleLeft(" ⏸️ 呼吸パルスを無効化（常時固定強度・パルス無しバージョン）", controller.DisableGhostRimPulse, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Toggle GhostRim Pulse");
                    controller.DisableGhostRimPulse = disablePulse;
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.BeginHorizontal();
                Color prevBtnBg = GUI.backgroundColor;
                GUI.backgroundColor = controller.DisableGhostRimPulse ? new Color(0.4f, 0.95f, 0.65f) : new Color(0.9f, 0.9f, 0.9f);
                if (GUILayout.Button("✨ 常時固定版 (Static / パルス無)", EditorStyles.miniButton))
                {
                    Undo.RecordObject(controller, "Set Static GhostRim");
                    controller.DisableGhostRimPulse = true;
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = !controller.DisableGhostRimPulse ? new Color(0.35f, 0.85f, 1.0f) : new Color(0.9f, 0.9f, 0.9f);
                if (GUILayout.Button("🌊 呼吸パルス版 (Pulse / 脈動有)", EditorStyles.miniButton))
                {
                    Undo.RecordObject(controller, "Set Pulse GhostRim");
                    controller.DisableGhostRimPulse = false;
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = prevBtnBg;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(3);

            // 本採用確定ボタン ＆ マテリアル一括生成ボタン
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.65f);
            if (GUILayout.Button($"⭐ 現在のホログラム【 {controller.HologramPattern} 】を本採用（確定）する", GUILayout.Height(30)))
            {
                Undo.RecordObject(controller, "Adopt Current Hologram");
                controller.AdoptCurrentHologram(false);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
                Debug.Log($"[PlayerPoseStatusUI] ホログラムパターン【 {controller.HologramPattern} 】を本採用として確定しました。");
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.85f, 0.92f, 1.0f);
            if (GUILayout.Button("💾 現在の設定を別名マテリアルとして保存 (Save As...)", GUILayout.Height(25)))
            {
                SaveAsCustomMaterial(controller);
            }

            GUI.backgroundColor = new Color(0.95f, 0.85f, 0.45f);
            if (GUILayout.Button("📁 UI専用マテリアル全16種を一括再生成・保存", GUILayout.Height(25)))
            {
                GenerateAllUIMaterialAssets();
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = origBg;
            EditorGUILayout.EndVertical();
        }

        private void DrawHologramButtonRow(
            PlayerPoseStatusUIController controller,
            (PlayerPoseStatusUIController.UIHologramPattern pattern, string label) item1,
            (PlayerPoseStatusUIController.UIHologramPattern pattern, string label) item2)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSelected1 = controller.HologramPattern == item1.pattern;
            GUI.backgroundColor = isSelected1 ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(item1.label, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Change Hologram Pattern");
                controller.ApplyHologramPattern(item1.pattern);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            bool isSelected2 = controller.HologramPattern == item2.pattern;
            GUI.backgroundColor = isSelected2 ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(item2.label, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Change Hologram Pattern");
                controller.ApplyHologramPattern(item2.pattern);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHologramButtonSingle(
            PlayerPoseStatusUIController controller,
            PlayerPoseStatusUIController.UIHologramPattern pattern,
            string label)
        {
            bool isSelected = controller.HologramPattern == pattern;
            GUI.backgroundColor = isSelected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button(label, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Change Hologram Pattern");
                controller.ApplyHologramPattern(pattern);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
        }

        private void DrawCustomMaterialSelector(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 カスタムマテリアル割り当て ＆ ホログラムアセット選定", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Material newMat = (Material)EditorGUILayout.ObjectField("割り当てマテリアル", controller.CustomMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Custom Material");
                controller.AssignMaterialAsset(newMat);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(3);

            // 本採用ボタン ＆ 別名保存ボタン
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.65f);
            string curMatName = controller.CustomMaterial != null ? controller.CustomMaterial.name : "未割り当て";
            if (GUILayout.Button($"⭐ 現在のカスタムマテリアル【 {curMatName} 】を本採用（確定）する", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Adopt Custom Material");
                controller.AdoptCurrentHologram(true);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
                Debug.Log($"[PlayerPoseStatusUI] カスタムマテリアル【 {curMatName} 】を本採用として確定しました。");
            }

            GUI.backgroundColor = new Color(0.85f, 0.92f, 1.0f);
            if (GUILayout.Button("💾 現在のカスタムマテリアルを別名保存 (Save As...)", GUILayout.Height(24)))
            {
                SaveAsCustomMaterial(controller);
            }
            GUI.backgroundColor = origBg;

            EditorGUILayout.Space(4);
            _showCustomMaterialsQuickList = EditorGUILayout.Foldout(_showCustomMaterialsQuickList, "⚡ ホログラムマテリアル一覧からワンクリック割り当て", true, EditorStyles.foldoutHeader);
            if (_showCustomMaterialsQuickList)
            {
                DrawMaterialQuickAssignButtons(controller);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialQuickAssignButtons(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.BeginVertical();

            // 1. UI専用ホログラムマテリアル全16種 ＋ 静止版
            EditorGUILayout.LabelField("【✨ UI専用ホログラムマテリアル 全16種 ＋ 静止/パルス無バリエーション】", EditorStyles.miniBoldLabel);
            string[] uiMatNames = new string[]
            {
                "M_UIHolo_12_CyberNeon",
                "M_UIHolo_13_QuantumLattice",
                "M_UIHolo_14_TacticalGlitch",
                "M_UIHolo_15_AuroraPlasma",
                "M_UIHolo_16_GlintPulseRadar",
                "M_UIHolo_01_ClassicScanline",
                "M_UIHolo_02_HexShield",
                "M_UIHolo_03_WireframeGrid",
                "M_UIHolo_04_CyberGlitch",
                "M_UIHolo_05_HoloLEDMatrix",
                "M_UIHolo_06_HoloPatternBanner",
                "M_UIHolo_07_HoloProjectorBeam",
                "M_UIHolo_08_GhostRim",
                "M_UIHolo_08_GhostRim_Static",
                "M_UIHolo_09_Dissolve",
                "M_UIHolo_10_TriplanarDots",
                "M_UIHolo_11_GradientEnergy"
            };

            for (int i = 0; i < uiMatNames.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawSingleMatAssignButton(controller, uiMatNames[i], false);
                if (i + 1 < uiMatNames.Length)
                {
                    DrawSingleMatAssignButton(controller, uiMatNames[i + 1], false);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            // 2. 既存3Dモデルホログラムマテリアル全11種 ＋ 静止版
            EditorGUILayout.LabelField("【🏛️ 既存3Dモデル用ホログラムマテリアル 全11種 ＋ 静止/パルス無バリエーション】", EditorStyles.miniBoldLabel);
            string[] existingMatNames = new string[]
            {
                "M_Model_01_ClassicScanline",
                "M_Model_02_HexShield",
                "M_Model_03_WireframeGrid",
                "M_Model_04_CyberGlitch",
                "M_Model_05_HoloLEDMatrix",
                "M_Model_06_HoloPatternBanner",
                "M_Model_07_HoloProjectorBeam",
                "M_Model_08_GhostRim",
                "M_Model_08_GhostRim_Static",
                "M_Model_09_Dissolve",
                "M_Model_10_TriplanarDots",
                "M_Model_11_GradientEnergy"
            };

            for (int i = 0; i < existingMatNames.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawSingleMatAssignButton(controller, existingMatNames[i], true);
                if (i + 1 < existingMatNames.Length)
                {
                    DrawSingleMatAssignButton(controller, existingMatNames[i + 1], true);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSingleMatAssignButton(PlayerPoseStatusUIController controller, string matName, bool isExistingFolder)
        {
            string folder = isExistingFolder ? ExistingHoloMaterialsFolderPath : MaterialsFolderPath;
            string path = $"{folder}/{matName}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

            bool isCurrent = controller.CustomMaterial == m && m != null;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isCurrent ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.9f, 0.9f, 0.9f);

            string shortName = matName.Replace("M_UIHolo_", "").Replace("M_Model_", "");
            if (shortName == "08_GhostRim_Static")
            {
                shortName = "08. GhostRim (常時固定/パルス無)";
            }
            else if (shortName == "08_GhostRim")
            {
                shortName = "08. GhostRim (呼吸パルス)";
            }
            if (GUILayout.Button(shortName, GUILayout.Height(24)))
            {
                if (m != null)
                {
                    Undo.RecordObject(controller, "Assign Hologram Material");
                    controller.AssignMaterialAsset(m);
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
                else
                {
                    Debug.LogWarning($"[PlayerPoseStatusUI] マテリアル '{path}' が見つかりません。");
                }
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawBackgroundAndTransparencySection(PlayerPoseStatusUIController controller)
        {
            EditorGUILayout.LabelField("【UI背景透過 ＆ レイアウト設定（03シーン準拠）】", EditorStyles.miniBoldLabel);

            // 透過ステータス表示
            bool isTransparent = controller.UIBackdropColor.a <= 0.001f && !controller.ShowUIBorderFrame;
            if (isTransparent)
            {
                EditorGUILayout.HelpBox("🟢 現在の背景状態：完全透過（03検証シーン仕様に完全準拠中）", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚪ 現在の背景状態：半透明またはカスタム枠線設定中", MessageType.None);
            }

            // 03シーン設定を一発適用するクイックボタン
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.9f, 1.0f);
            if (GUILayout.Button("✨ 03シーン推奨設定（背景完全透過・枠線なし・390×450）を一発適用", GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Apply 03 Scene Transparent Background Settings");
                controller.Apply03SceneDefaults();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = origBg;

            EditorGUILayout.Space(2);

            // 背景パネル色とアルファ
            EditorGUI.BeginChangeCheck();
            Color newBgCol = EditorGUILayout.ColorField("背景パネル色・アルファ (0=完全透過)", controller.UIBackdropColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change UI Backdrop Color");
                controller.UIBackdropColor = newBgCol;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            // 外枠ボーダー表示 & アルファ
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newShowBorder = EditorGUILayout.Toggle("外枠ボーダー表示", controller.ShowUIBorderFrame);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle UI Border Frame");
                controller.ShowUIBorderFrame = newShowBorder;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            EditorGUI.BeginChangeCheck();
            float newBorderAlpha = EditorGUILayout.Slider(controller.UIBorderAlpha, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change UI Border Alpha");
                controller.UIBorderAlpha = newBorderAlpha;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();

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

            // UI専用 Premultiplied Alpha トグル
            EditorGUI.BeginChangeCheck();
            bool newPremul = EditorGUILayout.ToggleLeft("✨ UI専用 Premultiplied Alpha 適用（ホログラムの加算発光・走査線を背景透過時も鮮やかに発光）", controller.UsePremultipliedUIAlpha);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle Premultiplied UI Alpha");
                controller.UsePremultipliedUIAlpha = newPremul;
                controller.UpdateUILayoutAndCamera();
                EditorUtility.SetDirty(controller);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("【演出 ＆ 表示切り替え】", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();

            // 瞬間フラッシュ演出 ON/OFF
            GUI.backgroundColor = controller.EnableEventFlash ? new Color(1.0f, 0.9f, 0.4f) : new Color(0.75f, 0.75f, 0.75f);
            string flashLabel = controller.EnableEventFlash ? "⚡ 瞬間フラッシュ: 有効" : "⚡ 瞬間フラッシュ: 無効";
            if (GUILayout.Button(flashLabel, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Toggle Event Flash");
                controller.EnableEventFlash = !controller.EnableEventFlash;
                EditorUtility.SetDirty(controller);
            }

            // 状態名テキスト表示 ON/OFF
            GUI.backgroundColor = controller.ShowStateLabel ? new Color(0.45f, 0.85f, 1.0f) : new Color(0.75f, 0.75f, 0.75f);
            string labelText = controller.ShowStateLabel ? "🔤 状態テキスト: 表示" : "🔤 状態テキスト: 非表示";
            if (GUILayout.Button(labelText, GUILayout.Height(26)))
            {
                Undo.RecordObject(controller, "Toggle State Label Text");
                controller.ShowStateLabel = !controller.ShowStateLabel;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = origBg;
            EditorGUILayout.EndHorizontal();
        }

        private static string GetPatternDescription(PlayerPoseStatusUIController.UIHologramPattern pattern)
        {
            switch (pattern)
            {
                case PlayerPoseStatusUIController.UIHologramPattern.ClassicScanline:
                    return "【01. 王道スキャンライン】等間隔の水平走査線 ＋ 上下に通過する太いレーザースイープ光帯 ＋ リム発光。SF映画でおなじみの最も標準的なホログラム表現。";
                case PlayerPoseStatusUIController.UIHologramPattern.HexShield:
                    return "【02. 六角形ヘックスシールド】モデル表面に六角形ハニカム網目が浮かび、上下にエネルギー波動が波打つ。バリアやSFシールド感の高いデザイン。";
                case PlayerPoseStatusUIController.UIHologramPattern.WireframeGrid:
                    return "【03. 3D等高線CADワイヤー】3D空間の等高線格子線 ＋ レーザースキャン平面が交差し、解析中サイバーホログラムを演出。";
                case PlayerPoseStatusUIController.UIHologramPattern.CyberGlitch:
                    return "【04. サイバーRGB色収差グリッチ】赤・緑・青のRGB分離 ＋ 周期的な同期乱れバー ＋ 頂点が瞬間的に矩形スライス崩壊するエラー演出。";
                case PlayerPoseStatusUIController.UIHologramPattern.LEDMatrix:
                    return "【05. サイバーLEDドットマトリクス】3Dモデル表面を丸いLEDドットの集合体として描画。LEDの隙間が程よく透けて軽快な視認性。";
                case PlayerPoseStatusUIController.UIHologramPattern.PatternBanner:
                    return "【06. 流れるサイバー矢印】モデル表面をサイバーな矢印パターン（>>>）やストライプが高速で駆け抜けるスピード感あるデザイン。";
                case PlayerPoseStatusUIController.UIHologramPattern.ProjectorBeam:
                    return "【07. 台座プロジェクター照射光】下部の台座位置からモデル全体へ上向きの光線ビームが投射され、色収差で揺らめく立体映像。";
                case PlayerPoseStatusUIController.UIHologramPattern.GhostRim:
                    return "【08. 最軽量フレネル輪郭】複雑な計算を排し、輪郭発光のみで構成される最高峰の軽さと視認性。呼吸パルス版と常時固定強度版（Static・パルス無）を選択可能。";
                case PlayerPoseStatusUIController.UIHologramPattern.Dissolve:
                    return "【09. 出現消滅ディゾルブ】3Dノイズでモデルが分解・消滅・再構成される表現。境界線のネオン発光エッジが特徴的。";
                case PlayerPoseStatusUIController.UIHologramPattern.TriplanarDots:
                    return "【10. 3軸均等ドット点群】モデルのUV展開に依存せず、3D空間3軸から歪みなく均等なLEDドット群を投影。";
                case PlayerPoseStatusUIController.UIHologramPattern.GradientEnergy:
                    return "【11. 高さグラデーション・上昇エネルギー】足元から頭部へ色が移り変わる美しいグラデーション ＋ 下から上へ湧き上がるエネルギー光ノイズ。";
                case PlayerPoseStatusUIController.UIHologramPattern.UI_CyberNeon:
                    return "【12. ✨ UI専用：サイバーネオン】深みのあるシアンのクリスタルコア ＋ ネオンバイオレットの鋭敏なフレネル輪郭。UI透過画面で最高レベルの輪郭視認性を誇る王道デザイン。";
                case PlayerPoseStatusUIController.UIHologramPattern.UI_QuantumLattice:
                    return "【13. ✨ UI専用：クアンタム格子】高密度CADワイヤー（密度32）と高速スキャンスライスが交差。キャラクターのボーン骨格やポーズの変化が最も際立つ設計。";
                case PlayerPoseStatusUIController.UIHologramPattern.UI_TacticalGlitch:
                    return "【14. ✨ UI専用：戦術データストリーム】ミリタリーHUD調の抑制されたスライスグリッチ ＋ テレメトリ同期乱れ。被弾やスタン時の緊迫感と最高のマッチング。";
                case PlayerPoseStatusUIController.UIHologramPattern.UI_AuroraPlasma:
                    return "【15. ✨ UI専用：オーロラプラズマ】ティールブルーからマゼンタバイオレットへ揺らめくプラズマエネルギー。レースUIに華やかさと洗練されたSF感をプラス。";
                case PlayerPoseStatusUIController.UIHologramPattern.UI_GlintPulseRadar:
                    return "【16. ✨ UI専用：レーダースキャン・パルス】高テンポな呼吸パルスと鋭いリム発光。一瞬のチラ見でもポーズと左右移動が100%直感的に伝わる超高視認性デザイン。";
                default:
                    return "サイバーホログラムシェーダー";
            }
        }

        private static void GenerateAllUIMaterialAssets()
        {
            if (!Directory.Exists(MaterialsFolderPath))
            {
                Directory.CreateDirectory(MaterialsFolderPath);
            }

            // 全16種のマテリアルアセット設定
            (string fileName, string shaderPath)[] materialConfigs = new (string, string)[]
            {
                ("M_UIHolo_01_ClassicScanline", "Custom/Hologram/ModelHologram"),
                ("M_UIHolo_02_HexShield", "Custom/Hologram/ModelHologram_HexGrid"),
                ("M_UIHolo_03_WireframeGrid", "Custom/Hologram/ModelHologram_WireframeGrid"),
                ("M_UIHolo_04_CyberGlitch", "Custom/Hologram/ModelHologram_CyberGlitch"),
                ("M_UIHolo_05_HoloLEDMatrix", "Custom/Hologram/ModelHologram_LEDMatrix"),
                ("M_UIHolo_06_HoloPatternBanner", "Custom/Hologram/ModelHologram_PatternBanner"),
                ("M_UIHolo_07_HoloProjectorBeam", "Custom/Hologram/ModelHologram_ProjectorBeam"),
                ("M_UIHolo_08_GhostRim", "Custom/Hologram/ModelHologram_GhostRim"),
                ("M_UIHolo_09_Dissolve", "Custom/Hologram/ModelHologram_Dissolve"),
                ("M_UIHolo_10_TriplanarDots", "Custom/Hologram/ModelHologram_TriplanarDots"),
                ("M_UIHolo_11_GradientEnergy", "Custom/Hologram/ModelHologram_GradientEnergy"),
                ("M_UIHolo_12_CyberNeon", "Custom/Hologram/ModelHologram"),
                ("M_UIHolo_13_QuantumLattice", "Custom/Hologram/ModelHologram_WireframeGrid"),
                ("M_UIHolo_14_TacticalGlitch", "Custom/Hologram/ModelHologram_CyberGlitch"),
                ("M_UIHolo_15_AuroraPlasma", "Custom/Hologram/ModelHologram_GradientEnergy"),
                ("M_UIHolo_16_GlintPulseRadar", "Custom/Hologram/ModelHologram_GhostRim")
            };

            int generatedCount = 0;
            for (int i = 0; i < materialConfigs.Length; i++)
            {
                var cfg = materialConfigs[i];
                string assetPath = $"{MaterialsFolderPath}/{cfg.fileName}.mat";
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                Shader shader = Shader.Find(cfg.shaderPath);
                if (shader == null) continue;

                bool isNew = (existing == null);
                if (isNew)
                {
                    existing = new Material(shader) { name = cfg.fileName };
                    PlayerPoseStatusUIController.ConfigurePatternSpecificDefaults(existing, (PlayerPoseStatusUIController.UIHologramPattern)i, true);
                    AssetDatabase.CreateAsset(existing, assetPath);
                }
                else
                {
                    existing.shader = shader;
                    PlayerPoseStatusUIController.ConfigurePatternSpecificDefaults(existing, (PlayerPoseStatusUIController.UIHologramPattern)i, false);
                }

                EditorUtility.SetDirty(existing);
                generatedCount++;
            }

            // 静止・パルス無し版 GhostRim マテリアル
            string staticPath = $"{MaterialsFolderPath}/M_UIHolo_08_GhostRim_Static.mat";
            Material staticMat = AssetDatabase.LoadAssetAtPath<Material>(staticPath);
            Shader ghostShader = Shader.Find("Custom/Hologram/ModelHologram_GhostRim");
            if (ghostShader != null)
            {
                if (staticMat == null)
                {
                    staticMat = new Material(ghostShader) { name = "M_UIHolo_08_GhostRim_Static" };
                    PlayerPoseStatusUIController.ConfigurePatternSpecificDefaults(staticMat, PlayerPoseStatusUIController.UIHologramPattern.GhostRim, true, true);
                    staticMat.SetFloat("_EnableBreathePulse", 0f);
                    staticMat.SetFloat("_BreatheAmount", 0f);
                    staticMat.SetFloat("_BreatheSpeed", 0f);
                    AssetDatabase.CreateAsset(staticMat, staticPath);
                }
                else
                {
                    staticMat.shader = ghostShader;
                    PlayerPoseStatusUIController.ConfigurePatternSpecificDefaults(staticMat, PlayerPoseStatusUIController.UIHologramPattern.GhostRim, false, true);
                    staticMat.SetFloat("_EnableBreathePulse", 0f);
                    staticMat.SetFloat("_BreatheAmount", 0f);
                    staticMat.SetFloat("_BreatheSpeed", 0f);
                    EditorUtility.SetDirty(staticMat);
                }
                generatedCount++;

                // NoPulse エイリアスアセットも同期
                string noPulsePath = $"{MaterialsFolderPath}/M_UIHolo_08_GhostRim_NoPulse.mat";
                Material noPulseMat = AssetDatabase.LoadAssetAtPath<Material>(noPulsePath);
                if (noPulseMat == null)
                {
                    noPulseMat = new Material(staticMat) { name = "M_UIHolo_08_GhostRim_NoPulse" };
                    AssetDatabase.CreateAsset(noPulseMat, noPulsePath);
                }
                else
                {
                    noPulseMat.CopyPropertiesFromMaterial(staticMat);
                    EditorUtility.SetDirty(noPulseMat);
                }
                generatedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerPoseStatusUI] 全{generatedCount}種類のUIホログラムマテリアルアセットを '{MaterialsFolderPath}' に保存・更新しました。");
        }

        private static void SaveAsCustomMaterial(PlayerPoseStatusUIController controller)
        {
            Material source = null;
            if (controller.MaterialMode == PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial)
            {
                source = controller.CustomMaterial;
            }
            if (source == null)
            {
                string assetPath = (controller.HologramPattern == PlayerPoseStatusUIController.UIHologramPattern.GhostRim && controller.DisableGhostRimPulse)
                    ? PlayerPoseStatusUIController.GetGhostRimMaterialAssetPath(true)
                    : PlayerPoseStatusUIController.GetHologramMaterialAssetPath(controller.HologramPattern);
                source = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            }
            if (source == null && (int)controller.HologramPattern < 11)
            {
                string existingPath = (controller.HologramPattern == PlayerPoseStatusUIController.UIHologramPattern.GhostRim && controller.DisableGhostRimPulse)
                    ? PlayerPoseStatusUIController.GetModelGhostRimMaterialAssetPath(true)
                    : $"{ExistingHoloMaterialsFolderPath}/{PlayerPoseStatusUIController.GetExistingModelHologramFileName((int)controller.HologramPattern)}.mat";
                source = AssetDatabase.LoadAssetAtPath<Material>(existingPath);
            }

            if (!Directory.Exists(MaterialsFolderPath))
            {
                Directory.CreateDirectory(MaterialsFolderPath);
            }

            string defaultName = $"M_UIHolo_Custom_{controller.HologramPattern}";
            string path = EditorUtility.SaveFilePanelInProject("マテリアルを別名保存", defaultName, "mat", "保存先を選択してください", MaterialsFolderPath);
            if (string.IsNullOrEmpty(path)) return;

            Material newMat = null;
            if (source != null)
            {
                newMat = new Material(source);
            }
            else
            {
                Shader s = Shader.Find(PlayerPoseStatusUIController.GetHologramShaderPath(controller.HologramPattern));
                if (s == null) s = Shader.Find("Custom/Hologram/ModelHologram");
                newMat = new Material(s);
                PlayerPoseStatusUIController.ConfigurePatternSpecificDefaults(newMat, controller.HologramPattern, true);
            }

            AssetDatabase.CreateAsset(newMat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(controller, "Assign Saved Custom Material");
            controller.CustomMaterial = newMat;
            controller.MaterialMode = PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial;
            EditorUtility.SetDirty(controller);
            EditorGUIUtility.PingObject(newMat);
            Debug.Log($"[PlayerPoseStatusUI] マテリアルを別名保存して割り当てました: {path}");
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

            // 状態カラー変更のクイック切替（テスト用）
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = controller.EnableStateColorOverride ? new Color(0.4f, 0.95f, 0.6f) : new Color(1.0f, 0.75f, 0.75f);
                string testToggleText = controller.EnableStateColorOverride
                    ? "🎨 状態カラー変化: [ ON (色変更あり) ]"
                    : "🎨 状態カラー変化: [ OFF (色変更なし・元の色維持) ]";
                if (GUILayout.Button(testToggleText, GUILayout.Height(24)))
                {
                    Undo.RecordObject(controller, "Toggle Test State Color Override");
                    controller.EnableStateColorOverride = !controller.EnableStateColorOverride;
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
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

            EditorGUILayout.Space(6);
            if (GUILayout.Button("🧪 全UIシステム・状態カラー・GhostRimパルス無効化 自動テストを実行 (全12項目)", GUILayout.Height(24)))
            {
                UILayoutSystemTest.RunAllTestsAndSaveLog();
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
