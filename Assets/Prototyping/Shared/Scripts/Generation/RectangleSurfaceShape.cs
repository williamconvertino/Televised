using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>Rounded rectangle: floors, walls, ceilings, pillars, curved-edge platforms.</summary>
    public class RectangleSurfaceShape : SurfaceShapeBase
    {
        [Header("Rectangle")]
        [SerializeField] Vector2 size = new Vector2(6f, 1f);
        [SerializeField, Min(0f)] float cornerRadius = 0.25f;
        [SerializeField, Range(1, 16)] int cornerSegments = 6;

        public Vector2 Size { get => size; set { size = value; Regenerate(); } }
        public float CornerRadius { get => cornerRadius; set { cornerRadius = value; Regenerate(); } }

        public void Configure(Vector2 newSize, float newCornerRadius)
        {
            size = newSize;
            cornerRadius = newCornerRadius;
            Regenerate();
        }

        protected override void BuildOutline(List<Vector2> points)
        {
            Vector2 half = new Vector2(Mathf.Max(0.01f, size.x) * 0.5f, Mathf.Max(0.01f, size.y) * 0.5f);
            float r = Mathf.Min(cornerRadius, half.x, half.y);

            if (r <= 1e-4f)
            {
                points.Add(new Vector2(-half.x, -half.y));
                points.Add(new Vector2(half.x, -half.y));
                points.Add(new Vector2(half.x, half.y));
                points.Add(new Vector2(-half.x, half.y));
                return;
            }

            // Corner centers CCW from bottom-right, each arc spanning 90 degrees.
            Vector2[] centers =
            {
                new Vector2(half.x - r, -half.y + r),
                new Vector2(half.x - r, half.y - r),
                new Vector2(-half.x + r, half.y - r),
                new Vector2(-half.x + r, -half.y + r),
            };
            float[] startAngles = { -90f, 0f, 90f, 180f };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float a = (startAngles[c] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    points.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
        }
    }
}
