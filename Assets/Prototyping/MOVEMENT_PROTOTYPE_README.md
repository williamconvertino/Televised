# 2D Roguelike Movement Prototype

## Purpose

This prototype exists to answer one question before the rest of the game is built:

> Is it fun to control an eyeball that crawls along arbitrary 2D surfaces, jumps between objects, automatically attaches to nearby surfaces, and can aim independently with the mouse?

The larger game concept is a 2D roguelike in which the player is an eyeball fighting through surreal television commercials. The final game may include combat, enemies, upgrades, procedural generation, and TV-themed level mechanics, but **none of those systems are in scope for this prototype**.

The goal here is to isolate and rapidly iterate on the movement mechanic.

The player should be able to:

1. Attach to the surface of an arbitrary 2D object.
2. Move smoothly around that surface using A/D.
3. Jump away from the surface.
4. Automatically attach to another nearby valid surface.
5. Transition smoothly between attached and airborne states.
6. Aim the visible eyeball/pupil independently toward the mouse cursor.
7. Visually walk/crawl using simple procedural legs.
8. Switch between multiple movement, jump-direction, and attachment behaviors during testing.

The prototype should favor **clarity, tunability, and rapid experimentation** over production-ready abstraction.

---

# Project Organization

All code, scenes, prefabs, materials, sprites, generated assets, debug utilities, and configuration files created for this prototype must live under:

```text
Assets/Prototyping/Movement/
```

Do not scatter prototype files across the rest of the Unity project.

Recommended structure:

```text
Assets/
└── Prototyping/
    └── Movement/
        ├── README.md
        ├── Scenes/
        │   ├── MovementSandbox.unity
        │   └── OptionalFocusedTests/
        ├── Scripts/
        │   ├── Player/
        │   ├── Surfaces/
        │   ├── Legs/
        │   ├── Debug/
        │   └── Generation/
        ├── Prefabs/
        │   ├── Player/
        │   └── Surfaces/
        ├── Materials/
        ├── Sprites/
        └── Settings/
```

If additional prototype-only packages, ScriptableObjects, or generated data are needed, they should also remain inside this folder whenever possible.

---

# Guiding Design Principles

## 1. Movement physics and visual animation must remain separate

The player's actual surface attachment and movement should **not** depend on the animated leg locations.

The movement system should calculate:

- true player/root position,
- current attached surface,
- closest surface point,
- surface normal,
- surface tangent,
- traversal direction,
- jump direction,
- airborne velocity,
- attachment target.

The leg system should read this information and animate itself visually.

This distinction is important. If a leg is temporarily lifted during a walking animation, that must not alter the player's collision, normal calculation, jump vector, or attachment state.

---

## 2. Attached movement should be controlled, not friction-based

Do not rely on ordinary Rigidbody2D friction/gravity to keep the eyeball attached to walls and ceilings.

When attached, the player should behave more like an entity constrained to a path/surface.

Conceptually:

```text
desiredPlayerCenter =
    surfacePoint + surfaceNormal * playerRadius
```

The controller can use Rigidbody2D movement if desired, but the attached state should explicitly constrain the player to the sampled surface rather than hoping normal collision response produces the intended result.

This should make curved traversal predictable and prevent unstable wall/ceiling physics.

---

## 3. Surface queries should be centralized

Avoid spreading unrelated raycasts and collider calculations throughout the player code.

Create a reusable surface representation/query API so all systems share the same concept of a surface sample.

A useful conceptual structure is:

```csharp
public struct SurfaceSample
{
    public Vector2 point;
    public Vector2 normal;
    public Vector2 tangent;

    // Optional but useful for spline/path-based surfaces.
    public float pathPosition;

    public Surface2D surface;
}
```

And an interface/component roughly equivalent to:

```csharp
SurfaceSample GetClosestSample(Vector2 worldPosition);
```

The exact implementation may differ, but the following should all consume the same surface information:

