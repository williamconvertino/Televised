using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>Circle / ellipse surface.</summary>
    public class EllipseSurfaceShape : SurfaceShapeBase
    {
        [Header("Ellipse")]
        [SerializeField] Vector2 radii = new Vector2(2f, 2f);
        [SerializeField, Range(8, 256)] int segments = 72;

        public void Configure(Vector2 newRadii, int newSegments = 72)
        {
            radii = newRadii;
            segments = newSegments;
            Regenerate();
        }

        protected override void BuildOutline(List<Vector2> points)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                points.Add(new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y));
            }
        }
    }
}
