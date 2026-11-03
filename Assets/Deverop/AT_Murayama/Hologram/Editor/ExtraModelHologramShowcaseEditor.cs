using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ExtraModelHologramShowcase 用のカスタムインスペクター。
/// Tools メニューは一切使用せず、インスペクター上のボタンからスタイル切り替え・比較サンプル生成・マテリアル保存を行えます。
/// </summary>
[CustomEditor(typeof(ExtraModelHologramShowcase))]
public class ExtraModelHologramShowcaseEditor : Editor
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

    public override void OnInspectorGUI()
    {
        ExtraModelHologramShowcase comp = (ExtraModelHologramShowcase)target;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("① 汎用モデルホログラム 4種クイック切り替え", EditorStyles.boldLabel);

        ExtraModelHologramShowcase.ExtraHologramStyle curr = comp.GetStyle();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "⑧ GhostRim (輪郭＋呼吸)", ExtraModelHologramShowcase.ExtraHologramStyle.GhostRim, curr, new Color(0.4f, 0.9f, 1f));
            DrawStyleButton(comp, "⑨ Dissolve (出現・消滅)", ExtraModelHologramShowcase.ExtraHologramStyle.Dissolve, curr, new Color(1f, 0.85f, 0.35f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStyleButton(comp, "⑩ TriplanarDots (3軸ドット)", ExtraModelHologramShowcase.ExtraHologramStyle.TriplanarDots, curr, new Color(0.4f, 1f, 0.7f));
            DrawStyleButton(comp, "⑪ GradientEnergy (高さグラデ)", ExtraModelHologramShowcase.ExtraHologramStyle.GradientEnergy, curr, new Color(0.95f, 0.6f, 1f));
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
                EditorGUILayout.HelpBox("インスペクターで調整した見た目を、元のサンプル（M_Model_08〜11）を壊さず新規マテリアル（.mat）として別名保存し、このオブジェクトに即座に適用します。", MessageType.None);
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
                if (GUILayout.Button("✨ 比較サンプル4種を子オブジェクトとして一括生成", GUILayout.Height(26f)))
                {
                    SpawnShowcaseChildren(comp);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = new Color(1f, 0.75f, 0.75f);
                    if (GUILayout.Button("🗑️ 生成した子サンプルを削除", GUILayout.Height(24f)))
                    {
                        ClearShowcaseChildren(comp);
                    }

                    GUI.backgroundColor = new Color(1f, 0.85f, 0.85f);
                    if (GUILayout.Button("⚠️ サンプルマテリアル4種（08〜11）復元", GUILayout.Height(24f)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "サンプルマテリアルの復元確認",
                            "元のサンプル用マテリアル4種（M_Model_08〜11.mat）を初期値で復元・上書きします。\nよろしいですか？",
                            "初期値で復元する",
                            "キャンセル"))
                        {
                            SaveExtraMaterials(ping: true);
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6f);
        DrawDefaultInspector();
    }

    private static void DrawStyleButton(
        ExtraModelHologramShowcase comp,
        string label,
        ExtraModelHologramShowcase.ExtraHologramStyle targetStyle,
        ExtraModelHologramShowcase.ExtraHologramStyle currentStyle,
        Color activeColor)
    {
        GUI.backgroundColor = (currentStyle == targetStyle) ? activeColor : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Undo.RecordObject(comp, "Switch Extra Hologram Style");
            comp.SetStyle(targetStyle);
            EditorUtility.SetDirty(comp);
        }
        GUI.backgroundColor = Color.white;
    }

    private static void SpawnShowcaseChildren(ExtraModelHologramShowcase comp)
    {
        // もしマテリアルがまだ無ければ自動作成してからサンプルに割り当てる
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Model_08_GhostRim.mat") == null)
        {
            SaveExtraMaterials(ping: false);
        }

        ClearShowcaseChildren(comp);

        string[] matFiles =
        {
            "M_Model_08_GhostRim.mat",
            "M_Model_09_Dissolve.mat",
            "M_Model_10_TriplanarDots.mat",
            "M_Model_11_GradientEnergy.mat"
        };
        PrimitiveType[] prims =
        {
            PrimitiveType.Sphere,
            PrimitiveType.Capsule,
            PrimitiveType.Cube,
            PrimitiveType.Cylinder
        };

        float spacing = Mathf.Max(0.5f, comp.Spacing);
        float scale = Mathf.Max(0.1f, comp.SampleScale);

        for (int i = 0; i < matFiles.Length; i++)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{matFiles[i]}");
            if (mat == null) continue;

            GameObject obj = GameObject.CreatePrimitive(prims[i]);
            Undo.RegisterCreatedObjectUndo(obj, "Spawn Extra Hologram Sample");
            obj.name = Path.GetFileNameWithoutExtension(matFiles[i]);
            obj.transform.SetParent(comp.transform, false);
            obj.transform.localPosition = new Vector3((i - 1.5f) * spacing, 0f, 0f);
            obj.transform.localScale = Vector3.one * scale;

            Collider col = obj.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
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

    private static void SaveAsCustomMaterial(ExtraModelHologramShowcase comp, bool useSavePanel)
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
            Debug.Log($"<color=#55ffaa>[ExtraModelHologram]</color> マテリアルを別名保存してモデルに適用しました: <b>{savePath}</b>");
        }
    }

    private static void ClearShowcaseChildren(ExtraModelHologramShowcase comp)
    {
        for (int i = comp.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(comp.transform.GetChild(i).gameObject);
        }
    }

    public static void SaveExtraMaterials(bool ping)
    {
        EnsureFolder(BaseFolder);
        EnsureFolder(MaterialsFolder);

        CreateOrUpdateMaterial("M_Model_08_GhostRim.mat",       "Custom/Hologram/ModelHologram_GhostRim");
        Material dissolve = CreateOrUpdateMaterial("M_Model_09_Dissolve.mat", "Custom/Hologram/ModelHologram_Dissolve");
        CreateOrUpdateMaterial("M_Model_10_TriplanarDots.mat",  "Custom/Hologram/ModelHologram_TriplanarDots");
        CreateOrUpdateMaterial("M_Model_11_GradientEnergy.mat", "Custom/Hologram/ModelHologram_GradientEnergy");

        if (dissolve != null && dissolve.HasProperty("_AutoLoopSpeed"))
        {
            if (dissolve.GetFloat("_AutoLoopSpeed") <= 0.001f) dissolve.SetFloat("_AutoLoopSpeed", 0.35f);
            dissolve.SetFloat("_DirectionMode", 1f);
            EditorUtility.SetDirty(dissolve);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ping)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(MaterialsFolder + "/M_Model_08_GhostRim.mat");
            if (obj != null) EditorGUIUtility.PingObject(obj);
            Debug.Log("[ExtraModelHologramShowcase] 汎用マテリアル4種（M_Model_08〜11）を保存しました！");
        }
    }

    private static Material CreateOrUpdateMaterial(string fileName, string shaderName)
    {
        string path = $"{MaterialsFolder}/{fileName}";
        Shader shader = Shader.Find(shaderName);
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
        EditorUtility.SetDirty(mat);
        return mat;
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
}

