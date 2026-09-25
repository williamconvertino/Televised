# Movement Prototype — Dev Notes

Things worth knowing about how the prototype is built, what hasn't been verified yet, known limits,
and a log of changes. Controls and the script map are in [`README.md`](README.md). The original design
brief is [`MOVEMENT_PROTOTYPE_README.md`](MOVEMENT_PROTOTYPE_README.md). The shared scripts and assets live in
`Assets/Prototyping/Shared/`.

---

## Verification status

- **Compiled outside the Editor.** All scripts compile against the project's Unity 6000.3 and Input
  System assemblies with Unity's bundled Roslyn compiler. There are no errors. The only warnings are for
  serialized fields that are never assigned in code, which Unity suppresses anyway.
- **Not yet run in the Editor.** The Editor was open but unfocused while the code was written, so it hadn't
  imported the new files. The scene, prefabs and generated assets haven't been created or play-tested yet.
  - **First time you focus Unity:** the builder runs automatically and generates everything.
  - **If it doesn't:** use **Prototyping ▸ Movement ▸ Rebuild Sandbox (Assets + Scene)**.
  - **If something errors:** check the Console first. The auto-build tries only once per Editor session.

## How generation works

- **Built by editor scripts, not hand-written YAML.** `Shared/Scripts/Editor/PrototypeAssetBuilder.cs` generates
  these under `Shared/`, and `Movement/Scripts/Editor/MovementSandboxBuilder.cs` builds the scene:
  - `Sprites/Circle.png`
  - `Materials/PrototypeUnlit.mat` (URP 2D Sprite-Unlit)
  - `Settings/DefaultMovementTuning.asset`
  - the player prefab and three surface prefabs (Rect, Ellipse, OrganicBlob)
  - `Scenes/MovementSandbox.unity`
- **When it runs.** It runs automatically once, if the sandbox scene doesn't exist yet, and it may ask
  you to save the scene you have open first. Run it again from the menu whenever you like.
  - **Rebuilding overwrites** the prefabs and the scene.
  - **Your tuning is kept.** The tuning asset is never overwritten.
- **Shapes regenerate themselves.** Each shape component rebuilds its Surface2D path, PolygonCollider2D
  and mesh whenever it's enabled and whenever you change a parameter in the Inspector. The meshes are
  never saved to disk.
  - Side effect: Unity may mark the sandbox scene as modified just from opening it.

## Tuning

- **Where tuning lives.** All gameplay tuning lives in `Shared/Settings/DefaultMovementTuning.asset`.
- **Play Mode changes are thrown away by default.** At the start of Play Mode the player makes a runtime
  copy of the asset, and everything edits that copy: the on-screen panel, the F1–F4 hotkeys, and the
  Inspector on the copy. The asset on disk isn't touched, so you can experiment freely.
- **Controls in the panel's "Tuning Persistence" section:**
  - **Persist changes to asset** (off by default, and back off at the start of each Play session). When
    you turn it on, the current values are written into the asset, and from then on edits go straight
    into it. When you turn it off, you get a fresh runtime copy of the asset's values.
  - **Save to asset** writes the runtime copy's current values into the asset once.
  - **Revert to asset** throws away runtime edits and reloads the asset's values.
  - **Inspect runtime copy** (Editor only) selects the copy, so you can edit it in the Inspector.
- **Inspector gotcha.** While not persisting, editing the `DefaultMovementTuning` **asset** in the Inspector
  does nothing to the running game until you click Revert to asset. Edit the runtime copy instead.
  - To always persist, as the prototype originally did, tick `persistTuningChanges` on the player's
    `PlayerMotor2D`.
- **Presets.** Duplicate the asset and assign it to the player's `PlayerMotor2D` to try a different
  configuration.
- **Cosmetic settings live elsewhere.** Leg settings are on the `ProceduralLegRig` component, and eye
  settings are on `EyeAimController` and `EyeVisualFeedback`.

## Movement input (WASD)

- **Keys.** A/D and W/S, or the arrow keys, all crawl along the surface. **Space is the only jump key.**
- **Intent vector.** The held keys make a screen-space direction: A/D = left/right, W/S = up/down,
  diagonals combine. Opposite keys cancel, so A+D is no horizontal input and W+S is no vertical input.
