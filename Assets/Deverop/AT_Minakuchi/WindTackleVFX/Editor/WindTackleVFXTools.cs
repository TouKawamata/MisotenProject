using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>Editor-only helpers. The effect itself uses native VFX Graph nodes.</summary>
public static class WindTackleVFXTools
{
    private const string Root = "Assets/Deverop/AT_Minakuchi/WindTackleVFX/";

    [MenuItem("Tools/Misoten/Wind Tackle VFX/Open Preview")]
    public static void OpenPreview()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(Root + "WindTackle_VFX_Preview.unity");
    }

    [MenuItem("Tools/Misoten/Wind Tackle VFX/Validate Assets")]
    public static void ValidateAssets()
    {
        var errors = new List<string>();
        Application.LogCallback capture = (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            AssetDatabase.ImportAsset(Root + "WindPattern.png", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Root + "WindTackle_Hemisphere.asset", ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in new[] { "WindTackle.vfx", "WindTackle_LoopPreview.vfx" })
            {
                string path = Root + name;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (asset == null) throw new InvalidOperationException("Could not import " + path);
                var properties = new List<VFXExposedProperty>();
                asset.GetExposedProperties(properties);
                foreach (string property in new[] { "Duration", "Shape", "WindColor", "ScrollSpeed" })
                    if (!properties.Any(p => p.name == property))
                        throw new InvalidOperationException(name + ": missing exposed property " + property);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "WindTackle_Hemisphere.asset");
            if (mesh == null || mesh.vertexCount != 4257 || mesh.triangles.Length != 24576)
                throw new InvalidOperationException("Hemisphere mesh geometry failed validation");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "WindTackle_VFX.prefab");
            if (prefab == null || prefab.GetComponent<VisualEffect>()?.visualEffectAsset == null)
                throw new InvalidOperationException("Native VisualEffect prefab reference is missing");
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("Wind Tackle VFX: native graphs, exposed properties, mesh and prefab validated.");
        }
        finally { Application.logMessageReceived -= capture; }
    }

    public static void ValidateBatch()
    {
        try
        {
            ValidateAssets();
            File.WriteAllText("validation-result.txt", "PASS: native VFX Graph import, properties, mesh and prefab.");
        }
        catch (Exception error)
        {
            File.WriteAllText("validation-result.txt", error.ToString());
            throw;
        }
    }
}
