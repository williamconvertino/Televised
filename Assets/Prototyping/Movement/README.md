# Movement Prototype — Usage

This folder implements the prototype described in `Assets/Prototyping/MOVEMENT_PROTOTYPE_README.md`.

## Getting started

1. Let Unity compile. The first time it does, it generates the sprite, material, tuning asset, prefabs and
   `Scenes/MovementSandbox.unity` automatically. It may first ask you to save the scene you have open.
2. If that doesn't happen, or you want a fresh copy, use **Prototyping ▸ Movement ▸ Rebuild Sandbox (Assets + Scene)**.
3. Open `Scenes/MovementSandbox.unity` and press Play.

## Playtesting

A playtest level with 28 movement configurations, ratings and a results summary is described in
[`Playtest/PLAYTEST_GUIDE.md`](Playtest/PLAYTEST_GUIDE.md). Open it with **Prototyping ▸ Movement ▸ Open Playtest Level**.

## Controls

| Input | Action |
|---|---|
| W A S D (or arrow keys) | Crawl along the surface toward that screen direction (see `DEV_NOTES.md`) |
| Space | Jump (in the air: double jump if enabled, and it releases a latched grapple) |
| Left mouse | Fire the grapple leg (if enabled). Swing: hold to stay latched. SwingPull: hold to reel in, release to swing freely. W/S adjust the rope while swinging |
| Right mouse | Cancel the grapple |
| Mouse | Eye aim (and the cursor-based jump modes) |
| F1 / F2 / F3 / F4 | Cycle movement / jump / attachment / candidate-selection mode |
| F5 | Toggle debug lines |
| Tab / F6 | Toggle the tuning panel |
| F7 | Switch between 2 and 4 legs |
| F8 / F9 | Toggle air jumps / grapple (both off by default) |
| R, 1–9, T | Respawn, jump to a spawn point, teleport to the cursor |
| C, scroll wheel | Overview camera, zoom |

All tuning lives in `Settings/DefaultMovementTuning.asset`. By default, changes you make in Play Mode edit a
runtime copy and are **discarded** when you stop playing. To keep them, tick **Persist changes to asset** or
click **Save to asset** in the panel. See `DEV_NOTES.md` for details.

## Debug line colors

| Color | Meaning |
|---|---|
| yellow cross | current surface point |
| green arrow | local (per-vertex smoothed) normal |
| lime arrow from center | virtual-foot smoothed normal (the visual "up") |
| red line | tangent (arrowhead = +path / counter-clockwise) |
| orange arrow | current traversal direction |
| magenta crosses | virtual foot samples used for the smoothed normal |
| cyan arrow + faint arc | jump direction preview and predicted trajectory; circle = targeted-jump landing |
| blue arrow | velocity |
| cyan circle | attach radius (purple = magnet range) |
| candidate lines | green = eligible, grey = in range but rejected, red = blocked by cooldown |
| yellow outline | selected attachment target |
| red outline | the surface you just left, while its reattach cooldown runs |
| white probe | closest sample to the mouse on any surface |
| light-cyan arrow (airborne) | air-jump direction preview, when air jumps are left |
| orange line / circle / cross | grapple: aim line to max range, predicted latch point, anchor (and swing rope circle) |

## Script map

| Script | Responsibility |
|---|---|
| `Surfaces/Surface2D` | Closed polygon path. Closest sample, sample at path position, chord ("two feet") frame, smoothed normals |
| `Surfaces/SurfaceSensor` | Finds candidate surfaces near the player |
| `Surfaces/SurfaceAttachmentController` | Scores candidates, cooldown, hysteresis, Strict / Nearest / Magnetic |
| `Player/PlayerMotor2D` | Attached/Airborne state, surface-constrained position, crawl, jump, gravity, collision push-out |
| `Player/SurfaceInputResolver` | Turns WASD into a traversal sign (Locked / Continuous / SurfaceRelative) |
| `Player/JumpDirectionResolver` | All six jump-direction modes, plus the air-jump direction |
| `Player/GrappleController` | Grapple leg: targeting, shoot / latch / retract, pull and swing physics hooks |
| `Player/EyeAimController` | Aims the iris and pupil at the mouse. Visual only |
| `Player/EyeVisualFeedback` | Landing squash, stretch while flying, idle breathing. Cosmetic |
| `Legs/ProceduralLegRig` | Cosmetic 2-bone-IK legs: planting, alternating steps, attach reach, airborne tuck, anticipation |
| `Generation/*SurfaceShape`, `OrganicSurfaceGenerator` | Rounded rectangles, ellipses and seeded blobs. Each shape generates its Surface2D, collider and mesh |
| `Debug/*` | Game-view debug lines, tuning panel, camera, spawn points |
| `Editor/MovementSandboxBuilder` | Generates the assets, prefabs and scene. Also adds GameObject ▸ Prototyping ▸ Movement create menus |

## Notes

Design decisions, known limits and the change log are in [`DEV_NOTES.md`](DEV_NOTES.md).