- **Screen-relative modes.** The player crawls in the path direction whose tangent best matches that
  direction. Examples:
  - W on the side of a blob or wall climbs up it.
  - S on a wall crawls down it.
  - D on a floor moves right.
- **Ties.** When the input is perpendicular to the surface (such as D on a wall), the tie-break reads the
  input as if the surface normal were "up" (`screenAmbiguityBias`). So D on the inside of the right wall
  climbs.
- **Input that points into or away from the surface.** Input more perpendicular to the surface than
  `minInputAlignment` does nothing. For example, W on a flat floor or S on a ceiling has no effect;
  Space is the jump.
- **ScreenRelativeLocked:**
  - The direction is chosen when movement starts and stays locked while any movement key is held.
  - **Pressing a key re-evaluates** the direction.
  - **Releasing one of several held keys doesn't.** So letting go of W while still holding D won't
    suddenly reverse you on a curve.
  - **Releasing everything clears the lock.**
  - An ambiguous new press, such as adding W while on a flat floor, keeps the current lock.
- **A/D on curves.** If A/D is ambiguous right where you are (the eye is sideways, so the bias and the
  screen direction cancel) but the surface curves there, the resolver looks about a body width along the
  path both ways and goes whichever way heads toward the key. Straight surfaces keep the old behavior
  (no movement).
- **Surface angle limit** (`maxSurfaceAngle`, default 180 = anywhere). The steepest surface the player can
  stick to, measured between the surface normal and world up: 90 allows floors and walls but not
  ceilings. `steepSurfaceBehavior` picks what crawling into a steeper part does: `Stop` (halt at the
  limit) or `FallOff` (let go). Too-steep surfaces can't be landed on or transferred onto either, and if
  a rotating platform tips you past the limit you fall off.
- **SurfaceRelative:** only A/D count (D = clockwise by default, `invertSurfaceRelative` flips it).
  W/S do nothing in this mode.
- **In the air:** air control is horizontal only (A/D); W/S have no effect while airborne.

## Speed-scaled levers

Both levers use the same `SpeedScale` setting:
- **Speed source:** `Horizontal` (|vx|), `Vertical` (|vy|), `Total` (|v|), or `Falling` (downward speed
  only, 0 while rising).
- **The formula:** the multiplier is **1 at speed 0** and `scaleAtReference` at `referenceSpeed`. It keeps
  changing at the same rate past the reference speed, and is always clamped to [`minScale`, `maxScale`].
- **Off by default:** `scaleAtReference = 1` means no effect, which is the default for both levers.
- **Live readout:** the panel shows each multiplier's current value, and the status box shows both.

**1. Attach / magnet range scaled by speed** (`attachRangeSpeedScale`, in Attachment)
- **What it scales:** `attachDistance` (`speedScaleAttachDistance`) and/or `magnetRange`
  (`speedScaleMagnetRange`). Strict mode's contact tolerance is never scaled.
- **Defaults:** source `Total`, reference 10 u/s, scale 1.0, limits 0.25–2.0.
- **Making fast fly-bys harder to catch:** set `scaleAtReference` below 1. For example, 0.5 halves the
  attach radius at 10 u/s and bottoms out at `minScale`.
- **Still attaches on contact.** Actually touching a surface still attaches if `alwaysAttachOnContact` is
  on, whatever the scale. Turn that off for fast fly-bys to truly pass by.
- **Debug circles:** the attach and magnet circles show the scaled size in the air. A faint second circle
  shows the base size whenever they differ.
- **Sensor range:** the sensor always searches out to the largest possible scaled range, so a larger
  `maxScale` never misses candidates.

**2. Air horizontal speed scaled by fall speed** (`airMoveSpeed` + `airControlSpeedScale`, in Airborne)
- **`airMoveSpeed`** (default 5) is the top horizontal speed you can steer to in the air. It used to be
  tied to `surfaceMoveSpeed`.
- **The scale** multiplies both `airMoveSpeed` and the air acceleration
  (`airControl × airAcceleration`), so it changes how far and how quickly you can steer.
