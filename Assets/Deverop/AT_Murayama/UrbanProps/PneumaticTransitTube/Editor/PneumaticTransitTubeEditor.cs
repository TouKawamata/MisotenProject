#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// PneumaticTransitTubeController 用のインスペクター拡張。
/// ・ワンクリックでのプリセット適用
/// ・ワンクリックでのカラーテーマ切替
/// ・全アセット（プレハブ4種・マテリアル・メッシュ）の一括生成・保存
/// ・シーン展示ショーケース（--- PneumaticTransitTube Showcase ---）のワンクリック配置
/// ・Toolsメニュー不使用（規約遵守）
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(PneumaticTransitTubeController))]
public class PneumaticTransitTubeEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/UrbanProps/PneumaticTransitTube";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    static PneumaticTransitTubeEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/TransitTube_Glass.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || m == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        PneumaticTransitTubeController comp = (PneumaticTransitTubeController)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🚀 Pneumatic Transit Tube Quick Controls", EditorStyles.boldLabel);

        // 1. Preset Buttons
        EditorGUILayout.LabelField("ワンクリック・プリセット", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🚀 スカイスクレイパー\n(80m / Cyan)"))
        {
            Undo.RecordObject(comp, "Apply Preset SkyscraperExpress");
            comp.ApplyPreset(PneumaticTransitTubeController.TubePreset.SkyscraperExpress);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🏢 標準トランジット\n(35m / White)"))
        {
            Undo.RecordObject(comp, "Apply Preset StandardTransit");
            comp.ApplyPreset(PneumaticTransitTubeController.TubePreset.StandardTransit);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌆 展望ピラー\n(45m / Black)"))
        {
            Undo.RecordObject(comp, "Apply Preset ObservationPillar");
            comp.ApplyPreset(PneumaticTransitTubeController.TubePreset.ObservationPillar);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🏗️ 重工業貨物\n(50m / Amber)"))
        {
            Undo.RecordObject(comp, "Apply Preset IndustrialFreight");
            comp.ApplyPreset(PneumaticTransitTubeController.TubePreset.IndustrialFreight);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        // 2. Color Theme Buttons
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("カラーテーマ切替", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clean White"))
        {
            Undo.RecordObject(comp, "Apply Theme CleanWhite");
            comp.ApplyTheme(PneumaticTransitTubeController.ColorTheme.CleanWhite);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Cyber Cyan"))
        {
            Undo.RecordObject(comp, "Apply Theme CyberCyan");
            comp.ApplyTheme(PneumaticTransitTubeController.ColorTheme.CyberCyan);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Industrial Amber"))
        {
            Undo.RecordObject(comp, "Apply Theme IndustrialAmber");
            comp.ApplyTheme(PneumaticTransitTubeController.ColorTheme.IndustrialAmber);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Corporate Black"))
        {
            Undo.RecordObject(comp, "Apply Theme CorporateBlack");
            comp.ApplyTheme(PneumaticTransitTubeController.ColorTheme.CorporateBlack);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawDefaultInspector();

        // 3. Asset & Showcase Utilities
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("📦 アセット管理 & ショーケース展示", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 1.0f);
        if (GUILayout.Button("📦 全アセット一括生成 (プレハブ4種・マテリアル・メッシュ)", GUILayout.Height(32)))
        {
            GenerateAllAssets(true);
        }

        GUI.backgroundColor = new Color(0.85f, 1.0f, 0.75f);
        if (GUILayout.Button("🌟 シーン展示を配置 (--- PneumaticTransitTube Showcase ---)", GUILayout.Height(28)))
        {
            SpawnShowcase(new Vector3(0, 44, 25));
        }
        GUI.backgroundColor = Color.white;
    }

    public static void GenerateAllAssets(bool ping = false)
    {
        EnsureFolder(MeshesFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);

        // 1. Mesh generation
        Mesh glassMesh     = PneumaticTransitTubeController.CreateTubeGlassMesh(35.0f, 1.5f);
        Mesh ringsMesh     = PneumaticTransitTubeController.CreateStructuralRingsMesh(35.0f, 1.5f, 6.0f);
        Mesh mountsMesh    = PneumaticTransitTubeController.CreateWallMountsMesh(35.0f, 1.5f, 6.0f, 2.2f);
        Mesh terminalsMesh = PneumaticTransitTubeController.CreateEndTerminalsMesh(35.0f, 1.5f);
        Mesh capsuleMesh   = PneumaticTransitTubeController.CreateCapsulePodMesh(1.5f);

        SaveOrUpdateMesh(glassMesh,     $"{MeshesFolder}/TransitTube_Glass.asset");
        SaveOrUpdateMesh(ringsMesh,     $"{MeshesFolder}/TransitTube_Rings.asset");
        SaveOrUpdateMesh(mountsMesh,    $"{MeshesFolder}/TransitTube_WallMounts.asset");
        SaveOrUpdateMesh(terminalsMesh, $"{MeshesFolder}/TransitTube_Terminals.asset");
        SaveOrUpdateMesh(capsuleMesh,   $"{MeshesFolder}/TransitTube_Capsule.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh pGlass     = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/TransitTube_Glass.asset");
        Mesh pRings     = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/TransitTube_Rings.asset");
        Mesh pMounts    = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/TransitTube_WallMounts.asset");
        Mesh pTerminals = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/TransitTube_Terminals.asset");
        Mesh pCapsule   = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/TransitTube_Capsule.asset");

        // 2. Materials
        Shader glassShader = Shader.Find("Custom/UrbanProps/S_PneumaticTubeGlass");
        Shader structShader = Shader.Find("Custom/UrbanProps/S_PneumaticTubeStructure");
        Shader capsuleShader = Shader.Find("Custom/UrbanProps/S_PneumaticCapsule");

        Material matGlassCyan = CreateOrUpdateMaterial("M_Tube_Glass_Cyan", glassShader,
            new Color(0.2f, 0.85f, 1.0f, 0.28f), new Color(0.0f, 0.95f, 1.0f, 0.9f));
        Material matGlassWhite = CreateOrUpdateMaterial("M_Tube_Glass_White", glassShader,
            new Color(0.92f, 0.97f, 1.0f, 0.22f), new Color(0.75f, 0.92f, 1.0f, 0.8f));

        Material matStructWhite = CreateOrUpdateStructMaterial("M_Tube_Structure_White", structShader,
            new Color(0.92f, 0.94f, 0.96f), new Color(0.20f, 0.22f, 0.26f), new Color(0.2f, 0.8f, 1.0f));
        Material matStructDark = CreateOrUpdateStructMaterial("M_Tube_Structure_Dark", structShader,
            new Color(0.18f, 0.22f, 0.28f), new Color(0.08f, 0.10f, 0.14f), new Color(0.0f, 1.0f, 1.0f));

        Material matCapsuleWhite = CreateOrUpdateCapsuleMaterial("M_Tube_Capsule_White", capsuleShader,
            new Color(0.96f, 0.97f, 0.99f), new Color(0.2f, 0.85f, 1.0f));

        // 3. Prefab creation
        CreateTubePrefab("Prefab_Tube_StandardTransit35m.prefab",
            PneumaticTransitTubeController.TubePreset.StandardTransit,
            pGlass, pRings, pMounts, pTerminals, pCapsule,
            matGlassWhite, matStructWhite, matCapsuleWhite);

        CreateTubePrefab("Prefab_Tube_SkyscraperExpress80m.prefab",
            PneumaticTransitTubeController.TubePreset.SkyscraperExpress,
            pGlass, pRings, pMounts, pTerminals, pCapsule,
            matGlassCyan, matStructDark, matCapsuleWhite);

        CreateTubePrefab("Prefab_Tube_ObservationPillar45m.prefab",
            PneumaticTransitTubeController.TubePreset.ObservationPillar,
            pGlass, pRings, pMounts, pTerminals, pCapsule,
            matGlassWhite, matStructDark, matCapsuleWhite);

        CreateTubePrefab("Prefab_Tube_IndustrialFreight50m.prefab",
            PneumaticTransitTubeController.TubePreset.IndustrialFreight,
            pGlass, pRings, pMounts, pTerminals, pCapsule,
            matGlassCyan, matStructDark, matCapsuleWhite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_Tube_StandardTransit35m.prefab");
            if (p != null) EditorGUIUtility.PingObject(p);
        }
        Debug.Log("<color=#70D0FF>[PneumaticTransitTube]</color> 全アセットを正常に生成・保存しました。");
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
            existing.vertices  = newMesh.vertices;
            existing.normals   = newMesh.normals;
            existing.uv        = newMesh.uv;
            existing.triangles = newMesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
        }
    }

    private static Material CreateOrUpdateMaterial(string name, Shader shader, Color baseCol, Color fresnelCol)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseCol);
        m.SetColor("_FresnelColor", fresnelCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material CreateOrUpdateStructMaterial(string name, Shader shader, Color baseCol, Color accentCol, Color ledCol)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseCol);
        m.SetColor("_AccentColor", accentCol);
        m.SetColor("_EmissionColor", ledCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material CreateOrUpdateCapsuleMaterial(string name, Shader shader, Color baseCol, Color thrusterCol)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseCol);
        m.SetColor("_ThrusterColor", thrusterCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void CreateTubePrefab(string fileName, PneumaticTransitTubeController.TubePreset preset,
        Mesh glass, Mesh rings, Mesh mounts, Mesh terminals, Mesh capsule,
        Material matGlass, Material matStruct, Material matCapsule)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(fileName));
        PneumaticTransitTubeController ctrl = root.AddComponent<PneumaticTransitTubeController>();
        ctrl.ApplyPreset(preset);

        ctrl.EnsureHierarchy();
        ctrl.FindChildrenIfNeeded();

        // Assign meshes
        AssignMesh(root.transform.Find("GlassTube"), glass, matGlass);
        AssignMesh(root.transform.Find("StructuralRings"), rings, matStruct);
        AssignMesh(root.transform.Find("WallMounts"), mounts, matStruct);
        AssignMesh(root.transform.Find("EndTerminals"), terminals, matStruct);
        AssignMesh(root.transform.Find("CapsulePod"), capsule, matCapsule);

        ctrl.ApplyMaterialProperties();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void AssignMesh(Transform t, Mesh mesh, Material mat)
    {
        if (t == null) return;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf != null && mesh != null) mf.sharedMesh = mesh;
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
    }

    public static void SpawnShowcase(Vector3 basePosition)
    {
        string rootName = "--- PneumaticTransitTube Showcase ---";
        GameObject existing = GameObject.Find(rootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject showcaseRoot = new GameObject(rootName);
        showcaseRoot.transform.position = basePosition;
        showcaseRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
        Undo.RegisterCreatedObjectUndo(showcaseRoot, "Spawn PneumaticTransitTube Showcase");

        float[] xOffsets = new float[] { -18f, -6f, 6f, 18f };
        PneumaticTransitTubeController.TubePreset[] presets = new PneumaticTransitTubeController.TubePreset[]
        {
            PneumaticTransitTubeController.TubePreset.StandardTransit,
            PneumaticTransitTubeController.TubePreset.SkyscraperExpress,
            PneumaticTransitTubeController.TubePreset.ObservationPillar,
            PneumaticTransitTubeController.TubePreset.IndustrialFreight
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject item = new GameObject($"Showcase_{i + 1}_{presets[i]}");
            item.transform.SetParent(showcaseRoot.transform, false);
            item.transform.localPosition = new Vector3(xOffsets[i], 0, 0);

            PneumaticTransitTubeController ctrl = item.AddComponent<PneumaticTransitTubeController>();
            ctrl.ApplyPreset(presets[i]);
        }

        Debug.Log("<color=#70D0FF>[PneumaticTransitTube]</color> 展示ショーケースをシーン内に配置しました。");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf   = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
