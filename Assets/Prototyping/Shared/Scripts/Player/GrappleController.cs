using System;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    public enum GrappleState
    {
        Idle,
        /// <summary>The leg tip is travelling out toward the target (or toward max range on a miss).</summary>
        Shooting,
        /// <summary>The leg is hooked onto a surface; the motor pulls or swings the player.</summary>
        Latched,
        /// <summary>The leg is coming back (after a miss, a release, or landing).</summary>
        Retracting
    }

    /// <summary>
    /// Grapple leg state machine. Targeting happens once at fire time (ray + optional aim-assist rays
    /// against Surface2D outlines); the tip then travels out at shoot speed and latches when it arrives.
    /// The motor asks this component for airborne velocity changes (Pull) and the rope constraint (Swing).
    /// Visuals (the stretched leg) are drawn by ProceduralLegRig from <see cref="TipPosition"/>.
    /// </summary>
    public class GrappleController : MonoBehaviour
    {
        public GrappleState State { get; private set; } = GrappleState.Idle;
        public bool IsLatched => State == GrappleState.Latched;
        public bool IsActive => State != GrappleState.Idle;

        public Vector2 TipPosition { get; private set; }
        public Vector2 FireOrigin { get; private set; }
        public Vector2 FireDirection { get; private set; }
        public Vector2 AnchorPoint { get; private set; }
        public Surface2D AnchorSurface { get; private set; }
        public float RopeLength { get; private set; }
        public float TimeLatched => IsLatched ? Time.time - _latchTime : 0f;
        /// <summary>SwingPull: true while LMB is held and the rope is reeling in; false = swinging freely.</summary>
        public bool IsReeling { get; private set; }

        // Idle-time preview of what a click would hit (for debug drawing).
        public bool PreviewHasHit { get; private set; }
        public Vector2 PreviewPoint { get; private set; }

        public event Action<GrappleController> Latched;

        static readonly float[] AssistFactors = { 0f, 0.5f, -0.5f, 1f, -1f };

        float _traveled, _targetDistance;
        bool _willHit, _bonk;
        SurfaceSample _target;
        float _cooldownUntil, _latchTime, _missFallTimer;
        Vector2 _tipVelocity;

        // ---------------------------------------------------------------- per-frame (called by the motor)

        public void Tick(PlayerMotor2D m, float dt)
        {
            MovementTuning t = m.Tuning;

            if (!t.enableGrapple)
            {
                PreviewHasHit = false;
                if (State == GrappleState.Shooting || State == GrappleState.Latched) BeginRetract();
                if (State == GrappleState.Idle) { TipPosition = m.Position; return; }
            }

            switch (State)
            {
                case GrappleState.Idle:
                    TipPosition = m.Position;
                    PreviewHasHit = FindTarget(m, t, m.AimDirection, out SurfaceSample preview, out _, out _, out _);
                    PreviewPoint = preview.point;
                    if (PrototypeInput.GrapplePressed && Time.time >= _cooldownUntil) Fire(m, t);
                    break;

                case GrappleState.Shooting:
                    if (PrototypeInput.GrappleCancelPressed) { BeginRetract(); break; }
                    _traveled += t.grappleShootSpeed * dt;
                    if (_traveled >= _targetDistance)
                    {
                        TipPosition = FireOrigin + FireDirection * _targetDistance;
                        if (_willHit) Latch(m, t);
                        else if (_bonk) BeginRetract();
                        else BeginMiss(t);
                    }
                    else
                    {
                        TipPosition = FireOrigin + FireDirection * _traveled;
                    }
                    break;

                case GrappleState.Latched:
                    TipPosition = AnchorPoint;
                    IsReeling = t.grappleMode == GrappleMode.SwingPull && PrototypeInput.GrappleHeld;
                    if (t.grappleMode == GrappleMode.Pull && PrototypeInput.GrapplePressed)
                    {
                        Fire(m, t); // click again while pulling: re-target (chain grapples)
                        break;
                    }
                    // SwingPull never times out: you can hang and swing as long as you like (Space lets go).
                    bool release = PrototypeInput.GrappleCancelPressed
                                   || AnchorSurface == null || !AnchorSurface.isActiveAndEnabled
                                   || (t.grappleMode == GrappleMode.Swing && !PrototypeInput.GrappleHeld)
                                   || (t.grappleMode == GrappleMode.Pull && TimeLatched > t.grappleMaxPullTime);
                    if (release) { BeginRetract(); break; }
                    if (t.grappleMode == GrappleMode.SwingPull)
                    {
                        float minRope = m.Radius * 0.95f; // reel all the way to contact so the player attaches
                        if (IsReeling)
                            RopeLength = Mathf.Max(minRope, RopeLength - t.grappleSwingPullReelSpeed * dt);
                        else // swinging freely: W/S adjust the rope like Swing mode
                            RopeLength = Mathf.Clamp(RopeLength - PrototypeInput.MoveVector.y * t.grappleReelSpeed * dt,
                                minRope, t.grappleMaxDistance);
                    }
                    else if (t.grappleMode == GrappleMode.Swing)
                    {
                        float reel = PrototypeInput.MoveVector.y * t.grappleReelSpeed * dt;
                        RopeLength = Mathf.Clamp(RopeLength - reel, t.grappleMinRopeLength, t.grappleMaxDistance);
                    }
                    break;

                case GrappleState.Retracting:
                    if (_missFallTimer > 0f)
                    {
                        _missFallTimer -= dt;
                        _tipVelocity += Vector2.down * (t.gravity * dt);
                        TipPosition += _tipVelocity * dt;
                    }
                    else
                    {
                        TipPosition = Vector2.MoveTowards(TipPosition, m.Position, t.grappleRetractSpeed * dt);
                        if ((TipPosition - m.Position).sqrMagnitude < 0.01f)
                        {
                            State = GrappleState.Idle;
                            _cooldownUntil = Time.time + t.grappleCooldown;
                        }
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- physics hooks

        /// <summary>Pull mode is weightless by default while reeling in.</summary>
        public bool SuppressesGravity(MovementTuning t) =>
            IsLatched && t.grappleMode == GrappleMode.Pull && !t.grapplePullUsesGravity;

        /// <summary>
        /// Called once per airborne frame while latched.
        /// Pull: steer velocity toward the anchor. Without swinging, the whole velocity is steered, so the path
        /// is a straight line. With swinging, only the radial speed is steered; motion around the anchor is kept,
        /// and gravity bends it (scaled by grappleSwingAmount).
        /// Both modes: motion around the anchor is damped by (1 - grappleSwingAmount).
        /// </summary>
        public void ApplyAirborneVelocity(ref Vector2 velocity, Vector2 position, float dt, MovementTuning t)
        {
            if (!IsLatched) return;
            Vector2 to = AnchorPoint - position;
            if (to.sqrMagnitude < 1e-6f) return;
            Vector2 n = to.normalized; // toward the anchor

            if (t.grappleMode == GrappleMode.Pull && !t.grappleAllowSwingWhilePulling)
            {
                velocity = Vector2.MoveTowards(velocity, n * t.grapplePullSpeed, t.grapplePullAcceleration * dt);
                return;
            }

            float radial = Vector2.Dot(velocity, n);
            Vector2 tangential = velocity - n * radial;

            if (t.grappleMode == GrappleMode.SwingPull)
            {
                if (IsReeling)
                {
                    // Match the reel-in speed (so momentum is sensible if released), keep a little swing.
                    radial = Mathf.Max(radial, t.grappleSwingPullReelSpeed);
                    tangential *= Mathf.Exp(-(1f - t.grappleSwingPullSwingAmount) * t.grappleSwingDamping * dt);
                }
                else
                {
                    // Button released: swing freely on the rope (Swing Amount applies, 1 = undamped).
                    tangential *= Mathf.Exp(-(1f - t.grappleSwingAmount) * t.grappleSwingDamping * dt);
                }
                velocity = n * radial + tangential;
                return;
            }

            if (t.grappleMode == GrappleMode.Pull)
            {
                radial = Mathf.MoveTowards(radial, t.grapplePullSpeed, t.grapplePullAcceleration * dt);
                // Gravity is off while pulling (unless grapplePullUsesGravity), so add its around-the-anchor part
                // back in, scaled, to curve the pull into an arc.
                if (SuppressesGravity(t))
                {
                    Vector2 g = Vector2.down * t.gravity;
                    Vector2 gTangential = g - n * Vector2.Dot(g, n);
                    tangential += gTangential * (t.grappleSwingAmount * dt);
                }
            }

            tangential *= Mathf.Exp(-(1f - t.grappleSwingAmount) * t.grappleSwingDamping * dt);
            velocity = n * radial + tangential;
        }

        /// <summary>Swing: keep the player within rope length of the anchor. Called every airborne sub-step.</summary>
        public void ConstrainSubstep(ref Vector2 position, ref Vector2 velocity, MovementTuning t)
        {
            if (!IsLatched || t.grappleMode == GrappleMode.Pull) return;
            Vector2 d = position - AnchorPoint;
            float len = d.magnitude;
            if (len <= RopeLength || len < 1e-5f) return;
            Vector2 n = d / len;
            position = AnchorPoint + n * RopeLength;
            float outward = Vector2.Dot(velocity, n);
            if (outward > 0f) velocity -= n * outward;
        }

        /// <summary>Landing anywhere ends a latch (the leg lets go and retracts).</summary>
        public void NotifyPlayerAttached()
        {
            if (IsLatched) BeginRetract();
        }

        public void Release()
        {
            if (State == GrappleState.Shooting || State == GrappleState.Latched) BeginRetract();
        }

        public void ResetImmediate()
        {
            State = GrappleState.Idle;
            _missFallTimer = 0f;
            AnchorSurface = null;
        }

        // ---------------------------------------------------------------- internals

        void Fire(PlayerMotor2D m, MovementTuning t)
        {
            FireOrigin = m.Position;
            TipPosition = FireOrigin;
            _traveled = 0f;
            AnchorSurface = null;

            _willHit = FindTarget(m, t, m.AimDirection, out _target, out float dist, out Vector2 dir, out float bonkDist);
            _bonk = !_willHit && bonkDist > 0f;
            FireDirection = _willHit ? dir : m.AimDirection;
            _targetDistance = _willHit ? dist : _bonk ? bonkDist : t.grappleMaxDistance;
            State = GrappleState.Shooting;
        }

        void Latch(PlayerMotor2D m, MovementTuning t)
        {
            State = GrappleState.Latched;
            AnchorPoint = _target.point;
            AnchorSurface = _target.surface;
            float minRope = t.grappleMode == GrappleMode.SwingPull ? m.Radius * 0.95f : t.grappleMinRopeLength;
            RopeLength = Mathf.Clamp(Vector2.Distance(m.Position, AnchorPoint), minRope, t.grappleMaxDistance);
            _latchTime = Time.time;
            Latched?.Invoke(this);
        }

        void BeginMiss(MovementTuning t)
        {
            State = GrappleState.Retracting;
            if (t.grappleMissBehavior == GrappleMissBehavior.FallThenRetract)
            {
                _missFallTimer = t.grappleMissFallTime;
                _tipVelocity = FireDirection * (t.grappleShootSpeed * 0.15f);
            }
        }

        void BeginRetract()
        {
            State = GrappleState.Retracting;
            IsReeling = false;
            _missFallTimer = 0f;
            AnchorSurface = null;
        }

        /// <summary>
        /// Center ray first, then aim-assist rays. The first ray that hits an attachable surface other than
        /// the one the player stands on wins. If the CENTER ray first hits the player's own (or a
        /// non-attachable) surface, bonkDistance is set (the leg stops there and retracts).
        /// </summary>
        static bool FindTarget(PlayerMotor2D m, MovementTuning t, Vector2 aim, out SurfaceSample hit,
            out float distance, out Vector2 direction, out float bonkDistance)
        {
            hit = default;
            distance = 0f;
            direction = aim;
            bonkDistance = 0f;
            Vector2 origin = m.Position;
            Surface2D own = m.IsAttached ? m.CurrentSurface : null;
            int rays = t.grappleAimAssistAngle > 0.01f ? AssistFactors.Length : 1;

            for (int k = 0; k < rays; k++)
            {
                Vector2 dir = Surface2D.Rotate(aim, AssistFactors[k] * t.grappleAimAssistAngle).normalized;
                if (!RaycastAll(origin, dir, t.grappleMaxDistance, out SurfaceSample h)) continue;
                if (h.surface == own || !h.surface.Attachable)
                {
                    if (k == 0) bonkDistance = h.distance;
                    continue;
                }
                hit = h;
                distance = h.distance;
                direction = dir;
                return true;
            }
            return false;
        }

        static bool RaycastAll(Vector2 origin, Vector2 dir, float maxDistance, out SurfaceSample best)
        {
            best = default;
            float bestDist = float.MaxValue;
            var all = Surface2D.All;
            for (int i = 0; i < all.Count; i++)
            {
                Surface2D s = all[i];
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) continue;
                if (s.Raycast(origin, dir, maxDistance, out SurfaceSample h) && h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                }
            }
            return best.IsValid;
        }
    }
}
