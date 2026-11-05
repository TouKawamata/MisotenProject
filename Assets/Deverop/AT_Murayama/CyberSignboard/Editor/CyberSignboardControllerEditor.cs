using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// CyberSignboardController 用のインスペクター拡張。
/// ・Toolsメニュー（[MenuItem]）は一切使用せず、全操作はインスペクター上のGUIで完結。
/// ・1クリックカラープリセット（シアン、ピンク、アンバー、グリーン）
/// ・1クリックアスペクト比調整（16:9, 4:3, 1:1, 9:16）
/// ・マテリアル別名保存（Save As）
/// ・全4形状×枠有無（計8プレハブ・5マテリアル・メッシュ）の一括生成・再保存
/// ・シーン内への展示用ショーケース（--- CyberSignboard Showcase ---）のワンクリック配置
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(CyberSignboardController))]
public class CyberSignboardControllerEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/CyberSignboard";
    private const string ShadersFolder   = BaseFolder + "/Shaders";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    private static bool _showAssetTools = false;

    static CyberSignboardControllerEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh holoBase = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/HoloBaseMesh.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || holoBase == null || holoBase.vertexCount < 80)
            {
                // 底面・内面閉鎖版の完全立体メッシュおよび最新プレハブ構成へ一括更新（GUIDは維持）
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        CyberSignboardController comp = (CyberSignboardController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【クイック設定】カラープリセット (HDR発光)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetColorButton(comp, "Cyan Cyber\n(#00F0FF)", CyberSignboardController.ColorPreset.Cyan, new Color(0.1f, 0.85f, 1f));
            DrawPresetColorButton(comp, "Neon Pink\n(#FF0055)", CyberSignboardController.ColorPreset.NeonPink, new Color(1f, 0.4f, 0.7f));
            DrawPresetColorButton(comp, "Warning Amber\n(#FF9900)", CyberSignboardController.ColorPreset.WarningAmber, new Color(1f, 0.75f, 0.2f));
            DrawPresetColorButton(comp, "Matrix Green\n(#00FF66)", CyberSignboardController.ColorPreset.MatrixGreen, new Color(0.3f, 1f, 0.6f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【ワンタッチ比率調整】アスペクト比プリセット", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAspectButton(comp, "16:9\n(ワイド)", CyberSignboardController.AspectRatioPreset.Ratio_16_9);
            DrawAspectButton(comp, "4:3\n(標準)", CyberSignboardController.AspectRatioPreset.Ratio_4_3);
            DrawAspectButton(comp, "1:1\n(スクエア)", CyberSignboardController.AspectRatioPreset.Ratio_1_1);
            DrawAspectButton(comp, "9:16\n(縦サイネージ)", CyberSignboardController.AspectRatioPreset.Ratio_9_16);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【外枠・金具の切り替え】", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = comp.ShowFrame ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.8f, 0.8f);
            string frameText = comp.ShowFrame ? "枠・金具・台座: [ ON ] (クリックで非表示)" : "枠・金具・台座: [ OFF ] (クリックで表示)";
            if (GUILayout.Button(frameText, GUILayout.Height(28f)))
            {
                Undo.RecordObject(comp, "Toggle Signboard Frame");
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
                EditorGUILayout.HelpBox("インスペクターで調整したネオン色やエフェクトを新規マテリアル（.mat）として別名保存し、この看板に割り当てます。", MessageType.None);
            }

            string defaultPlaceholder = $"M_CyberSign_{comp.gameObject.name}";
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
                if (GUILayout.Button("✨ シーン内に展示ショーケース（--- CyberSignboard Showcase ---）を配置", GUILayout.Height(30f)))
                {
                    SpawnCyberSignboardShowcase(comp.transform.position);
                }

                EditorGUILayout.Space(2f);
                GUI.backgroundColor = new Color(0.85f, 1f, 0.8f);
                if (GUILayout.Button("📁 8種プレハブ・5種マテリアル・メッシュを一括再生成 / 保存", GUILayout.Height(26f)))
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

    private static void DrawPresetColorButton(CyberSignboardController comp, string label, CyberSignboardController.ColorPreset preset, Color btnColor)
    {
        GUI.backgroundColor = btnColor;
        if (GUILayout.Button(label, GUILayout.Height(36f)))
        {
            Undo.RecordObject(comp, "Apply CyberSign Color Preset");
            comp.ApplyColorPreset(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawAspectButton(CyberSignboardController comp, string label, CyberSignboardController.AspectRatioPreset preset)
    {
        GUI.backgroundColor = (comp.AspectRatio == preset) ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(32f)))
        {
            Undo.RecordObject(comp, "Change Signboard Aspect Ratio");
            comp.SetAspectRatio(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SaveAsCustomMaterial(CyberSignboardController comp, bool useSavePanel)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        string savePath = "";
        string fileWithoutExt = string.IsNullOrWhiteSpace(comp.CustomSaveName)
            ? $"M_CyberSign_{comp.gameObject.name}"
            : comp.CustomSaveName.Trim();

        if (fileWithoutExt.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
        {
            fileWithoutExt = Path.GetFileNameWithoutExtension(fileWithoutExt);
        }

        if (useSavePanel)
        {
            savePath = EditorUtility.SaveFilePanelInProject(
                "サイバー看板マテリアルを別名保存",
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
            Undo.RecordObject(comp, "Apply Saved CyberSign Material");
            comp.CustomMaterial = loadedMat;
            comp.CustomSaveName = fileWithoutExt;
            EditorUtility.SetDirty(comp);

            EditorGUIUtility.PingObject(loadedMat);
            Debug.Log($"<color=#00f0ff>[CyberSignboard]</color> マテリアルを別名保存して適用しました: <b>{savePath}</b>");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 全アセット（メッシュ・マテリアル・プレハブ8種）の一括生成ロジック
    // ─────────────────────────────────────────────────────────────

    public static void UpdateAllMeshesInPlace()
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MeshesFolder);

        SaveOrUpdateMesh("WallScreenMesh.asset",      CyberSignboardController.CreateWallScreenMesh());
        SaveOrUpdateMesh("WallFrameMesh.asset",       CyberSignboardController.CreateWallFrameMesh());
        SaveOrUpdateMesh("CurvedSignboardMesh.asset", CyberSignboardController.CreateCurvedScreenMesh(2.0f, 1.0f, 32));
        SaveOrUpdateMesh("CurvedFrameMesh.asset",     CyberSignboardController.CreateCurvedFrameMesh(2.0f, 1.0f, 32));
        SaveOrUpdateMesh("BladeScreenMesh.asset",     CyberSignboardController.CreateBladeScreenMesh());
        SaveOrUpdateMesh("BladeFrameMesh.asset",      CyberSignboardController.CreateBladeFrameMesh());
        SaveOrUpdateMesh("HoloScreenMesh.asset",      CyberSignboardController.CreateHoloScreenMesh());
        SaveOrUpdateMesh("HoloBaseMesh.asset",        CyberSignboardController.CreateHoloBaseMesh());
        SaveOrUpdateMesh("HoloBeamMesh.asset",        CyberSignboardController.CreateHoloBeamMesh());

        AssetDatabase.SaveAssets();
    }

    public static void GenerateAllAssets(bool ping)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(ShadersFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        EnsureFolder(PrefabsFolder);

        // 1. メッシュ生成＆保存（CyberSignboardControllerの完全閉鎖メッシュ生成ロジックと一元化）
        Mesh wallScreenMesh   = SaveOrUpdateMesh("WallScreenMesh.asset",      CyberSignboardController.CreateWallScreenMesh());
        Mesh wallFrameMesh    = SaveOrUpdateMesh("WallFrameMesh.asset",       CyberSignboardController.CreateWallFrameMesh());
        Mesh curvedScreenMesh = SaveOrUpdateMesh("CurvedSignboardMesh.asset", CyberSignboardController.CreateCurvedScreenMesh(2.0f, 1.0f, 32));
        Mesh curvedFrameMesh  = SaveOrUpdateMesh("CurvedFrameMesh.asset",     CyberSignboardController.CreateCurvedFrameMesh(2.0f, 1.0f, 32));
        Mesh bladeScreenMesh  = SaveOrUpdateMesh("BladeScreenMesh.asset",     CyberSignboardController.CreateBladeScreenMesh());
        Mesh bladeFrameMesh   = SaveOrUpdateMesh("BladeFrameMesh.asset",      CyberSignboardController.CreateBladeFrameMesh());
        Mesh holoScreenMesh   = SaveOrUpdateMesh("HoloScreenMesh.asset",      CyberSignboardController.CreateHoloScreenMesh());
        Mesh holoBaseMesh     = SaveOrUpdateMesh("HoloBaseMesh.asset",        CyberSignboardController.CreateHoloBaseMesh());
        Mesh holoBeamMesh     = SaveOrUpdateMesh("HoloBeamMesh.asset",        CyberSignboardController.CreateHoloBeamMesh());

        // 2. プリセットマテリアル生成＆保存
        Color colCyan  = new Color(0.0f, 2.5f, 3.8f, 0.9f);
        Color colPink  = new Color(3.8f, 0.15f, 2.0f, 0.9f);
        Color colAmber = new Color(3.8f, 1.8f, 0.0f, 0.9f);
        Color colGreen = new Color(0.0f, 3.5f, 1.2f, 0.9f);

        Material matCyan  = SaveOrUpdateSignMaterial("M_CyberSign_Cyan.mat",  colCyan);
        Material matPink  = SaveOrUpdateSignMaterial("M_CyberSign_Pink.mat",  colPink);
        Material matAmber = SaveOrUpdateSignMaterial("M_CyberSign_Amber.mat", colAmber);
        Material matGreen = SaveOrUpdateSignMaterial("M_CyberSign_Green.mat", colGreen);
        Material matMetal = SaveOrUpdateMetalMaterial("M_SignFrame_Metal.mat");

        // 3. プレハブ8種の生成＆保存
        // ① 壁面フラット型 (Cyan)
        CreateWallPrefab("Prefab_Sign_Wall_Framed.prefab",     true,  wallScreenMesh, wallFrameMesh, matCyan, matMetal, colCyan);
        CreateWallPrefab("Prefab_Sign_Wall_ScreenOnly.prefab", false, wallScreenMesh, wallFrameMesh, matCyan, matMetal, colCyan);

        // ② 空中浮遊型 (Green)
        CreateHoloPrefab("Prefab_Sign_Holo_Beam.prefab",       true,  holoScreenMesh, holoBaseMesh, holoBeamMesh, matGreen, matMetal, colGreen);
        CreateHoloPrefab("Prefab_Sign_Holo_ScreenOnly.prefab", false, holoScreenMesh, holoBaseMesh, holoBeamMesh, matGreen, matMetal, colGreen);

        // ③ コーナー曲面型 (Pink)
        CreateCurvedPrefab("Prefab_Sign_Curved_Framed.prefab",     true,  curvedScreenMesh, curvedFrameMesh, matPink, matMetal, colPink);
        CreateCurvedPrefab("Prefab_Sign_Curved_ScreenOnly.prefab", false, curvedScreenMesh, curvedFrameMesh, matPink, matMetal, colPink);

        // ④ 袖看板・突出し型 (Amber)
        CreateBladePrefab("Prefab_Sign_Blade_Framed.prefab",     true,  bladeScreenMesh, bladeFrameMesh, matAmber, matMetal, colAmber);
        CreateBladePrefab("Prefab_Sign_Blade_ScreenOnly.prefab", false, bladeScreenMesh, bladeFrameMesh, matAmber, matMetal, colAmber);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(PrefabsFolder + "/Prefab_Sign_Wall_Framed.prefab");
            if (obj != null) EditorGUIUtility.PingObject(obj);
            Debug.Log("<color=#00f0ff>[CyberSignboard]</color> 全8種プレハブ・5種マテリアル・メッシュのアセット保存が完了しました！");
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Mesh SaveOrUpdateMesh(string fileName, Mesh generated)
    {
        string path = $"{MeshesFolder}/{fileName}";
        generated.name = Path.GetFileNameWithoutExtension(fileName);

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.SetVertices(generated.vertices);
            existing.SetNormals(generated.normals);
            existing.SetUVs(0, generated.uv);
            existing.SetTriangles(generated.triangles, 0);
            existing.bounds = generated.bounds;
            EditorUtility.SetDirty(existing);
            DestroyImmediate(generated);
            return existing;
        }
        else
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
    }

    private static Material SaveOrUpdateSignMaterial(string fileName, Color baseColor)
    {
        string path = $"{MaterialsFolder}/{fileName}";
        Shader shader = Shader.Find("Custom/CyberSignboard/S_CyberSignboard");
        if (shader == null) return null;

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

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_EmissionMultiplier", 2.0f);
        mat.SetFloat("_CyberOverlayIntensity", 1.0f);
        mat.SetFloat("_GlitchIntensity", 0.4f);
        mat.SetFloat("_ScanlineSpeed", 2.5f);
        mat.SetFloat("_ScanlineCount", 80.0f);
        mat.SetFloat("_ChromaticSplit", 0.015f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SaveOrUpdateMetalMaterial(string fileName)
    {
        string path = $"{MaterialsFolder}/{fileName}";
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(litShader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = litShader;
        }

        mat.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.16f, 1.0f));
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.9f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.75f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ─────────────────────────────────────────────────────────────
    // プレハブ生成ヘルパー（ScreenOnlyでも後から枠ON可能なよう非表示Frameを保持）
    // ─────────────────────────────────────────────────────────────

    private static void CreateWallPrefab(string fileName, bool framed, Mesh screenMesh, Mesh frameMesh, Material signMat, Material metalMat, Color baseColor)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        CyberSignboardController ctrl = root.AddComponent<CyberSignboardController>();

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
        fmr.sharedMaterial = metalMat;
        frameObj.SetActive(framed);

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_shape").enumValueIndex = (int)CyberSignboardController.SignboardShape.WallFlat;
        so.FindProperty("_showFrame").boolValue = framed;
        so.FindProperty("_signWidth").floatValue = 8.0f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_baseColor").colorValue = baseColor;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateHoloPrefab(string fileName, bool framed, Mesh screenMesh, Mesh baseMesh, Mesh beamMesh, Material signMat, Material metalMat, Color baseColor)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        CyberSignboardController ctrl = root.AddComponent<CyberSignboardController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject baseObj = new GameObject("ProjectorBase");
        baseObj.transform.SetParent(root.transform, false);
        MeshFilter bmf = baseObj.AddComponent<MeshFilter>();
        bmf.sharedMesh = baseMesh;
        MeshRenderer bmr = baseObj.AddComponent<MeshRenderer>();
        bmr.sharedMaterial = metalMat;
        baseObj.SetActive(framed);

        GameObject beamObj = new GameObject("ProjectorBeam");
        beamObj.transform.SetParent(root.transform, false);
        MeshFilter beamMf = beamObj.AddComponent<MeshFilter>();
        beamMf.sharedMesh = beamMesh;
        MeshRenderer beamMr = beamObj.AddComponent<MeshRenderer>();
        beamMr.sharedMaterial = signMat;
        beamMr.shadowCastingMode = ShadowCastingMode.Off;
        beamMr.receiveShadows = false;
        beamObj.SetActive(framed);

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_shape").enumValueIndex = (int)CyberSignboardController.SignboardShape.FloatingHolo;
        so.FindProperty("_showFrame").boolValue = framed;
        so.FindProperty("_signWidth").floatValue = 6.0f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_baseColor").colorValue = baseColor;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = baseObj;
        so.FindProperty("_beamObject").objectReferenceValue = beamObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateCurvedPrefab(string fileName, bool framed, Mesh screenMesh, Mesh frameMesh, Material signMat, Material metalMat, Color baseColor)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        CyberSignboardController ctrl = root.AddComponent<CyberSignboardController>();

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
        fmr.sharedMaterial = metalMat;
        frameObj.SetActive(framed);

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_shape").enumValueIndex = (int)CyberSignboardController.SignboardShape.CurvedCorner;
        so.FindProperty("_showFrame").boolValue = framed;
        so.FindProperty("_signWidth").floatValue = 3.14f;
        so.FindProperty("_signHeight").floatValue = 4.5f;
        so.FindProperty("_baseColor").colorValue = baseColor;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void CreateBladePrefab(string fileName, bool framed, Mesh screenMesh, Mesh frameMesh, Material signMat, Material metalMat, Color baseColor)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        CyberSignboardController ctrl = root.AddComponent<CyberSignboardController>();

        GameObject screenObj = new GameObject("Screen");
        screenObj.transform.SetParent(root.transform, false);
        MeshFilter smf = screenObj.AddComponent<MeshFilter>();
        smf.sharedMesh = screenMesh;
        MeshRenderer smr = screenObj.AddComponent<MeshRenderer>();
        smr.sharedMaterial = signMat;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;

        GameObject frameObj = new GameObject("MountArm");
        frameObj.transform.SetParent(root.transform, false);
        MeshFilter fmf = frameObj.AddComponent<MeshFilter>();
        fmf.sharedMesh = frameMesh;
        MeshRenderer fmr = frameObj.AddComponent<MeshRenderer>();
        fmr.sharedMaterial = metalMat;
        frameObj.SetActive(framed);

        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("_shape").enumValueIndex = (int)CyberSignboardController.SignboardShape.BladeSign;
        so.FindProperty("_aspectRatio").enumValueIndex = (int)CyberSignboardController.AspectRatioPreset.Ratio_9_16;
        so.FindProperty("_showFrame").boolValue = framed;
        so.FindProperty("_signWidth").floatValue = 4.5f;
        so.FindProperty("_signHeight").floatValue = 6.0f;
        so.FindProperty("_baseColor").colorValue = baseColor;
        so.FindProperty("_screenRenderer").objectReferenceValue = smr;
        so.FindProperty("_frameObject").objectReferenceValue = frameObj;
        so.ApplyModifiedPropertiesWithoutUndo();

        ctrl.UpdateSignboard();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    // ─────────────────────────────────────────────────────────────
    // シーン内への展示用ショーケース一括配置
    // ─────────────────────────────────────────────────────────────

    public static void SpawnCyberSignboardShowcase(Vector3 originPos)
    {
        const string ShowcaseName = "--- CyberSignboard Showcase ---";
        GameObject existing = GameObject.Find(ShowcaseName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(ShowcaseName);
        Undo.RegisterCreatedObjectUndo(root, "Spawn CyberSignboard Showcase");
        root.transform.position = originPos;

        // 4種類のプレハブを等間隔（12mピッチ）で配置
        string[] prefabNames = new string[]
        {
            "Prefab_Sign_Wall_Framed.prefab",
            "Prefab_Sign_Holo_Beam.prefab",
            "Prefab_Sign_Curved_Framed.prefab",
            "Prefab_Sign_Blade_Framed.prefab"
        };

        string[] labels = new string[]
        {
            "Showcase_01_Wall_Cyan",
            "Showcase_02_Holo_Green",
            "Showcase_03_Curved_Pink",
            "Showcase_04_Blade_Amber"
        };

        CyberSignboardController.ColorPreset[] presets = new CyberSignboardController.ColorPreset[]
        {
            CyberSignboardController.ColorPreset.Cyan,
            CyberSignboardController.ColorPreset.MatrixGreen,
            CyberSignboardController.ColorPreset.NeonPink,
            CyberSignboardController.ColorPreset.WarningAmber
        };

        float spacing = 12.0f;
        float startX = -18.0f;

        for (int i = 0; i < prefabNames.Length; i++)
        {
            string pPath = $"{PrefabsFolder}/{prefabNames[i]}";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
            GameObject signInstance = null;

            if (prefabAsset != null)
            {
                signInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, root.transform);
            }
            else
            {
                signInstance = new GameObject(labels[i]);
                signInstance.transform.SetParent(root.transform, false);
            }

            signInstance.name = labels[i];
            signInstance.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);

            CyberSignboardController ctrl = signInstance.GetComponent<CyberSignboardController>();
            if (ctrl != null)
            {
                ctrl.ApplyColorPreset(presets[i]);
            }
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("<color=#00f0ff>[CyberSignboard]</color> 展示用ショーケース（--- CyberSignboard Showcase ---）をシーン内に配置しました！");
    }
}