- player attachment,
- surface traversal,
- jump calculations,
- procedural feet,
- debug drawing,
- candidate scoring.

---

# Player Structure

A recommended player hierarchy:

```text
PlayerRoot
├── PhysicsCollider
├── EyeVisual
│   ├── Eyeball
│   └── Pupil
├── LegRig
│   ├── LeftLeg
│   └── RightLeg
└── SurfaceSensor
```

The root transform should represent the true player position.

The root/physics object should not rotate just because the pupil aims at the mouse.

The eye visual may rotate independently if useful, but ideally the player remains logically orientation-agnostic.

---

# Player States

For the MVP, use two primary gameplay states:

```text
Attached
Airborne
```

Optional visual/interpolation substates may be added:

```text
Attaching
Detaching
```

but these should not unnecessarily complicate the controller.

---

# Attached State

While attached:

- The player has a valid `Surface2D`.
- A closest/sampled surface point is tracked.
- The surface normal is known.
- The local tangent is known.
- The player center is maintained at approximately:

```text
surfacePoint + normal * playerRadius
```

- A/D input moves the player along the surface.
- Gravity should not pull the player off.
- The player's collision should remain stable while traveling across curves.
- The movement should work on:
  - floors,
  - walls,
  - ceilings,
  - circles,
  - organic blobs,
  - concave-ish test shapes where practical,
  - smooth transitions around corners.

The prototype should strongly prioritize stable motion over physically realistic dynamics.

---

# Airborne State

While airborne:

- The player behaves like a normal jumping/flying 2D body.
- Gravity should be configurable.
- Limited or zero air control should be configurable.
- Nearby valid surfaces should be detected.
- When attachment conditions are satisfied, the player should transition back into `Attached`.

The player should not immediately reattach to the exact surface it just jumped from.

Useful parameters:

```text
jumpSpeed
gravity
airControl
attachDistance
reattachCooldown
minimumApproachSpeed
attachmentMagnetism
```

---

# Eye / Mouse Aiming

The visible eyeball or pupil should independently look toward the mouse cursor.

Use the player's true root position:

```text
aimDirection =
    mouseWorldPosition - playerRootPosition
```

The eye/pupil visual should rotate or offset accordingly.

This aiming system is visual for now, but later it may be reused for:

- projectile firing,
- gaze attacks,
- cursor-directed jumping,
- target selection,
- interactions.

Keep it modular.

---

# Traversable Surfaces

## Surface2D

Every traversable object should have a dedicated surface component.

A surface should provide:

- closest point,
- outward normal,
- tangent,
- optional path/spline parameter,
- reference to its own collider/object.

The controller should preferably query the surface representation rather than infer everything independently from arbitrary colliders.

---

# Organic Surface Generator

Create a simple prototype utility for generating curved 2D objects.

The generator does not need to make aesthetically finished game assets. It only needs to rapidly create shapes useful for movement testing.

It should be able to generate:

- circles / ellipses,
- smooth blobs,
- stretched blobs,
- asymmetric organic shapes,
- larger platforms with curved edges.

A practical approach is:

1. Start from a circle or evenly distributed control points.
2. Randomize the radial distance of each control point.
3. Smooth/interpolate the result using a spline.
4. Sample that spline into a polygon or edge path.
5. Generate the collider from the sampled points.
6. Render a basic filled or outlined shape.

Expose useful settings such as:

```text
seed
pointCount
radius
radiusVariation
xScale
yScale
smoothing
sampleCount
```

The same shape should be reproducible from a seed.

---

# Smooth Geometry vs Collider Geometry

The visible/generated object may use a polygonal collider, but the movement system should avoid visibly snapping from collider edge to collider edge.

Prefer one of the following:

1. Query a smooth spline representation directly.
2. Smooth normals over neighboring collider samples.
3. Use interpolated neighboring path points.

The player's traversal and jump direction should feel smooth even when the collider is discretized.