- **Defaults:** source `Falling`, reference 15 u/s, scale 1.0, limits 0.1–3.0.
  - Scale above 1: easier to steer on long falls.
  - Scale below 1: long falls commit you.
- **Steering never brakes you.** If you're already moving faster than the air speed limit in the
  direction you're holding (for example, after a fast sideways jump), holding that direction won't slow
  you down. Pressing the opposite direction still decelerates you normally.

## Air jumps (double jump)

Off by default. Toggle with F8 or in the panel (`enableAirJumps`).

- **Count:** `airJumpCount` jumps (default 1). They come back when you land on a surface, and also when
  the grapple latches (`grappleLatchRefreshesAirJumps`).
- **Height is consistent.** An air jump *replaces* vertical velocity instead of adding to it, so it goes
  just as high whether you were rising or falling fast.
- **Momentum:** `airJumpMomentumKeep` (default 0.5) of your horizontal velocity is added on top.
- **Direction** (`airJumpDirectionMode`):
  - `Up`
  - `MoveInput`: WASD, or up if no key is held.
  - `Cursor`: unrestricted, so you can dive.
  - `ClampedCursor` (default): the cursor, within `airJumpMaxAimAngle` (default 45°) of up.

  `MoveInput` is clamped to the same angle. A cyan arrow previews the direction while you're airborne
  with jumps left.
- **No accidental double-spend:** air jumps are ignored for `airJumpMinDelay` (0.06 s) after leaving a
  surface, so a fast double-tap on takeoff doesn't use both.
- **Landing grace:** if you're about to land, the press waits and becomes a normal ground jump when you
  touch down, instead of spending your air jump. "About to land" means within `airJumpLandingGrace`
  (0.3) of a surface you're moving toward. This uses the existing jump buffer.
- **Grapple interaction:** Jump while the grapple is latched releases it, then air-jumps if you have one
  left. Otherwise the press is just spent letting go.
- **Feedback:** the legs kick out opposite the launch direction. The eye stretches with the new velocity.

## Grapple leg

Off by default. Toggle with F9 or in the panel (`enableGrapple`).

- **Firing (right mouse by default; swappable in the panel):**
  - **Targeting is decided on click.** A ray is cast toward the cursor. If that misses, extra rays at
    ±½ and ±1 × `grappleAimAssistAngle` (default 6°) are tried.
  - **What counts as a hit:** the first attachable surface within `grappleMaxDistance` (default 7). The
    surface you're standing on doesn't count.
  - **The leg then travels out** at `grappleShootSpeed` and latches when it arrives. While it travels, if
    the leg (drawn from the eye to the tip) runs into an attachable surface, because you moved or a
    platform did, it latches there instead of passing through. The debug overlay
    shows a faint aim line to max range, and an orange circle where a click would latch.
- **Missing:** the leg reaches max range, then retracts at `grappleRetractSpeed`. With `FallThenRetract`
  (the default), it first droops under gravity for `grappleMissFallTime`. The retracting tip never
  trails farther than `grappleMaxDistance` from the eye.
- **Bonk:** if the aim ray's first hit is your own surface or a non-attachable one, the leg stops there
  and retracts.
- **Pull mode (default):**
  - Latching pulls you off your current surface, if you're on one, and reels you toward the anchor.
    Speed: `grapplePullSpeed`, acceleration: `grapplePullAcceleration`.
  - No gravity while pulling unless `grapplePullUsesGravity` is on.
  - You attach through the normal attachment system, so every attachment mode, the speed-scaled range and
    the cooldown still apply. If another surface is in the way, you attach to that one instead.
  - **Chaining:** clicking again while latched fires a fresh grapple.
  - **Safety release:** the grapple lets go after `grappleMaxPullTime`.
- **Swing mode:**
  - Latching makes a rope as long as your current distance to the anchor. The rope is a constraint:
    gravity and A/D air control swing you.
  - W/S reel the rope in and out (`grappleReelSpeed`). Reeling in goes all the way to contact, so you
    attach to the anchor surface.
  - **You have to hold the button.** Releasing it lets go with your momentum, and you must still be holding
    when the leg arrives, or it releases straight away.
  - Touching any surface attaches you as usual.
