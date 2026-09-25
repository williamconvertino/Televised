using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Translates WASD / arrow input into a traversal sign along the current surface path
    /// (+1 = +pathPosition / counter-clockwise, -1 = clockwise, 0 = none).
    ///
    /// The held keys form a screen-space intent vector (A/D = left/right, W/S = up/down,
    /// opposite keys cancel). The resolver picks the path direction whose tangent best matches it.
    /// </summary>
    public class SurfaceInputResolver : MonoBehaviour
    {
        // Locked path sign for ScreenRelativeLocked (0 = not locked). Visible for debugging.
        [SerializeField] int lockedSign;

        public int LastSign { get; private set; }
        public int LockedSign => lockedSign;
        public Vector2 LastInput { get; private set; }

        public string LockDescription => lockedSign == 0 ? "-" : lockedSign.ToString("+0;-0");

        /// <param name="input">Screen-space intent from the held keys (see PrototypeInput.MoveVector).</param>
        /// <param name="newKeyPressed">True on the frame a movement key goes down. Locked mode re-evaluates then.</param>
        /// <param name="tangent">Current surface tangent (+path direction).</param>
        /// <param name="surfaceUp">Outward normal used for tie-breaking when the input is perpendicular to the tangent.</param>
        /// <param name="surface">Current surface (optional). Lets A/D follow a curve when the input is perpendicular to it here.</param>
        /// <param name="pathPosition">Current path position on <paramref name="surface"/>.</param>
        public int Resolve(Vector2 input, bool newKeyPressed, Vector2 tangent, Vector2 surfaceUp, MovementTuning t,
            Surface2D surface = null, float pathPosition = 0f)
        {
            LastInput = input;

            // No (net) input: stop and forget the lock.
            if (input == Vector2.zero)
            {
                lockedSign = 0;
                return LastSign = 0;
            }

            switch (t.surfaceMovementMode)
            {
                case SurfaceMovementMode.SurfaceRelative:
                {
                    // Fixed orientation: only the horizontal keys count. D = clockwise by default.
                    if (input.x == 0f) return LastSign = 0;
                    int clockwise = t.invertSurfaceRelative ? +1 : -1;
                    return LastSign = input.x > 0f ? clockwise : -clockwise;
                }

                case SurfaceMovementMode.ScreenRelativeContinuous:
                    return LastSign = ChooseScreenSign(input, tangent, surfaceUp, surface, pathPosition, t);

                default: // ScreenRelativeLocked
                {
                    // Lock when movement starts, and re-evaluate only when a key is newly pressed.
                    // Releasing one of several held keys keeps the current lock, so the player never
                    // reverses just because a key came up while travelling around a curve.
                    if (lockedSign == 0 || newKeyPressed)
                    {
                        int s = ChooseScreenSign(input, tangent, surfaceUp, surface, pathPosition, t);
                        if (s != 0) lockedSign = s; // ambiguous new press: keep the previous lock (if any)
                    }
                    return LastSign = lockedSign;
                }
            }
        }

        /// <summary>
        /// Called by the motor when the player transfers onto a new surface while moving,
        /// so a held key keeps moving the player in the continued direction.
        /// </summary>
        public void RemapLocks(int newSign)
        {
            if (lockedSign != 0) lockedSign = newSign;
            LastSign = newSign;
        }

        /// <summary>Forget the lock; the held keys re-evaluate against the new surface.</summary>
        public void ClearLocks() => lockedSign = 0;

        /// <summary>
        /// Path sign whose tangent best matches the screen-space input. Returns 0 when the input is
        /// (nearly) perpendicular to a straight surface, e.g. W on a flat floor.
        /// </summary>
        static int ChooseScreenSign(Vector2 input, Vector2 tangent, Vector2 surfaceUp, Surface2D surface, float pathPos,
            MovementTuning t)
        {
            Vector2 screenDir = input.normalized;
            // The same input as seen by a player standing on the surface (surfaceUp = "up").
            // This breaks ties, e.g. D on a wall climbs, and W on a wall's side faces goes up.
            float toSurface = Vector2.SignedAngle(Vector2.up, surfaceUp);
            Vector2 surfaceDir = Surface2D.Rotate(screenDir, toSurface);

            float score = Vector2.Dot(tangent, screenDir) + t.screenAmbiguityBias * Vector2.Dot(tangent, surfaceDir);
            if (Mathf.Abs(score) >= t.minInputAlignment) return score > 0f ? +1 : -1;

            // Ambiguous here (e.g. A/D with the eye sideways on an overhang). On a curve the path still
            // heads left or right a little further along, so follow it; straight surfaces stay put.
            return input.x != 0f ? CurveSign(screenDir, surface, pathPos, t) : 0;
        }

        const float CurveMinTurnDegrees = 8f;

        /// <summary>
        /// Look a short way along the path in both directions and pick the one that heads toward
        /// <paramref name="dir"/>. 0 if the surface is (nearly) straight there or neither way helps.
        /// </summary>
        static int CurveSign(Vector2 dir, Surface2D surface, float pathPos, MovementTuning t)
        {
            if (surface == null || !surface.HasGeometry) return 0;
            float look = Mathf.Max(t.playerRadius * 2f, 0.5f);
            SurfaceSample here = surface.SampleAt(pathPos);
            SurfaceSample ahead = surface.SampleAt(pathPos + look);
            SurfaceSample behind = surface.SampleAt(pathPos - look);
            if (Vector2.Angle(behind.tangent, ahead.tangent) < CurveMinTurnDegrees) return 0;

            Vector2 fwd = ahead.point - here.point, back = behind.point - here.point;
            if (fwd.sqrMagnitude < 1e-8f || back.sqrMagnitude < 1e-8f) return 0;
            float a = Vector2.Dot(fwd.normalized, dir), b = Vector2.Dot(back.normalized, dir);
            if (Mathf.Max(a, b) <= 0f || Mathf.Abs(a - b) < 0.05f) return 0;
            return a > b ? +1 : -1;
        }
    }
}