---

# Surface Attachment

When airborne, the player should search for nearby candidate surfaces.

Keep detection separate from selection.

Conceptually:

```text
Detect candidates
    ↓
Evaluate candidates
    ↓
Choose best target
    ↓
Attach if valid
```

Do not hard-code only one attachment-selection strategy.

---

# Basic Attachment Conditions

A candidate should generally require:

```text
distance <= attachDistance
```

plus optional checks such as:

```text
player is moving toward surface
surface is not temporarily blocked after takeoff
surface is within desired approach angle
```

The controller must prevent this failure case:

```text
jump from floor
→ still within attachment radius
→ immediately snap back to same floor
```

Use a configurable cooldown or temporary ignore window for the surface that was just left.

Example:

```text
recentlyDetachedSurface
recentDetachTime
reattachCooldown
```

---

# Attachment Magnetism

The prototype should support tuning how strongly nearby surfaces capture the player.

Potential modes:

```text
Strict
Nearest
Magnetic
```

### Strict

Attachment only happens when actual geometry/collision conditions are tightly satisfied.

### Nearest

If within the attachment radius, snap to the closest valid surface.

### Magnetic

Nearby surfaces can gently pull or redirect the player before attachment.

The initial implementation can be simple, but keep the logic modular enough to compare these behaviors.

---

# Candidate Scoring

Provide a framework that can eventually score attachment candidates using factors such as:

```text
distance
velocity alignment
surface approach angle
mouse/cursor alignment
jump direction alignment
```

A conceptual weighted score:

```text
score =
    distanceWeight * distanceScore
  + velocityWeight * approachScore
  + cursorWeight   * cursorScore
```

For the first prototype, nearest valid surface can be the default.

The important part is making candidate selection replaceable.

---

# Jump System

The jump system should support multiple direction modes selectable from the Inspector and, ideally, switchable during Play Mode.

Use an enum or strategy architecture similar to:

```csharp
public enum JumpDirectionMode
{
    LocalSurfaceNormal,
    SmoothedSurfaceNormal,
    CursorDirection,
    ClampedCursorDirection,
    NearestSurface,
    AssistedTarget
}
```

Not every advanced mode must be perfect immediately, but all should have a clear implementation path.

---

# Jump Mode 1: Local Surface Normal

The player jumps directly along the outward normal at the current surface point.

```text
      ↑ jump
      O
──────●──────
      normal
```

This is the simplest baseline.

Advantages:

- predictable,
- highly readable,
- strongly tied to the current surface.

Potential problem:

- noisy/generated geometry may cause direction jitter.

---

# Jump Mode 2: Smoothed / Virtual-Foot Normal

Do not use the rendered feet.

Instead, compute two virtual contact points at small offsets along the current surface:

```text
leftSample  = sample(pathPosition - footSpacing)
rightSample = sample(pathPosition + footSpacing)
```

Then:

```text
tangent =
    normalize(rightSample.point - leftSample.point)

normal =
    perpendicular(tangent)
```

Choose the outward-facing normal.

This approximates the direction implied by the player's two feet without allowing leg animation to influence gameplay.

This may provide smoother jumps on irregular shapes.

Expose:

```text
normalSampleSpacing
normalSmoothing
```

---

# Jump Mode 3: Cursor Direction

Jump directly toward the mouse.

```text
direction =
    normalize(mouseWorld - playerPosition)
```

This provides full 360-degree control.

It should be tested because it may work particularly well when the player is walking on ceilings, walls, and arbitrary objects.

Potential downside:

- aiming + platforming may become too demanding,
- the player can intentionally jump directly into the object they are attached to unless prevented.

---

# Jump Mode 4: Clamped Cursor Direction

This is an important experimental mode.

Start from the current surface normal and allow the cursor to tilt the jump direction only within a configurable angular range.

Example:

```text
maxJumpAimAngle = 45° or 60°
```

