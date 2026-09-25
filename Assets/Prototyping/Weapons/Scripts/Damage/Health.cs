using System;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Generic enemy health: current / max HP, damage, death. The global health multiplier from
    /// <see cref="EnemyManager"/> scales the max; everything else (respawn, visuals) lives on <see cref="DummyEnemy"/>.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxHealth = 50f;
        [Tooltip("Takes hits (numbers still show) but never loses HP. Useful for DPS testing.")]
        [SerializeField] bool invulnerable;

        /// <summary>Fired for every hit this health receives (including absorbed ones).</summary>
        public static event Action<Health, DamageEvent> AnyDamaged;
        public static event Action<Health> AnyDied;
        public event Action<Health, DamageEvent> Damaged;
        public event Action<Health> Died;

        public float BaseMaxHealth { get => maxHealth; set { maxHealth = Mathf.Max(1f, value); RecomputeMax(true); } }
        public float MaxHealth { get; private set; }
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public bool Invulnerable { get => invulnerable; set => invulnerable = value; }
        public float Fraction => MaxHealth > 0f ? Current / MaxHealth : 0f;
        public float LastHitTime { get; private set; } = -999f;

        float _multiplier = 1f;

        void Awake()
        {
            MaxHealth = maxHealth * _multiplier;
            Current = MaxHealth;
        }

        /// <summary>Apply the global multiplier. keepFraction keeps the same % of HP (otherwise clamps).</summary>
        public void SetMultiplier(float multiplier)
        {
            if (Mathf.Approximately(multiplier, _multiplier)) return;
            _multiplier = Mathf.Max(0.01f, multiplier);
            RecomputeMax(true);
        }

        void RecomputeMax(bool keepFraction)
        {
            float frac = Fraction;
            MaxHealth = maxHealth * _multiplier;
            Current = keepFraction && !IsDead ? MaxHealth * frac : Mathf.Min(Current, MaxHealth);
        }

        /// <summary>Apply damage. Returns false if already dead. Fills e.blocked / e.killed.</summary>
        public bool TakeDamage(ref DamageEvent e)
        {
            if (IsDead) return false;
            LastHitTime = Time.time;
            e.blocked = invulnerable;
            if (!invulnerable)
            {
                Current = Mathf.Max(0f, Current - e.finalDamage);
                e.killed = Current <= 0f;
            }
            Damaged?.Invoke(this, e);
            AnyDamaged?.Invoke(this, e);
            if (e.killed) Kill();
            return true;
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0f;
            IsDead = true;
            Died?.Invoke(this);
            AnyDied?.Invoke(this);
        }

        public void Restore()
        {
            IsDead = false;
            MaxHealth = maxHealth * _multiplier;
            Current = MaxHealth;
        }
    }
}
