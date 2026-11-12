using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class SpeedRingVisual : MonoBehaviour
{
    [Header("リングの見た目")]
    [SerializeField, InspectorName("描画用シェーダー")] private Shader _ringShader;

    [SerializeField, ColorUsage(false, true), InspectorName("加速時のリング色")]
    private Color _speedUpColor = new Color(0.08f, 1f, 0.16f);
    [SerializeField, ColorUsage(false, true), InspectorName("減速時のリング色")]
    private Color _slowDownColor = new Color(1f, 0.04f, 0.02f);
    [Header("中心へ流れる粒")]
    [SerializeField, Range(0.002f, 0.05f), InspectorName("粒の大きさ")]
    private float _particleSize = 0.012f;
    [SerializeField, Range(0f, 3f), InspectorName("加速時の粒の速さ")]
    private float _speedUpParticleSpeed = 0.7f;
    [SerializeField, Range(0f, 3f), InspectorName("減速時の粒の速さ")]
    private float _slowDownParticleSpeed = 0.18f;
    [SerializeField, Range(0.1f, 0.8f), InspectorName("外周で浮遊する時間の割合")]
    private float _particleHoverRatio = 0.5f;
    [SerializeField, Range(0f, 0.15f), InspectorName("外周の浮遊範囲")]
    private float _particleHoverWidth = 0.07f;
    [Header("加速時のトレイル")]
    [SerializeField, Range(0f, 0.5f), InspectorName("尾が残る時間（秒）")]
    private float _trailDuration = 0.16f;
    [SerializeField, Range(0f, 2f), InspectorName("尾の明るさ")]
    private float _trailBrightness = 0.65f;
    private static readonly int RingColor = Shader.PropertyToID("_RingColor");
    private static readonly int ParticleSize = Shader.PropertyToID("_ParticleSize");
    private static readonly int ParticleTravel = Shader.PropertyToID("_ParticleTravel");
    private static readonly int ParticleHoverRatio = Shader.PropertyToID("_ParticleHoverRatio");
    private static readonly int ParticleHoverWidth = Shader.PropertyToID("_ParticleHoverWidth");
    private static readonly int TrailSpan = Shader.PropertyToID("_TrailSpan");
    private static readonly int TrailBrightness = Shader.PropertyToID("_TrailBrightness");

    private Material _material;
    private Mesh _mesh;
    private bool _isSlowing;
    private float _particleTravel;

    public void SetSlowing(bool isSlowing)
    {
        _isSlowing = isSlowing;
        if (_material == null)
        {
            CreateRingVisual();
            return;
        }

        ApplyColor();
    }

    private void ApplyColor()
    {
        if (_material != null)
        {
            _material.SetColor(RingColor, _isSlowing ? _slowDownColor : _speedUpColor);
            _material.SetFloat(ParticleSize, _particleSize);
            _material.SetFloat(ParticleHoverRatio, _particleHoverRatio);
            _material.SetFloat(ParticleHoverWidth, _particleHoverWidth);
            ApplyTrail();
        }
    }

    private void ApplyTrail()
    {
        _material.SetFloat(TrailSpan, _isSlowing ? 0f : Mathf.Min(_speedUpParticleSpeed * _trailDuration, 0.4f));
        _material.SetFloat(TrailBrightness, _trailBrightness);
    }

    private void Update()
    {
        if (_material == null)
            return;

        // Integrate distance so changing ring state or speed never jumps the particles.
        float speed = _isSlowing ? _slowDownParticleSpeed : _speedUpParticleSpeed;
        _particleTravel = Mathf.Repeat(_particleTravel + Time.deltaTime * speed, 1f);
        _material.SetFloat(ParticleTravel, _particleTravel);
        ApplyTrail();
    }

    private void OnValidate()
    {
        ApplyColor();
    }

    private void CreateRingVisual()
    {
        if (_ringShader == null)
            return;

        _material = new Material(_ringShader);
        ApplyColor();

        var visual = new GameObject("Ring Visual");
        visual.layer = gameObject.layer;
        visual.transform.SetParent(transform, false);
        BoxCollider area = GetComponent<BoxCollider>();
        // The luminous edge is at UV radius 0.65. Leave space around it for the halo.
        Vector3 halfSize = new Vector3(area.size.x, area.size.y, 0f) * (0.5f / 0.65f);
        _mesh = new Mesh
        {
            name = "Speed Ring Portal Quad",
            vertices = new[]
            {
                area.center + new Vector3(-halfSize.x, -halfSize.y, 0),
                area.center + new Vector3(halfSize.x, -halfSize.y, 0),
                area.center + new Vector3(-halfSize.x, halfSize.y, 0),
                area.center + new Vector3(halfSize.x, halfSize.y, 0)
            },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        _mesh.RecalculateBounds();
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
