using UnityEngine.InputSystem;

// デバッグ用のキーボード入力。新Input System（Keyboard.current）のみを使用する。
// このプロジェクトはActive Input HandlingがNew Input System専用のため、UnityEngine.Inputは使用不可。
public class KeyboardRaceInput : IRaceInput
{
    public float Horizontal
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                value -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                value += 1f;
            }

            return value;
        }
    }

    public float Vertical
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                value -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                value += 1f;
            }

            return value;
        }
    }

    public bool BoostPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

    public bool ShieldPressed => Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;

    public bool MainActionPressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
}
