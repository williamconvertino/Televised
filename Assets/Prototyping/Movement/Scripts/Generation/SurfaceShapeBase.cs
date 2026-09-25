using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>
    /// Base for components that generate a closed outline and push it into Surface2D,
    /// a PolygonCollider2D and a simple filled + outlined mesh. Regenerates in edit mode
    /// whenever a parameter changes, and on enable (generated meshes are never saved).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Surface2D), typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class SurfaceShapeBase : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] protected Color fillColor = new Color(0.22f, 0.26f, 0.34f);
        [SerializeField] protected Color outlineColor = new Color(0.55f, 0.8f, 0.95f);
        [SerializeField, Min(0f)] protected float outlineWidth = 0.06f;
        [SerializeField] protected int sortingOrder = 0;

        [Header("Physics")]
        [SerializeField] protected bool generateCollider = true;

        readonly List<Vector2> _points = new List<Vector2>();
        Mesh _mesh;

        public IReadOnlyList<Vector2> Points => _points;
        public Color FillColor { get => fillColor; set { fillColor = value; Regenerate(); } }
        public Color OutlineColor { get => outlineColor; set { outlineColor = value; Regenerate(); } }

        /// <summary>Fill <paramref name="points"/> with a closed outline in local space.</summary>
        protected abstract void BuildOutline(List<Vector2> points);

        protected virtual void OnEnable() => Regenerate();

        protected virtual void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Mesh/collider changes are not allowed inside OnValidate; defer.
            if (UnityEditor.EditorUtility.IsPersistent(this)) return; // prefab asset, not an instance
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Regenerate(); };
        }
#endif

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            _points.Clear();
            BuildOutline(_points);
            for (int i = _points.Count - 1; i >= 0 && _points.Count > 1; i--)
                if ((_points[i] - _points[(i + 1) % _points.Count]).sqrMagnitude < 1e-8f) _points.RemoveAt(i);
            if (_points.Count < 3) return;
            if (PolygonTriangulator.SignedArea(_points) < 0f) _points.Reverse();

            GetComponent<Surface2D>().SetLocalPoints(_points);

            if (generateCollider)
            {
                var col = GetComponent<PolygonCollider2D>();
                if (col == null) col = gameObject.AddComponent<PolygonCollider2D>();
                col.pathCount = 1;
                col.SetPath(0, _points);
            }

            BuildMesh();
        }

        void BuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = $"{name}_SurfaceMesh", hideFlags = HideFlags.HideAndDontSave };
            }
            _mesh.Clear();

            int n = _points.Count;
            var verts = new List<Vector3>(n * 3);
            var colors = new List<Color>(n * 3);
            var tris = new List<int>(n * 9);

            // Fill.
            for (int i = 0; i < n; i++) { verts.Add(_points[i]); colors.Add(fillColor); }
            PolygonTriangulator.Triangulate(_points, tris);

            // Inward outline band (drawn after the fill so it sits on top).
            if (outlineWidth > 0f)
            {
                int start = verts.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 prev = _points[(i - 1 + n) % n], p = _points[i], next = _points[(i + 1) % n];
                    Vector2 n0 = OutwardNormal(prev, p), n1 = OutwardNormal(p, next);
                    Vector2 vn = (n0 + n1).sqrMagnitude > 1e-8f ? (n0 + n1).normalized : n1;
                    float miter = Mathf.Min(3f, 1f / Mathf.Max(0.2f, Vector2.Dot(vn, n1)));
                    verts.Add(p);
                    verts.Add(p - vn * (outlineWidth * miter));
                    colors.Add(outlineColor);
                    colors.Add(outlineColor);
                }
                for (int i = 0; i < n; i++)
                {
                    int o0 = start + i * 2, i0 = o0 + 1;
                    int o1 = start + ((i + 1) % n) * 2, i1 = o1 + 1;
                    tris.Add(o0); tris.Add(o1); tris.Add(i0);
                    tris.Add(i0); tris.Add(o1); tris.Add(i1);
                }
            }

            _mesh.SetVertices(verts);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sortingOrder = sortingOrder;
        }

        static Vector2 OutwardNormal(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            return new Vector2(d.y, -d.x);
        }
    }
}
