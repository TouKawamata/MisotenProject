using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Jagged circular waves scrolling across one hemisphere. Local +Z is forward.</summary>
[DisallowMultipleComponent]
public sealed class TackleEffect : MonoBehaviour
{
    [Header("Shape / 形状（ローカル軸）")]
    [Tooltip("全体の大きさ。Width / Height / Depth はこの半径に対する倍率です。")]
    [SerializeField, Min(0.1f)] private float _radius = 1.5f;
    [Tooltip("横幅（X軸）の倍率。1が標準。")]
    [SerializeField, Min(0.01f)] private float _width = 1f;
    [Tooltip("高さ（Y軸）の倍率。1が標準。")]
    [SerializeField, Min(0.01f)] private float _height = 1f;
    [Tooltip("前方への奥行き（Z軸）の倍率。1より大きくすると前へ長く伸びます。")]
    [SerializeField, Min(0.01f)] private float _depth = 0.7f;
    [Header("Animation / 動き")]
    [SerializeField, Range(0.08f, 5f)] private float _duration = 2.5f;
    [SerializeField, Range(0.2f, 5f)] private float _scrollSpeed = 1.8f;
    [UnityEngine.Serialization.FormerlySerializedAs("_gold")]
    [SerializeField, ColorUsage(true, true)] private Color _windColor = new Color(1.8f, 2.4f, 2.8f, 1f);
    [SerializeField, Range(0f, 2f)] private float _windStrength = 1.1f;
    [SerializeField] private bool _demoLoop = false;
    [SerializeField, Min(0.5f)] private float _demoInterval = 1.2f;

    private Material _material;
    private Mesh _mesh;
    private Transform _shell;
    private float _age;
    private float _demoAge;
    private float _strength = 1f;
    private bool _playing;
    private bool _fixedPosition;
    private Vector3 _position;
    private Quaternion _rotation;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");

    private void Awake() => Build();

    private bool Build()
    {
        if (_material != null) return true;
        Shader shader = Resources.Load<Shader>("TackleGlow");
        if (shader == null)
        {
            Debug.LogError("TackleGlow shader is missing", this);
            return false;
        }
        _material = new Material(shader) { name = "Tackle hemisphere (runtime)" };
        const int rows = 24, columns = 96;
        var vertices = new Vector3[(rows + 1) * (columns + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[rows * columns * 6];
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            int index = y * (columns + 1) + x;
            float u = x / (float)columns, v = y / (float)rows;
            float phi = v * Mathf.PI * 0.5f, theta = u * Mathf.PI * 2;
            Vector3 p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi));
            vertices[index] = normals[index] = p;
            uv[index] = new Vector2(u, v);
            if (x == columns || y == rows) continue;
            int k = (y * columns + x) * 6;
            triangles[k] = index; triangles[k + 1] = index + columns + 1; triangles[k + 2] = index + 1;
            triangles[k + 3] = index + 1; triangles[k + 4] = index + columns + 1; triangles[k + 5] = index + columns + 2;
        }
        _mesh = new Mesh { name = "Forward 180 degree impact", vertices = vertices,
            normals = normals, uv = uv, triangles = triangles };
        _mesh.RecalculateBounds();
        _shell = new GameObject("Hemisphere impact").transform;
        _shell.gameObject.layer = gameObject.layer;
        _shell.SetParent(transform, false);
        _shell.gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = _shell.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        _shell.gameObject.SetActive(false);
        return true;
    }

    [ContextMenu("Play Tackle (Play Mode)")]
    public void PlayTackle()
    {
        if (!Application.isPlaying || !Build()) return;
        _fixedPosition = false;
        Begin(1f);
    }

    // All entry points play one timed UV-scrolling effect.
    public void PlayRush() => PlayTackle();
    public void PlayImpact() => PlayTackle();

    public void PlayImpactAt(Vector3 position, Quaternion rotation, float strength = 1f)
    {
        if (!Application.isPlaying || !Build()) return;
        _fixedPosition = true;
        _position = position;
        _rotation = rotation;
        Begin(strength);
    }

    private void Begin(float strength)
    {
        _age = 0;
        _strength = Mathf.Clamp(strength, 0.1f, 2f);
        _playing = true;
        _shell.gameObject.SetActive(true);
        Draw(0);
    }

    [ContextMenu("Stop (Play Mode)")]
    public void StopRush()
    {
        _playing = false;
        if (_shell != null) _shell.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_demoLoop)
        {
            _demoAge -= Time.deltaTime;
            if (_demoAge <= 0)
            {
                PlayTackle();
                _demoAge = Mathf.Max(_demoInterval, _duration + 0.1f);
            }
        }
        if (!_playing) return;
        _age += Time.deltaTime;
        if (_age >= _duration) { StopRush(); return; }
        Draw(_age / _duration);
    }

    private void Draw(float t)
    {
        if (_fixedPosition) _shell.SetPositionAndRotation(_position, _rotation);
        else
        {
            _shell.localPosition = Vector3.zero;
            _shell.localRotation = Quaternion.identity;
        }
        // Keep one hemisphere in place; only the surface pattern travels outward.
        float radius = _radius * _strength;
        _shell.localScale = new Vector3(radius * Mathf.Max(0.01f, _width),
            radius * Mathf.Max(0.01f, _height), radius * Mathf.Max(0.01f, _depth));
        Color color = _windColor;
        color.a *= Mathf.Clamp01(_age / 0.06f)
            * Mathf.Clamp01((_duration - _age) / Mathf.Min(0.3f, _duration));
        _material.SetColor(ColorId, color);
        _material.SetFloat(PhaseId, _age * _scrollSpeed);
        _material.SetFloat(WindStrengthId, _windStrength);
    }

    private void OnDisable() { StopRush(); _demoAge = 0; }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_mesh != null) Destroy(_mesh);
    }
}


