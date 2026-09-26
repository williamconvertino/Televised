# Enemy Movement Sandbox Variants

## Purpose

This document defines a collection of prototype enemy variants and sandbox test setups for exercising the enemy movement architecture described in `Enemy_Movement.md`.

These are **not intended to be final enemy designs**. They are diagnostic and showcase configurations built from the shared movement framework.

The goals are to:

- Test each movement subsystem in isolation.
- Test important combinations of subsystems.
- Make failures easy to see.
- Provide visually distinct examples of what the architecture can support.
- Give designers a small number of parameters to tweak while watching the effects in real time.
- Provide repeatable regression scenarios as the movement architecture evolves.
- Stress coordination behavior with multiple enemies without requiring finished combat mechanics.

Each test enemy should be implemented primarily as a configuration/preset over the shared architecture. Avoid creating enemy-specific movement code unless a test exposes a genuinely missing generic capability.

---

# 1. Sandbox Philosophy

The sandbox should be built around a few principles.

## 1.1 Exaggerate Mechanics During Testing

A test enemy should make its target behavior obvious.

For example:

- A surface-angle test should include geometry that clearly transitions from legal to illegal.
- A declumping test should begin with enemies intentionally packed too tightly.
- A reservation test should offer one extremely desirable landing area so conflicts are easy to provoke.
- A bird-style flight test should use very limited turning and climbing so route planning is visible.
- An aggro-stagger test should use many identical enemies so synchronized behavior is obvious if the system fails.

Do not tune all test enemies to look polished. Some should be intentionally extreme diagnostic cases.

---

## 1.2 Prefer Reusable Test Arenas

Many variants can share the same geometry.

Recommended reusable arena pieces:

```text
Flat floor
Steep ramps
Vertical walls
Ceiling
Convex and concave corners
Small isolated platforms
Moving platforms
Tunnel
Tall chamber
Tiered ledges
Open flight space
Perches
Narrow choke points
Wide open room
```

The sandbox can contain several adjacent rooms or selectable scenarios rather than requiring a separate scene for every enemy.

---

## 1.3 Make Runtime Tuning Easy

For every spawned test enemy, expose the most relevant configuration values in:

- Inspector fields.
- A runtime debug panel if practical.
- Preset assets that can be duplicated and edited.

The sandbox should allow:

```text
Spawn one enemy
Spawn a small group
Spawn a large group
Reset positions
Toggle player aggro
Toggle debug overlays
Pause/unpause AI
Slow time
```

Slow-motion is especially useful for jumps, reservations, and flight steering.

---

# 2. Recommended Shared Sandbox Areas

Before defining individual enemies, it is useful to establish a small number of reusable test areas.

---

## Arena A: Surface Course

Purpose:

- Surface legality.
- Corner traversal.
- Edge behavior.
- Walking.
- Wandering.

Suggested geometry:

```text
flat floor
-> 20 degree ramp
-> 45 degree ramp
-> 70 degree ramp
-> vertical wall
-> rounded wall-to-ceiling transition
-> ceiling
-> sharp convex corner
-> ledge/drop
```

The player should be able to move beside, above, and below the course.

---

## Arena B: Platform Garden

Purpose:

- Jumping.
- Destination selection.
- Reservations.
- Moving-platform correction.

Suggested geometry:

```text
Several small platforms at different heights
One large central platform
One moving horizontal platform
One moving vertical platform
A ceiling platform
A few intentionally tempting shared destinations
```

Keep gaps short enough for some jump profiles and too long for others.

---

## Arena C: Crowd Tunnel

Purpose:

- Separation.
- Predictive crowding.
- Surface diversity.
- Declumping.
- Ceiling redistribution.

Suggested geometry:

```text
Long enclosed tunnel
Floor
Two walls
Ceiling
Wide enough for movement on all four surfaces
Narrow enough for local density to matter
```

Spawn points should support placing 10-30 enemies in a dense cluster.

---

## Arena D: Flight Chamber

Purpose:

- Temporary flight.
- Climb restrictions.
- Perching.
- Gliding.
- Intermediate destinations.

Suggested geometry:

```text
Tall open room
Low, medium, and high ledges
Several wall perches
Ceiling perches
Central open space
One unreachable high player position
One reachable multi-step route
```

---

## Arena E: Coordination Bowl

Purpose:

- Local action staggering.
- Group behavior.
- Aggro propagation.
- Destination diversity.

Suggested geometry:

```text
Wide central floor
Several surrounding ledges
Two ceiling strips
Multiple equally useful routes toward player
```

This arena should comfortably hold 20+ enemies.

---

## Arena F: Moving Platform Course

Purpose:

- Local-coordinate targets.
- Moving-surface attachment.
- Jump target tracking.
- Reservations on moving geometry.

Suggested geometry:

```text
One horizontal platform
One vertical platform
One oscillating platform
One rotating/curved surface if supported
Several static launch platforms
```

---

# 3. Variant 1 - Basic Walker

## Purpose

This is the baseline enemy.

It should showcase:

- Surface-attached movement.
- Acceleration/deceleration.
- Idle/wander.
- Basic range aggro.
- Pursuit along the current surface.
- Edge handling.

It should deliberately have no jumping or flight.

---

## Suggested Profile

```text
CanWalk = true
CanJump = false
CanFly = false

MaxSurfaceAngle = 35 degrees

WalkSpeed = medium
Acceleration = medium
Deceleration = medium

EdgeBehavior = Stop

DetectionRange = medium
RequireLineOfSight = false

PersonalSpace = low/moderate
```

---

## Best Sandbox Setups

### Setup 1: Flat Pursuit Lane

Use a long flat floor.

Place:

```text
enemy ---------------- player
```

The player walks toward and away from the enemy.

Observe:

- Idle/wander behavior before aggro.
- Detection range.
- Acceleration after detection.
- Pursuit speed.
- Deaggro behavior.

### Setup 2: Ledge Stop

Place the enemy on a platform with a clear drop.

Lure the player past the edge.

Expected:

- Enemy approaches edge.
- Enemy stops cleanly.
- No jittering.
- No accidental fall.

