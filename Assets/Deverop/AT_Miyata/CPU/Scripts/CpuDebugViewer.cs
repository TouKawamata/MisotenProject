using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// CPUがちゃんと動いているかを確かめるためのデバッグ表示（シーンに1つ、空のオブジェクトに付けて使う）。
//   ・一覧：画面の左上に、全CPUの「移動・狙い・妨害弾・今していること」を1行ずつ出す（カメラから遠くても見える）
//   ・観察用カメラ：選んだCPUの真後ろに付いていく。位置は遅れずにぴったり付くので、高速でも離れない。
//                   右下の小窓 → 全画面 → 切る、をキーで切り替える（Playerのカメラはそのまま）
//   ・線：狙っている相手へ赤、妨害弾を撃った直後の標的へ紫（Gizmosで描く。選択していなくても全CPU分。GameビューはGizmosボタンがONのときだけ）
// 本番のシーンには置かない。
public class CpuDebugViewer : MonoBehaviour
{
    private enum EViewMode
    {
        Off,
        Window,
        FullScreen
    }

    // CPUの一覧を探し直す間隔（秒）。
    private const float RefreshInterval = 1f;

    // 観察用カメラの向きの追従の速さ（大きいほど速い）。位置は遅らせない。
    private const float RotationSharpness = 12f;

    [Header("キー")]
    [Tooltip("観察用カメラを 小窓 → 全画面 → 切る の順に切り替える")]
    [SerializeField] private Key _viewModeKey = Key.F2;

    [Tooltip("次のCPUを観察する")]
    [SerializeField] private Key _nextKey = Key.F3;

    [Tooltip("前のCPUを観察する")]
    [SerializeField] private Key _previousKey = Key.F4;

    [Tooltip("一覧の表示・非表示")]
    [SerializeField] private Key _panelKey = Key.F5;

    [Header("観察用カメラ")]
    [Tooltip("始めたときの観察用カメラ")]
    [SerializeField] private EViewMode _startViewMode = EViewMode.Window;

    [Tooltip("小窓の位置と大きさ（画面に対する割合。x・yは左下が0）")]
    [SerializeField] private Rect _windowRect = new Rect(0.6f, 0.02f, 0.38f, 0.38f);

    [Tooltip("CPUの後ろへの距離（m）")]
    [SerializeField] private float _followDistance = 8f;

    [Tooltip("CPUの上への高さ（m）")]
    [SerializeField] private float _followHeight = 2.5f;

    [Tooltip("CPUの前方のこの距離（m）を見る")]
    [SerializeField] private float _lookAhead = 10f;

    [SerializeField] private float _fieldOfView = 70f;

    [Header("一覧・線")]
    [SerializeField] private bool _showPanel = true;

    [SerializeField] private int _panelFontSize = 13;

    [Tooltip("狙っている相手（赤）・妨害弾を撃った直後の標的（紫）への線をGizmosで出す")]
    [SerializeField] private bool _showLines = true;

    private readonly List<CpuRaceInput> _cpus = new List<CpuRaceInput>();

    private Camera _camera;
    private EViewMode _viewMode;
    private int _selectedIndex;
    private float _refreshTimer;
    private GUIStyle _panelStyle;

    private void Start()
    {
        CreateCamera();
        RefreshCpus();
        SetViewMode(_startViewMode);
    }

    private void OnDestroy()
    {
        if (CpuLabelDrawer.ViewerCamera == _camera)
        {
            CpuLabelDrawer.ViewerCamera = null;
            CpuLabelDrawer.IsViewerFullScreen = false;
        }
    }

    private void Update()
    {
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            RefreshCpus();
        }