- **SwingPull mode (hybrid):** a rope with gravity on.
  - **Hold the grapple button to reel in.** The rope shortens at `grappleSwingPullReelSpeed` (default 9), down to contact
    distance. You swing slightly on the way (`grappleSwingPullSwingAmount`, default 0.35) and attach
    normally when you touch the surface.
  - **Release the button to swing freely** on the current rope length. `grappleSwingAmount` applies here
    (default 1, a free pendulum). W/S lengthen or shorten the rope at `grappleReelSpeed`, and A/D air
    control adds to the swing.
  - **Hold it again to resume reeling.** Clicking doesn't re-fire in this mode.
  - **Space lets go**, then air-jumps if you have one left. The other mouse button cancels.
  - **No time limit:** the max pull time doesn't apply, so you can hang as long as you like.
  - The status box shows "(reeling)" or "(swinging)".
- **Swing controls:**
  - **Swing Amount** (`grappleSwingAmount`, 0–1, default 1) scales swinging motion in both modes.
    Motion around the anchor is damped at a rate of `(1 − swingAmount) × grappleSwingDamping` per second
    (default damping 10). So 1 is a free pendulum, 0.5 is a gently damped swing, and 0 kills the swing
    in about a tenth of a second.
  - **Allow swing while pulling** (`grappleAllowSwingWhilePulling`, Pull mode, default off). When on, the
    pull only controls your speed toward the anchor. Sideways momentum is kept, and gravity bends the path
    into an arc; the Swing Amount scales both. When off, the pull flies straight at the anchor, as before.
  - **Defaults leave both modes unchanged:** Swing Amount 1, and swinging while pulling off.
  - **Orbiting:** with swinging while pulling on and a high Swing Amount, a fast sideways approach can
    spiral around the anchor before it lands. The max pull time still releases you.
- **Ending a grapple:** the other mouse button cancels, Jump releases, and landing anywhere releases. Disabling the
  grapple mid-use retracts the leg.
- **Cooldown:** `grappleCooldown` applies after the leg is fully retracted.
- **Visuals:** the front leg (the last one in the rig) becomes the grapple leg while the grapple is out.
  It's drawn straight from its socket to the tip.
- **The other legs are hidden while grappling.** This covers the shooting and latched states, not
  retracting, so only the grapple leg is visible.
  - While hidden they still keep a pose underneath: planted feet stay put on a surface, and in the air
    they sit tucked under the body. That way they reappear cleanly as soon as the grapple leg starts
    retracting.
  - To turn this off, untick "Hide other legs while grappling" in the panel's Legs section, or
    `hideOtherLegsWhileGrappling` on `ProceduralLegRig`. It's on by default.
- **Panel clicks are safe:** clicks on the status box or tuning panel don't fire the grapple.

## Design decisions

- **Attached movement is a hard constraint.** The root sits at `point + normal * radius` at a tracked
  path position, with no friction or gravity involved. After landing, and after crawling onto another
  surface, an offset blends away (`surfaceSnapStrength`) so the snap isn't visible.
- **Airborne movement is custom, not Rigidbody physics.** It integrates in small steps: each step moves
  at most a quarter of the radius, then pushes the player out of any surface it's overlapping. The
  Rigidbody2D (kinematic) and CircleCollider2D are only there for future triggers.
- **No snapping between collider edges.** Normals blend toward the averaged corner normal within
  `normalBlendDistance` of each polygon corner.
- **Smoothed jump normal.** `SmoothedSurfaceNormal` jumps use the "chord" between two virtual contact
  points at ±`normalSampleSpacing` along the path, then smooth it over time (`normalSmoothing`). The same
  normal is the eye's and legs' visual "up".
- **Attachment checks.** Attaching needs:
  - the surface to be within range,
  - its reattach cooldown to be over,
  - the player to be moving toward it (`minimumApproachSpeed`, `maxApproachAngle`).

  Touching a surface always allows attaching (`alwaysAttachOnContact`).
