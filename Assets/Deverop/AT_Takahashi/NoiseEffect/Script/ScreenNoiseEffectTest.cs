using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ScreenNoiseEffectControllerの動作確認用。
///
/// Pキー : 選択中プリセットをPlay
/// Oキー : 再生中プリセットをStop
/// </summary>
public sealed class ScreenNoiseEffectTest : MonoBehaviour
{
    [SerializeField]
    private ScreenNoiseEffectController _controller;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // 選択中のプリセットを再生
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (_controller == null)
            {
                Debug.LogWarning(
                    "ScreenNoiseEffectTest: Controllerが未設定です。",
                    this
                );

                return;
            }

            Debug.Log(
                $"Screen Noise Play: {_controller.SelectedPresetName}",
                this
            );

            _controller.Play();
        }

        // 再生中のプリセットを停止
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            if (_controller == null)
            {
                Debug.LogWarning(
                    "ScreenNoiseEffectTest: Controllerが未設定です。",
                    this
                );

                return;
            }

            Debug.Log(
                $"Screen Noise Stop: {_controller.PlayingPresetName}",
                this
            );

            _controller.Stop();
        }
    }
}