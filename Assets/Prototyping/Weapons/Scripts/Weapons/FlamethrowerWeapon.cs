using System;
using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 5: a short cone toward the cursor that ticks damage on every enemy inside it, with strong distance
    /// falloff (point-blank hurts most). Uses heat: firing fills it, overheating forces a cooldown.
    /// Gameplay uses the cone; the particles are cosmetic.
    /// </summary>
    public class FlamethrowerWeapon : PrototypeWeapon
    {
        [Serializable]
        public class FlameSettings
        {
            [Min(0f)] public float damagePerSecond = 70f;
            [Min(0.02f)] public float tickInterval = 0.1f;
            public RangeSettings range = new RangeSettings(4.5f, 0.55f);
            [Tooltip("Full cone angle in degrees.")]
            [Range(1f, 180f)] public float coneAngle = 40f;
            [Tooltip("Seconds of continuous fire until overheating.")]
            [Min(0.1f)] public float maxFireDuration = 2.5f;
            [Tooltip("Heat lost per second while not firing (1 = a full bar per second).")]
            [Min(0f)] public float coolRate = 0.6f;
            [Tooltip("Forced cooldown when overheated.")]
            [Min(0f)] public float overheatCooldown = 1.5f;
            [Tooltip("Terrain between the eye and an enemy blocks the flame.")]
            public bool requireLineOfSight = true;
            public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.Linear, 0f, 0.2f);
            [Tooltip("Normally every enemy in the cone takes full damage. On: sort by distance and apply pierce falloff.")]
            public bool applyPierceFalloff;
            public PierceSettings pierce = new PierceSettings(true, 0, PierceFalloffMode.Multiplicative, 0.8f);
            public KnockbackSettings knockback = new KnockbackSettings(1f, KnockbackDirection.AwayFromSource);
            public Color innerColor = new Color(1f, 0.95f, 0.55f, 0.75f);
            public Color outerColor = new Color(1f, 0.3f, 0.05f, 0.5f);
        }

        [SerializeField] FlameSettings settings = new FlameSettings();

        public override string DisplayName => "Flamethrower";
        public override string Summary => "Hold: short cone, ticks everything inside. Strong falloff, overheats.";
        public override object Settings => settings;

        public override WeaponPhase Phase => _overheated ? WeaponPhase.Overheated : _firing ? WeaponPhase.Active : WeaponPhase.Ready;
        public override float PhaseRemaining => _overheated ? Mathf.Max(0f, CooldownUntil - Time.time) : 0f;
        public override string HudExtra => $"Heat {Mathf.RoundToInt(_heat * 100f)}%";
        /// <summary>The bar shows remaining fuel (1 - heat), or the overheat cooldown.</summary>
        public override float PhaseProgress => _overheated ? base.PhaseProgress : 1f - _heat;

        bool _firing, _overheated;
        float _heat;
        HitTracker _tracker = new HitTracker();
        readonly List<DummyEnemy> _inCone = new List<DummyEnemy>();

        struct Particle
        {
            public Vector2 pos, vel;
            public float age, life, size;
        }

        readonly List<Particle> _particles = new List<Particle>();
        float _emitAccum;

        public override void BeginFire()
        {
            if (_overheated) return;
            _firing = true;
            _tracker = new HitTracker();
            CountActivation();
        }

        public override void HoldFire(float dt)
        {
            if (!_firing && !_overheated) BeginFire();
        }

        public override void EndFire() => _firing = false;

        public override void Cancel()
        {
            _firing = false;
            _particles.Clear();
        }

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _heat = 0f;
            _overheated = false;
        }

        public override void Tick(float dt, bool selected)
        {
            FlameSettings s = settings;
            if (_overheated && CooldownReady)
            {
                _overheated = false;
                _heat = 0f;
            }

            if (_firing)
            {
                _heat += dt / s.maxFireDuration;
                if (_heat >= 1f)
                {
                    _heat = 1f;
                    _firing = false;
                    _overheated = true;
                    StartCooldown(s.overheatCooldown);
                }
                else
                {
                    DealDamage();
                }
            }
            else if (!_overheated)
            {
                _heat = Mathf.Max(0f, _heat - s.coolRate * dt);
            }

            UpdateParticles(dt);
        }

        void DealDamage()
        {
            FlameSettings s = settings;
            Vector2 origin = C.Origin, dir = C.AimDirection;
            CombatQueries.EnemiesInCone(origin, dir, s.range.maxRange, s.coneAngle * 0.5f, _inCone);
            int index = 0;
            foreach (DummyEnemy e in _inCone)
            {
                if (s.requireLineOfSight && !CombatQueries.LineOfSight(origin, e.Position)) continue;
                int pierceIndex = s.applyPierceFalloff ? index : 0;
                index++;
                if (!_tracker.CanHit(e)) continue;
                // Distance to the near edge of the enemy, so an enemy touching the eye takes full damage.
                float dist = Mathf.Max(0f, (e.Position - origin).magnitude - e.Radius);
                var ev = DamageEvent.Weapon(DisplayName, this, s.damagePerSecond * s.tickInterval, dist,
                    dist / s.range.maxRange, s.distanceFalloff, pierceIndex, s.applyPierceFalloff ? s.pierce : null);
                ev.sourcePosition = origin;
                ev.hitPoint = e.Position - (e.Position - origin).normalized * e.Radius;
                ev.hitDirection = dir;
                ev.knockback = s.knockback.Compute(origin, dir, ev.hitPoint, e.Position, ev.FalloffMultiplier);
                CombatDamage.Apply(e, ref ev);
                _tracker.Record(e, s.tickInterval);
            }
        }

        void UpdateParticles(float dt)
        {
            FlameSettings s = settings;
            if (_firing && C != null)
            {
                Vector2 dir = C.AimDirection;
                _emitAccum += dt * 90f;
                float speed = s.range.maxRange / 0.35f;
                while (_emitAccum >= 1f)
                {
                    _emitAccum -= 1f;
                    Vector2 d = Surface2D.Rotate(dir, UnityEngine.Random.Range(-0.5f, 0.5f) * s.coneAngle);
                    _particles.Add(new Particle
                    {
                        pos = C.Origin + d * 0.4f,
                        vel = d * speed * UnityEngine.Random.Range(0.75f, 1.05f) + C.Motor.Velocity * 0.5f,
                        life = 0.35f,
                        size = UnityEngine.Random.Range(0.08f, 0.18f),
                    });
                }
            }
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.age += dt;
                p.pos += p.vel * dt;
                p.vel *= Mathf.Exp(-1.5f * dt);
                p.vel += Vector2.up * (3f * dt); // flames rise
                if (p.age >= p.life) _particles.RemoveAt(i);
                else _particles[i] = p;
            }
        }

        void LateUpdate()
        {
            if (C == null) return;
            FlameSettings s = settings;
            Vector2 origin = C.Origin;
            if (_firing)
            {
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 18f, 0f);
                CombatShapes.Fan(origin, C.AimDirection, s.coneAngle * 0.5f, s.range.maxRange * flicker, s.innerColor, s.outerColor, s.range);
            }
            foreach (Particle p in _particles)
            {
                float t = p.age / p.life;
                Color c = Color.Lerp(s.innerColor, s.outerColor, t);
                c.a = Mathf.Min(1f, c.a + 0.3f) * (1f - t) * s.range.Fade((p.pos - origin).magnitude);
                CombatShapes.Disc(p.pos, p.size * (1f + t * 1.5f), c, 8);
            }
        }

        public override void DrawDebug()
        {
            Vector2 o = C.Origin, dir = C.AimDirection;
            if (CombatDebug.WeaponRange) DebugLines.Circle(o, settings.range.maxRange, CombatDebug.RangeColor, 48);
            if (CombatDebug.AttackGeometry) CombatDebug.Cone(o, dir, settings.coneAngle * 0.5f, settings.range.maxRange, CombatDebug.GeometryColor);
        }

        public override void DrawTuning(TuningGui g)
        {
            FlameSettings s = settings;
            g.Header("Damage");
            s.damagePerSecond = g.Slider("Damage Per Second", s.damagePerSecond, 0f, 300f, "0");
            s.tickInterval = g.Slider("Tick Interval", s.tickInterval, 0.02f, 0.5f);
            g.Info($"  = {s.damagePerSecond * s.tickInterval:0.0} per tick at point blank");
            g.Header("Flame");
            s.coneAngle = g.Slider("Cone Angle", s.coneAngle, 1f, 180f, "0");
            s.requireLineOfSight = g.Toggle("Blocked by terrain", s.requireLineOfSight);
            g.Header("Heat");
            s.maxFireDuration = g.Slider("Max Fire Duration", s.maxFireDuration, 0.2f, 10f);
            s.coolRate = g.Slider("Cool Rate (bar/s)", s.coolRate, 0f, 3f);
            s.overheatCooldown = g.Slider("Overheat Cooldown", s.overheatCooldown, 0f, 5f);
            g.Header("Range / Falloff");
            g.Range(s.range, 12f);
            g.Falloff(s.distanceFalloff);
            g.Header("Piercing");
            g.Info("Infinite: every enemy in the cone is hit.");
            s.applyPierceFalloff = g.Toggle("Apply pierce falloff (by distance order)", s.applyPierceFalloff);
            if (s.applyPierceFalloff) g.Pierce(s.pierce, 10, false);
            g.Header("Knockback");
            g.Knockback(s.knockback, 10f);
        }
    }
}
