using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheDawn
{
    public enum GameKey
    {
        W, A, S, D, Up, Down, Left, Right, Q, E, R, F, Escape, Space, Alpha1, Alpha2, Alpha3, Alpha4
    }

    /// <summary>
    /// Works with either the legacy Input Manager or the new Input System package,
    /// whichever the project has active.
    /// </summary>
    public static class InputShim
    {
#if ENABLE_INPUT_SYSTEM
        static Key ToKey(GameKey k)
        {
            switch (k)
            {
                case GameKey.W: return Key.W;
                case GameKey.A: return Key.A;
                case GameKey.S: return Key.S;
                case GameKey.D: return Key.D;
                case GameKey.Up: return Key.UpArrow;
                case GameKey.Down: return Key.DownArrow;
                case GameKey.Left: return Key.LeftArrow;
                case GameKey.Right: return Key.RightArrow;
                case GameKey.Q: return Key.Q;
                case GameKey.E: return Key.E;
                case GameKey.R: return Key.R;
                case GameKey.F: return Key.F;
                case GameKey.Escape: return Key.Escape;
                case GameKey.Space: return Key.Space;
                case GameKey.Alpha1: return Key.Digit1;
                case GameKey.Alpha2: return Key.Digit2;
                case GameKey.Alpha3: return Key.Digit3;
                default: return Key.Digit4;
            }
        }

        public static bool Held(GameKey k) { return Keyboard.current != null && Keyboard.current[ToKey(k)].isPressed; }
        public static bool Down(GameKey k) { return Keyboard.current != null && Keyboard.current[ToKey(k)].wasPressedThisFrame; }
        public static Vector2 MousePosition { get { return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero; } }
        public static float Scroll { get { return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f; } }

        static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
        {
            if (Mouse.current == null) return null;
            if (b == 0) return Mouse.current.leftButton;
            if (b == 1) return Mouse.current.rightButton;
            return Mouse.current.middleButton;
        }
        public static bool MouseDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
        public static bool MouseHeld(int b) { var c = Btn(b); return c != null && c.isPressed; }
#else
        static KeyCode ToKey(GameKey k)
        {
            switch (k)
            {
                case GameKey.W: return KeyCode.W;
                case GameKey.A: return KeyCode.A;
                case GameKey.S: return KeyCode.S;
                case GameKey.D: return KeyCode.D;
                case GameKey.Up: return KeyCode.UpArrow;
                case GameKey.Down: return KeyCode.DownArrow;
                case GameKey.Left: return KeyCode.LeftArrow;
                case GameKey.Right: return KeyCode.RightArrow;
                case GameKey.Q: return KeyCode.Q;
                case GameKey.E: return KeyCode.E;
                case GameKey.R: return KeyCode.R;
                case GameKey.F: return KeyCode.F;
                case GameKey.Escape: return KeyCode.Escape;
                case GameKey.Space: return KeyCode.Space;
                case GameKey.Alpha1: return KeyCode.Alpha1;
                case GameKey.Alpha2: return KeyCode.Alpha2;
                case GameKey.Alpha3: return KeyCode.Alpha3;
                default: return KeyCode.Alpha4;
            }
        }

        public static bool Held(GameKey k) { return Input.GetKey(ToKey(k)); }
        public static bool Down(GameKey k) { return Input.GetKeyDown(ToKey(k)); }
        public static Vector2 MousePosition { get { return Input.mousePosition; } }
        public static float Scroll { get { return Input.mouseScrollDelta.y; } }
        public static bool MouseDown(int b) { return Input.GetMouseButtonDown(b); }
        public static bool MouseHeld(int b) { return Input.GetMouseButton(b); }
#endif
    }
}
