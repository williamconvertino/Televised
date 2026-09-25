namespace Televised.Prototyping.Movement
{
    public enum PlayerMovementState
    {
        Attached,
        Airborne
    }

    /// <summary>How A/D is translated into a traversal sign along the current surface path.</summary>
    public enum SurfaceMovementMode
    {
        /// <summary>Direction chosen from screen left/right when the key is first held, then locked until release.</summary>
        ScreenRelativeLocked,
        /// <summary>Direction re-evaluated against screen left/right every frame (may flip near vertical tangents).</summary>
        ScreenRelativeContinuous,
        /// <summary>D = clockwise, A = counter-clockwise, regardless of screen orientation.</summary>
        SurfaceRelative
    }

    public enum JumpDirectionMode
    {
        LocalSurfaceNormal,
        SmoothedSurfaceNormal,
        CursorDirection,
        ClampedCursorDirection,
        NearestSurface,
        AssistedTarget
    }

    public enum AttachmentMode
    {
        /// <summary>Attach only on (near) actual contact.</summary>
        Strict,
        /// <summary>Attach to the best candidate as soon as it is within attachDistance.</summary>
        Nearest,
        /// <summary>Like Nearest, but surfaces within magnetRange also pull the player toward them.</summary>
        Magnetic
    }

    /// <summary>How the attachment controller picks among several valid candidates.</summary>
    public enum CandidateSelectionMode
    {
        /// <summary>Smallest gap wins.</summary>
        NearestGap,
        /// <summary>Weighted sum of distance / approach / cursor / jump alignment.</summary>
        WeightedScore
    }

    /// <summary>What unrestricted cursor jumping does when the cursor points into the attached surface.</summary>
    public enum CursorIntoSurfaceBehavior
    {
        /// <summary>Jump anyway (the player will immediately collide with the surface).</summary>
        Allow,
        /// <summary>Rotate the direction so it stays at least cursorMinSurfaceAngle above the surface.</summary>
        ClampAboveSurface,
        /// <summary>Ignore the cursor and use the surface normal.</summary>
        UseSurfaceNormal
    }

    /// <summary>Direction of an in-air (double) jump.</summary>
    public enum AirJumpDirectionMode
    {
        /// <summary>Straight up.</summary>
        Up,
        /// <summary>WASD direction (up if no input), clamped to airJumpMaxAimAngle from up.</summary>
        MoveInput,
        /// <summary>Toward the cursor, unrestricted (can dive downward).</summary>
        Cursor,
        /// <summary>Toward the cursor, clamped to airJumpMaxAimAngle from up.</summary>
        ClampedCursor
    }

    public enum GrappleMode
    {
        /// <summary>Reel the player toward the anchor until they attach.</summary>
        Pull,
        /// <summary>Rope constraint: swing from the anchor while the button is held; W/S reel in/out.</summary>
        Swing,
        /// <summary>
        /// Hybrid rope with gravity on. Hold LMB to reel in (swinging slightly, set by grappleSwingPullSwingAmount);
        /// release to swing freely on the rope (grappleSwingAmount; W/S adjust length). Space lets go.
        /// </summary>
        SwingPull
    }

    public enum GrappleMissBehavior
    {
        /// <summary>The leg retracts immediately after reaching max distance.</summary>
        Retract,
        /// <summary>The leg droops under gravity briefly, then retracts.</summary>
        FallThenRetract
    }
}
