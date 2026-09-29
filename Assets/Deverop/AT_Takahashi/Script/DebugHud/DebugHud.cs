#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using UnityEngine.InputSystem;

public class DebugHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerFlightController playerFlight;
    [SerializeField] private BoostController boostController;
    [SerializeField] private RaceInputProvider raceInputProvider;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private CourseSpline courseSpline;
    [SerializeField] private Transform player;

    [Header("HUD")]
    [SerializeField] private bool visible = true;
    [SerializeField] private Vector2 position = new(12f, 12f);
    [SerializeField, Min(10)] private int fontSize = 16;
    [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.75f;

    [Header("Horizontal Graph")]
    [SerializeField, Min(1f)] private float graphSeconds = 4f;
    [SerializeField, Min(10)] private int graphSamplesPerSecond = 30;
    [SerializeField] private Vector2 graphSize = new(360f, 100f);
    [SerializeField, Range(0f, 1f)] private float horizontalGuide = 0.7f;

    [Header("Sampling")]
    [SerializeField, Min(1)] private int courseSampleIntervalFrames = 5;

    [Header("Pressed Indicator")]
    [SerializeField, Min(0.05f)] private float pressedHoldSeconds = 0.5f;

    private float[] horizontalHistory;
    private int historyWriteIndex;
    private int historyCount;
    private float historyTimer;

    private float boostPressedUntil;
    private float shieldPressedUntil;
    private float shortcutPressedUntil;

    private float courseDistance;
    private float courseProgress;
    private float courseOffsetRight;
    private float courseOffsetUp;

    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private Texture2D whiteTexture;

    private Rect windowRect;

    private bool inputExpanded = true;
    private bool flightExpanded = true;
    private bool boostExpanded = true;
    private bool courseExpanded = true;
    private bool performanceExpanded = true;

    private float fps;
    private float smoothedDeltaTime;

    private void Awake()
    {
        int capacity =
            Mathf.Max(2, Mathf.CeilToInt(graphSeconds * graphSamplesPerSecond));

        horizontalHistory = new float[capacity];

        windowRect = new Rect(
            position.x,
            position.y,
            Mathf.Max(420f, graphSize.x + 40f),
            700f);

    }

    private void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
    }

    private void Update()
    {
        // F1 toggle
        if (Keyboard.current != null &&
            Keyboard.current.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
        }

        // 非表示でもF1だけは監視する。
        if (!visible)
            return;

        var input = raceInputProvider != null
            ? raceInputProvider.Current
            : null;

        if (input != null)
        {
            float horizontal = input.Horizontal;

            SampleHorizontal(horizontal);

            // Pressed系は1フレームのため、一定時間表示を保持する。
            if (input.BoostPressed)
                boostPressedUntil = Time.unscaledTime + pressedHoldSeconds;

            if (input.ShieldPressed)
                shieldPressedUntil = Time.unscaledTime + pressedHoldSeconds;

            if (input.ShortcutPressed)
                shortcutPressedUntil = Time.unscaledTime + pressedHoldSeconds;
        }

        // CourseSplineの重い検索は間引く。
        if (Time.frameCount % courseSampleIntervalFrames == 0)
            UpdateCourseInformation();

        // FPSは瞬間値だと読みにくいため簡易平滑化。
        smoothedDeltaTime = Mathf.Lerp(
            smoothedDeltaTime,
            Time.unscaledDeltaTime,
            0.1f);

        if (smoothedDeltaTime > Mathf.Epsilon)
            fps = 1f / smoothedDeltaTime;
    }

    private void SampleHorizontal(float value)
    {
        float interval = 1f / graphSamplesPerSecond;

        historyTimer += Time.unscaledDeltaTime;

        // フレームレート依存を避けて一定間隔で保存。
        while (historyTimer >= interval)
        {
            historyTimer -= interval;

            horizontalHistory[historyWriteIndex] = value;
            historyWriteIndex =
                (historyWriteIndex + 1) % horizontalHistory.Length;

            historyCount =
                Mathf.Min(historyCount + 1, horizontalHistory.Length);
        }
    }

    private void UpdateCourseInformation()
    {
        if (player == null || courseSpline == null)
            return;

        Vector3 playerPosition = player.position;

        courseDistance =
            courseSpline.FindNearestDistance(playerPosition);

        float totalLength = courseSpline.TotalLength;

        courseProgress = totalLength > Mathf.Epsilon
            ? courseDistance / totalLength * 100f
            : 0f;

        Vector3 centerPosition =
            courseSpline.GetCenterPosition(playerPosition);

        Vector3 offset = playerPosition - centerPosition;

        // NOTE:
        // GetRight / GetUp の実際のシグネチャが仕様書にはないため、
        // プロジェクト側APIに合わせてここだけ調整してください。
        Vector3 right = courseSpline.GetRight(playerPosition);
        Vector3 up = courseSpline.GetUp(playerPosition);

        courseOffsetRight = Vector3.Dot(offset, right);
        courseOffsetUp = Vector3.Dot(offset, up);
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();

        GUI.backgroundColor =
            new Color(0f, 0f, 0f, backgroundAlpha);

        windowRect = GUI.Window(
            GetInstanceID(),
            windowRect,
            DrawWindow,
            "DEBUG HUD  [F1 : Hide]");

        GUI.backgroundColor = Color.white;
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        DrawInputSection();
        DrawFlightSection();
        DrawBoostSection();
        DrawCourseSection();
        DrawPerformanceSection();

        GUILayout.EndVertical();

        GUI.DragWindow(
            new Rect(0f, 0f, windowRect.width, 24f));
    }

    // ----------------------------------------------------------------
    // Input
    // ----------------------------------------------------------------

    private void DrawInputSection()
    {
        if (!DrawFoldout("INPUT / TRACKING", ref inputExpanded))
            return;

        var input = raceInputProvider != null
            ? raceInputProvider.Current
            : null;

        if (input == null)
        {
            DrawValue("Input", "NULL", Color.red);
            return;
        }

        GUILayout.Label(
            $"Source : {input.GetType().Name}",
            labelStyle);

        DrawValue(
            "Horizontal",
            input.Horizontal.ToString("+0.000;-0.000;0.000"));

        DrawPressed(
            "BoostPressed",
            Time.unscaledTime < boostPressedUntil);

        DrawPressed(
            "ShieldPressed",
            Time.unscaledTime < shieldPressedUntil);

        DrawPressed(
            "ShortcutPressed",
            Time.unscaledTime < shortcutPressedUntil);

        GUILayout.Space(4f);

        Rect graphRect =
            GUILayoutUtility.GetRect(graphSize.x, graphSize.y);

        DrawHorizontalGraph(graphRect);

        // Tracking実装時だけ追加情報を表示。
        //if (input is ITrackingStatus tracking)
        //{
        //    GUILayout.Space(5f);

        //    GUILayout.Label("Tracking", headerStyle);

        //    DrawValue(
        //        "Connected",
        //        tracking.IsConnected ? "YES" : "NO",
        //        tracking.IsConnected ? Color.green : Color.red);

        //    DrawValue(
        //        "Raw Horizontal",
        //        tracking.RawHorizontal.ToString("+0.000;-0.000;0.000"));

        //    DrawValue(
        //        "Last Receive",
        //        $"{tracking.SecondsSinceLastReceive:F3} sec");

        //    DrawValue(
        //        "Receive Rate",
        //        $"{tracking.ReceiveRate:F1} Hz");
        //}
    }

    // ----------------------------------------------------------------
    // Flight
    // ----------------------------------------------------------------

    private void DrawFlightSection()
    {
        if (!DrawFoldout("FLIGHT", ref flightExpanded))
            return;

        if (playerFlight == null)
        {
            DrawValue("PlayerFlightController", "NULL", Color.red);
            return;
        }

        float speed = playerFlight.Velocity.magnitude;
        float maxSpeed =
            boostController != null
                ? boostController.CurrentMaxSpeed
                : 0f;

        DrawValue("Speed", $"{speed:F1} m/s");
        DrawValue("Max Speed", $"{maxSpeed:F1} m/s");

        DrawBar(
            maxSpeed > Mathf.Epsilon ? speed / maxSpeed : 0f,
            Color.cyan);

        if (player != null &&
            playerFlight.Velocity.sqrMagnitude > 0.001f)
        {
            float slipAngle = Vector3.Angle(
                player.forward,
                playerFlight.Velocity);

            DrawValue(
                "Slip Angle",
                $"{slipAngle:F1} deg");
        }

        DrawValue(
            "Roll",
            $"{playerFlight.CurrentRoll:F1} deg");

        DrawValue(
            "Tunnel Clamp",
            playerFlight.IsClampedToTunnel ? "CONTACT" : "OFF",
            playerFlight.IsClampedToTunnel
                ? new Color(1f, 0.6f, 0f)
                : Color.white);

        if (courseSpline != null &&
            courseSpline.TunnelRadius > Mathf.Epsilon)
        {
            float radius = courseSpline.TunnelRadius;

            DrawValue(
                "Offset LR",
                $"{courseOffsetRight:+0.00;-0.00;0.00} m  " +
                $"({courseOffsetRight / radius * 100f:+0.0;-0.0;0.0}%)");

            DrawValue(
                "Offset UD",
                $"{courseOffsetUp:+0.00;-0.00;0.00} m  " +
                $"({courseOffsetUp / radius * 100f:+0.0;-0.0;0.0}%)");
        }
    }

    // ----------------------------------------------------------------
    // Boost / Shortcut
    // ----------------------------------------------------------------

    private void DrawBoostSection()
    {
        if (!DrawFoldout(
                "BOOST / SHORTCUT",
                ref boostExpanded))
            return;

        if (boostController != null)
        {
            string state =
                boostController.IsBoosting
                    ? "BOOSTING"
                    : boostController.IsOnCooldown
                        ? "COOLDOWN"
                        : "IDLE";

            Color stateColor =
                boostController.IsBoosting
                    ? Color.green
                    : boostController.IsOnCooldown
                        ? Color.yellow
                        : Color.white;

            DrawValue("Boost", state, stateColor);

            DrawValue(
                "Boost Time",
                $"{boostController.BoostRemainingTime:F2} sec");

            DrawValue(
                "Cooldown",
                $"{boostController.CooldownRemainingTime:F2} sec");

            DrawValue(
                "Forward Accel",
                boostController.CurrentForwardAcceleration.ToString("F2"));

            DrawValue(
                "Steering Power",
                boostController.CurrentSteeringPower.ToString("F2"));
        }

        if (playerManager != null)
        {
            string profileName =
                playerManager.CurrentProfile != null
                    ? playerManager.CurrentProfile.name
                    : "(null)";

            DrawValue("Profile", profileName);

            DrawValue(
                "Shortcut Zone",
                playerManager.IsInShortcutZone ? "IN" : "OUT",
                playerManager.IsInShortcutZone
                    ? Color.yellow
                    : Color.white);

            DrawValue(
                "Shortcut Snap",
                playerManager.IsShortcutProfileActive
                    ? "ACTIVE"
                    : "OFF",
                playerManager.IsShortcutProfileActive
                    ? Color.green
                    : Color.white);
        }
    }

    // ----------------------------------------------------------------
    // Course
    // ----------------------------------------------------------------

    private void DrawCourseSection()
    {
        if (!DrawFoldout("COURSE", ref courseExpanded))
            return;

        if (courseSpline == null)
        {
            DrawValue("CourseSpline", "NULL", Color.red);
            return;
        }

        DrawValue(
            "Distance",
            $"{courseDistance:F1} / {courseSpline.TotalLength:F1} m");

        DrawValue(
            "Progress",
            $"{courseProgress:F1}%");

        DrawBar(
            Mathf.Clamp01(courseProgress / 100f),
            new Color(0.2f, 0.8f, 1f));
    }

    // ----------------------------------------------------------------
    // Performance
    // ----------------------------------------------------------------

    private void DrawPerformanceSection()
    {
        if (!DrawFoldout(
                "PERFORMANCE",
                ref performanceExpanded))
            return;

        DrawValue("FPS", fps.ToString("F1"));
        DrawValue(
            "Frame Time",
            $"{smoothedDeltaTime * 1000f:F2} ms");
    }

    // ----------------------------------------------------------------
    // Horizontal graph
    // ----------------------------------------------------------------

    private void DrawHorizontalGraph(Rect rect)
    {
        DrawRect(
            rect,
            new Color(0f, 0f, 0f, 0.65f));

        // zero
        DrawHorizontalLine(
            rect,
            0f,
            new Color(1f, 1f, 1f, 0.25f));

        // +0.7 / -0.7 guide
        DrawHorizontalLine(
            rect,
            horizontalGuide,
            new Color(1f, 0.65f, 0f, 0.75f));

        DrawHorizontalLine(
            rect,
            -horizontalGuide,
            new Color(1f, 0.65f, 0f, 0.75f));

        if (historyCount < 2)
            return;

        float columnWidth =
            rect.width / Mathf.Max(1, horizontalHistory.Length - 1);

        int firstIndex =
            historyCount == horizontalHistory.Length
                ? historyWriteIndex
                : 0;

        for (int i = 0; i < historyCount; i++)
        {
            int index =
                (firstIndex + i) % horizontalHistory.Length;

            float value =
                Mathf.Clamp(horizontalHistory[index], -1f, 1f);

            float x = rect.x + i * columnWidth;

            float centerY = rect.center.y;
            float valueY =
                centerY - value * rect.height * 0.5f;

            Color color =
                Mathf.Abs(value) >= horizontalGuide
                    ? new Color(1f, 0.75f, 0.1f)
                    : Color.green;

            // 1pixelの縦線。
            float top = Mathf.Min(centerY, valueY);
            float height = Mathf.Max(1f, Mathf.Abs(valueY - centerY));

            DrawRect(
                new Rect(x, top, Mathf.Max(1f, columnWidth), height),
                color);
        }

        GUI.Label(
            new Rect(rect.x + 4f, rect.y + 2f, 80f, 20f),
            "+1.0",
            labelStyle);

        GUI.Label(
            new Rect(rect.x + 4f, rect.yMax - 20f, 80f, 20f),
            "-1.0",
            labelStyle);
    }

    private void DrawHorizontalLine(
        Rect rect,
        float value,
        Color color)
    {
        value = Mathf.Clamp(value, -1f, 1f);

        float y =
            rect.center.y - value * rect.height * 0.5f;

        DrawRect(
            new Rect(rect.x, y, rect.width, 1f),
            color);
    }

    // ----------------------------------------------------------------
    // GUI helpers
    // ----------------------------------------------------------------

    private bool DrawFoldout(string title, ref bool expanded)
    {
        GUILayout.Space(4f);

        string symbol = expanded ? "▼ " : "▶ ";

        if (GUILayout.Button(
                symbol + title,
                buttonStyle))
        {
            expanded = !expanded;
        }

        return expanded;
    }

    private void DrawValue(
        string label,
        string value,
        Color? color = null)
    {
        Color old = GUI.contentColor;

        if (color.HasValue)
            GUI.contentColor = color.Value;

        GUILayout.Label(
            $"{label,-18} : {value}",
            labelStyle);

        GUI.contentColor = old;
    }

    private void DrawPressed(string label, bool active)
    {
        DrawValue(
            label,
            active ? "● ON" : "○ off",
            active ? Color.green : Color.gray);
    }

    private void DrawBar(float value, Color color)
    {
        value = Mathf.Clamp01(value);

        Rect rect =
            GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true));

        DrawRect(
            rect,
            new Color(1f, 1f, 1f, 0.15f));

        Rect fill = rect;
        fill.width *= value;

        DrawRect(fill, color);
    }

    private void DrawRect(Rect rect, Color color)
    {
        if (whiteTexture == null)
        {
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }

        Color oldColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(rect, whiteTexture);

        GUI.color = oldColor;
    }

    private void CreateStyles()
    {
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleLeft
        };
    }

    private void EnsureStyles()
    {
        if (labelStyle == null ||
            labelStyle.fontSize != fontSize)
        {
            CreateStyles();
        }
    }
}

#endif