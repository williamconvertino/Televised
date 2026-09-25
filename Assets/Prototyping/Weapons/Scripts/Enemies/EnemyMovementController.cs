using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum EnemyMovementMode
    {
        Static,
        Patrol,
        Wander,
        SeekPlayer,
        /// <summary>Circles a point (Orbit Center Offset from the spawn point).</summary>
        Orbit,
    }

    public enum EnemyMovementOverride
    {
        Individual,
        ForceStatic,
        ForcePatrol,
        ForceWander,
        ForceSeek,
    }

    /// <summary>
    /// Deliberately simple enemy steering. Produces a desired velocity; <see cref="DummyEnemy"/> integrates it with
    /// knockback and terrain push-out. Enemies float (no gravity) so they can sit anywhere in the sandbox.
    /// Mode priority: global override (EnemyManager) &gt; group override &gt; this component's mode.
    /// </summary>
    public class EnemyMovementController : MonoBehaviour
    {
        [SerializeField] EnemyMovementMode mode = EnemyMovementMode.Static;
        [SerializeField, Min(0f)] float moveSpeed = 2.5f;

        [Header("Static")]
        [Tooltip("Drift back to the spawn point after being knocked away (keeps range tests consistent).")]
        [SerializeField] bool returnToSpawn = true;

        [Header("Patrol")]
        [Tooltip("Direction of the patrol line (normalized at runtime). The line is centred on the spawn point.")]
        [SerializeField] Vector2 patrolAxis = Vector2.right;
        [SerializeField, Min(0f)] float patrolDistance = 5f;

        [Header("Wander")]
        [Tooltip("Wanders within this distance of its spawn point.")]
        [SerializeField, Min(0.5f)] float wanderRadius = 4f;
        [SerializeField] Vector2 wanderChangeInterval = new Vector2(0.8f, 2.2f);

        [Header("Seek")]
        [Tooltip("Starts chasing when the player is this close; otherwise drifts home.")]
        [SerializeField, Min(0f)] float seekRange = 14f;

        [Header("Orbit")]
        [Tooltip("From the spawn point to the orbit centre; its length is the orbit radius.")]
        [SerializeField] Vector2 orbitCenterOffset = new Vector2(0f, -3f);
        [Tooltip("Degrees per second (positive = counter-clockwise).")]
        [SerializeField] float orbitSpeed = 30f;

        public EnemyMovementMode Mode { get => mode; set => mode = value; }
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
        public float PatrolDistance { get => patrolDistance; set => patrolDistance = Mathf.Max(0f, value); }
        public Vector2 PatrolAxis => patrolAxis.sqrMagnitude > 1e-6f ? patrolAxis.normalized : Vector2.right;

        public EnemyMovementMode EffectiveMode
        {
            get
            {
                EnemyManager m = EnemyManager.Instance;
                if (m != null && m.movementOverride != EnemyMovementOverride.Individual) return FromOverride(m.movementOverride);
                EnemySpawnGroup group = _group;
                if (group != null && group.movementOverride != EnemyMovementOverride.Individual) return FromOverride(group.movementOverride);
                return mode;
            }
        }

        static EnemyMovementMode FromOverride(EnemyMovementOverride o)
        {
            switch (o)
            {
                case EnemyMovementOverride.ForcePatrol: return EnemyMovementMode.Patrol;
                case EnemyMovementOverride.ForceWander: return EnemyMovementMode.Wander;
                case EnemyMovementOverride.ForceSeek: return EnemyMovementMode.SeekPlayer;
                default: return EnemyMovementMode.Static;
            }
        }

        EnemySpawnGroup _group;
        int _patrolSign = 1;
        float _orbitAngle;
        bool _orbitInit;
        Vector2 _wanderDir;
        float _wanderTimer;

        void Awake() => _group = GetComponentInParent<EnemySpawnGroup>(true);

        public void ResetState()
        {
            _patrolSign = 1;
            _wanderTimer = 0f;
            _orbitInit = false;
        }

        /// <summary>Terrain pushed the enemy out along n: turn around / pick a new direction.</summary>
        public void NotifyBlocked(Vector2 n)
        {
            if (Vector2.Dot(_wanderDir, n) < 0f) _wanderDir = Vector2.Reflect(_wanderDir, n);
            if (Vector2.Dot(PatrolAxis * _patrolSign, n) < -0.3f) _patrolSign = -_patrolSign;
        }

        public Vector2 DesiredVelocity(DummyEnemy e, float dt)
        {
            EnemyManager m = EnemyManager.Instance;
            if (m != null && m.freezeEnemies) return Vector2.zero;

            Vector2 pos = e.Position, home = e.SpawnPosition;
            switch (EffectiveMode)
            {
                case EnemyMovementMode.Patrol:
                {
                    Vector2 axis = PatrolAxis;
                    float along = Vector2.Dot(pos - home, axis);
                    float half = patrolDistance * 0.5f;
                    if (along > half) _patrolSign = -1;
                    else if (along < -half) _patrolSign = 1;
                    // Steer back onto the line after knockback.
                    Vector2 offLine = (home + axis * along) - pos;
                    return axis * (_patrolSign * moveSpeed) + Vector2.ClampMagnitude(offLine * 3f, moveSpeed);
                }
                case EnemyMovementMode.Wander:
                {
                    _wanderTimer -= dt;
                    Vector2 toHome = home - pos;
                    if (toHome.magnitude > wanderRadius)
                    {
                        _wanderDir = toHome.normalized;
                        _wanderTimer = Mathf.Max(_wanderTimer, 0.4f);
                    }
                    else if (_wanderTimer <= 0f || _wanderDir == Vector2.zero)
                    {
                        _wanderDir = Random.insideUnitCircle.normalized;
                        _wanderTimer = Random.Range(wanderChangeInterval.x, wanderChangeInterval.y);
                    }
                    return _wanderDir * (moveSpeed * 0.8f);
                }
                case EnemyMovementMode.SeekPlayer:
                {
                    PlayerHealth p = PlayerHealth.Instance;
                    if (p != null && !p.IsDead)
                    {
                        Vector2 toPlayer = p.Motor.Position - pos;
                        if (toPlayer.magnitude <= seekRange)
                            return toPlayer.normalized * moveSpeed;
                    }
                    return ReturnHome(pos, home, 0.5f);
                }
                case EnemyMovementMode.Orbit:
                {
                    Vector2 center = home + orbitCenterOffset;
                    float r = orbitCenterOffset.magnitude;
                    if (!_orbitInit)
                    {
                        Vector2 from = home - center;
                        _orbitAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
                        _orbitInit = true;
                    }
                    _orbitAngle += orbitSpeed * dt;
                    float a = _orbitAngle * Mathf.Deg2Rad;
                    Vector2 target = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    float linear = Mathf.Abs(orbitSpeed * Mathf.Deg2Rad * r);
                    return Vector2.ClampMagnitude((target - pos) / Mathf.Max(dt, 1e-4f), linear + moveSpeed);
                }
                default:
                    return returnToSpawn ? ReturnHome(pos, home, 1f) : Vector2.zero;
            }
        }

        Vector2 ReturnHome(Vector2 pos, Vector2 home, float speedScale)
        {
            Vector2 d = home - pos;
            if (d.sqrMagnitude < 0.0025f) return Vector2.zero;
            return Vector2.ClampMagnitude(d * 2.5f, Mathf.Max(0.5f, moveSpeed * speedScale));
        }

        void OnDrawGizmosSelected()
        {
            Vector2 home = Application.isPlaying && TryGetComponent(out DummyEnemy e) ? e.SpawnPosition : (Vector2)transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            switch (mode)
            {
                case EnemyMovementMode.Patrol:
                    Gizmos.DrawLine(home - PatrolAxis * patrolDistance * 0.5f, home + PatrolAxis * patrolDistance * 0.5f);
                    break;
                case EnemyMovementMode.Wander:
                    Gizmos.DrawWireSphere(home, wanderRadius);
                    break;
                case EnemyMovementMode.SeekPlayer:
                    Gizmos.DrawWireSphere(home, seekRange);
                    break;
                case EnemyMovementMode.Orbit:
                    Gizmos.DrawWireSphere(home + orbitCenterOffset, orbitCenterOffset.magnitude);
                    break;
            }
        }

        /// <summary>Patrol line / wander area / seek radius, for the attack-geometry debug view.</summary>
        public void DrawDebug(DummyEnemy e, Color c)
        {
            Vector2 home = e.SpawnPosition;
            switch (EffectiveMode)
            {
                case EnemyMovementMode.Patrol:
                    DebugLines.Line(home - PatrolAxis * patrolDistance * 0.5f, home + PatrolAxis * patrolDistance * 0.5f, c);
                    break;
                case EnemyMovementMode.Wander:
                    DebugLines.Circle(home, wanderRadius, c, 24);
                    break;
                case EnemyMovementMode.Orbit:
                    DebugLines.Circle(home + orbitCenterOffset, orbitCenterOffset.magnitude, c, 32);
                    break;
            }
        }
    }
}
