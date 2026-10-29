using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TriToneTest : MonoBehaviour
{
    [SerializeField]
    private TriToneEffect _triToneEffect;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("TriTone Play");
            _triToneEffect.Play();
        }

        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            Debug.Log("TriTone Stop");
            _triToneEffect.Stop();
        }
    }
}