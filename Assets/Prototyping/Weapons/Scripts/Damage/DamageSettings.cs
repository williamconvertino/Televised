using System;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Reusable tuning blocks that weapons compose: range + visual fade, distance falloff, piercing and knockback.
    /// Weapons only include the blocks that mean something for them. The falloff maths lives here so no weapon
    /// re-implements it.
    /// </summary>
    [Serializable]
    public class RangeSettings
    {
        [Min(0.1f)] public float maxRange = 20f;
        [Tooltip("Visuals are fully opaque up to this fraction of the range, then fade to transparent at max range. " +
                 "Visual only: damage uses the distance falloff.")]
        [Range(0f, 1f)] public float fadeStartFraction = 0.75f;

        public RangeSettings() { }
        public RangeSettings(float maxRange, float fadeStartFraction)
        {
            this.maxRange = maxRange;
            this.fadeStartFraction = fadeStartFraction;
        }

        /// <summary>Visual opacity multiplier at a distance (1 = full, 0 = at/over max range).</summary>
        public float Fade(float distance)
        {
            float start = maxRange * fadeStartFraction;
            if (distance <= start) return 1f;
            return 1f - Mathf.Clamp01((distance - start) / Mathf.Max(1e-4f, maxRange - start));
        }
    }

    public enum FalloffMode
    {
        None,
        Linear,
        CustomCurve,
    }

    /// <summary>Damage multiplier as a function of distance fraction (0 = at the source, 1 = at max range / radius).</summary>
    [Serializable]
    public class DistanceFalloff
    {
        public FalloffMode mode = FalloffMode.None;
        [Tooltip("Linear: full damage until this fraction, then linear down to End Multiplier at 1.")]
        [Range(0f, 1f)] public float startFraction = 0f;
        [Tooltip("Linear: damage multiplier at max range.")]
        [Range(0f, 1f)] public float endMultiplier = 0.5f;
        [Tooltip("CustomCurve: x = distance fraction (0..1), y = damage multiplier. Edit in the Inspector.")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        public DistanceFalloff() { }
        public DistanceFalloff(FalloffMode mode, float startFraction, float endMultiplier)
        {
            this.mode = mode;
            this.startFraction = startFraction;
            this.endMultiplier = endMultiplier;
        }

        public float Evaluate(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            switch (mode)
            {
                case FalloffMode.Linear:
                    if (fraction <= startFraction) return 1f;
                    return Mathf.Lerp(1f, endMultiplier, (fraction - startFraction) / Mathf.Max(1e-4f, 1f - startFraction));
                case FalloffMode.CustomCurve:
                    return curve != null && curve.length > 0 ? Mathf.Max(0f, curve.Evaluate(fraction)) : 1f;
                default:
                    return 1f;
            }
        }
    }

    public enum PierceFalloffMode
    {
        None,
        FlatSubtraction,
        Multiplicative,
        CustomCurve,
    }

    /// <summary>
    /// How many enemies an attack continues through, and how its damage drops per pierced enemy.
    /// Pierce count 0 = stops at the first enemy. Terrain is handled by each weapon, never by this.
    /// </summary>
    [Serializable]
    public class PierceSettings
    {
        [Tooltip("Enemy hits never consume piercing.")]
        public bool infinite;
        [Tooltip("Extra enemies the attack may pass through after the first (0 = stops at the first enemy).")]
        [Min(0)] public int pierceCount;
        public PierceFalloffMode falloff = PierceFalloffMode.None;
        [Tooltip("Multiplicative: damage *= multiplier ^ hitIndex.")]
        [Range(0f, 1f)] public float multiplier = 0.8f;
        [Tooltip("FlatSubtraction: damage -= this * hitIndex.")]
        [Min(0f)] public float flatSubtraction = 10f;
        [Tooltip("CustomCurve: x = hit index (0, 1, 2...), y = damage multiplier. Edit in the Inspector.")]
        public AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(4f, 0.4f));

        public PierceSettings() { }
        public PierceSettings(bool infinite, int pierceCount, PierceFalloffMode falloff, float multiplier)
        {
            this.infinite = infinite;
            this.pierceCount = pierceCount;
            this.falloff = falloff;
            this.multiplier = multiplier;
        }

        /// <summary>Total enemies the attack may hit.</summary>
        public int MaxTargets => infinite ? int.MaxValue : pierceCount + 1;

        public string CountLabel => infinite ? "Infinite" : pierceCount.ToString();

        /// <summary>Damage multiplier for the hitIndex-th enemy (0 = first).</summary>
        public float Evaluate(int hitIndex, float baseDamage)
        {
            if (hitIndex <= 0) return 1f;
            switch (falloff)
            {
                case PierceFalloffMode.FlatSubtraction:
                    return baseDamage <= 0f ? 1f : Mathf.Max(0f, baseDamage - flatSubtraction * hitIndex) / baseDamage;
                case PierceFalloffMode.Multiplicative:
                    return Mathf.Pow(multiplier, hitIndex);
                case PierceFalloffMode.CustomCurve:
                    return curve != null && curve.length > 0 ? Mathf.Max(0f, curve.Evaluate(hitIndex)) : 1f;
                default:
                    return 1f;
            }
        }
    }

    public enum KnockbackDirection
    {
        /// <summary>Away from the attack's source (usually the player root).</summary>
        AwayFromSource,
        /// <summary>Along the projectile velocity / beam direction.</summary>
        AlongAttackDirection,
        /// <summary>From the point of impact through the target's centre.</summary>
        RadialFromImpact,
        PullTowardSource,
        /// <summary>A fixed world direction (Custom Angle, 90 = up).</summary>
        Custom,
    }

    [Serializable]
    public class KnockbackSettings
    {
        [Tooltip("Impulse in units/s added to the target's velocity (divided by its mass).")]
        [Min(0f)] public float force = 4f;
        public KnockbackDirection direction = KnockbackDirection.AlongAttackDirection;
        [Tooltip("Custom: world angle in degrees (0 = right, 90 = up).")]
        public float customAngle = 90f;
        [Tooltip("Scale knockback by the same distance / pierce multipliers as the damage.")]
        public bool scaleWithFalloff;

        public KnockbackSettings() { }
        public KnockbackSettings(float force, KnockbackDirection direction)
        {
            this.force = force;
            this.direction = direction;
        }

        public Vector2 Compute(Vector2 source, Vector2 attackDirection, Vector2 impactPoint, Vector2 target, float falloffMultiplier)
        {
            if (force <= 0f) return Vector2.zero;
            Vector2 dir;
            switch (direction)
            {
                case KnockbackDirection.AlongAttackDirection: dir = attackDirection; break;
                case KnockbackDirection.RadialFromImpact: dir = target - impactPoint; break;
                case KnockbackDirection.PullTowardSource: dir = source - target; break;
                case KnockbackDirection.Custom:
                    dir = new Vector2(Mathf.Cos(customAngle * Mathf.Deg2Rad), Mathf.Sin(customAngle * Mathf.Deg2Rad));
                    break;
                default: dir = target - source; break;
            }
            if (dir.sqrMagnitude < 1e-8f) dir = attackDirection.sqrMagnitude > 1e-8f ? attackDirection : Vector2.up;
            return dir.normalized * (force * (scaleWithFalloff ? falloffMultiplier : 1f));
        }
    }

    public enum RepeatHitMode
    {
        /// <summary>Each target is damaged at most once per attack instance.</summary>
        HitOnce,
        /// <summary>Each target can be damaged again after the repeat interval.</summary>
        Repeated,
    }
}