- **Target selection is sticky.** The current target, and the magnet target in Magnetic mode, only
  changes when another candidate beats it by `targetSwitchMargin`. This stops rapid flipping between
  nearby surfaces.
- **Targeted jumps are ballistic.** `NearestSurface` and `AssistedTarget` solve a real launch arc at the
  current jump speed. When no reachable target exists, the jump falls back to the smoothed normal and can
  miss; this is intended. `preferHighArc` switches to the high arc.
- **Crawling onto touching surfaces.** When crawling into another surface (floor → wall, pillar → floor,
  overhang → wall), the player moves onto it and keeps going away from the surface it came from. A locked
  direction carries over. Turn this off with `allowSurfaceTransfer`.
- **Curvature compensation.** With `compensateCurvature` on, the player's center moves at
  `surfaceMoveSpeed` on tight curves. Without it, the contact point moves at that speed, so the center
  speeds up on convex curves.
- **Unrestricted cursor jumps.** `CursorDirection` uses `cursorIntoSurface` when the cursor is behind the
  current surface. The default, `ClampAboveSurface`, keeps the jump at least `cursorMinSurfaceAngle` above
  the surface plane. `ClampedCursorDirection` is inherently prevented from jumping into the surface by its
  clamp.
- **Game-view debug lines.** The lines are drawn as one line mesh (`DebugLines`), so they appear in the
  Game view without Gizmos. The on-screen panel uses IMGUI to stay quick and unstyled.

## Sandbox layout

The arena is a floor, a ceiling and two walls, about 64 × 26 units. Inside it:

| # | Area | Notes |
|---|---|---|
| 1 | Floor, walls, ceiling | These overlap at the corners, which tests crawling from one surface onto the next |
| 2 | Circle | Radius 2 |
| 3 | Small blob, large irregular blob | Seeded organic shapes |
| 4 | Tall narrow pillar | Planted into the floor |
| 5 | Close pair | 1.1 gap, easy to jump between |
| 6 | Out-of-range pair | 1.7 gap, just beyond the player's diameter plus the attach distance |
| 7 | Cluster | Five small blobs, for candidate selection |
| 8 | Overhang | Sticks out from the left wall |
| 9 | Narrow gap | 1.3-wide gap between two pillars; the player's diameter is 1.0 |
| 10 | Compass | Four blobs above, below, left and right of a central point |
| 11 | Extras | A stretched, platform-like blob and a concave-ish blob |

Number keys 1–9 jump to the spawn points, in this order: Floor, CompassCenter, LargeBlob, Cluster,
UnderOverhang, ClosePair, TallNarrow, StretchedBlob, ConcaveBlob.

## Sandbox 2 layout (moving objects)

`Scenes/MovementSandbox2.unity`, built by `MovementSandbox2Builder`. Same arena idea, about 82 × 26 units.
Static surfaces are blue, moving ones orange. All motion comes from `PlaytestMover`.

| # | Area | Static | Moving |
|---|---|---|---|
| 1 | Elevator | Pedestal, tall ledge | Platform rising 6.2 beside the ledge |
| 2 | Spinners | Stepping circle, ceiling stalactite | Spinning ellipse, spinning blob |
| 3 | Ferry | Overhead circle, floor lump | Platform shuttling from the ledge to the windmill tower |
| 4 | Windmill | Tower, hub, pillar, landing lump | Bar rotating around the hub (crawl between bar and hub) |
| 5 | Orbit | Core circle | Three stones orbiting it (rotating parent pivot) |
| 6 | Pendulum | Launch circle, right-wall ledge | Arm + platform swinging ±32° from the ceiling |
| 7 | Bobbing stones | Stones 1, 3, 5 | Stones 2, 4 bob up and down, out of phase |
| 8 | Sliding slab | Step-up circle, stretched blob, far circle | Slab sliding under the ceiling |

Number keys 1–9: Start, Elevator, FerryLedge, WindmillTower, OrbitCore, PendulumLaunch, BobbingStones,
Spinners, SlidingSlab.

**Nothing can crush you.** The motor doesn't resolve a moving surface pushing into an attached player, so
every mover is placed to keep at least a player's width of clearance from anything you could stand on.
Keep that in mind when adding movers.

