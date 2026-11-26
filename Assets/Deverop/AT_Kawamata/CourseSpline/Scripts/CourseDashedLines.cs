using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class CourseDashedLines : MonoBehaviour
{
    [SerializeField] private Shader _shader;
    [SerializeField] private Color _color = new Color(0.65f, 0.9f, 1f);
    [SerializeField, Min(0f), Tooltip("スプラインの始点から点線の表示開始位置までの距離（m）")]
    private float _startDistance;
    [SerializeField, Min(0f), Tooltip("スプラインの終点から点線の表示終了位置までの距離（m）")]
    private float _goalDistanceFromEnd;
    [SerializeField, Min(0.25f)] private float _dashLength = 5f;
    [SerializeField, Min(0.25f)] private float _gapLength = 7f;
    [SerializeField, Min(0.01f)] private float _width = 0.28f;

    private CourseSpline _spline;
    private GameObject _visual;
    private Mesh _mesh;
    private Material _material;
    private int _sampleRevision = -1;

    private void OnEnable()
    {
        FindCourse();
        if (_shader == null) return;

        _visual = new GameObject("Course Dashed Lines") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        _visual.transform.SetParent(transform, false);
        _mesh = new Mesh { name = "Course Dashed Lines", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
        _material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
        _visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = _visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void OnValidate()
    {
        _sampleRevision = -1;
    }

    private void OnTransformParentChanged()
    {
        FindCourse();
        if (_mesh != null) _mesh.Clear();
    }

    private void FindCourse()
    {
        _spline = GetComponentInParent<CourseSpline>();
        _sampleRevision = -1;
    }

    private void LateUpdate()
    {
        if (_mesh == null || _spline == null || _sampleRevision == _spline.SampleRevision) return;

        _sampleRevision = _spline.SampleRevision;
        _material.SetColor("_BaseColor", _color);
        Rebuild();
    }

    private void OnDisable()
    {
        if (_visual != null) _visual.SetActive(false);
        Release(_visual);
        Release(_mesh);
        Release(_material);
        _visual = null;
        _mesh = null;
        _material = null;
        _sampleRevision = -1;
    }

    private void Rebuild()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float length = _spline.TotalLength;
        float displayStart = Mathf.Clamp(_startDistance, 0f, length);
        float displayEnd = length - Mathf.Clamp(_goalDistanceFromEnd, 0f, length);
        float dashLength = Mathf.Max(0.25f, _dashLength);
        float gapLength = Mathf.Max(0.25f, _gapLength);
        float halfWidth = Mathf.Max(0.01f, _width) * 0.5f;

        for (int horizontal = -1; horizontal <= 1; horizontal += 2)
        {
            for (int vertical = -1; vertical <= 1; vertical += 2)
            {
                for (float start = displayStart; start < displayEnd; start += dashLength + gapLength)
                {
                    float end = Mathf.Min(start + dashLength, displayEnd);
                    int steps = Mathf.CeilToInt(end - start);
                    int first = vertices.Count;
                    for (int step = 0; step <= steps; step++)
                    {
                        float distance = Mathf.Lerp(start, end, (float)step / steps);
                        Vector3 right = _spline.EvaluateRightByDistance(distance);
                        Vector3 up = _spline.EvaluateUpByDistance(distance);
                        float offset = _spline.GetTunnelRadiusByDistance(distance) / 3f;
                        Vector3 center = _spline.EvaluatePositionByDistance(distance)
                            + (right * horizontal + up * vertical) * offset;
                        Vector3 width = right * halfWidth;
                        Vector3 height = up * halfWidth;
                        vertices.Add(transform.InverseTransformPoint(center - width - height));
                        vertices.Add(transform.InverseTransformPoint(center + width - height));
                        vertices.Add(transform.InverseTransformPoint(center + width + height));
                        vertices.Add(transform.InverseTransformPoint(center - width + height));
                        if (step == 0) continue;

                        int ring = vertices.Count - 4;
                        for (int side = 0; side < 4; side++)
                        {
                            int nextSide = (side + 1) % 4;
                            AddQuad(triangles, ring - 4 + side, ring - 4 + nextSide, ring + nextSide, ring + side);
                        }
                    }

                    int last = vertices.Count - 4;
                    AddQuad(triangles, first + 3, first + 2, first + 1, first);
                    AddQuad(triangles, last, last + 1, last + 2, last + 3);
                }
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }
}