Conceptually:

```text
              mouse
                 *
                /
               /
              O
              ↑ normal
──────────────●──────────────
```

The resulting jump remains fundamentally "away from the surface" but gives the player directional control.

Expose:

```text
maxJumpAimAngle
```

This is likely to be one of the most useful modes to test.

---

# Jump Mode 5: Nearest Surface

When the player jumps, find the nearest valid reachable surface and launch toward it.

This mode should still allow failure when no destination exists.

Possible future refinement:

- choose nearest surface excluding the current one,
- require a minimum separation,
- optionally preview the target.

This mode tests a more automatic traversal style.

---

# Jump Mode 6: Assisted Target

Use cursor direction as player intent, but bias toward a nearby valid surface.

For example:

```text
rawDirection = mouseAim

if candidate surface is sufficiently aligned:
    direction = blend(rawDirection, directionToCandidate)
```

Expose:

```text
assistAngle
assistStrength
assistRange
```

This is not essential for the first implementation pass, but the architecture should support it.

---

# Horizontal / Surface Movement

A and D are the primary movement inputs.

The important design question is what "left" and "right" mean when the player can stand on every side of an object.

Support multiple testable modes.

Recommended enum:

```csharp
public enum SurfaceMovementMode
{
    ScreenRelativeLocked,
    ScreenRelativeContinuous,
    SurfaceRelative
}
```

---

# Movement Mode 1: Screen-Relative Locked

This is the primary intended experiment.

When A or D is first pressed:

1. Determine the two possible surface traversal directions:
   - `+tangent`
   - `-tangent`
2. Compare them to screen/world left or right.
3. Choose the direction that best matches the pressed key.
4. Lock that traversal direction until the key is released.

For D:

```text
choose tangent direction with greatest dot(Vector2.right)
```

For A:

```text
choose tangent direction with greatest dot(Vector2.left)
```

Once chosen, keep that path direction even if the surface curves around and the player starts physically traveling in the opposite screen-space direction.

Example:

```text
D pressed
→ choose clockwise
→ continue clockwise around object
→ release D
→ clear lock
```

This avoids rubber-banding when the local tangent becomes vertical or reverses.

---

# Why the Direction Lock Matters

Without locking:

```text
         O
       /
    __(   )__
```

As the local tangent crosses a point where "clockwise" changes from screen-right to screen-left, continuously reevaluating the meaning of D could reverse the player.

That would feel unstable.

The locked mode should treat a held key roughly as:

> Keep traveling around this surface in the direction I chose when I pressed the button.

Once the key is released, the next press can reevaluate direction.

---

# Movement Mode 2: Screen-Relative Continuous

Continuously choose the path direction that best matches screen left/right.

This may create direction flipping near vertical tangents, but it should be implemented because the prototype exists to test assumptions.

---

# Movement Mode 3: Surface-Relative

Use fixed path orientation.

For example:

```text
A = negative path direction
D = positive path direction
```

On a closed surface, this is effectively clockwise/counterclockwise.

This may prove more learnable than expected once players understand the geometry.

---

# Input Conflict Handling

For the MVP:

- A only: move according to active movement mode.
- D only: move according to active movement mode.
- Neither: stop or decelerate.
- A + D simultaneously: stop / neutral input.

When a locked direction mode is being used, releasing the relevant key must reset its directional lock.

---

# Optional Movement Tuning

Expose:

```text
surfaceMoveSpeed
surfaceAcceleration
surfaceDeceleration
surfaceSnapStrength
```

It may be useful to compare:

- instant arcade velocity,
- smooth acceleration,
- slight momentum.

Default toward responsive arcade-style movement.

---

# Procedural Leg System

The player should have simple visible legs even though they are cosmetic.

The goal is not polished character animation.

The goals are:

- make the prototype more readable,
- communicate surface attachment,
- make playtesting more enjoyable,
- give immediate feedback about whether the eye is attached or airborne.