### Setup 3: Gentle Ramp Course

Use 20-30 degree ramps.

Expected:

- Enemy traverses legal ramps without treating every slope change as a new obstacle.

---

## Most Useful Tuning Levers

```text
WalkSpeed
Acceleration
MaxSurfaceAngle
```

---

## Failure Signs

- Oscillation at edges.
- Rapid left/right changes.
- Falling despite `EdgeBehavior = Stop`.
- Movement direction disagreeing with surface tangent.
- Jitter on slope transitions.

---

# 4. Variant 2 - Edge Runner

## Purpose

Showcase different configurable edge responses.

Use several copies of the same enemy with different `EdgeBehavior` values.

---

## Suggested Profiles

Create four presets:

```text
EdgeStopper
EdgeTurner
EdgeFaller
EdgeTraverser
```

All other settings should be identical.

---

## Best Sandbox Setups

### Setup 1: Identical Parallel Platforms

Build four identical narrow platforms.

Spawn one variant on each.

Place the player beyond the far edge.

Expected:

```text
Stopper:
    stops

Turner:
    reverses

Faller:
    deliberately continues and falls

Traverser:
    searches for a jump/alternate route if capability is enabled
```

### Setup 2: Wander Test

Disable player aggro.

Allow each variant to wander for an extended period.

This reveals whether edge behavior is stable outside pursuit.

---

## Most Useful Tuning Levers

```text
EdgeBehavior
EdgeDetectionDistance
MinimumMoveDuration
```

---

## Failure Signs

- Enemy rapidly alternates between stop and turn.
- Turner repeatedly pivots every frame at edge.
- Faller gets stuck attempting to stay attached.
- Traverser repeatedly requests impossible movement.

---

# 5. Variant 3 - Wall Crawler

## Purpose

Showcase:

- Large legal surface-angle range.
- Wall traversal.
- Ceiling traversal.
- Corner transitions.
- Continuous pursuit across differently oriented surfaces.

This is the core "spider" locomotion test.

---

## Suggested Profile

```text
CanWalk = true
CanJump = false
CanFly = false

MaxSurfaceAngle = 180 degrees

MaxCornerTraversalAngle = generous

WalkSpeed = medium
SurfaceStickStrength = high

DetectionRange = large
RequireLineOfSight = false
```

---

## Best Sandbox Setups

### Setup 1: Loop Room

Use a rectangular room where all inner surfaces are traversable.

Place the enemy on floor and player on ceiling.

Expected:

```text
floor -> wall -> ceiling
```

without detachment.

### Setup 2: Rounded vs Sharp Corners

Include:

- One rounded floor-to-wall transition.
- One sharp transition.
- One intentionally too-sharp transition.

Use `MaxCornerTraversalAngle` to make the difference visible.

### Setup 3: Exterior Object

Put the crawler on the outside of a large box/cylinder.

Have the player move around the object.

Expected:

- Enemy follows around sides.
- Surface normal changes smoothly.
- No orientation jitter.

---

## Most Useful Tuning Levers

```text
MaxSurfaceAngle
MaxCornerTraversalAngle
SurfaceStickStrength
```

---

## Failure Signs

- Detachment at ordinary corners.
- Incorrect tangent direction after crossing a corner.
- Sudden orientation flips.
- Ceiling movement reversed.
- Corner oscillation.

---

# 6. Variant 4 - Shy Scout

## Purpose

Showcase the perception system.

This enemy should move slowly and only aggro under specific perceptual conditions.

---

## Suggested Profile

```text
CanWalk = true

WanderSpeedMultiplier = low

DetectionRange = medium
FieldOfView = narrow
RequireLineOfSight = true
LineOfSightMode = FacingCone

AggroReactionDelay = noticeable
MemoryTime = short
LoseRange = larger than DetectionRange
```

---

## Best Sandbox Setups

### Setup 1: Walk Behind It

Place the scout on a long floor.

Approach:

- From behind.
- From front.
- From outside FOV and then enter FOV.

Expected:

- Detection clearly depends on facing.

### Setup 2: Pillar Occlusion

Put a pillar between player and enemy.

Move into and out of LOS.

Expected:

- Scout acquires only with valid LOS.
- Short occlusions do not instantly erase aggro if memory is enabled.

### Setup 3: Range Hysteresis

Let player enter detection range and then move back slightly.

Expected:

- Enemy does not repeatedly acquire/deaggro at one exact radius.

---

## Most Useful Tuning Levers

```text
FieldOfView
AggroReactionDelay
MemoryTime
```

---

## Failure Signs

- Detection through geometry.
- Aggro flickering at range boundary.
- Facing cone not matching visual orientation.
- Instant synchronized reaction when many scouts are spawned.

---

# 7. Variant 5 - Surface Sentinel

## Purpose

Showcase same-surface aggro logic.

This enemy should be calm while the player is nearby but on another surface, then react when the player attaches to its surface.

---

## Suggested Profile

```text
CanWalk = true

DetectionRange = large

SameSurfaceAcquire = true
SameSurfaceRetain = false

AggroReactionDelay = small
LoseRange = large
```

Interpretation:

- Must share a surface to wake it.
- Once awakened, it can continue chasing after the player leaves.

---

## Best Sandbox Setups

### Setup 1: Parallel Platforms

Place player and sentinel on two close parallel platforms.

Expected:

- No aggro despite physical proximity.
- Jump/attach player onto sentinel's platform.
- Sentinel acquires.

### Setup 2: Shared Large Object

Use one large box with floor, wall, and ceiling attachment.

Test whether "same surface object" semantics behave as intended.

### Setup 3: Retention Test

Trigger aggro on same surface, then leave.

Expected:

- Enemy retains aggro according to retention configuration.

---

## Most Useful Tuning Levers

```text
SameSurfaceAcquire
SameSurfaceRetain
MemoryTime
```

---

## Failure Signs

- Surface identity changes unexpectedly across a continuous object.
- Enemy constantly toggles aggro around corners.
- Player is considered same-surface merely due to spatial proximity.

---

# 8. Variant 6 - Burst Pursuer

## Purpose

Showcase time-dependent aggro speed curves.

