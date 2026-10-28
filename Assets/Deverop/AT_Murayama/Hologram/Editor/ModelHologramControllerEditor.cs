using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
[CustomEditor(typeof(ModelHologramController))]
public class ModelHologramControllerEditor : Editor
{
    private const string BaseFolder      = "Assets/Deverop/AT_Murayama/Hologram";
    private const string MaterialsFolder = BaseFolder + "/Materials";

    private static bool _showSampleTools = false;

    // 元のサンプル用マテリアル名（ユーザーによる誤上書きを防止）
    private static readonly string[] ProtectedSampleNames = new string[]
    {
        "M_Model_01_ClassicScanline",
        "M_Model_02_HexShield",
        "M_Model_03_WireframeGrid",
        "M_Model_04_CyberGlitch",
        "M_Model_04_Hologram",
        "M_Model_05_HoloLEDMatrix",
        "M_Model_06_HoloPatternBanner",
        "M_Model_07_HoloProjectorBeam",
        "M_Model_08_GhostRim",
        "M_Model_09_Dissolve",
        "M_Model_10_TriplanarDots",
        "M_Model_11_GradientEnergy",
        "M_Flag_00_StandardCloth",
        "M_Flag_01_HoloScanline",
        "M_Flag_02_HoloLEDMatrix",
        "M_Flag_03_HoloCyberGlitch"
    };

