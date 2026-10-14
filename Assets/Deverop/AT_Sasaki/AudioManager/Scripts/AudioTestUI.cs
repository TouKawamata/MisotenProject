using UnityEngine;
using UnityEngine.InputSystem;

// AudioManager の動作確認用 UI（テストシーン専用）
public class AudioTestUI : MonoBehaviour
{
    [Header("テスト用サウンド名（AudioClip のファイル名）")]
    [SerializeField] private string m_bgmName1 = "maou_bgm_cyber43";
    [SerializeField] private string m_bgmName2 = "maou_bgm_8bit26";
    [SerializeField] private string m_seName1 = "ショット";
    [SerializeField] private string m_seName2 = "ショット命中";

    [SerializeField, Min(0f)] private float m_fadeDuration = 1f;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || AudioManager.Instance == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) AudioManager.Instance.PlayBGM(m_bgmName1, m_fadeDuration);
        if (keyboard.digit2Key.wasPressedThisFrame) AudioManager.Instance.StopBGM(m_fadeDuration);
        if (keyboard.digit3Key.wasPressedThisFrame) AudioManager.Instance.PlayBGM(m_bgmName2, m_fadeDuration);
        if (keyboard.spaceKey.wasPressedThisFrame) AudioManager.Instance.PlaySE(m_seName1);
        if (keyboard.digit4Key.wasPressedThisFrame) AudioManager.Instance.PlaySE(m_seName2);
    }

    private void OnGUI()
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 320, 520));
        GUILayout.Box("=== Audio Manager Test ===");
        GUILayout.Label($"現在の BGM: {audioManager.CurrentBgmName}");

        if (GUILayout.Button($"[1] BGM① 再生 ({m_bgmName1})", GUILayout.Height(30))) audioManager.PlayBGM(m_bgmName1, m_fadeDuration);
        if (GUILayout.Button($"[3] BGM② 再生 ({m_bgmName2})", GUILayout.Height(30))) audioManager.PlayBGM(m_bgmName2, m_fadeDuration);
        if (GUILayout.Button("[2] BGM 停止", GUILayout.Height(30))) audioManager.StopBGM(m_fadeDuration);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("BGM 一時停止", GUILayout.Height(30))) audioManager.PauseBGM();
        if (GUILayout.Button("BGM 再開", GUILayout.Height(30))) audioManager.ResumeBGM();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button($"[Space] SE① 再生 ({m_seName1})", GUILayout.Height(30))) audioManager.PlaySE(m_seName1);
        if (GUILayout.Button($"[4] SE② 再生 ({m_seName2})", GUILayout.Height(30))) audioManager.PlaySE(m_seName2);

        GUILayout.Space(10);

        GUILayout.Label($"Master: {audioManager.MasterVolume:F2}");
        audioManager.SetMasterVolume(GUILayout.HorizontalSlider(audioManager.MasterVolume, 0f, 1f));
        GUILayout.Label($"BGM: {audioManager.BgmVolume:F2}");
        audioManager.SetBGMVolume(GUILayout.HorizontalSlider(audioManager.BgmVolume, 0f, 1f));
        GUILayout.Label($"SE: {audioManager.SeVolume:F2}");
        audioManager.SetSEVolume(GUILayout.HorizontalSlider(audioManager.SeVolume, 0f, 1f));

        GUILayout.EndArea();
    }
}
