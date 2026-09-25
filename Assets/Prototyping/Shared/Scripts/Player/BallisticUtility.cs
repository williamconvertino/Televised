using UnityEngine;

namespace Televised.Prototyping.Shared
{
    public static class BallisticUtility
    {
        /// <summary>
        /// Launch direction that reaches <paramref name="delta"/> at fixed <paramref name="speed"/>
        /// under downward gravity. Returns false if the target is out of reach.
        /// </summary>
        public static bool TrySolve(Vector2 delta, float speed, float gravity, bool highArc, out Vector2 direction)
        {
            direction = delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector2.up;
            if (speed <= 0f) return false;
            if (gravity <= 1e-4f) return true; // straight line

            float dx = Mathf.Abs(delta.x);
            float dy = delta.y;
            float v2 = speed * speed;

            if (dx < 1e-3f)
            {
                if (dy <= 0f) { direction = Vector2.down; return true; }
                direction = Vector2.up;
                return v2 >= 2f * gravity * dy;
            }

            float disc = v2 * v2 - gravity * (gravity * dx * dx + 2f * dy * v2);
            if (disc < 0f) return false;

            float root = Mathf.Sqrt(disc);
            float angle = Mathf.Atan2(highArc ? v2 + root : v2 - root, gravity * dx);
            direction = new Vector2(Mathf.Cos(angle) * Mathf.Sign(delta.x), Mathf.Sin(angle));
            return true;
        }

        public static Vector2 PositionAt(Vector2 start, Vector2 velocity, float gravity, float time) =>
            start + velocity * time + 0.5f * gravity * time * time * Vector2.down;
    }
}
