using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// A moving projectile. Plain data simulated by <see cref="ProjectileSystem"/>; the weapon that fires it fills
    /// in the damage / range / pierce settings (by reference, so tuning mid-flight applies).
    /// </summary>
    public class Projectile
    {
        public PrototypeWeapon weapon;
        public string sourceName;

        public Vector2 position;
        public Vector2 velocity;
        public float gravity;
        public float radius = 0.1f;
        /// <summary>Travel the whole range on the first frame (hitscan).</summary>
        public bool instant;
        public bool stopsOnTerrain = true;

        public float baseDamage;
        public RangeSettings range;
        public DistanceFalloff distanceFalloff;
        public PierceSettings pierce;
        public KnockbackSettings knockback;

        /// <summary>Ricochets left. Each bounce off terrain, or off an enemy once piercing is used up, spends one.</summary>
        public int bounces;
        public bool bounceOffEnemies = true;
        /// <summary>Speed kept per bounce.</summary>
        public float bounceRestitution = 0.8f;
        /// <summary>Damage multiplier applied per bounce already made (1 = no change).</summary>
        public float bounceDamageMultiplier = 1f;
        /// <summary>Below this speed after a bounce, the projectile stops.</summary>
        public float minBounceSpeed = 2f;

        public Color color = Color.white;
        /// <summary>World length of the trail behind the head (0 = none).</summary>
        public float trailLength;
        public float trailWidth = 0.08f;
        /// <summary>Seconds the trail lingers after the projectile ends.</summary>
        public float lingerTime = 0.12f;

        // Runtime.
        public Vector2 origin;
        public float traveled;
        public int hitCount;
        public int bouncesUsed;
        public bool dead;
        public float deathTime;
        public readonly HashSet<DummyEnemy> hitEnemies = new HashSet<DummyEnemy>();
        public readonly List<Vector2> path = new List<Vector2>();
        public readonly List<float> pathDistance = new List<float>();
    }

    /// <summary>
    /// Simulates, collides and draws all projectiles. Range = total distance traveled (so arcing shots don't live
    /// forever); collision is swept per frame so very fast shots can't tunnel through enemies.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ProjectileSystem : MonoBehaviour
    {
        readonly List<Projectile> _live = new List<Projectile>();
        readonly List<CombatQueries.LineHit> _hits = new List<CombatQueries.LineHit>();

        struct PathRecord
        {
            public Vector2[] points;
            public float time;
            public Color color;
        }

        readonly List<PathRecord> _paths = new List<PathRecord>();
        const float PathShowTime = 2.5f;

        public int Count => _live.Count;

        public Projectile Spawn(Projectile p)
        {
            p.origin = p.position;
            p.path.Add(p.position);
            p.pathDistance.Add(0f);
            _live.Add(p);
            CombatTelemetry.Shot(p.sourceName);
            return p;
        }

        public void Clear()
        {
            _live.Clear();
            _paths.Clear();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Projectile p = _live[i];
                if (!p.dead) Simulate(p, dt);
                if (p.dead && Time.time - p.deathTime > p.lingerTime)
                {
                    if (CombatDebug.ProjectilePaths && p.path.Count > 1)
                        _paths.Add(new PathRecord { points = p.path.ToArray(), time = Time.time, color = p.color });
                    _live.RemoveAt(i);
                }
            }
        }

        void LateUpdate()
        {
            foreach (Projectile p in _live) Draw(p);
            DrawPaths();
        }

        // ---------------------------------------------------------------- simulation

        void Simulate(Projectile p, float dt)
        {
            float maxRange = p.range != null ? p.range.maxRange : 20f;
            if (p.instant)
            {
                Vector2 dir = p.velocity.sqrMagnitude > 1e-8f ? p.velocity.normalized : Vector2.right;
                Sweep(p, dir * (maxRange - p.traveled));
                if (!p.dead) Kill(p);
                return;
            }

            p.velocity += Vector2.down * (p.gravity * dt);
            Vector2 step = p.velocity * dt;
            float remaining = maxRange - p.traveled;
            bool expires = false;
            if (step.magnitude >= remaining)
            {
                step = step.normalized * Mathf.Max(0f, remaining);
                expires = true;
            }
            Sweep(p, step);
            if (expires && !p.dead) Kill(p);
        }

        void Sweep(Projectile p, Vector2 step)
        {
            float len = step.magnitude;
            if (len < 1e-6f) return;
            Vector2 dir = step / len;
            Vector2 start = p.position;

            float end = len;
            Vector2 wallPoint = default, wallNormal = default;
            bool hitTerrain = p.stopsOnTerrain && CombatQueries.RaycastTerrain(start, dir, len, out end, out wallPoint, out wallNormal);

            CombatQueries.EnemiesAlongLine(start, dir, end, p.radius, _hits);
            foreach (CombatQueries.LineHit h in _hits)
            {
                if (p.hitEnemies.Contains(h.enemy)) continue;
                p.hitEnemies.Add(h.enemy);
                float distance = p.traveled + h.along;
                float maxRange = p.range != null ? p.range.maxRange : 20f;
                float bounceMult = p.bouncesUsed > 0 ? Mathf.Pow(p.bounceDamageMultiplier, p.bouncesUsed) : 1f;
                var e = DamageEvent.Weapon(p.sourceName, p.weapon, p.baseDamage, distance, distance / maxRange,
                    p.distanceFalloff, p.hitCount, p.pierce, bounceMult);
                e.sourcePosition = p.origin;
                e.hitPoint = h.point;
                e.hitDirection = dir;
                if (p.knockback != null)
                    e.knockback = p.knockback.Compute(p.origin, dir, h.point, h.enemy.Position, e.FalloffMultiplier);
                if (!CombatDamage.Apply(h.enemy, ref e)) continue;

                if (p.hitCount == 0) CombatTelemetry.ShotHit(p.sourceName);
                p.hitCount++;
                if (p.pierce != null && p.hitCount < p.pierce.MaxTargets) continue;

                Advance(p, dir, h.along);
                if (p.bounceOffEnemies && p.bounces > 0)
                {
                    // Ricochet off the enemy's circle.
                    Vector2 n = h.point - h.enemy.Position;
                    n = n.sqrMagnitude > 1e-8f ? n.normalized : -dir;
                    p.position = h.enemy.Position + n * (h.enemy.Radius + p.radius + 0.02f);
                    Bounce(p, n);
                }
                else
                {
                    Kill(p);
                }
                return;
            }

            Advance(p, dir, end);
            if (hitTerrain)
            {
                if (p.bounces > 0)
                {
                    p.position = wallPoint + wallNormal * Mathf.Max(p.radius, 0.02f);
                    Bounce(p, wallNormal);
                }
                else
                {
                    Kill(p);
                }
                return;
            }

            // Bouncy shots use their full radius against terrain (the sweep above only uses the centre line).
            if (p.bounces > 0 && p.stopsOnTerrain)
            {
                Vector2 n = CombatQueries.PushOutOfTerrain(ref p.position, p.radius);
                if (n != Vector2.zero && Vector2.Dot(p.velocity, n) < 0f) Bounce(p, n);
            }
        }

        static void Bounce(Projectile p, Vector2 normal)
        {
            p.bounces--;
            p.bouncesUsed++;
            p.velocity = Vector2.Reflect(p.velocity, normal) * p.bounceRestitution;
            p.hitEnemies.Clear(); // a ricochet may come back and hit the same enemy again
            p.path.Add(p.position);
            p.pathDistance.Add(p.traveled);
            if (p.velocity.magnitude < p.minBounceSpeed) Kill(p);
        }

        static void Advance(Projectile p, Vector2 dir, float distance)
        {
            p.position += dir * distance;
            p.traveled += distance;
            p.path.Add(p.position);
            p.pathDistance.Add(p.traveled);
        }

        static void Kill(Projectile p)
        {
            p.dead = true;
            p.deathTime = Time.time;
        }

        // ---------------------------------------------------------------- drawing

        readonly List<Vector2> _trailPts = new List<Vector2>();
        readonly List<Color> _trailCols = new List<Color>();

        void Draw(Projectile p)
        {
            float linger = p.dead ? 1f - Mathf.Clamp01((Time.time - p.deathTime) / Mathf.Max(0.01f, p.lingerTime)) : 1f;
            RangeSettings r = p.range ?? new RangeSettings();

            if (!p.dead)
            {
                Color head = p.color;
                head.a *= r.Fade(p.traveled);
                CombatShapes.Disc(p.position, p.radius, head, p.radius > 0.25f ? 28 : 12);
            }

            if (p.trailLength <= 0f || p.path.Count < 2) return;
            // Walk back along the recorded path for trailLength world units.
            _trailPts.Clear();
            _trailCols.Clear();
            float headDist = p.traveled;
            for (int i = p.path.Count - 1; i >= 0; i--)
            {
                float d = p.pathDistance[i];
                float behind = headDist - d;
                if (behind > p.trailLength && i < p.path.Count - 1)
                {
                    // Clip the last segment to the trail length.
                    float d1 = p.pathDistance[i + 1];
                    float t = Mathf.InverseLerp(d1, d, headDist - p.trailLength);
                    AddTrailPoint(Vector2.Lerp(p.path[i + 1], p.path[i], t), headDist - p.trailLength, 1f);
                    break;
                }
                AddTrailPoint(p.path[i], d, Mathf.Clamp01(behind / p.trailLength));
            }
            if (_trailPts.Count >= 2) CombatShapes.Polyline(_trailPts, p.trailWidth, _trailCols);

            void AddTrailPoint(Vector2 pt, float dist, float behind01)
            {
                Color c = p.color;
                c.a *= (1f - behind01) * r.Fade(dist) * linger;
                _trailPts.Add(pt);
                _trailCols.Add(c);
            }
        }

        void DrawPaths()
        {
            if (!CombatDebug.ProjectilePaths)
            {
                _paths.Clear();
                return;
            }
            foreach (Projectile p in _live)
                for (int i = 0; i + 1 < p.path.Count; i++) DebugLines.Line(p.path[i], p.path[i + 1], WithAlpha(p.color, 0.6f));
            for (int k = _paths.Count - 1; k >= 0; k--)
            {
                float age = Time.time - _paths[k].time;
                if (age > PathShowTime)
                {
                    _paths.RemoveAt(k);
                    continue;
                }
                Color c = WithAlpha(_paths[k].color, 0.6f * (1f - age / PathShowTime));
                Vector2[] pts = _paths[k].points;
                for (int i = 0; i + 1 < pts.Length; i++) DebugLines.Line(pts[i], pts[i + 1], c);
                DebugLines.Cross(pts[pts.Length - 1], 0.15f, c);
            }
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
