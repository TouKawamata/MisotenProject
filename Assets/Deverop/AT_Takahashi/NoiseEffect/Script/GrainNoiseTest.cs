using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GrainNoiseTest : MonoBehaviour
{
    [SerializeField]
    private GrainNoiseEffect _grainNoiseEffect;

    private void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            _grainNoiseEffect.Play();
        }

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            _grainNoiseEffect.Stop();
        }
    }
}