At minimum, use two visible legs.

Four legs are optional if simple to add.

---

# Leg Rig Concept

Each leg contains:

```text
body socket
knee / bend point
foot target
```

or a simpler equivalent.

If implementing IK is easy, use basic 2-bone IK.

If not, use:

- a line renderer,
- a curved procedural line,
- a simple sprite strip,
- or two line segments.

Do not spend excessive time on visual polish.

---

# Leg Contact Targets

When attached, calculate desired feet using virtual offsets along the surface.

For example:

```text
leftFootDesired =
    surface sample behind player

rightFootDesired =
    surface sample ahead of player
```

Offset slightly outward if necessary.

The feet should visually sit on the surface.

The leg system should read surface samples from the same surface API as the controller.

Again:

> Visual foot targets do not determine movement physics.

---

# Basic Walking Animation

Implement a simple alternating procedural step cycle.

A useful behavior:

```text
if foot is farther than stepThreshold from desired target:
    move that foot to a new target
```

Alternate feet so they do not both step at once.

Example:

```text
left foot planted
→ right foot steps
→ right foot planted
→ left foot steps
```

Foot repositioning can use a short interpolation arc:

```text
start
→ lift slightly away from surface
→ move forward
→ plant
```

Useful parameters:

```text
stepThreshold
stepDuration
stepHeight
footSpacing
legLength
```

Step speed can scale with player speed.

If the player reverses direction, feet should recover gracefully rather than entering a broken state.

---

# Idle Attached Animation

When attached but not moving:

- feet remain planted,
- legs continue pointing to the surface,
- optionally add tiny breathing/wobble motion,
- no unnecessary stepping.

The eyeball/pupil should still follow the mouse.

---

# Attach Animation

When the player attaches to a surface:

1. Movement snaps/blends the player into the correct attached location.
2. Legs extend rapidly toward appropriate surface points.
3. Feet settle onto the surface.
4. Walking cycle begins only after attachment stabilizes.

The animation should make the attachment visually obvious.

Optional:

- tiny squash/stretch on the eyeball,
- small landing bounce,
- brief leg compression.

Keep these cosmetic.

---

# Detach / Jump Animation

When the player jumps:

1. Feet release from the surface.
2. Legs briefly trail behind the eye.
3. Legs retract toward the body during flight.

Optional visual idea:

```text
attached:
   \ O /
____\_/____

airborne:
    \|/
     O
    / \
```

The specific artwork is unimportant.

The key is that the character visibly stops "walking" when airborne.

---

# Airborne Leg Animation

While airborne:

- legs should not search for physical contact every frame,
- feet should retract toward a neutral flying pose,
- legs may trail opposite velocity,
- optionally wobble or kick slightly.

When the attachment system identifies an imminent target, legs may optionally begin extending toward it shortly before attachment.

That anticipation is optional.

---

# Eye Rotation vs Body Orientation

Do not use the eye's mouse-facing direction to determine leg attachment or body orientation unless explicitly testing a jump mode based on aim.

The following concepts must remain separate:

```text
surface orientation
movement direction
eye aim direction
jump direction
leg animation
```

This separation is critical for experimentation.

---

# Debug Visualization

Debug visualization is a required feature, not an optional polish item.

Provide a toggleable debug overlay using Gizmos, Debug.DrawLine, or equivalent.

Display:

- true player center,
- current surface point,
- current surface normal,
- current tangent,
- current traversal direction,
- active jump vector,
- attachment radius,
- nearby candidate surfaces,
- selected attachment target,
- recently detached surface,
- virtual left/right foot samples,
- velocity vector.

Suggested visual concept:

```text
              jump
                ↗

              O
         ← tangent →
              |
              ↓ normal
______________●______________
          surface point
```

Use distinct debug colors if convenient.

Also show useful text in the UI:

