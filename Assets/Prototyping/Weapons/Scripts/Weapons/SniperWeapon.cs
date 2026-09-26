using System;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 7: very fast, precise, high-damage shot with strong piercing and per-pierce damage falloff, and no
    /// distance falloff (same first-target damage at any range). A real projectile by default; Hitscan resolves the
    /// whole range on the frame it's fired.
    /// </summary>
    public class SniperWeapon : PrototypeWeapon
    {
        [Serializable]
        public class SniperSettings
        {
            [Min(0f)] public float baseDamage = 60f;
            [Min(1f)] public float projectileSpeed = 160f;
            public bool hitscan;
            [Min(0.02f)] public float projectileRadius = 0.08f;
            [Min(0f)] public float cooldown = 1.3f;
            public bool refireWhileHeld = true;
            public RangeSettings range = new RangeSettings(35f, 0.8f);
            public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.None, 0.5f, 0.7f);
            public PierceSettings pierce = new PierceSettings(false, 3, PierceFalloffMode.Multiplicative, 0.8f);
            public KnockbackSettings knockback = new KnockbackSettings(7f, KnockbackDirection.AlongAttackDirection);
            public Color color = new Color(0.95f, 0.95f, 1f, 1f);
        }

        [SerializeField] SniperSettings settings = new SniperSettings();

        public override string DisplayName => "Sniper Shot";
        public override string Summary => "Near-instant piercing shot. No distance falloff; damage drops per enemy pierced.";
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
            SniperSettings s = settings;
            Vector2 dir = C.AimDirection;
            C.Projectiles.Spawn(new Projectile
            {
                weapon = this,
                sourceName = DisplayName,
                position = C.Origin,
                velocity = dir * s.projectileSpeed,
                instant = s.hitscan,
                radius = s.projectileRadius,
                baseDamage = s.baseDamage,
                range = s.range,
                distanceFalloff = s.distanceFalloff,
                pierce = s.pierce,
                knockback = s.knockback,
                color = s.color,
                trailLength = s.hitscan ? s.range.maxRange : 6f,
                trailWidth = 0.07f,
                lingerTime = s.hitscan ? 0.2f : 0.1f,
            });
            CountActivation();
            StartCooldown(s.cooldown);
        }

        public override void DrawDebug()
        {
            Vector2 o = C.Origin, dir = C.AimDirection;
            if (CombatDebug.WeaponRange)
            {
                DebugLines.Line(o, o + dir * settings.range.maxRange, CombatDebug.RangeColor);
                DebugLines.Cross(o + dir * settings.range.maxRange, 0.3f, CombatDebug.RangeColor);
            }
            if (CombatDebug.AttackGeometry)
                CombatDebug.Capsule(o, o + dir * settings.range.maxRange, settings.projectileRadius, CombatDebug.GeometryColor);
        }

        public override void DrawTuning(TuningGui g)
        {
            SniperSettings s = settings;
            g.Header("Damage");
            s.baseDamage = g.Slider("Base Damage", s.baseDamage, 0f, 300f, "0");
            s.cooldown = g.Slider("Cooldown", s.cooldown, 0f, 5f);
            s.refireWhileHeld = g.Toggle("Re-fire while held", s.refireWhileHeld);
            g.Header("Projectile");
            s.hitscan = g.Toggle("Hitscan (instant)", s.hitscan);
            if (!s.hitscan) s.projectileSpeed = g.Slider("Projectile Speed", s.projectileSpeed, 10f, 400f, "0");
            s.projectileRadius = g.Slider("Projectile Radius", s.projectileRadius, 0.02f, 0.5f);
            g.Header("Range / Falloff");
            g.Range(s.range, 80f);
            g.Falloff(s.distanceFalloff);
            g.Header("Piercing");
            g.Pierce(s.pierce);
            g.Header("Knockback");
            g.Knockback(s.knockback, 30f);
        }
    }
}
