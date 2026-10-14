using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Player のゴールを検知してスローモーション → Shader Graph トランジション → ResultScene。
/// Shader Graph の _Transition を 1 → 0 に変更する。変化の速さは Inspector の AnimationCurve で調整。
/// </summary>
public sealed class RaceGoalDirection : MonoBehaviour
{
    private static readonly int TransitionPropertyId = Shader.PropertyToID("_Transition");

    [Header("レース参照")]
    [SerializeField] private RaceManager raceManager;

    [Header("演出")]
    [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.2f;
    [SerializeField, Min(0f)] private float slowMotionSeconds = 1.2f;

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

    private float originalFixedDeltaTime;
    private bool playing;
    private bool timeModified;
    private Material originalGraphicMaterial;
    private Material runtimeTransitionMaterial;

    private void Awake()
    {
        // レース開始時に画面を覆わないようにする。
        HideTransition();
        PrepareTransitionMaterial();
    }

    private void OnEnable()
    {
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
    public void PlayGoal()
    {
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
        originalFixedDeltaTime = Time.fixedDeltaTime;
        timeModified = true;

        Log($"スローモーション開始: timeScale={slowMotionScale}, 継続={slowMotionSeconds:F2}秒");
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * slowMotionScale;

        yield return new WaitForSecondsRealtime(slowMotionSeconds);

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

        RestoreTime();

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
        if (raceManager != null)
        {
            raceManager.RacerFinished -= OnRacerFinished;
            Log("RacerFinishedイベントの登録解除。");
        }
        RestoreTime();
    }

    private void OnDestroy()
    {
        RestoreTime();
        if (runtimeTransitionMaterial != null)
        {
            if (transitionGraphic != null && transitionGraphic.material == runtimeTransitionMaterial)
                transitionGraphic.material = originalGraphicMaterial;
            Destroy(runtimeTransitionMaterial);
            runtimeTransitionMaterial = null;
        }
    }

    private void RestoreTime()
    {
        if (!timeModified)
            return;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        timeModified = false;
    }

    /// <summary>レースを再実行するときに呼び出す。</summary>
    public void ResetDirection()
    {
        StopAllCoroutines();
        RestoreTime();
        playing = false;
        HideTransition();
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[RaceGoalDirection] {message}", this);
    }
}