The enemy should be visibly different from a normal chaser.

---

## Suggested Profile

```text
CanWalk = true

WanderSpeedMultiplier = 0.4

AggroSpeedCurve:
    immediate high burst
    then decay toward normal speed

AggroReactionDelay = short
```

Suggested conceptual curve:

```text
0.0 sec -> 2.8x
0.5 sec -> 2.2x
1.5 sec -> 1.3x
3.0 sec -> 1.0x
```

---

## Best Sandbox Setups

### Setup 1: Long Straight Hall

Give the enemy enough distance to display the whole speed curve.

### Setup 2: Repeated Aggro/Deaggro

Use a barrier or trigger arrangement where the player can repeatedly leave and re-enter acquisition conditions.

Expected:

- Each new valid aggro event restarts the burst intentionally.
- No accidental reset every time LOS flickers.

---

## Most Useful Tuning Levers

```text
AggroSpeedCurve
AggroReactionDelay
DeaggroDelay
```

---

## Failure Signs

- Speed curve restarts continuously while already aggroed.
- Burst produces physics instability.
- Enemy overshoots and oscillates badly.

---

# 9. Variant 7 - Basic Hopper

## Purpose

Showcase:

- Surface candidate search.
- Jump reachability.
- Jump commitment.
- Landing.
- Jump cooldown.

This should be the simplest reliable surface-to-surface jumper.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true
CanFly = false

JumpRange = medium
JumpHeight = medium
JumpWindup = noticeable
AirSteering = low/moderate
MovingTargetCorrection = moderate

PlayerProgressWeight = high
CrowdEscapeWeight = low
SurfaceDiversityWeight = low
```

---

## Best Sandbox Setups

### Setup 1: Stepping Platforms

Arrange platforms so each successive platform is just inside jump range.

Expected:

```text
walk -> windup -> jump -> land -> continue
```

### Setup 2: Reachability Boundary

Place three potential targets:

```text
one clearly reachable
one just barely reachable
one clearly unreachable
```

Expected:

- Invalid candidate is rejected.
- Enemy chooses a legal destination.
- No repeated impossible jump attempts.

### Setup 3: Opposite-Side Player

Place player on a platform requiring multiple jumps.

Expected:

- Enemy makes useful incremental progress.

---

## Most Useful Tuning Levers

```text
JumpRange
JumpHeight
ArcBias
```

---

## Failure Signs

- Selecting unreachable candidates.
- Replanning constantly during windup.
- Airborne retarget jitter.
- Landing but failing to attach.
- Immediate repeated jumps without cooldown.

---

# 10. Variant 8 - Moving-Platform Hopper

## Purpose

Specifically test:

- `SurfaceReference + LocalPosition` targeting.
- Moving-platform reservation tracking.
- Guided jump correction.
- Attachment to moving surfaces.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

JumpRange = medium
AirSteering = low
MovingTargetCorrection = high

LandingClaimRadius = moderate
```

---

## Best Sandbox Setups

### Setup 1: Horizontal Platform

Enemy chooses a landing spot on a platform that moves sideways after takeoff.

Expected:

- Landing target follows the platform.
- Enemy adjusts smoothly.
- No world-space stale target.

### Setup 2: Vertical Platform

Repeat with a vertically oscillating platform.

### Setup 3: Two Enemies, One Moving Platform

Both enemies want the platform.

Expected:

- First enemy claims one region.
- Second chooses another valid region or waits/chooses another target.

---

## Most Useful Tuning Levers

```text
MovingTargetCorrection
AirSteering
LandingTolerance
```

---

## Failure Signs

- Enemy jumps toward where platform used to be.
- Correction looks like teleporting.
- Enemy reaches platform but slides/falls because attachment does not inherit motion.
- Reservation remains at stale world coordinates.

---

# 11. Variant 9 - Perch Claimer

## Purpose

Aggressively showcase hard and soft destination reservations.

This test should make reservation behavior visually undeniable.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

JumpRange = high
PlayerProgressWeight = very high

HardClaimRadius = moderate
SoftClaimRadius = large
ClaimPenalty = high

DecisionInterval = short
```

Spawn several identical enemies.

---

## Best Sandbox Setups

### Setup 1: Single Tempting Platform

Create one long destination platform directly beside the player.

Spawn 5-10 enemies opposite it.

Expected:

- Enemies choose distinct landing positions.
- Claims become visibly distributed across the surface.

### Setup 2: Tiny Platform

Use a destination that can only safely fit one or two enemies.

Expected:

- Once claimed/full, other enemies choose inferior alternatives rather than stacking.

### Setup 3: Kill/Interrupt Claim Owner

While an enemy has a claim:

- Destroy/disable it.
- Knock it away if supported.

Expected:

- Reservation is released or expires.
- Another enemy can use the location.

---

## Most Useful Tuning Levers

```text
HardClaimRadius
SoftClaimRadius
ClaimPenalty
```

---

## Failure Signs

- Multiple enemies select the same target before claims register.
- Claims never expire.
- Soft claims behave like hard exclusions.
- Claimed positions do not move with their surfaces.

---

# 12. Variant 10 - Personal-Space Walker

## Purpose

Isolate local separation without jumping or alternate-surface movement.

This is the cleanest test of soft personal space.

---

## Suggested Profile

```text
CanWalk = true
CanJump = false
CanFly = false

PersonalSpaceMean = relatively large
PersonalSpaceVariance = moderate

SeparationStrength = high
CrowdPressureThreshold = high

