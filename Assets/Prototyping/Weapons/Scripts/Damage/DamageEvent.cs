using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum DamageKind
    {
        Weapon,
        BodyImpact,
        EnemyContact,
        ImpactSelfDamage,
        /// <summary>An enemy launched into terrain (knockback, chain swing) - damage from its speed into the surface.</summary>
        WallSlam,
    }

    /// <summary>
    /// One application of damage, carrying every number that went into it so the debug overlay can show the
    /// whole pipeline: base -> distance falloff -> pierce falloff -> other modifiers -> final.
    /// </summary>
    public struct DamageEvent
    {
        public DamageKind kind;
        public string sourceName;
        public Object source;

        public float baseDamage;
        public float distanceMultiplier;
        public float pierceMultiplier;
        public float otherMultiplier;
        public float finalDamage;

        public Vector2 sourcePosition;
        public Vector2 hitPoint;
        public Vector2 hitDirection;
        public float distance;
        public int pierceIndex;
        public Vector2 knockback;

        /// <summary>Body impacts: speed along the collision direction, and the part of the damage it added.</summary>
        public float impactSpeed;
        public float speedDamage;
        public float playerDamageTaken;
        /// <summary>Wall slams: the attack that launched the enemy.</summary>
        public string cause;

        /// <summary>Set by the receiver: absorbed (invulnerable / indestructible) or killed the target.</summary>
        public bool blocked;
        public bool killed;

        /// <summary>
        /// The standard weapon pipeline. distanceFraction is distance / range (or / radius for radial attacks).
        /// </summary>
        public static DamageEvent Weapon(string sourceName, Object source, float baseDamage,
            float distance, float distanceFraction, DistanceFalloff distanceFalloff,
            int pierceIndex, PierceSettings pierce, float otherMultiplier = 1f)
        {
            float dm = distanceFalloff != null ? distanceFalloff.Evaluate(distanceFraction) : 1f;
            float pm = pierce != null ? pierce.Evaluate(pierceIndex, baseDamage) : 1f;
            return new DamageEvent
            {
                kind = DamageKind.Weapon,
                sourceName = sourceName,
                source = source,
                baseDamage = baseDamage,
                distanceMultiplier = dm,
                pierceMultiplier = pm,
                otherMultiplier = otherMultiplier,
                finalDamage = baseDamage * dm * pm * otherMultiplier,
                distance = distance,
                pierceIndex = pierceIndex,
            };
        }

        public float FalloffMultiplier => distanceMultiplier * pierceMultiplier;

        public string DetailText()
        {
            var sb = new StringBuilder();
            if (kind == DamageKind.WallSlam)
            {
                sb.Append($"Wall Slam: {finalDamage:0.0}\n");
                sb.Append($"Speed Into Surface: {impactSpeed:0.0}\n");
                sb.Append($"Base {baseDamage:0.#} + Speed {speedDamage:0.0}\n");
                sb.Append($"Launched By: {cause ?? "?"}");
            }
            else if (kind == DamageKind.BodyImpact)
            {
                sb.Append($"Impact Damage: {finalDamage:0.0}\n");
                sb.Append($"Relative Speed: {impactSpeed:0.0}\n");
                sb.Append($"Base Damage: {baseDamage:0.#}\n");
                sb.Append($"Speed Damage: +{speedDamage:0.0}\n");
                sb.Append($"Player Damage Taken: {playerDamageTaken:0.#}");
            }
            else
            {
                sb.Append($"Damage: {baseDamage:0.0}\n");
                sb.Append($"Weapon: {sourceName}\n");
                sb.Append($"Distance: {distance:0.0}\n");
                sb.Append($"Distance Multiplier: {distanceMultiplier:0.00}\n");
                sb.Append($"Pierce Index: {pierceIndex}\n");
                sb.Append($"Pierce Multiplier: {pierceMultiplier:0.00}\n");
                if (impactSpeed > 0f) sb.Append($"Impact Speed: {impactSpeed:0.0}  (speed damage +{speedDamage:0.0})\n");
                if (!Mathf.Approximately(otherMultiplier, 1f)) sb.Append($"Other Multiplier: {otherMultiplier:0.00}\n");
                sb.Append($"Final Damage: {finalDamage:0.0}");
            }
            if (knockback.sqrMagnitude > 1e-6f) sb.Append($"\nKnockback: {knockback.magnitude:0.0}");
            if (blocked) sb.Append("\n(absorbed)");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Per-target repeat-hit bookkeeping for one attack instance (one beam activation, one flail, one chain throw).
    /// Create a new tracker per activation so an old attack's cooldowns never block a new one.
    /// </summary>
    public class HitTracker
    {
        readonly Dictionary<DummyEnemy, float> _nextAllowed = new Dictionary<DummyEnemy, float>();

        public int Count => _nextAllowed.Count;

        public bool CanHit(DummyEnemy target) =>
            !_nextAllowed.TryGetValue(target, out float t) || Time.time >= t;

        /// <summary>Record a hit. interval &lt; 0 means "never again for this attack".</summary>
        public void Record(DummyEnemy target, float interval) =>
            _nextAllowed[target] = interval < 0f ? float.PositiveInfinity : Time.time + interval;

        public bool HasHit(DummyEnemy target) => _nextAllowed.ContainsKey(target);

        public void Clear() => _nextAllowed.Clear();
    }

    /// <summary>The one place weapon / impact damage is delivered to enemies.</summary>
    public static class CombatDamage
    {
        /// <summary>Damage the target and apply knockback. Returns false if the target couldn't take it.</summary>
        public static bool Apply(DummyEnemy target, ref DamageEvent e)
        {
            if (target == null || !target.IsTargetable) return false;
            if (e.hitDirection.sqrMagnitude < 1e-8f) e.hitDirection = (target.Position - e.sourcePosition).normalized;
            if (!target.Health.TakeDamage(ref e)) return false;
            CombatTelemetry.EnemyDamaged(e);
            if (e.knockback.sqrMagnitude > 1e-8f)
            {
                target.AddKnockback(e.knockback, e.sourceName);
                CombatDebug.RecordKnockback(target.Position, e.knockback * (EnemyManager.Instance != null ? EnemyManager.Instance.knockbackMultiplier : 1f) / target.Mass);
            }
            return true;
        }
    }
}
