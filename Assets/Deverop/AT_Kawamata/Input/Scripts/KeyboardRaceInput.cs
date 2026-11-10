using UnityEngine;
using UnityEngine.InputSystem;

// デバッグ用のキーボード入力。新Input System（Keyboard.current）のみを使用する。
// このプロジェクトはActive Input HandlingがNew Input System専用のため、UnityEngine.Inputは使用不可。
// 移動：A/D・←/→で左右、W/S・↑/↓で上下。ブースト：Space、シールド：左Shift。
public class KeyboardRaceInput : IRaceInput
{
    public Vector2 Move
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 value = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                value.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                value.x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                value.y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                value.y += 1f;
            }

            // 斜め（例：(1, 1)）でも大きさが1を超えないようにする（IRaceInput.Moveの約束）。
            return Vector2.ClampMagnitude(value, 1f);
        }
    }

    public bool BoostPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

    public bool BoostHeld => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

    public bool ShieldPressed => Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;

    public bool ShieldHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
}
