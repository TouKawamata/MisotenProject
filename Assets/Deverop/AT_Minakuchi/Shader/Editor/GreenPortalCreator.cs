using UnityEditor;
using UnityEngine;

internal static class GreenPortalCreator
{
    private const string MaterialPath = "Assets/Deverop/AT_Minakuchi/Shader/GreenPortal.mat";

    [MenuItem("GameObject/Misoten/Green Portal", false, 10)]
    private static void Create(MenuCommand command)
    {
        var shader = Shader.Find("Misoten/Effects/Green Portal");
        if (shader == null)
        {
            Debug.LogError("Green Portal shader could not be found. Check shader import errors.");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "GreenPortal" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portal.name = "Green Portal";
        GameObjectUtility.SetParentAndAlign(portal, command.context as GameObject);
        portal.transform.localScale = new Vector3(3, 4.5f, 1);
        Object.DestroyImmediate(portal.GetComponent<Collider>());
        portal.GetComponent<MeshRenderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(portal, "Create Green Portal");
        Selection.activeGameObject = portal;
    }
}
