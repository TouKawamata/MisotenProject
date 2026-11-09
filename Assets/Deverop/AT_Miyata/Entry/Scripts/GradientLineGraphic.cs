using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 左端から指定位置まで透明度が 0→1 に変化する線
/// RectTransform の幅が長さ、高さが太さになる
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class GradientLineGraphic : MaskableGraphic {
	[SerializeField, Range(0.0f, 1.0f), Tooltip("左端からどこまでを薄くするか(線の長さに対する割合)")] private float _fadeRatio = 0.5f;
	[SerializeField, Tooltip("薄くなり方")] private Ease _fadeEase = Ease.Linear;
	[SerializeField, Min(1), Tooltip("フェード部分の分割数。Linear なら 1 で十分")] private int _fadeSegments = 8;

	protected override void Awake() {
		base.Awake();
		raycastTarget = false;
	}

	protected override void OnPopulateMesh(VertexHelper vh) {
		vh.Clear();

		Rect rect = GetPixelAdjustedRect();
		float fadeEndX = rect.xMin + rect.width * _fadeRatio;

		// 左端からフェード終了位置までを分割し、最後に右端を足す
		int column = 0;
		for (int i = 0; i <= _fadeSegments; i++) {
			float t = (float)i / _fadeSegments;
			float x = Mathf.Lerp(rect.xMin, fadeEndX, t);
			float alpha = _fadeRatio <= 0.0f ? 1.0f : DOVirtual.EasedValue(0.0f, 1.0f, t, _fadeEase);
			AddColumn(vh, x, rect, alpha, ref column);
		}
		AddColumn(vh, rect.xMax, rect, 1.0f, ref column);
	}

	// 上下2頂点を追加し、直前の列との間を四角形でつなぐ
	private void AddColumn(VertexHelper vh, float x, Rect rect, float alpha, ref int column) {
		Color32 c = color;
		c.a = (byte)(c.a * Mathf.Clamp01(alpha));

		vh.AddVert(new Vector3(x, rect.yMin), c, Vector4.zero);
		vh.AddVert(new Vector3(x, rect.yMax), c, Vector4.zero);

		if (column > 0) {
			int i = column * 2;
			vh.AddTriangle(i - 2, i - 1, i + 1);
			vh.AddTriangle(i + 1, i, i - 2);
		}
		column++;
	}

#if UNITY_EDITOR
	protected override void OnValidate() {
		base.OnValidate();
		SetVerticesDirty();
	}
#endif
}
