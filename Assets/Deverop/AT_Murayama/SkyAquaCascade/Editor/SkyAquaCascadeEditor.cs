using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// SkyAquaCascadeController 用のインスペクター拡張。
/// ・チーム規約厳守：上部Toolsメニュー項目は一切追加せず、全操作はインスペクターUIで完結。
/// ・1クリック4大プリセット切替（標準スパウト滝＆虹、ワイド大瀑布＆二重虹、プリズム虹ゲート単体、高高度スリム滝）
/// ・1クリック構成要素トグル（放水口金具 / 滝メッシュ / 円形プリズム虹 / 3D水煙・飛沫パーティクル）
/// ・虹のカメラ追従モード切替（常時正対 / Y軸回転のみ / 固定角度）
/// ・マテリアル別名保存（Save As）
/// ・2Kシームレス流体テクスチャ生成、ネイティブメッシュ・マテリアル・プレハブ一括生成、展示ショーケース配置
/// </summary>
[InitializeOnLoad]
[CustomEditor(typeof(SkyAquaCascadeController))]
public class SkyAquaCascadeEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/SkyAquaCascade";
    private const string ShadersFolder   = BaseFolder + "/Shaders";
    private const string TexturesFolder  = BaseFolder + "/Textures";
    private const string MaterialsFolder = BaseFolder + "/Materials";
    private const string MeshesFolder    = BaseFolder + "/Meshes";
    private const string PrefabsFolder   = BaseFolder + "/Prefabs";

    private static bool _showAssetTools = false;

    static SkyAquaCascadeEditor()
    {
        EditorApplication.delayCall += () =>
        {
            Mesh stdCore = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/Mesh_CascadeCore_Standard.asset");
            Texture2D fluidTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesFolder + "/T_AquaCascade_FluidNoise.png");
            if (!AssetDatabase.IsValidFolder(MeshesFolder) || stdCore == null || fluidTex == null)
            {
                GenerateAllAssets(ping: false);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        SkyAquaCascadeController comp = (SkyAquaCascadeController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【クイック設定】4大プリセット 1クリック切替", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(
                comp,
                "💧 ① 標準：建築スパウト滝＆虹\n(8m幅 × 80m落下 ＋ 半径22m虹)",
                SkyAquaCascadeController.CascadePreset.StandardSpoutAndHalo,
                new Color(0.70f, 0.94f, 1.0f));

            DrawPresetButton(
                comp,
                "🌊 ② ワイドカーテン大瀑布＆二重虹\n(20m幅 × 120m落下 ＋ 半径38m二重虹)",
                SkyAquaCascadeController.CascadePreset.WideTerraceMegafall,
                new Color(0.65f, 0.88f, 1.0f));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(
                comp,
                "🌈 ③ プリズム虹ゲート単体（滝なし）\n(半径32m 主虹＋副虹＋水滴煌めき)",
                SkyAquaCascadeController.CascadePreset.PrismaticSolarHaloOnly,
                new Color(1.0f, 0.88f, 0.95f));

            DrawPresetButton(
                comp,
                "✨ ④ 高高度スリム・ミストフォール\n(3.5m幅 × 150m長尺霧散滝 ＋ 小型虹)",
                SkyAquaCascadeController.CascadePreset.SlenderHighMistfall,
                new Color(0.80f, 0.98f, 0.92f));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【構成コンポーネント ON / OFF 切替】", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawToggleButton(comp.ShowSpoutHardware, "🏗️ 建築放水スパウト: ON", "🏗️ 建築放水スパウト: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Spout Hardware");
                comp.SetShowSpoutHardware(!comp.ShowSpoutHardware);
                EditorUtility.SetDirty(comp);
            });

            DrawToggleButton(comp.ShowWaterfallCascade, "🌊 滝メッシュ(2層): ON", "🌊 滝メッシュ(2層): OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Waterfall Cascade");
                comp.SetShowWaterfallCascade(!comp.ShowWaterfallCascade);
                EditorUtility.SetDirty(comp);
            });
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawToggleButton(comp.ShowPrismaticRainbow, "🌈 円形プリズム虹: ON", "🌈 円形プリズム虹: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle Prismatic Rainbow");
                comp.SetShowPrismaticRainbow(!comp.ShowPrismaticRainbow);
                EditorUtility.SetDirty(comp);
            });

            DrawToggleButton(comp.Enable3DMistParticles, "☁️ 3D水煙＆飛沫粒: ON", "☁️ 3D水煙＆飛沫粒: OFF", () =>
            {
                Undo.RecordObject(comp, "Toggle 3D Mist Particles");
                comp.SetEnable3DMistParticles(!comp.Enable3DMistParticles);
                EditorUtility.SetDirty(comp);
            });
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("【円形プリズム虹 カメラ追従モード】", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawBillboardButton(comp, "カメラ常時正対", SkyAquaCascadeController.RainbowBillboardMode.FullCameraFacing);
            DrawBillboardButton(comp, "Y軸回転のみ追従", SkyAquaCascadeController.RainbowBillboardMode.YAxisOnly);
            DrawBillboardButton(comp, "固定角度(ゲート用)", SkyAquaCascadeController.RainbowBillboardMode.FixedOrientation);
        }

        EditorGUILayout.Space(6f);

        // --- マテリアル別名保存セクション ---
        EditorGUILayout.LabelField("【マテリアル別名保存 (Save As)】", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (comp.CustomWaterfallMaterial != null || comp.CustomRainbowMaterial != null)
            {
                EditorGUILayout.HelpBox("現在、別名保存したカスタムマテリアルが適用されています。", MessageType.Info);
                GUI.backgroundColor = new Color(1f, 0.85f, 0.7f);
                if (GUILayout.Button("🔄 カスタムマテリアル解除（標準共有マテリアルに戻る）", GUILayout.Height(24f)))
                {
                    Undo.RecordObject(comp, "Clear Custom Materials");
                    comp.ClearCustomMaterials();
                    EditorUtility.SetDirty(comp);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(4f);
            }

            string defaultPlaceholder = $"M_AquaCascade_{comp.gameObject.name}";
            comp.CustomSaveName = EditorGUILayout.TextField("保存ファイル名", comp.CustomSaveName);
            if (string.IsNullOrWhiteSpace(comp.CustomSaveName))
            {
                EditorGUILayout.LabelField($"※空欄の場合は「{defaultPlaceholder}.mat」で保存されます", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.35f, 0.95f, 0.6f);
                if (GUILayout.Button("💾 滝＆虹マテリアルを別名保存＆適用", GUILayout.Height(26f)))
                {
                    SaveAsCustomMaterials(comp);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.Space(6f);

        // --- アセット一括生成 ＆ 展示ショーケース配置 ---
        _showAssetTools = EditorGUILayout.Foldout(_showAssetTools, "【セットアップ・アセット生成 & 展示ショーケース】", true);
        if (_showAssetTools)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.backgroundColor = new Color(0.75f, 0.95f, 1f);
                if (GUILayout.Button("✨ シーン内に展示ショーケース（--- SkyAquaCascade Showcase ---）を配置", GUILayout.Height(30f)))
                {
                    SpawnSkyAquaCascadeShowcase(comp.transform.position);
                }

                EditorGUILayout.Space(2f);
                GUI.backgroundColor = new Color(0.85f, 1f, 0.8f);
                if (GUILayout.Button("📁 4種プレハブ・6種マテリアル・16種メッシュ・2Kテクスチャを一括再生成", GUILayout.Height(26f)))
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
            comp.UpdateCascade();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawPresetButton(
        SkyAquaCascadeController comp,
        string label,
        SkyAquaCascadeController.CascadePreset preset,
        Color tint)
    {
        bool isCurrent = comp.Preset == preset;
        GUI.backgroundColor = isCurrent ? tint : new Color(tint.r * 0.82f, tint.g * 0.82f, tint.b * 0.82f);
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };

        if (GUILayout.Button(label, style, GUILayout.Height(38f)))
        {
            Undo.RecordObject(comp, $"Apply Preset {preset}");
            comp.ApplyPreset(preset);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawToggleButton(bool state, string onLabel, string offLabel, System.Action onClick)
    {
        GUI.backgroundColor = state ? new Color(0.55f, 0.95f, 0.72f) : new Color(0.82f, 0.82f, 0.82f);
        if (GUILayout.Button(state ? onLabel : offLabel, GUILayout.Height(26f)))
        {
            onClick?.Invoke();
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawBillboardButton(
        SkyAquaCascadeController comp,
        string label,
        SkyAquaCascadeController.RainbowBillboardMode mode)
    {
        bool isCurrent = comp.BillboardMode == mode;
        GUI.backgroundColor = isCurrent ? new Color(0.65f, 0.92f, 1.0f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
        {
            Undo.RecordObject(comp, "Change Rainbow Billboard Mode");
            comp.SetBillboardMode(mode);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SaveAsCustomMaterials(SkyAquaCascadeController comp)
    {
        EnsureDirectories();
        string baseName = string.IsNullOrWhiteSpace(comp.CustomSaveName)
            ? $"M_AquaCascade_{comp.gameObject.name}"
            : comp.CustomSaveName.Trim();

        Material srcWater = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_AquaCascade_CoreWater.mat");
        Material srcRainbow = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_PrismaticRainbow_Halo.mat");

        Undo.RecordObject(comp, "Save Custom AquaCascade Materials");

        if (srcWater != null)
        {
            string waterPath = AssetDatabase.GenerateUniqueAssetPath($"{MaterialsFolder}/{baseName}_Water.mat");
            Material newWater = new Material(srcWater);
            newWater.SetFloat("_FlowSpeed", comp.FlowSpeed);
            newWater.SetFloat("_Turbulence", comp.Turbulence);
            newWater.SetFloat("_RefractionStrength", comp.RefractionStrength);
            newWater.SetFloat("_EvaporationStart", comp.EvaporationStart);
            AssetDatabase.CreateAsset(newWater, waterPath);
            comp.CustomWaterfallMaterial = newWater;
        }

        if (srcRainbow != null)
        {
            string rainbowPath = AssetDatabase.GenerateUniqueAssetPath($"{MaterialsFolder}/{baseName}_Rainbow.mat");
            Material newRainbow = new Material(srcRainbow);
            newRainbow.SetFloat("_RainbowIntensity", comp.RainbowIntensity);
            newRainbow.SetFloat("_RingThickness", comp.RingThickness);
            newRainbow.SetFloat("_SecondaryBowOpacity", comp.SecondaryBowOpacity);
            AssetDatabase.CreateAsset(newRainbow, rainbowPath);
            comp.CustomRainbowMaterial = newRainbow;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(comp);
    }

    private static void EnsureDirectories()
    {
        string[] dirs = { BaseFolder, ShadersFolder, TexturesFolder, MaterialsFolder, MeshesFolder, PrefabsFolder };
        foreach (string d in dirs)
        {
            if (!Directory.Exists(d))
            {
                Directory.CreateDirectory(d);
            }
        }
    }

    public static void GenerateAllAssets(bool ping)
    {
        EnsureDirectories();

        // 1. プリセット別ネイティブ3Dメッシュを保存
        SavePresetMeshes(SkyAquaCascadeController.CascadePreset.StandardSpoutAndHalo,   8.0f,  80.0f,  4.5f, 22.0f);
        SavePresetMeshes(SkyAquaCascadeController.CascadePreset.WideTerraceMegafall,   20.0f, 120.0f,  7.0f, 38.0f);
        SavePresetMeshes(SkyAquaCascadeController.CascadePreset.PrismaticSolarHaloOnly, 8.0f,  60.0f,  4.0f, 32.0f);
        SavePresetMeshes(SkyAquaCascadeController.CascadePreset.SlenderHighMistfall,    3.5f, 150.0f,  3.0f, 14.0f);

        // 2. プレハブ4種を生成・更新
        CreateOrUpdatePrefab("PF_SkyAquaCascade_01_StandardSpoutHalo",  SkyAquaCascadeController.CascadePreset.StandardSpoutAndHalo);
        CreateOrUpdatePrefab("PF_SkyAquaCascade_02_WideTerraceMegafall", SkyAquaCascadeController.CascadePreset.WideTerraceMegafall);
        CreateOrUpdatePrefab("PF_SkyAquaCascade_03_PrismaticSolarHaloOnly", SkyAquaCascadeController.CascadePreset.PrismaticSolarHaloOnly);
        CreateOrUpdatePrefab("PF_SkyAquaCascade_04_SlenderHighMistfall", SkyAquaCascadeController.CascadePreset.SlenderHighMistfall);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(PrefabsFolder);
            if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
        }
    }

    private static void SavePresetMeshes(
        SkyAquaCascadeController.CascadePreset preset,
        float width,
        float drop,
        float archThrow,
        float rainbowRadius)
    {
        string suffix = SkyAquaCascadeController.GetPresetAssetSuffix(preset);
        if (string.IsNullOrEmpty(suffix)) return;

        SaveOrUpdateMeshAsset(SkyAquaCascadeController.BuildSpoutHardwareMesh(width, archThrow), $"{MeshesFolder}/Mesh_Spout_{suffix}.asset");
        SaveOrUpdateMeshAsset(SkyAquaCascadeController.BuildWaterfallCoreMesh(width, drop, archThrow), $"{MeshesFolder}/Mesh_CascadeCore_{suffix}.asset");
        SaveOrUpdateMeshAsset(SkyAquaCascadeController.BuildWaterfallSprayVeilMesh(width, drop, archThrow), $"{MeshesFolder}/Mesh_CascadeVeil_{suffix}.asset");
        SaveOrUpdateMeshAsset(SkyAquaCascadeController.BuildPrismaticRainbowMesh(rainbowRadius), $"{MeshesFolder}/Mesh_RainbowHalo_{suffix}.asset");
    }

    private static void SaveOrUpdateMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            mesh.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            DestroyImmediate(mesh);
        }
    }

    private static void CreateOrUpdatePrefab(string prefabName, SkyAquaCascadeController.CascadePreset preset)
    {
        string path = $"{PrefabsFolder}/{prefabName}.prefab";
        GameObject tempRoot = new GameObject(prefabName);
        try
        {
            SkyAquaCascadeController ctrl = tempRoot.AddComponent<SkyAquaCascadeController>();
            ctrl.ApplyPreset(preset);
            PrefabUtility.SaveAsPrefabAsset(tempRoot, path);
        }
        finally
        {
            DestroyImmediate(tempRoot);
        }
    }

    public static void SpawnSkyAquaCascadeShowcase(Vector3 basePos)
    {
        const string rootName = "--- SkyAquaCascade Showcase ---";
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Spawn SkyAquaCascade Showcase");
        root.transform.position = basePos;
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        SpawnShowcaseItem(root.transform, "Showcase_01_StandardSpoutHalo",      SkyAquaCascadeController.CascadePreset.StandardSpoutAndHalo,   new Vector3(-45f, 0f, 0f));
        SpawnShowcaseItem(root.transform, "Showcase_02_WideTerraceMegafall",    SkyAquaCascadeController.CascadePreset.WideTerraceMegafall,    new Vector3(-10f, 0f, 0f));
        SpawnShowcaseItem(root.transform, "Showcase_03_PrismaticSolarHaloOnly", SkyAquaCascadeController.CascadePreset.PrismaticSolarHaloOnly, new Vector3( 30f, -20f, 0f));
        SpawnShowcaseItem(root.transform, "Showcase_04_SlenderHighMistfall",    SkyAquaCascadeController.CascadePreset.SlenderHighMistfall,    new Vector3( 65f, 0f, 0f));

        Selection.activeGameObject = root;
    }

    private static void SpawnShowcaseItem(
        Transform parent,
        string name,
        SkyAquaCascadeController.CascadePreset preset,
        Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        SkyAquaCascadeController ctrl = go.AddComponent<SkyAquaCascadeController>();
        ctrl.ApplyPreset(preset);
    }
}
