using System;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 1: a plain straight projectile toward the cursor, one per trigger pull (or repeating while held).
    /// The baseline the other weapons are compared against.
    /// </summary>
    public class SimpleShotWeapon : PrototypeWeapon
    {
        [Serializable]
        public class SimpleSettings
        {
            [Min(0f)] public float damage = 15f;
            [Min(1f)] public float projectileSpeed = 28f;
            [Min(0.02f)] public float projectileRadius = 0.14f;
            [Min(0f)] public float cooldown = 0.25f;
            [Tooltip("Holding fire re-fires whenever the cooldown ends.")]
            public bool refireWhileHeld = true;
            [Tooltip("Fraction of the player's velocity added to the shot.")]
            [Range(0f, 1f)] public float inheritPlayerVelocity = 0f;

            public RangeSettings range = new RangeSettings(18f, 0.75f);
            public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.None, 0.5f, 0.7f);
            public PierceSettings pierce = new PierceSettings(false, 0, PierceFalloffMode.Multiplicative, 0.7f);
            public KnockbackSettings knockback = new KnockbackSettings(3f, KnockbackDirection.AlongAttackDirection);
            public Color color = new Color(0.55f, 0.9f, 1f, 1f);
        }

        [SerializeField] SimpleSettings settings = new SimpleSettings();

        public override string DisplayName => "Simple Shot";
        public override string Summary => "A regular straight projectile toward the cursor.";
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
            SimpleSettings s = settings;
            Vector2 dir = C.AimDirection;
            C.Projectiles.Spawn(new Projectile
            {
                weapon = this,
                sourceName = DisplayName,
                position = C.Origin,
                velocity = dir * s.projectileSpeed + C.Motor.Velocity * s.inheritPlayerVelocity,
                radius = s.projectileRadius,
                baseDamage = s.damage,
                range = s.range,
                distanceFalloff = s.distanceFalloff,
                pierce = s.pierce,
                knockback = s.knockback,
                color = s.color,
                trailLength = 0.8f,
                trailWidth = s.projectileRadius * 1.1f,
            });
            CountActivation();
            StartCooldown(s.cooldown);
        }

        public override void DrawDebug()
        {
            SimpleSettings s = settings;
            Vector2 o = C.Origin, dir = C.AimDirection;
            if (CombatDebug.WeaponRange)
                DebugLines.Circle(o, s.range.maxRange, CombatDebug.RangeColor, 64);
            if (CombatDebug.AttackGeometry)
                DebugLines.Line(o, o + dir * s.range.maxRange, CombatDebug.GeometryColor);
        }

        public override void DrawTuning(TuningGui g)
        {
            SimpleSettings s = settings;
            g.Header("Damage");
            s.damage = g.Slider("Damage", s.damage, 0f, 100f, "0.0");
            s.cooldown = g.Slider("Cooldown", s.cooldown, 0f, 2f);
            s.refireWhileHeld = g.Toggle("Re-fire while held", s.refireWhileHeld);
            g.Header("Projectile");
            s.projectileSpeed = g.Slider("Projectile Speed", s.projectileSpeed, 5f, 100f, "0");
            s.projectileRadius = g.Slider("Projectile Radius", s.projectileRadius, 0.02f, 0.5f);
            s.inheritPlayerVelocity = g.Slider("Inherit Player Velocity", s.inheritPlayerVelocity, 0f, 1f);
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
