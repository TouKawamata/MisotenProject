using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Minakuchi.Sky
{
    // A closed slab whose two surfaces are displaced by the original 2D cloud noise.
    public sealed class MeshCloudLayer : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;

        public static MeshCloudLayer Create(Shader shader)
        {
            var root = new GameObject("立体雲（実行中）");
            root.hideFlags = HideFlags.DontSave;
            var layer = root.AddComponent<MeshCloudLayer>();
            layer.material = new Material(shader) { hideFlags = HideFlags.DontSave };
            layer.mesh = BuildMesh();
            root.AddComponent<MeshFilter>().sharedMesh = layer.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = layer.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return layer;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            // Keep the mesh around each rendering camera; noise remains anchored in world space.
            var p = camera.transform.position;
            transform.position = new Vector3(Mathf.Floor(p.x / 128) * 128, transform.position.y,
                Mathf.Floor(p.z / 128) * 128);
        }

        public void Apply(float height, float thickness, float scale, float coverage, float density,
            Vector2 offset, Vector3 sun, float night, float twilight, float sunset, Color tint)
        {
            transform.position = new Vector3(transform.position.x, height, transform.position.z);
            material.SetFloat("_Thickness", Mathf.Max(1, thickness));
            material.SetFloat("_Scale", Mathf.Max(1, scale));
            material.SetFloat("_Coverage", coverage);
            material.SetFloat("_Density", density);
            material.SetVector("_Offset", new Vector4(offset.x, offset.y, 0, 0));
            material.SetVector("_SunDir", sun);
            material.SetFloat("_Night", night);
            material.SetFloat("_Twilight", twilight);
            material.SetFloat("_Sunset", sunset);
            material.SetColor("_Tint", tint);
            // Vertex displacement must be included in CPU culling bounds.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(18000, Mathf.Max(1, thickness) + 2, 18000));
        }

        private static Mesh BuildMesh()
        {
            const int cells = 128, row = cells + 1, surface = row * row;
            var vertices = new Vector3[surface * 2];
            var indices = new List<int>(cells * cells * 12 + cells * 24);
            for (int side = 0; side < 2; side++)
                for (int z = 0; z <= cells; z++)
                    for (int x = 0; x <= cells; x++)
                        vertices[side * surface + z * row + x] =
                            new Vector3((x / (float)cells - 0.5f) * 18000, side == 0 ? 1 : -1,
                                (z / (float)cells - 0.5f) * 18000);
            void Quad(int a, int b, int c, int d)
            {
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(a); indices.Add(c); indices.Add(d);
            }
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * row + x;
                    Quad(a, a + row, a + row + 1, a + 1);
                    a += surface;
                    Quad(a, a + 1, a + row + 1, a + row);
                }
            for (int i = 0; i < cells; i++)
            {
                Quad(i, i + 1, i + 1 + surface, i + surface);
                int a = cells * row + i;
                Quad(a + 1, a, a + surface, a + 1 + surface);
                a = i * row;
                Quad(a + row, a, a + surface, a + row + surface);
                a += cells;
                Quad(a, a + row, a + row + surface, a + surface);
            }
            var mesh = new Mesh { name = "雲海の上下と側面", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.SetTriangles(indices, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(18000, 1000, 18000));
            return mesh;
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
