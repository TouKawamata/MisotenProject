using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SmartSignageController 用のインスペクター拡張。
/// ・チーム規約遵守：Toolsメニュー（[MenuItem]）は一切追加せず、全操作はインスペクター上のGUIで完結。
/// ・1クリック機能プリセット（テレメトリHUD、気象、順位表、ルート案内）
/// ・1クリックアスペクト比調整（16:9, 21:9, 4:3, 1:1, 9:16）
/// ・1クリックUI発光カラー調整（ソフトシアン、ピュアホワイト、ホログラムスカイ、エアロライム、サンライズゴールド）
/// ・マテリアル別名保存（Save As）
/// ・4種プレハブ・6種マテリアル・4種メッシュ・4種2Kテクスチャの一括自動生成
/// ・シーン内への展示用ショーケース（--- SmartSignage Showcase ---）のワンクリック配置
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(SmartSignageController))]
public class SmartSignageControllerEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/SmartSignage";
    private const string ShadersFolder   = BaseFolder + "/Shaders";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string TexturesFolder  = BaseFolder + "/Textures";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    private static bool _showAssetTools = false;

    static SmartSignageControllerEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh screenMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/SmartSign_Screen_RoundedRect.asset");
            Texture2D hudTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesFolder + "/Tex_SmartSign_TelemetryHUD.png");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || screenMesh == null || hudTex == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        SmartSignageController comp = (SmartSignageController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【クイック設定】機能プリセット", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(comp, "🚀 テレメトリHUD\n(高度/速度/波形)", SmartSignageController.SignagePreset.TelemetryHUD, new Color(0.75f, 0.95f, 1f));
            DrawPresetButton(comp, "💨 気象ステーション\n(風向/気圧/レーダー)", SmartSignageController.SignagePreset.WeatherStation, new Color(0.7f, 0.9f, 1f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(comp, "🏁 レース順位表\n(ラップ/順位/タイム)", SmartSignageController.SignagePreset.RaceLeaderboard, new Color(1f, 0.95f, 0.75f));
            DrawPresetButton(comp, "🧭 ルート航法\n(空路/ウェイポイント)", SmartSignageController.SignagePreset.RouteGuide, new Color(0.75f, 1f, 0.85f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【ワンタッチ比率調整】アスペクト比プリセット", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAspectButton(comp, "16:9\n(ワイド)", SmartSignageController.AspectRatioPreset.Ratio_16_9);
            DrawAspectButton(comp, "21:9\n(超ワイド)", SmartSignageController.AspectRatioPreset.Ratio_21_9);
            DrawAspectButton(comp, "4:3\n(HUD)", SmartSignageController.AspectRatioPreset.Ratio_4_3);
            DrawAspectButton(comp, "1:1\n(スクエア)", SmartSignageController.AspectRatioPreset.Ratio_1_1);
            DrawAspectButton(comp, "9:16\n(縦サイネージ)", SmartSignageController.AspectRatioPreset.Ratio_9_16);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【UIカラープリセット】(HDR Bloom連動)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawColorButton(comp, "Soft Cyan", SmartSignageController.UIColorPreset.SoftCyan, new Color(0.78f, 0.94f, 1.0f));
            DrawColorButton(comp, "Pure White", SmartSignageController.UIColorPreset.PureWhite, Color.white);
            DrawColorButton(comp, "Holo Sky", SmartSignageController.UIColorPreset.HologramSky, new Color(0.44f, 0.82f, 1.0f));
            DrawColorButton(comp, "Aero Lime", SmartSignageController.UIColorPreset.AeroLime, new Color(0.44f, 1.0f, 0.72f));
            DrawColorButton(comp, "Sunrise Gold", SmartSignageController.UIColorPreset.SunriseGold, new Color(1.0f, 0.88f, 0.50f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【外枠・マウント金具の切り替え】", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = comp.ShowFrame ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.8f, 0.8f);
            string frameText = comp.ShowFrame ? "枠・金具・パイロン: [ ON ] (クリックで非表示)" : "枠・金具・パイロン: [ OFF ] (クリックで表示)";
            if (GUILayout.Button(frameText, GUILayout.Height(28f)))
            {
                Undo.RecordObject(comp, "Toggle Signage Frame");
                comp.SetShowFrame(!comp.ShowFrame);
                EditorUtility.SetDirty(comp);
            }
            GUI.backgroundColor = Color.white;
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
                EditorGUILayout.HelpBox("インスペクターで調整したフロストガラス質感やUI色を新規マテリアル（.mat）として別名保存し、このサイネージに割り当てます。", MessageType.None);
            }

            string defaultPlaceholder = $"M_SmartSign_{comp.gameObject.name}";
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
                if (GUILayout.Button("✨ シーン内に展示ショーケース（--- SmartSignage Showcase ---）を配置", GUILayout.Height(30f)))
                {
                    SpawnSmartSignageShowcase(comp.transform.position);
                }

                EditorGUILayout.Space(2f);
                GUI.backgroundColor = new Color(0.85f, 1f, 0.8f);
                if (GUILayout.Button("📁 4種プレハブ・6種マテリアル・メッシュ・テクスチャを一括再生成 / 保存", GUILayout.Height(26f)))
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

        EditorGUILayout.Space(6f);

        if (GUILayout.Button("マテリアル・表示を再適用", GUILayout.Height(26f)))
        {
            comp.UpdateSignboard();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawPresetButton(SmartSignageController comp, string label, SmartSignageController.SignagePreset preset, Color btnColor)
    {
        GUI.backgroundColor = (comp.Preset == preset) ? new Color(0.4f, 0.9f, 1f) : btnColor;
        if (GUILayout.Button(label, GUILayout.Height(38f)))
        {
            Undo.RecordObject(comp, "Apply SmartSign Preset");
            comp.ApplyPreset(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawAspectButton(SmartSignageController comp, string label, SmartSignageController.AspectRatioPreset preset)
    {
        GUI.backgroundColor = (comp.AspectRatio == preset) ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(32f)))
        {
            Undo.RecordObject(comp, "Change Signage Aspect Ratio");
            comp.SetAspectRatio(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawColorButton(SmartSignageController comp, string label, SmartSignageController.UIColorPreset preset, Color btnColor)
    {
        GUI.backgroundColor = btnColor;
        if (GUILayout.Button(label, GUILayout.Height(26f)))
        {
            Undo.RecordObject(comp, "Apply UI Color Preset");
            comp.ApplyUIColor(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SaveAsCustomMaterial(SmartSignageController comp, bool useSavePanel)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        string savePath = "";
        string fileWithoutExt = string.IsNullOrWhiteSpace(comp.CustomSaveName)
            ? $"M_SmartSign_{comp.gameObject.name}"
            : comp.CustomSaveName.Trim();

        if (fileWithoutExt.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
        {
            fileWithoutExt = Path.GetFileNameWithoutExtension(fileWithoutExt);
        }

        if (useSavePanel)
        {
            savePath = EditorUtility.SaveFilePanelInProject(
                "スマートサイネージマテリアルを別名保存",
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
            Undo.RecordObject(comp, "Assign Custom SmartSign Material");
            comp.CustomMaterial = loadedMat;
            comp.CustomSaveName = fileWithoutExt;
            comp.UpdateSignboard();
            EditorUtility.SetDirty(comp);
            EditorGUIUtility.PingObject(loadedMat);
            Debug.Log($"<color=#70D0FF>[SmartSignage]</color> マテリアルを保存し適用しました: {savePath}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // アセット一括生成（プレハブ・マテリアル・メッシュ・テクスチャ）
    // ─────────────────────────────────────────────────────────────

    public static void GenerateAllAssets(bool ping = true)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(ShadersFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        EnsureFolder(TexturesFolder);
        EnsureFolder(PrefabsFolder);

        // 1. テクスチャの生成
        GeneratePresetTextures();

        // 2. メッシュの生成
        Mesh screenMesh = SmartSignageController.CreateRoundedScreenMesh(1.0f, 1.0f, 0.08f, 0.02f);
        Mesh frameMesh  = SmartSignageController.CreateAeroFrameMesh(1.0f, 1.0f, 0.08f, 0.04f, 0.035f);
        Mesh pylonMesh  = SmartSignageController.CreateFloatingPylonsMesh(1.0f, 1.0f);
        Mesh mountMesh  = SmartSignageController.CreateWallMountMesh(1.0f, 1.0f);

        SaveOrUpdateMesh(screenMesh, $"{MeshesFolder}/SmartSign_Screen_RoundedRect.asset");
        SaveOrUpdateMesh(frameMesh,  $"{MeshesFolder}/SmartSign_AeroFrame.asset");
        SaveOrUpdateMesh(pylonMesh,  $"{MeshesFolder}/SmartSign_FloatingPylons.asset");
        SaveOrUpdateMesh(mountMesh,  $"{MeshesFolder}/SmartSign_WallMount.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh persistentScreen = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartSign_Screen_RoundedRect.asset");
        Mesh persistentFrame  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartSign_AeroFrame.asset");
        Mesh persistentPylon  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartSign_FloatingPylons.asset");
        Mesh persistentMount  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/SmartSign_WallMount.asset");

        // 3. マテリアルの生成
        Shader signShader = Shader.Find("Custom/SmartSignage/S_FrostedGlassSignboard");
        Shader litShader  = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Texture2D texHUD     = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/Tex_SmartSign_TelemetryHUD.png");
        Texture2D texWeather = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/Tex_SmartSign_WeatherStation.png");
        Texture2D texRace    = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/Tex_SmartSign_RaceLeaderboard.png");
        Texture2D texRoute   = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/Tex_SmartSign_RouteGuide.png");

        Material matHUD     = CreateOrUpdateSignMaterial("M_SmartSign_TelemetryHUD", signShader, texHUD, new Color(0.85f, 0.97f, 1.0f, 1.0f), 2.4f, 0.015f);
        Material matWeather = CreateOrUpdateSignMaterial("M_SmartSign_WeatherStation", signShader, texWeather, new Color(0.70f, 0.92f, 1.0f, 1.0f), 2.2f, 0.018f);
        Material matRace    = CreateOrUpdateSignMaterial("M_SmartSign_RaceLeaderboard", signShader, texRace, new Color(1.0f, 1.0f, 1.0f, 1.0f), 2.6f, 0.012f);
        Material matRoute   = CreateOrUpdateSignMaterial("M_SmartSign_RouteGuide", signShader, texRoute, new Color(0.65f, 1.0f, 0.85f, 1.0f), 2.5f, 0.016f);

        Material matFrameWhite = CreateOrUpdateFrameMaterial("M_SmartSign_AeroFrame_White", litShader, new Color(0.95f, 0.97f, 1.0f), 0.1f, 0.9f);
        Material matFrameDark  = CreateOrUpdateFrameMaterial("M_SmartSign_AeroFrame_Dark", litShader, new Color(0.12f, 0.14f, 0.16f), 0.7f, 0.75f);

        // 4. プレハブの生成（全4種）
        CreateScreenOnlyPrefab("Prefab_SmartSign_ScreenOnly.prefab", persistentScreen, persistentFrame, matHUD, matFrameWhite);
        CreateFramedPrefab("Prefab_SmartSign_Framed.prefab", persistentScreen, persistentFrame, matHUD, matFrameWhite);
        CreateFloatingPrefab("Prefab_SmartSign_Floating.prefab", persistentScreen, persistentPylon, matHUD, matFrameWhite);
        CreateWallMountPrefab("Prefab_SmartSign_WallMount.prefab", persistentScreen, persistentMount, matHUD, matFrameWhite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject samplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_SmartSign_ScreenOnly.prefab");
            if (samplePrefab != null) EditorGUIUtility.PingObject(samplePrefab);
        }
        Debug.Log("<color=#70D0FF>[SmartSignage]</color> 全アセット（プレハブ4種・マテリアル6種・メッシュ4種・2Kテクスチャ4種）を正常に生成・保存しました。");
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

    private static Material CreateOrUpdateSignMaterial(string matName, Shader shader, Texture2D tex, Color uiColor, float emission, float blurRadius)
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

        mat.SetFloat("_UseSceneBlur", 1.0f);
        mat.SetFloat("_BlurRadius", blurRadius);
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

        if (tex != null)
        {
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_HasCustomTex", 1.0f);
        }
        mat.SetFloat("_AspectRatio", 1.7777f);
        mat.SetColor("_UIColor", uiColor);
        mat.SetFloat("_UIIntensity", emission);
        mat.SetFloat("_UIOpacity", 0.95f);
        mat.SetFloat("_DynamicTelemetry", 1.0f);
        mat.SetFloat("_Cull", 2.0f);
        mat.SetFloat("_SrcBlend", 5.0f);
        mat.SetFloat("_DstBlend", 10.0f);
        mat.SetFloat("_ZWrite", 0.0f);
        mat.SetFloat("_ZTest", 4.0f);

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

    // ─────────────────────────────────────────────────────────────
    // プレハブ組み立て API（全4形状）
    // ─────────────────────────────────────────────────────────────

    private static void CreateScreenOnlyPrefab(string fileName, Mesh screenMesh, Mesh frameMesh, Material signMat, Material frameMat)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SmartSignageController ctrl = root.AddComponent<SmartSignageController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject frameObj = new GameObject("Frame");
        frameObj.transform.SetParent(root.transform, false);
        MeshFilter fmf = frameObj.AddComponent<MeshFilter>();
        fmf.sharedMesh = frameMesh;
        MeshRenderer fmr = frameObj.AddComponent<MeshRenderer>();
        fmr.sharedMaterial = frameMat;
        fmr.shadowCastingMode = ShadowCastingMode.On;
        fmr.receiveShadows = true;
        frameObj.SetActive(false);

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_style").enumValueIndex = (int)SmartSignageController.SignageStyle.ScreenOnly;
        so.FindProperty("_showFrame").boolValue = false;
        so.FindProperty("_signWidth").floatValue = 8.0f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateFramedPrefab(string fileName, Mesh screenMesh, Mesh frameMesh, Material signMat, Material frameMat)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SmartSignageController ctrl = root.AddComponent<SmartSignageController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject frameObj = new GameObject("Frame");
        frameObj.transform.SetParent(root.transform, false);
        MeshFilter fmf = frameObj.AddComponent<MeshFilter>();
        fmf.sharedMesh = frameMesh;
        MeshRenderer fmr = frameObj.AddComponent<MeshRenderer>();
        fmr.sharedMaterial = frameMat;
        fmr.shadowCastingMode = ShadowCastingMode.On;
        fmr.receiveShadows = true;

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_style").enumValueIndex = (int)SmartSignageController.SignageStyle.AeroFramed;
        so.FindProperty("_showFrame").boolValue = true;
        so.FindProperty("_signWidth").floatValue = 8.0f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateFloatingPrefab(string fileName, Mesh screenMesh, Mesh pylonMesh, Material signMat, Material frameMat)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SmartSignageController ctrl = root.AddComponent<SmartSignageController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject pylonObj = new GameObject("Pylons");
        pylonObj.transform.SetParent(root.transform, false);
        MeshFilter fmf = pylonObj.AddComponent<MeshFilter>();
        fmf.sharedMesh = pylonMesh;
        MeshRenderer fmr = pylonObj.AddComponent<MeshRenderer>();
        fmr.sharedMaterial = frameMat;
        fmr.shadowCastingMode = ShadowCastingMode.On;
        fmr.receiveShadows = true;

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_style").enumValueIndex = (int)SmartSignageController.SignageStyle.FloatingPylons;
        so.FindProperty("_aspectRatio").enumValueIndex = (int)SmartSignageController.AspectRatioPreset.Ratio_21_9;
        so.FindProperty("_showFrame").boolValue = true;
        so.FindProperty("_signWidth").floatValue = 10.5f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = pylonObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateWallMountPrefab(string fileName, Mesh screenMesh, Mesh mountMesh, Material signMat, Material frameMat)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        SmartSignageController ctrl = root.AddComponent<SmartSignageController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject mountObj = new GameObject("MountArm");
        mountObj.transform.SetParent(root.transform, false);
        MeshFilter fmf = mountObj.AddComponent<MeshFilter>();
        fmf.sharedMesh = mountMesh;
        MeshRenderer fmr = mountObj.AddComponent<MeshRenderer>();
        fmr.sharedMaterial = frameMat;
        fmr.shadowCastingMode = ShadowCastingMode.On;
        fmr.receiveShadows = true;

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_style").enumValueIndex = (int)SmartSignageController.SignageStyle.WallMount;
        so.FindProperty("_aspectRatio").enumValueIndex = (int)SmartSignageController.AspectRatioPreset.Ratio_9_16;
        so.FindProperty("_showFrame").boolValue = true;
        so.FindProperty("_signWidth").floatValue = 4.5f;
        so.FindProperty("_signHeight").floatValue = 8.0f;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = mountObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    // ─────────────────────────────────────────────────────────────
    // シーン内への展示用ショーケース一括配置
    // ─────────────────────────────────────────────────────────────

    public static void SpawnSmartSignageShowcase(Vector3 originPos)
    {
        const string ShowcaseName = "--- SmartSignage Showcase ---";
        GameObject existing = GameObject.Find(ShowcaseName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(ShowcaseName);
        Undo.RegisterCreatedObjectUndo(root, "Spawn SmartSignage Showcase");
        root.transform.position = originPos;

        string[] prefabNames = new string[]
        {
            "Prefab_SmartSign_ScreenOnly.prefab",
            "Prefab_SmartSign_Framed.prefab",
            "Prefab_SmartSign_Floating.prefab",
            "Prefab_SmartSign_WallMount.prefab"
        };

        string[] labels = new string[]
        {
            "Showcase_01_TelemetryHUD",
            "Showcase_02_WeatherStation",
            "Showcase_03_RaceLeaderboard",
            "Showcase_04_RouteGuide"
        };

        SmartSignageController.SignagePreset[] presets = new SmartSignageController.SignagePreset[]
        {
            SmartSignageController.SignagePreset.TelemetryHUD,
            SmartSignageController.SignagePreset.WeatherStation,
            SmartSignageController.SignagePreset.RaceLeaderboard,
            SmartSignageController.SignagePreset.RouteGuide
        };

        float spacing = 12.0f;
        float startX = -18.0f;

        for (int i = 0; i < prefabNames.Length; i++)
        {
            string pPath = $"{PrefabsFolder}/{prefabNames[i]}";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
            if (prefabAsset == null) continue;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, root.transform);
            instance.name = labels[i];
            instance.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);
            instance.transform.localRotation = Quaternion.identity;

            SmartSignageController ctrl = instance.GetComponent<SmartSignageController>();
            if (ctrl != null)
            {
                ctrl.ApplyPreset(presets[i]);
                ctrl.UpdateSignboard();
            }
        }

        Selection.activeGameObject = root;
        Debug.Log($"<color=#70D0FF>[SmartSignage]</color> 展示ショーケースを配置しました: {ShowcaseName}");
    }

    // ─────────────────────────────────────────────────────────────
    // 2K 高解像度 UI テクスチャ生成エンジン（精密ビットマップHUD）
    // ─────────────────────────────────────────────────────────────

    private static void GeneratePresetTextures()
    {
        int width = 2048;
        int height = 1152; // 16:9

        // 1. Telemetry HUD (コンセプトアート直系のテレメトリHUD)
        Texture2D texHUD = CreateTexture(width, height);
        DrawTelemetryHUD(texHUD);
        SavePNG(texHUD, $"{TexturesFolder}/Tex_SmartSign_TelemetryHUD.png");
        DestroyImmediate(texHUD);

        // 2. Weather Station
        Texture2D texWeather = CreateTexture(width, height);
        DrawWeatherStation(texWeather);
        SavePNG(texWeather, $"{TexturesFolder}/Tex_SmartSign_WeatherStation.png");
        DestroyImmediate(texWeather);

        // 3. Race Leaderboard
        Texture2D texRace = CreateTexture(width, height);
        DrawRaceLeaderboard(texRace);
        SavePNG(texRace, $"{TexturesFolder}/Tex_SmartSign_RaceLeaderboard.png");
        DestroyImmediate(texRace);

        // 4. Route Guide
        Texture2D texRoute = CreateTexture(width, height);
        DrawRouteGuide(texRoute);
        SavePNG(texRoute, $"{TexturesFolder}/Tex_SmartSign_RouteGuide.png");
        DestroyImmediate(texRoute);
    }

    private static Texture2D CreateTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels32(pixels);
        return tex;
    }

    private static void SavePNG(Texture2D tex, string path)
    {
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // HUD 描画ルーチン
    // ─────────────────────────────────────────────────────────────

    private static void DrawTelemetryHUD(Texture2D tex)
    {
        Color white = Color.white;
        Color cyan = new Color(0.80f, 0.95f, 1.0f);

        // 外周コーナーブラケット
        DrawCornerBrackets(tex, 120, 120, tex.width - 240, tex.height - 240, 80, 4, white);

        // 左上: ALTITUDE
        DrawText(tex, "ALTITUDE:", 180, 940, 5, white);
        DrawText(tex, "4,520", 180, 820, 15, white);
        DrawText(tex, "M", 620, 820, 9, cyan);

        // 左中: VELOCITY
        DrawText(tex, "VELOCITY:", 180, 710, 5, white);
        DrawText(tex, "712", 180, 590, 15, white);
        DrawText(tex, "KM/H", 480, 590, 8, cyan);

        // 左下: WIND & VECTOR
        DrawText(tex, "WIND:", 180, 480, 5, white);
        DrawText(tex, "NW 14", 180, 400, 9, white);
        DrawText(tex, "KM/H", 450, 400, 6, cyan);

        DrawText(tex, "VECTOR:", 180, 290, 5, white);
        DrawArrow(tex, 180, 220, 320, 220, 5, cyan);
        DrawRect(tex, 360, 190, 90, 60, cyan, 3);
        DrawArrow(tex, 375, 220, 435, 220, 4, cyan);

        // 下部テレメトリ波形グラフ
        DrawLineGraph(tex, 510, 160, 460, 160, cyan);

        // 右上: LAP & RANK
        DrawText(tex, "LAP", 1250, 940, 5, white);
        DrawText(tex, "3/5", 1250, 830, 11, white);

        DrawText(tex, "RANK", 1550, 940, 5, white);
        DrawText(tex, "1/16", 1550, 830, 11, white);

        // 右下: RACER
        DrawText(tex, "RACER:", 1250, 450, 5, white);
        DrawText(tex, "LUNA VOYAGER", 1250, 360, 9, cyan);

        // マイクロテクニカル座標
        DrawText(tex, "045017 // SEC-04", 180, 140, 3, new Color(0.7f, 0.85f, 1f, 0.7f));
        DrawText(tex, "280010 // AERO-SYS", 1600, 140, 3, new Color(0.7f, 0.85f, 1f, 0.7f));
    }

    private static void DrawWeatherStation(Texture2D tex)
    {
        Color white = Color.white;
        Color sky = new Color(0.65f, 0.90f, 1.0f);

        DrawCornerBrackets(tex, 120, 120, tex.width - 240, tex.height - 240, 80, 4, white);

        DrawText(tex, "WEATHER STATION // ATMOSPHERE SEC-04", 180, 960, 5, white);
        DrawHLine(tex, 180, 920, 1680, 3, sky);

        DrawText(tex, "AIR DENSITY:", 180, 820, 5, white);
        DrawText(tex, "1.18", 180, 720, 12, white);
        DrawText(tex, "KG/M3", 440, 720, 6, sky);

        DrawText(tex, "BAROMETER:", 180, 600, 5, white);
        DrawText(tex, "1013.2", 180, 500, 12, white);
        DrawText(tex, "HPA [STABLE]", 630, 500, 6, sky);

        DrawText(tex, "THERMAL LIFT:", 180, 380, 5, white);
        DrawText(tex, "+5.4", 180, 280, 12, sky);
        DrawText(tex, "M/S", 460, 280, 6, sky);

        // コンパスレーダーサークル
        DrawCircle(tex, 1450, 560, 220, sky, 3);
        DrawCircle(tex, 1450, 560, 140, sky, 2);
        DrawCircle(tex, 1450, 560, 60, sky, 2);
        DrawArrow(tex, 1450, 560, 1330, 680, 6, white);
        DrawText(tex, "N", 1435, 800, 5, white);
        DrawText(tex, "WIND: NW 24 KM/H", 1250, 260, 7, white);
    }

    private static void DrawRaceLeaderboard(Texture2D tex)
    {
        Color white = Color.white;
        Color gold = new Color(1.0f, 0.90f, 0.60f);
        Color cyan = new Color(0.70f, 0.95f, 1.0f);

        DrawCornerBrackets(tex, 120, 120, tex.width - 240, tex.height - 240, 80, 4, white);

        DrawText(tex, "SKYLINE GRAND PRIX // LIVE STANDINGS", 180, 960, 6, gold);
        DrawText(tex, "LAP 3/5  -  SECTOR 2", 1400, 960, 5, white);
        DrawHLine(tex, 180, 920, 1680, 3, gold);

        int rowY = 800;
        int rowH = 130;

        // P1
        DrawText(tex, "01", 180, rowY, 9, gold);
        DrawText(tex, "LUNA VOYAGER", 360, rowY, 9, white);
        DrawText(tex, "1:12.458", 1250, rowY, 9, gold);
        DrawText(tex, "LEADER", 1680, rowY, 6, gold);
        DrawHLine(tex, 180, rowY - 25, 1680, 2, new Color(1f, 1f, 1f, 0.3f));

        // P2
        rowY -= rowH;
        DrawText(tex, "02", 180, rowY, 9, white);
        DrawText(tex, "AERO FALCON", 360, rowY, 9, white);
        DrawText(tex, "+0.342S", 1250, rowY, 9, cyan);
        DrawHLine(tex, 180, rowY - 25, 1680, 2, new Color(1f, 1f, 1f, 0.2f));

        // P3
        rowY -= rowH;
        DrawText(tex, "03", 180, rowY, 9, white);
        DrawText(tex, "SOLARIS 7", 360, rowY, 9, white);
        DrawText(tex, "+1.120S", 1250, rowY, 9, cyan);
        DrawHLine(tex, 180, rowY - 25, 1680, 2, new Color(1f, 1f, 1f, 0.2f));

        // P4
        rowY -= rowH;
        DrawText(tex, "04", 180, rowY, 9, white);
        DrawText(tex, "NEO VORTEX", 360, rowY, 9, white);
        DrawText(tex, "+1.895S", 1250, rowY, 9, cyan);

        // 下部スピードトラップ
        DrawText(tex, "SPEED TRAP RECORD: 748 KM/H (LUNA VOYAGER)", 180, 180, 5, gold);
    }

    private static void DrawRouteGuide(Texture2D tex)
    {
        Color white = Color.white;
        Color emerald = new Color(0.55f, 1.0f, 0.80f);
        Color cyan = new Color(0.70f, 0.95f, 1.0f);

        DrawCornerBrackets(tex, 120, 120, tex.width - 240, tex.height - 240, 80, 4, white);

        DrawText(tex, "SKYWAY ROUTE 101 // CORRIDOR ALPHA", 180, 960, 6, emerald);
        DrawHLine(tex, 180, 920, 1680, 3, emerald);

        DrawText(tex, "NEXT WAYPOINT:", 180, 820, 5, white);
        DrawText(tex, "APEX GATE 04", 180, 710, 12, white);
        DrawText(tex, "DISTANCE: 820M", 180, 610, 7, cyan);

        DrawText(tex, "REC. VELOCITY:", 180, 480, 5, white);
        DrawText(tex, "680 KM/H", 180, 380, 11, emerald);

        DrawText(tex, "GLIDE SLOPE: -3.5 DEG", 180, 260, 6, white);
        DrawText(tex, "AIR TRAFFIC CLEARANCE: 100% GRANTED", 180, 180, 5, emerald);

        // 右側ナビゲーションアロー
        DrawArrow(tex, 1200, 520, 1650, 520, 12, emerald);
        DrawRect(tex, 1100, 320, 650, 400, emerald, 4);
    }

    // ─────────────────────────────────────────────────────────────
    // 基本描画ユーティリティ（ビットマップフォント・図形）
    // ─────────────────────────────────────────────────────────────

    private static void DrawCornerBrackets(Texture2D tex, int x, int y, int w, int h, int size, int thick, Color col)
    {
        // 左下
        DrawHLine(tex, x, y, size, thick, col);
        DrawVLine(tex, x, y, size, thick, col);
        // 右下
        DrawHLine(tex, x + w - size, y, size, thick, col);
        DrawVLine(tex, x + w - thick, y, size, thick, col);
        // 左上
        DrawHLine(tex, x, y + h - thick, size, thick, col);
        DrawVLine(tex, x, y + h - size, size, thick, col);
        // 右上
        DrawHLine(tex, x + w - size, y + h - thick, size, thick, col);
        DrawVLine(tex, x + w - thick, y + h - size, size, thick, col);
    }

    private static void DrawHLine(Texture2D tex, int x, int y, int len, int thick, Color col)
    {
        for (int i = 0; i < len; i++)
        {
            for (int t = 0; t < thick; t++)
            {
                SetPixelSafe(tex, x + i, y + t, col);
            }
        }
    }

    private static void DrawVLine(Texture2D tex, int x, int y, int len, int thick, Color col)
    {
        for (int i = 0; i < len; i++)
        {
            for (int t = 0; t < thick; t++)
            {
                SetPixelSafe(tex, x + t, y + i, col);
            }
        }
    }

    private static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color col, int thick)
    {
        DrawHLine(tex, x, y, w, thick, col);
        DrawHLine(tex, x, y + h - thick, w, thick, col);
        DrawVLine(tex, x, y, h, thick, col);
        DrawVLine(tex, x + w - thick, y, h, thick, col);
    }

    private static void DrawCircle(Texture2D tex, int cx, int cy, int radius, Color col, int thick)
    {
        int r2Min = (radius - thick) * (radius - thick);
        int r2Max = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int d2 = dx * dx + dy * dy;
                if (d2 >= r2Min && d2 <= r2Max)
                {
                    SetPixelSafe(tex, cx + dx, cy + dy, col);
                }
            }
        }
    }

    private static void DrawArrow(Texture2D tex, int x0, int y0, int x1, int y1, int thick, Color col)
    {
        DrawLine(tex, x0, y0, x1, y1, thick, col);
        // 矢印ヘッド
        int headSize = thick * 3 + 12;
        DrawLine(tex, x1, y1, x1 - headSize, y1 + headSize, thick, col);
        DrawLine(tex, x1, y1, x1 - headSize, y1 - headSize, thick, col);
    }

    private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, int thick, Color col)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            for (int tx = -thick / 2; tx <= thick / 2; tx++)
            {
                for (int ty = -thick / 2; ty <= thick / 2; ty++)
                {
                    SetPixelSafe(tex, x0 + tx, y0 + ty, col);
                }
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx)  { err += dx; y0 += sy; }
        }
    }

    private static void DrawLineGraph(Texture2D tex, int x, int y, int w, int h, Color col)
    {
        // グラフ枠・グリッド
        DrawRect(tex, x, y, w, h, new Color(col.r, col.g, col.b, 0.4f), 2);
        for (int gy = 1; gy < 4; gy++)
        {
            DrawHLine(tex, x, y + (h * gy) / 4, w, 1, new Color(col.r, col.g, col.b, 0.2f));
        }

        // 滑らかな曲線グラフ
        int prevX = x + 10;
        int prevY = y + 25;
        for (int i = 1; i <= 20; i++)
        {
            int curX = x + (w * i) / 20 - 10;
            float t = (float)i / 20f;
            float val = Mathf.Sin(t * 3.5f) * 0.5f + Mathf.Cos(t * 7f) * 0.2f + 0.35f;
            int curY = y + (int)(val * (h - 40)) + 20;

            DrawLine(tex, prevX, prevY, curX, curY, 3, col);
            DrawCircle(tex, curX, curY, 4, Color.white, 3);
            prevX = curX;
            prevY = curY;
        }
    }

    private static void SetPixelSafe(Texture2D tex, int x, int y, Color col)
    {
        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
        {
            Color cur = tex.GetPixel(x, y);
            Color blended = Color.Lerp(cur, col, col.a);
            blended.a = Mathf.Max(cur.a, col.a);
            tex.SetPixel(x, y, blended);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 5x7 ビットマップフォントレンダラー（超高精細テレメトリ英数字）
    // ─────────────────────────────────────────────────────────────

    private static void DrawText(Texture2D tex, string str, int startX, int startY, int scale, Color col)
    {
        if (string.IsNullOrEmpty(str)) return;
        int curX = startX;
        for (int i = 0; i < str.Length; i++)
        {
            char c = char.ToUpperInvariant(str[i]);
            if (c == ' ')
            {
                curX += 4 * scale;
                continue;
            }

            byte[] glyph = GetGlyph5x7(c);
            for (int row = 0; row < 7; row++)
            {
                byte rowBits = glyph[row];
                for (int colIdx = 0; colIdx < 5; colIdx++)
                {
                    if ((rowBits & (1 << (4 - colIdx))) != 0)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            for (int sy = 0; sy < scale; sy++)
                            {
                                int px = curX + colIdx * scale + sx;
                                int py = startY + (6 - row) * scale + sy;
                                SetPixelSafe(tex, px, py, col);
                            }
                        }
                    }
                }
            }
            curX += 6 * scale;
        }
    }

    private static byte[] GetGlyph5x7(char c)
    {
        switch (c)
        {
            case '0': return new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
            case '1': return new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case '2': return new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F };
            case '3': return new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E };
            case '4': return new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
            case '5': return new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
            case '6': return new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
            case '7': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
            case '8': return new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
            case '9': return new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
            case 'A': return new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'B': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
            case 'C': return new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
            case 'D': return new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C };
            case 'E': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
            case 'F': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
            case 'G': return new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0E };
            case 'H': return new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'I': return new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case 'J': return new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C };
            case 'K': return new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
            case 'L': return new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
            case 'M': return new byte[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 };
            case 'N': return new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
            case 'O': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'P': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
            case 'Q': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
            case 'R': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
            case 'S': return new byte[] { 0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E };
            case 'T': return new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            case 'U': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'V': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
            case 'W': return new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
            case 'X': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
            case 'Y': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
            case 'Z': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };
            case ':': return new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            case '/': return new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x00, 0x00 };
            case '-': return new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            case '+': return new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
            case '.': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
            case ',': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04, 0x04, 0x08 };
            case '%': return new byte[] { 0x19, 0x19, 0x02, 0x04, 0x08, 0x13, 0x13 };
            case '[': return new byte[] { 0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E };
            case ']': return new byte[] { 0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E };
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
