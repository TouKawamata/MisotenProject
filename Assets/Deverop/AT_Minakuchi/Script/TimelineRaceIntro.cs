using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// RaceManager の IRaceIntro 用スタート演出。
/// Player の降下は Timeline で再生し、カメラの移動はこのスクリプトで自動制御する。
/// RaceManager / PlayerManager / CameraController の変更は不要。
/// </summary>
public sealed class TimelineRaceIntro : MonoBehaviour, IRaceIntro
{
    // 既存の Timeline の Player 用 Animation Track 名に合わせる。
    private const string PlayerTrackName = "Animation Track";

    [Header("必須参照")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private Animator playerModelAnimator;
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("カメラ演出（設定しなくても初期値で動く）")]
    [Tooltip("Playerから見た開始時のカメラ相対位置。X=右、Y=上、Z=前")]
    [SerializeField] private Vector3 openingOffset = new Vector3(8f, 6f, 14f);
    [Tooltip("Playerの横を回り込む途中のカメラ相対位置")]
    [SerializeField] private Vector3 sideOffset = new Vector3(8f, 4f, -3f);
    [Tooltip("Player後方でのカメラ相対位置")]
    [SerializeField] private Vector3 behindOffset = new Vector3(0f, 3.5f, -10f);
    [Tooltip("カメラがPlayerのどこを見るか（上方向のオフセット）")]
    [SerializeField] private float lookHeight = 0.5f;

    [Header("スキップ")]
    [SerializeField] private bool skipWithSpace = true;

    private bool isPlaying;
    private bool finished;
    private bool skipRequested;

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();
    }

    private void Update()
    {
        if (isPlaying && skipWithSpace &&
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Skip();
        }
    }

    private void LateUpdate()
    {
        // Timeline が終了したあとに、初期時刻へ戻ったカメラ位置で上書きしない。
        if (isPlaying && !finished && !skipRequested)
            UpdateCinematicCamera();
    }

    // UIボタンからも呼び出せる。
    public void Skip()
    {
        if (isPlaying)
            skipRequested = true;
    }

    public async UniTask PlayAsync(CancellationToken token)
    {
        if (isPlaying)
            return;

        if (director == null || director.playableAsset == null || playerModelAnimator == null ||
            introCamera == null || gameplayCamera == null || introCamera == gameplayCamera)
        {
            Debug.LogError("[TimelineRaceIntro] 必須参照（Director、Player Model Animator、Intro Camera、Gameplay Camera）を確認してください。", this);
            return;
        }

        token.ThrowIfCancellationRequested();
        BindPlayerTrack();

        isPlaying = true;
        finished = false;
        skipRequested = false;
        director.stopped += OnDirectorStopped;

        // Timeline に直接カメラアニメーションを入れなくても動作する。
        director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

        try
        {
            director.time = 0;
            director.Play();
            director.Evaluate();

            // 最初のフレームからカメラ位置を合わせる。
            UpdateCinematicCamera();
            SetIntroCameraActive(true);

            // Timeline が自然終了するか、スキップされるまで待つ。
            await UniTask.WaitUntil(
                () => finished || skipRequested,
                cancellationToken: token);
        }
        finally
        {
            director.stopped -= OnDirectorStopped;

            // スキップ時も降下アニメの最後の姿勢に移動させてから止める。
            if (skipRequested && director.playableAsset != null)
            {
                double endTime = director.duration;
                if (!double.IsNaN(endTime) && !double.IsInfinity(endTime) && endTime > 0)
                {
                    director.time = endTime;
                    director.Evaluate();
                }
            }

            // 最重要：Timeline を Stop する前にカメラを切り替える。
            // Stop により Animation の値や再生位置がリセットされても、演出カメラを映さない。
            isPlaying = false;
            SetIntroCameraActive(false);
            director.Stop();

            // 今の降下Animationは、終了時のlocalPositionが(0, 0, 0)。
            // スキップでも必ずスタート位置に戻して、レース側の座標に影響させない。
            playerModelAnimator.transform.localPosition = Vector3.zero;
        }
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director || !isPlaying)
            return;

        // 自然終了の場合、WaitUntil / finally の実行を待つと
        // Timeline のリセットされたカメラ位置が一瞬映る可能性がある。
        // stopped イベントの時点で表示カメラを切り替え、以後 LateUpdate で更新しない。
        finished = true;
        isPlaying = false;
        SetIntroCameraActive(false);
    }

    private void BindPlayerTrack()
    {
        if (!(director.playableAsset is TimelineAsset timeline))
            return;

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is AnimationTrack && track.name == PlayerTrackName)
            {
                director.SetGenericBinding(track, playerModelAnimator);
                return;
            }
        }

        Debug.LogWarning("[TimelineRaceIntro] Player用Animation Trackが見つかりません。トラック名を確認してください。", this);
    }

    private void UpdateCinematicCamera()
    {
        if (director == null || introCamera == null || gameplayCamera == null || playerModelAnimator == null)
            return;

        double duration = director.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
            duration = 5.0;

        float t = Mathf.Clamp01((float)(director.time / duration));

        // Animator のモデルが降下している位置を、毎フレームそのまま追いかける。
        Transform model = playerModelAnimator.transform;
        Vector3 modelPosition = model.position;

        // モデルの前・右を基準にするので、Playerの配置場所や向きが変わっても追える。
        Vector3 right = model.right;
        Vector3 forward = model.forward;

        // 前斜め上 -> 横を回り込む -> 後方 -> 通常ゲームカメラへ合流
        Vector3 offset;
        if (t < 0.55f)
        {
            float u = Ease(t / 0.55f);
            offset = Vector3.LerpUnclamped(openingOffset, sideOffset, u);
        }
        else
        {
            float u = Ease((t - 0.55f) / 0.25f);
            offset = Vector3.LerpUnclamped(sideOffset, behindOffset, u);
        }

        Vector3 cameraPosition = modelPosition + right * offset.x + Vector3.up * offset.y + forward * offset.z;
        Vector3 lookTarget = modelPosition + Vector3.up * lookHeight;
        Vector3 lookDirection = lookTarget - cameraPosition;
        Quaternion cameraRotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection, Vector3.up)
            : gameplayCamera.transform.rotation;

        // 終盤20%で既存のゲームカメラに位置・角度を完全一致させる。
        // カメラを切り替える瞬間の視点の飛びを防ぐ。
        float blendToGameplay = Ease(Mathf.InverseLerp(0.8f, 1f, t));
        introCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(cameraPosition, gameplayCamera.transform.position, blendToGameplay),
            Quaternion.Slerp(cameraRotation, gameplayCamera.transform.rotation, blendToGameplay));

        // FOVも合わせる（カメラ切替時の画角の飛びを抑える）。
        introCamera.fieldOfView = Mathf.Lerp(introCamera.fieldOfView, gameplayCamera.fieldOfView, blendToGameplay);
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void SetIntroCameraActive(bool active)
    {
        // 同時に2つのAudioListenerが有効になるのを避ける。
        AudioListener introListener = introCamera.GetComponent<AudioListener>();
        AudioListener gameplayListener = gameplayCamera.GetComponent<AudioListener>();

        // 切替前に旧カメラをOFFにする。
        if (active)
        {
            gameplayCamera.enabled = false;
            if (gameplayListener != null) gameplayListener.enabled = false;
            introCamera.enabled = true;
            if (introListener != null) introListener.enabled = true;
        }
        else
        {
            introCamera.enabled = false;
            if (introListener != null) introListener.enabled = false;
            gameplayCamera.enabled = true;
            if (gameplayListener != null) gameplayListener.enabled = true;
        }
    }
}
