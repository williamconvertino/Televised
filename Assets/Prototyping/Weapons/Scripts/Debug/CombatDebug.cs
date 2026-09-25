using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Debug toggles shared by every combat script (set from the DEBUG tab), plus short-lived debug records such as
    /// knockback vectors. Drawing goes through <see cref="DebugLines"/>.
    /// </summary>
    public static class CombatDebug
    {
        public static bool DamageNumbers = true;
        public static bool MergeRapidHits = true;
        public static bool HealthLabels = true;
        public static bool DetailedHitInfo;
        public static bool ProjectilePaths;
        public static bool WeaponRange = true;
        public static bool AttackGeometry;
        public static bool AimAssist;
        public static bool KnockbackVectors;
        public static bool ImpactVelocity;
        public static bool ZoneLabels = true;
        /// <summary>Health bar over the player (when hurt) and the weapon charge / cooldown bar under it.</summary>
        public static bool PlayerBars = true;

        public static readonly Color RangeColor = new Color(1f, 1f, 1f, 0.22f);
        public static readonly Color GeometryColor = new Color(1f, 0.35f, 0.9f, 0.9f);
        public static readonly Color KnockbackColor = new Color(1f, 0.55f, 0.1f, 1f);

        struct KnockbackRecord
        {
            public Vector2 position, vector;
            public float time;
        }

        static readonly List<KnockbackRecord> s_knockbacks = new List<KnockbackRecord>();
        const float KnockbackShowTime = 0.8f;

        public static void RecordKnockback(Vector2 position, Vector2 velocityChange)
        {
            if (!KnockbackVectors) return;
            s_knockbacks.Add(new KnockbackRecord { position = position, vector = velocityChange, time = Time.time });
        }

        /// <summary>Called every frame by the world overlay.</summary>
        public static void DrawRecords()
        {
            for (int i = s_knockbacks.Count - 1; i >= 0; i--)
            {
                KnockbackRecord r = s_knockbacks[i];
                float age = Time.time - r.time;
                if (age > KnockbackShowTime || !KnockbackVectors)
                {
                    s_knockbacks.RemoveAt(i);
                    continue;
                }
                Color c = KnockbackColor;
                c.a = 1f - age / KnockbackShowTime;
                // 0.15 s worth of the velocity change, so vectors stay on screen.
                DebugLines.Arrow(r.position, r.position + r.vector * 0.15f, c, 0.25f);
            }
        }

        public static void ClearRecords() => s_knockbacks.Clear();

        // ---------------------------------------------------------------- common shapes

        public static void Cone(Vector2 origin, Vector2 dir, float halfAngle, float range, Color c)
        {
            Vector2 a = Surface2D.Rotate(dir, -halfAngle), b = Surface2D.Rotate(dir, halfAngle);
            DebugLines.Line(origin, origin + a * range, c);
            DebugLines.Line(origin, origin + b * range, c);
            const int segs = 16;
            Vector2 prev = origin + a * range;
            for (int i = 1; i <= segs; i++)
            {
                Vector2 p = origin + Surface2D.Rotate(dir, Mathf.Lerp(-halfAngle, halfAngle, (float)i / segs)) * range;
                DebugLines.Line(prev, p, c);
                prev = p;
            }
        }

        public static void Capsule(Vector2 a, Vector2 b, float radius, Color c)
        {
            Vector2 d = b - a;
            Vector2 n = d.sqrMagnitude > 1e-8f ? new Vector2(-d.y, d.x).normalized * radius : Vector2.up * radius;
            DebugLines.Line(a + n, b + n, c);
            DebugLines.Line(a - n, b - n, c);
            DebugLines.Circle(a, radius, c, 16);
            DebugLines.Circle(b, radius, c, 16);
        }

        public static void Box(Vector2 center, Vector2 halfExtents, Color c)
        {
            Vector2 a = center + new Vector2(-halfExtents.x, -halfExtents.y), b = center + new Vector2(halfExtents.x, -halfExtents.y);
            Vector2 d = center + new Vector2(-halfExtents.x, halfExtents.y), e = center + new Vector2(halfExtents.x, halfExtents.y);
            DebugLines.Line(a, b, c);
            DebugLines.Line(b, e, c);
            DebugLines.Line(e, d, c);
            DebugLines.Line(d, a, c);
        }
    }
}
