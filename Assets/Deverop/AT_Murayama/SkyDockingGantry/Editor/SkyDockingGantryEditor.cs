using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SkyDockingGantryController 用のインスペクター拡張。
/// ・チーム規約厳守：Toolsメニュー（[MenuItem]）は一切追加せず、全操作はインスペクター上のGUIで完結。
/// ・1クリックサイズプリセット（標準12m、ロング24m、テラス据え置き、大型ツインピット）
/// ・1クリック設置スタイル切替（壁面カンチレバー型 / テラス据え置き型 / コーナー抱き込み型）
/// ・1クリックカラーテーマ切替（Clean White / Industrial Navy / Construction Orange / Grating Metal）
/// ・マテリアル別名保存（Save As）
/// ・4種プレハブ・5種マテリアル・4種ネイティブメッシュの一括自動生成・保存
/// ・シーン内への展示用ショーケース（--- SkyDockingGantry Showcase ---）のワンクリック配置
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(SkyDockingGantryController))]
public class SkyDockingGantryEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/SkyDockingGantry";
    private const string ShadersFolder   = BaseFolder + "/Shaders";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    private static bool _showAssetTools = false;

    static SkyDockingGantryEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh trussMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/Gantry_TrussArm.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || trussMesh == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        SkyDockingGantryController comp = (SkyDockingGantryController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【クイック設定】サイズプリセット", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(comp, "🚀 標準カンチレバー\n(12m × 8m)", SkyDockingGantryController.GantryPreset.StandardCantilever, new Color(0.75f, 0.95f, 1f));
            DrawPresetButton(comp, "🏗️ ロング給電アーム\n(24m × 10m)", SkyDockingGantryController.GantryPreset.LongReachGantry, new Color(0.7f, 0.85f, 1f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(comp, "🏢 テラス・屋上据置\n(10m × 8m)", SkyDockingGantryController.GantryPreset.TerraceDeck, new Color(0.85f, 1f, 0.85f));
            DrawPresetButton(comp, "🛸 大型ツインピット\n(16m × 16m)", SkyDockingGantryController.GantryPreset.HeavyTwinPad, new Color(1f, 0.9f, 0.7f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【設置スタイル切替】(ビルの形状に適合)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "壁面カンチレバー", SkyDockingGantryController.GantryStyle.CantileverWall);
            DrawStyleButton(comp, "テラス据え置き", SkyDockingGantryController.GantryStyle.TerraceDeck);
            DrawStyleButton(comp, "コーナー抱き込み", SkyDockingGantryController.GantryStyle.CornerWrap);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【カラーテーマ切替】(URP Lit / 警戒帯連動)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawThemeButton(comp, "Clean White", SkyDockingGantryController.ColorTheme.CleanWhite, new Color(0.92f, 0.94f, 0.98f));
            DrawThemeButton(comp, "Industrial Navy", SkyDockingGantryController.ColorTheme.IndustrialNavy, new Color(0.4f, 0.6f, 0.85f));
            DrawThemeButton(comp, "Construction Orange", SkyDockingGantryController.ColorTheme.ConstructionOrange, new Color(1f, 0.7f, 0.4f));
            DrawThemeButton(comp, "Grating Metal", SkyDockingGantryController.ColorTheme.GratingMetal, new Color(0.75f, 0.78f, 0.82f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【設備・構造トグル】", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawToggleButton(comp.HasRailing, "安全手すり: ON", "安全手すり: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Railing");
                comp.SetRailing(!comp.HasRailing);
                EditorUtility.SetDirty(comp);
            });

            DrawToggleButton(comp.HasPowerArm, "給電アーム: ON", "給電アーム: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Power Arm");
                comp.SetPowerArm(!comp.HasPowerArm);
                EditorUtility.SetDirty(comp);
            });
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawToggleButton(comp.HasGrating, "金網床: ON (透過)", "金網床: OFF (平板)", () =>
            {
                Undo.RecordObject(comp, "Toggle Grating");
                comp.SetGrating(!comp.HasGrating);
                EditorUtility.SetDirty(comp);
            });

            DrawToggleButton(comp.EnableCollider, "天板コライダー: ON", "天板コライダー: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Deck Collider");
                comp.SetEnableCollider(!comp.EnableCollider);
                EditorUtility.SetDirty(comp);
            });
        }

        EditorGUILayout.Space(6f);

        // --- マテリアル別名保存セクション ---
        EditorGUILayout.LabelField("【マテリアル別名保存 (Save As)】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (comp.CustomMaterial != null)
            {
                EditorGUILayout.HelpBox($"現在、別名保存したカスタムマテリアルが適用されています:\n{comp.CustomMaterial.name}", MessageType.Info);
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
                EditorGUILayout.HelpBox("現在の質感や警戒帯設定を新規マテリアル（.mat）として別名保存し、このガントリーに割り当てます。", MessageType.None);
            }

            string defaultPlaceholder = $"M_Gantry_{comp.gameObject.name}";
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

        // --- アセット一括生成 ＆ 展示ショーケースセクション ---
        _showAssetTools = EditorGUILayout.Foldout(_showAssetTools, "【セットアップ・アセット生成 & 展示ショーケース】", true);
        if (_showAssetTools)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.backgroundColor = new Color(0.75f, 0.95f, 1f);
                if (GUILayout.Button("✨ シーン内に展示ショーケース（--- SkyDockingGantry Showcase ---）を配置", GUILayout.Height(30f)))
                {
                    SpawnSkyDockingGantryShowcase(comp.transform.position);
                }

                EditorGUILayout.Space(2f);
                GUI.backgroundColor = new Color(0.85f, 1f, 0.8f);
                if (GUILayout.Button("📁 4種プレハブ・5種マテリアル・メッシュを一括再生成 / 保存", GUILayout.Height(26f)))
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
            comp.RebuildMeshesFromParameters();
            comp.UpdateGantry();
            EditorUtility.SetDirty(comp);
        }

        EditorGUILayout.Space(6f);

        if (GUILayout.Button("メッシュ・マテリアルを再適用", GUILayout.Height(26f)))
        {
            comp.RebuildMeshesFromParameters();
            comp.UpdateGantry();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawPresetButton(SkyDockingGantryController comp, string label, SkyDockingGantryController.GantryPreset preset, Color btnColor)
    {
        GUI.backgroundColor = (comp.Preset == preset) ? new Color(0.4f, 0.9f, 1f) : btnColor;
        if (GUILayout.Button(label, GUILayout.Height(38f)))
        {
            Undo.RecordObject(comp, "Apply Gantry Preset");
            comp.ApplyPreset(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawStyleButton(SkyDockingGantryController comp, string label, SkyDockingGantryController.GantryStyle style)
    {
        GUI.backgroundColor = (comp.Style == style) ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(30f)))
        {
            Undo.RecordObject(comp, "Change Gantry Style");
            comp.SetStyle(style);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawThemeButton(SkyDockingGantryController comp, string label, SkyDockingGantryController.ColorTheme theme, Color btnColor)
    {
        GUI.backgroundColor = (comp.CurrentColorTheme == theme) ? new Color(0.4f, 0.9f, 1f) : btnColor;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Undo.RecordObject(comp, "Apply Color Theme");
            comp.SetColorTheme(theme);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawToggleButton(bool state, string onText, string offText, System.Action onToggle)
    {
        GUI.backgroundColor = state ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.8f, 0.8f);
        if (GUILayout.Button(state ? onText : offText, GUILayout.Height(28f)))
        {
            onToggle?.Invoke();
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SaveAsCustomMaterial(SkyDockingGantryController comp, bool useSavePanel)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        string savePath = "";
        string fileWithoutExt = string.IsNullOrWhiteSpace(comp.CustomSaveName)
            ? $"M_Gantry_{comp.gameObject.name}"
            : comp.CustomSaveName.Trim();

        if (fileWithoutExt.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
        {
            fileWithoutExt = Path.GetFileNameWithoutExtension(fileWithoutExt);
        }

        if (useSavePanel)
        {
            savePath = EditorUtility.SaveFilePanelInProject(
                "ガントリーマテリアルを別名保存",
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

        Shader shader = Shader.Find("Custom/SkyDockingGantry/S_SkyDockingIndustrial");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        Material newMat = new Material(shader);
        newMat.name = fileWithoutExt;

        Color baseCol;
        Color accentCol;
        Color hazA;
        Color hazB;
        float metallic = 0.35f;
        float smoothness = 0.65f;
        float grating = comp.HasGrating ? 1.0f : 0.0f;

        switch (comp.CurrentColorTheme)
        {
            case SkyDockingGantryController.ColorTheme.IndustrialNavy:
                baseCol   = new Color(0.12f, 0.18f, 0.26f, 1.0f);
                accentCol = new Color(0.08f, 0.11f, 0.16f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.08f, 0.10f, 0.12f, 1.0f);
                metallic  = 0.55f;
                smoothness = 0.55f;
                break;
            case SkyDockingGantryController.ColorTheme.ConstructionOrange:
                baseCol   = new Color(0.88f, 0.40f, 0.05f, 1.0f);
                accentCol = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                hazA      = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                hazB      = new Color(0.85f, 0.15f, 0.05f, 1.0f);
                metallic  = 0.25f;
                smoothness = 0.50f;
                break;
            case SkyDockingGantryController.ColorTheme.GratingMetal:
                baseCol   = new Color(0.70f, 0.72f, 0.75f, 1.0f);
                accentCol = new Color(0.20f, 0.22f, 0.26f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                metallic  = 0.80f;
                smoothness = 0.60f;
                grating   = 1.0f;
                break;
            default:
                baseCol   = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                accentCol = new Color(0.16f, 0.18f, 0.22f, 1.0f);
                hazA      = new Color(0.98f, 0.82f, 0.05f, 1.0f);
                hazB      = new Color(0.12f, 0.12f, 0.14f, 1.0f);
                break;
        }

        newMat.SetColor("_BaseColor", baseCol);
        newMat.SetColor("_DarkAccentColor", accentCol);
        newMat.SetColor("_HazardColorA", hazA);
        newMat.SetColor("_HazardColorB", hazB);
        newMat.SetFloat("_HazardEnable", 1.0f);
        newMat.SetFloat("_HazardWidth", 0.08f);
        newMat.SetFloat("_HazardTiling", 24.0f);
        newMat.SetFloat("_GratingEnable", grating);
        newMat.SetFloat("_GratingTiling", 24.0f);
        newMat.SetFloat("_GratingBarThickness", 0.18f);
        newMat.SetFloat("_Cutoff", 0.5f);
        newMat.SetFloat("_TreadEnable", (grating < 0.5f) ? 1.0f : 0.0f);
        newMat.SetFloat("_TreadTiling", 36.0f);
        newMat.SetFloat("_Aspect", Mathf.Max(0.05f, comp.DeckDepth / Mathf.Max(0.1f, comp.DeckWidth)));
        newMat.SetFloat("_Metallic", metallic);
        newMat.SetFloat("_Smoothness", smoothness);

        AssetDatabase.CreateAsset(newMat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material loaded = AssetDatabase.LoadAssetAtPath<Material>(savePath);
        if (loaded != null)
        {
            Undo.RecordObject(comp, "Assign Custom Saved Material");
            comp.CustomMaterial = loaded;
            comp.CustomSaveName = fileWithoutExt;
            EditorUtility.SetDirty(comp);
            EditorGUIUtility.PingObject(loaded);
            Debug.Log($"<color=#70D0FF>[SkyDockingGantry]</color> マテリアルを保存し割り当てました: {savePath}");
        }
    }

    /// <summary>
    /// 4種プレハブ・5種マテリアル・4種メッシュの一括生成
    /// </summary>
    public static void GenerateAllAssets(bool ping = false)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(ShadersFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        EnsureFolder(PrefabsFolder);

        // 1. ネイティブ3Dメッシュ生成
        Mesh bracketMesh = SkyDockingGantryController.CreateWallBracketMesh(8.0f, 4.5f, 0.35f, SkyDockingGantryController.GantryStyle.CantileverWall);
        Mesh trussMesh   = SkyDockingGantryController.CreateTrussArmMesh(12.0f, 8.0f, 4.5f, SkyDockingGantryController.GantryStyle.CantileverWall);
        Mesh deckMesh    = SkyDockingGantryController.CreatePlatformDeckMesh(6.0f, 12.0f, 8.0f, 0.4f);
        Mesh railingMesh = SkyDockingGantryController.CreateRailingMesh(6.0f, 12.0f, 8.0f, 1.1f);
        Mesh powerArmMesh = SkyDockingGantryController.CreatePowerArmMesh(12.0f, 8.0f, 0.4f);

        SaveOrUpdateMesh(bracketMesh,  $"{MeshesFolder}/Gantry_WallBracket.asset");
        SaveOrUpdateMesh(trussMesh,    $"{MeshesFolder}/Gantry_TrussArm.asset");
        SaveOrUpdateMesh(deckMesh,     $"{MeshesFolder}/Gantry_PlatformDeck.asset");
        SaveOrUpdateMesh(railingMesh,  $"{MeshesFolder}/Gantry_Railing.asset");
        SaveOrUpdateMesh(powerArmMesh, $"{MeshesFolder}/Gantry_PowerArm.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh persistentBracket  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Gantry_WallBracket.asset");
        Mesh persistentTruss    = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Gantry_TrussArm.asset");
        Mesh persistentDeck     = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Gantry_PlatformDeck.asset");
        Mesh persistentRailing  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Gantry_Railing.asset");
        Mesh persistentPowerArm = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Gantry_PowerArm.asset");

        // 2. マテリアル生成
        Shader gantryShader = Shader.Find("Custom/SkyDockingGantry/S_SkyDockingIndustrial");
        Shader litShader    = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material matWhite  = CreateOrUpdateGantryMaterial("M_Gantry_CleanWhite", gantryShader,
            new Color(0.92f, 0.94f, 0.96f), new Color(0.16f, 0.18f, 0.22f),
            new Color(0.98f, 0.82f, 0.05f), new Color(0.12f, 0.12f, 0.14f), 0.30f, 0.65f, 0.0f);

        Material matNavy   = CreateOrUpdateGantryMaterial("M_Gantry_IndustrialNavy", gantryShader,
            new Color(0.12f, 0.18f, 0.26f), new Color(0.08f, 0.11f, 0.16f),
            new Color(0.98f, 0.82f, 0.05f), new Color(0.08f, 0.10f, 0.12f), 0.55f, 0.55f, 0.0f);

        Material matOrange = CreateOrUpdateGantryMaterial("M_Gantry_ConstructionOrange", gantryShader,
            new Color(0.88f, 0.40f, 0.05f), new Color(0.12f, 0.12f, 0.14f),
            new Color(1.0f, 1.0f, 1.0f),    new Color(0.85f, 0.15f, 0.05f), 0.25f, 0.50f, 0.0f);

        Material matGrating = CreateOrUpdateGantryMaterial("M_Gantry_GratingFloor", gantryShader,
            new Color(0.70f, 0.72f, 0.75f), new Color(0.20f, 0.22f, 0.26f),
            new Color(0.98f, 0.82f, 0.05f), new Color(0.12f, 0.12f, 0.14f), 0.80f, 0.60f, 1.0f);

        Material matFrameDark = CreateOrUpdateFrameMaterial("M_Gantry_FrameDark", litShader,
            new Color(0.18f, 0.20f, 0.24f), 0.55f, 0.60f);

        // 3. プレハブ組み立て（全4種）
        CreateGantryPrefab("Prefab_Gantry_StandardCantilever.prefab",
            SkyDockingGantryController.GantryPreset.StandardCantilever,
            persistentBracket, persistentTruss, persistentDeck, persistentRailing, persistentPowerArm,
            matWhite, matFrameDark);

        CreateGantryPrefab("Prefab_Gantry_LongReachGantry.prefab",
            SkyDockingGantryController.GantryPreset.LongReachGantry,
            persistentBracket, persistentTruss, persistentDeck, persistentRailing, persistentPowerArm,
            matNavy, matFrameDark);

        CreateGantryPrefab("Prefab_Gantry_TerraceDeck.prefab",
            SkyDockingGantryController.GantryPreset.TerraceDeck,
            persistentBracket, persistentTruss, persistentDeck, persistentRailing, persistentPowerArm,
            matWhite, matFrameDark);

        CreateGantryPrefab("Prefab_Gantry_HeavyTwinPad.prefab",
            SkyDockingGantryController.GantryPreset.HeavyTwinPad,
            persistentBracket, persistentTruss, persistentDeck, persistentRailing, persistentPowerArm,
            matOrange, matFrameDark);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject samplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_Gantry_StandardCantilever.prefab");
            if (samplePrefab != null) EditorGUIUtility.PingObject(samplePrefab);
        }
        Debug.Log("<color=#70D0FF>[SkyDockingGantry]</color> 全アセット（プレハブ4種・マテリアル5種・ネイティブメッシュ5種）を正常に生成・保存しました。");
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

    private static Material CreateOrUpdateGantryMaterial(string matName, Shader shader,
        Color baseCol, Color darkCol, Color hazA, Color hazB, float metallic, float smoothness, float grating)
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

        mat.SetColor("_BaseColor", baseCol);
        mat.SetColor("_DarkAccentColor", darkCol);
        mat.SetColor("_HazardColorA", hazA);
        mat.SetColor("_HazardColorB", hazB);
        mat.SetFloat("_HazardEnable", 1.0f);
        mat.SetFloat("_HazardWidth", 0.08f);
        mat.SetFloat("_HazardTiling", 24.0f);
        mat.SetFloat("_GratingEnable", grating);
        mat.SetFloat("_GratingTiling", 24.0f);
        mat.SetFloat("_GratingBarThickness", 0.18f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_TreadEnable", (grating < 0.5f) ? 1.0f : 0.0f);
        mat.SetFloat("_TreadTiling", 36.0f);
        mat.SetFloat("_Aspect", 0.75f);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

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

    private static void CreateGantryPrefab(string fileName, SkyDockingGantryController.GantryPreset preset,
        Mesh bracketMesh, Mesh trussMesh, Mesh deckMesh, Mesh railingMesh, Mesh powerArmMesh,
        Material deckMat, Material frameMat)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SkyDockingGantryController ctrl = root.AddComponent<SkyDockingGantryController>();

        // 1. WallBracket
        GameObject bracketObj = new GameObject("WallBracket");
        bracketObj.transform.SetParent(root.transform, false);
        MeshFilter bmf = bracketObj.AddComponent<MeshFilter>();
        bmf.sharedMesh = bracketMesh;
        MeshRenderer bmr = bracketObj.AddComponent<MeshRenderer>();
        bmr.sharedMaterial = frameMat;
        bmr.shadowCastingMode = ShadowCastingMode.On;
        bmr.receiveShadows = true;

        // 2. TrussArm
        GameObject trussObj = new GameObject("TrussArm");
        trussObj.transform.SetParent(root.transform, false);
        MeshFilter tmf = trussObj.AddComponent<MeshFilter>();
        tmf.sharedMesh = trussMesh;
        MeshRenderer tmr = trussObj.AddComponent<MeshRenderer>();
        tmr.sharedMaterial = frameMat;
        tmr.shadowCastingMode = ShadowCastingMode.On;
        tmr.receiveShadows = true;

        // 3. PlatformDeck
        GameObject deckObj = new GameObject("PlatformDeck");
        deckObj.transform.SetParent(root.transform, false);
        MeshFilter dmf = deckObj.AddComponent<MeshFilter>();
        dmf.sharedMesh = deckMesh;
        MeshRenderer dmr = deckObj.AddComponent<MeshRenderer>();
        dmr.sharedMaterial = deckMat;
        dmr.shadowCastingMode = ShadowCastingMode.On;
        dmr.receiveShadows = true;

        // 4. Railing
        GameObject railObj = new GameObject("Railing");
        railObj.transform.SetParent(root.transform, false);
        MeshFilter rmf = railObj.AddComponent<MeshFilter>();
        rmf.sharedMesh = railingMesh;
        MeshRenderer rmr = railObj.AddComponent<MeshRenderer>();
        rmr.sharedMaterial = frameMat;
        rmr.shadowCastingMode = ShadowCastingMode.On;
        rmr.receiveShadows = true;

        // 5. PowerArm
        GameObject armObj = new GameObject("PowerArm");
        armObj.transform.SetParent(root.transform, false);
        MeshFilter amf = armObj.AddComponent<MeshFilter>();
        amf.sharedMesh = powerArmMesh;
        MeshRenderer amr = armObj.AddComponent<MeshRenderer>();
        amr.sharedMaterial = frameMat;
        amr.shadowCastingMode = ShadowCastingMode.On;
        amr.receiveShadows = true;

        // Serialized property setup
        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_preset").enumValueIndex = (int)preset;
        so.FindProperty("_wallBracketRenderer").objectReferenceValue = bmr;
        so.FindProperty("_trussArmRenderer").objectReferenceValue = tmr;
        so.FindProperty("_platformDeckRenderer").objectReferenceValue = dmr;
        so.FindProperty("_railingRenderer").objectReferenceValue = rmr;
        so.FindProperty("_powerArmRenderer").objectReferenceValue = amr;
        so.FindProperty("_enableCollider").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.ApplyPreset(preset);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    public static void SpawnSkyDockingGantryShowcase(Vector3 basePosition)
    {
        string rootName = "--- SkyDockingGantry Showcase ---";
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject showcaseRoot = new GameObject(rootName);
        showcaseRoot.transform.position = basePosition + new Vector3(0, 0, 5);
        Undo.RegisterCreatedObjectUndo(showcaseRoot, "Spawn SkyDockingGantry Showcase");

        string[] prefabNames = new string[]
        {
            "Prefab_Gantry_StandardCantilever.prefab",
            "Prefab_Gantry_LongReachGantry.prefab",
            "Prefab_Gantry_TerraceDeck.prefab",
            "Prefab_Gantry_HeavyTwinPad.prefab"
        };

        float spacing = 22.0f;
        float startX = -((prefabNames.Length - 1) * spacing) * 0.5f;

        for (int i = 0; i < prefabNames.Length; i++)
        {
            string prefabPath = $"{PrefabsFolder}/{prefabNames[i]}";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = null;

            if (prefabAsset != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, showcaseRoot.transform);
            }
            else
            {
                instance = new GameObject(Path.GetFileNameWithoutExtension(prefabNames[i]));
                instance.transform.SetParent(showcaseRoot.transform, false);
                SkyDockingGantryController ctrl = instance.AddComponent<SkyDockingGantryController>();
                ctrl.ApplyPreset((SkyDockingGantryController.GantryPreset)i);
            }

            instance.name = $"Showcase_{i + 1:D2}_{Path.GetFileNameWithoutExtension(prefabNames[i]).Replace("Prefab_Gantry_", "")}";
            instance.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);
            instance.transform.localRotation = Quaternion.identity;
        }

        Selection.activeGameObject = showcaseRoot;
        EditorGUIUtility.PingObject(showcaseRoot);
        Debug.Log("<color=#70D0FF>[SkyDockingGantry]</color> 展示ショーケースをシーン内に配置しました。");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
