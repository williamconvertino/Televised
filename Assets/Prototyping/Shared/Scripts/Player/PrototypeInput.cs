using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Thin wrapper over the Input System so every prototype script reads input the same way
    /// and can be rebound in one place.
    /// </summary>
    public static class PrototypeInput
    {
        public static bool LeftHeld => Held(Key.A) || Held(Key.LeftArrow);
        public static bool RightHeld => Held(Key.D) || Held(Key.RightArrow);
        public static bool UpHeld => Held(Key.W) || Held(Key.UpArrow);
        public static bool DownHeld => Held(Key.S) || Held(Key.DownArrow);
        public static bool JumpPressed => Pressed(Key.Space);

        /// <summary>
        /// Screen-space movement intent from WASD / arrows. Opposite keys cancel
        /// (A+D = no horizontal, W+S = no vertical). Not normalized: diagonals are (±1, ±1).
        /// </summary>
        public static Vector2 MoveVector => new Vector2(
            (RightHeld ? 1f : 0f) - (LeftHeld ? 1f : 0f),
            (UpHeld ? 1f : 0f) - (DownHeld ? 1f : 0f));

        /// <summary>True on the frame any movement key goes down (used to re-evaluate locked directions).</summary>
        public static bool MoveKeyPressedThisFrame =>
            Pressed(Key.A) || Pressed(Key.D) || Pressed(Key.W) || Pressed(Key.S) ||
            Pressed(Key.LeftArrow) || Pressed(Key.RightArrow) || Pressed(Key.UpArrow) || Pressed(Key.DownArrow);

        /// <summary>
        /// Set by UI while a form/menu is open (e.g. the playtest rating screen) so typing a comment can't trigger
        /// gameplay hotkeys. Blocks all keyboard reads through this class, plus the scroll wheel.
        /// </summary>
        public static bool KeyboardBlocked { get; set; }

        public static bool Held(Key key)
        {
            var kb = Keyboard.current;
            return !KeyboardBlocked && kb != null && kb[key].isPressed;
        }

        public static bool Pressed(Key key)
        {
            var kb = Keyboard.current;
            return !KeyboardBlocked && kb != null && kb[key].wasPressedThisFrame;
        }

        /// <summary>Set by UI (the debug panel) while the pointer is over it, so clicks don't fire the grapple.</summary>
        public static bool PointerBlocked { get; set; }

        public static bool GrapplePressed
        {
            get { var m = Mouse.current; return m != null && !PointerBlocked && m.leftButton.wasPressedThisFrame; }
        }

        public static bool GrappleHeld
        {
            get { var m = Mouse.current; return m != null && m.leftButton.isPressed; }
        }

        public static bool GrappleCancelPressed
        {
            get { var m = Mouse.current; return m != null && !PointerBlocked && m.rightButton.wasPressedThisFrame; }
        }

        public static float ScrollDelta
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && !KeyboardBlocked ? mouse.scroll.ReadValue().y : 0f;
            }
        }

        public static Vector2 MouseScreen
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }

        public static Vector2 MouseWorld(Camera cam)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return Vector2.zero;
            Vector3 screen = MouseScreen;
            screen.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(screen);
        }
    }
}