## Known limits

- **Sharp inward corners.** A sharp inward (concave) corner inside a single shape can make the player
  overlap its own surface. Rounded and organic shapes behave fine; touching separate surfaces are handled
  by crawling from one onto the other.
- **Closed shapes only.** Every surface is a closed polygon; there are no open edges or one-way platforms.
- **Cost.** Surface queries check every polygon edge (after a bounding-box check). This is fine at
  sandbox scale; big levels would need spatial partitioning.
- **Anticipation reach.** Legs reach toward the nearest surface the player is moving toward, within
  `anticipateDistance`. Candidates are only found within the sensor range (the attach distance or magnet
  range), so in practice the legs start reaching shortly before contact.
- **Legs only exist in Play Mode.** The leg rig builds its legs at runtime, so they don't show in the
  prefab or in Edit Mode.

## Change log

### 2026-09-25: Sandbox 2 (moving objects)
- New `Scenes/MovementSandbox2.unity`, built by `MovementSandbox2Builder` (**Prototyping ▸ Movement ▸ Rebuild /
  Open Sandbox 2 (Moving Objects)**). It builds itself once, after the first sandbox exists. See "Sandbox 2 layout".
- **Grapple on moving surfaces:** the anchor is stored in the surface's local space, so a latched grapple
  moves and rotates with its surface. A leg in flight also steers toward where its target point is now.
- `PlaytestMover.swingAngle`: pendulum swing (±degrees once per period). Default 0, so existing courses are unchanged.
- `MovementSandboxBuilder`'s `Rect` / `Ellipse` / `Blob` helpers are now `internal`, return the created object,
  and `Blob` takes optional colors.

### 2026-09-25: Movement fixes, surface angle limit, grapple button
- A/D keep working on curves when the eye is sideways (curve lookahead in `SurfaceInputResolver`).
- `landingJumpDelay` (default 0.1 s): a jump pressed right on landing waits until the landing settles,
  then fires, instead of an instant skip.
- Legs on moving/rotating platforms: planted feet and steps in progress move with the surface.
- Swing mode: W reels in all the way to contact. `grappleMinRopeLength` was removed.
- Missed grapple: the drooping/retracting tip stays within `grappleMaxDistance` of the eye.
- Shooting grapple latches onto surfaces that come into the leg's path, instead of clipping through.
- Grapple button: right mouse by default, swappable in the panel (`PrototypeInput.GrappleButton`,
  saved in PlayerPrefs). Playtest hints show the current binding.
- `maxSurfaceAngle` / `steepSurfaceBehavior`: limit which surfaces can be crawled on (default: all).

### 2026-09-24: Playtest: new Hard course, merged Medium, catalog order, hotkeys off in forms
- **Hotkeys are off in forms.** `PrototypeInput.KeyboardBlocked` makes every keyboard read (and the
  scroll wheel) return nothing. The playtest session sets it on the setup, rating, finished and results
  screens, so typing a comment can't trigger C (overview camera), R, N and so on.
- **Medium course:** now the former Medium and Hard merged, each section type once, at the harder
  settings.
- **Hard course:** new, with dedicated skill sections and **moving surfaces**:
  - long precise jumps, tiny spiked targets, a tight chimney, a ceiling marathon and a deep drop shaft
  - moving or rotating pieces: a ferry, pistons, a windmill, bobbing stones and an elevator
  - `PlaytestMover` moves or rotates any surface on a time-based ping-pong path.
- **Platform velocity inheritance:** `PlayerMotor2D` now tracks the velocity of the surface it's attached
  to (`PlatformVelocity`) and adds it to jumps, scaled by the new `inheritPlatformVelocity` (default 1).
  An attached player was already carried along automatically, because the motor resamples the surface
  every frame.
- **Config order:** configs are now played in catalog order (C01 Easy → Medium → Hard → C02 ...). The
  setup screen has two options, both off by default: **Shuffle config order** and **Pick the least-rated
  configs** (for subsets). The old Balanced / Random / Sequential selector is gone.
