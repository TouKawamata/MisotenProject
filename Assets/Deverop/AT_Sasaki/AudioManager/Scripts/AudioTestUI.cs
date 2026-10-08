using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class AudioTestUI : MonoBehaviour
{
    [Header("ÉeÉXÉgópÉTÉEÉìÉhñº")]
    [SerializeField] private string bgmName1 = "TitleBGM";
    [SerializeField] private string bgmName2 = "StageBGM";
    [SerializeField] private string seName1 = "Click";
    [SerializeField] private string seName2 = "Boom";

    private void Update()
    {
        bool is1Pressed = false;
        bool is2Pressed = false;
        bool is3Pressed = false;
        bool is4Pressed = false;
        bool isSpacePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) is1Pressed = true;
            if (Keyboard.current.digit2Key.wasPressedThisFrame) is2Pressed = true;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) is3Pressed = true;
            if (Keyboard.current.digit4Key.wasPressedThisFrame) is4Pressed = true; // digit4KeyÇ…ïœçX
            if (Keyboard.current.spaceKey.wasPressedThisFrame) isSpacePressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1)) is1Pressed = true;
        if (Input.GetKeyDown(KeyCode.Alpha2)) is2Pressed = true;
        if (Input.GetKeyDown(KeyCode.Alpha3)) is3Pressed = true;
        if (Input.GetKeyDown(KeyCode.Alpha4)) is4Pressed = true; // Alpha4Ç…ïœçX
        if (Input.GetKeyDown(KeyCode.Space)) isSpacePressed = true;
#endif

        if (is1Pressed) PlayBGM(bgmName1);
        if (is2Pressed) StopBGM();
        if (is3Pressed) PlayBGM(bgmName2);

        // SEá@ÇÕ SpaceÉLÅ[ÅASEáAÇÕ 4ÉLÅ[ Ç…äÑÇËìñÇƒ
        if (isSpacePressed) PlaySE(seName1);
        if (is4Pressed) PlaySE(seName2);
    }

    private void PlayBGM(string name)
    {
        if (AudioManager.instance != null)
        {
            Debug.Log($"[Test] BGMçƒê∂: {name}");
            AudioManager.instance.PlayBGM(name, 1.0f);
        }
    }

    private void StopBGM()
    {
        if (AudioManager.instance != null)
        {
            Debug.Log("[Test] BGMí‚é~");
            AudioManager.instance.StopBGM(1.0f);
        }
    }

    // à¯êîÇ≈ñ¬ÇÁÇµÇΩÇ¢SEñºÇéÛÇØéÊÇÍÇÈÇÊÇ§Ç…ïœçX
    private void PlaySE(string name)
    {
        if (AudioManager.instance != null)
        {
            Debug.Log($"[Test] SEçƒê∂: {name}");
            AudioManager.instance.PlaySE(name);
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 320, 420));
        GUILayout.Box("=== Audio Manager Multi-Test ===");

        GUILayout.Label($"Åy 1 ÉLÅ[ Åz: BGMá@çƒê∂ ({bgmName1})");
        if (GUILayout.Button($"BGMá@çƒê∂ ({bgmName1})", GUILayout.Height(30)))
        {
            PlayBGM(bgmName1);
        }

        GUILayout.Space(5);

        GUILayout.Label($"Åy 3 ÉLÅ[ Åz: BGMáAçƒê∂ ({bgmName2})");
        if (GUILayout.Button($"BGMáAçƒê∂ ({bgmName2})", GUILayout.Height(30)))
        {
            PlayBGM(bgmName2);
        }

        GUILayout.Space(5);

        GUILayout.Label("Åy 2 ÉLÅ[ Åz: BGMí‚é~");
        if (GUILayout.Button("BGMí‚é~", GUILayout.Height(30)))
        {
            StopBGM();
        }

        GUILayout.Space(10);

        GUILayout.Label($"Åy Space ÉLÅ[ Åz: SEá@çƒê∂ ({seName1})");
        if (GUILayout.Button($"SEá@çƒê∂ ({seName1})", GUILayout.Height(35)))
        {
            PlaySE(seName1);
        }

        GUILayout.Space(5);

        GUILayout.Label($"Åy 4 ÉLÅ[ Åz: SEáAçƒê∂ ({seName2})");
        if (GUILayout.Button($"SEáAçƒê∂ ({seName2})", GUILayout.Height(35)))
        {
            PlaySE(seName2);
        }

        GUILayout.EndArea();
    }
}