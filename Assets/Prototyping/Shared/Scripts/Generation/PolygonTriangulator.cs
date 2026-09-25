using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>Ear-clipping triangulation for simple (non self-intersecting) polygons.</summary>
    public static class PolygonTriangulator
    {
        public static void Triangulate(IList<Vector2> pts, List<int> triangles, int indexOffset = 0)
        {
            int n = pts.Count;
            if (n < 3) return;

            var idx = new List<int>(n);
            bool ccw = SignedArea(pts) > 0f;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);

            int guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Cross(b - a, c - b) <= 1e-9f) continue; // reflex
                    bool contains = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int k = idx[j];
                        if (k == ia || k == ib || k == ic) continue;
                        if (PointInTriangle(pts[k], a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;

                    triangles.Add(indexOffset + ia);
                    triangles.Add(indexOffset + ib);
                    triangles.Add(indexOffset + ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // degenerate input; fall through to fan the rest
            }

            for (int i = 1; i + 1 < idx.Count; i++)
            {
                triangles.Add(indexOffset + idx[0]);
                triangles.Add(indexOffset + idx[i]);
                triangles.Add(indexOffset + idx[i + 1]);
            }
        }

        public static float SignedArea(IList<Vector2> pts)
        {
            float a = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }
    }
}
