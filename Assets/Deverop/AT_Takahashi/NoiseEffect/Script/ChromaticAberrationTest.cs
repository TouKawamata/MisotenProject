using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ChromaticAberrationTest : MonoBehaviour
{
    [SerializeField]
    private ChromaticAberrationEffect _effect;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log("Chromatic Aberration Play");
            _effect.Play();
        }

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            Debug.Log("Chromatic Aberration Stop");
            _effect.Stop();
        }
    }
}