using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum ImpactSelfDamageMode
    {
        /// <summary>The player only takes the enemy's ordinary contact damage.</summary>
        EnemyContactOnly,
        /// <summary>Contact damage plus extra self-damage from the impact itself.</summary>
        ContactPlusImpactSelfDamage,
    }

    /// <summary>
    /// The eyeball as a weapon: ramming an enemy damages it by the approach speed along the collision direction.
    /// Reads the motor's position / velocity only; nothing here lives in PlayerMotor2D.
    ///
    ///   approachSpeed = max(0, dot(playerVelocity - enemyVelocity, directionIntoEnemy))
    ///   damage        = base + max(0, approachSpeed - minimumImpactSpeed) * speedScale   (clamped to max)
    ///
    /// An impact needs actual approach (Approach Threshold), so just sitting overlapped never re-triggers it.
    /// </summary>
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(PlayerMotor2D))]
    public class BodyImpactDamage : MonoBehaviour
    {
        public const string SourceName = "Body Impact";

        public bool bodyDamageEnabled = true;
        [Min(0f)] public float baseImpactDamage = 5f;
        [Tooltip("Approach speed below which only the base damage applies.")]
        [Min(0f)] public float minimumImpactSpeed = 3f;
        [Tooltip("Damage per unit of approach speed above the minimum.")]
        [Min(0f)] public float impactSpeedDamageScale = 5f;
        [Min(0f)] public float maximumImpactDamage = 150f;
        [Tooltip("Seconds before the same enemy can be rammed again.")]
        [Min(0f)] public float impactCooldownPerEnemy = 0.4f;
        [Tooltip("Minimum approach speed for a touch to count as an impact at all.")]
        [Min(0f)] public float approachThreshold = 1.5f;
        [Tooltip("Extra distance at which a touch counts (helps with fast, sub-stepped movement).")]
        [Min(0f)] public float contactMargin = 0.08f;

        [Header("Knockback")]
        [Min(0f)] public float impactKnockback = 5f;
        [Tooltip("Extra knockback per unit of approach speed.")]
        [Min(0f)] public float knockbackSpeedScale = 0.8f;

        [Header("Player side")]
        public ImpactSelfDamageMode selfDamageMode = ImpactSelfDamageMode.EnemyContactOnly;
        [Min(0f)] public float additionalImpactSelfDamage = 5f;
        [Tooltip("Self-damage as a fraction of the impact damage dealt.")]
        [Min(0f)] public float impactSelfDamageScale;
        [Tooltip("Off: a successful ram stops that enemy's contact damage for its cooldown, so ramming is safe.")]
        public bool enemyContactDamageOnImpact = true;

        public PlayerMotor2D Motor { get; private set; }
        public DamageEvent LastImpact { get; private set; }
        public bool HasImpact { get; private set; }
        public float LastImpactTime { get; private set; } = -999f;
        /// <summary>Highest approach speed toward any touching enemy this frame (debug).</summary>
        public float CurrentApproachSpeed { get; private set; }

        readonly HitTracker _cooldowns = new HitTracker();
        readonly List<DummyEnemy> _touching = new List<DummyEnemy>();

        void Awake() => Motor = GetComponent<PlayerMotor2D>();

        public void ResetState()
        {
            _cooldowns.Clear();
            HasImpact = false;
        }

        void Update()
        {
            CurrentApproachSpeed = 0f;
            PlayerHealth health = PlayerHealth.Instance;
            if (!bodyDamageEnabled || Motor == null || (health != null && health.IsDead)) return;

            Vector2 pos = Motor.Position;
            CombatQueries.EnemiesInCircle(pos, Motor.Radius + contactMargin, _touching);
            foreach (DummyEnemy enemy in _touching)
            {
                if (enemy.IsControlled) continue;
                Vector2 into = enemy.Position - pos;
                into = into.sqrMagnitude > 1e-8f ? into.normalized : Motor.Velocity.normalized;
                Vector2 relative = Motor.Velocity - enemy.Velocity;
                float approach = Mathf.Max(0f, Vector2.Dot(relative, into));
                CurrentApproachSpeed = Mathf.Max(CurrentApproachSpeed, approach);
                if (approach < approachThreshold || !_cooldowns.CanHit(enemy)) continue;
                Impact(enemy, into, approach);
            }
        }

        void Impact(DummyEnemy enemy, Vector2 into, float approach)
        {
            float speedDamage = Mathf.Max(0f, approach - minimumImpactSpeed) * impactSpeedDamageScale;
            float damage = Mathf.Min(baseImpactDamage + speedDamage, maximumImpactDamage);
            float self = selfDamageMode == ImpactSelfDamageMode.ContactPlusImpactSelfDamage
                ? additionalImpactSelfDamage + damage * impactSelfDamageScale
                : 0f;

            var e = new DamageEvent
            {
                kind = DamageKind.BodyImpact,
                sourceName = SourceName,
                source = this,
                baseDamage = baseImpactDamage,
                distanceMultiplier = 1f,
                pierceMultiplier = 1f,
                otherMultiplier = 1f,
                finalDamage = damage,
                sourcePosition = Motor.Position,
                hitPoint = Motor.Position + into * Motor.Radius,
                hitDirection = into,
                impactSpeed = approach,
                speedDamage = Mathf.Min(speedDamage, Mathf.Max(0f, maximumImpactDamage - baseImpactDamage)),
                playerDamageTaken = self,
                knockback = into * (impactKnockback + approach * knockbackSpeedScale),
            };
            _cooldowns.Record(enemy, impactCooldownPerEnemy);
            if (!CombatDamage.Apply(enemy, ref e)) return;

            LastImpact = e;
            HasImpact = true;
            LastImpactTime = Time.time;
            if (!enemyContactDamageOnImpact) enemy.Contact.Suppress(Mathf.Max(impactCooldownPerEnemy, enemy.Contact.Cooldown));

            if (self > 0f && PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.TakeDamage(new DamageEvent
                {
                    kind = DamageKind.ImpactSelfDamage,
                    sourceName = "Impact Self-Damage",
                    source = this,
                    baseDamage = self,
                    distanceMultiplier = 1f,
                    pierceMultiplier = 1f,
                    otherMultiplier = 1f,
                    finalDamage = self,
                    sourcePosition = enemy.Position,
                    hitPoint = e.hitPoint,
                    hitDirection = -into,
                }, true);
            }
        }
    }
}
