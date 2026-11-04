#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// HologramProjectorSpireController 用のインスペクター拡張。
/// ・ワンクリックでのプリセット適用
/// ・ワンクリックでの照射ヘッド数＆カラーテーマ切替
/// ・全アセット（プレハブ4種・マテリアル・メッシュ）の一括生成・保存
/// ・シーン展示ショーケース（--- HologramProjectorSpire Showcase ---）のワンクリック配置
/// ・Toolsメニュー不使用（規約遵守）
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(HologramProjectorSpireController))]
public class HologramProjectorSpireEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/UrbanProps/HologramProjectorSpire";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    static HologramProjectorSpireEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/Projector_PedestalBase.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || m == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        HologramProjectorSpireController comp = (HologramProjectorSpireController)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔷 Hologram Projector Spire Quick Controls", EditorStyles.boldLabel);

        // 1. Preset Buttons
        EditorGUILayout.LabelField("ワンクリック・プリセット", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔷 クアッド・スパイヤ\n(4基 / 15m / Cyan)"))
        {
            Undo.RecordObject(comp, "Apply Preset QuadSpireArray");
            comp.ApplyPreset(HologramProjectorSpireController.SpirePreset.QuadSpireArray);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🏗️ 重工業三脚\n(3基 / 20m / Amber)"))
        {
            Undo.RecordObject(comp, "Apply Preset HeavyTripodPlatform");
            comp.ApplyPreset(HologramProjectorSpireController.SpirePreset.HeavyTripodPlatform);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🏢 屋上グランド・アレイ\n(6基 / 25m / White)"))
        {
            Undo.RecordObject(comp, "Apply Preset RooftopGrandArray");
            comp.ApplyPreset(HologramProjectorSpireController.SpirePreset.RooftopGrandArray);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🟢 コンパクト台座\n(4基 / 8m / Emerald)"))
        {
            Undo.RecordObject(comp, "Apply Preset CompactPedestal");
            comp.ApplyPreset(HologramProjectorSpireController.SpirePreset.CompactPedestal);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        // 2. Color Theme Buttons
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("カラーテーマ切替", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Cyber Cyan"))
        {
            Undo.RecordObject(comp, "Apply Theme CyberCyan");
            comp.ApplyTheme(HologramProjectorSpireController.ColorTheme.CyberCyan);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Industrial Amber"))
        {
            Undo.RecordObject(comp, "Apply Theme IndustrialAmber");
            comp.ApplyTheme(HologramProjectorSpireController.ColorTheme.IndustrialAmber);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Clean White"))
        {
            Undo.RecordObject(comp, "Apply Theme CleanWhite");
            comp.ApplyTheme(HologramProjectorSpireController.ColorTheme.CleanWhite);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Stealth Carbon"))
        {
            Undo.RecordObject(comp, "Apply Theme StealthCarbon");
            comp.ApplyTheme(HologramProjectorSpireController.ColorTheme.StealthCarbon);
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
        if (GUILayout.Button("🌟 シーン展示を配置 (--- HologramProjectorSpire Showcase ---)", GUILayout.Height(28)))
        {
            SpawnShowcase(new Vector3(0, 2, 25));
        }
        GUI.backgroundColor = Color.white;
    }

    public static void GenerateAllAssets(bool ping = false)
    {
        EnsureFolder(MeshesFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);

        // 1. Mesh generation
        Mesh baseMesh  = HologramProjectorSpireController.CreatePedestalBaseMesh(3.0f, 1.0f);
        Mesh headsMesh = HologramProjectorSpireController.CreateEmitterHeadsMesh(3.0f, 1.0f, 4, 15.0f);
        Mesh coreMesh  = HologramProjectorSpireController.CreateProjectorCoreMesh(3.0f, 1.0f);
        Mesh beamMesh  = HologramProjectorSpireController.CreateBeamConeMesh(3.0f * 0.72f, 6.0f, 15.0f, 1.0f);

        SaveOrUpdateMesh(baseMesh,  $"{MeshesFolder}/Projector_PedestalBase.asset");
        SaveOrUpdateMesh(headsMesh, $"{MeshesFolder}/Projector_EmitterHeads.asset");
        SaveOrUpdateMesh(coreMesh,  $"{MeshesFolder}/Projector_CoreCrystal.asset");
        SaveOrUpdateMesh(beamMesh,  $"{MeshesFolder}/Projector_BeamCone.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh pBase  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Projector_PedestalBase.asset");
        Mesh pHeads = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Projector_EmitterHeads.asset");
        Mesh pCore  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Projector_CoreCrystal.asset");
        Mesh pBeam  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Projector_BeamCone.asset");

        // 2. Materials
        Shader baseShader = Shader.Find("Custom/UrbanProps/S_ProjectorSpireBase");
        Shader beamShader = Shader.Find("Custom/UrbanProps/S_ProjectorBeamCone");

        Material matBaseCyan = CreateOrUpdateBaseMaterial("M_Projector_SpireBase_Cyan", baseShader,
            new Color(0.18f, 0.22f, 0.28f), new Color(0.85f, 0.90f, 0.95f), new Color(0.0f, 0.85f, 1.0f), new Color(0.0f, 1.0f, 0.9f));

        Material matBaseAmber = CreateOrUpdateBaseMaterial("M_Projector_SpireBase_Amber", baseShader,
            new Color(0.85f, 0.55f, 0.1f), new Color(0.14f, 0.14f, 0.16f), new Color(1.0f, 0.65f, 0.1f), new Color(1.0f, 0.85f, 0.2f));

        Material matBeamCyan = CreateOrUpdateBeamMaterial("M_Projector_BeamCone_Cyan", beamShader,
            new Color(0.0f, 0.88f, 1.0f, 1.0f), 1.8f);

        Material matBeamAmber = CreateOrUpdateBeamMaterial("M_Projector_BeamCone_Amber", beamShader,
            new Color(1.0f, 0.75f, 0.15f, 1.0f), 2.0f);

        // 3. Prefab creation
        CreateSpirePrefab("Prefab_Projector_QuadSpireArray15m.prefab",
            HologramProjectorSpireController.SpirePreset.QuadSpireArray,
            pBase, pHeads, pCore, pBeam,
            matBaseCyan, matBeamCyan);

        CreateSpirePrefab("Prefab_Projector_HeavyTripodPlatform20m.prefab",
            HologramProjectorSpireController.SpirePreset.HeavyTripodPlatform,
            pBase, pHeads, pCore, pBeam,
            matBaseAmber, matBeamAmber);

        CreateSpirePrefab("Prefab_Projector_RooftopGrandArray25m.prefab",
            HologramProjectorSpireController.SpirePreset.RooftopGrandArray,
            pBase, pHeads, pCore, pBeam,
            matBaseCyan, matBeamCyan);

        CreateSpirePrefab("Prefab_Projector_CompactPedestal8m.prefab",
            HologramProjectorSpireController.SpirePreset.CompactPedestal,
            pBase, pHeads, pCore, pBeam,
            matBaseCyan, matBeamCyan);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_Projector_QuadSpireArray15m.prefab");
            if (p != null) EditorGUIUtility.PingObject(p);
        }
        Debug.Log("<color=#70D0FF>[HologramProjectorSpire]</color> 全アセットを正常に生成・保存しました。");
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

    private static Material CreateOrUpdateBaseMaterial(string name, Shader shader, Color baseCol, Color accentCol, Color ventCol, Color ledCol)
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
        m.SetColor("_VentColor", ventCol);
        m.SetColor("_LedColor", ledCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material CreateOrUpdateBeamMaterial(string name, Shader shader, Color beamCol, float intensity)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BeamColor", beamCol);
        m.SetFloat("_BeamIntensity", intensity);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void CreateSpirePrefab(string fileName, HologramProjectorSpireController.SpirePreset preset,
        Mesh pedestal, Mesh heads, Mesh core, Mesh beam,
        Material matBase, Material matBeam)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(fileName));
        HologramProjectorSpireController ctrl = root.AddComponent<HologramProjectorSpireController>();
        ctrl.ApplyPreset(preset);

        ctrl.EnsureHierarchy();
        ctrl.FindChildrenIfNeeded();

        AssignMesh(root.transform.Find("PedestalBase"), pedestal, matBase);
        AssignMesh(root.transform.Find("EmitterHeads"), heads, matBase);
        AssignMesh(root.transform.Find("ProjectorCore"), core, matBase);
        AssignMesh(root.transform.Find("BeamCone"), beam, matBeam);

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
        string rootName = "--- HologramProjectorSpire Showcase ---";
        GameObject existing = GameObject.Find(rootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject showcaseRoot = new GameObject(rootName);
        showcaseRoot.transform.position = basePosition;
        showcaseRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
        Undo.RegisterCreatedObjectUndo(showcaseRoot, "Spawn HologramProjectorSpire Showcase");

        float[] xOffsets = new float[] { -15f, -5f, 5f, 15f };
        HologramProjectorSpireController.SpirePreset[] presets = new HologramProjectorSpireController.SpirePreset[]
        {
            HologramProjectorSpireController.SpirePreset.QuadSpireArray,
            HologramProjectorSpireController.SpirePreset.HeavyTripodPlatform,
            HologramProjectorSpireController.SpirePreset.RooftopGrandArray,
            HologramProjectorSpireController.SpirePreset.CompactPedestal
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject item = new GameObject($"Showcase_{i + 1}_{presets[i]}");
            item.transform.SetParent(showcaseRoot.transform, false);
            item.transform.localPosition = new Vector3(xOffsets[i], 0, 0);

            HologramProjectorSpireController ctrl = item.AddComponent<HologramProjectorSpireController>();
            ctrl.ApplyPreset(presets[i]);
        }

        Debug.Log("<color=#70D0FF>[HologramProjectorSpire]</color> 展示ショーケースをシーン内に配置しました。");
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
