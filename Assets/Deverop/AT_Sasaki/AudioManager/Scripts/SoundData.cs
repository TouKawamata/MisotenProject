using UnityEngine;

[System.Serializable]
public class SoundData
{
    [Header("サウンド設定")]
    public string soundName;        // 再生時に指定する名前
    public AudioClip clip;         // 音声ファイル

    [Range(0f, 1f)]
    public float volume = 1f;      // 個別音量
    [Range(0.5f, 1.5f)]
    public float pitch = 1f;       // ピッチ
    public bool loop;              // ループ再生するか（BGMはtrue）
}