using UnityEngine;

// CPUの頭上に、個性・難易度・今していること（狙っている相手・ブースト・シールドなど）を表示する（デバッグ用）。
// Gameビューは OnGUI、Sceneビューは Handles で描く。どちらも CpuRaceInput から呼ばれる。
public static class CpuLabelDrawer
{
    // この距離（m）より遠いCPUは、Gameビューに表示しない。
    private const float MaxGameViewDistance = 300f;

    private const int FontSize = 14;

    private static GUIStyle _style;
    private static GUIStyle _sceneStyle;

    public static void DrawGameView(Vector3 worldPosition, string text, Color color)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f || screen.z > MaxGameViewDistance)
        {
            return;
        }

        GUIStyle style = GetStyle();
        GUIContent content = new GUIContent(text);
        Vector2 size = style.CalcSize(content);

        // OnGUIの座標は左上が原点なので、上下を反転する。
        Rect rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);

        // 背景が明るくても読めるよう、黒い影を付ける。
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, style);
        style.normal.textColor = color;
        GUI.Label(rect, content, style);
    }

    public static void DrawSceneView(Vector3 worldPosition, string text, Color color)
    {
#if UNITY_EDITOR
        // OnDrawGizmosの中ではGUI.skinを使えないので、Gameビューとは別のスタイルを使う。
        _sceneStyle ??= new GUIStyle
        {
            fontSize = FontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerCenter
        };
        _sceneStyle.normal.textColor = color;
        UnityEditor.Handles.Label(worldPosition, text, _sceneStyle);
#endif
    }

    // GUI.skinを使うので、OnGUIの中からだけ呼ぶ。
    private static GUIStyle GetStyle()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerCenter
            };
        }

        return _style;
    }
}
