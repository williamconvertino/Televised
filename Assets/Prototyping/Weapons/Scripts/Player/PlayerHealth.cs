using System;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Player HP, damage reception, invulnerability window, indestructible mode, death and respawn.
    /// Sits next to <see cref="PlayerMotor2D"/> but only uses its public API (position, Respawn, movement lock).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D))]
    public class PlayerHealth : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Seconds of immunity after taking damage.")]
        [Min(0f)] public float invulnerabilityDuration = 0.5f;
        [Tooltip("Hits still show and are counted, but HP never drops and the player can't die.")]
        public bool indestructible;
        [Tooltip("Seconds between death and respawn (movement is frozen meanwhile).")]
        [Min(0f)] public float respawnDelay = 0.6f;

        public static PlayerHealth Instance { get; private set; }

        public PlayerMotor2D Motor { get; private set; }
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float InvulnerableRemaining => Mathf.Max(0f, _invulnerableUntil - Time.time);
        public int Deaths { get; private set; }
        public DamageEvent LastHit { get; private set; }

        public event Action<DamageEvent> Damaged;
        public event Action Died;
        public event Action Respawned;

        float _invulnerableUntil;
        float _respawnAt;
        float _trailHealth;     // lags behind Current to show the chunk just lost
        float _lastHitTime = -999f;

        void Awake()
        {
            Instance = this;
            Motor = GetComponent<PlayerMotor2D>();
            Current = maxHealth;
            _trailHealth = maxHealth;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (IsDead && Time.time >= _respawnAt) Respawn();
            if (Current > maxHealth) Current = maxHealth;
        }

        void LateUpdate()
        {
            // The trailing chunk holds briefly, then drains down to the real value.
            if (Time.time - _lastHitTime > 0.35f) _trailHealth = Mathf.MoveTowards(_trailHealth, Current, maxHealth * 1.5f * Time.deltaTime);
            if (_trailHealth < Current) _trailHealth = Current;

            if (CombatDebug.PlayerBars) DrawHealthBar();
            if (IsDead || InvulnerableRemaining <= 0f) return;
            // Blinking ring while immune.
            if (Mathf.Repeat(Time.time * 12f, 1f) < 0.5f)
                CombatShapes.Ring(Motor.Position, Motor.Radius + 0.12f, 0.08f, new Color(1f, 0.3f, 0.3f, 0.8f));
        }

        /// <summary>Shown while below max HP, and for a moment after any hit (even when indestructible).</summary>
        void DrawHealthBar()
        {
            float sinceHit = Time.time - _lastHitTime;
            bool hurt = Current < maxHealth - 0.01f || IsDead;
            float alpha = hurt ? 1f : 1f - Mathf.Clamp01((sinceHit - 1.2f) / 0.5f);
            if (alpha <= 0f) return;

            const float width = 1.4f, height = 0.16f;
            Vector2 left = Motor.Position + new Vector2(-width * 0.5f, Motor.Radius + 0.45f);
            float frac = maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;
            float trail = maxHealth > 0f ? Mathf.Clamp01(_trailHealth / maxHealth) : 0f;
            Color fill = frac > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(0.35f, 1f, 0.4f), (frac - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.25f), frac * 2f);
            if (indestructible) fill = new Color(0.55f, 0.8f, 1f);
            float flash = Mathf.Clamp01(1f - sinceHit / 0.15f);
            fill = Color.Lerp(fill, Color.white, flash * 0.7f);

            Bar(left, width + 0.08f, height + 0.08f, new Color(0f, 0f, 0f, 0.65f * alpha), -0.04f);
            Bar(left, width * trail, height, new Color(1f, 1f, 1f, 0.55f * alpha), 0f);
            Bar(left, width * frac, height, CombatShapes.WithAlpha(fill, alpha), 0f);
        }

        /// <summary>Horizontal bar from left (its vertical centre) rightward. inset offsets the start (for borders).</summary>
        public static void Bar(Vector2 left, float width, float height, Color c, float inset)
        {
            if (width <= 0f) return;
            Vector2 a = left + new Vector2(inset, 0f);
            CombatShapes.Line(a, a + Vector2.right * width, height, c);
        }

        /// <summary>
        /// Returns false if the hit was ignored (dead or still immune from the previous hit).
        /// bypassInvulnerability: always lands and doesn't start an immunity window (impact self-damage, so it stacks
        /// with the enemy's ordinary contact damage instead of blocking it).
        /// </summary>
        public bool TakeDamage(DamageEvent e, bool bypassInvulnerability = false)
        {
            if (IsDead || e.finalDamage <= 0f) return false;
            if (!bypassInvulnerability && Time.time < _invulnerableUntil) return false;
            e.blocked = indestructible;
            _lastHitTime = Time.time;
            if (!indestructible) Current = Mathf.Max(0f, Current - e.finalDamage);
            e.killed = Current <= 0f;
            if (!bypassInvulnerability) _invulnerableUntil = Time.time + invulnerabilityDuration;
            LastHit = e;
            CombatTelemetry.PlayerDamaged(e);
            Damaged?.Invoke(e);
            if (e.killed) Die();
            return true;
        }

        void Die()
        {
            IsDead = true;
            Deaths++;
            _respawnAt = Time.time + respawnDelay;
            Motor.AddMovementLock(this);
            Died?.Invoke();
        }

        public void Respawn()
        {
            IsDead = false;
            Motor.RemoveMovementLock(this);
            Motor.Respawn();
            RestoreHealth();
            Respawned?.Invoke();
        }

        public void RestoreHealth()
        {
            Current = maxHealth;
            _trailHealth = maxHealth;
            _invulnerableUntil = 0f;
        }
    }
}
