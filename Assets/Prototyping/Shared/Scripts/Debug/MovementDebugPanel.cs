using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// On-screen status text + tuning panel (IMGUI) with hotkeys, so movement variants can be
    /// compared without leaving Play Mode. Edits go straight into the MovementTuning asset.
    /// </summary>
    [DefaultExecutionOrder(-100)] // runs before the motor so PointerBlocked is current
    public class MovementDebugPanel : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        [SerializeField] MovementDebugRenderer debugRenderer;
        [SerializeField] ProceduralLegRig legRig;
        [SerializeField] bool panelVisible = true;
        [SerializeField] bool showSurfaceLabels = true;
        [Tooltip("Show the status box and key hints even while the tuning panel is closed. Off: only with the panel " +
                 "(the status box blocks clicks under it, which other prototypes may need).")]
        [SerializeField] bool alwaysShowStatus = true;
        [SerializeField, Range(0.5f, 3f)] float uiScale = 1f;

        public const float PanelWidth = 300f;
        /// <summary>Whether the tuning panel (right side) is showing. Other overlays lay themselves out around it.</summary>
        public bool PanelVisible { get => panelVisible; set => panelVisible = value; }
        public float UiScale => uiScale;
        Vector2 _scroll;
        GUIStyle _box, _label, _header, _worldLabel;

        void Update()
        {
            if (motor == null) return;
            MovementTuning t = motor.Tuning;

            if (PrototypeInput.Pressed(Key.F1)) { t.surfaceMovementMode = Next(t.surfaceMovementMode); MarkDirty(t); }
            if (PrototypeInput.Pressed(Key.F2)) { t.jumpDirectionMode = Next(t.jumpDirectionMode); MarkDirty(t); }
            if (PrototypeInput.Pressed(Key.F3)) { t.attachmentMode = Next(t.attachmentMode); MarkDirty(t); }
            if (PrototypeInput.Pressed(Key.F4)) { t.candidateSelectionMode = Next(t.candidateSelectionMode); MarkDirty(t); }
            if (PrototypeInput.Pressed(Key.F5) && debugRenderer != null) debugRenderer.drawEnabled = !debugRenderer.drawEnabled;
            if (PrototypeInput.Pressed(Key.F6) || PrototypeInput.Pressed(Key.Tab)) panelVisible = !panelVisible;
            if (PrototypeInput.Pressed(Key.F7) && legRig != null) legRig.LegCount = legRig.LegCount == 2 ? 4 : 2;
            if (PrototypeInput.Pressed(Key.F8)) { t.enableAirJumps = !t.enableAirJumps; MarkDirty(t); }
            if (PrototypeInput.Pressed(Key.F9)) { t.enableGrapple = !t.enableGrapple; MarkDirty(t); }

            // Stop clicks on the UI from firing the grapple.
            Vector2 mouse = PrototypeInput.MouseScreen;
            Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y) / uiScale;
            float screenW = Screen.width / uiScale, screenH = Screen.height / uiScale;
            bool overStatus = StatusVisible && new Rect(10, 10, 360, 300).Contains(gui);
            bool overPanel = panelVisible && new Rect(screenW - PanelWidth - 10, 10, PanelWidth, screenH - 80).Contains(gui);
            PrototypeInput.PointerBlocked = overStatus || overPanel;
        }

        bool StatusVisible => alwaysShowStatus || panelVisible;

        static T Next<T>(T value) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, value);
            return values[(i + 1) % values.Length];
        }

        static void MarkDirty(UnityEngine.Object o)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(o);