StayPutBias = moderate
```

---

## Best Sandbox Setups

### Setup 1: Forced Dense Spawn

Spawn 10 enemies nearly overlapping on a long floor.

Expected:

- They spread gradually.
- They do not form a perfect equal-spacing lattice.
- Not all enemies move at exactly the same moment.

### Setup 2: Moving Player Compression

Place enemies between the player and a wall.

Move player toward them.

Expected:

- They compress somewhat.
- They do not pass through one another.
- They recover spacing when pressure is released.

### Setup 3: Narrow Hall

Make the hall too short for everyone to achieve ideal personal space.

Expected:

- System settles into an imperfect compromise rather than oscillating forever.

---

## Most Useful Tuning Levers

```text
PersonalSpaceMean
PersonalSpaceVariance
SeparationStrength
```

---

## Failure Signs

- Perfect crystalline spacing.
- Constant micro-shuffling.
- Explosive repulsion.
- Enemies crossing through one another.
- Entire cluster translating together unnecessarily.

---

# 13. Variant 11 - Patient Declumper

## Purpose

Showcase:

- Crowd pressure.
- Crowd patience.
- Relocation intents.
- Predictive departures.
- Staggered declumping.

This should explicitly test the rule:

> Some enemies move first; others realize the problem is already being solved.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

CrowdPressureThreshold = moderate
CrowdPatience = moderate
CrowdPatienceVariance = high

CrowdEscapeWeight = high
RelocationCooldown = long

PredictedDepartureWeight = high
ActionStaggerStrength = high
```

---

## Best Sandbox Setups

### Setup 1: Dense Platform With Empty Alternatives

Spawn 12 enemies on one medium platform.

Place several empty nearby platforms.

Do not initially place the player nearby.

Expected:

- A few enemies decide to move.
- Others wait.
- Predicted density falls.
- The group redistributes gradually.

### Setup 2: Toggle Predictive Crowding

Run identical setup with predicted occupancy:

```text
ON
OFF
```

Expected difference:

- OFF should cause much more overreaction.
- ON should look calmer and more staggered.

### Setup 3: Long Observation

Let the settled group remain idle.

Expected:

- They should stop moving once distribution is acceptable.
- No perpetual optimization.

---

## Most Useful Tuning Levers

```text
CrowdPatience
PredictedDepartureWeight
RelocationCooldown
```

---

## Failure Signs

- Entire cluster relocates simultaneously.
- Agents endlessly trade places.
- Departures do not lower predicted crowd pressure.
- Every enemy eventually migrates away despite sufficient space.

---

# 14. Variant 12 - Ceiling Redistributor

## Purpose

Showcase surface diversity.

This is the canonical test for:

> Some enemies should deliberately use walls/ceilings because the obvious route is crowded.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

MaxSurfaceAngle = 180 degrees

SurfaceDiversityWeight = high
AlternateSurfaceBonus = high
CrowdEscapeWeight = high

PlayerProgressWeight = moderate
StayPutBias = moderate
```

---

## Best Sandbox Setups

### Setup 1: Crowd Tunnel

Spawn 15-25 enemies on the floor of Arena C.

Put player ahead in the tunnel.

Expected:

- Some pursue along floor.
- Others jump to walls/ceiling.
- Enemies continue making player progress while distributing vertically.

### Setup 2: Change Surface Diversity Weight

Compare:

```text
0
medium
very high
```

Expected:

- 0: floor-heavy crowd.
- medium: natural mixed occupancy.
- very high: exaggerated spreading to alternate surfaces.

### Setup 3: Artificial Ceiling Congestion

Start with ceiling already crowded.

Expected:

- New enemies should prefer less occupied legal surfaces.

---

## Most Useful Tuning Levers

```text
SurfaceDiversityWeight
CrowdEscapeWeight
StayPutBias
```

---

## Failure Signs

- Everyone jumps to ceiling simultaneously.
- Enemies repeatedly switch floor/ceiling.
- Surface diversity overwhelms pursuit entirely.
- Empty alternate surfaces are ignored despite heavy crowding.

---

# 15. Variant 13 - Balloon Drifter

## Purpose

Showcase temporary, soft, floaty flight.

This should feel very different from a missile or direct chaser.

---

## Suggested Profile

```text
CanWalk = true
CanFly = true

FlightSpeed = low/medium
Acceleration = low
TurnRate = low

MinFlightTime = moderate
MaxFlightTime = long
FlightCooldown = moderate

MaxClimbAngle = generous

DirectPlayerWeight = low/moderate
LandingSearchRadius = large
LandingCommitment = high

DesiredAltitude = moderate/high
```

---

## Best Sandbox Setups

### Setup 1: Open Flight Chamber

Place player at several heights.

Expected:

- Balloon drifts rather than snapping toward player.
- Turns are gradual.
- It eventually lands.

### Setup 2: Moving Player Around Balloon

Circle around it rapidly.

Expected:

- It should not track the player perfectly.
- Heading commitment and low turn rate should remain visible.

### Setup 3: Flight Expiry

Place player somewhere that encourages continued pursuit.

Expected:

- Max flight duration still forces a sensible landing.
- Enemy does not hover forever.

---

## Most Useful Tuning Levers

```text
Acceleration
TurnRate
MaxFlightTime
```

---

## Failure Signs

- Missile-like tracking.
- Rapid heading flips.
- Hovering at max flight duration because no landing is selected.
- Landing immediately after takeoff.
- Repeated takeoff/landing loops.

---

# 16. Variant 14 - Swooper

## Purpose

Showcase bird/pterodactyl-style flight:

- Forward commitment.
- Limited turning.
- Limited climb.
- Swooping.
- Perch selection.
- Multi-stage pursuit.

---

## Suggested Profile

```text
CanWalk = true
CanFly = true

FlightSpeed = high
Acceleration = medium/high
TurnRate = limited

MinHeadingCommitment = high
MaxClimbAngle = moderate
MaxDiveAngle = generous

MinFlightTime = moderate
MaxFlightTime = moderate

PerchProgressWeight = high
DirectPlayerWeight = moderate

LandingCommitment = high
```

---

## Best Sandbox Setups

### Setup 1: Perch Circuit

Arrange 5-8 ledges around a tall room.

Expected:

```text
perch -> launch -> swoop -> turn -> land
```

### Setup 2: Player Above Direct Climb Limit

Place player substantially above enemy.

Provide an intermediate ledge.

Expected:

```text
enemy -> intermediate perch -> higher perch/player
```

rather than endless upward steering.

### Setup 3: Player Crosses Behind

Move through/behind the swooper while it is flying.

Expected:

- Enemy completes or substantially maintains its current trajectory.
- It does not instantly reverse.

---

## Most Useful Tuning Levers

```text
TurnRate
MinHeadingCommitment
MaxClimbAngle
```

---

## Failure Signs

- Orbiting player in tiny circles.
- Instant U-turns.
- Repeated failed direct climb.
- Refusal to use intermediate perches.
- Retargeting during every steering update.

---

# 17. Variant 15 - Glider

## Purpose

Showcase constrained flight where altitude is a resource.

The enemy should be able to move mostly forward/downward and require surfaces to regain height.

---

## Suggested Profile

```text
CanWalk = true
CanFly = true

