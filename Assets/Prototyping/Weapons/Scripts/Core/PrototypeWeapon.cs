using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum WeaponPhase
    {
        Ready,
        Windup,
        Active,
        Recovery,
        CoolingDown,
        Overheated,
    }

    /// <summary>
    /// Base for every prototype weapon. Weapons are components on the "Weapons" object; the
    /// <see cref="WeaponController"/> selects one and forwards fire input. Each weapon keeps its tuning in a plain
    /// serializable settings object (<see cref="Settings"/>) so it can be reset, saved and loaded as JSON.
    ///
    /// Call order per frame: Tick (every weapon, selected or not) -> BeginFire / HoldFire / EndFire (selected only)
    /// -> DrawDebug (selected only, when debug drawing is on).
    /// </summary>
    public abstract class PrototypeWeapon : MonoBehaviour
    {
        protected WeaponController C { get; private set; }

        public abstract string DisplayName { get; }
        /// <summary>One line shown under the weapon name in the panel.</summary>
        public abstract string Summary { get; }
        /// <summary>The weapon's tuning (a [Serializable] class) for JSON reset / save / load.</summary>
        public abstract object Settings { get; }

        public abstract WeaponPhase Phase { get; }
        /// <summary>Seconds left in the current phase (cooldown remaining etc.), or 0.</summary>
        public virtual float PhaseRemaining => Mathf.Max(0f, CooldownUntil - Time.time);
        /// <summary>Optional extra HUD info (heat, skewered count...).</summary>
        public virtual string HudExtra => null;

        protected float CooldownUntil;
        protected float CooldownDuration;
        protected bool CooldownReady => Time.time >= CooldownUntil;

        protected void StartCooldown(float seconds)
        {
            CooldownDuration = Mathf.Max(0f, seconds);
            CooldownUntil = Time.time + CooldownDuration;
        }

        /// <summary>
        /// 0..1 fill for the charge / cooldown bar: cooldowns and charges fill up toward 1 (= ready / fires),
        /// active phases drain. 1 when ready.
        /// </summary>
        public virtual float PhaseProgress
        {
            get
            {
                if (Phase == WeaponPhase.CoolingDown || Phase == WeaponPhase.Overheated)
                    return CooldownDuration > 0f ? 1f - Mathf.Clamp01((CooldownUntil - Time.time) / CooldownDuration) : 1f;
                return 1f;
            }
        }

        public static Color PhaseColor(WeaponPhase phase)
        {
            switch (phase)
            {
                case WeaponPhase.Windup: return new Color(1f, 0.85f, 0.3f);
                case WeaponPhase.Active: return new Color(0.4f, 0.85f, 1f);
                case WeaponPhase.Recovery: return new Color(1f, 0.65f, 0.3f);
                case WeaponPhase.CoolingDown: return new Color(1f, 0.45f, 0.3f);
                case WeaponPhase.Overheated: return new Color(1f, 0.2f, 0.2f);
                default: return new Color(0.45f, 1f, 0.45f);
            }
        }

        string _defaults;

        public void Init(WeaponController controller)
        {
            C = controller;
            _defaults = JsonUtility.ToJson(Settings);
            OnInit();
        }

        protected virtual void OnInit() { }

        /// <summary>Restore the settings this weapon started Play Mode with.</summary>
        public void ResetSettings()
        {
            if (!string.IsNullOrEmpty(_defaults)) JsonUtility.FromJsonOverwrite(_defaults, Settings);
        }

        public virtual void OnSelected() { }

        /// <summary>Stop anything that can't safely persist while unselected.</summary>
        public virtual void OnDeselected() => Cancel();

        public virtual void BeginFire() { }
        public virtual void HoldFire(float dt) { }
        public virtual void EndFire() { }
        public virtual void Tick(float dt, bool selected) { }

        /// <summary>Clear transient state: active beams, chains, flail, movement locks, hit bookkeeping.</summary>
        public virtual void Cancel() { }

        /// <summary>Also clears the cooldown (used by Reset Entire Sandbox).</summary>
        public virtual void ResetRuntime()
        {
            Cancel();
            CooldownUntil = 0f;
        }

        public abstract void DrawTuning(TuningGui g);

        /// <summary>Range / geometry / aim-assist debug for the selected weapon (see <see cref="CombatDebug"/>).</summary>
        public virtual void DrawDebug() { }

        // ---------------------------------------------------------------- helpers

        protected void CountActivation() => CombatTelemetry.Activation(DisplayName);

        public string StatusText
        {
            get
            {
                string s;
                switch (Phase)
                {
                    case WeaponPhase.Ready: s = "<color=#7CFC7C>READY</color>"; break;
                    case WeaponPhase.Windup: s = $"<color=#FFE070>WINDUP</color> {PhaseRemaining:0.00}s"; break;
                    case WeaponPhase.Active: s = $"<color=#70D0FF>ACTIVE</color>{(PhaseRemaining > 0f ? $" {PhaseRemaining:0.00}s" : "")}"; break;
                    case WeaponPhase.Recovery: s = $"<color=#FFB060>RECOVERY</color> {PhaseRemaining:0.00}s"; break;
                    case WeaponPhase.Overheated: s = $"<color=#FF6060>OVERHEATED</color> {PhaseRemaining:0.0}s"; break;
                    default: s = $"<color=#FF9070>Cooldown</color> {PhaseRemaining:0.0}s"; break;
                }
                string extra = HudExtra;
                return string.IsNullOrEmpty(extra) ? s : $"{s}   {extra}";
            }
        }
    }
}
