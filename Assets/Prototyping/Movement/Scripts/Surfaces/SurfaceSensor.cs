using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>
    /// Detection only: finds Surface2D objects near a circle and keeps the candidate list.
    /// Does not decide which one to attach to (see SurfaceAttachmentController).
    /// </summary>
    public class SurfaceSensor : MonoBehaviour
    {
        readonly List<SurfaceCandidate> _candidates = new List<SurfaceCandidate>();

        public List<SurfaceCandidate> Candidates => _candidates;
        public Vector2 LastQueryCenter { get; private set; }
        public float LastQueryRange { get; private set; }

        /// <summary>Rebuild the candidate list: every surface whose gap to the circle is at most <paramref name="range"/>.</summary>
        public List<SurfaceCandidate> Refresh(Vector2 center, float radius, float range)
        {
            _candidates.Clear();
            LastQueryCenter = center;
            LastQueryRange = range;

            IReadOnlyList<Surface2D> all = Surface2D.All;
            for (int i = 0; i < all.Count; i++)
            {
                Surface2D s = all[i];
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) continue;
                if (!s.BoundsWithin(center, radius + range)) continue;

                SurfaceSample sample = s.GetClosestSample(center);
                float gap = sample.signedDistance - radius;
                if (gap > range) continue;

                _candidates.Add(new SurfaceCandidate { sample = sample, gap = gap });
            }
            return _candidates;
        }

        /// <summary>Surfaces within a larger range (used by targeted jump modes). Does not touch the main candidate list.</summary>
        public static void FindSurfacesInRange(Vector2 center, float range, Surface2D exclude, List<SurfaceSample> results)
        {
            results.Clear();
            IReadOnlyList<Surface2D> all = Surface2D.All;
            for (int i = 0; i < all.Count; i++)
            {
                Surface2D s = all[i];
                if (s == null || s == exclude || !s.isActiveAndEnabled || !s.Attachable || !s.HasGeometry) continue;
                if (!s.BoundsWithin(center, range)) continue;
                SurfaceSample sample = s.GetClosestSample(center);
                if (sample.distance <= range) results.Add(sample);
            }
        }
    }
}
