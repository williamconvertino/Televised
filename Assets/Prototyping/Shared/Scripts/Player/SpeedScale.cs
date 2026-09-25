using System;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>Which part of the velocity a <see cref="SpeedScale"/> reads.</summary>
    public enum VelocityComponent
    {
        /// <summary>|velocity.x|</summary>
        Horizontal,
        /// <summary>|velocity.y| (rising or falling)</summary>
        Vertical,
        /// <summary>|velocity|</summary>
        Total,
        /// <summary>Downward speed only (0 while rising)</summary>
        Falling
    }

    /// <summary>
    /// A clamped linear multiplier driven by speed:
    /// scale = 1 at speed 0, <see cref="scaleAtReference"/> at <see cref="referenceSpeed"/>,
    /// extrapolated beyond that and clamped to [<see cref="minScale"/>, <see cref="maxScale"/>].
    /// With scaleAtReference = 1 it has no effect.
    /// </summary>
    [Serializable]
    public struct SpeedScale
    {
        public VelocityComponent source;
        [Tooltip("Speed at which the multiplier equals scaleAtReference.")]
        [Min(0.01f)] public float referenceSpeed;
        [Tooltip("Multiplier at referenceSpeed. 1 = no effect, <1 shrinks with speed, >1 grows with speed.")]
        [Min(0f)] public float scaleAtReference;
        [Tooltip("Lower limit of the multiplier.")]
        [Min(0f)] public float minScale;
        [Tooltip("Upper limit of the multiplier.")]
        [Min(0f)] public float maxScale;

        public SpeedScale(VelocityComponent source, float referenceSpeed, float scaleAtReference, float minScale, float maxScale)
        {
            this.source = source;
            this.referenceSpeed = referenceSpeed;
            this.scaleAtReference = scaleAtReference;
            this.minScale = minScale;
            this.maxScale = maxScale;
        }

        public float Speed(Vector2 velocity)
        {
            switch (source)
            {
                case VelocityComponent.Horizontal: return Mathf.Abs(velocity.x);
                case VelocityComponent.Vertical: return Mathf.Abs(velocity.y);
                case VelocityComponent.Falling: return Mathf.Max(0f, -velocity.y);
                default: return velocity.magnitude;
            }
        }

        public float Evaluate(Vector2 velocity)
        {
            float t = Speed(velocity) / Mathf.Max(0.01f, referenceSpeed);
            float scale = 1f + (scaleAtReference - 1f) * t;
            float lo = Mathf.Min(minScale, maxScale), hi = Mathf.Max(minScale, maxScale);
            return Mathf.Clamp(scale, lo, hi);
        }
    }
}
