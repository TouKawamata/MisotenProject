using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RaceManager.RacerFinishedを購読し、Playerゴール時に演出を再生する。
/// </summary>
public sealed class RaceGoalDirection : MonoBehaviour
{
    [Header("レース参照")]
    [SerializeField] private RaceManager raceManager;

    [Header("演出")]
    [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.2f;
    [SerializeField, Min(0f)] private float slowMotionSeconds = 1.2f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("シーン")]
    [SerializeField] private string resultSceneName = "ResultScene";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[RaceGoalDirection] {message}", this);
    }

    private float originalTimeScale;
    private float originalFixedDeltaTime;
    private bool playing;
    private bool timeModified;

    private void Awake()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
    }

    private void OnEnable()
    {
        if (raceManager != null)
        {
            raceManager.RacerFinished += OnRacerFinished;
            Log("RacerFinishedイベントへの登録成功。Playerのゴールを待機中。");
        }
        else
            Debug.LogWarning("[RaceGoalDirection] RaceManagerが未設定です。イベントを受信できません。", this);
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

    /// <summary>Playerゴール時の演出開始。テスト時は外部から呼び出してもよい。</summary>
    public void PlayGoal()
    {
        if (playing)
        {
            Log("演出はすでに実行中のため、重複起動を無視しました。");
            return;
        }
        playing = true;
        Log("ゴール演出コルーチン開始。");
        StartCoroutine(PlayGoalRoutine());
    }

    private IEnumerator PlayGoalRoutine()
    {
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        timeModified = true;

        Log($"スローモーション開始: timeScale={slowMotionScale}, 継続={slowMotionSeconds:F2}秒");
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * slowMotionScale;

        // timeScale の影響を受けない実時間で待機
        yield return new WaitForSecondsRealtime(slowMotionSeconds);

        if (fadeCanvasGroup != null)
        {
            Log($"フェードアウト開始: {fadeSeconds:F2}秒");
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        else
        {
            Debug.LogWarning("[RaceGoalDirection] フェードCanvasGroupが未設定です。フェードせずに遷移します。", this);
        }

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
        }
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
    private void OnDestroy() => RestoreTime();

    private void RestoreTime()
    {
        if (!timeModified) return;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        timeModified = false;
    }

    // レースを再実行する場合の初期化用
    public void ResetDirection()
    {
        StopAllCoroutines();
        RestoreTime();
        playing = false;
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;
    }
}
