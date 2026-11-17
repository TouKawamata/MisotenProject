using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 斜め帯・稲妻用の単色ポリゴン。凹形状も輪郭どおりに三角形へ分割する。
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResultPolygonGraphic : MaskableGraphic
{
    [SerializeField] private Vector2[] _points = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_points == null || _points.Length < 3) return;
        Rect r = rectTransform.rect;
        var indices = new List<int>(_points.Length);
        float area = 0;
        for (int i = 0; i < _points.Length; i++)
        {
            Vector2 p = _points[i];
            Vector2 q = _points[(i + 1) % _points.Length];
            area += p.x * q.y - q.x * p.y;
            vh.AddVert(new Vector3(r.x + p.x * r.width, r.y + p.y * r.height), color, Vector2.zero);
            indices.Add(i);
        }
        if (area < 0) indices.Reverse();
        int guard = _points.Length * _points.Length;
        while (indices.Count > 2 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[(i + indices.Count - 1) % indices.Count];
                int b = indices[i];
                int c = indices[(i + 1) % indices.Count];
                if (Cross(_points[b] - _points[a], _points[c] - _points[b]) <= 0.000001f) continue;
                bool containsPoint = false;
                foreach (int candidate in indices)
                {
                    if (candidate == a || candidate == b || candidate == c) continue;
                    Vector2 p = _points[candidate];
                    if (Cross(_points[b] - _points[a], p - _points[a]) >= 0 &&
                        Cross(_points[c] - _points[b], p - _points[b]) >= 0 &&
                        Cross(_points[a] - _points[c], p - _points[c]) >= 0)
                    { containsPoint = true; break; }
                }
                if (containsPoint) continue;
                vh.AddTriangle(a, b, c);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