MaxClimbAngle = near zero or negative
GlideBias = very high

TurnRate = moderate
FlightSpeed = medium/high

MaxFlightTime = moderate
PerchProgressWeight = high
```

---

## Best Sandbox Setups

### Setup 1: Descending Stair-Step Chamber

Place high starting perch and progressively lower surfaces.

Expected:

- Enemy glides downward through the route.

### Setup 2: Player Above Enemy

Expected:

- Enemy does NOT simply fly upward.
- It seeks an appropriate grounded/surface route or remains unable to reach.

### Setup 3: Player Below Enemy

Expected:

- Direct glide becomes highly attractive.

---

## Most Useful Tuning Levers

```text
MaxClimbAngle
GlideBias
PerchProgressWeight
```

---

## Failure Signs

- Gaining altitude despite restrictions.
- Hovering when no legal glide exists.
- Trying the same impossible upward route repeatedly.

---

# 18. Variant 16 - Launch Bug

## Purpose

Showcase the "jumping so far/straight that it is effectively flight" profile.

This tests whether flight can represent a highly committed launch without separate bespoke movement code.

---

## Suggested Profile

```text
CanWalk = true
CanFly = true

FlightSpeed = very high
Acceleration = high
TurnRate = near zero

MinFlightTime = short
MaxFlightTime = short

MinHeadingCommitment = full flight duration

LandingCommitment = very high
DirectPlayerWeight = high
```

---

## Best Sandbox Setups

### Setup 1: Gap Launcher

Enemy launches across a large gap toward a legal surface.

Expected:

- Clear launch.
- Near-ballistic travel.
- Minimal steering.
- Clean landing.

### Setup 2: Moving Player

Have player change direction after launch.

Expected:

- Enemy should not home aggressively.
- Commitment remains obvious.

### Setup 3: Multiple Launchers

Spawn several enemies.

Expected:

- Launches are staggered.
- Landing claims distribute them.

---

## Most Useful Tuning Levers

```text
FlightSpeed
TurnRate
MinHeadingCommitment
```

---

## Failure Signs

- Midair homing.
- Multiple enemies launching in perfect synchrony.
- Multiple enemies landing on exactly the same point.

---

# 19. Variant 17 - Reservation Flock

## Purpose

Stress-test destination claiming and predicted occupancy among many jumping/flying enemies.

Unlike `Perch Claimer`, this is a group-scale test.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true
CanFly = true

CoordinationRadius = large
MaxNeighbors = bounded

HardClaimRadius = moderate
SoftClaimRadius = moderate/high

TargetDiversity = high
PredictedCrowdWeight = high

ActionStaggerStrength = moderate/high
```

---

## Best Sandbox Setups

### Setup 1: Ten Enemies, Five Perches

Spawn 10-15 enemies opposite five attractive perches.

Expected:

- Perches fill progressively.
- Later enemies account for incoming occupants.
- Some choose second-best destinations.

### Setup 2: Remove a Perch Mid-Action

Disable one perch while claims exist.

Expected:

- Associated claims invalidate.
- Affected enemies recover gracefully.
- Other enemies do not preserve ghost occupancy forever.

### Setup 3: Small vs Large Soft Radius

Toggle reservation spacing.

Expected:

- Small radius produces denser landings.
- Large radius produces obvious distribution.

---

## Most Useful Tuning Levers

```text
SoftClaimRadius
PredictedCrowdWeight
TargetDiversity
```

---

## Failure Signs

- All agents prefer one perch.
- Oscillatory mass migration between perches.
- Claim cleanup leaks.
- Performance degrades dramatically with group size.

---

# 20. Variant 18 - Stagger Pack

## Purpose

Showcase local action pressure and individual rhythms.

Every enemy should be otherwise nearly identical.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

DecisionIntervalVariance = moderate
ActionHesitationVariance = moderate

ActionStaggerStrength = high
RecentActionWindow = noticeable

JumpActionPenalty = high
RelocationActionPenalty = high
```

---

## Best Sandbox Setups

### Setup 1: Shared Aggro Trigger

Spawn 12 enemies in a line.

Have player simultaneously enter everyone's detection range.

Expected:

- They notice in a loose cascade.
- Movement does not begin on exactly one frame.

### Setup 2: Shared Jump Opportunity

Put player on a nearby elevated platform.

Expected:

- A few enemies jump.
- Others hesitate.
- Subsequent jumps follow naturally.

### Setup 3: Disable Staggering

Compare:

```text
ActionStaggerStrength = 0
ActionStaggerStrength = high
```

This should produce a visually dramatic difference.

---

## Most Useful Tuning Levers

```text
ActionStaggerStrength
RecentActionWindow
DecisionIntervalVariance
```

---

## Failure Signs

- Lockstep jumping.
- Artificially rigid one-at-a-time queueing.
- Stagger penalty permanently suppresses action.
- Large groups become unresponsive.

---

# 21. Variant 19 - Alert Chain

## Purpose

Showcase group aggro propagation.

The visual target is a cascading "wake-up" effect.

---

## Suggested Profile

```text
CanWalk = true

DirectDetectionRange = short

AggroPropagationEnabled = true
AggroPropagationRadius = medium

