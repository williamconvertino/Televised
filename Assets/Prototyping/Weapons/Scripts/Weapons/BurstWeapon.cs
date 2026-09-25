using System;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 3: one trigger pull fires several medium projectiles in sequence, each with random spread and a light
    /// aim assist that bends the shot part of the way toward a nearby target (never guaranteeing a hit).
    /// </summary>
    public class BurstWeapon : PrototypeWeapon
    {
        [Serializable]
        public class BurstSettings
        {
            [Min(0f)] public float damagePerProjectile = 12f;
            [Min(1f)] public float projectileSpeed = 32f;
            [Min(0.02f)] public float projectileRadius = 0.12f;
            [Min(1)] public int burstCount = 4;
            [Min(0f)] public float burstInterval = 0.07f;
            [Min(0f)] public float cooldown = 0.6f;
            public bool refireWhileHeld = true;
            [Tooltip("Total random spread in degrees (each shot within ±half).")]
            [Range(0f, 90f)] public float spreadAngle = 8f;
            [Tooltip("Each shot re-reads the cursor. Off = the whole burst uses the aim at the trigger pull.")]
            public bool followCursorDuringBurst = true;

            [Header("Aim assist")]
            [Tooltip("Targets within this angle of the cursor direction are candidates (0 = off).")]
            [Range(0f, 60f)] public float aimAssistAngle = 12f;
            [Tooltip("Fraction of the angle to the target the shot is bent (1 = straight at it).")]
            [Range(0f, 1f)] public float aimAssistStrength = 0.5f;
            [Min(0f)] public float aimAssistRange = 14f;
            [Tooltip("Aim where a moving target will be (uses its velocity and the shot speed).")]
            public bool leadTargets = true;
            [Tooltip("Ignore targets behind terrain.")]
            public bool requireLineOfSight = true;

            public RangeSettings range = new RangeSettings(16f, 0.7f);
            public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.Linear, 0.3f, 0.5f);
            public PierceSettings pierce = new PierceSettings(false, 0, PierceFalloffMode.Multiplicative, 0.7f);
            public KnockbackSettings knockback = new KnockbackSettings(2.5f, KnockbackDirection.AlongAttackDirection);
            public Color color = new Color(1f, 0.92f, 0.35f, 1f);
        }

        [SerializeField] BurstSettings settings = new BurstSettings();

        public override string DisplayName => "Burst Shot";
        public override string Summary => "Burst of fast shots with spread and light aim assist.";
        public override object Settings => settings;

        public override WeaponPhase Phase => _remaining > 0 ? WeaponPhase.Active : CooldownReady ? WeaponPhase.Ready : WeaponPhase.CoolingDown;
        public override float PhaseProgress => _remaining > 0 ? (float)_remaining / Mathf.Max(1, settings.burstCount) : base.PhaseProgress;
        public override string HudExtra => _remaining > 0 ? $"{settings.burstCount - _remaining}/{settings.burstCount}" : null;

        int _remaining;
        float _nextShot;
        Vector2 _burstAim;

        // Last aim-assist evaluation, for the debug view.
        Vector2 _dbgRaw, _dbgAssisted;
        DummyEnemy _dbgTarget;

        public override void BeginFire() => TryStartBurst();

        public override void HoldFire(float dt)
        {
            if (settings.refireWhileHeld) TryStartBurst();
        }

        void TryStartBurst()
        {
            if (_remaining > 0 || !CooldownReady) return;
            _remaining = Mathf.Max(1, settings.burstCount);
            _nextShot = Time.time;
            _burstAim = C.AimDirection;
            CountActivation();
        }

        public override void Cancel()
        {
            if (_remaining > 0) StartCooldown(settings.cooldown);
            _remaining = 0;
        }

        public override void Tick(float dt, bool selected)
        {
            while (_remaining > 0 && Time.time >= _nextShot)
            {
                FireOne();
                _remaining--;
                _nextShot += settings.burstInterval;
                if (_remaining == 0) StartCooldown(settings.cooldown);
            }
        }

        void FireOne()
        {
            BurstSettings s = settings;
            Vector2 raw = s.followCursorDuringBurst ? C.AimDirection : _burstAim;
            Vector2 dir = AssistedDirection(raw, out _);
            dir = Surface2D.Rotate(dir, UnityEngine.Random.Range(-0.5f, 0.5f) * s.spreadAngle).normalized;
            C.Projectiles.Spawn(new Projectile
            {
                weapon = this,
                sourceName = DisplayName,
                position = C.Origin,
                velocity = dir * s.projectileSpeed,
                radius = s.projectileRadius,
                baseDamage = s.damagePerProjectile,
                range = s.range,
                distanceFalloff = s.distanceFalloff,
                pierce = s.pierce,
                knockback = s.knockback,
                color = s.color,
                trailLength = 0.9f,
                trailWidth = s.projectileRadius * 1.2f,
            });
        }

        /// <summary>Bend raw toward the best target in the assist cone. Also records the debug state.</summary>
        Vector2 AssistedDirection(Vector2 raw, out DummyEnemy target)
        {
            BurstSettings s = settings;
            target = null;
            _dbgRaw = raw;
            _dbgAssisted = raw;
            _dbgTarget = null;
            if (s.aimAssistAngle <= 0f || s.aimAssistStrength <= 0f) return raw;

            Vector2 origin = C.Origin;
            float bestScore = float.MaxValue;
            Vector2 bestAim = raw;
            foreach (DummyEnemy e in DummyEnemy.All)
            {
                if (!e.IsTargetable) continue;
                Vector2 aimPoint = e.Position;
                float dist = (aimPoint - origin).magnitude;
                if (dist > s.aimAssistRange || dist < 1e-3f) continue;
                if (s.leadTargets) aimPoint += e.Velocity * (dist / s.projectileSpeed);
                Vector2 to = aimPoint - origin;
                float angle = Vector2.Angle(raw, to);
                if (angle > s.aimAssistAngle) continue;
                if (s.requireLineOfSight && !CombatQueries.LineOfSight(origin, e.Position)) continue;
                // Mostly "closest to the crosshair", slightly favouring nearer targets.
                float score = angle / s.aimAssistAngle + 0.25f * dist / s.aimAssistRange;
                if (score < bestScore)
                {
                    bestScore = score;
                    target = e;
                    bestAim = to.normalized;
                }
            }
            if (target == null) return raw;

            float signed = Vector2.SignedAngle(raw, bestAim);
            Vector2 assisted = Surface2D.Rotate(raw, signed * s.aimAssistStrength).normalized;
            _dbgAssisted = assisted;
            _dbgTarget = target;
            return assisted;
        }

        public override void DrawDebug()
        {
            BurstSettings s = settings;
            Vector2 o = C.Origin, raw = C.AimDirection;
            if (CombatDebug.WeaponRange)
                DebugLines.Circle(o, s.range.maxRange, CombatDebug.RangeColor, 64);
            if (CombatDebug.AttackGeometry)
                CombatDebug.Cone(o, raw, s.spreadAngle * 0.5f, s.range.maxRange, CombatDebug.GeometryColor);

            if (!CombatDebug.AimAssist) return;
            if (_remaining == 0) AssistedDirection(raw, out _); // live preview between bursts
            Color cone = new Color(0.3f, 1f, 0.5f, 0.5f);
            if (s.aimAssistAngle > 0f) CombatDebug.Cone(o, raw, s.aimAssistAngle, s.aimAssistRange, cone);
            DebugLines.Arrow(o, o + _dbgRaw * 3f, Color.white, 0.2f);
            foreach (DummyEnemy e in DummyEnemy.All)
            {
                if (!e.IsTargetable || (e.Position - o).magnitude > s.aimAssistRange) continue;
                if (Vector2.Angle(raw, e.Position - o) <= s.aimAssistAngle)
                    DebugLines.Circle(e.Position, e.Radius + 0.15f, cone, 16);
            }
            if (_dbgTarget != null)
            {
                DebugLines.Circle(_dbgTarget.Position, _dbgTarget.Radius + 0.25f, Color.green, 20);
                DebugLines.Arrow(o, o + _dbgAssisted * 4f, Color.green, 0.25f);
            }
        }

        public override void DrawTuning(TuningGui g)
        {
            BurstSettings s = settings;
            g.Header("Damage");
            s.damagePerProjectile = g.Slider("Damage Per Projectile", s.damagePerProjectile, 0f, 100f, "0.0");
            s.cooldown = g.Slider("Cooldown (after burst)", s.cooldown, 0f, 3f);
            s.refireWhileHeld = g.Toggle("Re-fire while held", s.refireWhileHeld);
            g.Header("Burst");
            s.burstCount = g.IntSlider("Burst Count", s.burstCount, 1, 12);
            s.burstInterval = g.Slider("Burst Interval", s.burstInterval, 0f, 0.3f, "0.000");
            s.spreadAngle = g.Slider("Spread Angle (total)", s.spreadAngle, 0f, 60f, "0.0");
            s.followCursorDuringBurst = g.Toggle("Follow cursor during burst", s.followCursorDuringBurst);
            s.projectileSpeed = g.Slider("Projectile Speed", s.projectileSpeed, 5f, 100f, "0");
            s.projectileRadius = g.Slider("Projectile Radius", s.projectileRadius, 0.02f, 0.5f);
            g.Header("Aim Assist");
            s.aimAssistAngle = g.Slider("Aim Assist Angle", s.aimAssistAngle, 0f, 60f, "0.0");
            s.aimAssistStrength = g.Slider("Aim Assist Strength", s.aimAssistStrength, 0f, 1f);
            s.aimAssistRange = g.Slider("Aim Assist Range", s.aimAssistRange, 0f, 40f, "0.0");
            s.leadTargets = g.Toggle("Lead moving targets", s.leadTargets);
            s.requireLineOfSight = g.Toggle("Require line of sight", s.requireLineOfSight);
            g.Header("Range / Falloff");
            g.Range(s.range, 40f);
            g.Falloff(s.distanceFalloff);
            g.Header("Piercing");
            g.Pierce(s.pierce);
            g.Header("Knockback");
            g.Knockback(s.knockback, 15f);
        }
    }
}
