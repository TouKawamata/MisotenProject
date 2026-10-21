using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Player のゴールを検知して着陸・順位配置・周回撮影 → ボタン → トランジション。
/// Shader Graph の _Transition を 1 → 0 に変更する。変化の速さは Inspector の AnimationCurve で調整。
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class RaceGoalDirection : MonoBehaviour
{
    private static readonly int TransitionPropertyId = Shader.PropertyToID("_Transition");

    [Header("レース参照")]
    [SerializeField] private RaceManager raceManager;

    [Header("トランジション")]
    [Tooltip("トランジションにかける実時間（秒）。Time.timeScale の影響を受けません")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;
    [Tooltip("既存のフェード用 CanvasGroup。トランジション前は非表示にします")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [Tooltip("_Transition を持つマテリアルを設定した UI の Image / RawImage。未設定なら Fade Canvas Group 配下から探します")]
    [SerializeField] private Graphic transitionGraphic;
    [Tooltip("横軸 = 経過時間（0～1）、縦軸 = Shader Graph の Transition 値（1 → 0）")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("シーン")]
    [SerializeField] private string resultSceneName = "ResultScene";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private bool playing;
    private Material originalGraphicMaterial;
    private Material runtimeTransitionMaterial;
    private bool continueRequested;
    public bool IsWaitingForContinue { get; private set; }

    private void Awake()
    {
        // レース開始時に画面を覆わないようにする。
        HideTransition();
        PrepareTransitionMaterial();
    }

    private void OnEnable()
    {
        if (raceManager == null)
            raceManager = FindFirstObjectByType<RaceManager>();
        if (raceManager != null)
        {
            raceManager.RacerFinished += OnRacerFinished;
            Log("RacerFinishedイベントへの登録成功。Playerのゴールを待機中。");
        }
        else
        {
            Debug.LogWarning("[RaceGoalDirection] RaceManagerが未設定です。イベントを受信できません。", this);
        }
    }

    private void OnRacerFinished(IReadOnlyRacerData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[RaceGoalDirection] RacerFinishedを受信しましたが、dataがnullです。", this);
            return;
        }

        Log($"RacerFinished受信: {data.DisplayName} / IsPlayer={data.IsPlayer} / 順位={data.CurrentRank} / タイム={data.FinishTime:F2}秒");
        if (!data.IsPlayer)
        {
            Log("CPUのゴールなので演出を開始しません。");
            return;
        }

        Log("Playerのゴールを確認。ゴール演出を開始します。");
        PlayGoal();
    }

    /// <summary>Player のゴール演出を開始。外部からの動作テストにも使用可能。</summary>
    [ContextMenu("Play Goal (Play Mode)")]
    public void PlayGoal()
    {
        if (!Application.isPlaying) return;
        if (playing)
        {
            Log("演出はすでに実行中のため、重複起動を無視しました。");
            return;
        }

        playing = true;
        StartCoroutine(PlayGoalRoutine());
    }

    private IEnumerator PlayGoalRoutine()
    {
        continueRequested = false;
        yield return PlayPresentation(raceManager != null ? raceManager.PlayerData : null, raceManager);
        if (!IsHolding)
        {
            playing = false;
            yield break;
        }
        IsWaitingForContinue = true;
        ShowContinue();
        yield return new WaitUntil(() => continueRequested);
        IsWaitingForContinue = false;
        HideContinue();

        // シーン設定ミスで画面を塞いだままにしない。ボタンで再試行できる。
        while (!Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            Debug.LogError($"[RaceGoalDirection] シーン '{resultSceneName}' をBuild ProfilesのScene Listに追加してください。", this);
            continueRequested = false;
            IsWaitingForContinue = true;
            ShowContinue();
            yield return new WaitUntil(() => continueRequested);
            IsWaitingForContinue = false;
            HideContinue();
        }

        bool useShader = PrepareTransitionMaterial();
        if (!useShader)
        {
            Debug.LogWarning("[RaceGoalDirection] _Transitionを持つGraphicのマテリアルが見つかりません。CanvasGroupフェードで代用します。", this);
        }

        if (fadeCanvasGroup != null)
        {
            // Shader使用時はShader側で透過を制御するためCanvasGroupは常に1。
            // Shader未設定の場合のみCanvasGroupのAlphaを0→1に変化させる。
            fadeCanvasGroup.alpha = useShader ? 1f : 0f;
            fadeCanvasGroup.blocksRaycasts = true;
        }
        if (useShader)
        {
            SetTransition(1f);
        }

        Log($"トランジション開始: {fadeSeconds:F2}秒 ({(useShader ? "_Transition 1→0" : "CanvasGroup フェード")})");
        float elapsed = 0f;
        if (fadeSeconds > 0f)
        {
            while (elapsed < fadeSeconds)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / fadeSeconds);
                ApplyTransition(normalizedTime, useShader);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }
        ApplyTransition(1f, useShader);


        // TODO: ResultSceneへ結果を渡す。
        // Player順位: raceManager.PlayerData.CurrentRank
        // Playerタイム: raceManager.PlayerData.FinishTime
        // 全員の順位・タイム: raceManager.Racers
        if (Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            Log($"ResultSceneへの遷移開始: {resultSceneName}");
            SceneManager.LoadScene(resultSceneName);
        }
        else
        {
            Debug.LogError($"[RaceGoalDirection] シーン '{resultSceneName}' がBuild ProfilesのScene Listにありません。", this);
            playing = false;
            HideTransition();
        }
    }

    private void ApplyTransition(float normalizedTime, bool useShader)
    {
        // グラフの形状は自由に編集できるが、開始=1・終了=0は必ず保証する。
        float transitionValue;
        if (normalizedTime <= 0f)
            transitionValue = 1f;
        else if (normalizedTime >= 1f)
            transitionValue = 0f;
        else
            transitionValue = transitionCurve != null && transitionCurve.length > 0
                ? Mathf.Clamp01(transitionCurve.Evaluate(normalizedTime))
                : 1f - normalizedTime;

        if (useShader)
        {
            SetTransition(transitionValue);
        }
        else if (fadeCanvasGroup != null)
        {
            // Shaderが設定されていない場合は元のフェード方式で代用。
            fadeCanvasGroup.alpha = 1f - transitionValue;
        }
    }

    private void SetTransition(float value)
    {
        if (runtimeTransitionMaterial != null)
        {
            runtimeTransitionMaterial.SetFloat(TransitionPropertyId, value);
        }
    }

    private bool PrepareTransitionMaterial()
    {
        if (runtimeTransitionMaterial != null)
            return true;

        // InspectorでGraphicを指定していない場合、既存のCanvasGroup配下を自動検索。
        if (transitionGraphic == null && fadeCanvasGroup != null)
        {
            Graphic[] candidates = fadeCanvasGroup.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic candidate in candidates)
            {
                Material mat = candidate.material;
                if (mat != null && mat.HasProperty(TransitionPropertyId))
                {
                    transitionGraphic = candidate;
                    break;
                }
            }
        }

        if (transitionGraphic == null)
            return false;

        Material sourceMaterial = transitionGraphic.material;
        if (sourceMaterial == null || !sourceMaterial.HasProperty(TransitionPropertyId))
            return false;

        // 共通マテリアルを書き換えず、プレハブ専用の実行時インスタンスを作る。
        originalGraphicMaterial = sourceMaterial;
        runtimeTransitionMaterial = new Material(sourceMaterial)
        {
            name = sourceMaterial.name + " (RaceGoalDirection Runtime)"
        };
        runtimeTransitionMaterial.SetFloat(TransitionPropertyId, 1f);
        transitionGraphic.material = runtimeTransitionMaterial;
        Log("Shader Graphの_Transitionへの接続に成功。");
        return true;
    }

    private void HideTransition()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
        SetTransition(1f);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsWaitingForContinue = false;
        playing = false;
        ResetPresentation();
        HideTransition();
        if (raceManager != null)
        {
            raceManager.RacerFinished -= OnRacerFinished;
            Log("RacerFinishedイベントの登録解除。");
        }
    }

    private void OnDestroy()
    {
        if (runtimeTransitionMaterial != null)
        {
            if (transitionGraphic != null && transitionGraphic.material == runtimeTransitionMaterial)
                transitionGraphic.material = originalGraphicMaterial;
            Destroy(runtimeTransitionMaterial);
            runtimeTransitionMaterial = null;
        }
    }

    /// <summary>レースを再実行するときに呼び出す。</summary>
    public void ResetDirection()
    {
        StopAllCoroutines();
        playing = false;
        continueRequested = false;
        IsWaitingForContinue = false;
        ResetPresentation();
        HideTransition();
    }

    /// <summary>UI Button の OnClick からも呼び出せる。着陸・移動中の入力は無視。</summary>
    public void ContinueToNextScene()
    {
        if (IsWaitingForContinue) continueRequested = true;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[RaceGoalDirection] {message}", this);
    }

    [Header("参照（シーンに設定済み。未指定なら自動検索）")]
    [SerializeField] private Transform player;
    [Tooltip("壇上へ移動させるCPU本体をドラッグして登録。順位はRaceManagerの結果を使用")]
    [SerializeField] private Transform[] cpuRacers = new Transform[0];
    [SerializeField] private Camera gameplayCamera;
    [Tooltip("地面の接地点。プレイヤー原点までの高さはGround Clearanceで加算")]
    [SerializeField] private Transform landingPoint;
    [Tooltip("左から1位・2位・3位の接地点。4位以下は右側に追加配置")]
    [SerializeField] private Transform[] rankPoints = new Transform[3];

    [Header("着陸と移動（実時間の秒）")]
    [SerializeField, Min(0.1f)] private float landingSeconds = 3f;
    [SerializeField, Min(0f)] private float landingPause = 0.45f;
    [SerializeField, Min(0.1f)] private float moveSeconds = 2.4f;
    [SerializeField, Min(0f)] private float groundClearance = 1f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(1f)] private float groundSearchDistance = 500f;
    [SerializeField, Min(1f)] private float maxLandingTravel = 60f;

    [Header("周回カメラ")]
    [SerializeField, Min(2f)] private float orbitRadius = 11f;
    [SerializeField, Min(0.5f)] private float orbitHeight = 4f;
    [SerializeField] private float lookHeight = 0.7f;
    [SerializeField] private float orbitDegreesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float cameraBlendSeconds = 2f;
    [SerializeField, Range(20f, 90f)] private float cinematicFieldOfView = 55f;
    [SerializeField] private LayerMask cameraObstacleMask = ~0;

    [Header("次へ")]
    [Tooltip("未設定なら画面下にNEXTボタンを生成。Enter / Space / ゲームパッド南ボタンでも進む")]
    [SerializeField] private Button continueButton;

    public bool IsHolding { get; private set; }
    private bool active;
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private float cameraFov;
    private float age;
    private float orbitAngle;
    private bool continueVisible;
    private Rigidbody body;
    private bool bodyWasKinematic;
    private Vector3 bodyVelocity;
    private Vector3 bodyAngularVelocity;
    private readonly List<Collider> colliders = new List<Collider>();
    private readonly List<bool> colliderStates = new List<bool>();
    private GameObject generatedCanvas;
    private bool buttonWasActive;
    private bool buttonWasInteractable;
    private CameraController cameraController;
    private bool cameraControllerWasEnabled;
    private RaceManager presentationRace;
    private bool preview;
    private readonly List<CpuPresentation> cpuPresentations = new List<CpuPresentation>();

    private sealed class CpuPresentation
    {
        public Transform Target;
        public Vector3 Start, Control, Landed, Destination;
        public Quaternion StartRotation, Upright, FinalRotation;
        public float Elapsed;
        public bool Complete;
        public Collider[] Colliders;
        public bool[] ColliderStates;
        public Rigidbody Body;
        public bool WasKinematic;
        public Vector3 Velocity, AngularVelocity;
    }

    private IEnumerator PlayPresentation(IReadOnlyRacerData data, RaceManager race = null)
    {
        ResetPresentation();
        presentationRace = race;
        preview = data == null || !data.IsFinished;
        if (data != null) player = data.Transform;
        if (player == null)
        {
            PlayerManager manager = FindFirstObjectByType<PlayerManager>();
            if (manager != null) player = manager.transform;
        }
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (player == null || gameplayCamera == null)
        {
            Debug.LogError("[RaceGoalDirection] Player / Gameplay Camera が見つかりません。", this);
            yield break;
        }

        int rank = data != null ? Mathf.Max(1, data.CurrentRank) : 1;
        savedPosition = desiredPosition = player.position;
        savedRotation = desiredRotation = player.rotation;
        PlayerFlightController flight = player.GetComponent<PlayerFlightController>();
        Vector3 velocity = flight != null ? flight.Velocity : player.forward * 20f;
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Quaternion upright = Quaternion.LookRotation(forward, Vector3.up);
        CapturePhysics();

        cameraPosition = gameplayCamera.transform.position;
        cameraRotation = gameplayCamera.transform.rotation;
        cameraLocalPosition = gameplayCamera.transform.localPosition;
        cameraLocalRotation = gameplayCamera.transform.localRotation;
        cameraFov = gameplayCamera.fieldOfView;
        cameraController = gameplayCamera.GetComponent<CameraController>();
        if (cameraController != null)
        {
            cameraControllerWasEnabled = cameraController.enabled;
            cameraController.enabled = false;
        }
        Vector3 offset = cameraPosition - savedPosition;
        orbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        age = 0f;
        active = true;

        float duration = Mathf.Max(0.1f, landingSeconds);
        Vector3 drift = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up) * duration / 3f, maxLandingTravel);
        Vector3 ground = landingPoint != null
            ? landingPoint.position + landingPoint.right * (rank - 1) * 3f : FindGround(savedPosition + drift);
        Vector3 landed = ground + Vector3.up * groundClearance;
        // Cubic Bezier: 最初の接線はゴール時の速度。終点の2制御点を重ねて速度ゼロで接地。
        Vector3 tangent = Vector3.ClampMagnitude(velocity * duration / 3f, maxLandingTravel);
        Vector3 control = savedPosition + tangent;
        control.y = Mathf.Max(landed.y, control.y);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float u = Mathf.Clamp01(t / duration);
            float v = 1f - u;
            desiredPosition = v * v * v * savedPosition + 3f * v * v * u * control
                + (3f * v * u * u + u * u * u) * landed;
            desiredRotation = Quaternion.Slerp(savedRotation, upright, Smooth(u));
            yield return null;
        }
        desiredPosition = landed;
        desiredRotation = upright;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, landingPause));

        Transform rankPoint = rankPoints != null && rankPoints.Length > 0
            ? rankPoints[Mathf.Min(rank - 1, rankPoints.Length - 1)] : null;
        Vector3 right = upright * Vector3.right;
        Vector3 destination = rankPoint != null ? rankPoint.position + Vector3.up * groundClearance
            : landed + forward * 7f + right * (rank == 1 ? 0f : rank == 2 ? -4f : 4f + (rank - 3) * 4f);
        if (rank > 3 && rankPoint != null) destination += rankPoint.right * (rank - 3) * 4f;
        Quaternion finalRotation = rankPoint != null ? rankPoint.rotation : upright;
        duration = Mathf.Max(0.1f, moveSeconds);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float u = Smooth(Mathf.Clamp01(t / duration));
            desiredPosition = Vector3.Lerp(landed, destination, u);
            desiredRotation = Quaternion.Slerp(upright, finalRotation, u);
            yield return null;
        }
        desiredPosition = destination;
        desiredRotation = finalRotation;
        // CPUの遅着やRaceFinished時点での未ゴールCPUの配置も待つ。
        while (!AreCpusOnPodium()) yield return null;
        IsHolding = true;
    }

    private void LateUpdate()
    {
        if (!active || player == null || gameplayCamera == null) return;
        // RaceManagerはMonoBehaviour.enabledにかかわらずIRacer.Tickを呼ぶため、最後に描画位置を確定する。
        player.SetPositionAndRotation(desiredPosition, desiredRotation);
        UpdateCpuPresentations();
        age += Time.unscaledDeltaTime;
        orbitAngle += orbitDegreesPerSecond * Time.unscaledDeltaTime;
        float angle = orbitAngle * Mathf.Deg2Rad;
        Vector3 focus = desiredPosition + Vector3.up * lookHeight;
        Vector3 orbit = desiredPosition + new Vector3(Mathf.Sin(angle) * orbitRadius, orbitHeight, Mathf.Cos(angle) * orbitRadius);
        Vector3 ray = orbit - focus;
        if (Physics.SphereCast(focus, 0.25f, ray.normalized, out RaycastHit hit, ray.magnitude,
            cameraObstacleMask, QueryTriggerInteraction.Ignore))
            orbit = focus + ray.normalized * Mathf.Max(0.5f, hit.distance - 0.3f);
        float blend = Smooth(Mathf.Clamp01(age / Mathf.Max(0.1f, cameraBlendSeconds)));
        Vector3 position = Vector3.Lerp(cameraPosition, orbit, blend);
        Vector3 look = focus - position;
        Quaternion rotation = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look, Vector3.up) : cameraRotation;
        gameplayCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(cameraRotation, rotation, blend));
        gameplayCamera.fieldOfView = Mathf.Lerp(cameraFov, cinematicFieldOfView, blend);
    }

    private void UpdateCpuPresentations()
    {
        if (presentationRace == null) return;
        foreach (IReadOnlyRacerData racer in presentationRace.Racers)
        {
            if (racer.IsPlayer || !IsSelectedCpu(racer.Transform) || HasCpu(racer.Transform)) continue;
            // 走行中のCPUはゴールするまで操作しない。制限時間でレースが終わったら現順位で配置。
            if (!racer.IsFinished && presentationRace.State != ERaceState.Finished && !preview) continue;
            cpuPresentations.Add(CaptureCpu(racer));
        }
        float landingDuration = Mathf.Max(0.1f, landingSeconds);
        float pause = Mathf.Max(0f, landingPause);
        float moveDuration = Mathf.Max(0.1f, moveSeconds);
        foreach (CpuPresentation cpu in cpuPresentations)
        {
            if (cpu.Target == null) { cpu.Complete = true; continue; }
            cpu.Elapsed += Time.unscaledDeltaTime;
            Vector3 position;
            Quaternion rotation;
            if (cpu.Elapsed < landingDuration)
            {
                float u = Mathf.Clamp01(cpu.Elapsed / landingDuration);
                float v = 1f - u;
                position = v * v * v * cpu.Start + 3f * v * v * u * cpu.Control
                    + (3f * v * u * u + u * u * u) * cpu.Landed;
                rotation = Quaternion.Slerp(cpu.StartRotation, cpu.Upright, Smooth(u));
            }
            else
            {
                float u = Mathf.Clamp01((cpu.Elapsed - landingDuration - pause) / moveDuration);
                position = Vector3.Lerp(cpu.Landed, cpu.Destination, Smooth(u));
                rotation = Quaternion.Slerp(cpu.Upright, cpu.FinalRotation, Smooth(u));
                cpu.Complete = u >= 1f;
            }
            // DummyRacerもRaceManagerからTickされるので、壇上に着いた後も位置を保持する。
            cpu.Target.SetPositionAndRotation(position, rotation);
        }
    }

    private bool HasCpu(Transform target)
    {
        foreach (CpuPresentation cpu in cpuPresentations)
            if (cpu.Target == target) return true;
        return false;
    }

    private bool IsSelectedCpu(Transform target)
    {
        if (target == null || cpuRacers == null) return false;
        foreach (Transform cpu in cpuRacers)
            if (cpu == target) return true;
        return false;
    }

    private bool AreCpusOnPodium()
    {
        if (presentationRace == null) return true;
        foreach (IReadOnlyRacerData racer in presentationRace.Racers)
            if (!racer.IsPlayer && IsSelectedCpu(racer.Transform) && !HasCpu(racer.Transform)) return false;
        foreach (CpuPresentation cpu in cpuPresentations)
            if (!cpu.Complete) return false;
        return true;
    }

    private CpuPresentation CaptureCpu(IReadOnlyRacerData racer)
    {
        Transform target = racer.Transform;
        int rank = Mathf.Max(1, racer.CurrentRank);
        var cpu = new CpuPresentation { Target = target, Start = target.position, StartRotation = target.rotation };
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        cpu.Upright = Quaternion.LookRotation(forward, Vector3.up);
        DummyRacer dummy = target.GetComponent<DummyRacer>();
        Vector3 velocity = dummy != null ? dummy.Velocity : forward * 20f;
        float duration = Mathf.Max(0.1f, landingSeconds);
        Vector3 drift = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up) * duration / 3f, maxLandingTravel);
        // 着陸待機場所も順位ごとにずらして重なりを防ぐ。
        Vector3 ground = landingPoint != null
            ? landingPoint.position + landingPoint.right * (rank - 1) * 3f : FindGround(cpu.Start + drift);
        cpu.Landed = ground + Vector3.up * groundClearance;
        cpu.Control = cpu.Start + Vector3.ClampMagnitude(velocity * duration / 3f, maxLandingTravel);
        cpu.Control.y = Mathf.Max(cpu.Landed.y, cpu.Control.y);
        Transform point = rankPoints != null && rankPoints.Length > 0
            ? rankPoints[Mathf.Min(rank - 1, rankPoints.Length - 1)] : null;
        cpu.Destination = point != null ? point.position + Vector3.up * groundClearance
            : cpu.Landed + forward * 7f + cpu.Upright * Vector3.right * (rank == 1 ? 0f : rank == 2 ? -4f : 4f + (rank - 3) * 4f);
        if (rank > 3 && point != null) cpu.Destination += point.right * (rank - 3) * 4f;
        cpu.FinalRotation = point != null ? point.rotation : cpu.Upright;
        cpu.Colliders = target.GetComponentsInChildren<Collider>(true);
        cpu.ColliderStates = new bool[cpu.Colliders.Length];
        for (int i = 0; i < cpu.Colliders.Length; i++)
        {
            cpu.ColliderStates[i] = cpu.Colliders[i].enabled;
            cpu.Colliders[i].enabled = false;
        }
        cpu.Body = target.GetComponent<Rigidbody>();
        if (cpu.Body != null)
        {
            cpu.WasKinematic = cpu.Body.isKinematic;
            cpu.Velocity = cpu.Body.linearVelocity;
            cpu.AngularVelocity = cpu.Body.angularVelocity;
            cpu.Body.isKinematic = true;
        }
        return cpu;
    }

    private Vector3 FindGround(Vector3 from)
    {
        RaycastHit[] hits = Physics.RaycastAll(from + Vector3.up * 5f, Vector3.down,
            groundSearchDistance, groundMask, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 result = from;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<IRacer>() != null || hit.normal.y < 0.5f) continue;
            if (hit.distance < nearest) { nearest = hit.distance; result = hit.point; }
        }
        if (float.IsPositiveInfinity(nearest))
            Debug.LogWarning("[RaceGoalDirection] 地面が見つかりません。Landing Pointを指定してください。", this);
        return result;
    }

    private void CapturePhysics()
    {
        colliders.Clear();
        colliderStates.Clear();
        foreach (Collider collider in player.GetComponentsInChildren<Collider>(true))
        {
            colliders.Add(collider);
            colliderStates.Add(collider.enabled);
            collider.enabled = false;
        }
        body = player.GetComponent<Rigidbody>();
        if (body == null) return;
        bodyWasKinematic = body.isKinematic;
        bodyVelocity = body.linearVelocity;
        bodyAngularVelocity = body.angularVelocity;
        body.isKinematic = true;
    }

    private void ShowContinue()
    {
        continueVisible = true;
        if (continueButton == null) CreateContinueButton();
        if (generatedCanvas != null) generatedCanvas.SetActive(true);
        buttonWasActive = continueButton.gameObject.activeSelf;
        buttonWasInteractable = continueButton.interactable;
        continueButton.gameObject.SetActive(true);
        continueButton.interactable = true;
        continueButton.onClick.AddListener(Continue);
        // Input SystemのEventSystemが既にある場合はそのまま使用する。
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    private void Update()
    {
        if (!continueVisible || !IsWaitingForContinue) return;
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonSouth.wasPressedThisFrame)) Continue();
    }

    private void Continue() { ContinueToNextScene(); }

    public void HideContinue()
    {
        if (continueButton != null && continueVisible)
        {
            continueButton.onClick.RemoveListener(Continue);
            continueButton.gameObject.SetActive(buttonWasActive);
            continueButton.interactable = buttonWasInteractable;
        }
        if (generatedCanvas != null) generatedCanvas.SetActive(false);
        continueVisible = false;
    }

    private void CreateContinueButton()
    {
        generatedCanvas = new GameObject("Goal Continue UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        generatedCanvas.transform.SetParent(transform, false);
        Canvas canvas = generatedCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = generatedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        GameObject buttonObject = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(generatedCanvas.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.1f);
        rect.sizeDelta = new Vector2(420f, 76f);
        buttonObject.GetComponent<Image>().color = new Color(0.035f, 0.09f, 0.16f, 0.94f);
        continueButton = buttonObject.GetComponent<Button>();
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        Text text = labelObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "NEXT   /   ENTER";
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        // EventSystemがない単独テストでもマウスボタンを使用できる。
        if (EventSystem.current == null)
        {
            GameObject events = new GameObject("Goal EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            events.transform.SetParent(generatedCanvas.transform, false);
        }
        buttonObject.SetActive(false);
    }

    public void ResetPresentation()
    {
        HideContinue();
        IsHolding = false;
        if (!active) return;
        active = false;
        foreach (CpuPresentation cpu in cpuPresentations)
        {
            if (cpu.Target != null) cpu.Target.SetPositionAndRotation(cpu.Start, cpu.StartRotation);
            for (int i = 0; i < cpu.Colliders.Length; i++)
                if (cpu.Colliders[i] != null) cpu.Colliders[i].enabled = cpu.ColliderStates[i];
            if (cpu.Body == null) continue;
            cpu.Body.isKinematic = cpu.WasKinematic;
            if (!cpu.WasKinematic)
            {
                cpu.Body.linearVelocity = cpu.Velocity;
                cpu.Body.angularVelocity = cpu.AngularVelocity;
            }
        }
        cpuPresentations.Clear();
        presentationRace = null;
        if (player != null) player.SetPositionAndRotation(savedPosition, savedRotation);
        for (int i = 0; i < colliders.Count; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        colliders.Clear();
        colliderStates.Clear();
        if (body != null)
        {
            body.isKinematic = bodyWasKinematic;
            if (!bodyWasKinematic) { body.linearVelocity = bodyVelocity; body.angularVelocity = bodyAngularVelocity; }
        }
        if (gameplayCamera != null)
        {
            gameplayCamera.transform.localPosition = cameraLocalPosition;
            gameplayCamera.transform.localRotation = cameraLocalRotation;
            gameplayCamera.fieldOfView = cameraFov;
        }
        if (cameraController != null) cameraController.enabled = cameraControllerWasEnabled;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (landingPoint != null) Gizmos.DrawWireSphere(landingPoint.position, 1f);
        if (rankPoints == null) return;
        for (int i = 0; i < rankPoints.Length; i++)
        {
            if (rankPoints[i] == null) continue;
            Gizmos.color = i == 0 ? Color.yellow : i == 1 ? Color.white : new Color(0.8f, 0.45f, 0.2f);
            Gizmos.DrawWireSphere(rankPoints[i].position, 1f);
            Gizmos.DrawRay(rankPoints[i].position, rankPoints[i].forward * 3f);
        }
    }
}
