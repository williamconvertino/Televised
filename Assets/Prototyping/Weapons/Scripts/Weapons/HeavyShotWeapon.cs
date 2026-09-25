using System;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 2: a large, slow, gravity-affected projectile with high damage and knockback and a long cooldown.
    /// Range is total distance traveled along the arc.
    /// </summary>
    public class HeavyShotWeapon : PrototypeWeapon
    {
        [Serializable]
        public class HeavySettings
        {
            [Min(0f)] public float baseDamage = 45f;
            [Min(0.5f)] public float projectileSpeed = 13f;
            [Min(0f)] public float projectileGravity = 12f;
            [Min(0.05f)] public float projectileRadius = 0.4f;
            [Tooltip("Fraction of the player's velocity added to the shot.")]
            [Range(0f, 1f)] public float inheritPlayerVelocity = 0.3f;
            [Min(0f)] public float cooldown = 1.4f;
            [Tooltip("Holding fire re-fires whenever the cooldown ends.")]
            public bool refireWhileHeld = true;
            [Header("Ricochet")]
            [Tooltip("Bounces off surfaces and enemies before it stops (0 = stops at the first thing it hits).")]
            [Range(0, 10)] public int ricochets = 2;
            [Tooltip("Also ricochet off enemies (otherwise it stops at the first enemy once piercing is used up).")]
            public bool ricochetOffEnemies = true;
            [Tooltip("Speed kept per bounce.")]
            [Range(0.1f, 1.2f)] public float bounceRestitution = 0.8f;
            [Tooltip("Damage multiplier per bounce already made (1 = full damage after every bounce).")]
            [Range(0f, 1.5f)] public float damagePerBounce = 1f;

            public RangeSettings range = new RangeSettings(28f, 0.75f);
            public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.None, 0.6f, 0.8f);
            public PierceSettings pierce = new PierceSettings(false, 0, PierceFalloffMode.None, 0.7f);
            public KnockbackSettings knockback = new KnockbackSettings(16f, KnockbackDirection.AlongAttackDirection);
            public Color color = new Color(1f, 0.55f, 0.2f, 1f);
        }

        [SerializeField] HeavySettings settings = new HeavySettings();

        public override string DisplayName => "Heavy Shot";
        public override string Summary => "Slow ballistic cannonball that ricochets off surfaces and enemies. Big damage and knockback.";
        public override object Settings => settings;
        public override WeaponPhase Phase => CooldownReady ? WeaponPhase.Ready : WeaponPhase.CoolingDown;

        public override void BeginFire() => TryFire();

        public override void HoldFire(float dt)
        {
            if (settings.refireWhileHeld) TryFire();
        }

        void TryFire()
        {
            if (!CooldownReady) return;
            HeavySettings s = settings;
            Vector2 dir = C.AimDirection;
            C.Projectiles.Spawn(new Projectile
            {
                weapon = this,
                sourceName = DisplayName,
                position = C.Origin + dir * 0.2f,
                velocity = dir * s.projectileSpeed + C.Motor.Velocity * s.inheritPlayerVelocity,
                gravity = s.projectileGravity,
                radius = s.projectileRadius,
                baseDamage = s.baseDamage,
                range = s.range,
                distanceFalloff = s.distanceFalloff,
                pierce = s.pierce,
                knockback = s.knockback,
                bounces = s.ricochets,
                bounceOffEnemies = s.ricochetOffEnemies,
                bounceRestitution = s.bounceRestitution,
                bounceDamageMultiplier = s.damagePerBounce,
                color = s.color,
                trailLength = 1.2f,
                trailWidth = s.projectileRadius * 0.9f,
                lingerTime = 0.15f,
            });
            CountActivation();
            StartCooldown(s.cooldown);
        }

        public override void DrawDebug()
        {
            HeavySettings s = settings;
            Vector2 o = C.Origin, dir = C.AimDirection;
            if (CombatDebug.WeaponRange || CombatDebug.AttackGeometry)
            {
                // Predicted arc for the full traveled range.
                Vector2 p = o, v = dir * s.projectileSpeed + C.Motor.Velocity * s.inheritPlayerVelocity;
                float traveled = 0f;
                const float h = 0.02f;
                for (int i = 0; i < 400 && traveled < s.range.maxRange; i++)
                {
                    v += Vector2.down * (s.projectileGravity * h);
                    Vector2 n = p + v * h;
                    traveled += (n - p).magnitude;
                    Color c = CombatDebug.RangeColor;
                    c.a *= s.range.Fade(traveled) * 2f;
                    DebugLines.Line(p, n, c);
                    p = n;
                }
                DebugLines.Circle(p, s.projectileRadius, CombatDebug.RangeColor, 16);
            }
        }

        public override void DrawTuning(TuningGui g)
        {
            HeavySettings s = settings;
            g.Header("Damage");
            s.baseDamage = g.Slider("Base Damage", s.baseDamage, 0f, 300f, "0");
            s.cooldown = g.Slider("Cooldown", s.cooldown, 0f, 5f);
            s.refireWhileHeld = g.Toggle("Re-fire while held", s.refireWhileHeld);
            g.Header("Projectile");
            s.projectileSpeed = g.Slider("Projectile Speed", s.projectileSpeed, 1f, 50f, "0.0");
            s.projectileGravity = g.Slider("Projectile Gravity", s.projectileGravity, 0f, 40f, "0.0");
            s.projectileRadius = g.Slider("Projectile Radius", s.projectileRadius, 0.05f, 1.5f);
            s.inheritPlayerVelocity = g.Slider("Inherit Player Velocity", s.inheritPlayerVelocity, 0f, 1f);
            g.Header("Ricochet");
            s.ricochets = g.IntSlider("Ricochets (bounces)", s.ricochets, 0, 10);
            if (s.ricochets > 0)
            {
                s.ricochetOffEnemies = g.Toggle("Ricochet off enemies too", s.ricochetOffEnemies);
                s.bounceRestitution = g.Slider("Speed Kept Per Bounce", s.bounceRestitution, 0.1f, 1.2f);
                s.damagePerBounce = g.Slider("Damage Multiplier Per Bounce", s.damagePerBounce, 0f, 1.5f);
            }
            g.Header("Range / Falloff");
            g.Range(s.range, 60f, "Max Range (distance traveled)");
            g.Falloff(s.distanceFalloff);
            g.Header("Piercing");
            g.Pierce(s.pierce);
            g.Header("Knockback");
            g.Knockback(s.knockback, 40f);
        }
    }
}