- The scene version marker is now `PlaytestLevel_v3`, so the playtest scene rebuilds itself on the next
  compile.

### 2026-09-24: Playtest warm-up removed
- Removed the optional unrated warm-up run: it didn't add anything. Sessions start directly with the
  first config's first level.

### 2026-09-24: Playtest: one config at a time, per-level ratings, skip options
- Each config now plays every ticked difficulty in order (Easy → Medium → Hard), with a rating after
  each level. The setup screen no longer has the "one difficulty per config" option.
- New skip options:
  - **Skip this level (no rating):** on the intro screen and in the HUD.
  - **Skip the rest of this config:** on the intro screen.
  - **Skip rating:** on the rating screen.
  - **Stop this config here:** a checkbox on the rating screen.
- The session JSON gains `unratedRuns` (played but not rated, with metrics) and `skippedLevels`. The CSV
  still holds only rated levels.

### 2026-09-24: Playtest fixes
- **Out-of-date scenes rebuild themselves.** The playtest scene now contains a version marker object
  (`PlaytestLevel_v2`). When scripts compile, the auto-builder rebuilds the scene once if it's missing
  or doesn't contain the current marker. Bump `PlaytestLevelBuilder.VersionMarker` whenever course
  layouts change.
- **CSV fix:** `ratings.csv` is now written without a byte-order mark. The BOM had become part of the
  first column's name, so `sessionId` wasn't read back, and the header migration re-ran on every save.
  Reading tolerates an existing BOM.
- Deleted the outdated results from the one-course version (a single test run).

### 2026-09-24: Playtest difficulties, feature courses, module library, skip control
- The playtest scene now holds **nine courses**: General, Double jump and Grapple, each at Easy,
  Medium and Hard.
  - They're built from a module library in `PlaytestLevelBuilder.cs`. Each module forces one kind of
    movement: wrap under a circle, roof-crawl over tunnel pits, switch walls around hazard rungs,
    underside chains, floating columns, spiked sparse blobs, and so on.
  - The Easy General course was redesigned around these modules, for more forced variety.
  - The Double-jump and Grapple courses can't be finished without their feature.
- Setup screen additions:
  - difficulty toggles
  - a choice between one difficulty per config (least-rated first) and every selected difficulty per
    config
  - a feature-course toggle
- **N** or the HUD button skips to the next checkpoint, or to the end. Skips are recorded, and skipping
  to the end counts as not finished.
- The CSV gains `difficulty`, `course` and `skips` columns, and old CSVs are migrated automatically. Δ is
  now relative to each tester's average on the same course and difficulty.
- The shared baseline's grapple range went from 8 to 9, which the grapple chains need.
- **The existing PlaytestLevel scene needs rebuilding:** Prototyping ▸ Movement ▸ Build Playtest Level.

### 2026-09-24: Playtest level, configurations and ratings
- New `Scenes/PlaytestLevel.unity`, built by **Prototyping ▸ Movement ▸ Build Playtest Level**. It builds
  itself automatically once, if it's missing. The course has 6 sections, 5 checkpoints, hazards and a goal.
- `Scripts/Playtest/`:
  - `PlaytestConfigCatalog`: 28 configs on a shared comfortable baseline.
  - `PlaytestSession`: setup, intro, run, rating and results screens.
  - `PlaytestResultsStore`: CSV, JSON and summary.md, with ratings adjusted for each tester's average.
  - `PlaytestCheckpoint`, `PlaytestHazard` and `PlaytestSign`.
- Full design rationale, config table, facilitator checklist and analysis notes are in
  `Playtest/PLAYTEST_GUIDE.md`.
- Supporting changes:
  - `PlayerMotor2D.Respawned` event and `LoadRuntimeTuning()`.
  - `MovementDebugRenderer.hintsOnly`: player-facing grapple and air-jump hints only.
  - Legs no longer reach toward non-attachable surfaces.
  - The shared builder helpers are now `internal`.

### 2026-09-24: SwingPull is hold-to-reel, other legs hidden while grappling
- SwingPull: hold LMB to reel in (with a slight swing); release to swing freely on the rope. While
  swinging freely, W/S adjust the rope length and Space lets go. SwingPull no longer times out or re-fires
  on click.