        HandleKeys();
    }

    // CPUが動いた後に、カメラを合わせる。
    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void HandleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (WasPressed(keyboard, _viewModeKey))
        {
            SetViewMode((EViewMode)(((int)_viewMode + 1) % 3));
        }

        if (WasPressed(keyboard, _nextKey))
        {
            Select(_selectedIndex + 1);
        }

        if (WasPressed(keyboard, _previousKey))
        {
            Select(_selectedIndex - 1);
        }

        if (WasPressed(keyboard, _panelKey))
        {
            _showPanel = !_showPanel;
        }
    }

    private static bool WasPressed(Keyboard keyboard, Key key)
    {
        return key != Key.None && keyboard[key].wasPressedThisFrame;
    }

    private void Select(int index)
    {
        if (_cpus.Count == 0)
        {
            return;
        }

        _selectedIndex = (index % _cpus.Count + _cpus.Count) % _cpus.Count;
    }

    // シーンのCPUを名前順に集め直す（途中で増えた・消えたときのため）。
    private void RefreshCpus()
    {
        _refreshTimer = RefreshInterval;
        CpuRaceInput selected = _selectedIndex < _cpus.Count ? _cpus[_selectedIndex] : null;

        _cpus.Clear();
        _cpus.AddRange(FindObjectsByType<CpuRaceInput>(FindObjectsSortMode.None));
        _cpus.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        int index = selected != null ? _cpus.IndexOf(selected) : -1;
        _selectedIndex = index >= 0 ? index : 0;
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("CpuDebugViewerCamera");
        cameraObject.transform.SetParent(transform, false);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.fieldOfView = _fieldOfView;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 5000f;

        // Playerのカメラより後に描いて、上に重ねる。
        Camera main = Camera.main;
        _camera.depth = (main != null ? main.depth : 0f) + 10f;
    }

    private void SetViewMode(EViewMode mode)
    {
        _viewMode = mode;
        _camera.enabled = mode != EViewMode.Off;
        _camera.rect = mode == EViewMode.FullScreen ? new Rect(0f, 0f, 1f, 1f) : _windowRect;
        CpuLabelDrawer.ViewerCamera = _camera;
        CpuLabelDrawer.IsViewerFullScreen = mode == EViewMode.FullScreen;
    }

    // 位置は毎フレームCPUの後ろにぴったり合わせる（遅らせると、高速なので離れていく）。向きだけ少しなめらかにする。
    private void UpdateCamera()
    {
        CpuRaceInput cpu = GetSelected();
        if (!_camera.enabled || cpu == null)
        {
            return;
        }

        Transform target = cpu.transform;
        if (!cpu.TryGetCourseFrame(out Vector3 forward, out Vector3 up))
        {
            forward = target.forward;
            up = target.up;
        }

        _camera.transform.position = target.position - forward * _followDistance + up * _followHeight;
        Vector3 lookDirection = target.position + forward * _lookAhead - _camera.transform.position;
        Quaternion goal = Quaternion.LookRotation(lookDirection, up);
        float blend = 1f - Mathf.Exp(-RotationSharpness * Time.unscaledDeltaTime);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, goal, blend);
    }

    // 選択していなくても、全CPUの線を出す。
    private void OnDrawGizmos()
    {
        if (!_showLines || !Application.isPlaying)
        {
            return;
        }

        foreach (CpuRaceInput cpu in _cpus)
        {
            if (cpu == null || !cpu.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 position = cpu.transform.position;
            Transform aimTarget = cpu.AimTargetTransform;
            if (aimTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(position, aimTarget.position);
            }

            Transform fireTarget = cpu.RecentFireTargetTransform;
            if (fireTarget != null)
            {
                Vector3 lift = cpu.transform.up * 0.5f;
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(position + lift, fireTarget.position + lift);
            }
        }
    }

    private CpuRaceInput GetSelected()
    {
        return _selectedIndex < _cpus.Count ? _cpus[_selectedIndex] : null;
    }

    private void OnGUI()
    {
        if (!_showPanel)
        {
            return;
        }

        _panelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = _panelFontSize,
            fontStyle = FontStyle.Bold,
            richText = true,
            wordWrap = false
        };

        GUILayout.BeginArea(new Rect(10f, 10f, Screen.width * 0.58f, Screen.height - 20f));
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"CPUデバッグ　{_viewModeKey}：カメラ（{GetViewModeName()}）　{_nextKey}/{_previousKey}：CPUを切り替え　{_panelKey}：一覧", _panelStyle);

        for (int i = 0; i < _cpus.Count; i++)
        {
            CpuRaceInput cpu = _cpus[i];
            if (cpu == null)
            {
                continue;
            }

            string color = ColorUtility.ToHtmlStringRGB(cpu.LabelColor);
            string marker = i == _selectedIndex && _viewMode != EViewMode.Off ? "▶" : "　";
            string text = cpu.DebugText.Replace("\n", "　|　");
            GUILayout.Label($"{marker}<color=#{color}>{cpu.name}</color>　{text}", _panelStyle);
        }

        if (_cpus.Count == 0)
        {
            GUILayout.Label("CpuRaceInput が付いたCPUがシーンにありません", _panelStyle);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private string GetViewModeName()
    {
        switch (_viewMode)
        {
            case EViewMode.Window:
                return "小窓";
            case EViewMode.FullScreen:
                return "全画面";
            default:
                return "切";
        }
    }
}