    static ModelHologramControllerEditor()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.IsValidFolder(MaterialsFolder) &&
                AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Model_02_HexShield.mat") == null)
            {
                SaveAllModelMaterials(ping: false);
            }
        };
    }

    public static void MenuGenerateModelMaterials()
    {
        SaveAllModelMaterials(ping: true);
    }

    public override void OnInspectorGUI()
    {
        ModelHologramController comp = (ModelHologramController)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【基本】3Dモデル用ホログラムスタイル 4種", EditorStyles.boldLabel);

        ModelHologramController.ModelHologramStyle curr = comp.GetHologramStyle();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "① 王道スキャンライン", ModelHologramController.ModelHologramStyle.ClassicScanline, curr, new Color(0.35f, 0.9f, 1f));
            DrawStyleButton(comp, "② 六角形ヘックスシールド", ModelHologramController.ModelHologramStyle.HexShield, curr, new Color(0.3f, 1f, 0.7f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "③ 3D等高線CADワイヤー", ModelHologramController.ModelHologramStyle.WireframeGrid, curr, new Color(1f, 0.85f, 0.35f));
            DrawStyleButton(comp, "④ サイバーRGBグリッチ", ModelHologramController.ModelHologramStyle.CyberGlitch, curr, new Color(1f, 0.55f, 0.85f));
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("【旗スタイル移植】3Dモデル用ホログラム 3種", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "⑤ 旗風：LEDドットマトリクス", ModelHologramController.ModelHologramStyle.FlagStyle_LEDMatrix, curr, new Color(0.4f, 1f, 0.65f));
            DrawStyleButton(comp, "⑥ 旗風：流れるサイバー矢印", ModelHologramController.ModelHologramStyle.FlagStyle_PatternBanner, curr, new Color(0.2f, 0.85f, 1f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "⑦ 旗風：台座照射ビーム＆色収差", ModelHologramController.ModelHologramStyle.FlagStyle_ProjectorBeam, curr, new Color(1f, 0.65f, 0.9f));
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
                EditorGUILayout.HelpBox("インスペクターで調整した見た目を、元のサンプル（M_Model_01〜07）を壊さず新規マテリアル（.mat）として別名保存し、このオブジェクトに即座に適用します。", MessageType.None);
            }

            string defaultPlaceholder = $"M_Holo_{comp.gameObject.name}";
            comp.CustomSaveName = EditorGUILayout.TextField("保存ファイル名", comp.CustomSaveName);
            if (string.IsNullOrWhiteSpace(comp.CustomSaveName))
            {
                EditorGUILayout.LabelField($"※空欄の場合は「{defaultPlaceholder}.mat」で保存されます", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.35f, 0.95f, 0.6f);
                if (GUILayout.Button("💾 別名で新規保存＆適用", GUILayout.Height(30f)))
                {
                    SaveAsCustomMaterial(comp, false);
                }

                GUI.backgroundColor = new Color(0.85f, 0.95f, 1f);
                if (GUILayout.Button("📂 保存先を選んで別名保存...", GUILayout.Height(30f)))
                {
                    SaveAsCustomMaterial(comp, true);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6f);

        // --- サンプル一括操作・復元（折りたたみ＆安全ガード付き） ---
        _showSampleTools = EditorGUILayout.Foldout(_showSampleTools, "【サンプル用】一括比較サンプルの生成 / サンプルマテリアル復元", true);
        if (_showSampleTools)
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.backgroundColor = new Color(0.75f, 0.95f, 1f);
                if (GUILayout.Button("✨ シーン内に7種のモデルホログラム比較サンプルを一括生成", GUILayout.Height(26f)))
                {
                    SpawnModelComparisonShowcase(comp.transform);
                }

                GUI.backgroundColor = new Color(1f, 0.85f, 0.85f);
                if (GUILayout.Button("⚠️ サンプルマテリアル7種（M_Model_01〜07）を初期値で復元・リセット", GUILayout.Height(24f)))
                {
                    if (EditorUtility.DisplayDialog(
                        "サンプルマテリアルの復元確認",
                        "元のサンプル用マテリアル7種（M_Model_01〜07.mat）を初期値で復元・上書きします。\nよろしいですか？",
                        "初期値で復元する",
                        "キャンセル"))
                    {
                        SaveAllModelMaterials(ping: true);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6f);
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);

        if (GUILayout.Button("マテリアル再適用", GUILayout.Height(26f)))
        {
            comp.UpdateMaterial();
            EditorUtility.SetDirty(comp);
        }
    }

    private static void DrawStyleButton(
        ModelHologramController comp,
        string label,
        ModelHologramController.ModelHologramStyle targetStyle,
        ModelHologramController.ModelHologramStyle currentStyle,
        Color activeColor)
    {
        GUI.backgroundColor = (currentStyle == targetStyle) ? activeColor : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Undo.RecordObject(comp, "Switch Model Hologram Style");
            comp.SetHologramStyle(targetStyle);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static bool IsProtectedSampleName(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return false;
        foreach (var p in ProtectedSampleNames)
        {
            if (string.Equals(p, fileNameWithoutExt, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void SaveAsCustomMaterial(ModelHologramController comp, bool useSavePanel)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        string savePath = "";

        if (useSavePanel)
        {
            string defaultName = string.IsNullOrWhiteSpace(comp.CustomSaveName)
                ? $"M_Holo_{comp.gameObject.name}.mat"
                : comp.CustomSaveName;
            if (!defaultName.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) defaultName += ".mat";

            savePath = EditorUtility.SaveFilePanelInProject(
                "ホログラムマテリアルを別名保存",
                defaultName,
                "mat",
                "マテリアルの保存先とファイル名を指定してください",
                MaterialsFolder);

            if (string.IsNullOrEmpty(savePath)) return;
        }
        else
        {
            string fileName = string.IsNullOrWhiteSpace(comp.CustomSaveName)
                ? $"M_Holo_{comp.gameObject.name}"
                : comp.CustomSaveName.Trim();

            if (fileName.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            savePath = $"{MaterialsFolder}/{fileName}.mat";
        }

        string fileWithoutExt = Path.GetFileNameWithoutExtension(savePath);

        // 元のサンプルマテリアルを上書きしないよう保護
        if (IsProtectedSampleName(fileWithoutExt))
        {
            EditorUtility.DisplayDialog(
                "別名保存エラー（保護されたファイル名）",
                $"'{fileWithoutExt}' はシステム予約のサンプルマテリアルのため、上書き保存できません。\n元のサンプルを残すため、別の名前（例: M_Holo_{comp.gameObject.name}）を指定してください。",
                "OK");
            return;
        }

        // 既に同名の別名マテリアルが存在する場合の確認（SavePanelを使っていない場合）
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
            EditorUtility.DisplayDialog("エラー", "マテリアルの生成に失敗しました。", "OK");
            return;
        }

        AssetDatabase.CreateAsset(newMat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material loadedMat = AssetDatabase.LoadAssetAtPath<Material>(savePath);
        if (loadedMat != null)
        {
            Undo.RecordObject(comp, "Apply Saved Hologram Material");
            comp.CustomMaterial = loadedMat;
            comp.CustomSaveName = fileWithoutExt;
            EditorUtility.SetDirty(comp);

            EditorGUIUtility.PingObject(loadedMat);
            Debug.Log($"<color=#55ffaa>[ModelHologram]</color> マテリアルを別名保存してモデルに適用しました: <b>{savePath}</b>");
        }
    }

    public static void SaveAllModelMaterials(bool ping)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        // 1. 王道スキャンライン
        SaveMaterial(
            "M_Model_01_ClassicScanline.mat",
            ModelHologramController.ModelHologramStyle.ClassicScanline,
            new Color(0.0f, 1.8f, 2.8f, 0.45f),
            new Color(0.4f, 3.8f, 4.5f, 0.95f),
            new Color(0.2f, 4.0f, 3.5f, 1.0f));

        // 2. 六角形ヘックスシールド
        SaveMaterial(
            "M_Model_02_HexShield.mat",
            ModelHologramController.ModelHologramStyle.HexShield,
            new Color(0.0f, 1.4f, 2.5f, 0.35f),
            new Color(0.5f, 3.5f, 4.5f, 0.95f),
            new Color(0.2f, 4.0f, 3.8f, 1.0f));

        // 3. 3D等高線CADワイヤーフレーム
        SaveMaterial(
            "M_Model_03_WireframeGrid.mat",
            ModelHologramController.ModelHologramStyle.WireframeGrid,
            new Color(0.0f, 1.2f, 2.2f, 0.25f),
            new Color(0.4f, 2.8f, 4.0f, 0.9f),
            new Color(0.2f, 3.5f, 4.5f, 0.95f));

        // 4. サイバーRGBグリッチ
        SaveMaterial(
            "M_Model_04_CyberGlitch.mat",
            ModelHologramController.ModelHologramStyle.CyberGlitch,
            new Color(0.1f, 2.5f, 3.8f, 0.6f),
            new Color(0.5f, 3.5f, 4.5f, 0.95f),
            new Color(3.5f, 0.4f, 2.0f, 1.0f));

        // 5. 旗風：サイバーLEDドットマトリクス
        SaveMaterial(
            "M_Model_05_HoloLEDMatrix.mat",
            ModelHologramController.ModelHologramStyle.FlagStyle_LEDMatrix,
            new Color(0.0f, 3.2f, 1.8f, 0.95f),
            new Color(0.3f, 4.0f, 3.5f, 0.95f),
            new Color(0.0f, 0.45f, 0.8f, 0.25f));

        // 6. 旗風：流れるサイバーパターン＆スキャンライン
        SaveMaterial(
            "M_Model_06_HoloPatternBanner.mat",
            ModelHologramController.ModelHologramStyle.FlagStyle_PatternBanner,
            new Color(0.0f, 2.2f, 3.2f, 0.75f),
            new Color(0.5f, 3.8f, 4.5f, 0.95f),
            new Color(0.6f, 4.0f, 4.2f, 1.0f));

        // 7. 旗風：台座プロジェクター照射＆RGB色収差
        SaveMaterial(
            "M_Model_07_HoloProjectorBeam.mat",
            ModelHologramController.ModelHologramStyle.FlagStyle_ProjectorBeam,
            new Color(0.1f, 2.4f, 3.5f, 0.75f),
            new Color(0.5f, 3.5f, 4.5f, 0.95f),
            new Color(1.5f, 4.0f, 4.5f, 1.0f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            Object targetObj = AssetDatabase.LoadAssetAtPath<Object>(MaterialsFolder + "/M_Model_05_HoloLEDMatrix.mat");
            if (targetObj != null) EditorGUIUtility.PingObject(targetObj);
            Debug.Log("[ModelHologram] 3Dモデル用ホログラムマテリアル7種を保存しました！");
        }
    }

    private static void SaveMaterial(
        string fileName,
        ModelHologramController.ModelHologramStyle style,
        Color mainColor,
        Color rimColor,
        Color accentColor)
    {
        string path = $"{MaterialsFolder}/{fileName}";
        Shader shader = Shader.Find(ModelHologramController.GetShaderPath(style));
        if (shader == null) return;

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

        if (mat.HasProperty("_MainColor"))        mat.SetColor("_MainColor", mainColor);
        if (mat.HasProperty("_RimColor"))         mat.SetColor("_RimColor", rimColor);
        if (mat.HasProperty("_HexWireColor"))     mat.SetColor("_HexWireColor", accentColor);
        if (mat.HasProperty("_GridColor"))        mat.SetColor("_GridColor", accentColor);
        if (mat.HasProperty("_ScanPlaneColor"))   mat.SetColor("_ScanPlaneColor", accentColor);
        if (mat.HasProperty("_GlitchGlowColor"))  mat.SetColor("_GlitchGlowColor", accentColor);
        EditorUtility.SetDirty(mat);
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

    private static void SpawnModelComparisonShowcase(Transform origin)
    {
        GameObject root = new GameObject("ModelHologram_Comparison_Showcase");
        Undo.RegisterCreatedObjectUndo(root, "Spawn Model Hologram Showcase");
        root.transform.position = origin.position;
        root.transform.rotation = origin.rotation;

        // 1. 王道スキャンライン（Sphere）
        CreateModelSample(root.transform, "1_Model_ClassicScanline", PrimitiveType.Sphere, new Vector3(-6f, 2.0f, 0f),
            ModelHologramController.ModelHologramStyle.ClassicScanline,
            new Color(0.0f, 1.8f, 2.8f, 0.45f), new Color(0.4f, 3.8f, 4.5f, 0.95f), new Color(0.2f, 4.0f, 3.5f, 1.0f));

        // 2. 六角形ヘックスシールド（Capsule）
        CreateModelSample(root.transform, "2_Model_HexShield", PrimitiveType.Capsule, new Vector3(-2f, 2.0f, 0f),
            ModelHologramController.ModelHologramStyle.HexShield,
            new Color(0.0f, 1.4f, 2.5f, 0.35f), new Color(0.5f, 3.5f, 4.5f, 0.95f), new Color(0.2f, 4.0f, 3.8f, 1.0f));

        // 3. 3D等高線CADワイヤーフレーム（Cylinder）
        CreateModelSample(root.transform, "3_Model_WireframeGrid", PrimitiveType.Cylinder, new Vector3(2f, 2.0f, 0f),
            ModelHologramController.ModelHologramStyle.WireframeGrid,
            new Color(0.0f, 1.2f, 2.2f, 0.25f), new Color(0.4f, 2.8f, 4.0f, 0.9f), new Color(0.2f, 3.5f, 4.5f, 0.95f));

        // 4. サイバーRGBグリッチ（Cube）
        CreateModelSample(root.transform, "4_Model_CyberGlitch", PrimitiveType.Cube, new Vector3(6f, 2.0f, 0f),
            ModelHologramController.ModelHologramStyle.CyberGlitch,
            new Color(0.1f, 2.5f, 3.8f, 0.6f), new Color(0.5f, 3.5f, 4.5f, 0.95f), new Color(3.5f, 0.4f, 2.0f, 1.0f));

        // 5. 旗風：サイバーLEDドットマトリクス（Sphere）
        CreateModelSample(root.transform, "5_Model_FlagLEDMatrix", PrimitiveType.Sphere, new Vector3(-4f, -2.0f, 0f),
            ModelHologramController.ModelHologramStyle.FlagStyle_LEDMatrix,
            new Color(0.0f, 3.2f, 1.8f, 0.95f), new Color(0.3f, 4.0f, 3.5f, 0.95f), new Color(0.0f, 0.45f, 0.8f, 0.25f));

        // 6. 旗風：流れるサイバーパターン＆スキャンライン（Cylinder）
        CreateModelSample(root.transform, "6_Model_FlagPatternBanner", PrimitiveType.Cylinder, new Vector3(0f, -2.0f, 0f),
            ModelHologramController.ModelHologramStyle.FlagStyle_PatternBanner,
            new Color(0.0f, 2.2f, 3.2f, 0.75f), new Color(0.5f, 3.8f, 4.5f, 0.95f), new Color(0.6f, 4.0f, 4.2f, 1.0f));

        // 7. 旗風：台座プロジェクター照射＆RGB色収差（Capsule）
        CreateModelSample(root.transform, "7_Model_FlagProjectorBeam", PrimitiveType.Capsule, new Vector3(4f, -2.0f, 0f),
            ModelHologramController.ModelHologramStyle.FlagStyle_ProjectorBeam,
            new Color(0.1f, 2.4f, 3.5f, 0.75f), new Color(0.5f, 3.5f, 4.5f, 0.95f), new Color(1.5f, 4.0f, 4.5f, 1.0f));

        Selection.activeGameObject = root;
    }

    private static void CreateModelSample(
        Transform parent,
        string name,
        PrimitiveType primType,
        Vector3 localPos,
        ModelHologramController.ModelHologramStyle style,
        Color mainColor,
        Color rimColor,
        Color accentColor)
    {
        GameObject obj = GameObject.CreatePrimitive(primType);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = Vector3.one * 2.2f;

        Collider col = obj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        ModelHologramController ctrl = obj.AddComponent<ModelHologramController>();
        ctrl.Configure(style, mainColor, rimColor, accentColor);
    }
}

