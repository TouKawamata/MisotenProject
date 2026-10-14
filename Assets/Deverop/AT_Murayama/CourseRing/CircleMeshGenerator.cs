using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CircleMeshGenerator : MonoBehaviour
{
    [SerializeField] private int segments = 64;
    [SerializeField] private float radius = 1.0f;

#if UNITY_EDITOR
    [ContextMenu("Generate Circle Mesh")]
    private void GenerateCircleMesh()
    {
        segments = Mathf.Max(3, segments);

        Mesh mesh = new Mesh();
        mesh.name = $"CircleMesh_{segments}";

        // 中心 + 円周
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];

        // 中心
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        // 円周
        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            vertices[i + 1] = new Vector3(x, 0.0f, z);

            // 円形UV
            uvs[i + 1] = new Vector2(
                x / radius * 0.5f + 0.5f,
                z / radius * 0.5f + 0.5f
            );
        }

        // 三角形
        int[] triangles = new int[segments * 3];

        for (int i = 0; i < segments; i++)
        {
            int current = i + 1;
            int next = (i + 1) % segments + 1;

            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = current;
            triangles[i * 3 + 2] = next;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Circle Mesh",
            $"CircleMesh_{segments}",
            "asset",
            "保存する円形メッシュを選択してください"
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Circle Mesh saved: {path}");

            Selection.activeObject = mesh;
        }
    }
#endif
}