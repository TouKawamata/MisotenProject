using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseMinimapController))]
public class CourseMinimapControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CourseMinimapController controller = (CourseMinimapController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("① 表示方式の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isModeA = controller.GetDisplayMode() == CourseMinimapController.MinimapDisplayMode.ModeA_RenderTexture;
            GUI.backgroundColor = isModeA ? new Color(0.3f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button("方式A：サブカメラ ＋ UI (RenderTexture)", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap to Mode A");
                controller.SetDisplayMode(CourseMinimapController.MinimapDisplayMode.ModeA_RenderTexture);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = !isModeA ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button("方式B：カメラ前 3Dミニチュアホログラム", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap to Mode B");
                controller.SetDisplayMode(CourseMinimapController.MinimapDisplayMode.ModeB_3DMiniature);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 表示範囲（ズーム ⇄ 全体）の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isAround = controller.GetScopeMode() == CourseMinimapController.MinimapScopeMode.AroundPlayer;
            GUI.backgroundColor = isAround ? new Color(1f, 0.85f, 0.35f) : Color.white;
            if (GUILayout.Button("🔍 周辺ズーム（自機中心・周囲切り抜き）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Scope to AroundPlayer");
                controller.SetScopeMode(CourseMinimapController.MinimapScopeMode.AroundPlayer);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = !isAround ? new Color(0.95f, 0.55f, 1f) : Color.white;
            if (GUILayout.Button("🗺️ コース全体マップ（全域俯瞰）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Scope to WholeCourse");
                controller.SetScopeMode(CourseMinimapController.MinimapScopeMode.WholeCourse);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        // コース全体マップ 視点・回転アングル調整パネル
        bool isWholeCourse = controller.GetScopeMode() == CourseMinimapController.MinimapScopeMode.WholeCourse;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("🗺️【コース全体マップ 視点・回転角度設定 (WholeCourse Map Rotation)】", EditorStyles.boldLabel);
            if (!isWholeCourse)
            {
                EditorGUILayout.HelpBox("※現在「周辺ズーム」表示中です。上の「🗺️ コース全体マップ」を選択すると本設定の角度で全体マップが表示されます。", MessageType.None);
            }

            EditorGUI.BeginChangeCheck();
            float newYaw = EditorGUILayout.Slider("水平回転角度 (Yaw: 0〜360°)", controller.WholeCourseMapRotation, 0f, 360f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change WholeCourse Map Rotation");
                controller.SetWholeCourseMapRotation(newYaw, snapImmediately: true);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }

            // 4方位クイックアングルボタン
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAnglePresetButton(controller, "↑ 北 (0°)", 0f);
                DrawAnglePresetButton(controller, "→ 東 (90°)", 90f);
                DrawAnglePresetButton(controller, "↓ 南 (180°)", 180f);
                DrawAnglePresetButton(controller, "← 西 (270°)", 270f);
            }

            // ステップ回転ボタン
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↺ -45°", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(controller, "Rotate WholeCourse -45");
                    controller.RotateWholeCourseMap(-45f, snapImmediately: true);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("↺ -15°", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(controller, "Rotate WholeCourse -15");
                    controller.RotateWholeCourseMap(-15f, snapImmediately: true);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("↻ +15°", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(controller, "Rotate WholeCourse +15");
                    controller.RotateWholeCourseMap(15f, snapImmediately: true);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("↻ +45°", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(controller, "Rotate WholeCourse +45");
                    controller.RotateWholeCourseMap(45f, snapImmediately: true);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("🔄 0°リセット", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(controller, "Reset WholeCourse Rotation");
                    controller.ResetWholeCourseMapRotation(snapImmediately: true);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUI.BeginChangeCheck();
            bool newRotateInWhole = EditorGUILayout.ToggleLeft("🔄 コース全体時も自機の進行方向に合わせて回す（自機追従）", controller.RotateInWholeCourse);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle Rotate In WholeCourse");
                controller.RotateInWholeCourse = newRotateInWhole;
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("③ コース形状（帯リボン ⇄ 円柱トンネル）の切り替え", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool isCylinder = controller.GetMeshShape() == CourseMinimapController.MinimapMeshShape.CylinderTube;
            GUI.backgroundColor = !isCylinder ? new Color(0.5f, 0.9f, 0.9f) : Color.white;
            if (GUILayout.Button("🛣️ 平らな帯（フラットリボン）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Shape to FlatRibbon");
                controller.SetMeshShape(CourseMinimapController.MinimapMeshShape.FlatRibbon);
                EditorUtility.SetDirty(controller);
            }

            GUI.backgroundColor = isCylinder ? new Color(1f, 0.65f, 0.3f) : Color.white;
            if (GUILayout.Button("⭕ 円柱トンネル（CourseSplineギズモ風チューブ）", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Switch Minimap Shape to CylinderTube");
                controller.SetMeshShape(CourseMinimapController.MinimapMeshShape.CylinderTube);
                EditorUtility.SetDirty(controller);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6f);
        DrawScreenPositionAndLayoutSection(controller);

        EditorGUILayout.Space(6f);

        // コース・分岐の検出ステータス表示
        string mainName = controller.CourseSpline != null ? controller.CourseSpline.name : "未検出";
        string netName = controller.CourseNetwork != null ? controller.CourseNetwork.name : "なし（シーン内自動検出）";
        int branchCount = controller.ActiveBranchCount;
        EditorGUILayout.HelpBox(
            $"【コース＆分岐ステータス】\n" +
            $"・メインスプライン : {mainName}\n" +
            $"・CourseNetwork : {netName}\n" +
            $"・結合済み分岐ルート数 : {branchCount} 本（分岐を追加・編集した際は下の Rebuild を押してください）",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Rebuild（ミニマップ＆分岐メッシュ・アイコン再生成）", GUILayout.Height(32f)))
        {
            foreach (Object targetObject in targets)
            {
                CourseMinimapController c = (CourseMinimapController)targetObject;
                Undo.RecordObject(c, "Rebuild CourseMinimap");
                c.Rebuild();
                EditorUtility.SetDirty(c);
            }
        }
    }

    private void DrawScreenPositionAndLayoutSection(CourseMinimapController controller)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📐 画面配置・レイアウト直感調整 (Screen Placement)", EditorStyles.boldLabel);

        bool isModeA = controller.GetDisplayMode() == CourseMinimapController.MinimapDisplayMode.ModeA_RenderTexture;

        // Visual screen preview box (16:9)
        DrawMinimapScreenPreview(controller, isModeA);
        EditorGUILayout.Space(6f);

        if (isModeA)
        {
            CourseMinimapController.MinimapScreenAnchor curAnchor = controller.GetScreenAnchor();
            EditorGUILayout.LabelField($"【方式A Canvas UI 配置プリセット】 現在：【 {curAnchor} 】", EditorStyles.miniBoldLabel);

            // アンカーグリッド:
            // 上段: 左上 | 上中央 | 右上 (レースゲーム標準)
            EditorGUILayout.BeginHorizontal();
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.TopLeft, "↖ 左上\n(Top-Left)");
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.TopCenter, "⬆ 上中央\n(Top-Center)");
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.TopRight, "↗ 右上 (レース標準)\n(Top-Right)");
            EditorGUILayout.EndHorizontal();

            // 下段: 左下 | 下中央 | 右下
            EditorGUILayout.BeginHorizontal();
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomLeft, "↙ 左下 (標準)\n(Bottom-Left)");
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomCenter, "⬇ 下中央\n(Bottom-Center)");
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomRight, "↘ 右下\n(Bottom-Right)");
            EditorGUILayout.EndHorizontal();

            // 特殊: 画面中央 | カスタム自由配置
            EditorGUILayout.BeginHorizontal();
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.Center, "🎯 画面中央\n(Center)");
            DrawMinimapAnchorButton(controller, CourseMinimapController.MinimapScreenAnchor.Custom, "⚙️ カスタム自由配置\n(Custom)");
            EditorGUILayout.EndHorizontal();

            if (curAnchor == CourseMinimapController.MinimapScreenAnchor.Custom)
            {
                EditorGUILayout.Space(4f);
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
                }
            }

            EditorGUILayout.Space(4f);

            // 表示サイズ
            EditorGUILayout.LabelField("【UI表示サイズ (Size)】", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            Vector2 newSize = EditorGUILayout.Vector2Field("サイズ (幅 x 高さ px)", controller.UISize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Minimap UI Size");
                controller.UISize = newSize;
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("小 (220×220)", GUILayout.Height(22)))
            {
                Undo.RecordObject(controller, "Set Minimap UI Size Small");
                controller.UISize = new Vector2(220f, 220f);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            if (GUILayout.Button("中 (280×280・標準)", GUILayout.Height(22)))
            {
                Undo.RecordObject(controller, "Set Minimap UI Size Medium");
                controller.UISize = new Vector2(280f, 280f);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            if (GUILayout.Button("大 (360×360)", GUILayout.Height(22)))
            {
                Undo.RecordObject(controller, "Set Minimap UI Size Large");
                controller.UISize = new Vector2(360f, 360f);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // マージン・オフセット
            bool isCenterBased = (curAnchor == CourseMinimapController.MinimapScreenAnchor.BottomCenter ||
                                  curAnchor == CourseMinimapController.MinimapScreenAnchor.TopCenter ||
                                  curAnchor == CourseMinimapController.MinimapScreenAnchor.Center);
            string labelX = isCenterBased ? "↔️ 水平微調整オフセット (X: 0で完全中央)" : "↔️ 画面横端からのマージン (X)";
            string labelY = isCenterBased ? "↕️ 画面端からの縦マージン (Y)" : "↕️ 画面縦端からのマージン (Y)";

            EditorGUILayout.LabelField("【マージン ＆ オフセット調整】", EditorStyles.miniBoldLabel);
            Vector2 curMargin = controller.UIMargin;

            EditorGUI.BeginChangeCheck();
            float newX = EditorGUILayout.Slider(labelX, curMargin.x, isCenterBased ? -400f : 0f, isCenterBased ? 400f : 200f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Minimap UI Margin X");
                controller.UIMargin = new Vector2(newX, curMargin.y);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            if (isCenterBased && Mathf.Abs(curMargin.x) > 0.01f)
            {
                if (GUILayout.Button("🎯 横オフセットを 0 にリセット (完全中央揃え)", GUILayout.Height(20)))
                {
                    Undo.RecordObject(controller, "Reset Minimap UI Margin X");
                    controller.UIMargin = new Vector2(0f, curMargin.y);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            }

            EditorGUI.BeginChangeCheck();
            float newY = EditorGUILayout.Slider(labelY, curMargin.y, 0f, 250f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Minimap UI Margin Y");
                controller.UIMargin = new Vector2(controller.UIMargin.x, newY);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
        else
        {
            // ── 方式B：メインカメラ前 3Dミニチュア ──
            EditorGUILayout.LabelField("【方式B カメラ前 3Dミニチュア配置プリセット】", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.TopLeft, "↖ 左上");
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.TopCenter, "⬆ 上中央");
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.TopRight, "↗ 右上");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomLeft, "↙ 左下 (コックピット前)");
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomCenter, "⬇ 下中央");
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.BottomRight, "↘ 右下");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawMinimapModeBPresetButton(controller, CourseMinimapController.MinimapScreenAnchor.Center, "🎯 画面中央");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // 詳細スライダー
            Vector3 curOffset = controller.MiniatureScreenOffset;
            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.Slider("↔️ 水平オフセット X (左 - / 右 +)", curOffset.x, -0.8f, 0.8f);
            float y = EditorGUILayout.Slider("↕️ 上下オフセット Y (下 - / 上 +)", curOffset.y, -0.5f, 0.5f);
            float z = EditorGUILayout.Slider("🔍 前方距離 Z (m)", curOffset.z, 0.4f, 2.5f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Miniature Screen Offset");
                controller.MiniatureScreenOffset = new Vector3(x, y, z);
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            if (Mathf.Abs(curOffset.x) > 0.01f)
            {
                if (GUILayout.Button("🎯 水平オフセット X を 0 にリセット (完全中央揃え)", GUILayout.Height(20)))
                {
                    Undo.RecordObject(controller, "Reset Miniature Screen Offset X");
                    controller.MiniatureScreenOffset = new Vector3(0f, curOffset.y, curOffset.z);
                    EditorUtility.SetDirty(controller);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            }

            EditorGUI.BeginChangeCheck();
            float scale = EditorGUILayout.Slider("🔬 ミニチュア縮小スケール", controller.MiniatureScale, 0.0005f, 0.005f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Change Miniature Scale");
                controller.MiniatureScale = scale;
                EditorUtility.SetDirty(controller);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawMinimapScreenPreview(CourseMinimapController controller, bool isModeA)
    {
        EditorGUILayout.LabelField("🖥️ 画面レイアウトプレビュー (16:9 Screen Guide)", EditorStyles.miniBoldLabel);

        float screenW = 200f;
        float screenH = 112.5f; // 16:9 ratio
        Rect totalRect = GUILayoutUtility.GetRect(0f, screenH + 8f, GUILayout.ExpandWidth(true));
        float startX = totalRect.x + (totalRect.width - screenW) * 0.5f;
        float startY = totalRect.y + 4f;
        Rect screenRect = new Rect(startX, startY, screenW, screenH);

        // Screen background
        EditorGUI.DrawRect(screenRect, new Color(0.08f, 0.10f, 0.14f, 1f));

        // Center crosshair lines
        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        Handles.DrawLine(new Vector3(screenRect.x, screenRect.y + screenH * 0.5f, 0), new Vector3(screenRect.x + screenW, screenRect.y + screenH * 0.5f, 0));
        Handles.DrawLine(new Vector3(screenRect.x + screenW * 0.5f, screenRect.y, 0), new Vector3(screenRect.x + screenW * 0.5f, screenRect.y + screenH, 0));

        // Screen frame border
        Handles.color = new Color(0.35f, 0.45f, 0.55f, 0.8f);
        Handles.DrawPolyLine(
            new Vector3(screenRect.x, screenRect.y, 0),
            new Vector3(screenRect.x + screenW, screenRect.y, 0),
            new Vector3(screenRect.x + screenW, screenRect.y + screenH, 0),
            new Vector3(screenRect.x, screenRect.y + screenH, 0),
            new Vector3(screenRect.x, screenRect.y, 0)
        );

        // Player Status UI hint (bottom center) to visualize relative layout
        Rect playerUIHint = new Rect(screenRect.x + screenW * 0.5f - 18f, screenRect.y + screenH - 42f, 36f, 40f);
        EditorGUI.DrawRect(playerUIHint, new Color(1f, 0.75f, 0.2f, 0.15f));
        Handles.color = new Color(1f, 0.75f, 0.2f, 0.4f);
        Handles.DrawPolyLine(
            new Vector3(playerUIHint.x, playerUIHint.y, 0),
            new Vector3(playerUIHint.x + playerUIHint.width, playerUIHint.y, 0),
            new Vector3(playerUIHint.x + playerUIHint.width, playerUIHint.y + playerUIHint.height, 0),
            new Vector3(playerUIHint.x, playerUIHint.y + playerUIHint.height, 0),
            new Vector3(playerUIHint.x, playerUIHint.y, 0)
        );
        GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.75f, 0.2f, 0.6f) },
            fontSize = 8
        };
        GUI.Label(playerUIHint, "Player UI\n(画面下中央)", hintStyle);

        // Minimap element rect
        Rect elemRect;
        if (isModeA)
        {
            CourseMinimapController.MinimapScreenAnchor anchor = controller.GetScreenAnchor();
            Vector2 normAnchor;
            Vector2 pivot;

            switch (anchor)
            {
                case CourseMinimapController.MinimapScreenAnchor.BottomLeft:
                    normAnchor = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f); break;
                case CourseMinimapController.MinimapScreenAnchor.BottomRight:
                    normAnchor = new Vector2(1f, 0f); pivot = new Vector2(1f, 0f); break;
                case CourseMinimapController.MinimapScreenAnchor.TopLeft:
                    normAnchor = new Vector2(0f, 1f); pivot = new Vector2(0f, 1f); break;
                case CourseMinimapController.MinimapScreenAnchor.TopRight:
                    normAnchor = new Vector2(1f, 1f); pivot = new Vector2(1f, 1f); break;
                case CourseMinimapController.MinimapScreenAnchor.BottomCenter:
                    normAnchor = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 0f); break;
                case CourseMinimapController.MinimapScreenAnchor.TopCenter:
                    normAnchor = new Vector2(0.5f, 1f); pivot = new Vector2(0.5f, 1f); break;
                case CourseMinimapController.MinimapScreenAnchor.Center:
                    normAnchor = new Vector2(0.5f, 0.5f); pivot = new Vector2(0.5f, 0.5f); break;
                case CourseMinimapController.MinimapScreenAnchor.Custom:
                    normAnchor = controller.CustomAnchor; pivot = controller.CustomPivot; break;
                default:
                    normAnchor = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f); break;
            }

            float scale = screenW / 1920f;
            float elemW = Mathf.Clamp(controller.UISize.x * scale, 18f, 55f);
            float elemH = Mathf.Clamp(controller.UISize.y * scale, 18f, 55f);

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

            elemRect = new Rect(px, py, elemW, elemH);
        }
        else
        {
            // Mode B 3D Miniature
            Vector3 off = controller.MiniatureScreenOffset;
            float normX = Mathf.Clamp01((off.x / 0.84f) * 0.5f + 0.5f);
            float normY = Mathf.Clamp01((off.y / 0.48f) * 0.5f + 0.5f);

            float elemW = 34f;
            float elemH = 34f;
            float px = screenRect.x + normX * (screenRect.width - elemW);
            float py = screenRect.y + (1f - normY) * (screenRect.height - elemH);
            elemRect = new Rect(px, py, elemW, elemH);
        }

        // Draw Minimap box (Cyan glow)
        EditorGUI.DrawRect(elemRect, new Color(0.15f, 0.75f, 1f, 0.45f));
        Handles.color = new Color(0.35f, 0.95f, 1f, 0.95f);
        Handles.DrawPolyLine(
            new Vector3(elemRect.x, elemRect.y, 0),
            new Vector3(elemRect.x + elemRect.width, elemRect.y, 0),
            new Vector3(elemRect.x + elemRect.width, elemRect.y + elemRect.height, 0),
            new Vector3(elemRect.x, elemRect.y + elemRect.height, 0),
            new Vector3(elemRect.x, elemRect.y, 0)
        );

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontSize = 9
        };
        GUI.Label(elemRect, isModeA ? "🗺️ Minimap" : "🗺️ 3D", style);
    }

    private void DrawMinimapAnchorButton(CourseMinimapController controller, CourseMinimapController.MinimapScreenAnchor anchor, string label)
    {
        bool isSelected = controller.GetScreenAnchor() == anchor;
        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = isSelected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.88f, 0.88f, 0.88f);
        if (GUILayout.Button(label, GUILayout.Height(32)))
        {
            Undo.RecordObject(controller, $"Set Minimap Screen Anchor to {anchor}");
            controller.SetScreenAnchor(anchor, true);
            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        GUI.backgroundColor = origBg;
    }

    private void DrawMinimapModeBPresetButton(CourseMinimapController controller, CourseMinimapController.MinimapScreenAnchor anchor, string label)
    {
        bool isSelected = false;
        Vector3 cur = controller.MiniatureScreenOffset;
        switch (anchor)
        {
            case CourseMinimapController.MinimapScreenAnchor.BottomLeft:
                isSelected = Mathf.Abs(cur.x - (-0.42f)) < 0.05f && Mathf.Abs(cur.y - (-0.24f)) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.BottomRight:
                isSelected = Mathf.Abs(cur.x - 0.42f) < 0.05f && Mathf.Abs(cur.y - (-0.24f)) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.BottomCenter:
                isSelected = Mathf.Abs(cur.x) < 0.05f && Mathf.Abs(cur.y - (-0.24f)) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.TopLeft:
                isSelected = Mathf.Abs(cur.x - (-0.42f)) < 0.05f && Mathf.Abs(cur.y - 0.24f) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.TopRight:
                isSelected = Mathf.Abs(cur.x - 0.42f) < 0.05f && Mathf.Abs(cur.y - 0.24f) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.TopCenter:
                isSelected = Mathf.Abs(cur.x) < 0.05f && Mathf.Abs(cur.y - 0.24f) < 0.05f;
                break;
            case CourseMinimapController.MinimapScreenAnchor.Center:
                isSelected = Mathf.Abs(cur.x) < 0.05f && Mathf.Abs(cur.y) < 0.05f;
                break;
        }

        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = isSelected ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.88f, 0.92f, 1.0f);
        if (GUILayout.Button(label, GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, $"Apply Mode B Miniature Preset {anchor}");
            controller.ApplyModeBMiniaturePreset(anchor);
            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        GUI.backgroundColor = origBg;
    }

    private static void DrawAnglePresetButton(CourseMinimapController controller, string label, float angle)
    {
        bool isCurrent = Mathf.Approximately(Mathf.Repeat(controller.WholeCourseMapRotation, 360f), angle);
        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = isCurrent ? new Color(0.4f, 0.95f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
        {
            Undo.RecordObject(controller, $"Set WholeCourse Angle to {angle}");
            controller.SetWholeCourseMapRotation(angle, snapImmediately: true);
            EditorUtility.SetDirty(controller);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = origBg;
    }
}
