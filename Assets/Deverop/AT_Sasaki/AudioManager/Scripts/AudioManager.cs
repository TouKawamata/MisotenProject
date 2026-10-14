using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// BGM と SE を分けて管理するサウンドマネージャー。
// 音は AudioClip のファイル名で指定する（例: PlayBGM("maou_bgm_8bit26")）。
// ・BGM : 同時に鳴るのは 1 曲だけ。切り替え時はクロスフェードする。
// ・SE  : 何個でも同時に鳴らせる（フェードなし）。AudioSource は使い回す。
// 最終的な音量は「個別音量 × BGM（SE）音量 × マスター音量」。
// 個別音量は呼び出し時の volume 引数を優先し、省略時は Inspector で音声ごとに設定した値を使う。
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Inspector で登録する音声 1 件分
    [System.Serializable]
    private class SoundEntry
    {
        [SerializeField] private AudioClip m_clip;
        [SerializeField, Range(0f, 1f)] private float m_volume = 1f;

        // Inspector でリストに追加した要素は初期値が使われず m_volume が 0 になるため、
        // 未初期化の要素を見分けて 1 にするためのフラグ
        [SerializeField, HideInInspector] private bool m_isInitialized = true;

        public AudioClip Clip => m_clip;
        public float Volume => m_volume;

        public void InitializeIfNeeded()
        {
            if (m_isInitialized)
            {
                return;
            }

            m_volume = 1f;
            m_isInitialized = true;
        }
    }

    [Header("Sounds（音声ファイルと個別音量）")]
    [SerializeField] private List<SoundEntry> m_bgmSounds = new List<SoundEntry>();
    [SerializeField] private List<SoundEntry> m_seSounds = new List<SoundEntry>();

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float m_masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float m_bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float m_seVolume = 1f;

    [Header("Audio Mixer（任意）")]
    [SerializeField] private AudioMixerGroup m_bgmMixerGroup;
    [SerializeField] private AudioMixerGroup m_seMixerGroup;

    [Header("3D Audio Settings")]
    [SerializeField, Min(0f)] private float m_maxDistance = 100f;
    [SerializeField, Min(0f)] private float m_minDistance = 1f;

    // BGM はクロスフェード用に 2 つの AudioSource を交互に使う
    private const int BgmSourceCount = 2;

    private readonly Dictionary<string, SoundEntry> m_bgmDictionary = new Dictionary<string, SoundEntry>();
    private readonly Dictionary<string, SoundEntry> m_seDictionary = new Dictionary<string, SoundEntry>();

    private readonly AudioSource[] m_bgmSources = new AudioSource[BgmSourceCount];
    // 呼び出し時に指定された個別音量（null = Inspector の値を使う）
    private readonly float?[] m_bgmVolumeOverrides = new float?[BgmSourceCount];
    private readonly float[] m_bgmFadeVolumes = new float[BgmSourceCount];
    private readonly Coroutine[] m_bgmFadeCoroutines = new Coroutine[BgmSourceCount];

    // 現在メインで鳴っている BGM の AudioSource 番号（-1 = 停止中）
    private int m_currentBgmIndex = -1;
    private bool m_isBgmPaused;

    private readonly List<SeSlot> m_seSlots = new List<SeSlot>();

    public float MasterVolume => m_masterVolume;
    public float BgmVolume => m_bgmVolume;
    public float SeVolume => m_seVolume;

    // 現在の BGM 名（停止中は空文字）
    public string CurrentBgmName =>
        IsCurrentBgmActive()
            ? m_bgmSources[m_currentBgmIndex].clip.name
            : string.Empty;

    // SE 用 AudioSource の使い回し情報
    private class SeSlot
    {
        public AudioSource Source;
        // 呼び出し時に指定された個別音量（null = Inspector の値を使う）
        public float? VolumeOverride;
        // 再生開始予定時刻（unscaledTime）。遅延再生中に空き扱いにしないために使う
        public float StartTime;
        public bool InUse;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.parent = null;
        DontDestroyOnLoad(gameObject);

        InitializeSounds(m_bgmSounds, m_bgmDictionary, "BGM", true);
        InitializeSounds(m_seSounds, m_seDictionary, "SE", true);
        CreateBgmSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // インスペクターで値を変更した際の補正と、再生中の音量への反映
    private void OnValidate()
    {
        m_minDistance = Mathf.Max(0f, m_minDistance);
        m_maxDistance = Mathf.Max(m_minDistance, m_maxDistance);

        foreach (SoundEntry entry in m_bgmSounds)
        {
            entry.InitializeIfNeeded();
        }

        foreach (SoundEntry entry in m_seSounds)
        {
            entry.InitializeIfNeeded();
        }

        // 再生中に Inspector で個別音量などを変えた場合も、鳴っている音にすぐ反映する
        if (Application.isPlaying && Instance == this)
        {
            InitializeSounds(m_bgmSounds, m_bgmDictionary, "BGM", false);
            InitializeSounds(m_seSounds, m_seDictionary, "SE", false);
            ApplyAllVolumes();
        }
    }

    #region BGM

    // BGM を再生する。別の BGM が鳴っていればクロスフェードで切り替える。
    // volume を省略すると Inspector で設定した個別音量を使う
    public bool PlayBGM(string clipName, float fadeDuration = 1f, float? volume = null, bool loop = true)
    {
        if (!TryGetSound(m_bgmDictionary, clipName, "BGM", out SoundEntry entry))
        {
            return false;
        }

        AudioClip clip = entry.Clip;

        // 一時停止中に呼ばれた場合は再開してから切り替える
        if (m_isBgmPaused)
        {
            ResumeBGM();
        }

        // 同じ曲がすでにメインで鳴っている場合は何もしない
        if (IsCurrentBgmActive() && m_bgmSources[m_currentBgmIndex].clip == clip)
        {
            return true;
        }

        // 同じ曲がフェードアウト中なら、その AudioSource を再利用してフェードインし直す
        int nextIndex = FindBgmSourceIndex(clip);

        if (nextIndex < 0)
        {
            nextIndex = SelectNextBgmIndex();

            AudioSource source = m_bgmSources[nextIndex];
            StopBgmFade(nextIndex);
            source.Stop();
            source.clip = clip;
            m_bgmFadeVolumes[nextIndex] = 0f;
        }

        AudioSource nextSource = m_bgmSources[nextIndex];
        nextSource.loop = loop;
        m_bgmVolumeOverrides[nextIndex] = volume;

        if (!nextSource.isPlaying)
        {
            nextSource.Play();
        }

        // 今までの BGM はフェードアウトして停止
        if (m_currentBgmIndex >= 0)
        {
            StartBgmFade(m_currentBgmIndex, 0f, fadeDuration);
        }

        m_currentBgmIndex = nextIndex;
        StartBgmFade(nextIndex, 1f, fadeDuration);

        return true;
    }

    // BGM をフェードアウトして停止する（fadeDuration = 0 で即停止）
    public void StopBGM(float fadeDuration = 1f)
    {
        if (m_currentBgmIndex < 0)
        {
            return;
        }

        StartBgmFade(m_currentBgmIndex, 0f, fadeDuration);
        m_currentBgmIndex = -1;
    }

    public void PauseBGM()
    {
        m_isBgmPaused = true;

        foreach (AudioSource source in m_bgmSources)
        {
            source.Pause();
        }
    }

    public void ResumeBGM()
    {
        m_isBgmPaused = false;

        foreach (AudioSource source in m_bgmSources)
        {
            source.UnPause();
        }
    }

    // メインの BGM が再生中（一時停止中を含む）か
    private bool IsCurrentBgmActive()
    {
        if (m_currentBgmIndex < 0)
        {
            return false;
        }

        AudioSource source = m_bgmSources[m_currentBgmIndex];
        return source.clip != null && (source.isPlaying || m_isBgmPaused);
    }

    private void CreateBgmSources()
    {
        for (int i = 0; i < BgmSourceCount; i++)
        {
            AudioSource source = CreateAudioSource($"AudioSource_BGM_{i}", m_bgmMixerGroup);
            source.spatialBlend = 0f;
            m_bgmSources[i] = source;
        }
    }

    private int FindBgmSourceIndex(AudioClip clip)
    {
        for (int i = 0; i < BgmSourceCount; i++)
        {
            if (m_bgmSources[i].clip == clip && m_bgmSources[i].isPlaying)
            {
                return i;
            }
        }

        return -1;
    }

    // 次の BGM に使う AudioSource を選ぶ。鳴っていないものを優先する
    private int SelectNextBgmIndex()
    {
        for (int i = 0; i < BgmSourceCount; i++)
        {
            if (i != m_currentBgmIndex && !m_bgmSources[i].isPlaying)
            {
                return i;
            }
        }

        return m_currentBgmIndex == 0 ? 1 : 0;
    }

    private void StartBgmFade(int index, float targetVolume, float fadeDuration)
    {
        StopBgmFade(index);
        m_bgmFadeCoroutines[index] = StartCoroutine(BgmFadeCoroutine(index, targetVolume, fadeDuration));
    }

    private void StopBgmFade(int index)
    {
        if (m_bgmFadeCoroutines[index] != null)
        {
            StopCoroutine(m_bgmFadeCoroutines[index]);
            m_bgmFadeCoroutines[index] = null;
        }
    }

    // 現在のフェード値から targetVolume まで変化させる。0 になったら停止する。
    // ポーズ中（timeScale = 0）でも進むように unscaledDeltaTime を使う
    private IEnumerator BgmFadeCoroutine(int index, float targetVolume, float fadeDuration)
    {
        float startVolume = m_bgmFadeVolumes[index];
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            m_bgmFadeVolumes[index] = Mathf.Lerp(startVolume, targetVolume, elapsedTime / fadeDuration);
            ApplyBgmVolume(index);
            yield return null;
        }

        m_bgmFadeVolumes[index] = targetVolume;
        ApplyBgmVolume(index);

        if (targetVolume <= 0f)
        {
            m_bgmSources[index].Stop();
            m_bgmSources[index].clip = null;
        }

        m_bgmFadeCoroutines[index] = null;
    }

    private void ApplyBgmVolume(int index)
    {
        AudioSource source = m_bgmSources[index];
        float baseVolume = ResolveVolume(m_bgmDictionary, source.clip, m_bgmVolumeOverrides[index]);
        source.volume = baseVolume * m_bgmFadeVolumes[index] * m_bgmVolume * m_masterVolume;
    }

    #endregion

    #region SE

    // 2D の SE を再生する。volume を省略すると Inspector で設定した個別音量を使う
    public bool PlaySE(string clipName, bool loop = false, float? volume = null, float pitch = 1f, float delay = 0f)
    {
        return PlaySEInternal(clipName, null, loop, volume, pitch, delay);
    }

    // 指定位置で 3D の SE を再生する。volume を省略すると Inspector で設定した個別音量を使う
    public bool PlaySE3D(string clipName, Vector3 worldPosition, bool loop = false, float? volume = null, float pitch = 1f, float delay = 0f)
    {
        return PlaySEInternal(clipName, worldPosition, loop, volume, pitch, delay);
    }

    // 指定した SE をすべて停止する（ループ SE の停止などに使う）
    public void StopSE(string clipName)
    {
        foreach (SeSlot slot in m_seSlots)
        {
            if (IsSlotPlaying(slot) && slot.Source.clip != null && slot.Source.clip.name == clipName)
            {
                ReleaseSlot(slot);
            }
        }
    }

    public void StopAllSE()
    {
        foreach (SeSlot slot in m_seSlots)
        {
            if (IsSlotPlaying(slot))
            {
                ReleaseSlot(slot);
            }
        }
    }

    private bool PlaySEInternal(string clipName, Vector3? worldPosition, bool loop, float? volume, float pitch, float delay)
    {
        if (!TryGetSound(m_seDictionary, clipName, "SE", out SoundEntry entry))
        {
            return false;
        }

        // 負の遅延は即時再生扱いにする
        float playDelay = Mathf.Max(0f, delay);

        SeSlot slot = AcquireSlot();
        slot.VolumeOverride = volume;
        slot.StartTime = Time.unscaledTime + playDelay;

        AudioSource source = slot.Source;
        source.clip = entry.Clip;
        source.loop = loop;
        source.pitch = pitch;

        if (worldPosition.HasValue)
        {
            source.transform.position = worldPosition.Value;
            source.spatialBlend = 1f;
            source.minDistance = m_minDistance;
            source.maxDistance = m_maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        ApplySeVolume(slot);

        if (playDelay > 0f)
        {
            source.PlayDelayed(playDelay);
        }
        else
        {
            source.Play();
        }

        return true;
    }

    // 空いている AudioSource を探し、無ければ新しく作る
    private SeSlot AcquireSlot()
    {
        foreach (SeSlot slot in m_seSlots)
        {
            if (!IsSlotPlaying(slot))
            {
                slot.InUse = true;
                return slot;
            }
        }

        SeSlot newSlot = new SeSlot
        {
            Source = CreateAudioSource($"AudioSource_SE_{m_seSlots.Count}", m_seMixerGroup),
            InUse = true,
        };
        m_seSlots.Add(newSlot);

        return newSlot;
    }

    // 再生中、または遅延再生の待機中なら true
    private bool IsSlotPlaying(SeSlot slot)
    {
        if (!slot.InUse)
        {
            return false;
        }

        if (slot.Source.isPlaying || Time.unscaledTime < slot.StartTime)
        {
            return true;
        }

        // 再生が終わっていれば空きに戻す
        slot.InUse = false;
        slot.Source.clip = null;
        return false;
    }

    private void ReleaseSlot(SeSlot slot)
    {
        slot.Source.Stop();
        slot.Source.clip = null;
        slot.InUse = false;
    }

    private void ApplySeVolume(SeSlot slot)
    {
        float baseVolume = ResolveVolume(m_seDictionary, slot.Source.clip, slot.VolumeOverride);
        slot.Source.volume = baseVolume * m_seVolume * m_masterVolume;
    }

    #endregion

    #region Volume

    public void SetMasterVolume(float volume)
    {
        m_masterVolume = Mathf.Clamp01(volume);
        ApplyAllVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        m_bgmVolume = Mathf.Clamp01(volume);
        ApplyAllVolumes();
    }

    public void SetSEVolume(float volume)
    {
        m_seVolume = Mathf.Clamp01(volume);
        ApplyAllVolumes();
    }

    // 再生中の音すべてに現在の音量設定を反映する
    private void ApplyAllVolumes()
    {
        for (int i = 0; i < BgmSourceCount; i++)
        {
            if (m_bgmSources[i] != null)
            {
                ApplyBgmVolume(i);
            }
        }

        foreach (SeSlot slot in m_seSlots)
        {
            if (slot.InUse)
            {
                ApplySeVolume(slot);
            }
        }
    }

    #endregion

    #region Common

    // 登録された音声をファイル名で引けるように辞書へ登録する
    private void InitializeSounds(List<SoundEntry> sounds, Dictionary<string, SoundEntry> dictionary, string label, bool logWarnings)
    {
        dictionary.Clear();

        foreach (SoundEntry entry in sounds)
        {
            if (entry == null || entry.Clip == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"[AudioManager] {label} に AudioClip が未設定の要素があります。");
                }
                continue;
            }

            if (!dictionary.TryAdd(entry.Clip.name, entry) && logWarnings)
            {
                Debug.LogWarning($"[AudioManager] {label} の AudioClip '{entry.Clip.name}' が重複しています。");
            }
        }
    }

    private bool TryGetSound(Dictionary<string, SoundEntry> dictionary, string clipName, string label, out SoundEntry entry)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            Debug.LogError($"[AudioManager] {label} の clipName が空です。");
            entry = null;
            return false;
        }

        if (dictionary.TryGetValue(clipName, out entry))
        {
            return true;
        }

        Debug.LogError($"[AudioManager] {label} の AudioClip '{clipName}' が登録されていません。");
        return false;
    }

    // 個別音量を決める。呼び出し時の指定があればそれを、無ければ Inspector の値を使う
    private float ResolveVolume(Dictionary<string, SoundEntry> dictionary, AudioClip clip, float? volumeOverride)
    {
        if (volumeOverride.HasValue)
        {
            return Mathf.Clamp01(volumeOverride.Value);
        }

        if (clip != null && dictionary.TryGetValue(clip.name, out SoundEntry entry))
        {
            return entry.Volume;
        }

        return 1f;
    }

    private AudioSource CreateAudioSource(string objectName, AudioMixerGroup mixerGroup)
    {
        GameObject audioObject = new GameObject(objectName);
        audioObject.transform.SetParent(transform);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = mixerGroup;

        return source;
    }

    #endregion
}
