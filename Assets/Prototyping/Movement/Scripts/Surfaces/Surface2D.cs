using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>
    /// A traversable closed 2D surface, stored as a polygon path in local space.
    /// All gameplay and visual systems query surfaces through this component
    /// (closest sample, sample at path position) rather than reading colliders directly.
    ///
    /// Normals are smoothed across polygon vertices: within <see cref="normalBlendDistance"/>
    /// of a vertex the normal blends toward the averaged vertex normal, so traversal never
    /// snaps from segment to segment.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Surface2D : MonoBehaviour
    {
        static readonly List<Surface2D> s_All = new List<Surface2D>();
        public static IReadOnlyList<Surface2D> All => s_All;

        [SerializeField] List<Vector2> localPoints = new List<Vector2>();
        [Tooltip("Distance from each vertex over which normals blend into the neighbouring segment.")]
        [SerializeField, Min(0f)] float normalBlendDistance = 0.35f;
        [Tooltip("If false the player can collide with but never attach to this surface.")]
        [SerializeField] bool attachable = true;

        // World-space cache (always counter-clockwise).
        Vector2[] _pts = new Vector2[0];
        Vector2[] _segDir;
        float[] _segLen;
        float[] _cumLen;
        Vector2[] _segNormal;
        Vector2[] _vertNormal;
        float _length;
        Rect _bounds;
        Matrix4x4 _cachedMatrix;
        bool _dirty = true;

        public bool Attachable { get => attachable; set => attachable = value; }
        public float NormalBlendDistance { get => normalBlendDistance; set { normalBlendDistance = value; _dirty = true; } }
        public IReadOnlyList<Vector2> LocalPoints => localPoints;

        public float Length { get { EnsureCache(); return _length; } }
        public Rect WorldBounds { get { EnsureCache(); return _bounds; } }
        public int PointCount { get { EnsureCache(); return _pts.Length; } }
        public bool HasGeometry { get { EnsureCache(); return _pts.Length >= 3 && _length > 1e-5f; } }

        public Vector2 GetWorldPoint(int i) { EnsureCache(); return _pts[i]; }

        void OnEnable() { if (!s_All.Contains(this)) s_All.Add(this); _dirty = true; }
        void OnDisable() { s_All.Remove(this); }
        void OnValidate() { _dirty = true; }

        public void SetLocalPoints(IList<Vector2> points)
        {
            localPoints.Clear();
            localPoints.AddRange(points);
            _dirty = true;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Closest point on the surface to a world position, with smoothed normal/tangent.</summary>
        public SurfaceSample GetClosestSample(Vector2 worldPosition)
        {
            EnsureCache();
            if (_pts.Length < 2) return default;

            int n = _pts.Length;
            float bestSqr = float.MaxValue;
            int bestSeg = 0;
            float bestAlong = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = _pts[i];
                float len = _segLen[i];
                float along = len > 1e-6f ? Mathf.Clamp(Vector2.Dot(worldPosition - a, _segDir[i]), 0f, len) : 0f;
                Vector2 p = a + _segDir[i] * along;
                float sqr = (worldPosition - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestSeg = i;
                    bestAlong = along;
                }
            }

            SurfaceSample s = SampleSegment(bestSeg, bestAlong);
            s.distance = Mathf.Sqrt(bestSqr);
            bool inside = ContainsPoint(worldPosition);
            s.signedDistance = inside ? -s.distance : s.distance;
            if (s.distance > 1e-5f)
            {
                s.separation = (worldPosition - s.point) / s.distance;
                if (inside) s.separation = -s.separation;
            }
            else
            {
                s.separation = s.normal;
            }
            return s;
        }

        /// <summary>Sample at an arc-length path position (wraps around the closed loop).</summary>
        public SurfaceSample SampleAt(float pathPosition)
        {
            EnsureCache();
            if (_pts.Length < 2 || _length <= 1e-6f) return default;

            float s = WrapPathPosition(pathPosition);
            // Binary search for the segment containing s.
            int lo = 0, hi = _pts.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_cumLen[mid] <= s) lo = mid; else hi = mid - 1;
            }
            return SampleSegment(lo, s - _cumLen[lo]);
        }

        /// <summary>
        /// Tangent/normal implied by two virtual contact points at pathPosition ± spacing
        /// (the "two feet" normal). Independent of any rendered legs.
        /// </summary>
        public void GetChordFrame(float pathPosition, float spacing, out Vector2 tangent, out Vector2 normal,
            out SurfaceSample left, out SurfaceSample right)
        {
            left = SampleAt(pathPosition - spacing);
            right = SampleAt(pathPosition + spacing);
            Vector2 chord = right.point - left.point;
            if (chord.sqrMagnitude < 1e-8f)
            {
                SurfaceSample c = SampleAt(pathPosition);
                tangent = c.tangent;
                normal = c.normal;
                return;
            }
            tangent = chord.normalized;
            normal = new Vector2(tangent.y, -tangent.x); // outward for a CCW path
        }

        /// <summary>
        /// First intersection of a ray with this surface's outline within maxDistance.
        /// hit.distance = distance along the ray; hit.separation = the outward normal.
        /// </summary>
        public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out SurfaceSample hit)
        {
            hit = default;
            EnsureCache();
            int n = _pts.Length;
            if (n < 3 || !BoundsWithin(origin, maxDistance)) return false;

            float best = float.MaxValue;
            int bestSeg = -1;
            float bestAlong = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 e = _segDir[i] * _segLen[i];
                float denom = direction.x * e.y - direction.y * e.x;
                if (Mathf.Abs(denom) < 1e-9f) continue;
                Vector2 w = _pts[i] - origin;
                float t = (w.x * e.y - w.y * e.x) / denom;
                float u = (w.x * direction.y - w.y * direction.x) / denom;
                if (t < 0f || t > maxDistance || u < 0f || u > 1f || t >= best) continue;
                best = t;
                bestSeg = i;
                bestAlong = u * _segLen[i];
            }
            if (bestSeg < 0) return false;

            hit = SampleSegment(bestSeg, bestAlong);
            hit.distance = best;
            hit.signedDistance = best;
            hit.separation = hit.normal;
            return true;
        }

        public float WrapPathPosition(float s)
        {
            EnsureCache();
            if (_length <= 1e-6f) return 0f;
            s %= _length;
            if (s < 0f) s += _length;
            return s;
        }

        /// <summary>Shortest signed path distance from a to b around the loop.</summary>
        public float PathDelta(float a, float b)
        {
            EnsureCache();
            float d = WrapPathPosition(b - a);
            if (d > _length * 0.5f) d -= _length;
            return d;
        }

        public bool ContainsPoint(Vector2 p)
        {
            EnsureCache();
            int n = _pts.Length;
            if (n < 3 || !_bounds.Contains(p)) return false;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = _pts[i], b = _pts[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Cheap reject: can any point of this surface be within <paramref name="range"/> of p?</summary>
        public bool BoundsWithin(Vector2 p, float range)
        {
            EnsureCache();
            Rect b = _bounds;
            float dx = Mathf.Max(b.xMin - p.x, 0f, p.x - b.xMax);
            float dy = Mathf.Max(b.yMin - p.y, 0f, p.y - b.yMax);
            return dx * dx + dy * dy <= range * range;
        }

        // ------------------------------------------------------------------ internals

        SurfaceSample SampleSegment(int i, float along)
        {
            int n = _pts.Length;
            float len = _segLen[i];
            Vector2 normal = _segNormal[i];

            float blend = Mathf.Min(normalBlendDistance, len * 0.5f);
            if (blend > 1e-5f)
            {
                if (along < blend)
                    normal = SlerpUnit(_vertNormal[i], _segNormal[i], along / blend);
                else if (len - along < blend)
                    normal = SlerpUnit(_vertNormal[(i + 1) % n], _segNormal[i], (len - along) / blend);
            }

            return new SurfaceSample
            {
                surface = this,
                point = _pts[i] + _segDir[i] * along,
                normal = normal,
                tangent = new Vector2(-normal.y, normal.x),
                pathPosition = _cumLen[i] + along,
            };
        }

        static Vector2 SlerpUnit(Vector2 a, Vector2 b, float t)
        {
            float angle = Vector2.SignedAngle(a, b) * Mathf.Clamp01(t);
            return Rotate(a, angle);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public void MarkDirty() => _dirty = true;

        void EnsureCache()
        {
            Matrix4x4 m = transform.localToWorldMatrix;
            if (!_dirty && m == _cachedMatrix) return;
            _dirty = false;
            _cachedMatrix = m;

            // Transform and strip duplicate consecutive points.
            var world = new List<Vector2>(localPoints.Count);
            foreach (Vector2 lp in localPoints)
            {
                Vector2 wp = m.MultiplyPoint3x4(lp);
                if (world.Count == 0 || (world[world.Count - 1] - wp).sqrMagnitude > 1e-10f) world.Add(wp);
            }
            if (world.Count > 1 && (world[0] - world[world.Count - 1]).sqrMagnitude <= 1e-10f)
                world.RemoveAt(world.Count - 1);

            // Enforce counter-clockwise winding so (dir.y, -dir.x) is outward.
            float area = 0f;
            for (int i = 0; i < world.Count; i++)
            {
                Vector2 a = world[i], b = world[(i + 1) % world.Count];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) world.Reverse();

            _pts = world.ToArray();
            int n = _pts.Length;
            _segDir = new Vector2[n];
            _segLen = new float[n];
            _cumLen = new float[n];
            _segNormal = new Vector2[n];
            _vertNormal = new Vector2[n];
            _length = 0f;

            if (n == 0) { _bounds = new Rect(); return; }

            Vector2 min = _pts[0], max = _pts[0];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = _pts[(i + 1) % n] - _pts[i];
                float len = d.magnitude;
                _segLen[i] = len;
                _segDir[i] = len > 1e-6f ? d / len : Vector2.right;
                _segNormal[i] = new Vector2(_segDir[i].y, -_segDir[i].x);
                _cumLen[i] = _length;
                _length += len;
                min = Vector2.Min(min, _pts[i]);
                max = Vector2.Max(max, _pts[i]);
            }
            _bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            for (int i = 0; i < n; i++)
            {
                Vector2 sum = _segNormal[(i - 1 + n) % n] + _segNormal[i];
                _vertNormal[i] = sum.sqrMagnitude > 1e-8f ? sum.normalized : _segNormal[i];
            }
        }

        void OnDrawGizmosSelected()
        {
            EnsureCache();
            if (_pts.Length < 2) return;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            for (int i = 0; i < _pts.Length; i++)
                Gizmos.DrawLine(_pts[i], _pts[(i + 1) % _pts.Length]);

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            int count = Mathf.Clamp(Mathf.CeilToInt(_length / 0.25f), 8, 400);
            for (int i = 0; i < count; i++)
            {
                SurfaceSample s = SampleAt(_length * i / count);
                Gizmos.DrawLine(s.point, s.point + s.normal * 0.25f);
            }
        }
    }
}
