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

        const string GrappleButtonPref = "Televised.Prototyping.GrappleButton";
        static GrappleMouseButton? s_grappleButton;

        /// <summary>
        /// Which mouse button fires the grapple (the other one cancels). Default: right. Changed from the debug
        /// panel and remembered between sessions (PlayerPrefs).
        /// </summary>
        public static GrappleMouseButton GrappleButton
        {
            get
            {
                s_grappleButton ??= (GrappleMouseButton)PlayerPrefs.GetInt(GrappleButtonPref, (int)GrappleMouseButton.Right);
                return s_grappleButton.Value;
            }
            set
            {
                if (s_grappleButton == value) return;
                s_grappleButton = value;
                PlayerPrefs.SetInt(GrappleButtonPref, (int)value);
            }
        }

        /// <summary>Human-readable names for hints, e.g. "Right-click" / "right mouse".</summary>
        public static string GrappleClickName => GrappleButton == GrappleMouseButton.Right ? "Right-click" : "Left-click";
        public static string GrappleCancelClickName => GrappleButton == GrappleMouseButton.Right ? "Left-click" : "Right-click";
        public static string GrappleButtonName => GrappleButton == GrappleMouseButton.Right ? "right mouse" : "left mouse";

        /// <summary>Replaces {grappleClick}, {cancelClick} and {grappleButton} in hint text with the current binding.</summary>
        public static string FormatBindings(string text) => string.IsNullOrEmpty(text) ? text : text
            .Replace("{grappleClick}", GrappleClickName)
            .Replace("{cancelClick}", GrappleCancelClickName)
            .Replace("{grappleButton}", GrappleButtonName);

        static UnityEngine.InputSystem.Controls.ButtonControl MouseButton(Mouse m, bool grapple) =>
            (GrappleButton == GrappleMouseButton.Right) == grapple ? m.rightButton : m.leftButton;

        public static bool GrapplePressed
        {
            get { var m = Mouse.current; return m != null && !PointerBlocked && MouseButton(m, true).wasPressedThisFrame; }
        }

        public static bool GrappleHeld
        {
            get { var m = Mouse.current; return m != null && MouseButton(m, true).isPressed; }
        }

        public static bool GrappleCancelPressed
        {
            get { var m = Mouse.current; return m != null && !PointerBlocked && MouseButton(m, false).wasPressedThisFrame; }
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
