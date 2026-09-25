using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>Everything a jump mode may need, gathered by the motor.</summary>
    public struct JumpContext
    {
        public Vector2 position;
        public float radius;
        public SurfaceSample surfaceSample;     // current attached sample
        public Vector2 localNormal;             // raw smoothed-per-vertex normal at the contact
        public Vector2 smoothedNormal;          // virtual-foot normal, temporally smoothed
        public Vector2 cursorWorld;
        public float jumpSpeed;
        public float gravity;
    }

    public struct JumpResult
    {
        public Vector2 direction;
        public JumpDirectionMode mode;
        public bool hasTarget;
        public Vector2 targetPoint;   // where a targeted jump aims the player's center
        public Surface2D targetSurface;
        public bool usedFallback;     // targeted mode found nothing (or no solution) and fell back
    }

    /// <summary>
    /// Implements every jump-direction mode. Given the current player/surface/input state,
    /// returns a normalized launch direction. The motor never switches on jump mode itself.
    /// </summary>
    public class JumpDirectionResolver : MonoBehaviour
    {
        readonly List<SurfaceSample> _nearby = new List<SurfaceSample>();

        /// <summary>Result of the most recent Resolve (updated every attached frame for preview).</summary>
        public JumpResult LastResult { get; private set; }

        public JumpResult Resolve(in JumpContext ctx, MovementTuning t)
        {
            JumpResult r = new JumpResult { mode = t.jumpDirectionMode };
            Vector2 cursorDir = ctx.cursorWorld - ctx.position;
            cursorDir = cursorDir.sqrMagnitude > 1e-6f ? cursorDir.normalized : ctx.smoothedNormal;

            switch (t.jumpDirectionMode)
            {
                case JumpDirectionMode.LocalSurfaceNormal:
                    r.direction = ctx.localNormal;
                    break;

                case JumpDirectionMode.SmoothedSurfaceNormal:
                    r.direction = ctx.smoothedNormal;
                    break;

                case JumpDirectionMode.CursorDirection:
                    r.direction = ApplyIntoSurfaceRule(cursorDir, ctx.smoothedNormal, t);
                    break;

                case JumpDirectionMode.ClampedCursorDirection:
                    r.direction = ClampAngle(ctx.smoothedNormal, cursorDir, t.maxJumpAimAngle);
                    break;

                case JumpDirectionMode.NearestSurface:
                    r = ResolveNearest(ctx, t, r);
                    break;

                case JumpDirectionMode.AssistedTarget:
                    r = ResolveAssisted(ctx, t, r, ApplyIntoSurfaceRule(cursorDir, ctx.smoothedNormal, t));
                    break;
            }

            if (r.direction.sqrMagnitude < 1e-8f) r.direction = ctx.smoothedNormal;
            r.direction.Normalize();
            LastResult = r;
            return r;
        }

        /// <summary>Direction for an in-air (double) jump. Pure function of input + tuning.</summary>
        public Vector2 ResolveAirJump(Vector2 moveInput, Vector2 aimDirection, MovementTuning t)
        {
            switch (t.airJumpDirectionMode)
            {
                case AirJumpDirectionMode.MoveInput:
                    return moveInput == Vector2.zero ? Vector2.up : ClampAngle(Vector2.up, moveInput.normalized, t.airJumpMaxAimAngle);
                case AirJumpDirectionMode.Cursor:
                    return aimDirection.sqrMagnitude > 1e-6f ? aimDirection.normalized : Vector2.up;
                case AirJumpDirectionMode.ClampedCursor:
                    return aimDirection.sqrMagnitude > 1e-6f ? ClampAngle(Vector2.up, aimDirection.normalized, t.airJumpMaxAimAngle) : Vector2.up;
                default:
                    return Vector2.up;
            }
        }

        // ------------------------------------------------------------------ modes

        JumpResult ResolveNearest(in JumpContext ctx, MovementTuning t, JumpResult r)
        {
            SurfaceSensor.FindSurfacesInRange(ctx.position, t.nearestSurfaceRange, ctx.surfaceSample.surface, _nearby);

            float bestDist = float.MaxValue;
            foreach (SurfaceSample s in _nearby)
            {
                Vector2 landing = s.point + s.separation * ctx.radius;
                Vector2 delta = landing - ctx.position;
                // Skip targets that would require launching into the current surface.
                if (Vector2.Dot(delta.normalized, ctx.smoothedNormal) < -0.1f) continue;
                if (!BallisticUtility.TrySolve(delta, ctx.jumpSpeed, ctx.gravity, t.preferHighArc, out Vector2 dir)) continue;
                if (Vector2.Dot(dir, ctx.smoothedNormal) < 0f &&
                    !BallisticUtility.TrySolve(delta, ctx.jumpSpeed, ctx.gravity, true, out dir)) continue;
                if (Vector2.Dot(dir, ctx.smoothedNormal) < 0f) continue;

                if (s.distance < bestDist)
                {
                    bestDist = s.distance;
                    r.direction = dir;
                    r.hasTarget = true;
                    r.targetPoint = landing;
                    r.targetSurface = s.surface;
                }
            }

            if (!r.hasTarget)
            {
                r.direction = ctx.smoothedNormal; // no destination: jump still happens, and may fail
                r.usedFallback = true;
            }
            return r;
        }

        JumpResult ResolveAssisted(in JumpContext ctx, MovementTuning t, JumpResult r, Vector2 rawDir)
        {
            r.direction = rawDir;
            SurfaceSensor.FindSurfacesInRange(ctx.position, t.assistRange, ctx.surfaceSample.surface, _nearby);

            float bestAngle = t.assistAngle;
            Vector2 bestDir = rawDir;
            foreach (SurfaceSample s in _nearby)
            {
                Vector2 landing = s.point + s.separation * ctx.radius;
                Vector2 delta = landing - ctx.position;
                float angle = Vector2.Angle(rawDir, delta);
                if (angle > bestAngle) continue;

                if (!BallisticUtility.TrySolve(delta, ctx.jumpSpeed, ctx.gravity, t.preferHighArc, out Vector2 dir))
                    continue;
                if (Vector2.Dot(dir, ctx.smoothedNormal) < 0f) continue;

                bestAngle = angle;
                bestDir = dir;
                r.hasTarget = true;
                r.targetPoint = landing;
                r.targetSurface = s.surface;
            }

            if (r.hasTarget) r.direction = SlerpDir(rawDir, bestDir, t.assistStrength);
            else r.usedFallback = true;
            return r;
        }

        // ------------------------------------------------------------------ helpers

        static Vector2 ApplyIntoSurfaceRule(Vector2 dir, Vector2 normal, MovementTuning t)
        {
            switch (t.cursorIntoSurface)
            {
                case CursorIntoSurfaceBehavior.Allow:
                    return dir;
                case CursorIntoSurfaceBehavior.UseSurfaceNormal:
                    return Vector2.Angle(normal, dir) > 90f - t.cursorMinSurfaceAngle ? normal : dir;
                default:
                    return ClampAngle(normal, dir, 90f - t.cursorMinSurfaceAngle);
            }
        }

        /// <summary>Rotate <paramref name="dir"/> so it lies within <paramref name="maxAngle"/> of <paramref name="axis"/>.</summary>
        public static Vector2 ClampAngle(Vector2 axis, Vector2 dir, float maxAngle)
        {
            float a = Vector2.SignedAngle(axis, dir);
            return Surface2D.Rotate(axis, Mathf.Clamp(a, -maxAngle, maxAngle)).normalized;
        }

        static Vector2 SlerpDir(Vector2 a, Vector2 b, float t) =>
            Surface2D.Rotate(a, Vector2.SignedAngle(a, b) * Mathf.Clamp01(t)).normalized;
    }
}
