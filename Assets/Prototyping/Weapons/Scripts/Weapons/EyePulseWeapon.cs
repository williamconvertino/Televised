using System;
using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 10: a short radial "get away from me" burst centred on the player root. The ring expands over the
    /// active duration and hits each enemy once as it passes; damage falls off radially (touching the eye = full).
    /// </summary>
    public class EyePulseWeapon : PrototypeWeapon
    {
        [Serializable]
        public class PulseSettings
        {
            [Min(0f)] public float baseDamage = 20f;
            [Min(0.1f)] public float radius = 2.6f;
            [Min(0f)] public float windup = 0.06f;
            [Tooltip("Seconds for the ring to expand to full radius.")]
            [Min(0.01f)] public float activeDuration = 0.14f;
            [Min(0f)] public float cooldown = 1.1f;
            public bool refireWhileHeld;
            [Tooltip("Terrain between the eye and an enemy blocks the pulse.")]
            public bool requireLineOfSight;
            [Tooltip("x = distance from the eye's edge / radius.")]
            public DistanceFalloff radialFalloff = new DistanceFalloff(FalloffMode.Linear, 0.15f, 0.4f);
            public KnockbackSettings knockback = new KnockbackSettings(20f, KnockbackDirection.AwayFromSource);
            public Color color = new Color(1f, 0.45f, 0.85f, 0.8f);
        }

        [SerializeField] PulseSettings settings = new PulseSettings();

        public override string DisplayName => "Eye Pulse";
        public override string Summary => "Close radial burst around the eye with strong knockback.";
        public override object Settings => settings;

        enum State { Idle, Windup, Active }

        State _state;
        float _stateTime;
        float _fadeTime = -1f;
        HitTracker _tracker = new HitTracker();
        readonly List<DummyEnemy> _inside = new List<DummyEnemy>();

        public override WeaponPhase Phase => _state == State.Windup ? WeaponPhase.Windup
            : _state == State.Active ? WeaponPhase.Active
            : CooldownReady ? WeaponPhase.Ready : WeaponPhase.CoolingDown;

        public override float PhaseProgress =>
            _state == State.Windup ? Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, settings.windup))
            : _state == State.Active ? 1f - Mathf.Clamp01(_stateTime / settings.activeDuration)
            : base.PhaseProgress;

        public override void BeginFire() => TryFire();

        public override void HoldFire(float dt)
        {
            if (settings.refireWhileHeld) TryFire();
        }

        void TryFire()
        {
            if (_state != State.Idle || !CooldownReady) return;
            CountActivation();
            _tracker = new HitTracker();
            _state = State.Windup;
            _stateTime = 0f;
        }

        public override void Cancel()
        {
            if (_state != State.Idle) StartCooldown(settings.cooldown);
            _state = State.Idle;
        }

        float CurrentRadius => _state == State.Active
            ? settings.radius * Mathf.Clamp01(_stateTime / settings.activeDuration)
            : 0f;

        public override void Tick(float dt, bool selected)
        {
            if (_state == State.Idle) return;
            PulseSettings s = settings;
            _stateTime += dt;
            if (_state == State.Windup)
            {
                if (_stateTime < s.windup) return;
                _state = State.Active;
                _stateTime = 0f;
            }

            Vector2 origin = C.Origin;
            float eye = C.Motor.Radius;
            // The ring starts at the eye's surface.
            float ringRadius = eye + CurrentRadius;
            CombatQueries.EnemiesInCircle(origin, ringRadius, _inside);
            foreach (DummyEnemy e in _inside)
            {
                if (_tracker.HasHit(e)) continue;
                if (s.requireLineOfSight && !CombatQueries.LineOfSight(origin, e.Position)) continue;
                float edgeDist = Mathf.Max(0f, (e.Position - origin).magnitude - e.Radius - eye);
                var ev = DamageEvent.Weapon(DisplayName, this, s.baseDamage, edgeDist, edgeDist / s.radius,
                    s.radialFalloff, 0, null);
                Vector2 dir = (e.Position - origin).normalized;
                ev.sourcePosition = origin;
                ev.hitPoint = origin + dir * (eye + edgeDist);
                ev.hitDirection = dir;
                ev.knockback = s.knockback.Compute(origin, dir, ev.hitPoint, e.Position, ev.FalloffMultiplier);
                CombatDamage.Apply(e, ref ev);
                _tracker.Record(e, -1f);
            }

            if (_stateTime >= s.activeDuration)
            {
                _state = State.Idle;
                _fadeTime = Time.time;
                StartCooldown(s.cooldown);
            }
        }

        void LateUpdate()
        {
            if (C == null) return;
            PulseSettings s = settings;
            Vector2 o = C.Origin;
            float eye = C.Motor.Radius;
            if (_state == State.Windup)
            {
                float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.windup));
                CombatShapes.Ring(o, eye + 0.4f * (1f - k), 0.1f, CombatShapes.WithAlpha(s.color, 0.4f + 0.5f * k));
            }
            else if (_state == State.Active)
            {
                float r = eye + CurrentRadius;
                Color inner = CombatShapes.WithAlpha(s.color, 0.05f);
                CombatShapes.Ring(o, eye, r, inner, CombatShapes.WithAlpha(s.color, s.color.a * 0.5f), 48);
                CombatShapes.Ring(o, r, 0.14f, s.color);
            }
            else if (_fadeTime > 0f)
            {
                // Afterglow at full radius.
                float k = 1f - (Time.time - _fadeTime) / 0.2f;
                if (k > 0f) CombatShapes.Ring(o, eye + s.radius, 0.14f * k, CombatShapes.WithAlpha(s.color, s.color.a * k));
            }
        }

        public override void DrawDebug()
        {
            Vector2 o = C.Origin;
            float r = C.Motor.Radius + settings.radius;
            if (CombatDebug.WeaponRange) DebugLines.Circle(o, r, CombatDebug.RangeColor, 48);
            if (CombatDebug.AttackGeometry && _state == State.Active)
                DebugLines.Circle(o, C.Motor.Radius + CurrentRadius, CombatDebug.GeometryColor, 48);
        }

        public override void DrawTuning(TuningGui g)
        {
            PulseSettings s = settings;
            g.Header("Damage");
            s.baseDamage = g.Slider("Base Damage", s.baseDamage, 0f, 200f, "0");
            s.cooldown = g.Slider("Cooldown", s.cooldown, 0f, 5f);
            s.refireWhileHeld = g.Toggle("Re-fire while held", s.refireWhileHeld);
            g.Header("Pulse");
            s.radius = g.Slider("Radius (beyond the eye)", s.radius, 0.2f, 8f);
            s.windup = g.Slider("Windup", s.windup, 0f, 1f);
            s.activeDuration = g.Slider("Active Duration (expansion)", s.activeDuration, 0.01f, 1f);
            s.requireLineOfSight = g.Toggle("Blocked by terrain", s.requireLineOfSight);
            g.Header("Radial Falloff");
            g.Falloff(s.radialFalloff, "Radial Falloff");
            g.Info("Piercing doesn't apply: every enemy in the radius is hit once.");
            g.Header("Knockback");
            g.Knockback(s.knockback, 40f);
        }
    }
}
