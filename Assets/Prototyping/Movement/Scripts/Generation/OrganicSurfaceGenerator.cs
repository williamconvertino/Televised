using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>
    /// Seeded organic blob generator for movement testing.
    /// 1. Evenly spaced control points around a circle (optionally angle-jittered).
    /// 2. Randomized radial distance per control point (+ a low-frequency lobe for asymmetry).
    /// 3. Neighbour smoothing of the radii.
    /// 4. Closed Catmull-Rom spline through the control points, sampled into a polygon.
    /// The same settings + seed always produce the same shape.
    /// </summary>
    public class OrganicSurfaceGenerator : SurfaceShapeBase
    {
        [Header("Organic Shape")]
        public int seed = 1;
        [Range(3, 32)] public int pointCount = 9;
        [Min(0.05f)] public float radius = 2f;
        [Range(0f, 0.9f)] public float radiusVariation = 0.3f;
        [Range(0f, 0.9f)] public float angleJitter = 0.25f;
        [Tooltip("Strength of a single random low-frequency lobe (makes shapes lopsided).")]
        [Range(0f, 0.6f)] public float asymmetry = 0.15f;
        [Min(0.05f)] public float xScale = 1f;
        [Min(0.05f)] public float yScale = 1f;
        [Tooltip("0 = raw random radii, 1 = heavily averaged with neighbours.")]
        [Range(0f, 1f)] public float smoothing = 0.35f;
        [Range(12, 512)] public int sampleCount = 96;
        [Tooltip("Rotation of the whole shape in degrees (in addition to the transform).")]
        public float rotation = 0f;

        public void Configure(int newSeed, float newRadius, float variation, float sx = 1f, float sy = 1f,
            int points = 9, float newSmoothing = 0.35f)
        {
            seed = newSeed;
            radius = newRadius;
            radiusVariation = variation;
            xScale = sx;
            yScale = sy;
            pointCount = points;
            smoothing = newSmoothing;
            Regenerate();
        }

        public void RandomizeSeed()
        {
            seed = Random.Range(0, 100000);
            Regenerate();
        }

        protected override void BuildOutline(List<Vector2> points)
        {
            var rng = new System.Random(seed);
            int n = Mathf.Max(3, pointCount);

            float lobePhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            int lobeFreq = 1 + rng.Next(2);

            var angles = new float[n];
            var radii = new float[n];
            for (int i = 0; i < n; i++)
            {
                float step = Mathf.PI * 2f / n;
                angles[i] = step * i + ((float)rng.NextDouble() * 2f - 1f) * angleJitter * step * 0.5f;
                float r = 1f + ((float)rng.NextDouble() * 2f - 1f) * radiusVariation;
                r += Mathf.Sin(angles[i] * lobeFreq + lobePhase) * asymmetry;
                radii[i] = Mathf.Max(0.25f, r);
            }

            // Neighbour smoothing of the radii (two passes).
            for (int pass = 0; pass < 2; pass++)
            {
                var copy = (float[])radii.Clone();
                for (int i = 0; i < n; i++)
                {
                    float avg = (copy[(i - 1 + n) % n] + copy[(i + 1) % n]) * 0.5f;
                    radii[i] = Mathf.Lerp(copy[i], avg, smoothing);
                }
            }

            float rot = rotation * Mathf.Deg2Rad;
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            var control = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = new Vector2(Mathf.Cos(angles[i]) * xScale, Mathf.Sin(angles[i]) * yScale) * (radii[i] * radius);
                control[i] = new Vector2(p.x * cr - p.y * sr, p.x * sr + p.y * cr);
            }

            // Closed centripetal Catmull-Rom (avoids cusps/self-intersection).
            int perSpan = Mathf.Max(2, Mathf.CeilToInt((float)sampleCount / n));
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = control[(i - 1 + n) % n], p1 = control[i], p2 = control[(i + 1) % n], p3 = control[(i + 2) % n];
                for (int s = 0; s < perSpan; s++)
                    points.Add(CentripetalCatmullRom(p0, p1, p2, p3, (float)s / perSpan));
            }
        }

        static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p0, p1)));
            float t2 = t1 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p1, p2)));
            float t3 = t2 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p2, p3)));
            float u = Mathf.Lerp(t1, t2, t);

            Vector2 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
            Vector2 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
            Vector2 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
            Vector2 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
            Vector2 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
            return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
        }
    }
}