#endif
        }

        void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            _worldLabel = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            _worldLabel.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        }

        void OnGUI()
        {
            if (motor == null) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float screenW = Screen.width / uiScale, screenH = Screen.height / uiScale;
            MovementTuning t = motor.Tuning;

            if (showSurfaceLabels) DrawSurfaceLabels(screenH);

            if (!StatusVisible) return;

            // ---- status
            GUILayout.BeginArea(new Rect(10, 10, 360, 300), _box);
            GUILayout.Label(StatusText(t), _label);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(10, screenH - 58, 900, 50));
            GUILayout.Label("<b>WASD/Arrows</b> crawl   <b>Space</b> jump   <b>Mouse</b> aim   <b>F1</b> move mode   <b>F2</b> jump mode   " +
                            "<b>F3</b> attach mode   <b>F4</b> selection   <b>F5</b> debug lines   <b>Tab/F6</b> panel   <b>F7</b> 2/4 legs   <b>F8</b> air jumps   <b>F9</b> grapple\n" +
                            $"<b>{GrappleBtn}</b> grapple ({CancelBtn} cancel)   <b>R</b> respawn   <b>1-9</b> spawn points   <b>T</b> teleport to cursor   <b>C</b> overview camera   <b>Scroll</b> zoom", _label);
            GUILayout.EndArea();

            if (!panelVisible) return;

            // ---- tuning panel
            float w = PanelWidth;
            GUILayout.BeginArea(new Rect(screenW - w - 10, 10, w, screenH - 80), _box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            EditorLikeTuning(t);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string GrappleBtn => PrototypeInput.GrappleButton == GrappleMouseButton.Right ? "RMB" : "LMB";
        static string CancelBtn => PrototypeInput.GrappleButton == GrappleMouseButton.Right ? "LMB" : "RMB";

        void DrawSurfaceLabels(float screenH)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (Surface2D s in Surface2D.All)
            {
                if (s == null || !s.isActiveAndEnabled || !s.HasGeometry) continue;
                Rect b = s.WorldBounds;
                if (b.width > 20f || b.height > 20f) continue; // skip arena walls/floor
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(b.center.x, b.center.y, 0f));
                if (sp.z < 0f) continue;
                var r = new Rect(sp.x / uiScale - 80f, screenH - sp.y / uiScale - 9f, 160f, 18f);
                GUI.Label(r, s.name, _worldLabel);
            }
        }

        string GrappleStatus(MovementTuning t)
        {
            GrappleController g = motor.Grapple;
            if (g == null) return "-";
            return t.enableGrapple || g.IsActive ? g.State.ToString() : "off";
        }

        string StatusText(MovementTuning t)
        {
            SurfaceAttachmentController att = motor.Attachment;
            string surface = motor.CurrentSurface != null ? motor.CurrentSurface.name : "-";
            string target = att != null && att.SelectedTarget is SurfaceCandidate s ? s.Surface.name : "-";
            string detached = att != null && att.RecentlyDetachedSurface != null && att.CooldownRemaining(t) > 0f
                ? $"{att.RecentlyDetachedSurface.name} ({att.CooldownRemaining(t):0.00}s)"
                : "-";
            JumpResult jr = motor.JumpResolver != null ? motor.JumpResolver.LastResult : default;
            string jumpInfo = jr.hasTarget && jr.targetSurface != null ? $" -> {jr.targetSurface.name}" : jr.usedFallback ? " (no target, fallback)" : "";

            return
                $"<b>State:</b> {motor.State}\n" +
                $"<b>Surface:</b> {surface}   <b>Path:</b> {motor.PathPosition:0.00}\n" +
                $"<b>Movement Mode:</b> {t.surfaceMovementMode}\n" +
                $"<b>Jump Mode:</b> {t.jumpDirectionMode}{(motor.IsAttached ? jumpInfo : "")}\n" +
                $"<b>Attachment:</b> {t.attachmentMode} / {t.candidateSelectionMode}\n" +
                $"<b>Traversal Sign:</b> {motor.TraversalSign:+0;-0;0}   <b>Lock:</b> {(motor.InputResolver != null ? motor.InputResolver.LockDescription : "-")}   <b>Input:</b> {(motor.InputResolver != null ? motor.InputResolver.LastInput : Vector2.zero)}\n" +
                $"<b>Speed:</b> {(motor.IsAttached ? Mathf.Abs(motor.SurfaceSpeed) : motor.Velocity.magnitude):0.0}   <b>Velocity:</b> {motor.Velocity.x:0.0}, {motor.Velocity.y:0.0}\n" +
                $"<b>Candidates:</b> {(motor.Sensor != null && !motor.IsAttached ? motor.Sensor.Candidates.Count : 0)}   <b>Target:</b> {target}\n" +
                $"<b>Recently detached:</b> {detached}\n" +
                $"<b>Range scale:</b> x{t.attachRangeSpeedScale.Evaluate(motor.IsAttached ? Vector2.zero : motor.Velocity):0.00}   " +
                $"<b>Air control scale:</b> x{t.airControlSpeedScale.Evaluate(motor.Velocity):0.00}\n" +
                $"<b>Air jumps:</b> {(t.enableAirJumps ? $"{motor.AirJumpsRemaining}/{t.airJumpCount}" : "off")}   " +
                $"<b>Grapple:</b> {GrappleStatus(t)}{(motor.Grapple != null && motor.Grapple.IsLatched && t.grappleMode == GrappleMode.SwingPull ? (motor.Grapple.IsReeling ? " (reeling)" : " (swinging)") : "")}\n" +
                $"<b>Tuning:</b> {(motor.PersistTuningChanges ? "asset (changes persist)" : "runtime copy (not saved)")}";
        }

        void EditorLikeTuning(MovementTuning t)
        {
            PersistenceControls();
            t = motor.Tuning; // may have switched between asset and runtime copy
            GUI.changed = false;

            GUILayout.Label("Modes", _header);
            t.surfaceMovementMode = EnumButton("Move (F1)", t.surfaceMovementMode);
            t.jumpDirectionMode = EnumButton("Jump (F2)", t.jumpDirectionMode);
            t.attachmentMode = EnumButton("Attach (F3)", t.attachmentMode);
            t.candidateSelectionMode = EnumButton("Select (F4)", t.candidateSelectionMode);
            t.cursorIntoSurface = EnumButton("Cursor into surface", t.cursorIntoSurface);

            GUILayout.Space(6);
            GUILayout.Label("Surface Movement", _header);
            t.surfaceMoveSpeed = Slider("Move Speed", t.surfaceMoveSpeed, 0f, 12f);
            t.surfaceAcceleration = Slider("Acceleration (0=instant)", t.surfaceAcceleration, 0f, 150f);
            t.surfaceDeceleration = Slider("Deceleration (0=instant)", t.surfaceDeceleration, 0f, 150f);
            t.surfaceSnapStrength = Slider("Snap Strength", t.surfaceSnapStrength, 1f, 60f);
            t.compensateCurvature = GUILayout.Toggle(t.compensateCurvature, " Compensate curvature");
            t.allowSurfaceTransfer = GUILayout.Toggle(t.allowSurfaceTransfer, " Crawl onto touching surfaces");
            t.screenAmbiguityBias = Slider("Surface-Relative Tie-Break", t.screenAmbiguityBias, 0f, 1f);
            t.minInputAlignment = Slider("Min Input Alignment", t.minInputAlignment, 0f, 0.9f);
            t.maxSurfaceAngle = Slider(t.maxSurfaceAngle >= 180f ? "Max Surface Angle (anywhere)" : "Max Surface Angle (90 = walls)",
                t.maxSurfaceAngle, 0f, 180f);
            if (t.maxSurfaceAngle < 180f)
                t.steepSurfaceBehavior = EnumButton("At the limit", t.steepSurfaceBehavior);

            GUILayout.Space(6);
            GUILayout.Label("Jump", _header);
            t.jumpSpeed = Slider("Jump Speed", t.jumpSpeed, 0f, 20f);
            t.landingJumpDelay = Slider("Jump Delay After Landing (s)", t.landingJumpDelay, 0f, 0.3f);
            t.maxJumpAimAngle = Slider("Cursor Clamp Angle", t.maxJumpAimAngle, 0f, 90f);
            t.normalSampleSpacing = Slider("Normal Sample Spacing", t.normalSampleSpacing, 0.05f, 1.5f);
            t.normalSmoothing = Slider("Normal Smoothing (s)", t.normalSmoothing, 0f, 0.4f);
            t.inheritSurfaceVelocity = Slider("Inherit Crawl Velocity", t.inheritSurfaceVelocity, 0f, 1f);
            t.assistAngle = Slider("Assist Angle", t.assistAngle, 0f, 90f);
            t.assistStrength = Slider("Assist Strength", t.assistStrength, 0f, 1f);
            t.preferHighArc = GUILayout.Toggle(t.preferHighArc, " Targeted jumps: high arc");

            GUILayout.Space(6);
            GUILayout.Label("Airborne", _header);
            t.gravity = Slider("Gravity", t.gravity, 0f, 40f);
            t.airControl = Slider("Air Control", t.airControl, 0f, 1f);
            t.airMoveSpeed = Slider("Air Move Speed", t.airMoveSpeed, 0f, 15f);
            t.airAcceleration = Slider("Air Acceleration", t.airAcceleration, 0f, 100f);
            t.airControlSpeedScale = SpeedScaleGUI("Air control speed scale", t.airControlSpeedScale, 30f);

            GUILayout.Space(6);
            GUILayout.Label("Attachment", _header);
            t.attachDistance = Slider("Attach Distance", t.attachDistance, 0f, 2f);
            t.reattachCooldown = Slider("Reattach Cooldown", t.reattachCooldown, 0f, 0.6f);
            t.minimumApproachSpeed = Slider("Min Approach Speed", t.minimumApproachSpeed, -5f, 5f);
            t.maxApproachAngle = Slider("Max Approach Angle", t.maxApproachAngle, 0f, 180f);
            t.alwaysAttachOnContact = GUILayout.Toggle(t.alwaysAttachOnContact, " Always attach on contact");
            t.magnetRange = Slider("Magnet Range", t.magnetRange, 0f, 4f);
            t.magnetStrength = Slider("Magnet Strength", t.magnetStrength, 0f, 100f);
            t.targetSwitchMargin = Slider("Target Switch Margin", t.targetSwitchMargin, 0f, 0.5f);
            t.attachRangeSpeedScale = SpeedScaleGUI("Attach/magnet range speed scale", t.attachRangeSpeedScale, 25f);
            t.speedScaleAttachDistance = GUILayout.Toggle(t.speedScaleAttachDistance, " Scale attach distance");
            t.speedScaleMagnetRange = GUILayout.Toggle(t.speedScaleMagnetRange, " Scale magnet range");

            GUILayout.Space(6);
            GUILayout.Label("Air Jumps (F8)", _header);
            t.enableAirJumps = GUILayout.Toggle(t.enableAirJumps, " Enabled");
            if (t.enableAirJumps)
            {
                t.airJumpCount = Mathf.RoundToInt(Slider("Air Jump Count", t.airJumpCount, 0f, 5f));
                t.airJumpSpeed = Slider("Air Jump Speed", t.airJumpSpeed, 0f, 20f);
                t.airJumpDirectionMode = EnumButton("Direction", t.airJumpDirectionMode);
                t.airJumpMaxAimAngle = Slider("Max Aim Angle (from up)", t.airJumpMaxAimAngle, 0f, 180f);
                t.airJumpMomentumKeep = Slider("Horizontal Momentum Kept", t.airJumpMomentumKeep, 0f, 1f);
                t.airJumpMinDelay = Slider("Min Delay After Leaving", t.airJumpMinDelay, 0f, 0.3f);
                t.airJumpLandingGrace = Slider("Landing Grace (buffer instead)", t.airJumpLandingGrace, 0f, 1.5f);
            }

            GUILayout.Space(6);
            GUILayout.Label($"Grapple Leg (F9, {GrappleBtn})", _header);
            t.enableGrapple = GUILayout.Toggle(t.enableGrapple, " Enabled");
            PrototypeInput.GrappleButton = EnumButton("Grapple Button", PrototypeInput.GrappleButton);
            if (t.enableGrapple)
            {
                t.grappleMode = EnumButton("Mode", t.grappleMode);
                t.grappleMissBehavior = EnumButton("On Miss", t.grappleMissBehavior);
                t.grappleMaxDistance = Slider("Max Distance", t.grappleMaxDistance, 1f, 20f);
                t.grappleShootSpeed = Slider("Shoot Speed", t.grappleShootSpeed, 5f, 120f);
                t.grappleRetractSpeed = Slider("Retract Speed", t.grappleRetractSpeed, 5f, 120f);
                t.grappleMissFallTime = Slider("Miss Droop Time", t.grappleMissFallTime, 0f, 1f);
                t.grappleAimAssistAngle = Slider("Aim Assist Angle", t.grappleAimAssistAngle, 0f, 30f);
                t.grappleCooldown = Slider("Cooldown", t.grappleCooldown, 0f, 1f);
                t.grappleLatchRefreshesAirJumps = GUILayout.Toggle(t.grappleLatchRefreshesAirJumps, " Latch refreshes air jumps");
                t.grappleSwingAmount = Slider(t.grappleMode == GrappleMode.SwingPull ? $"Free Swing Amount ({GrappleBtn} released)" : "Swing Amount (1 = free)",
                    t.grappleSwingAmount, 0f, 1f);
                t.grappleSwingDamping = Slider("Swing Damping at 0", t.grappleSwingDamping, 0f, 30f);
                if (t.grappleMode == GrappleMode.SwingPull)
                {
                    t.grappleSwingPullReelSpeed = Slider($"Reel-In Speed (hold {GrappleBtn})", t.grappleSwingPullReelSpeed, 0f, 30f);
                    t.grappleSwingPullSwingAmount = Slider("Swing While Reeling", t.grappleSwingPullSwingAmount, 0f, 1f);
                    t.grappleReelSpeed = Slider("Rope Adjust Speed (W/S, released)", t.grappleReelSpeed, 0f, 15f);
                }
                else if (t.grappleMode == GrappleMode.Pull)
                {
                    t.grapplePullSpeed = Slider("Pull Speed", t.grapplePullSpeed, 0f, 40f);
                    t.grapplePullAcceleration = Slider("Pull Acceleration", t.grapplePullAcceleration, 0f, 300f);
                    t.grapplePullUsesGravity = GUILayout.Toggle(t.grapplePullUsesGravity, " Gravity while pulling");
                    t.grappleAllowSwingWhilePulling = GUILayout.Toggle(t.grappleAllowSwingWhilePulling, " Allow swing while pulling");
                    t.grappleMaxPullTime = Slider("Max Pull Time", t.grappleMaxPullTime, 0.1f, 5f);
                }
                else
                {
                    t.grappleReelSpeed = Slider("Reel Speed (W/S)", t.grappleReelSpeed, 0f, 15f);
                }
            }

            if (t.candidateSelectionMode == CandidateSelectionMode.WeightedScore)
            {
                GUILayout.Space(6);
                GUILayout.Label("Candidate Weights", _header);
                t.distanceWeight = Slider("Distance", t.distanceWeight, 0f, 2f);
                t.velocityWeight = Slider("Velocity", t.velocityWeight, 0f, 2f);
                t.cursorWeight = Slider("Cursor", t.cursorWeight, 0f, 2f);
                t.jumpDirectionWeight = Slider("Jump Direction", t.jumpDirectionWeight, 0f, 2f);
            }

            if (GUI.changed) MarkDirty(t);

            if (debugRenderer != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Debug Draw (F5)", _header);
                debugRenderer.drawEnabled = GUILayout.Toggle(debugRenderer.drawEnabled, " Enabled");
                debugRenderer.showSurfaceFrame = GUILayout.Toggle(debugRenderer.showSurfaceFrame, " Surface point / normal / tangent");
                debugRenderer.showVirtualFeet = GUILayout.Toggle(debugRenderer.showVirtualFeet, " Virtual feet samples");
                debugRenderer.showJumpPreview = GUILayout.Toggle(debugRenderer.showJumpPreview, " Jump vector + arc");
                debugRenderer.showCandidates = GUILayout.Toggle(debugRenderer.showCandidates, " Attach candidates");
                debugRenderer.showVelocity = GUILayout.Toggle(debugRenderer.showVelocity, " Velocity");
                debugRenderer.showMouseProbe = GUILayout.Toggle(debugRenderer.showMouseProbe, " Mouse surface probe");
                debugRenderer.showAllSurfaceNormals = GUILayout.Toggle(debugRenderer.showAllSurfaceNormals, " All surface normals");
                showSurfaceLabels = GUILayout.Toggle(showSurfaceLabels, " Surface name labels");
            }

            if (legRig != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Legs (F7)", _header);
                bool four = GUILayout.Toggle(legRig.LegCount == 4, " Four legs");
                legRig.LegCount = four ? 4 : 2;
                legRig.HideOtherLegsWhileGrappling = GUILayout.Toggle(legRig.HideOtherLegsWhileGrappling, " Hide other legs while grappling");
            }
        }

        void PersistenceControls()
        {
            GUILayout.Label("Tuning Persistence", _header);
            motor.PersistTuningChanges = GUILayout.Toggle(motor.PersistTuningChanges, " Persist changes to asset");
            if (motor.PersistTuningChanges)
            {
                GUILayout.Label("<i>Edits go straight into the asset and are kept after Play Mode.</i>", _label);
            }
            else
            {
                GUILayout.Label("<i>Editing a runtime copy. Changes are discarded when Play Mode ends.</i>", _label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save to asset")) motor.SaveTuningToAsset();
                if (GUILayout.Button("Revert to asset")) motor.RevertTuningToAsset();
                GUILayout.EndHorizontal();
#if UNITY_EDITOR
                if (GUILayout.Button("Inspect runtime copy")) UnityEditor.Selection.activeObject = motor.Tuning;
#endif
            }
            GUILayout.Space(6);
        }

        SpeedScale SpeedScaleGUI(string label, SpeedScale sc, float maxReferenceSpeed)
        {
            GUILayout.Label($"<b>{label}</b>", _label);
            sc.source = EnumButton("  Speed source", sc.source);
            sc.referenceSpeed = Slider("  Reference Speed", sc.referenceSpeed, 0.5f, maxReferenceSpeed);
            sc.scaleAtReference = Slider("  Scale at Reference (1 = off)", sc.scaleAtReference, 0f, 3f);
            sc.minScale = Slider("  Min Scale (limit)", sc.minScale, 0f, 1f);
            sc.maxScale = Slider("  Max Scale (limit)", sc.maxScale, 1f, 5f);
            GUILayout.Label($"  Now: <b>x{sc.Evaluate(motor.Velocity):0.00}</b> at speed {sc.Speed(motor.Velocity):0.0}", _label);
            return sc;
        }

        T EnumButton<T>(string label, T value) where T : Enum
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(115));
            if (GUILayout.Button(value.ToString())) { value = Next(value); GUI.changed = true; }
            GUILayout.EndHorizontal();
            return value;
        }

        float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: <b>{value:0.00}</b>", _label);
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
