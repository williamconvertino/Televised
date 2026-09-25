using System;
using System.Collections.Generic;
using System.Linq;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    public enum PlaytestCategory
    {
        Core,
        DoubleJump,
        Grapple
    }

    /// <summary>One playtest configuration: a named set of overrides applied on top of the shared baseline.</summary>
    public class PlaytestConfig
    {
        public string id;
        /// <summary>Designer-facing name (hidden from testers until the results screen).</summary>
        public string title;
        public PlaytestCategory category;
        /// <summary>What the tester is told before playing (controls only, no value judgements).</summary>
        public string playerHint;
        /// <summary>Why this config exists / what it tests.</summary>
        public string hypothesis;
        /// <summary>Short tag for the "feel" levers changed (air control, gravity, momentum...). "-" = baseline feel.</summary>
        public string feelTag = "-";
        public Action<MovementTuning> apply;

        public MovementTuning Build()
        {
            MovementTuning t = ScriptableObject.CreateInstance<MovementTuning>();
            PlaytestConfigCatalog.ApplyBaseline(t);
            apply?.Invoke(t);
            return t;
        }

        public static string FeatureOf(MovementTuning t)
        {
            if (t.enableGrapple) return "Grapple:" + t.grappleMode;
            if (t.enableAirJumps) return "DoubleJump:" + t.airJumpDirectionMode;
            return "None";
        }
    }

    /// <summary>
    /// The playtest design space. See Playtest/PLAYTEST_GUIDE.md for the reasoning behind each config.
    ///
    /// Fixed for every config: WASD + ScreenRelativeLocked, player size, and low-consequence settings
    /// (grapple miss behaviour, aim assist, cooldowns, buffers...), which are tuned once in ApplyBaseline.
    ///
    /// Core configs (C01-C20) vary the major axes: jump direction mode, attachment mode, candidate
    /// selection, plus a few "feel" levers (air control, gravity/arc, crawl momentum, speed-scaled attach).
    /// Feature configs (C21-C28) add double jump or one grapple mode on top of two fixed bases:
    ///   Base A = C01 (clamped cursor jump, aim-driven)   Base B = C03 (smoothed normal jump, no aiming)
    /// </summary>
    public static class PlaytestConfigCatalog
    {
        static List<PlaytestConfig> s_all;

        public static IReadOnlyList<PlaytestConfig> All => s_all ??= Create();

        public static PlaytestConfig Find(string id) => All.FirstOrDefault(c => c.id == id);

        /// <summary>The shared, comfortable baseline every config starts from.</summary>
        public static void ApplyBaseline(MovementTuning t)
        {
            // Fixed design decisions.
            t.surfaceMovementMode = SurfaceMovementMode.ScreenRelativeLocked;
            t.playerRadius = 0.5f;
            t.maxSurfaceAngle = 180f;
            t.steepSurfaceBehavior = SteepSurfaceBehavior.Stop;

            // Base A defaults (C01).
            t.jumpDirectionMode = JumpDirectionMode.ClampedCursorDirection;
            t.maxJumpAimAngle = 55f;
            t.attachmentMode = AttachmentMode.Nearest;
            t.candidateSelectionMode = CandidateSelectionMode.NearestGap;

            // Crawl.
            t.surfaceMoveSpeed = 5f;
            t.surfaceAcceleration = 60f;
            t.surfaceDeceleration = 80f;
            t.surfaceSnapStrength = 18f;
            t.compensateCurvature = true;
            t.allowSurfaceTransfer = true;
            t.inheritSurfaceVelocity = 0f;

            // Jump / air.
            t.jumpSpeed = 9f;
            t.gravity = 16f;
            t.maxFallSpeed = 25f;
            t.airControl = 0.25f;
            t.airMoveSpeed = 5f;
            t.airAcceleration = 30f;
            t.jumpBufferTime = 0.12f;
            t.landingJumpDelay = 0.1f;
            t.cursorIntoSurface = CursorIntoSurfaceBehavior.ClampAboveSurface;
            t.cursorMinSurfaceAngle = 10f;
            t.normalSampleSpacing = 0.45f;
            t.normalSmoothing = 0.06f;
            t.nearestSurfaceRange = 7f;
            t.assistRange = 7f;
            t.assistAngle = 30f;
            t.assistStrength = 0.6f;

            // Attachment.
            t.attachDistance = 0.5f;
            t.reattachCooldown = 0.15f;
            t.minimumApproachSpeed = 0f;
            t.maxApproachAngle = 180f;
            t.alwaysAttachOnContact = true;
            t.magnetRange = 1.6f;
            t.magnetStrength = 30f;
            t.attachRangeSpeedScale = new SpeedScale(VelocityComponent.Total, 10f, 1f, 0.25f, 2f);
            t.airControlSpeedScale = new SpeedScale(VelocityComponent.Falling, 15f, 1f, 0.1f, 3f);

            // Features off; comfortable settings for when a config turns them on.
            t.enableAirJumps = false;
            t.airJumpCount = 1;
            t.airJumpSpeed = 8.5f;
            t.airJumpDirectionMode = AirJumpDirectionMode.ClampedCursor;
            t.airJumpMaxAimAngle = 45f;
            t.airJumpMomentumKeep = 0.5f;
            t.airJumpMinDelay = 0.06f;
            t.airJumpLandingGrace = 0.3f;

            t.enableGrapple = false;
            t.grappleMode = GrappleMode.Pull;
            t.grappleMaxDistance = 9f;
            t.grappleShootSpeed = 50f;
            t.grappleRetractSpeed = 40f;
            t.grappleMissBehavior = GrappleMissBehavior.Retract;
            t.grappleAimAssistAngle = 8f;
            t.grappleCooldown = 0.1f;
            t.grappleLatchRefreshesAirJumps = true;
            t.grapplePullSpeed = 14f;
            t.grapplePullAcceleration = 90f;
            t.grapplePullUsesGravity = false;
            t.grappleAllowSwingWhilePulling = false;
            t.grappleMaxPullTime = 1.5f;
            t.grappleSwingAmount = 1f;
            t.grappleSwingDamping = 10f;
            t.grappleSwingPullReelSpeed = 9f;
            t.grappleSwingPullSwingAmount = 0.35f;
            t.grappleReelSpeed = 5f;
        }

        // Shared base overrides for feature configs.
        static void BaseA(MovementTuning t) { } // = baseline (C01)
        static void BaseB(MovementTuning t) => t.jumpDirectionMode = JumpDirectionMode.SmoothedSurfaceNormal; // = C03

        const string HintClamped = "Space jumps toward your cursor, but only within a cone pointing away from the surface.";
        const string HintNormal = "Space jumps straight away from the surface you're on. Use curved edges to angle your jumps.";

        static List<PlaytestConfig> Create()
        {
            var list = new List<PlaytestConfig>
            {
                // ------------------------------------------------ jump direction mode (Nearest / NearestGap)
                new PlaytestConfig
                {
                    id = "C01", title = "Baseline: clamped cursor jump (55°)", category = PlaytestCategory.Core,
                    playerHint = HintClamped,
                    hypothesis = "Reference point. Directional control without jumping into the surface.",
                    apply = t => { },
                },
                new PlaytestConfig
                {
                    id = "C02", title = "Local surface normal jump", category = PlaytestCategory.Core,
                    playerHint = HintNormal,
                    hypothesis = "Simplest, most readable jump; is aiming through surface curvature fun or frustrating?",
                    apply = t => t.jumpDirectionMode = JumpDirectionMode.LocalSurfaceNormal,
                },
                new PlaytestConfig
                {
                    id = "C03", title = "Smoothed (virtual-foot) normal jump", category = PlaytestCategory.Core,
                    playerHint = HintNormal,
                    hypothesis = "Like C02 but steadier on bumpy shapes. Does smoothing noticeably help vs C02?",
                    apply = BaseB,
                },
                new PlaytestConfig
                {
                    id = "C04", title = "Free cursor jump", category = PlaytestCategory.Core,
                    playerHint = "Space jumps exactly toward your cursor.",
                    hypothesis = "Full 360° control. Too demanding, or the most expressive?",
                    apply = t => t.jumpDirectionMode = JumpDirectionMode.CursorDirection,
                },
                new PlaytestConfig
                {
                    id = "C05", title = "Clamped cursor, tight cone (30°)", category = PlaytestCategory.Core,
                    playerHint = HintClamped,
                    hypothesis = "Mostly surface-driven with a little steering. Where is the sweet spot of the clamp?",
                    apply = t => t.maxJumpAimAngle = 30f,
                },
                new PlaytestConfig
                {
                    id = "C06", title = "Clamped cursor, wide cone (80°)", category = PlaytestCategory.Core,
                    playerHint = HintClamped,
                    hypothesis = "Nearly free aim but never into the surface.",
                    apply = t => t.maxJumpAimAngle = 80f,
                },
                new PlaytestConfig
                {
                    id = "C07", title = "Auto-target nearest surface jump", category = PlaytestCategory.Core,
                    playerHint = "Space automatically launches you toward the nearest other surface (if one is reachable).",
                    hypothesis = "Most automatic traversal. Does it fight the player's intent on a linear course?",
                    apply = t => t.jumpDirectionMode = JumpDirectionMode.NearestSurface,
                },
                new PlaytestConfig
                {
                    id = "C08", title = "Assisted cursor jump (30°, 0.6)", category = PlaytestCategory.Core,
                    playerHint = "Space jumps toward your cursor, and bends toward a nearby surface you're roughly aiming at.",
                    hypothesis = "Intent + forgiveness. Does the assist feel helpful or like losing control?",
                    apply = t => t.jumpDirectionMode = JumpDirectionMode.AssistedTarget,
                },

                // ------------------------------------------------ attachment / selection (clamped cursor jump)
                new PlaytestConfig
                {
                    id = "C09", title = "Strict attachment (contact only)", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " You only stick when you actually touch a surface.",
                    hypothesis = "Precise, physical landings. How much does losing the attach radius hurt?",
                    apply = t => t.attachmentMode = AttachmentMode.Strict,
                },
                new PlaytestConfig
                {
                    id = "C10", title = "Magnetic attachment", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Nearby surfaces pull you in.",
                    hypothesis = "Generous capture. Does magnetism feel good or does it steal jumps?",
                    apply = t => t.attachmentMode = AttachmentMode.Magnetic,
                },
                new PlaytestConfig
                {
                    id = "C11", title = "Magnetic + cursor-weighted selection", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Nearby surfaces pull you in, preferring ones near your cursor.",
                    hypothesis = "Can cursor intent resolve which surface grabs you in dense areas?",
                    apply = t =>
                    {
                        t.attachmentMode = AttachmentMode.Magnetic;
                        t.candidateSelectionMode = CandidateSelectionMode.WeightedScore;
                        t.distanceWeight = 1f; t.velocityWeight = 0.4f; t.cursorWeight = 1.2f; t.jumpDirectionWeight = 0.2f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C12", title = "Weighted selection (velocity-led), Nearest attach", category = PlaytestCategory.Core,
                    playerHint = HintClamped,
                    hypothesis = "Prefer the surface you're flying toward over the one that's merely closest.",
                    apply = t =>
                    {
                        t.candidateSelectionMode = CandidateSelectionMode.WeightedScore;
                        t.distanceWeight = 1f; t.velocityWeight = 1.2f; t.cursorWeight = 0.3f; t.jumpDirectionWeight = 0.5f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C13", title = "Big attach radius that shrinks with speed", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Easy to stick when slow; at speed you fly past surfaces.",
                    hypothesis = "Forgiving landings without accidental grabs during fast fly-bys.",
                    feelTag = "speed-scaled attach",
                    apply = t =>
                    {
                        t.attachDistance = 1.0f;
                        t.attachRangeSpeedScale = new SpeedScale(VelocityComponent.Total, 10f, 0.35f, 0.25f, 1f);
                    },
                },

                // ------------------------------------------------ feel levers
                new PlaytestConfig
                {
                    id = "C14", title = "Normal jump + strong air steering", category = PlaytestCategory.Core,
                    playerHint = HintNormal + " A/D steer strongly in the air.",
                    hypothesis = "Surface-driven jumps corrected mid-air. Does air control replace the need to aim?",
                    feelTag = "air control high",
                    apply = t =>
                    {
                        t.jumpDirectionMode = JumpDirectionMode.SmoothedSurfaceNormal;
                        t.airControl = 0.75f; t.airMoveSpeed = 6f; t.airAcceleration = 35f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C15", title = "Free cursor jump, zero air control", category = PlaytestCategory.Core,
                    playerHint = "Space jumps exactly toward your cursor. No steering once you're airborne.",
                    hypothesis = "Fully committed ballistic jumps. Satisfying or punishing?",
                    feelTag = "air control none",
                    apply = t =>
                    {
                        t.jumpDirectionMode = JumpDirectionMode.CursorDirection;
                        t.airControl = 0f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C16", title = "Momentum crawl (jumps inherit crawl speed)", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Crawling builds up speed, and your jumps carry that momentum.",
                    hypothesis = "Does momentum make crawling feel alive, or less precise?",
                    feelTag = "momentum",
                    apply = t =>
                    {
                        t.surfaceMoveSpeed = 7f; t.surfaceAcceleration = 10f; t.surfaceDeceleration = 9f;
                        t.inheritSurfaceVelocity = 0.7f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C17", title = "Heavy & snappy arcs", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Fast, heavy jumps.",
                    hypothesis = "Quick, decisive jumps. Better for precision?",
                    feelTag = "gravity high",
                    apply = t => { t.gravity = 28f; t.jumpSpeed = 12f; t.maxFallSpeed = 32f; },
                },
                new PlaytestConfig
                {
                    id = "C18", title = "Floaty arcs", category = PlaytestCategory.Core,
                    playerHint = HintClamped + " Slow, floaty jumps.",
                    hypothesis = "More time to read the air. Relaxing or sluggish?",
                    feelTag = "gravity low",
                    apply = t => { t.gravity = 9f; t.jumpSpeed = 7f; t.airControl = 0.3f; t.maxFallSpeed = 16f; },
                },

                // ------------------------------------------------ combinations of the extremes
                new PlaytestConfig
                {
                    id = "C19", title = "Local normal jump + strong magnet", category = PlaytestCategory.Core,
                    playerHint = HintNormal + " Nearby surfaces pull you in.",
                    hypothesis = "Can generous magnetism compensate for having no aim at all?",
                    apply = t =>
                    {
                        t.jumpDirectionMode = JumpDirectionMode.LocalSurfaceNormal;
                        t.attachmentMode = AttachmentMode.Magnetic; t.magnetRange = 2f; t.magnetStrength = 35f;
                    },
                },
                new PlaytestConfig
                {
                    id = "C20", title = "Full auto: nearest-surface jump + magnet", category = PlaytestCategory.Core,
                    playerHint = "Space automatically launches you toward the nearest other surface. Nearby surfaces pull you in.",
                    hypothesis = "Maximum automation. Is there still a game here?",
                    apply = t =>
                    {
                        t.jumpDirectionMode = JumpDirectionMode.NearestSurface;
                        t.attachmentMode = AttachmentMode.Magnetic;
                    },
                },

                // ------------------------------------------------ double jump
                new PlaytestConfig
                {
                    id = "C21", title = "Double jump (cursor cone) on Base A", category = PlaytestCategory.DoubleJump,
                    playerHint = HintClamped + " Press Space again in the air for one extra jump toward your cursor (within a cone).",
                    hypothesis = "Does a mid-air correction make aimed jumping more forgiving/fun?",
                    apply = t => { BaseA(t); t.enableAirJumps = true; t.airJumpDirectionMode = AirJumpDirectionMode.ClampedCursor; },
                },
                new PlaytestConfig
                {
                    id = "C22", title = "Double jump (WASD direction) on Base B", category = PlaytestCategory.DoubleJump,
                    playerHint = HintNormal + " Press Space again in the air for one extra jump in the WASD direction (up if none).",
                    hypothesis = "Double jump as the aiming tool for surface-driven jumps.",
                    apply = t => { BaseB(t); t.enableAirJumps = true; t.airJumpDirectionMode = AirJumpDirectionMode.MoveInput; },
                },

                // ------------------------------------------------ grapple
                new PlaytestConfig
                {
                    id = "C23", title = "Grapple: Pull, on Base A", category = PlaytestCategory.Grapple,
                    playerHint = HintClamped + " {grappleClick} shoots a grappling leg that pulls you to where it hits. {cancelClick} cancels.",
                    hypothesis = "Point-to-point zipping alongside aimed jumps.",
                    apply = t => { BaseA(t); t.enableGrapple = true; t.grappleMode = GrappleMode.Pull; },
                },
                new PlaytestConfig
                {
                    id = "C24", title = "Grapple: Pull, on Base B", category = PlaytestCategory.Grapple,
                    playerHint = HintNormal + " {grappleClick} shoots a grappling leg that pulls you to where it hits. {cancelClick} cancels.",
                    hypothesis = "Grapple as the only aimed movement tool.",
                    apply = t => { BaseB(t); t.enableGrapple = true; t.grappleMode = GrappleMode.Pull; },
                },
                new PlaytestConfig
                {
                    id = "C25", title = "Grapple: Swing, on Base A", category = PlaytestCategory.Grapple,
                    playerHint = HintClamped + " Hold {grappleButton} to grapple and swing. W/S shorten/lengthen the rope. Release to let go.",
                    hypothesis = "Pendulum swinging: skillful and fun, or hard to control?",
                    apply = t => { BaseA(t); t.enableGrapple = true; t.grappleMode = GrappleMode.Swing; },
                },
                new PlaytestConfig
                {
                    id = "C26", title = "Grapple: Swing, on Base B", category = PlaytestCategory.Grapple,
                    playerHint = HintNormal + " Hold {grappleButton} to grapple and swing. W/S shorten/lengthen the rope. Release to let go.",
                    hypothesis = "Swinging with surface-driven jumps.",
                    apply = t => { BaseB(t); t.enableGrapple = true; t.grappleMode = GrappleMode.Swing; },
                },
                new PlaytestConfig
                {
                    id = "C27", title = "Grapple: SwingPull hybrid, on Base A", category = PlaytestCategory.Grapple,
                    playerHint = HintClamped + " Hold {grappleButton} to grapple and reel in. Release to swing freely (W/S adjust rope). Space lets go.",
                    hypothesis = "Hybrid: controlled reel-in with swing freedom.",
                    apply = t => { BaseA(t); t.enableGrapple = true; t.grappleMode = GrappleMode.SwingPull; },
                },
                new PlaytestConfig
                {
                    id = "C28", title = "Grapple: SwingPull hybrid, on Base B", category = PlaytestCategory.Grapple,
                    playerHint = HintNormal + " Hold {grappleButton} to grapple and reel in. Release to swing freely (W/S adjust rope). Space lets go.",
                    hypothesis = "Hybrid grapple as the aiming tool.",
                    apply = t => { BaseB(t); t.enableGrapple = true; t.grappleMode = GrappleMode.SwingPull; },
                },
            };
            return list;
        }

        // ---------------------------------------------------------------- session selection

        /// <summary>
        /// Pick the configs for a session.
        /// Which: the first <paramref name="count"/> in catalog order, or (preferLeastRated) the ones with the fewest
        /// existing ratings, which evens out coverage across testers when each plays a subset.
        /// Order: catalog order (C01, C02, ...) unless <paramref name="shuffle"/>.
        /// </summary>
        public static List<PlaytestConfig> SelectForSession(int count, bool shuffle, bool preferLeastRated, bool core,
            bool doubleJump, bool grapple, IDictionary<string, int> existingRatingCounts, System.Random rng)
        {
            List<PlaytestConfig> pool = All.Where(c =>
                (core && c.category == PlaytestCategory.Core) ||
                (doubleJump && c.category == PlaytestCategory.DoubleJump) ||
                (grapple && c.category == PlaytestCategory.Grapple)).ToList();
            count = Mathf.Clamp(count, 1, Mathf.Max(1, pool.Count));

            IEnumerable<PlaytestConfig> chosen = preferLeastRated
                ? pool.OrderBy(c => existingRatingCounts != null && existingRatingCounts.TryGetValue(c.id, out int n) ? n : 0)
                      .ThenBy(_ => rng.Next())
                      .Take(count)
                : pool.Take(count);

            return shuffle
                ? chosen.OrderBy(_ => rng.Next()).ToList()
                : chosen.OrderBy(c => pool.IndexOf(c)).ToList();
        }
    }
}