AggroPropagationDelay = moderate
AggroPropagationDelayVariance = moderate
```

---

## Best Sandbox Setups

### Setup 1: Enemy Line

Place enemies in a long chain, each within propagation range of the next.

Player approaches one end.

Expected:

```text
A notices
then B
then C
then D
```

### Setup 2: Broken Chain

Leave one spacing gap larger than propagation radius.

Expected:

- Alert wave stops at the gap unless another enemy independently detects the player.

### Setup 3: Dense Cluster

Spawn many enemies together.

Expected:

- Propagation is staggered.
- Not every enemy wakes on the exact same frame.

---

## Most Useful Tuning Levers

```text
AggroPropagationRadius
AggroPropagationDelay
AggroPropagationDelayVariance
```

---

## Failure Signs

- Entire scene instantly aggroes.
- Propagation crosses beyond configured radius.
- Recursive propagation repeatedly resets reaction timers.

---

# 22. Variant 20 - Pack Hunter

## Purpose

Showcase optional group-level bias without centralized control.

The group should spread around the player and use several surfaces/routes.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

GroupEnabled = true
CoordinationStrength = high

PreferredGroupSpread = medium
GroupCohesion = moderate

SurfaceDiversityPreference = high
TargetDiversity = high

PreferredRelocatingFraction = low/moderate
```

---

## Best Sandbox Setups

### Setup 1: Player in Center of Coordination Bowl

Spawn 8-12 pack hunters from one side.

Expected:

- They do not all choose the shortest identical route.
- Some take alternate ledges/surfaces.
- Group remains generally cohesive.

### Setup 2: Player Crosses Arena

Move rapidly from one side to the other.

Expected:

- Group reorients.
- Individual commitments remain smooth.
- Members do not all replan on one frame.

### Setup 3: Split Arena

Put an obstacle through the center with two routes.

Expected:

- Group can divide between routes while continuing toward common target.

---

## Most Useful Tuning Levers

```text
CoordinationStrength
TargetDiversity
GroupCohesion
```

---

## Failure Signs

- Group acts like one rigid blob.
- Group spreads so much that individuals abandon useful pursuit.
- Central group state appears to issue synchronized commands.
- Every agent still chooses identical targets.

---

# 23. Variant 21 - Horde

## Purpose

Provide a useful contrast against the coordinated pack.

This variant intentionally uses weak coordination.

It is important to verify that the architecture can produce messy, dense behavior rather than forcing every enemy into elegant spacing.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

CoordinationStrength = low

PersonalSpace = low
SoftClaimRadius = small
ActionStaggerStrength = low

TargetDiversity = low
CrowdEscapeWeight = low
```

---

## Best Sandbox Setups

### Setup 1: Direct Rush

Spawn 20 enemies opposite player.

Expected:

- Dense pursuit.
- Some overlapping intentions.
- Still no actual invalid physical overlap.

### Setup 2: Compare Against Pack Hunter

Use identical geometry and enemy count.

The difference should be immediately visible.

---

## Most Useful Tuning Levers

```text
PersonalSpace
ActionStaggerStrength
TargetDiversity
```

---

## Failure Signs

- Horde still spreads into perfect tactical formation.
- Low coordination causes physical overlap.
- Claim system is so strict that horde cannot feel dense.

---

# 24. Variant 22 - Synchronized Drone

## Purpose

Test the opposite artistic extreme.

The movement system should be capable of deliberately synchronized enemies when desired.

This verifies that anti-synchronization behavior is configurable rather than hard-coded.

---

## Suggested Profile

```text
DecisionIntervalVariance = near zero
ActionHesitationVariance = near zero

ActionStaggerStrength = zero

SharedDecisionBias = high if available

PersonalSpace = moderate
```

---

## Best Sandbox Setups

### Setup 1: Identical Jump

Place several drones on identical positions/routes.

Trigger aggro simultaneously.

Expected:

- They may jump or turn together intentionally.

### Setup 2: Compare With Stagger Pack

Run the same geometry.

The contrast validates that staggering is a tunable style choice.

---

## Most Useful Tuning Levers

```text
ActionStaggerStrength
DecisionIntervalVariance
ActionHesitationVariance
```

---

## Failure Signs

- They still stagger heavily because anti-synchronization cannot be disabled.
- Synchronization causes reservation conflicts that bypass hard safety rules.

Important:

Even intentionally synchronized enemies must still respect:

```text
hard collision
hard landing reservations
surface legality
```

Synchronization should affect timing, not invalidate safety constraints.

---

# 25. Variant 23 - Commitment Tester

## Purpose

Specifically test retarget hysteresis and action commitment.

This enemy should be placed in situations where two destinations repeatedly trade places as the "best" option.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

CommitmentTime = high
RetargetThreshold = moderate/high

DecisionInterval = short
```

---

## Best Sandbox Setups

### Setup 1: Player Oscillation

Place two symmetric platforms.

Have player move slightly left/right.

Expected:

- Enemy chooses one.
- Small player movement does not constantly change target.

### Setup 2: Dramatic Target Change

After enemy commits, move player far enough that the alternate destination becomes substantially better.

Expected:

- Before movement begins, retarget may occur if improvement exceeds threshold.
- During committed jump, target should remain stable.

### Setup 3: Crowd Score Fluctuation

Have nearby enemies enter/leave the candidate regions.

Expected:

- Small crowd-score changes do not produce oscillation.

---

## Most Useful Tuning Levers

```text
CommitmentTime
RetargetThreshold
StayPutBias
```

---

## Failure Signs

- Target flicker.
- Repeated jump windup cancellation.
- Staying committed to clearly invalid targets.
- Never responding to major environmental changes.

---

# 26. Variant 24 - Intermediate-Route Flyer

## Purpose

This is the dedicated test for non-naive flight planning.

The enemy should be physically unable to fly directly to the player but capable of reaching the player through intermediate surfaces.

---

## Suggested Profile

```text
CanWalk = true
CanFly = true

MaxClimbAngle = low/moderate
MaxFlightTime = moderate

PerchProgressWeight = very high
DirectPlayerWeight = moderate
LandingSearchRadius = large

LandingCommitment = high
```

---

## Best Sandbox Setups

### Setup 1: Stair-Step Vertical Route

Place enemy at bottom, player at top.

Direct route violates climb angle.

Provide:

```text
low perch
medium perch
high perch
player
```

Expected:

- Enemy deliberately climbs through intermediate stops.

### Setup 2: Remove Middle Perch

Expected:

- Enemy realizes route is no longer feasible.
- It does not hover under the player indefinitely.

