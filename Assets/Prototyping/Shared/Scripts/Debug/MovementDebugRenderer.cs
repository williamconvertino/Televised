using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Draws the movement state in the Game view: surface point, normals, tangent, traversal
    /// direction, jump vector + predicted arc, attach radius, candidates, selected target,
    /// recently detached surface, virtual feet, velocity, and a mouse probe for inspecting
    /// any surface's sample data.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class MovementDebugRenderer : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        public bool drawEnabled = true;
        [Tooltip("Player-facing mode: draw only gameplay hints (grapple aim/latch point, air-jump arrow), no debug data.")]
        public bool hintsOnly = false;

        [Header("Layers")]
        public bool showSurfaceFrame = true;
        public bool showVirtualFeet = true;
        public bool showJumpPreview = true;
        public bool showCandidates = true;
        public bool showVelocity = true;
        public bool showMouseProbe = true;
        public bool showAllSurfaceNormals = false;

        static readonly Color ColCenter = Color.white;
        static readonly Color ColAttachRadius = new Color(0.3f, 0.9f, 1f, 0.45f);
        static readonly Color ColMagnetRadius = new Color(0.8f, 0.4f, 1f, 0.3f);
        static readonly Color ColPoint = new Color(1f, 0.92f, 0.2f);
        static readonly Color ColNormal = new Color(0.25f, 1f, 0.35f);
        static readonly Color ColSmoothedNormal = new Color(0.7f, 1f, 0.2f);
        static readonly Color ColTangent = new Color(1f, 0.3f, 0.3f);
        static readonly Color ColTraversal = new Color(1f, 0.6f, 0.1f);
        static readonly Color ColFeet = new Color(1f, 0.3f, 0.9f);
        static readonly Color ColJump = new Color(0.2f, 0.85f, 1f);
        static readonly Color ColJumpArc = new Color(0.2f, 0.85f, 1f, 0.45f);
        static readonly Color ColVelocity = new Color(0.35f, 0.5f, 1f);
        static readonly Color ColEligible = new Color(0.3f, 1f, 0.4f, 0.9f);
        static readonly Color ColBlocked = new Color(1f, 0.25f, 0.25f, 0.9f);
        static readonly Color ColIneligible = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        static readonly Color ColSelected = new Color(1f, 0.92f, 0.2f);
        static readonly Color ColMagnet = new Color(0.8f, 0.4f, 1f);
        static readonly Color ColDetached = new Color(1f, 0.25f, 0.25f, 0.8f);
        static readonly Color ColProbe = new Color(1f, 1f, 1f, 0.7f);
        static readonly Color ColAirJump = new Color(0.55f, 1f, 0.95f);
        static readonly Color ColGrapple = new Color(1f, 0.55f, 0.2f);
        static readonly Color ColGrappleFaint = new Color(1f, 0.55f, 0.2f, 0.25f);

        void LateUpdate()
        {
            if (!drawEnabled || motor == null || !DebugLines.Available) return;
            MovementTuning t = motor.Tuning;
            Vector2 c = motor.Position;
            float r = motor.Radius;

            if (hintsOnly)
            {
                if (!motor.IsAttached && motor.AirJumpsRemaining > 0)
                    DebugLines.Arrow(c, c + motor.AirJumpPreviewDirection * 1.3f, ColAirJump, 0.15f);
                DrawGrapple(motor, t, c);
                return;
            }

            // Player center + radii.
            DebugLines.Cross(c, 0.08f, ColCenter);
            DebugLines.Circle(c, r, new Color(1f, 1f, 1f, 0.25f));
            // Attach / magnet circles at the current (speed-scaled) size; the base size is shown faintly when they differ.
            Vector2 v = motor.IsAttached ? Vector2.zero : motor.Velocity;
            float attachNow = t.EffectiveAttachDistance(v), attachBase = t.EffectiveAttachDistance(Vector2.zero);
            DebugLines.Circle(c, r + attachNow, ColAttachRadius, 40);
            if (!Mathf.Approximately(attachNow, attachBase)) DebugLines.Circle(c, r + attachBase, ColAttachRadius * 0.5f, 40);
            if (t.attachmentMode == AttachmentMode.Magnetic)
            {
                float magnetNow = t.EffectiveMagnetRange(v), magnetBase = t.EffectiveMagnetRange(Vector2.zero);
                DebugLines.Circle(c, r + magnetNow, ColMagnetRadius, 48);
                if (!Mathf.Approximately(magnetNow, magnetBase)) DebugLines.Circle(c, r + magnetBase, ColMagnetRadius * 0.5f, 48);
            }

            if (showVelocity && motor.Velocity.sqrMagnitude > 1e-4f)
                DebugLines.Arrow(c, c + motor.Velocity * 0.15f, ColVelocity);

            if (motor.IsAttached) DrawAttached(motor, t, c);
            else DrawAirborne(motor, t, c);

            // Air-jump preview (while airborne with jumps left).
            if (showJumpPreview && !motor.IsAttached && motor.AirJumpsRemaining > 0)
                DebugLines.Arrow(c, c + motor.AirJumpPreviewDirection * 1.3f, ColAirJump, 0.15f);

            DrawGrapple(motor, t, c);

            // Recently detached surface (while its cooldown is running).
            SurfaceAttachmentController att = motor.Attachment;
            if (att != null && att.RecentlyDetachedSurface != null && att.CooldownRemaining(t) > 0f)
                DebugLines.SurfaceOutline(att.RecentlyDetachedSurface, ColDetached);

            if (showMouseProbe) DrawMouseProbe();
            if (showAllSurfaceNormals) DrawAllNormals();
        }

        void DrawAttached(PlayerMotor2D m, MovementTuning t, Vector2 c)
        {
            SurfaceSample s = m.CurrentSample;
            if (showSurfaceFrame)
            {
                DebugLines.Cross(s.point, 0.1f, ColPoint);
                DebugLines.Line(s.point, c, new Color(1f, 1f, 1f, 0.3f));
                DebugLines.Arrow(s.point, s.point + s.normal * 0.9f, ColNormal);
                DebugLines.Arrow(c, c + m.SmoothedNormal * 1.1f, ColSmoothedNormal, 0.1f);
                DebugLines.Line(s.point - s.tangent * 0.6f, s.point + s.tangent * 0.6f, ColTangent);
                DebugLines.Arrow(s.point, s.point + s.tangent * 0.6f, ColTangent, 0.08f); // +path direction

                if (Mathf.Abs(m.SurfaceSpeed) > 0.01f)
                {
                    Vector2 dir = s.tangent * Mathf.Sign(m.SurfaceSpeed);
                    DebugLines.Arrow(c, c + dir * (0.4f + Mathf.Abs(m.SurfaceSpeed) * 0.15f), ColTraversal);
                }
            }

            if (showVirtualFeet)
            {
                SurfaceSample a = m.VirtualFootBack, b = m.VirtualFootFront;
                DebugLines.Cross(a.point, 0.07f, ColFeet);
                DebugLines.Cross(b.point, 0.07f, ColFeet);
                DebugLines.Line(a.point, b.point, ColFeet);
            }

            if (showJumpPreview && m.JumpResolver != null)
            {
                JumpResult jr = m.JumpResolver.LastResult;
                DebugLines.Arrow(c, c + jr.direction * 1.6f, ColJump, 0.2f);
                if (jr.hasTarget)
                {
                    DebugLines.Circle(jr.targetPoint, m.Radius * 0.5f, ColJump, 20);
                    DebugLines.Cross(jr.targetPoint, 0.12f, ColJump);
                }
                DrawTrajectory(m, t, c, jr.direction * t.jumpSpeed + s.tangent * (m.SurfaceSpeed * t.inheritSurfaceVelocity));
            }
        }

        void DrawAirborne(PlayerMotor2D m, MovementTuning t, Vector2 c)
        {
            if (m.LastJumpDirection != Vector2.zero && m.TimeSinceDetach < 1.5f)
                DebugLines.Arrow(m.LastJumpOrigin, m.LastJumpOrigin + m.LastJumpDirection * 1.6f,
                    new Color(ColJump.r, ColJump.g, ColJump.b, 0.4f), 0.2f);

            if (!showCandidates || m.Sensor == null) return;

            foreach (SurfaceCandidate cand in m.Sensor.Candidates)
            {
                Color col = cand.blocked ? ColBlocked : cand.eligible ? ColEligible : ColIneligible;
                DebugLines.Line(c, cand.sample.point, col);
                DebugLines.Cross(cand.sample.point, 0.06f, col);
                DebugLines.Ray(cand.sample.point, cand.sample.normal * 0.35f, col);
            }

            SurfaceAttachmentController att = m.Attachment;
            if (att != null && att.MagnetTarget is SurfaceCandidate mag)
                DebugLines.Arrow(c, mag.sample.point, ColMagnet, 0.12f);
            if (att != null && att.SelectedTarget is SurfaceCandidate sel)
            {
                DebugLines.Circle(sel.sample.point, 0.15f, ColSelected, 16);
                DebugLines.SurfaceOutline(sel.Surface, ColSelected);
            }
        }

        static void DrawGrapple(PlayerMotor2D m, MovementTuning t, Vector2 c)
        {
            GrappleController g = m.Grapple;
            if (g == null || !t.enableGrapple) return;

            if (g.State == GrappleState.Idle)
            {
                // Aim line to max range, and what a click would latch onto.
                DebugLines.Line(c, c + m.AimDirection * t.grappleMaxDistance, ColGrappleFaint);
                if (g.PreviewHasHit)
                {
                    DebugLines.Line(c, g.PreviewPoint, ColGrapple);
                    DebugLines.Circle(g.PreviewPoint, 0.15f, ColGrapple, 16);
                }
            }
            else if (g.IsLatched)
            {
                DebugLines.Cross(g.AnchorPoint, 0.15f, ColGrapple);
                if (t.grappleMode != GrappleMode.Pull) DebugLines.Circle(g.AnchorPoint, g.RopeLength, ColGrappleFaint, 64);
            }
        }

        static void DrawTrajectory(PlayerMotor2D m, MovementTuning t, Vector2 start, Vector2 v)
        {
            const float dt = 0.025f;
            Vector2 prev = start;
            Surface2D current = m.CurrentSurface;
            for (int i = 1; i < 80; i++)
            {
                Vector2 p = BallisticUtility.PositionAt(start, v, t.gravity, i * dt);
                DebugLines.Line(prev, p, ColJumpArc);
                prev = p;

                // Stop the preview at the first surface contact (ignoring the one we're leaving early on).
                var all = Surface2D.All;
                for (int k = 0; k < all.Count; k++)
                {
                    Surface2D s = all[k];
                    if (s == null || !s.isActiveAndEnabled || (s == current && i * dt < t.reattachCooldown + 0.05f)) continue;
                    if (!s.BoundsWithin(p, m.Radius)) continue;
                    if (s.GetClosestSample(p).signedDistance < m.Radius)
                    {
                        DebugLines.Circle(p, m.Radius, ColJumpArc, 20);
                        return;
                    }
                }
            }
        }

        void DrawMouseProbe()
        {
            Vector2 mouse = motor.CursorWorld;
            Surface2D best = null;
            SurfaceSample bestSample = default;
            float bestDist = 3f;
            foreach (Surface2D s in Surface2D.All)
            {
                if (s == null || !s.isActiveAndEnabled || !s.BoundsWithin(mouse, bestDist)) continue;
                SurfaceSample smp = s.GetClosestSample(mouse);
                if (smp.distance < bestDist) { bestDist = smp.distance; best = s; bestSample = smp; }
            }
            if (best == null) return;
            DebugLines.Line(mouse, bestSample.point, ColProbe);
            DebugLines.Cross(bestSample.point, 0.08f, ColProbe);
            DebugLines.Arrow(bestSample.point, bestSample.point + bestSample.normal * 0.6f, ColNormal, 0.1f);
            DebugLines.Line(bestSample.point - bestSample.tangent * 0.4f, bestSample.point + bestSample.tangent * 0.4f, ColTangent);
        }

        static void DrawAllNormals()
        {
            foreach (Surface2D s in Surface2D.All)
            {
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) continue;
                int count = Mathf.Clamp(Mathf.CeilToInt(s.Length / 0.35f), 8, 300);
                for (int i = 0; i < count; i++)
                {
                    SurfaceSample smp = s.SampleAt(s.Length * i / count);
                    DebugLines.Ray(smp.point, smp.normal * 0.25f, new Color(0.25f, 1f, 0.35f, 0.5f));
                }
            }
        }
    }
}
