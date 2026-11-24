using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 選んでいるCourseSplineに、トンネル表示（CourseTunnelMesh）を作るメニュー。マテリアルが無ければ作る。
public static class CourseTunnelMenu
{
    private const string MaterialFolder = "Assets/Deverop/AT_Kawamata/CourseTunnel/Materials";
    private const string MaterialPath = MaterialFolder + "/M_CourseTunnel.mat";
    private const string ShaderName = "Misoten/Course Tunnel";

    [MenuItem("Tools/Course/トンネル表示を作成")]
    private static void CreateTunnel()
    {
        GameObject selected = Selection.activeGameObject;
        CourseNetwork network = selected != null ? selected.GetComponent<CourseNetwork>() : null;
        CourseSpline spline = selected != null ? selected.GetComponent<CourseSpline>() : null;
        if (network == null && spline == null)
        {
            EditorUtility.DisplayDialog("トンネル表示を作成", "HierarchyでCourseNetwork（本線と分岐をまとめて作る）か、CourseSplineが付いたGameObjectを選んでから実行してください。", "OK");
            return;
        }

        Material material = GetOrCreateMaterial();
        if (material == null)
        {
            return;
        }

        // CourseSplineは子を制御点として扱うので、子ではなく同じ階層に置く。
        GameObject tunnel = new GameObject($"CourseTunnel_{selected.name}");
        Undo.RegisterCreatedObjectUndo(tunnel, "トンネル表示を作成");
        tunnel.transform.SetParent(selected.transform.parent, false);
        tunnel.transform.SetSiblingIndex(selected.transform.GetSiblingIndex() + 1);

        MeshRenderer meshRenderer = tunnel.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        CourseTunnelMesh tunnelMesh = tunnel.AddComponent<CourseTunnelMesh>();
        if (network != null)
        {
            tunnelMesh.CourseNetwork = network;
        }
        else
        {
            tunnelMesh.CourseSpline = spline;
        }

        tunnel.AddComponent<CourseTunnelHitGlow>();

        Selection.activeGameObject = tunnel;
    }

    [MenuItem("Tools/Course/トンネル表示を作成", true)]
    private static bool ValidateCreateTunnel()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null && (selected.GetComponent<CourseNetwork>() != null || selected.GetComponent<CourseSpline>() != null);
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"CourseTunnelMenu: シェーダー {ShaderName} が見つかりません");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Deverop/AT_Kawamata/CourseTunnel", "Materials");
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