### Setup 3: Alternate Two-Route Tower

Provide a short crowded route and a longer empty route.

Expected:

- Crowd/reservations can influence route choice.

---

## Most Useful Tuning Levers

```text
MaxClimbAngle
PerchProgressWeight
LandingSearchRadius
```

---

## Failure Signs

- Direct missile behavior.
- Hovering against climb restriction.
- Failure to recognize useful intermediate surfaces.
- Cycling indefinitely among low-value perches.

---

# 27. Variant 25 - Mixed-Mobility Creature

## Purpose

Test transitions between multiple locomotion modes in one enemy.

This is intentionally more complex and should be built only after walking, jumping, and flying work independently.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true
CanFly = true

JumpRange = short
FlightRange = longer

JumpCooldown = short
FlightCooldown = long

CrowdEscapeWeight = moderate
PlayerProgressWeight = high
```

Expected interpretation:

- Walk whenever possible.
- Jump short gaps.
- Use flight only when it provides substantial value.
- Land and resume crawling.

---

## Best Sandbox Setups

### Setup 1: Mixed Traversal Course

Build:

```text
walkable floor
small gap
wall
larger gap
high ledge
moving platform
```

Expected:

- Walking handles ordinary movement.
- Jump handles small gaps.
- Flight handles larger traversal.
- Enemy returns to attached movement afterward.

### Setup 2: Flight Cooldown Challenge

Place two large gaps close together.

Expected:

- Enemy may fly across first.
- It cannot immediately fly again.
- It must use available walking/jumping/perch route or wait appropriately.

### Setup 3: Crowded Alternate Route

Make direct walking route crowded and flight route open.

Expected:

- Depending on weights, flight may become desirable as crowd avoidance.

---

## Most Useful Tuning Levers

```text
JumpRange
FlightCooldown
TraversalDifficultyCost
```

---

## Failure Signs

- Always choosing flight because it dominates all other locomotion.
- Repeated mode switching.
- Jump and flight planners fighting over the same target.
- Cooldowns being ignored by destination scoring.

---

# 28. Variant 26 - Large Swarm Stress Test

## Purpose

This is primarily a performance and scaling test.

It should use a simple enemy profile so CPU cost comes mostly from coordination and crowd systems.

---

## Suggested Profile

```text
CanWalk = true
CanJump = true

Moderate crowding
Moderate claims
Moderate stagger

MaxNeighbors = deliberately bounded
DecisionInterval = moderate
```

Spawn counts:

```text
25
50
100
possibly 200 if practical
```

---

## Best Sandbox Setups

### Setup 1: Wide Floor

Measure baseline neighbor-query cost.

### Setup 2: Dense Tunnel

Worst-case local density.

### Setup 3: Multiple Separated Clusters

Spawn several distant groups.

Expected:

- Each group mostly reasons locally.
- Cost should not behave like full all-to-all interaction.

---

## Most Useful Tuning Levers

```text
MaxNeighbors
CoordinationRadius
DecisionInterval
```

---

## Failure Signs

- Frame cost approximately quadratic in total enemy count.
- Distant clusters affect each other's crowd calculations.
- Dense clusters produce large frame spikes.
- Every agent replans simultaneously.

---

# 29. Recommended Sandbox Preset Matrix

The following presets provide broad coverage with relatively little duplication.

| Variant | Walk | Jump | Fly | Aggro | Crowd | Claims | Stagger | Groups | Primary Test |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Basic Walker | Yes | No | No | Basic | Light | No | Light | No | Baseline movement |
| Edge Runner | Yes | Optional | No | Basic | No | No | No | No | Edge policies |
| Wall Crawler | Yes | No | No | Basic | Light | No | Light | No | Surface traversal |
| Shy Scout | Yes | No | No | Advanced | Light | No | Yes | No | FOV/LOS |
| Surface Sentinel | Yes | Optional | No | Surface | Light | Optional | Yes | No | Surface aggro |
| Burst Pursuer | Yes | No | No | Curve | Light | No | Yes | No | Aggro speed |
| Basic Hopper | Yes | Yes | No | Basic | Light | Yes | Light | No | Jumping |
| Moving-Platform Hopper | Yes | Yes | No | Basic | Light | Yes | Light | No | Moving targets |
| Perch Claimer | Yes | Yes | No | Basic | Medium | Heavy | Medium | No | Reservations |
| Personal-Space Walker | Yes | No | No | Optional | Heavy | No | Medium | No | Separation |
| Patient Declumper | Yes | Yes | No | Optional | Heavy | Yes | Heavy | No | Predictive crowd |
| Ceiling Redistributor | Yes | Yes | No | Basic | Heavy | Yes | Medium | No | Surface diversity |
| Balloon Drifter | Yes | No | Yes | Basic | Medium | Yes | Medium | No | Floaty flight |
| Swooper | Yes | No | Yes | Basic | Medium | Yes | Medium | No | Perch/swoop |
| Glider | Yes | No | Yes | Basic | Light | Yes | Medium | No | Flight restrictions |
| Launch Bug | Yes | No | Yes | Basic | Medium | Yes | Heavy | No | Committed launch |
| Reservation Flock | Yes | Yes | Yes | Basic | Heavy | Heavy | Heavy | Optional | Multi-agent claims |
| Stagger Pack | Yes | Yes | No | Basic | Medium | Yes | Heavy | Optional | Action staggering |
| Alert Chain | Yes | No | No | Propagated | Light | No | Heavy | Yes | Aggro propagation |
| Pack Hunter | Yes | Yes | Optional | Basic | Heavy | Heavy | Heavy | Yes | Group biases |
| Horde | Yes | Yes | Optional | Basic | Low | Light | Low | Yes | Low coordination |
| Synchronized Drone | Yes | Yes | Optional | Basic | Medium | Yes | None | Optional | Intentional sync |
| Commitment Tester | Yes | Yes | No | Basic | Medium | Yes | Medium | No | Hysteresis |
| Intermediate Flyer | Yes | No | Yes | Basic | Medium | Yes | Medium | No | Multi-stage flight |
| Mixed-Mobility Creature | Yes | Yes | Yes | Basic | Medium | Yes | Medium | Optional | Mode selection |
| Large Swarm Stress Test | Yes | Yes | No | Basic | Medium | Medium | Medium | Optional | Scaling |

---

# 30. Minimum Initial Sandbox Set

Claude does not need to implement all variants immediately.

A strong first-pass sandbox should include these ten:

```text
1. Basic Walker
2. Wall Crawler
3. Shy Scout
4. Basic Hopper
5. Moving-Platform Hopper
6. Perch Claimer
7. Personal-Space Walker
8. Ceiling Redistributor
9. Balloon Drifter
10. Swooper
```

Then add:

```text
11. Patient Declumper
12. Stagger Pack
13. Pack Hunter
14. Intermediate-Route Flyer
15. Large Swarm Stress Test
```

once the shared systems are stable.

---

# 31. Recommended Scene Organization

One possible Unity hierarchy:

```text
EnemyMovementSandbox
|
+-- Player
|
+-- SandboxManager
|
+-- UI
|   +-- VariantSelector
|   +-- SpawnControls
|   +-- RuntimeTuningPanel
|   +-- DebugTogglePanel
|
+-- Arenas
|   +-- SurfaceCourse
|   +-- PlatformGarden
|   +-- CrowdTunnel
|   +-- FlightChamber
|   +-- CoordinationBowl
|   +-- MovingPlatformCourse
|
+-- EnemySpawnGroups
|
+-- SharedSystems
    +-- EnemySpatialRegistry
    +-- EnemyCoordinationSystem
    +-- EnemyGroupManager
