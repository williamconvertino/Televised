using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// A floating test dummy: a circle with health, a movement mode, contact damage and respawning.
    /// Coordinates its sibling components (<see cref="Health"/>, <see cref="EnemyMovementController"/>,
    /// <see cref="EnemyContactDamage"/>) and owns position, knockback, terrain push-out and the respawn timer.
    /// Enemies are not physics bodies: weapons find them through <see cref="All"/> and <see cref="CombatQueries"/>.
    /// </summary>
    [DefaultExecutionOrder(30)]
    [RequireComponent(typeof(Health), typeof(EnemyMovementController), typeof(EnemyContactDamage))]
    public class DummyEnemy : MonoBehaviour
    {
        public static readonly List<DummyEnemy> All = new List<DummyEnemy>();

        [SerializeField, Min(0.1f)] float radius = 0.5f;
        [Tooltip("Knockback impulses are divided by this.")]
        [SerializeField, Min(0.05f)] float mass = 1f;
        [Tooltip("How quickly knockback velocity decays (1/s).")]
        [SerializeField, Min(0f)] float knockbackDamping = 5f;
        [Tooltip("Pushed out of terrain instead of passing through it.")]
        [SerializeField] bool collideWithTerrain = true;

        [Header("Respawn")]
        [Tooltip("This enemy may auto-respawn (also needs auto-respawn on globally or on its group).")]
        [SerializeField] bool respawnEnabled = true;
        [Tooltip("Seconds; negative = use the global respawn delay.")]
        [SerializeField] float respawnDelayOverride = -1f;

        [Header("Visuals")]
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer core;
        [Tooltip("Tint by movement mode. Off = keep Custom Color.")]
        [SerializeField] bool colorByMode = true;
        [SerializeField] Color customColor = new Color(0.8f, 0.25f, 0.25f);

        public Health Health { get; private set; }
        public EnemyMovementController Movement { get; private set; }
        public EnemyContactDamage Contact { get; private set; }
        public EnemySpawnGroup Group { get; private set; }

        public float Radius { get => radius; set { radius = Mathf.Max(0.1f, value); ApplyScale(); } }
        public float Mass { get => mass; set => mass = Mathf.Max(0.05f, value); }
        public bool RespawnEnabled { get => respawnEnabled; set => respawnEnabled = value; }
        public Vector2 Position => _position;
        /// <summary>Measured velocity (movement + knockback + external pulls).</summary>
        public Vector2 Velocity { get; private set; }
        public Vector2 KnockbackVelocity => _knockback;
        public Vector2 SpawnPosition { get; private set; }
        public bool IsAlive => Health != null && !Health.IsDead;
        /// <summary>Alive, active, and not in a disabled group.</summary>
        public bool IsTargetable => IsAlive && isActiveAndEnabled;
        /// <summary>Some weapon (the chain) is positioning this enemy directly.</summary>
        public bool IsControlled => _controller != null;
        public float RespawnRemaining => Health.IsDead && _respawnAt > 0f ? Mathf.Max(0f, _respawnAt - Time.time) : -1f;

        Vector2 _position;
        Vector2 _knockback;
        object _controller;
        float _respawnAt = -1f;
        float _nextSlamTime;
        Vector2 _lastFramePos;

        void Awake()
        {
            Health = GetComponent<Health>();
            Movement = GetComponent<EnemyMovementController>();
            Contact = GetComponent<EnemyContactDamage>();
            Group = GetComponentInParent<EnemySpawnGroup>(true);
            _position = transform.position;
            _lastFramePos = _position;
            SpawnPosition = _position;
            Health.Died += OnDied;
            ApplyScale();
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (Health != null) Health.Died -= OnDied;
        }

        void OnValidate() => ApplyScale();

        void ApplyScale()
        {
            if (body != null) body.transform.localScale = Vector3.one * (radius * 2f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (dt <= 0f) return;
            EnemyManager m = EnemyManager.Instance;
            if (m != null) Health.SetMultiplier(m.healthMultiplier);

            if (Health.IsDead)
            {
                if (_respawnAt > 0f && Time.time >= _respawnAt) Respawn();
                return;
            }

            // Measured from last frame's end, so chain-driven moves (applied before this Update) count too.
            Vector2 prev = _lastFramePos;
            if (!IsControlled)
            {
                Vector2 move = Movement.DesiredVelocity(this, dt);
                _knockback *= Mathf.Exp(-knockbackDamping * dt);
                if (_knockback.sqrMagnitude < 0.0025f) _knockback = Vector2.zero;
                // Knockback temporarily overrides AI movement instead of being cancelled by it.
                float kb = _knockback.magnitude;
                float moveWeight = 1f - Mathf.Clamp01(kb / Mathf.Max(0.5f, Movement.MoveSpeed * 2f));
                _position += (move * moveWeight + _knockback) * dt;

                if (collideWithTerrain)
                {
                    Vector2 n = CombatQueries.PushOutOfTerrain(ref _position, radius);
                    if (n != Vector2.zero)
                    {
                        // Only the launched (knockback) part of the motion can slam; AI walking into walls can't.
                        TryWallSlam(_knockback, n);
                        float into = Vector2.Dot(_knockback, n);
                        float restitution = m != null ? m.wallSlamRestitution : 0.5f;
                        if (into < 0f) _knockback -= n * (into * (1f + restitution));
                        Movement.NotifyBlocked(n);
                    }
                }
            }
            Velocity = (_position - prev) / dt;
            _lastFramePos = _position;
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (body == null) return;
            Color baseColor = colorByMode ? ModeColor(Movement.EffectiveMode) : customColor;
            float flash = Mathf.Clamp01(1f - (Time.time - Health.LastHitTime) / 0.12f);
            Color c = Color.Lerp(baseColor, Color.white, flash * 0.8f);
            if (Health.Invulnerable) c = Color.Lerp(c, new Color(0.6f, 0.8f, 1f), 0.4f);
            body.color = c;
            if (core != null)
            {
                // Core shrinks as HP drops.
                core.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.55f, Health.Fraction);
                core.color = IsControlled ? new Color(1f, 0.9f, 0.4f) : new Color(0.12f, 0.05f, 0.07f);
            }
        }

        public static Color ModeColor(EnemyMovementMode mode)
        {
            switch (mode)
            {
                case EnemyMovementMode.Patrol: return new Color(0.9f, 0.55f, 0.2f);
                case EnemyMovementMode.Wander: return new Color(0.65f, 0.35f, 0.85f);
                case EnemyMovementMode.SeekPlayer: return new Color(0.95f, 0.2f, 0.25f);
                case EnemyMovementMode.Orbit: return new Color(0.3f, 0.7f, 0.75f);
                default: return new Color(0.7f, 0.35f, 0.35f);
            }
        }

        // ---------------------------------------------------------------- knockback / control

        /// <summary>The attack that last launched this enemy (credited for wall slams).</summary>
        public string LastLaunchedBy { get; private set; }

        public void AddKnockback(Vector2 impulse, string source = null)
        {
            if (IsControlled || !IsAlive) return;
            float mult = EnemyManager.Instance != null ? EnemyManager.Instance.knockbackMultiplier : 1f;
            _knockback += impulse * mult / mass;
            if (!string.IsNullOrEmpty(source)) LastLaunchedBy = source;
        }

        /// <summary>
        /// Wall-slam damage when this enemy hits terrain (normal n) with the given velocity. SNKRX-style: knock enemies
        /// into walls for extra damage.
        /// </summary>
        void TryWallSlam(Vector2 velocity, Vector2 n)
        {
            EnemyManager m = EnemyManager.Instance;
            if (m == null || !m.wallSlamEnabled || Time.time < _nextSlamTime || !IsAlive) return;
            float into = -Vector2.Dot(velocity, n);
            if (into < m.wallSlamMinSpeed) return;
            float speedDamage = (into - m.wallSlamMinSpeed) * m.wallSlamDamagePerSpeed;
            float damage = Mathf.Min(m.wallSlamBaseDamage + speedDamage, m.wallSlamMaxDamage);
            _nextSlamTime = Time.time + m.wallSlamCooldown;
            var e = new DamageEvent
            {
                kind = DamageKind.WallSlam,
                sourceName = "Wall Slam",
                cause = LastLaunchedBy,
                source = this,
                baseDamage = m.wallSlamBaseDamage,
                distanceMultiplier = 1f,
                pierceMultiplier = 1f,
                otherMultiplier = 1f,
                finalDamage = damage,
                sourcePosition = _position,
                hitPoint = _position - n * radius,
                hitDirection = -n,
                impactSpeed = into,
                speedDamage = speedDamage,
            };
            CombatDamage.Apply(this, ref e);
        }

        /// <summary>Take direct control of this enemy's position (skewered by the chain).</summary>
        public bool TryTakeControl(object owner)
        {
            if (_controller != null && _controller != owner) return false;
            _controller = owner;
            _knockback = Vector2.zero;
            if (owner is PrototypeWeapon w) LastLaunchedBy = w.DisplayName; // slams while held count for the holder
            return true;
        }

        /// <summary>
        /// Move a controlled enemy toward a position over dt seconds. It's still pushed out of terrain (and slams
        /// into it if moving fast); returns where it actually ended up and the push normal (zero if none).
        /// </summary>
        public Vector2 SetControlledPosition(object owner, Vector2 position, float dt, out Vector2 pushNormal)
        {
            pushNormal = Vector2.zero;
            if (_controller != owner) return _position;
            Vector2 velocity = dt > 1e-5f ? (position - _position) / dt : Vector2.zero;
            _position = position;
            if (collideWithTerrain)
            {
                pushNormal = CombatQueries.PushOutOfTerrain(ref _position, radius);
                if (pushNormal != Vector2.zero) TryWallSlam(velocity, pushNormal);
            }
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
            return _position;
        }

        public void ReleaseControl(object owner, Vector2 releaseVelocity, string source = null)
        {
            if (_controller != owner) return;
            _controller = null;
            _knockback = releaseVelocity;
            if (!string.IsNullOrEmpty(source)) LastLaunchedBy = source;
        }

        // ---------------------------------------------------------------- death / respawn

        void OnDied(Health h)
        {
            _controller = null;
            _knockback = Vector2.zero;
            SetVisible(false);
            _respawnAt = AutoRespawnActive ? Time.time + RespawnDelay : -1f;
        }

        bool AutoRespawnActive
        {
            get
            {
                if (!respawnEnabled) return false;
                bool global = EnemyManager.Instance == null || EnemyManager.Instance.autoRespawn;
                return Group != null ? Group.ResolveAutoRespawn(global) : global;
            }
        }

        float RespawnDelay => respawnDelayOverride >= 0f ? respawnDelayOverride
            : EnemyManager.Instance != null ? EnemyManager.Instance.respawnDelay : 2f;

        /// <summary>Called when auto-respawn settings change so pending timers follow them.</summary>
        public void RefreshRespawnTimer()
        {
            if (!Health.IsDead) return;
            if (!AutoRespawnActive) _respawnAt = -1f;
            else if (_respawnAt < 0f) _respawnAt = Time.time + RespawnDelay;
        }

        public void Respawn()
        {
            _position = SpawnPosition;
            _lastFramePos = _position;
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
            _knockback = Vector2.zero;
            _controller = null;
            _respawnAt = -1f;
            Velocity = Vector2.zero;
            Health.Restore();
            Movement.ResetState();
            Contact.ResetCooldown();
            SetVisible(true);
        }

        public void Kill() => Health.Kill();

        void SetVisible(bool visible)
        {
            if (body != null) body.enabled = visible;
            if (core != null) core.enabled = visible;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
