using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>
    /// Marks a (non-attachable) Surface2D as deadly: touching it respawns the player at the last checkpoint.
    /// The surface still collides normally, so the player can't pass through it.
    /// </summary>
    [RequireComponent(typeof(Surface2D))]
    public class PlaytestHazard : MonoBehaviour
    {
        [Tooltip("Extra forgiveness (units). Positive = you must overlap a little before it counts.")]
        public float forgiveness = -0.03f;

        Surface2D _surface;

        public Surface2D Surface => _surface != null ? _surface : (_surface = GetComponent<Surface2D>());

        public bool Touches(Vector2 center, float radius)
        {
            Surface2D s = Surface;
            if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) return false;
            if (!s.BoundsWithin(center, radius + 0.1f)) return false;
            return s.GetClosestSample(center).signedDistance < radius - forgiveness;
        }
    }
}