```

---

# 32. Recommended Sandbox Controls

The user should be able to:

```text
Select enemy preset
Select arena
Spawn 1 enemy
Spawn 5 enemies
Spawn 10 enemies
Spawn 25 enemies
Clear enemies
Reset arena
Teleport player
Toggle aggro
Pause AI
Advance one decision tick
Set time scale
Toggle debug overlays
```

If practical, add:

```text
Random seed input
```

so behaviors can be reproduced exactly.

---

# 33. Debug Overlay Modes

Provide independent toggles where practical.

Recommended:

```text
Surface normals
Legal/illegal surfaces
Current surface
Current locomotion state

Perception range
Field of view
Line-of-sight ray

Personal space
Separation vector
Crowd pressure

Candidate destinations
Candidate score labels
Selected destination

Hard claims
Soft claims
Predicted occupancy

Recent jump events
Recent flight events
Recent relocation events

Group center
Group membership
Group target
```

Do not require all overlays to be visible simultaneously.

---

# 34. Selected-Enemy Inspector

Clicking/selecting an enemy in the sandbox should ideally display:

```text
Variant name

Intent
Locomotion state

Current surface
Current target

Aggro state
Aggro time

Crowd pressure
Personal space radius

Current candidate score
Selected candidate score

Jump cooldown
Flight cooldown

Current claim
Claim expiry

Recent action pressure

Group membership
Group airborne count
```

This can initially be editor/debug UI rather than polished game UI.

---

# 35. Regression Test Checklist

When changes are made to the movement architecture, quickly re-check:

## Surface

```text
Basic Walker
Wall Crawler
Edge Runner
```

## Perception

```text
Shy Scout
Surface Sentinel
Burst Pursuer
```

## Jumping

```text
Basic Hopper
Moving-Platform Hopper
Perch Claimer
```

## Crowding

```text
Personal-Space Walker
Patient Declumper
Ceiling Redistributor
```

## Flight

```text
Balloon Drifter
Swooper
Glider
Intermediate-Route Flyer
```

## Coordination

```text
Reservation Flock
Stagger Pack
Alert Chain
Pack Hunter
Horde
Synchronized Drone
```

## Stability

```text
Commitment Tester
Mixed-Mobility Creature
Large Swarm Stress Test
```

---

# 36. What Success Should Look Like

The sandbox should make it possible to demonstrate all of the following without writing one-off behavior scripts:

### Walking

- Enemy moves naturally along legal surfaces.
- Illegal slopes and edges behave according to configuration.

### Surface traversal

- Some enemies remain floor-bound.
- Some can traverse walls.
- Some can crawl around full objects and ceilings.

### Aggro

- Enemies can use range, FOV, LOS, and same-surface rules.
- Acquisition and retention can differ.
- Reactions can be delayed or propagated.

### Jumping

- Enemies choose reachable surfaces.
- Enemies can jump to moving platforms.
- Multiple enemies avoid selecting identical landing positions.

### Crowding

- Enemies avoid overlap.
- Personal space is imperfect and natural.
- Dense clusters gradually redistribute.
- Enemies account for others already planning to leave.

### Surface diversity

- Crowded floor enemies can choose walls/ceilings when legal.
- Redistribution is useful but not hyperactive.

### Flight

- Balloon-like enemies drift.
- Bird-like enemies swoop and perch.
- Gliders respect altitude constraints.
- Launch creatures commit to fast straight travel.
- Restricted flyers use intermediate surfaces instead of repeatedly attempting impossible direct paths.

### Coordination

- Claims reduce destination conflicts.
- Major actions are staggered.
- Predicted occupancy reduces mass overreaction.
- Groups can bias individual decisions without directly controlling them.

### Style flexibility

The same architecture can produce:

```text
disciplined pack
messy horde
staggered swarm
synchronized group
floor crawler
ceiling spider
balloon
bird
glider
launcher
```

primarily through configuration.

---

# 37. Final Recommendation

The sandbox should not be treated as disposable debug content.

It should become a permanent movement-development scene that can be reopened whenever:

- A new enemy is being designed.
- A shared locomotion system changes.
- A crowding bug appears.
- A new surface type is introduced.
- A moving-platform issue appears.
- Performance changes.
- A designer wants to discover a new enemy movement style.

The most important implementation rule is:

> **Every test variant should be built from the same shared movement architecture that production enemies will use.**

If a variant requires custom code, first determine whether the shared architecture is missing a generally useful capability.

The purpose of these variants is not simply to prove that the architecture works. They should also act as a palette of movement behaviors from which future enemies can be designed.