- While grappling, the non-grapple legs are now **hidden** rather than frozen. The setting was renamed to
  `hideOtherLegsWhileGrappling` and is still on by default.

### 2026-09-24: SwingPull grapple mode, other legs freeze while grappling
- New `GrappleMode.SwingPull`: an auto-reeling rope with gravity on, so you swing slightly on the way in.
  - Settings: `grappleSwingPullReelSpeed`, `grappleSwingPullSwingAmount`.
  - Shares the max pull time, re-fire and damping with the other modes.
- `ProceduralLegRig.freezeOtherLegsWhileGrappling` (on by default): the non-grapple legs stop animating
  while the grapple is shooting or latched. Planted feet stay put; in the air they hold a still, tucked
  pose. There's a toggle in the panel's Legs section.

### 2026-09-24: Grapple swing controls
- `grappleAllowSwingWhilePulling` (a toggle, default off): lets Pull mode keep sideways swing and
  gravity-curved arcs.
- `grappleSwingAmount` (0–1, default 1) scales how much swinging the grapple allows in both modes.
  `grappleSwingDamping` (default 10) sets how fast swing is damped at amount 0.
- Both are in the panel's Grapple section. The defaults leave existing behavior unchanged.

### 2026-09-24: Air jumps and grapple leg (both off by default)
- Air jumps:
  - count, speed, and four direction modes
  - replaces vertical velocity, keeps some horizontal momentum
  - takeoff delay, landing grace, leg kick
  - refreshed on landing or grapple latch
  - F8 toggles
- Grapple leg:
  - new `GrappleController` and `Surface2D.Raycast`
  - Pull and Swing modes
  - aim assist, miss droop, bonk, chaining, cooldown
  - the front leg is drawn as the grapple
  - F9 toggles, left mouse fires, right mouse cancels
- Jump while latched releases the grapple, then air-jumps if one is available.
- Clicks over the debug UI no longer reach gameplay (`PrototypeInput.PointerBlocked`).
- Debug: cyan air-jump preview arrow; grapple aim line, predicted latch point, anchor, and swing rope circle.
- The player prefab builder now adds `GrappleController`. Existing prefabs also work, because the motor
  adds it at runtime if it's missing.

### 2026-09-24: Speed-scaled attach range, air speed lever, tuning persistence toggle
- New `SpeedScale` setting type: a clamped, linear multiplier driven by speed, with a selectable velocity
  component.
- `attachRangeSpeedScale`, `speedScaleAttachDistance` and `speedScaleMagnetRange` scale the attach distance
  and magnet range by speed. Off by default (scale 1.0). The debug circles show the scaled size.
- New `airMoveSpeed` (air steering is no longer tied to `surfaceMoveSpeed`) and `airControlSpeedScale`,
  which scales air speed and acceleration by speed (falling speed by default). Air steering no longer
  brakes momentum in the direction you're holding.
- Tuning persistence:
  - Play Mode edits now go to a runtime copy by default.
  - The panel has a "Persist changes to asset" toggle (off by default) and Save / Revert / Inspect buttons.
  - `PlayerMotor2D.persistTuningChanges` sets the starting state.
- The panel has new sliders for all of the above, plus air acceleration. The status box shows the live
  multipliers and the persistence mode.

### 2026-09-24: WASD crawling, Space-only jump
- W/S (and ↑/↓) no longer jump. They crawl up or down along the surface, like A/D.
- Space is the only jump key.
- `SurfaceInputResolver` now takes a screen-space intent vector instead of separate left/right keys. It
  has a single lock that is re-evaluated on each new key press.
- New tuning value: `minInputAlignment` (default 0.1). The panel now has sliders for it and for
  `screenAmbiguityBias`.
- Air control uses only the horizontal input.
- The on-screen status shows the current input vector and lock.

### 2026-09-24: Initial prototype
- Everything in the design brief's Phases 1–7:
  - surface representation, attached crawling, jumping, attachment variants
  - procedural legs, organic generator, testing UI
- Debug overlay, sandbox scene and editor builder.
