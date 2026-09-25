using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Immediate-mode line drawing that is visible in the Game view (unlike Debug.DrawLine).
    /// Any script can call DebugLines.Line(...) during Update/LateUpdate; everything is
    /// flushed into a single line-topology mesh at the end of the frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DebugLines : MonoBehaviour
    {
        static DebugLines s_instance;

        [SerializeField] int sortingOrder = 100;

        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Color> _colors = new List<Color>(4096);
        readonly List<int> _indices = new List<int>(4096);
        Mesh _mesh;

        public static bool Available => s_instance != null && s_instance.isActiveAndEnabled;

        void Awake()
        {
            s_instance = this;
            _mesh = new Mesh { name = "DebugLines", hideFlags = HideFlags.DontSave };
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
                _mesh.SetIndices(_indices, MeshTopology.Lines, 0, false);
                _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
            _verts.Clear();
            _colors.Clear();
            _indices.Clear();
        }

        // ---------------------------------------------------------------- API

        public static void Line(Vector2 a, Vector2 b, Color c)
        {
            if (s_instance == null) return;
            var i = s_instance;
            i._indices.Add(i._verts.Count);
            i._verts.Add(a);
            i._colors.Add(c);
            i._indices.Add(i._verts.Count);
            i._verts.Add(b);
            i._colors.Add(c);
        }

        public static void Ray(Vector2 origin, Vector2 dir, Color c) => Line(origin, origin + dir, c);

        public static void Arrow(Vector2 from, Vector2 to, Color c, float head = 0.15f)
        {
            Line(from, to, c);
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-8f) return;
            d.Normalize();
            float h = Mathf.Min(head, Vector2.Distance(from, to) * 0.4f);
            Line(to, to - Surface2D.Rotate(d, 25f) * h, c);
            Line(to, to - Surface2D.Rotate(d, -25f) * h, c);
        }

        public static void Circle(Vector2 center, float radius, Color c, int segments = 32)
        {
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int s = 1; s <= segments; s++)
            {
                float a = 2f * Mathf.PI * s / segments;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Line(prev, p, c);
                prev = p;
            }
        }

        public static void Cross(Vector2 p, float size, Color c)
        {
            Line(p + new Vector2(-size, -size), p + new Vector2(size, size), c);
            Line(p + new Vector2(-size, size), p + new Vector2(size, -size), c);
        }

        public static void SurfaceOutline(Surface2D s, Color c)
        {
            if (s == null || !s.HasGeometry) return;
            int n = s.PointCount;
            for (int i = 0; i < n; i++) Line(s.GetWorldPoint(i), s.GetWorldPoint((i + 1) % n), c);
        }
    }
}