```text
State: Attached
Surface: Blob_03
Movement Mode: ScreenRelativeLocked
Jump Mode: SmoothedSurfaceNormal
Traversal Sign: +1
Speed: 4.7
```

---

# Runtime Prototype Controls

The prototype should make it easy to compare variants without editing code.

Prefer one of:

- Inspector settings exposed during Play Mode,
- a small debug UI,
- keyboard shortcuts,
- ScriptableObject configuration.

At minimum, allow changing:

```text
Jump Direction Mode
Surface Movement Mode
Attachment Mode
Jump Speed
Move Speed
Gravity
Air Control
Attach Distance
Reattach Cooldown
Cursor Clamp Angle
Surface Normal Smoothing
```

during rapid testing.

A simple on-screen debug menu is encouraged if it is quick to implement.

---

# Test Scene

Create at least one primary scene:

```text
Assets/Prototyping/Movement/Scenes/MovementSandbox.unity
```

The sandbox should contain a compact collection of surfaces designed to stress different controller behaviors.

Include:

1. Flat floor.
2. Vertical wall.
3. Ceiling.
4. Circle.
5. Small blob.
6. Large irregular blob.
7. Tall/narrow object.
8. Two surfaces close enough to jump between.
9. Two surfaces barely outside normal attachment range.
10. A cluster of several surfaces that tests candidate selection.
11. An overhang.
12. A narrow gap.
13. Surfaces arranged above, below, left, and right of the player.

Avoid building a polished level.

The scene should be a laboratory.

---

# Optional Focused Test Scenes

If useful, additional tiny scenes can be created under:

```text
Scenes/OptionalFocusedTests/
```

Possible examples:

```text
NormalTesting.unity
AttachmentTesting.unity
ControlDirectionTesting.unity
LegAnimationTesting.unity
```

Only create these if they genuinely make iteration easier.

---

# Suggested Script Responsibilities

The exact names may change, but preserve clear responsibility boundaries.

## PlayerMotor2D

Responsible for:

- attached movement,
- airborne movement,
- state transitions,
- velocity,
- movement speed,
- gravity,
- surface-constrained positioning.

Not responsible for:

- rendering legs,
- picking visual colors,
- generating surfaces.

---

## Surface2D

Responsible for:

- representing traversable geometry,
- providing closest surface samples,
- normals,
- tangents,
- optional path parameters,
- querying neighboring samples.

---

## SurfaceSensor

Responsible for:

- finding nearby Surface2D candidates,
- overlap/range detection,
- maintaining candidate lists.

---

## SurfaceAttachmentController

Responsible for:

- evaluating attachment candidates,
- selecting targets,
- applying reattachment cooldown rules,
- entering/exiting attachment state.

---

## JumpDirectionResolver

Responsible for:

- all jump-direction modes.

Given player/surface/input state, return a normalized direction.

Avoid embedding jump-mode switch logic throughout the main motor.

---

## SurfaceInputResolver

Responsible for:

- translating A/D into a traversal sign,
- implementing:
  - ScreenRelativeLocked,
  - ScreenRelativeContinuous,
  - SurfaceRelative.

---

## EyeAimController

Responsible for:

- mouse-world lookup,
- pupil/eye aiming,
- nothing related to physical player orientation.

---

## ProceduralLegRig

Responsible for:

- virtual cosmetic feet,
- planting,
- step cycles,
- attach animation,
- detach animation,
- airborne pose.

Must not control gameplay.

---

## OrganicSurfaceGenerator

Responsible for:

- creating organic shape samples,
- generating visible geometry,
- generating collider geometry,
- exposing seed and shape parameters.

---

## MovementDebugRenderer

Responsible for:

- normals,
- tangents,
- candidate rendering,
- jump vectors,
- status text,
- optional debug UI.

---

# Recommended Configuration Types

Example enums:

```csharp
public enum PlayerMovementState
{
    Attached,
    Airborne
}

public enum SurfaceMovementMode
{
    ScreenRelativeLocked,
    ScreenRelativeContinuous,
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
    Strict,
    Nearest,
    Magnetic
}
```

These exact names are not mandatory, but the modes should be easy to switch.

---

# Important Edge Cases

The prototype should explicitly test and handle the following.

## Immediate reattachment

Jumping must not instantly stick the player back onto the surface they left.

---

## Tangent sign flipping

Screen-relative movement should not reverse unexpectedly as the player travels around a curved object.

---

## Surface seams

The player should not visibly jitter at transitions between collider segments.

---

## Very curved geometry

Normals should remain stable on small-radius curves.

---

## Closely packed surfaces

Attachment selection should be deterministic and understandable.

---

## Jumping between adjacent objects

The player should not accidentally attach to a less sensible target because it is a few pixels closer.

The initial algorithm may still be imperfect; debug visualization should make this easy to diagnose.

---

## Mouse behind current surface

Cursor-directed jump modes must decide whether jumping "into" the currently attached object is permitted.

For `ClampedCursorDirection`, it should naturally be prevented by the angular clamp.

For unrestricted cursor jump, choose a sensible behavior and expose it if necessary.

---

## Input reversal

Rapidly switching from A to D should cleanly reset traversal locks.

---

## Airborne near several surfaces

Candidate selection should not oscillate rapidly between targets.

---

# Recommended Initial Values

These are starting points only and should be tunable.

```text
Player radius:          ~0.4–0.6 Unity units
Surface move speed:     ~4–6
Jump speed:             ~7–10
Gravity:                ~12–20
Air control:            0–0.4
Attach distance:        ~0.3–0.8
Reattach cooldown:      ~0.10–0.20 s
Clamp jump angle:       45–60 degrees
Virtual foot spacing:   roughly player diameter
Step duration:          ~0.10–0.20 s
```

Do not treat these as requirements.

The debug UI should make tuning easy.

---

# Prototype Priorities

Implement in roughly this order.

## Phase 1 — Surface Representation

- Surface2D abstraction.
- Flat test surface.
- Circle/blob surface.
- Closest sample.
- Tangent.
- Normal.
- Debug visualization.

Success condition:

> Clicking/moving around the scene shows correct and smooth points, tangents, and normals.

---

## Phase 2 — Attached Player Movement

- PlayerRoot.
- Circular collider.
- Attached state.
- Surface-following.
- A/D traversal.
- ScreenRelativeLocked behavior.
- Movement debugging.

Success condition:

> The player can smoothly crawl completely around a curved object, including its underside.

---

## Phase 3 — Jumping

- Airborne state.
- Jump velocity.
- Gravity.
- Reattachment.
- Reattach cooldown.

Implement first:

```text
LocalSurfaceNormal
SmoothedSurfaceNormal
CursorDirection
ClampedCursorDirection
```

Success condition:

> The player can repeatedly jump between several test objects without getting stuck or instantly reattaching.

---

## Phase 4 — Attachment Variants

- candidate list,
- nearest target,
- configurable attach range,
- approach checks,
- Strict / Nearest / Magnetic modes where feasible.

Success condition:

> It is easy to understand why the player attached to a particular object using debug visualization.

---

## Phase 5 — Procedural Legs

- leg sockets,
- foot targets,
- planted feet,
- alternating walking,
- attach pose,
- detach pose,
- airborne pose.

Success condition:

> The player clearly looks attached while crawling and clearly looks airborne after jumping.

No polished artwork is necessary.

---

## Phase 6 — Organic Generator

- adjustable seed,
- blob generation,
- smoothing,
- collider creation,
- reproducible generated surfaces.

Success condition:

> A designer can quickly generate different curved layouts for movement testing.

---

## Phase 7 — Testing UI

Add simple runtime controls for movement experiments.

Example:

```text
F1 / UI dropdown:
Movement Mode

F2 / UI dropdown:
Jump Mode

F3 / UI dropdown:
Attachment Mode
```

Exact controls do not matter.

The goal is to avoid stopping Play Mode every time we compare two behaviors.

---

# MVP Completion Criteria

The movement prototype is complete enough for playtesting when all of the following are true:

- [ ] All prototype files live under `Assets/Prototyping/Movement/`.
- [ ] Player is represented by a circular eyeball.
- [ ] Pupil/eye can point toward the mouse.
- [ ] Player can attach to floors, walls, ceilings, circles, and organic blobs.
- [ ] Player can move continuously around curved surfaces.
- [ ] A/D movement supports multiple interpretation modes.
- [ ] `ScreenRelativeLocked` is implemented.
- [ ] Player can jump and become airborne.
- [ ] Player does not immediately reattach to the surface it left.
- [ ] Player automatically attaches to nearby valid surfaces.
- [ ] Multiple jump-direction modes are available.
- [ ] Local normal jump is available.
- [ ] Smoothed/virtual-foot normal jump is available.
- [ ] Cursor-directed jump is available.
- [ ] Clamped cursor-directed jump is available.
- [ ] Attachment range is tunable.
- [ ] Movement speed, jump speed, gravity, and air control are tunable.
- [ ] Organic test shapes can be generated quickly.
- [ ] Surface normals/tangents are smooth enough for playtesting.
- [ ] Basic procedural legs visually attach to the current surface.
- [ ] Legs alternate while walking.
- [ ] Legs visibly detach/retract during jumps.
- [ ] Legs return to surface contact when landing.
- [ ] Leg animation does not affect gameplay calculations.
- [ ] Debug lines clearly show surface data and jump behavior.
- [ ] The sandbox contains multiple traversal layouts.
- [ ] Major movement modes can be switched rapidly during testing.

---

# What Is Explicitly Out of Scope

Do not spend prototype time on:

- combat,
- enemy AI,
- roguelike progression,
- health,
- weapons,
- procedural dungeon generation,
- final art,
- commercial-themed levels,
- inventory,
- menus,
- save systems,
- networking,
- polished sound,
- advanced animation rigs,
- production architecture unrelated to movement testing.

Small visual or audio feedback is acceptable if it directly improves movement playtesting.

---

# Primary Questions We Want the Prototype to Answer

The implementation should make it easy to answer these questions experimentally.

### Surface movement

Does crawling around arbitrary curves feel fun?

Does `ScreenRelativeLocked` feel intuitive?

Is fixed clockwise/counterclockwise movement actually easier to understand?

How much acceleration or momentum feels good?

---

### Jumping

Does jumping along the exact surface normal feel too rigid?

Does the smoothed virtual-foot normal feel better?

Is full cursor-directed jumping too precise or demanding?

Does clamped cursor aiming provide a better compromise?

Would automatic target selection make traversal more fluid?

---

### Attachment

How generous should automatic attachment be?

Should surfaces strongly "magnetize" the player?

Does attachment need velocity/approach-angle filtering?

How frequently does nearest-surface selection disagree with player intent?

---

### Character readability

Do simple procedural legs make it obvious which surface the player is attached to?

Does the attach/detach animation communicate state changes clearly?

Does eye aiming remain visually understandable while the body crawls around arbitrary orientations?

---

# Final Development Philosophy

This is an experimentation scene, not the final movement code.

Prefer:

```text
easy to modify
easy to visualize
easy to compare
easy to tune
```

over:

```text
architecturally perfect
hyper-general
highly optimized
production-ready
```

However, maintain clean boundaries between:

```text
surface geometry
player physics
input interpretation
jump direction
attachment selection
eye aiming
leg animation
```

because those are the systems we specifically want to vary independently.

The ideal outcome is a sandbox where we can spend five minutes changing settings and quickly answer:

> Which version of moving, jumping, and attaching actually feels good?
