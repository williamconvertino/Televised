using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement
{
    /// <summary>
    /// Evaluates attachment candidates, applies the reattach cooldown, and selects a target.
    /// Detection lives in SurfaceSensor; this class only scores and chooses.
    /// </summary>
    public class SurfaceAttachmentController : MonoBehaviour
    {
        public Surface2D RecentlyDetachedSurface { get; private set; }
        public float RecentDetachTime { get; private set; } = -999f;

        /// <summary>Best eligible candidate from the last evaluation (the one we attach to).</summary>
        public SurfaceCandidate? SelectedTarget { get; private set; }
        /// <summary>Magnetic mode: the surface currently pulling the player (may not be in attach range yet).</summary>
        public SurfaceCandidate? MagnetTarget { get; private set; }

        /// <summary>Speed-scaled ranges from the last evaluation (for debug drawing and magnet pull).</summary>
        public float CurrentAttachDistance { get; private set; }
        public float CurrentMagnetRange { get; private set; }
        public float CurrentRangeScale { get; private set; } = 1f;

        Surface2D _previousTarget;
        Surface2D _previousMagnet;

        public float CooldownRemaining(MovementTuning t) =>
            RecentlyDetachedSurface == null ? 0f : Mathf.Max(0f, RecentDetachTime + t.reattachCooldown - Time.time);

        public bool IsBlocked(Surface2D s, MovementTuning t) =>
            s != null && s == RecentlyDetachedSurface && CooldownRemaining(t) > 0f;

        public void NotifyDetached(Surface2D s)
        {
            RecentlyDetachedSurface = s;
            RecentDetachTime = Time.time;
            _previousTarget = null;
            _previousMagnet = null;
            SelectedTarget = null;
            MagnetTarget = null;
        }

        public void NotifyAttached()
        {
            _previousTarget = null;
            _previousMagnet = null;
            SelectedTarget = null;
            MagnetTarget = null;
        }

        /// <summary>
        /// Score all candidates in place and pick the best eligible one.
        /// Returns true if the player should attach now.
        /// </summary>
        public bool Evaluate(List<SurfaceCandidate> candidates, Vector2 center, Vector2 velocity, Vector2 aimDirection,
            Vector2 lastJumpDirection, MovementTuning t, out SurfaceCandidate chosen)
        {
            chosen = default;
            float range = Mathf.Max(0.001f, t.SensorRange);
            float attachRange = t.EffectiveAttachDistance(velocity);
            float magnetRange = t.EffectiveMagnetRange(velocity);
            CurrentAttachDistance = attachRange;
            CurrentMagnetRange = magnetRange;
            CurrentRangeScale = t.attachRangeSpeedScale.Evaluate(velocity);
            Vector2 velDir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector2.zero;

            int bestIndex = -1, prevIndex = -1, magnetIndex = -1, prevMagnetIndex = -1;
            float bestScore = float.MinValue, magnetGap = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                SurfaceCandidate c = candidates[i];
                Surface2D s = c.Surface;
                Vector2 into = -c.sample.separation; // from player toward the surface

                c.blocked = !s.Attachable || IsBlocked(s, t);
                c.approachSpeed = Vector2.Dot(velocity, into);

                c.inAttachRange = c.gap <= attachRange;

                bool angleOk = t.maxApproachAngle >= 180f || velDir == Vector2.zero ||
                               Vector2.Angle(velDir, into) <= t.maxApproachAngle;
                c.approachOk = (c.approachSpeed >= t.minimumApproachSpeed && angleOk) ||
                               (t.alwaysAttachOnContact && c.gap <= 0.001f);

                c.eligible = !c.blocked && c.inAttachRange && c.approachOk;
                c.score = Score(c, center, velDir, aimDirection, lastJumpDirection, range, t);

                candidates[i] = c;

                if (!c.blocked && t.attachmentMode == AttachmentMode.Magnetic && c.gap <= magnetRange &&
                    c.approachSpeed > -1f)
                {
                    if (s == _previousMagnet) prevMagnetIndex = i;
                    if (c.gap < magnetGap) { magnetGap = c.gap; magnetIndex = i; }
                }

                if (!c.eligible) continue;
                if (s == _previousTarget) prevIndex = i;
                if (c.score > bestScore) { bestScore = c.score; bestIndex = i; }
            }

            // Hysteresis: keep the previous target unless the new best clearly beats it.
            if (prevIndex >= 0 && bestIndex != prevIndex &&
                candidates[prevIndex].score >= bestScore - t.targetSwitchMargin)
                bestIndex = prevIndex;

            if (prevMagnetIndex >= 0 && candidates[prevMagnetIndex].gap <= magnetGap + t.targetSwitchMargin)
                magnetIndex = prevMagnetIndex;
            MagnetTarget = magnetIndex >= 0 ? candidates[magnetIndex] : (SurfaceCandidate?)null;
            _previousMagnet = magnetIndex >= 0 ? candidates[magnetIndex].Surface : null;

            if (bestIndex < 0)
            {
                SelectedTarget = null;
                _previousTarget = null;
                return false;
            }

            chosen = candidates[bestIndex];
            SelectedTarget = chosen;
            _previousTarget = chosen.Surface;
            return true;
        }

        static float Score(in SurfaceCandidate c, Vector2 center, Vector2 velDir, Vector2 aimDir, Vector2 jumpDir,
            float range, MovementTuning t)
        {
            float distanceScore = 1f - Mathf.Clamp01(c.gap / range);
            if (t.candidateSelectionMode == CandidateSelectionMode.NearestGap)
                return distanceScore;

            Vector2 toPoint = c.sample.point - center;
            Vector2 toDir = toPoint.sqrMagnitude > 1e-8f ? toPoint.normalized : -c.sample.separation;

            float approachScore = velDir == Vector2.zero ? 0.5f : Vector2.Dot(velDir, toDir) * 0.5f + 0.5f;
            float cursorScore = aimDir == Vector2.zero ? 0.5f : Vector2.Dot(aimDir, toDir) * 0.5f + 0.5f;
            float jumpScore = jumpDir == Vector2.zero ? 0.5f : Vector2.Dot(jumpDir, toDir) * 0.5f + 0.5f;

            return t.distanceWeight * distanceScore
                 + t.velocityWeight * approachScore
                 + t.cursorWeight * cursorScore
                 + t.jumpDirectionWeight * jumpScore;
        }
    }
}
