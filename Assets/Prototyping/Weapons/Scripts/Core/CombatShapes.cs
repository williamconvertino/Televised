using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;
using UnityEngine.Rendering;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Immediate-mode filled shapes (beams, cones, rings, discs, projectiles) for the weapon visuals. Works like
    /// <see cref="DebugLines"/>: any script adds shapes during Update / LateUpdate and everything is flushed into one
    /// vertex-coloured triangle mesh at the end of the frame. Per-vertex alpha is what makes range fades cheap.
    /// </summary>
    [DefaultExecutionOrder(990)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CombatShapes : MonoBehaviour
    {
        static CombatShapes s_instance;

        [SerializeField] int sortingOrder = 15;

        readonly List<Vector3> _verts = new List<Vector3>(8192);
        readonly List<Color> _colors = new List<Color>(8192);
        readonly List<int> _tris = new List<int>(16384);
        Mesh _mesh;

        public static bool Available => s_instance != null && s_instance.isActiveAndEnabled;

        void Awake()
        {
            s_instance = this;
            _mesh = new Mesh { name = "CombatShapes", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_mesh != null) Destroy(_mesh);
        }

        void LateUpdate()
        {
            _mesh.Clear();
            if (_verts.Count > 0)
            {
                _mesh.SetVertices(_verts);
                _mesh.SetColors(_colors);
                _mesh.SetTriangles(_tris, 0, false);
                _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
        }

        // ---------------------------------------------------------------- primitives

        static int V(Vector2 p, Color c)
        {
            var i = s_instance;
            i._verts.Add(p);
            i._colors.Add(c);
            return i._verts.Count - 1;
        }

        static void T(int a, int b, int c)
        {
            var l = s_instance._tris;
            l.Add(a);
            l.Add(b);
            l.Add(c);
        }

        public static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            if (s_instance == null) return;
            T(V(a, ca), V(b, cb), V(c, cc));
        }

        /// <summary>Quad a-b-c-d (any winding).</summary>
        public static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            if (s_instance == null) return;
            int ia = V(a, ca), ib = V(b, cb), ic = V(c, cc), id = V(d, cd);
            T(ia, ib, ic);
            T(ia, ic, id);
        }

        /// <summary>Thick line with a colour gradient from a to b.</summary>
        public static void Line(Vector2 a, Vector2 b, float width, Color ca, Color cb)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-10f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(a - n, a + n, b + n, b - n, ca, ca, cb, cb);
        }

        public static void Line(Vector2 a, Vector2 b, float width, Color c) => Line(a, b, width, c, c);

        /// <summary>
        /// A straight beam from origin along dir for drawLength, whose alpha follows the weapon's range fade:
        /// full up to fadeStartFraction * maxRange, then fading to 0 at maxRange.
        /// </summary>
        public static void RangeBeam(Vector2 origin, Vector2 dir, float drawLength, float width, Color c, RangeSettings range)
        {
            if (drawLength <= 0f) return;
            float fadeStart = range.maxRange * range.fadeStartFraction;
            if (drawLength <= fadeStart)
            {
                Line(origin, origin + dir * drawLength, width, c);
                return;
            }
            Vector2 mid = origin + dir * fadeStart;
            Line(origin, mid, width, c);
            Line(mid, origin + dir * drawLength, width, c, WithAlpha(c, c.a * range.Fade(drawLength)));
        }

        public static void Disc(Vector2 center, float radius, Color c, int segments = 24)
        {
            if (s_instance == null || radius <= 0f) return;
            int ic = V(center, c);
            int first = V(center + new Vector2(radius, 0f), c), prev = first;
            for (int s = 1; s <= segments; s++)
            {
                float a = 2f * Mathf.PI * s / segments;
                int cur = s == segments ? first : V(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c);
                T(ic, prev, cur);
                prev = cur;
            }
        }

        public static void Ring(Vector2 center, float radius, float thickness, Color c, int segments = 48) =>
            Ring(center, radius - thickness * 0.5f, radius + thickness * 0.5f, c, c, segments);

        /// <summary>Annulus with separate inner / outer colours.</summary>
        public static void Ring(Vector2 center, float inner, float outer, Color ci, Color co, int segments = 48)
        {
            if (s_instance == null || outer <= 0f) return;
            inner = Mathf.Max(0f, inner);
            for (int s = 0; s < segments; s++)
            {
                float a0 = 2f * Mathf.PI * s / segments, a1 = 2f * Mathf.PI * (s + 1) / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Quad(center + d0 * inner, center + d0 * outer, center + d1 * outer, center + d1 * inner, ci, co, co, ci);
            }
        }

        /// <summary>
        /// Cone / pie slice from origin, centred on dir with the given half angle. Alpha fades with distance using
        /// the weapon's range fade, and the colour blends from inner to outer.
        /// </summary>
        public static void Fan(Vector2 origin, Vector2 dir, float halfAngle, float radius, Color inner, Color outer,
            RangeSettings range, int segments = 16)
        {
            if (s_instance == null || radius <= 0f) return;
            // Radial bands so the fade and colour gradient read clearly.
            float fadeStart = Mathf.Min(radius, range.maxRange * range.fadeStartFraction);
            float[] rings = { 0f, fadeStart * 0.5f, fadeStart, Mathf.Lerp(fadeStart, radius, 0.5f), radius };
            for (int b = 0; b < rings.Length - 1; b++)
            {
                float r0 = rings[b], r1 = rings[b + 1];
                if (r1 - r0 < 1e-4f) continue;
                Color c0 = BandColor(r0), c1 = BandColor(r1);
                for (int s = 0; s < segments; s++)
                {
                    float a0 = Mathf.Lerp(-halfAngle, halfAngle, (float)s / segments);
                    float a1 = Mathf.Lerp(-halfAngle, halfAngle, (float)(s + 1) / segments);
                    Vector2 d0 = Surface2D.Rotate(dir, a0), d1 = Surface2D.Rotate(dir, a1);
                    Quad(origin + d0 * r0, origin + d0 * r1, origin + d1 * r1, origin + d1 * r0, c0, c1, c1, c0);
                }
            }

            Color BandColor(float r)
            {
                Color c = Color.Lerp(inner, outer, r / radius);
                c.a *= range.Fade(r);
                return c;
            }
        }

        /// <summary>Axis-aligned rectangle with an alpha fade toward both ends of its long axis (holy beam).</summary>
        public static void Column(Vector2 center, Vector2 axis, float halfLength, float halfWidth, Color c, float fadeStartFraction)
        {
            Vector2 side = new Vector2(-axis.y, axis.x) * halfWidth;
            float solid = halfLength * Mathf.Clamp01(fadeStartFraction);
            Color clear = WithAlpha(c, 0f);
            Vector2 a = center - axis * halfLength, b = center - axis * solid, d = center + axis * solid, e = center + axis * halfLength;
            Quad(a - side, a + side, b + side, b - side, clear, clear, c, c);
            Quad(b - side, b + side, d + side, d - side, c, c, c, c);
            Quad(d - side, d + side, e + side, e - side, c, c, clear, clear);
        }

        /// <summary>Thick polyline with per-point colours (trails, ropes, chains).</summary>
        public static void Polyline(IReadOnlyList<Vector2> points, float width, IReadOnlyList<Color> colors)
        {
            for (int i = 0; i + 1 < points.Count; i++)
                Line(points[i], points[i + 1], width, colors[i], colors[i + 1]);
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
