using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// All gameplay tunables for the movement prototype in one asset.
    /// Because this is a ScriptableObject asset, edits made during Play Mode (Inspector or the
    /// on-screen debug panel) persist after exiting Play Mode. Duplicate the asset to keep presets.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototyping/Shared/Movement Tuning", fileName = "MovementTuning")]
    public class MovementTuning : ScriptableObject
    {
        [Header("Modes")]
        public SurfaceMovementMode surfaceMovementMode = SurfaceMovementMode.ScreenRelativeLocked;
        public JumpDirectionMode jumpDirectionMode = JumpDirectionMode.ClampedCursorDirection;
        public AttachmentMode attachmentMode = AttachmentMode.Nearest;
        public CandidateSelectionMode candidateSelectionMode = CandidateSelectionMode.NearestGap;

        [Header("Body")]
        [Min(0.05f)] public float playerRadius = 0.5f;

        [Header("Surface Movement")]
        [Min(0f)] public float surfaceMoveSpeed = 5f;
        [Tooltip("Units/s². 0 = instant arcade velocity.")]
        [Min(0f)] public float surfaceAcceleration = 60f;
        [Tooltip("Units/s². 0 = instant stop.")]
        [Min(0f)] public float surfaceDeceleration = 80f;
        [Tooltip("How fast the root blends onto the surface after attaching / transferring (1/s). Higher = snappier.")]
        [Min(0.1f)] public float surfaceSnapStrength = 18f;
        [Tooltip("Scale path steps so the player's CENTER moves at surfaceMoveSpeed on tight curves (instead of the contact point).")]
        public bool compensateCurvature = true;
        [Tooltip("When crawling into another surface (e.g. floor into wall), transfer onto it instead of stopping.")]
        public bool allowSurfaceTransfer = true;
        [Min(0f)] public float surfaceTransferDistance = 0.04f;
        [Tooltip("Screen-relative modes: how much the input as seen from the surface (surface normal = up) breaks ties, e.g. D on a wall.")]
        [Range(0f, 1f)] public float screenAmbiguityBias = 0.25f;
        [Tooltip("Screen-relative modes: input more perpendicular to the surface than this does nothing (e.g. W on a flat floor).")]
        [Range(0f, 0.9f)] public float minInputAlignment = 0.1f;
        [Tooltip("SurfaceRelative mode: false = D is clockwise, true = D is counter-clockwise.")]
        public bool invertSurfaceRelative = false;
        [Tooltip("Steepest surface the player can stick to, as the angle between the surface normal and world up. " +
                 "0 = flat floors only, 90 = floors and walls, 180 = anywhere (including ceilings).")]
        [Range(0f, 180f)] public float maxSurfaceAngle = 180f;
        [Tooltip("What happens when crawling reaches a surface steeper than Max Surface Angle.")]
        public SteepSurfaceBehavior steepSurfaceBehavior = SteepSurfaceBehavior.Stop;

        [Header("Jump")]
        [Min(0f)] public float jumpSpeed = 9f;
        [Tooltip("Fraction of the surface crawl velocity carried into the jump.")]
        [Range(0f, 1f)] public float inheritSurfaceVelocity = 0f;
        [Tooltip("Fraction of a moving/rotating surface's velocity carried into the jump (1 = physically natural).")]
        [Range(0f, 1f)] public float inheritPlatformVelocity = 1f;
        [Min(0f)] public float jumpBufferTime = 0.1f;
        [Tooltip("Jumps are held back until this long after landing (stops the 'skip' of an instant re-jump). " +
                 "A press during this window is kept and fires when it ends.")]
        [Min(0f)] public float landingJumpDelay = 0.1f;
        [Range(0f, 90f)] public float maxJumpAimAngle = 55f;
        public CursorIntoSurfaceBehavior cursorIntoSurface = CursorIntoSurfaceBehavior.ClampAboveSurface;
        [Range(0f, 89f)] public float cursorMinSurfaceAngle = 10f;

        [Header("Air Jumps (double jump)")]
        public bool enableAirJumps = false;
        [Range(0, 5)] public int airJumpCount = 1;
        [Tooltip("Launch speed. The jump REPLACES vertical velocity, so it feels the same whether rising or falling fast.")]
        [Min(0f)] public float airJumpSpeed = 8.5f;
        public AirJumpDirectionMode airJumpDirectionMode = AirJumpDirectionMode.ClampedCursor;
        [Range(0f, 180f)] public float airJumpMaxAimAngle = 45f;
        [Tooltip("Fraction of the current horizontal velocity kept on top of the air-jump velocity.")]
        [Range(0f, 1f)] public float airJumpMomentumKeep = 0.5f;
        [Tooltip("Air jumps are ignored this soon after leaving a surface (stops a double-tap spending both jumps).")]
        [Min(0f)] public float airJumpMinDelay = 0.06f;
        [Tooltip("If you're about to land (gap below this, moving toward the surface), a jump press waits and becomes a ground jump instead of spending an air jump.")]
        [Min(0f)] public float airJumpLandingGrace = 0.3f;

        [Header("Grapple Leg (mouse button: see PrototypeInput.GrappleButton)")]
        public bool enableGrapple = false;
        public GrappleMode grappleMode = GrappleMode.Pull;
        [Min(0.5f)] public float grappleMaxDistance = 7f;
        [Min(1f)] public float grappleShootSpeed = 45f;
        [Min(1f)] public float grappleRetractSpeed = 35f;
        public GrappleMissBehavior grappleMissBehavior = GrappleMissBehavior.FallThenRetract;
        [Min(0f)] public float grappleMissFallTime = 0.25f;
        [Tooltip("Extra rays at ±half and ±full this angle around the aim, used when the center ray misses. 0 = exact aim only.")]
        [Range(0f, 30f)] public float grappleAimAssistAngle = 6f;
        [Min(0f)] public float grappleCooldown = 0.15f;
        [Tooltip("Latching onto a surface gives back all air jumps.")]
        public bool grappleLatchRefreshesAirJumps = true;
        [Tooltip("Pull: target speed toward the anchor.")]
        [Min(0f)] public float grapplePullSpeed = 14f;
        [Min(0f)] public float grapplePullAcceleration = 80f;
        public bool grapplePullUsesGravity = false;
        [Tooltip("Pull: release automatically if the player hasn't attached after this long.")]
        [Min(0.1f)] public float grappleMaxPullTime = 1.5f;
        [Tooltip("Pull mode: keep sideways (swinging) motion around the anchor while being reeled in, instead of flying straight at it.")]
        public bool grappleAllowSwingWhilePulling = false;
        [Tooltip("How much swinging motion the grapple allows (both modes). 1 = free pendulum; lower values damp motion " +
                 "around the anchor; 0 = hardly any swing. In Pull mode it also scales how much gravity bends the path " +
                 "(only when swinging while pulling is on).")]
        [Range(0f, 1f)] public float grappleSwingAmount = 1f;
        [Tooltip("How quickly swing motion is damped when Swing Amount is 0 (1/s). The damping rate is (1 - swingAmount) times this.")]
        [Min(0f)] public float grappleSwingDamping = 10f;
        [Tooltip("SwingPull: how fast the rope reels in while the grapple button is held (units/s).")]
        [Min(0f)] public float grappleSwingPullReelSpeed = 9f;
        [Tooltip("SwingPull: how much swinging is allowed while reeling in (0 = almost straight, 1 = free pendulum). " +
                 "When the grapple button is released, grappleSwingAmount applies instead.")]
        [Range(0f, 1f)] public float grappleSwingPullSwingAmount = 0.35f;
        [Tooltip("Swing: W/S reel speed. Reeling in goes all the way to contact, so the player can attach.")]
        [Min(0f)] public float grappleReelSpeed = 4f;

        [Header("Smoothed / Virtual-Foot Normal")]
        [Tooltip("Half-distance between the two virtual contact samples along the path.")]
        [Min(0.01f)] public float normalSampleSpacing = 0.45f;
        [Tooltip("Time constant (s) of temporal smoothing applied to the virtual-foot normal. 0 = none.")]
        [Min(0f)] public float normalSmoothing = 0.06f;

        [Header("Targeted Jumps (NearestSurface / AssistedTarget)")]
        [Min(0f)] public float nearestSurfaceRange = 7f;
        [Min(0f)] public float assistRange = 7f;
        [Range(0f, 90f)] public float assistAngle = 30f;
        [Range(0f, 1f)] public float assistStrength = 0.6f;
        [Tooltip("Use the high ballistic arc when solving targeted jumps.")]
        public bool preferHighArc = false;

        [Header("Airborne")]
        [Min(0f)] public float gravity = 16f;
        [Tooltip("Multiplier on air acceleration (0 = no air control).")]
        [Range(0f, 1f)] public float airControl = 0.2f;
        [Tooltip("Top horizontal speed the player can steer to in the air (input never slows you below your current speed).")]
        [Min(0f)] public float airMoveSpeed = 5f;
        [Min(0f)] public float airAcceleration = 30f;
        [Tooltip("Scales airMoveSpeed and air acceleration by velocity. Default: by falling speed, 1.0 = no effect.")]
        public SpeedScale airControlSpeedScale = new SpeedScale(VelocityComponent.Falling, 15f, 1f, 0.1f, 3f);
        [Min(0f)] public float maxFallSpeed = 25f;

        [Header("Attachment")]
        [Tooltip("Max gap between the player's edge and a surface for attachment (Nearest/Magnetic).")]
        [Min(0f)] public float attachDistance = 0.5f;
        [Tooltip("Gap tolerance for Strict mode.")]
        [Min(0f)] public float strictContactTolerance = 0.03f;
        [Min(0f)] public float reattachCooldown = 0.15f;
        [Tooltip("Required speed toward a surface to attach (negative allows attaching while moving slightly away).")]
        public float minimumApproachSpeed = 0f;
        [Tooltip("Max angle between velocity and the direction into the surface. 180 = no angle check.")]
        [Range(0f, 180f)] public float maxApproachAngle = 180f;
        [Tooltip("Always allow attaching to a non-blocked surface the player is touching, ignoring approach checks.")]
        public bool alwaysAttachOnContact = true;
        [Tooltip("A new candidate must beat the current target's score by this much to replace it (prevents oscillation).")]
        [Min(0f)] public float targetSwitchMargin = 0.08f;
        [Tooltip("Scales attachDistance / magnetRange by the player's speed. 1.0 at the reference speed = no effect; <1 makes fast fly-bys harder to catch.")]
        public SpeedScale attachRangeSpeedScale = new SpeedScale(VelocityComponent.Total, 10f, 1f, 0.25f, 2f);
        public bool speedScaleAttachDistance = true;
        public bool speedScaleMagnetRange = true;

        [Header("Magnetic Mode")]
        [Min(0f)] public float magnetRange = 1.6f;
        [Tooltip("Acceleration toward the magnet target at zero gap (units/s²).")]
        [Min(0f)] public float magnetStrength = 30f;

        [Header("Candidate Scoring (WeightedScore)")]
        [Min(0f)] public float distanceWeight = 1f;
        [Min(0f)] public float velocityWeight = 0.5f;
        [Min(0f)] public float cursorWeight = 0.3f;
        [Min(0f)] public float jumpDirectionWeight = 0.2f;

        /// <summary>True if the player may stick to a surface with this outward normal (see maxSurfaceAngle).</summary>
        public bool AllowsSurfaceNormal(Vector2 normal, float slack = 0f) =>
            maxSurfaceAngle >= 180f || Vector2.Angle(Vector2.up, normal) <= maxSurfaceAngle + slack;

        /// <summary>Attach distance (Nearest/Magnetic) or contact tolerance (Strict) at the given velocity.</summary>
        public float EffectiveAttachDistance(Vector2 velocity)
        {
            if (attachmentMode == AttachmentMode.Strict) return strictContactTolerance;
            return attachDistance * (speedScaleAttachDistance ? attachRangeSpeedScale.Evaluate(velocity) : 1f);
        }

        public float EffectiveMagnetRange(Vector2 velocity) =>
            magnetRange * (speedScaleMagnetRange ? attachRangeSpeedScale.Evaluate(velocity) : 1f);

        /// <summary>Largest range the sensor ever needs (covers the maximum speed scale).</summary>
        public float SensorRange
        {
            get
            {
                float maxScale = Mathf.Max(1f, attachRangeSpeedScale.minScale, attachRangeSpeedScale.maxScale);
                float attach = attachDistance * (speedScaleAttachDistance ? maxScale : 1f);
                float magnet = magnetRange * (speedScaleMagnetRange ? maxScale : 1f);
                return Mathf.Max(attach, strictContactTolerance, magnet, surfaceTransferDistance) + 0.05f;
            }
        }
    }
}
