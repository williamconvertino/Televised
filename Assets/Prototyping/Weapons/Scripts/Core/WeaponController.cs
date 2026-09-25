using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Weapons
{
    public enum FireBinding
    {
        /// <summary>Whichever mouse button the grapple isn't using (its cancel button; the grapple is released with Space).</summary>
        OtherMouseButton,
        LeftMouse,
        RightMouse,
        KeyE,
    }

    /// <summary>
    /// Selected weapon, switching ([ and ], or the panel's dropdown), fire input forwarding and clean cancellation.
    /// Aim is measured from the player root toward the cursor, independent of body rotation or the pupil visuals.
    /// </summary>
    [DefaultExecutionOrder(20)] // after the motor (-50), so the root position is this frame's
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        [SerializeField] ProjectileSystem projectiles;
        [Tooltip("Stable weapon order used by the dropdown and [ / ].")]
        [SerializeField] List<PrototypeWeapon> weapons = new List<PrototypeWeapon>();
        [SerializeField] int selectedIndex;

        const string FireBindingPref = "Televised.Prototyping.CombatFireBinding";
        const string SelectedPref = "Televised.Prototyping.CombatWeapon";

        public PlayerMotor2D Motor => motor;
        public ProjectileSystem Projectiles => projectiles;
        public IReadOnlyList<PrototypeWeapon> Weapons => weapons;
        public int SelectedIndex => selectedIndex;
        public PrototypeWeapon Current => selectedIndex >= 0 && selectedIndex < weapons.Count ? weapons[selectedIndex] : null;
        public bool IsFiring { get; private set; }
        bool _heldLastFrame;

        /// <summary>Authoritative origin for every attack: the player root.</summary>
        public Vector2 Origin => motor.Position;
        public Vector2 CursorWorld => motor.CursorWorld;
        public Vector2 AimDirection
        {
            get
            {
                Vector2 d = CursorWorld - Origin;
                return d.sqrMagnitude > 1e-6f ? d.normalized : motor.AimDirection;
            }
        }

        static FireBinding? s_binding;
        public static FireBinding Binding
        {
            get
            {
                s_binding ??= (FireBinding)PlayerPrefs.GetInt(FireBindingPref, (int)FireBinding.OtherMouseButton);
                return s_binding.Value;
            }
            set
            {
                if (s_binding == value) return;
                s_binding = value;
                PlayerPrefs.SetInt(FireBindingPref, (int)value);
            }
        }

        public static string FireButtonName
        {
            get
            {
                switch (ResolvedBinding)
                {
                    case FireBinding.LeftMouse: return "LMB";
                    case FireBinding.RightMouse: return "RMB";
                    default: return "E";
                }
            }
        }

        static FireBinding ResolvedBinding => Binding != FireBinding.OtherMouseButton ? Binding
            : PrototypeInput.GrappleButton == GrappleMouseButton.Right ? FireBinding.LeftMouse : FireBinding.RightMouse;

        /// <summary>True when firing uses the grapple's cancel button, so the grapple mustn't treat it as cancel.</summary>
        static bool FireUsesGrappleCancel
        {
            get
            {
                FireBinding b = ResolvedBinding;
                if (b == FireBinding.KeyE) return false;
                bool fireRight = b == FireBinding.RightMouse;
                bool grappleRight = PrototypeInput.GrappleButton == GrappleMouseButton.Right;
                return fireRight != grappleRight;
            }
        }

        static bool FireHeld
        {
            get
            {
                switch (ResolvedBinding)
                {
                    case FireBinding.KeyE: return PrototypeInput.Held(Key.E);
                    case FireBinding.RightMouse: return Mouse.current != null && Mouse.current.rightButton.isPressed;
                    default: return Mouse.current != null && Mouse.current.leftButton.isPressed;
                }
            }
        }

        void Awake()
        {
            if (motor == null) motor = FindFirstObjectByType<PlayerMotor2D>();
            if (projectiles == null) projectiles = GetComponent<ProjectileSystem>();
            weapons.RemoveAll(w => w == null);
            foreach (PrototypeWeapon w in weapons) w.Init(this);
            selectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectedPref, selectedIndex), 0, Mathf.Max(0, weapons.Count - 1));
        }

        void Start()
        {
            Current?.OnSelected();
        }

        void OnDisable()
        {
            PrototypeInput.GrappleCancelSuppressed = false;
        }

        void Update()
        {
            if (motor == null || weapons.Count == 0) return;
            float dt = Time.deltaTime;
            PrototypeInput.GrappleCancelSuppressed = FireUsesGrappleCancel;

            if (PrototypeInput.Pressed(Key.LeftBracket)) Select(selectedIndex - 1);
            if (PrototypeInput.Pressed(Key.RightBracket)) Select(selectedIndex + 1);

            for (int i = 0; i < weapons.Count; i++) weapons[i].Tick(dt, i == selectedIndex);

            PrototypeWeapon w = Current;
            bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            bool held = FireHeld && !PrototypeInput.KeyboardBlocked && !dead;
            bool pressed = held && !_heldLastFrame;
            _heldLastFrame = held;
            if (!IsFiring)
            {
                // Only a fresh press starts an attack, and UI clicks (panel hover, dragging a slider) never do.
                if (pressed && !PrototypeInput.PointerBlocked && GUIUtility.hotControl == 0)
                {
                    IsFiring = true;
                    w.BeginFire();
                }
            }
            else if (held)
            {
                w.HoldFire(dt);
            }
            else
            {
                IsFiring = false;
                w.EndFire();
            }

            if (DebugLines.Available) w.DrawDebug();
        }

        void LateUpdate()
        {
            // Charge / cooldown bar under the player while the weapon isn't simply ready.
            PrototypeWeapon w = Current;
            if (!CombatDebug.PlayerBars || w == null || motor == null) return;
            WeaponPhase phase = w.Phase;
            float progress = Mathf.Clamp01(w.PhaseProgress);
            if (progress >= 0.999f && (phase == WeaponPhase.Ready || phase == WeaponPhase.Active)) return; // (flame refuelling still shows)
            const float width = 1.1f, height = 0.1f;
            Vector2 left = motor.Position + new Vector2(-width * 0.5f, -(motor.Radius + 0.3f));
            PlayerHealth.Bar(left, width + 0.06f, height + 0.06f, new Color(0f, 0f, 0f, 0.6f), -0.03f);
            PlayerHealth.Bar(left, width * progress, height, PrototypeWeapon.PhaseColor(phase), 0f);
        }

        public void Select(int index)
        {
            if (weapons.Count == 0) return;
            index = (index % weapons.Count + weapons.Count) % weapons.Count;
            if (index == selectedIndex) return;
            PrototypeWeapon old = Current;
            if (IsFiring)
            {
                old?.EndFire();
                IsFiring = false;
            }
            old?.OnDeselected();
            selectedIndex = index;
            PlayerPrefs.SetInt(SelectedPref, selectedIndex);
            Current?.OnSelected();
        }

        public void Select(PrototypeWeapon weapon) => Select(weapons.IndexOf(weapon));

        /// <summary>Clear weapon effects: active beams, chains, flail swing, locks (cooldowns kept).</summary>
        public void CancelAllEffects()
        {
            IsFiring = false;
            foreach (PrototypeWeapon w in weapons) w.Cancel();
            Current?.OnSelected();
        }

        /// <summary>Full reset: effects, cooldowns, projectiles.</summary>
        public void ResetAll()
        {
            IsFiring = false;
            foreach (PrototypeWeapon w in weapons) w.ResetRuntime();
            if (projectiles != null) projectiles.Clear();
            Current?.OnSelected();
        }
    }
}
