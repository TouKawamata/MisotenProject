using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RGBBlockNoiseTest : MonoBehaviour
{
    [SerializeField]
    private RGBBlockNoiseEffect _effect;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            Debug.Log("RGB Block Noise Play");
            _effect.Play();
        }

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            Debug.Log("RGB Block Noise Stop");
            _effect.Stop();
        }

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            Debug.Log("RGB Block Noise Refresh");
            _effect.RefreshNoise();
        }
    }
}