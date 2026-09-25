using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// One sampled location on a Surface2D. Shared by attachment, traversal, jumping,
    /// procedural feet, candidate scoring and debug drawing.
    /// </summary>
    public struct SurfaceSample
    {
        public Surface2D surface;
        /// <summary>World-space point on the surface.</summary>
        public Vector2 point;
        /// <summary>Smoothed outward normal (unit).</summary>
        public Vector2 normal;
        /// <summary>Unit tangent pointing in the +pathPosition (counter-clockwise) direction.</summary>
        public Vector2 tangent;
        /// <summary>Arc-length position along the closed surface path, in [0, surface.Length).</summary>
        public float pathPosition;
        /// <summary>For closest-point queries: distance from the query position to <see cref="point"/>.</summary>
        public float distance;
        /// <summary>For closest-point queries: distance, negative if the query position is inside the shape.</summary>
        public float signedDistance;
        /// <summary>For closest-point queries: unit direction from <see cref="point"/> toward the query position (outward if inside).</summary>
        public Vector2 separation;

        public bool IsValid => surface != null;
    }

    /// <summary>A surface found near the player, plus the attachment controller's evaluation of it.</summary>
    public struct SurfaceCandidate
    {
        public SurfaceSample sample;
        /// <summary>Distance between the player's edge and the surface (negative = penetrating).</summary>
        public float gap;

        // Filled in by SurfaceAttachmentController.
        public bool blocked;
        public bool inAttachRange;
        public bool approachOk;
        public bool eligible;
        public float approachSpeed;
        public float score;

        public Surface2D Surface => sample.surface;
    }
}
