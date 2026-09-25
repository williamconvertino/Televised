using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Damages the player on touch, at most once per cooldown (per enemy). Independent from body impact damage,
    /// which is the player's attack (<see cref="BodyImpactDamage"/>).
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] float contactDamage = 10f;
        [SerializeField, Min(0f)] float contactDamageCooldown = 0.5f;

        public float BaseDamage { get => contactDamage; set => contactDamage = Mathf.Max(0f, value); }
        public float Cooldown { get => contactDamageCooldown; set => contactDamageCooldown = Mathf.Max(0f, value); }
        public bool TouchingPlayer { get; private set; }

        DummyEnemy _enemy;
        float _nextAllowed;

        void Awake() => _enemy = GetComponent<DummyEnemy>();

        public void ResetCooldown() => _nextAllowed = 0f;

        /// <summary>Stop this enemy damaging the player for a while (e.g. right after the player rams it).</summary>
        public void Suppress(float seconds) => _nextAllowed = Mathf.Max(_nextAllowed, Time.time + seconds);

        void Update()
        {
            TouchingPlayer = false;
            PlayerHealth p = PlayerHealth.Instance;
            if (p == null || p.IsDead || _enemy == null || !_enemy.IsAlive || _enemy.IsControlled) return;

            float reach = _enemy.Radius + p.Motor.Radius;
            Vector2 d = p.Motor.Position - _enemy.Position;
            TouchingPlayer = d.sqrMagnitude <= reach * reach;
            if (!TouchingPlayer || Time.time < _nextAllowed) return;

            float mult = EnemyManager.Instance != null ? EnemyManager.Instance.contactDamageMultiplier : 1f;
            float dmg = contactDamage * mult;
            if (dmg <= 0f) return;
            var e = new DamageEvent
            {
                kind = DamageKind.EnemyContact,
                sourceName = "Enemy Contact",
                source = _enemy,
                baseDamage = dmg,
                distanceMultiplier = 1f,
                pierceMultiplier = 1f,
                otherMultiplier = 1f,
                finalDamage = dmg,
                sourcePosition = _enemy.Position,
                hitPoint = _enemy.Position + d.normalized * _enemy.Radius,
                hitDirection = d.normalized,
            };
            if (p.TakeDamage(e)) _nextAllowed = Time.time + contactDamageCooldown;
        }
    }
}
