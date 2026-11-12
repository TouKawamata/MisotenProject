using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class SpeedRingVisual : MonoBehaviour
{
    [SerializeField, InspectorName("リング描画用シェーダー")] private Shader _ringShader;

    private static readonly Color SpeedUpColor = new Color(0.1f, 1f, 0.55f);
    private static readonly Color SlowDownColor = new Color(1f, 0.25f, 0.1f);
    private static readonly int RingColor = Shader.PropertyToID("_RingColor");

    private Material _material;
    private Mesh _mesh;

    public void SetSlowing(bool isSlowing)
    {
        if (_material == null)
        {
            CreateRingVisual(isSlowing);
            return;
        }

        _material.SetColor(RingColor, isSlowing ? SlowDownColor : SpeedUpColor);
    }

    private void CreateRingVisual(bool isSlowing)
    {
        if (_ringShader == null)
            return;

        _material = new Material(_ringShader);
        _material.SetColor(RingColor, isSlowing ? SlowDownColor : SpeedUpColor);

        var visual = new GameObject("Ring Visual");
        visual.layer = gameObject.layer;
        visual.transform.SetParent(transform, false);
        const int segments = 64;
        BoxCollider area = GetComponent<BoxCollider>();
        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];
        vertices[0] = area.center;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            vertices[i + 1] = area.center
                + new Vector3(Mathf.Cos(angle) * area.size.x, Mathf.Sin(angle) * area.size.y, 0f) * 0.5f;

            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = (i + 1) % segments + 1;
        }

        _mesh = new Mesh { name = "Speed Ring Disc", vertices = vertices, triangles = triangles };
        visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
        if (_mesh != null)
            Destroy(_mesh);
    }
}
