using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Attack-geometry queries: which enemies does a line / wide line / circle / cone / box touch, and where does a
    /// ray meet terrain. Enemies are circles and terrain is <see cref="Surface2D"/> geometry, so no physics layers
    /// or colliders are involved. Results are appended to caller-owned lists sorted as documented.
    /// </summary>
    public static class CombatQueries
    {
        public struct LineHit
        {
            public DummyEnemy enemy;
            /// <summary>Distance along the line to the closest approach to the enemy's centre.</summary>
            public float along;
            public Vector2 point;
        }

        // ---------------------------------------------------------------- terrain

        /// <summary>First terrain intersection along a ray (distance = maxDistance if none).</summary>
        public static bool RaycastTerrain(Vector2 origin, Vector2 dir, float maxDistance, out float distance, out Vector2 point) =>
            RaycastTerrain(origin, dir, maxDistance, out distance, out point, out _);

        /// <summary>As above, plus the surface's outward normal at the hit.</summary>
        public static bool RaycastTerrain(Vector2 origin, Vector2 dir, float maxDistance, out float distance, out Vector2 point,
            out Vector2 normal)
        {
            distance = maxDistance;
            point = origin + dir * maxDistance;
            normal = -dir;
            bool any = false;
            var all = Surface2D.All;
            for (int i = 0; i < all.Count; i++)
            {
                Surface2D s = all[i];
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) continue;
                if (s.Raycast(origin, dir, distance, out SurfaceSample h) && h.distance < distance)
                {
                    distance = h.distance;
                    point = h.point;
                    normal = h.normal;
                    any = true;
                }
            }
            return any;
        }

        public static bool LineOfSight(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-5f) return true;
            return !RaycastTerrain(from, d / len, len, out _, out _);
        }

        /// <summary>Push a circle out of any terrain it overlaps. Returns the combined push normal (zero if none).</summary>
        public static Vector2 PushOutOfTerrain(ref Vector2 position, float radius)
        {
            Vector2 total = Vector2.zero;
            var all = Surface2D.All;
            for (int i = 0; i < all.Count; i++)
            {
                Surface2D s = all[i];
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry || !s.BoundsWithin(position, radius)) continue;
                SurfaceSample c = s.GetClosestSample(position);
                float gap = c.signedDistance - radius;
                if (gap >= 0f) continue;
                position += c.separation * -gap;
                total += c.separation;
            }
            return total.sqrMagnitude > 1e-8f ? total.normalized : Vector2.zero;
        }

        // ---------------------------------------------------------------- enemies

        /// <summary>
        /// Enemies whose circle comes within halfWidth of the segment origin..origin+dir*length, sorted by distance along.
        /// </summary>
        public static void EnemiesAlongLine(Vector2 origin, Vector2 dir, float length, float halfWidth, List<LineHit> results)
        {
            results.Clear();
            var all = DummyEnemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                DummyEnemy e = all[i];
                if (!e.IsTargetable) continue;
                Vector2 to = e.Position - origin;
                float along = Mathf.Clamp(Vector2.Dot(to, dir), 0f, length);
                Vector2 closest = origin + dir * along;
                float reach = halfWidth + e.Radius;
                if ((e.Position - closest).sqrMagnitude > reach * reach) continue;
                results.Add(new LineHit { enemy = e, along = along, point = closest });
            }
            results.Sort((a, b) => a.along.CompareTo(b.along));
        }

        /// <summary>Enemies overlapping a circle, sorted by distance from the centre.</summary>
        public static void EnemiesInCircle(Vector2 center, float radius, List<DummyEnemy> results)
        {
            results.Clear();
            var all = DummyEnemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                DummyEnemy e = all[i];
                if (!e.IsTargetable) continue;
                float reach = radius + e.Radius;
                if ((e.Position - center).sqrMagnitude <= reach * reach) results.Add(e);
            }
            results.Sort((a, b) => (a.Position - center).sqrMagnitude.CompareTo((b.Position - center).sqrMagnitude));
        }

        /// <summary>Enemies overlapping a cone (pie slice), sorted by distance from the origin.</summary>
        public static void EnemiesInCone(Vector2 origin, Vector2 dir, float range, float halfAngle, List<DummyEnemy> results)
        {
            results.Clear();
            var all = DummyEnemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                DummyEnemy e = all[i];
                if (!e.IsTargetable) continue;
                Vector2 to = e.Position - origin;
                float dist = to.magnitude;
                if (dist - e.Radius > range) continue;
                if (dist > e.Radius)
                {
                    // Widen the cone by the angle the enemy's circle subtends.
                    float slack = Mathf.Asin(Mathf.Clamp01(e.Radius / dist)) * Mathf.Rad2Deg;
                    if (Vector2.Angle(dir, to) > halfAngle + slack) continue;
                }
                results.Add(e);
            }
            results.Sort((a, b) => (a.Position - origin).sqrMagnitude.CompareTo((b.Position - origin).sqrMagnitude));
        }

        /// <summary>Enemies overlapping an axis-aligned box.</summary>
        public static void EnemiesInBox(Vector2 center, Vector2 halfExtents, List<DummyEnemy> results)
        {
            results.Clear();
            var all = DummyEnemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                DummyEnemy e = all[i];
                if (!e.IsTargetable) continue;
                Vector2 d = e.Position - center;
                Vector2 clamped = new Vector2(Mathf.Clamp(d.x, -halfExtents.x, halfExtents.x), Mathf.Clamp(d.y, -halfExtents.y, halfExtents.y));
                if ((d - clamped).sqrMagnitude <= e.Radius * e.Radius) results.Add(e);
            }
        }
    }
}
