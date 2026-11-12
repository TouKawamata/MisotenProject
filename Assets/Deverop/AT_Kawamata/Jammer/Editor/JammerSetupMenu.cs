using UnityEditor;
using UnityEngine;

// Tools/Jammer/初期セットアップ：妨害弾に必要なアセット・プレハブを作り、Player.prefabに組み込む。
// 何度押しても、すでにあるものは作り直さず、足りない分だけ作る（設定を変えたアセットも上書きしない）。
// ・Jammer/Profiles：JammerProfileと、弾の種類6つ（減速／リングを減速リングに／視界反転（左右）／視界反転（上下）／左右移動が遅い／ブースト不可）
// ・Jammer/Material/Jammer.mat、Jammer/Prefab/JammerProjectile.prefab（球。Colliderは無し）
// ・Player.prefab：JammerLauncher（Profileと種類を登録）とViewFlipPresentationを付け、PlayerManagerのJammer Launcherに入れる
public static class JammerSetupMenu
{
    private const string RootFolder = "Assets/Deverop/AT_Kawamata/Jammer";
    private const string ProfilesFolder = RootFolder + "/Profiles";
    private const string PrefabFolder = RootFolder + "/Prefab";
    private const string MaterialFolder = RootFolder + "/Material";
    private const string PlayerPrefabPath = "Assets/Deverop/AT_Kawamata/Prefab/Player.prefab";

    private static readonly Color ProjectileColor = new Color(1f, 0.25f, 0.85f);

    [MenuItem("Tools/Jammer/初期セットアップ")]
    public static void Setup()
    {
        EnsureFolder(RootFolder, ProfilesFolder);
        EnsureFolder(RootFolder, PrefabFolder);
        EnsureFolder(RootFolder, MaterialFolder);

        JammerTypeProfile[] types =
        {
            CreateType<SlowJammerType>("SlowJammer", "減速", true, null),
            CreateType<RingSwapJammerType>("RingSwapJammer", "リングを減速リングに", false, null),
            CreateType<ViewFlipJammerType>("ViewFlipHorizontalJammer", "視界反転（左右）", true, EViewFlip.Horizontal),
            CreateType<ViewFlipJammerType>("ViewFlipVerticalJammer", "視界反転（上下）", true, EViewFlip.Vertical),
            CreateType<LateralSlowJammerType>("LateralSlowJammer", "左右移動が遅い", true, null),
            CreateType<BoostLockJammerType>("BoostLockJammer", "ブースト不可", true, null)
        };

        JammerProjectile projectile = CreateProjectilePrefab(CreateMaterial());
        JammerProfile profile = CreateProfile(projectile);
        SetupPlayerPrefab(profile, types);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("JammerSetupMenu: 妨害弾の初期セットアップが終わりました");
    }

    private static T CreateType<T>(string fileName, string displayName, bool canBeBlockedByShield, EViewFlip? flip) where T : JammerTypeProfile
    {
        string path = $"{ProfilesFolder}/{fileName}.asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            return existing;
        }

        T type = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(type, path);

        SerializedObject serialized = new SerializedObject(type);
        serialized.FindProperty("_displayName").stringValue = displayName;
        serialized.FindProperty("_canBeBlockedByShield").boolValue = canBeBlockedByShield;
        if (flip.HasValue)
        {
            serialized.FindProperty("_flip").intValue = (int)flip.Value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"JammerSetupMenu: {path} を作りました");
        return type;
    }

    private static Material CreateMaterial()
    {
        string path = $"{MaterialFolder}/Jammer.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.SetColor("_BaseColor", ProjectileColor);
        material.SetColor("_Color", ProjectileColor);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", ProjectileColor * 2f);
        AssetDatabase.CreateAsset(material, path);
        Debug.Log($"JammerSetupMenu: {path} を作りました");
        return material;
    }

    private static JammerProjectile CreateProjectilePrefab(Material material)
    {
        string path = $"{PrefabFolder}/JammerProjectile.prefab";
        JammerProjectile existing = AssetDatabase.LoadAssetAtPath<JammerProjectile>(path);
        if (existing != null)
        {
            return existing;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "JammerProjectile";
        sphere.transform.localScale = Vector3.one * 1.5f;

        // 当たりは弾が自分で判定するので、物理のColliderは要らない（レーサーやギミックと衝突させない）。
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
        sphere.AddComponent<JammerProjectile>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sphere, path);
        Object.DestroyImmediate(sphere);
        Debug.Log($"JammerSetupMenu: {path} を作りました");
        return prefab.GetComponent<JammerProjectile>();
    }

    private static JammerProfile CreateProfile(JammerProjectile projectile)
    {
        string path = $"{ProfilesFolder}/JammerProfile.asset";
        JammerProfile profile = AssetDatabase.LoadAssetAtPath<JammerProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<JammerProfile>();
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"JammerSetupMenu: {path} を作りました");
        }

        SerializedObject serialized = new SerializedObject(profile);
        SerializedProperty prefab = serialized.FindProperty("_projectilePrefab");
        if (prefab.objectReferenceValue == null)
        {
            prefab.objectReferenceValue = projectile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return profile;
    }

    // Player.prefabを開いて、足りない部品を付けて保存する。すでに付いていて設定済みの項目は変えない。
    private static void SetupPlayerPrefab(JammerProfile profile, JammerTypeProfile[] types)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerManager playerManager = root.GetComponentInChildren<PlayerManager>(true);
            if (playerManager == null)
            {
                Debug.LogError($"JammerSetupMenu: {PlayerPrefabPath} にPlayerManagerが見つかりません", AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
                return;
            }

            GameObject owner = playerManager.gameObject;
            JammerLauncher launcher = GetOrAdd<JammerLauncher>(owner);
            SerializedObject launcherSerialized = new SerializedObject(launcher);
            SetIfEmpty(launcherSerialized.FindProperty("_profile"), profile);
            SerializedProperty typeList = launcherSerialized.FindProperty("_types");
            if (typeList.arraySize == 0)
            {
                typeList.arraySize = types.Length;
                for (int i = 0; i < types.Length; i++)
                {
                    typeList.GetArrayElementAtIndex(i).objectReferenceValue = types[i];
                }
            }

            launcherSerialized.ApplyModifiedPropertiesWithoutUndo();

            ViewFlipPresentation viewFlip = GetOrAdd<ViewFlipPresentation>(owner);
            SerializedObject viewFlipSerialized = new SerializedObject(viewFlip);
            SetIfEmpty(viewFlipSerialized.FindProperty("_racerManager"), playerManager);
            SetIfEmpty(viewFlipSerialized.FindProperty("_camera"), root.GetComponentInChildren<Camera>(true));
            viewFlipSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject managerSerialized = new SerializedObject(playerManager);
            SetIfEmpty(managerSerialized.FindProperty("_jammerLauncher"), launcher);
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"JammerSetupMenu: {PlayerPrefabPath} に妨害弾を組み込みました");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }

    private static void SetIfEmpty(SerializedProperty property, Object value)
    {
        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(parent))
        {
            int slash = parent.LastIndexOf('/');
            AssetDatabase.CreateFolder(parent.Substring(0, slash), parent.Substring(slash + 1));
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
