using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
[CustomEditor(typeof(HologramFlagMesh))]
public class HologramFlagMeshEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/Hologram";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    static HologramFlagMeshEditor()
    {
        // スクリプトコンパイル後、まだ手動配置用のメッシュ素材フォルダが無ければ自動生成する
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(MeshesFolder) ||
                AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/Mesh_Flag_01_Rectangle.asset") == null)
            {
                GenerateAndSaveAllAssets(pingFolder: false);
            }
        };
    }

    public static void MenuGenerateAssets()
    {
        GenerateAndSaveAllAssets(pingFolder: true);
    }

    public override void OnInspectorGUI()
    {
        HologramFlagMesh comp = (HologramFlagMesh)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("① シェーダー 5種クイック切り替え（ホログラム ⇄ 通常布）", EditorStyles.boldLabel);

        HologramFlagMesh.HologramShaderType currShader = comp.GetShaderType();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawShaderButton(comp, "案A：空間スキャンライン旗", HologramFlagMesh.HologramShaderType.Flag_Scanline, currShader, new Color(0.35f, 0.9f, 1f));
            DrawShaderButton(comp, "案B：LEDドットマトリクス旗", HologramFlagMesh.HologramShaderType.Flag_LEDMatrix, currShader, new Color(0.4f, 1f, 0.65f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawShaderButton(comp, "案C：RGBグリッチ＆照射旗", HologramFlagMesh.HologramShaderType.Flag_CyberGlitch, currShader, new Color(1f, 0.6f, 0.95f));
            DrawShaderButton(comp, "🏳️ ホロ無し：通常布の旗", HologramFlagMesh.HologramShaderType.Flag_StandardCloth, currShader, new Color(1f, 0.78f, 0.35f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawShaderButton(comp, "🧊 3Dモデル汎用ホログラム (S_ModelHologram)", HologramFlagMesh.HologramShaderType.Model_Hologram, currShader, new Color(0.8f, 0.85f, 1f));
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 旗メッシュの形状（6種類）クイック切り替え", EditorStyles.boldLabel);

        HologramFlagMesh.FlagMeshShape currShape = comp.GetMeshShape();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawShapeButton(comp, "1. 長方形", HologramFlagMesh.FlagMeshShape.Rectangle, currShape);
            DrawShapeButton(comp, "2. 燕尾V字(二股)", HologramFlagMesh.FlagMeshShape.Swallowtail, currShape);
            DrawShapeButton(comp, "3. 三角ペナント", HologramFlagMesh.FlagMeshShape.TrianglePennant, currShape);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawShapeButton(comp, "4. 斜めカット", HologramFlagMesh.FlagMeshShape.AngledSlash, currShape);
            DrawShapeButton(comp, "5. 細長吹き流し", HologramFlagMesh.FlagMeshShape.TaperedStreamer, currShape);
            DrawShapeButton(comp, "6. 縦長垂れ幕", HologramFlagMesh.FlagMeshShape.VerticalBanner, currShape);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("③ 旗の絵柄パターン切り替え（テクスチャ未指定でも即確認可）", EditorStyles.boldLabel);

        HologramFlagMesh.BuiltinPatternType currPat = comp.GetPatternType();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPatternButton(comp, "テクスチャのみ", HologramFlagMesh.BuiltinPatternType.TextureOnly, currPat);
            DrawPatternButton(comp, "🏁 チェッカー", HologramFlagMesh.BuiltinPatternType.CheckerFlag, currPat);
            DrawPatternButton(comp, "⏩ サイバー矢印", HologramFlagMesh.BuiltinPatternType.CyberArrows, currPat);
            DrawPatternButton(comp, "⚡ ストライプ", HologramFlagMesh.BuiltinPatternType.RacingStripes, currPat);
        }

        EditorGUILayout.Space(6f);

        GUI.backgroundColor = new Color(0.75f, 0.95f, 1f);
        if (GUILayout.Button("✨ シーン内に全パターン比較サンプル（ホロ旗・通常布旗・6形状）を一括生成", GUILayout.Height(30f)))
        {
            SpawnComparisonShowcase(comp.transform);
        }

        GUI.backgroundColor = new Color(0.8f, 1f, 0.75f);
        if (GUILayout.Button("📁 手動配置用アセット（6種メッシュ・5種マテリアル・プレハブ）をフォルダに保存/更新", GUILayout.Height(30f)))
        {
            GenerateAndSaveAllAssets(pingFolder: true);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6f);
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Rebuild（メッシュ＆マテリアル再生成）", GUILayout.Height(26f)))
        {
            comp.RebuildAll();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawShaderButton(
        HologramFlagMesh comp,
        string label,
        HologramFlagMesh.HologramShaderType targetType,
        HologramFlagMesh.HologramShaderType currentType,
        Color activeColor)
    {
        GUI.backgroundColor = (currentType == targetType) ? activeColor : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(25f)))
        {
            Undo.RecordObject(comp, "Switch Flag Shader");
            comp.SetShaderType(targetType);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawShapeButton(
        HologramFlagMesh comp,
        string label,
        HologramFlagMesh.FlagMeshShape targetShape,
        HologramFlagMesh.FlagMeshShape currentShape)
    {
        GUI.backgroundColor = (currentShape == targetShape) ? new Color(0.55f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
        {
            Undo.RecordObject(comp, "Switch Flag Shape");
            comp.SetMeshShape(targetShape);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawPatternButton(
        HologramFlagMesh comp,
        string label,
        HologramFlagMesh.BuiltinPatternType targetPat,
        HologramFlagMesh.BuiltinPatternType currentPat)
    {
        GUI.backgroundColor = (currentPat == targetPat) ? new Color(1f, 0.9f, 0.4f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
        {
            Undo.RecordObject(comp, "Switch Flag Pattern");
            comp.SetPatternType(targetPat);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    // ──────────────────────────────────────────────
    // 手動配置用のメッシュ(.asset) / マテリアル(.mat) / プレハブ(.prefab) のフォルダ保存処理
    // ──────────────────────────────────────────────

    public static void GenerateAndSaveAllAssets(bool pingFolder)
    {
        EnsureFolderExists(BaseFolder);
        EnsureFolderExists(MeshesFolder);
        EnsureFolderExists(MaterialsFolder);
        EnsureFolderExists(PrefabsFolder);

        // 1. 6種類の分割済みフラッグメッシュ（.asset）を Meshes フォルダに保存
        Mesh meshRect     = SaveOrUpdateMeshAsset("Mesh_Flag_01_Rectangle.asset",       HologramFlagMesh.FlagMeshShape.Rectangle,       4.0f, 2.0f);
        Mesh meshSwallow  = SaveOrUpdateMeshAsset("Mesh_Flag_02_Swallowtail.asset",     HologramFlagMesh.FlagMeshShape.Swallowtail,     4.2f, 2.0f);
        Mesh meshPennant  = SaveOrUpdateMeshAsset("Mesh_Flag_03_TrianglePennant.asset", HologramFlagMesh.FlagMeshShape.TrianglePennant, 4.2f, 2.0f);
        Mesh meshSlash    = SaveOrUpdateMeshAsset("Mesh_Flag_04_AngledSlash.asset",     HologramFlagMesh.FlagMeshShape.AngledSlash,     4.2f, 2.0f);
        Mesh meshStreamer = SaveOrUpdateMeshAsset("Mesh_Flag_05_TaperedStreamer.asset", HologramFlagMesh.FlagMeshShape.TaperedStreamer, 4.0f, 2.0f);
        Mesh meshVertical = SaveOrUpdateMeshAsset("Mesh_Flag_06_VerticalBanner.asset",  HologramFlagMesh.FlagMeshShape.VerticalBanner,  4.0f, 2.0f);

        // 2. 5種類のマテリアル（.mat）を Materials フォルダに保存
        Material matCloth = SaveOrUpdateMaterialAsset(
            "M_Flag_00_StandardCloth.mat",
            HologramFlagMesh.HologramShaderType.Flag_StandardCloth,
            HologramFlagMesh.BuiltinPatternType.CheckerFlag,
            new Color(0.95f, 0.95f, 0.98f, 1.0f),
            new Color(1.0f, 0.75f, 0.15f, 1.0f));

        Material matScanline = SaveOrUpdateMaterialAsset(
            "M_Flag_01_HoloScanline.mat",
            HologramFlagMesh.HologramShaderType.Flag_Scanline,
            HologramFlagMesh.BuiltinPatternType.CheckerFlag,
            new Color(0.0f, 2.2f, 3.2f, 0.85f),
            new Color(0.6f, 4.0f, 4.5f, 1.0f));

        Material matLED = SaveOrUpdateMaterialAsset(
            "M_Flag_02_HoloLEDMatrix.mat",
            HologramFlagMesh.HologramShaderType.Flag_LEDMatrix,
            HologramFlagMesh.BuiltinPatternType.CyberArrows,
            new Color(0.0f, 3.2f, 1.6f, 0.95f),
            new Color(0.2f, 4.5f, 2.5f, 1.0f));

        Material matGlitch = SaveOrUpdateMaterialAsset(
            "M_Flag_03_HoloCyberGlitch.mat",
            HologramFlagMesh.HologramShaderType.Flag_CyberGlitch,
            HologramFlagMesh.BuiltinPatternType.RacingStripes,
            new Color(0.3f, 2.0f, 3.8f, 0.9f),
            new Color(1.5f, 4.0f, 4.5f, 1.0f));

        SaveOrUpdateMaterialAsset(
            "M_Model_04_Hologram.mat",
            HologramFlagMesh.HologramShaderType.Model_Hologram,
            HologramFlagMesh.BuiltinPatternType.TextureOnly,
            new Color(0.0f, 1.8f, 2.6f, 0.5f),
            new Color(0.5f, 3.5f, 4.0f, 0.95f));

        // 3. スクリプト無し（標準の MeshFilter + MeshRenderer のみ）で手動配置できるプレハブを Prefabs フォルダに保存
        SaveFlagPrefab("P_Flag_00_StandardCloth_Checker.prefab", meshRect,     matCloth);
        SaveFlagPrefab("P_Flag_01_Rectangle_HoloScan.prefab",    meshRect,     matScanline);
        SaveFlagPrefab("P_Flag_02_Swallowtail_HoloScan.prefab",  meshSwallow,  matScanline);
        SaveFlagPrefab("P_Flag_03_Pennant_HoloGlitch.prefab",    meshPennant,  matGlitch);
        SaveFlagPrefab("P_Flag_04_AngledSlash_HoloLED.prefab",   meshSlash,    matLED);
        SaveFlagPrefab("P_Flag_05_Streamer_HoloLED.prefab",      meshStreamer, matLED);
        SaveFlagPrefab("P_Flag_06_VerticalBanner_Holo.prefab",   meshVertical, matScanline);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (pingFolder)
        {
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(MeshesFolder);
            if (folderObj != null)
            {
                EditorGUIUtility.PingObject(folderObj);
            }
            Debug.Log($"[HologramFlagMesh] 手動配置用の6種メッシュ(.asset)・5種マテリアル(.mat)・7種プレハブ(.prefab)を '{BaseFolder}' 以下に保存しました！");
        }
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolderExists(parent);
        }
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Mesh SaveOrUpdateMeshAsset(string fileName, HologramFlagMesh.FlagMeshShape shape, float w, float h)
    {
        string assetPath = $"{MeshesFolder}/{fileName}";
        Mesh generated = HologramFlagMesh.CreateFlagMesh(shape, w, h, 32, 16);
        generated.name = Path.GetFileNameWithoutExtension(fileName);

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
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
            AssetDatabase.CreateAsset(generated, assetPath);
            return generated;
        }
    }

    private static Material SaveOrUpdateMaterialAsset(
        string fileName,
        HologramFlagMesh.HologramShaderType shaderType,
        HologramFlagMesh.BuiltinPatternType patternType,
        Color mainColor,
        Color borderColor)
    {
        string assetPath = $"{MaterialsFolder}/{fileName}";
        Shader shader = Shader.Find(HologramFlagMesh.GetShaderPath(shaderType));
        if (shader == null) return null;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_PatternMode")) mat.SetFloat("_PatternMode", (float)patternType);
        if (mat.HasProperty("_MainColor"))   mat.SetColor("_MainColor", mainColor);
        if (mat.HasProperty("_BorderColor")) mat.SetColor("_BorderColor", borderColor);
        if (mat.HasProperty("_RimColor"))    mat.SetColor("_RimColor", borderColor);
        if (mat.HasProperty("_EmitterColor")) mat.SetColor("_EmitterColor", borderColor);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SaveFlagPrefab(string fileName, Mesh mesh, Material mat)
    {
        if (mesh == null || mat == null) return;

        string prefabPath = $"{PrefabsFolder}/{fileName}";
        GameObject tempObj = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        MeshFilter mf = tempObj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = tempObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);
        DestroyImmediate(tempObj);
    }

    // ──────────────────────────────────────────────
    // シーン内への全パターン横並び比較スポーン
    // ──────────────────────────────────────────────

    private static void SpawnComparisonShowcase(Transform originTransform)
    {
        GameObject root = new GameObject("HologramFlag_Comparison_Showcase");
        Undo.RegisterCreatedObjectUndo(root, "Spawn Hologram Comparison Showcase");
        root.transform.position = originTransform.position;
        root.transform.rotation = originTransform.rotation;

        // 上段：シェーダー種類の比較（通常布 ＋ ホログラム3種 ＋ 3Dモデル）
        CreateSample(root.transform, "0_StandardCloth_Rectangle (ホロ無し・通常布チェッカー)", new Vector3(-10f, 3.0f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_StandardCloth,
            HologramFlagMesh.FlagMeshShape.Rectangle,
            HologramFlagMesh.BuiltinPatternType.CheckerFlag,
            new Color(0.95f, 0.95f, 0.98f, 1f), new Color(1f, 0.75f, 0.15f, 1f));

        CreateSample(root.transform, "1_HoloScanline_Swallowtail (空間スキャンライン＋燕尾V字)", new Vector3(-4.5f, 3.0f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_Scanline,
            HologramFlagMesh.FlagMeshShape.Swallowtail,
            HologramFlagMesh.BuiltinPatternType.CheckerFlag,
            new Color(0.0f, 2.2f, 3.2f, 0.85f), new Color(0.6f, 4.0f, 4.5f, 1f));

        CreateSample(root.transform, "2_HoloLED_AngledSlash (LEDドット＋斜めカット＋矢印)", new Vector3(1.0f, 3.0f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_LEDMatrix,
            HologramFlagMesh.FlagMeshShape.AngledSlash,
            HologramFlagMesh.BuiltinPatternType.CyberArrows,
            new Color(0.0f, 3.2f, 1.6f, 0.95f), new Color(0.2f, 4.5f, 2.5f, 1f));

        CreateSample(root.transform, "3_HoloGlitch_Pennant (RGBグリッチ＋三角ペナント)", new Vector3(6.5f, 3.0f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_CyberGlitch,
            HologramFlagMesh.FlagMeshShape.TrianglePennant,
            HologramFlagMesh.BuiltinPatternType.RacingStripes,
            new Color(0.3f, 2.0f, 3.8f, 0.9f), new Color(1.5f, 4.0f, 4.5f, 1f));

        // 下段：特殊形状（吹き流しストリーマー ＆ 縦長垂れ幕バナー ＆ 通常布ペナント）
        CreateSample(root.transform, "4_StandardCloth_Swallowtail (ホロ無し・通常布燕尾フラッグ)", new Vector3(-10f, -0.5f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_StandardCloth,
            HologramFlagMesh.FlagMeshShape.Swallowtail,
            HologramFlagMesh.BuiltinPatternType.RacingStripes,
            new Color(0.95f, 0.2f, 0.25f, 1f), new Color(1f, 0.85f, 0.2f, 1f));

        CreateSample(root.transform, "5_HoloStreamer (細長吹き流しストリーマー)", new Vector3(-4.5f, -0.5f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_LEDMatrix,
            HologramFlagMesh.FlagMeshShape.TaperedStreamer,
            HologramFlagMesh.BuiltinPatternType.CyberArrows,
            new Color(0.0f, 2.8f, 2.5f, 0.95f), new Color(0.5f, 4.2f, 3.5f, 1f));

        CreateSample(root.transform, "6_HoloVerticalBanner (上端固定・縦長垂れ幕バナー)", new Vector3(3.0f, 1.2f, 0f),
            HologramFlagMesh.HologramShaderType.Flag_Scanline,
            HologramFlagMesh.FlagMeshShape.VerticalBanner,
            HologramFlagMesh.BuiltinPatternType.CheckerFlag,
            new Color(0.0f, 2.4f, 3.2f, 0.85f), new Color(0.6f, 4.0f, 4.5f, 1f));

        // 3Dモデル汎用ホログラム
        GameObject modelSample = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        modelSample.name = "7_Model_Hologram (3Dモデル汎用ホログラム)";
        modelSample.transform.SetParent(root.transform, false);
        modelSample.transform.localPosition = new Vector3(8.5f, -0.5f, 0f);
        modelSample.transform.localScale = Vector3.one * 2.2f;

        Collider col = modelSample.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        HologramFlagMesh modelHolo = modelSample.AddComponent<HologramFlagMesh>();
        modelHolo.ConfigurePreset(
            HologramFlagMesh.HologramShaderType.Model_Hologram,
            HologramFlagMesh.FlagMeshShape.Rectangle,
            HologramFlagMesh.BuiltinPatternType.TextureOnly,
            new Color(0.0f, 2.0f, 3.0f, 0.55f),
            new Color(0.5f, 3.5f, 4.0f, 0.95f),
            generateFlagMesh: false);

        Selection.activeGameObject = root;
    }

    private static void CreateSample(
        Transform parent,
        string name,
        Vector3 localPos,
        HologramFlagMesh.HologramShaderType shaderType,
        HologramFlagMesh.FlagMeshShape meshShape,
        HologramFlagMesh.BuiltinPatternType patternType,
        Color mainColor,
        Color borderColor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;

        HologramFlagMesh flag = obj.AddComponent<HologramFlagMesh>();
        flag.ConfigurePreset(shaderType, meshShape, patternType, mainColor, borderColor, generateFlagMesh: true);
    }
}
