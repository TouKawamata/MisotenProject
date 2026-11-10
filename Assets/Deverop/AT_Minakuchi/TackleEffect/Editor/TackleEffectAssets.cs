using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TackleEffectAssets
{
    private const string Root = "Assets/Deverop/AT_Minakuchi/TackleEffect/";

    [MenuItem("Tools/Misoten/Create Tackle Effect Preview")]
    public static void CreatePreview()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Generate();
    }

    // Also callable in batch mode to validate and generate the standalone assets.
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var effect = new GameObject("Tackle Effect");
        var component = effect.AddComponent<TackleEffect>();
        PrefabUtility.SaveAsPrefabAsset(effect, Root + "TackleEffect.prefab");
        var settings = new SerializedObject(component);
        settings.FindProperty("_demoLoop").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var cameraObject = new GameObject("Preview Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.transform.position = new Vector3(4, 1.8f, 5);
        camera.transform.LookAt(new Vector3(0, 0, 0.4f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.012f, 0.021f, 0.038f);
        camera.fieldOfView = 38;
        camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var volume = new GameObject("Bloom").AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "TacklePreviewVolume.asset");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Root + "TacklePreviewVolume.asset");
        }
        if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>();
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.7f);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        EditorSceneManager.SaveScene(scene, Root + "TackleEffectPreview.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Tackle effect prefab and preview scene generated successfully.");
    }
}

