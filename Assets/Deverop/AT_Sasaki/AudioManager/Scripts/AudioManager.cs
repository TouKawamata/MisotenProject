using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("オーディオ設定")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("マスター音量設定 (0.0 ～ 1.0)")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float bgmVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float seVolume = 1f;

    [Header("サウンドリスト")]
    [SerializeField] private List<SoundData> bgmList = new List<SoundData>();
    [SerializeField] private List<SoundData> seList = new List<SoundData>();

    private AudioSource bgmSource;
    private AudioSource seSource;

    private Dictionary<string, SoundData> bgmDict = new Dictionary<string, SoundData>();
    private Dictionary<string, SoundData> seDict = new Dictionary<string, SoundData>();

    private Coroutine bgmFadeCoroutine;
    private string currentBgmName = "";

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        // 自分自身にAudioSourceを直接2つ追加して使う（確実な方法）
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2)
        {
            bgmSource = sources[0];
            seSource = sources[1];
        }
        else
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            seSource = gameObject.AddComponent<AudioSource>();
        }

        // 2Dサウンドとして設定を確定させる
        bgmSource.spatialBlend = 0f;
        seSource.spatialBlend = 0f;

        if (audioMixer != null)
        {
            AudioMixerGroup[] bgmGroups = audioMixer.FindMatchingGroups("BGM");
            if (bgmGroups.Length > 0) bgmSource.outputAudioMixerGroup = bgmGroups[0];

            AudioMixerGroup[] seGroups = audioMixer.FindMatchingGroups("SE");
            if (seGroups.Length > 0) seSource.outputAudioMixerGroup = seGroups[0];
        }

        foreach (var bgm in bgmList)
        {
            if (!string.IsNullOrEmpty(bgm.soundName) && !bgmDict.ContainsKey(bgm.soundName))
                bgmDict.Add(bgm.soundName, bgm);
        }

        foreach (var se in seList)
        {
            if (!string.IsNullOrEmpty(se.soundName) && !seDict.ContainsKey(se.soundName))
                seDict.Add(se.soundName, se);
        }
    }

    #region BGM Control

    public void PlayBGM(string name, float fadeDuration = 0.5f)
    {
        Debug.Log($"[AudioManager] BGM再生試行: 探している名前 = '{name}'");

        // 1. 辞書に名前が存在するかチェック
        if (!bgmDict.ContainsKey(name))
        {
            Debug.LogError($"[エラー] BGMの登録名が見つかりません！ 探した名前: '{name}'");

            // 現在Bgm Listに登録されている名前の一覧をログに出す
            foreach (var key in bgmDict.Keys)
            {
                Debug.Log($" └ 現在登録されているBGM名: '{key}'");
            }
            return;
        }

        SoundData data = bgmDict[name];

        // 2. Clipがセットされているかチェック
        if (data.clip == null)
        {
            Debug.LogError($"[エラー] BGM '{name}' に Audio Clip がセットされていません！ Inspectorの Bgm List を確認してください。");
            return;
        }

        // 3. 既に同じ曲が鳴っているかチェック
        if (bgmSource.clip == data.clip && bgmSource.isPlaying)
        {
            Debug.Log("[AudioManager] 既に同じBGMが再生中です。");
            return;
        }

        currentBgmName = name;

        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(ChangeBGMCoroutine(data, fadeDuration));

        Debug.Log($"★【成功】 BGM '{name}' の再生（フェードコルーチン）を開始しました！");
    }
    public void StopBGM(float fadeDuration = 0.5f)
    {
        currentBgmName = "";
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeOutBGMCoroutine(fadeDuration));
    }

    public void PauseBGM() => bgmSource.Pause();
    public void UnPauseBGM() => bgmSource.UnPause();

    private IEnumerator ChangeBGMCoroutine(SoundData data, float fadeDuration)
    {
        // 最終的な到達音量を計算
        float soundDataVol = data.volume <= 0f ? 1f : data.volume;
        float targetVol = soundDataVol * bgmVolume * masterVolume;

        // 1. 既に別のBGMが再生中なら、まずフェードアウトする
        if (bgmSource.isPlaying && fadeDuration > 0f)
        {
            float startVol = bgmSource.volume;
            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / fadeDuration);
                yield return null;
            }
            bgmSource.Stop();
        }

        // 2. 新しいBGMをセット
        bgmSource.clip = data.clip;
        bgmSource.loop = data.loop;
        bgmSource.pitch = data.pitch <= 0f ? 1f : data.pitch;

        // フェードインなし（即時再生）の場合
        if (fadeDuration <= 0f)
        {
            bgmSource.volume = targetVol;
            bgmSource.Play();
            yield break;
        }

        // 3. フェードイン再生（音量0からスタートして目標値へ）
        bgmSource.volume = 0f;
        bgmSource.Play();

        float fadeInTimer = 0f;
        while (fadeInTimer < fadeDuration)
        {
            fadeInTimer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVol, fadeInTimer / fadeDuration);
            yield return null;
        }

        // 最後に確実に目標音量に固定
        bgmSource.volume = targetVol;
    }
    private IEnumerator FadeOutBGMCoroutine(float fadeDuration)
    {
        if (fadeDuration > 0)
        {
            float startVol = bgmSource.volume;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVol, 0, t / fadeDuration);
                yield return null;
            }
        }
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    #endregion

    #region SE Control

    public void PlaySE(string name)
    {
        Debug.Log($"[AudioManager] PlaySE実行試行: 探している名前 = '{name}'");

        // 辞書に名前が存在するかチェック
        if (!seDict.ContainsKey(name))
        {
            Debug.LogError($"[エラー] 登録名が見つかりません！ 探した名前: '{name}'");

            // 現在登録されている名前の一覧をログに出す（デバッグ用）
            foreach (var key in seDict.Keys)
            {
                Debug.Log($" └ 現在登録されている名前: '{key}'");
            }
            return;
        }

        SoundData data = seDict[name];

        // Clipがセットされているかチェック
        if (data.clip == null)
        {
            Debug.LogError($"[エラー] '{name}' に Audio Clip がセットされていません！ Inspectorで音声ファイルをドラッグ＆ドロップしてください。");
            return;
        }

        // 音声再生
        seSource.pitch = 1f;
        seSource.volume = 1f;
        seSource.PlayOneShot(data.clip, 1f);

        Debug.Log("★【成功】 PlayOneShotが正常に実行されました！音が出ているか確認してください。");
    }

    public void StopAllSE() => seSource.Stop();

    #endregion

    #region Volume Settings

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (!string.IsNullOrEmpty(currentBgmName) && bgmDict.TryGetValue(currentBgmName, out SoundData data))
        {
            bgmSource.volume = data.volume * bgmVolume * masterVolume;
        }
    }

    public void SetSEVolume(float volume) => seVolume = Mathf.Clamp01(volume);

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        SetBGMVolume(bgmVolume);
    }

    #endregion
}