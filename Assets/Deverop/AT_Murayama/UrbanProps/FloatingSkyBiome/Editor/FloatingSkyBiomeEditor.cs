#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// FloatingSkyBiomeController 用のインスペクター拡張。
/// ・ワンクリックでのプリセット適用
/// ・ワンクリックでのスタイル＆エコバイオーム切替
/// ・全アセット（プレハブ4種・マテリアル・メッシュ）の一括生成・保存
/// ・シーン展示ショーケース（--- FloatingSkyBiome Showcase ---）のワンクリック配置
/// ・Toolsメニュー不使用（規約遵守）
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(FloatingSkyBiomeController))]
public class FloatingSkyBiomeEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/UrbanProps/FloatingSkyBiome";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    static FloatingSkyBiomeEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/Biome_GeodesicDome.asset");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || m == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        FloatingSkyBiomeController comp = (FloatingSkyBiomeController)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🌿 Floating Sky-Biome Quick Controls", EditorStyles.boldLabel);

        // 1. Preset Buttons
        EditorGUILayout.LabelField("ワンクリック・プリセット", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌿 スカイバイオーム標準\n(10m / Tropical)"))
        {
            Undo.RecordObject(comp, "Apply Preset StandardSkyDome");
            comp.ApplyPreset(FloatingSkyBiomeController.BiomePreset.StandardSkyDome);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🪐 メガバイオーム球体\n(22m / Cyber)"))
        {
            Undo.RecordObject(comp, "Apply Preset MegaBiomeSphere");
            comp.ApplyPreset(FloatingSkyBiomeController.BiomePreset.MegaBiomeSphere);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🏔️ 高山ピナクル\n(14m / Alpine)"))
        {
            Undo.RecordObject(comp, "Apply Preset AlpineTerracePinnacle");
            comp.ApplyPreset(FloatingSkyBiomeController.BiomePreset.AlpineTerracePinnacle);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("🌸 展望ミニポッド\n(6m / Sakura)"))
        {
            Undo.RecordObject(comp, "Apply Preset ObservationMiniPod");
            comp.ApplyPreset(FloatingSkyBiomeController.BiomePreset.ObservationMiniPod);
            EditorUtility.SetDirty(comp);
        }
        EditorGUILayout.EndHorizontal();

        // 2. Eco Theme Buttons
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("生態系バイオーム切替", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Lush Tropical"))
        {
            Undo.RecordObject(comp, "Apply Theme LushTropical");
            comp.ApplyTheme(FloatingSkyBiomeController.EcoBiomeType.LushTropical);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Cyber Bioluminescent"))
        {
            Undo.RecordObject(comp, "Apply Theme CyberBioluminescent");
            comp.ApplyTheme(FloatingSkyBiomeController.EcoBiomeType.CyberBioluminescent);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Alpine Terrace"))
        {
            Undo.RecordObject(comp, "Apply Theme AlpineTerrace");
            comp.ApplyTheme(FloatingSkyBiomeController.EcoBiomeType.AlpineTerrace);
            EditorUtility.SetDirty(comp);
        }
        if (GUILayout.Button("Sakura Arboretum"))
        {
            Undo.RecordObject(comp, "Apply Theme SakuraArboretum");
            comp.ApplyTheme(FloatingSkyBiomeController.EcoBiomeType.SakuraArboretum);
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
        if (GUILayout.Button("🌟 シーン展示を配置 (--- FloatingSkyBiome Showcase ---)", GUILayout.Height(28)))
        {
            SpawnShowcase(new Vector3(0, 65, 25));
        }
        GUI.backgroundColor = Color.white;
    }

    public static void GenerateAllAssets(bool ping = false)
    {
        EnsureFolder(MeshesFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);

        // 1. Mesh generation
        Mesh domeMesh   = FloatingSkyBiomeController.CreateGeodesicDomeMesh(10.0f, FloatingSkyBiomeController.DomeStyle.UpperHemisphere);
        Mesh ringMesh   = FloatingSkyBiomeController.CreateAntiGravRingMesh(10.0f, FloatingSkyBiomeController.DomeStyle.UpperHemisphere);
        Mesh gardenMesh = FloatingSkyBiomeController.CreateTerracedGardenMesh(10.0f, FloatingSkyBiomeController.DomeStyle.UpperHemisphere);
        Mesh spireMesh  = FloatingSkyBiomeController.CreateBioSpireMesh(10.0f, 12.0f, FloatingSkyBiomeController.DomeStyle.UpperHemisphere);

        SaveOrUpdateMesh(domeMesh,   $"{MeshesFolder}/Biome_GeodesicDome.asset");
        SaveOrUpdateMesh(ringMesh,   $"{MeshesFolder}/Biome_AntiGravRing.asset");
        SaveOrUpdateMesh(gardenMesh, $"{MeshesFolder}/Biome_TerracedGarden.asset");
        SaveOrUpdateMesh(spireMesh,  $"{MeshesFolder}/Biome_BioSpire.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh pDome   = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Biome_GeodesicDome.asset");
        Mesh pRing   = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Biome_AntiGravRing.asset");
        Mesh pGarden = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Biome_TerracedGarden.asset");
        Mesh pSpire  = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesFolder}/Biome_BioSpire.asset");

        // 2. Materials
        Shader glassShader = Shader.Find("Custom/UrbanProps/S_SkyBiomeDomeGlass");
        Shader ringShader  = Shader.Find("Custom/UrbanProps/S_SkyBiomeAntiGrav");
        Shader foliageShader = Shader.Find("Custom/UrbanProps/S_SkyBiomeFoliageLit");

        Material matGlass = CreateOrUpdateMaterial("M_Biome_DomeGlass", glassShader,
            new Color(0.85f, 0.96f, 1.0f, 0.22f), new Color(0.75f, 0.95f, 1.0f, 0.85f));

        Material matRing = CreateOrUpdateRingMaterial("M_Biome_AntiGravChassis", ringShader,
            new Color(0.16f, 0.20f, 0.26f), new Color(0.88f, 0.92f, 0.96f), new Color(0.0f, 0.85f, 1.0f));

        Material matFoliageTropical = CreateOrUpdateFoliageMaterial("M_Biome_FoliageTropical", foliageShader,
            new Color(0.16f, 0.52f, 0.20f), new Color(0.40f, 0.85f, 0.32f), new Color(0.75f, 0.95f, 0.25f), new Color(0.1f, 0.95f, 0.8f));

        Material matFoliageCyber = CreateOrUpdateFoliageMaterial("M_Biome_FoliageCyber", foliageShader,
            new Color(0.25f, 0.12f, 0.45f), new Color(0.1f, 0.75f, 0.85f), new Color(0.6f, 0.2f, 0.9f), new Color(0.0f, 1.0f, 0.9f));

        // 3. Prefab creation
        CreateBiomePrefab("Prefab_Biome_StandardSkyDome10m.prefab",
            FloatingSkyBiomeController.BiomePreset.StandardSkyDome,
            pDome, pRing, pGarden, pSpire,
            matGlass, matRing, matFoliageTropical);

        CreateBiomePrefab("Prefab_Biome_MegaSphere22m.prefab",
            FloatingSkyBiomeController.BiomePreset.MegaBiomeSphere,
            pDome, pRing, pGarden, pSpire,
            matGlass, matRing, matFoliageCyber);

        CreateBiomePrefab("Prefab_Biome_AlpineTerracePinnacle14m.prefab",
            FloatingSkyBiomeController.BiomePreset.AlpineTerracePinnacle,
            pDome, pRing, pGarden, pSpire,
            matGlass, matRing, matFoliageTropical);

        CreateBiomePrefab("Prefab_Biome_ObservationMiniPod6m.prefab",
            FloatingSkyBiomeController.BiomePreset.ObservationMiniPod,
            pDome, pRing, pGarden, pSpire,
            matGlass, matRing, matFoliageTropical);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Prefab_Biome_StandardSkyDome10m.prefab");
            if (p != null) EditorGUIUtility.PingObject(p);
        }
        Debug.Log("<color=#70D0FF>[FloatingSkyBiome]</color> 全アセットを正常に生成・保存しました。");
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

    private static Material CreateOrUpdateRingMaterial(string name, Shader shader, Color baseCol, Color accentCol, Color repulsorCol)
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
        m.SetColor("_RepulsorColor", repulsorCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material CreateOrUpdateFoliageMaterial(string name, Shader shader, Color baseCol, Color tipCol, Color sssCol, Color glowCol)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseCol);
        m.SetColor("_TipColor", tipCol);
        m.SetColor("_SubsurfaceColor", sssCol);
        m.SetColor("_BioGlowColor", glowCol);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void CreateBiomePrefab(string fileName, FloatingSkyBiomeController.BiomePreset preset,
        Mesh dome, Mesh ring, Mesh garden, Mesh spire,
        Material matGlass, Material matRing, Material matFoliage)
    {
        string path = $"{PrefabsFolder}/{fileName}";
        GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(fileName));
        FloatingSkyBiomeController ctrl = root.AddComponent<FloatingSkyBiomeController>();
        ctrl.ApplyPreset(preset);

        ctrl.EnsureHierarchy();
        ctrl.FindChildrenIfNeeded();

        AssignMesh(root.transform.Find("GeodesicDome"), dome, matGlass);
        AssignMesh(root.transform.Find("AntiGravRing"), ring, matRing);
        AssignMesh(root.transform.Find("TerracedGarden"), garden, matFoliage);
        AssignMesh(root.transform.Find("BioSpire"), spire, matFoliage);

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
        string rootName = "--- FloatingSkyBiome Showcase ---";
        GameObject existing = GameObject.Find(rootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject showcaseRoot = new GameObject(rootName);
        showcaseRoot.transform.position = basePosition;
        showcaseRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
        Undo.RegisterCreatedObjectUndo(showcaseRoot, "Spawn FloatingSkyBiome Showcase");

        float[] xOffsets = new float[] { -22f, -7f, 7f, 22f };
        FloatingSkyBiomeController.BiomePreset[] presets = new FloatingSkyBiomeController.BiomePreset[]
        {
            FloatingSkyBiomeController.BiomePreset.StandardSkyDome,
            FloatingSkyBiomeController.BiomePreset.MegaBiomeSphere,
            FloatingSkyBiomeController.BiomePreset.AlpineTerracePinnacle,
            FloatingSkyBiomeController.BiomePreset.ObservationMiniPod
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject item = new GameObject($"Showcase_{i + 1}_{presets[i]}");
            item.transform.SetParent(showcaseRoot.transform, false);
            item.transform.localPosition = new Vector3(xOffsets[i], 0, 0);

            FloatingSkyBiomeController ctrl = item.AddComponent<FloatingSkyBiomeController>();
            ctrl.ApplyPreset(presets[i]);
        }

        Debug.Log("<color=#70D0FF>[FloatingSkyBiome]</color> 展示ショーケースをシーン内に配置しました。");
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
