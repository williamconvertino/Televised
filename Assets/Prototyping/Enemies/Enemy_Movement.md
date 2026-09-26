# Enemy Movement Architecture

## Purpose

This document specifies the prototype architecture for enemy movement, navigation, coordination, and locomotion in the game.

The goal is **not** to define individual enemy archetypes yet. Instead, the system should provide a flexible set of shared movement primitives that can be configured and combined to create many enemy behaviors.

The system should support enemies that can:

- Walk along surfaces.
- Respect configurable legal surface angles.
- Stop, turn around, fall, or attempt another traversal when reaching an invalid surface or edge.
- Idle and wander.
- Aggro based on configurable perception rules.
- Change speed dynamically after aggro.
- Jump between valid surfaces.
- Jump onto moving surfaces.
- Temporarily fly between surfaces.
- Support balloon-like, bird-like, gliding, swooping, and near-ballistic "long jump" movement.
- Avoid overlapping and excessive clumping.
- Spread across floors, walls, and ceilings when appropriate.
- Reserve intended landing positions so multiple enemies do not choose the same target.
- Stagger jumps, flights, relocations, and aggro responses so groups do not move in unnatural lockstep.
- Coordinate locally without every enemy communicating directly with every other enemy.
- Optionally participate in lightweight enemy groups/swarms that influence behavior without fully controlling individual units.

The desired result is a system where enemies appear to behave as independent creatures while still exhibiting coordinated, aesthetically pleasing movement.

---

# 1. Core Design Principles

## 1.1 Separate Intent, Navigation, and Locomotion

Enemy movement should be divided into three conceptual layers.

### Intent

The enemy decides what it wants.

Examples:

- Idle.
- Wander.
- Approach the player.
- Maintain distance.
- Move to a less crowded region.
- Find a better surface.
- Reach a perch.
- Land.
- Escape congestion.

Intent should not directly control physics.

### Navigation

The navigation layer decides **where the enemy would like to go**.

Examples:

- Move left along the current surface.
- Jump to the ceiling.
- Land on the ledge near the player.
- Move away from a dense enemy cluster.
- Use an intermediate perch before reaching a higher platform.

Navigation should evaluate candidate positions and surfaces.

### Locomotion

The locomotion layer determines **how the enemy physically reaches the chosen target**.

Examples:

- Walk.
- Jump.
- Fly.
- Fall.
- Attach to a new surface.

This separation is critical. Do not create a separate monolithic AI script for every locomotion combination such as:

```text
WalkingChaserEnemy
JumpingChaserEnemy
FlyingChaserEnemy
WalkingFlyingWanderEnemy
```

Instead, enemies should be configured from reusable capabilities.

---

## 1.2 Keep Physical Locomotion State Separate From Behavioral Intent

A small locomotion state machine is desirable.

Recommended locomotion states:

```text
Attached
Jumping
Flying
Falling
Landing
```

Optional transient states may include:

```text
JumpWindup
Takeoff
LandingRecovery
Stunned
```

Behavioral intent should be represented separately.

Example:

```text
Intent: ChasePlayer
Locomotion: Attached
```

then:

```text
Intent: ChasePlayer
Locomotion: Jumping
```

then:

```text
Intent: ChasePlayer
Locomotion: Attached
```

Avoid combinatorial states such as:

```text
IdleAttached
IdleFlying
WanderAttached
WanderFlying
ChaseAttached
ChaseJumping
ChaseFlying
```

---

## 1.3 Enemies Should Prefer Positions, Not Just Directions

The enemy AI should not reduce every decision to:

```text
Move toward player.
```

Instead, enemies should continuously evaluate:

> Which reachable positions are desirable?

A candidate destination may be desirable because it:

- Brings the enemy closer to the player.
- Produces a good attack angle.
- Reduces crowding.
- Places the enemy on an underused surface.
- Matches an enemy's preferred surface type.
- Requires little movement cost.
- Avoids a claimed destination.
- Preserves group cohesion.
- Creates visual variety.

This should produce emergent behavior such as:

- Some enemies remaining on the floor.
- Some crawling onto walls.
- Some jumping to ceilings.
- Some moving around obstacles.
- Flying enemies using intermediate perches instead of repeatedly attempting impossible direct approaches.

---

## 1.4 Favor Soft Preferences Over Hard Rules

Where possible, movement should be based on weighted scoring rather than binary commands.

Prefer:

```text
+3 penalty for landing near another enemy
```

over:

```text
Never land within 2 meters of another enemy
```

Hard constraints should be reserved for cases that are truly invalid:

- Surface cannot legally support the enemy.
- Landing point is obstructed.
- Jump cannot physically reach destination.
- Destination falls inside a hard reservation radius.
- Enemy would overlap another collider.
- Locomotion capability is unavailable.

Soft preferences create more natural behavior and allow occasional dense or chaotic formations.

---

## 1.5 Use Commitment and Hysteresis

Enemies should not constantly change their minds.

Once an enemy chooses a movement action or target, it should generally commit until:

- The target becomes invalid.
- The action fails.
- A timeout occurs.
- A substantially better target appears.
- The enemy receives an external interruption.

A candidate that is only slightly better should not cause retargeting.

Recommended concept:

```text
SwitchTargetOnlyIf:
    NewScore >= CurrentScore + RetargetThreshold
```

This prevents jitter such as:

```text
floor
ceiling
floor
ceiling
```

and:

```text
left
right
left
right
```

---

# 2. Recommended High-Level Components

A typical enemy should be composed of modular components rather than one large controller.

Recommended conceptual components:

```text
EnemyBrain
EnemyPerception
EnemyLocomotionController
SurfaceMotor
JumpMotor
FlightMotor
SurfaceQuery
DestinationEvaluator
CrowdAgent
EnemyCoordinationAgent
EnemyGroupMember (optional)
EnemyDebugView
```

A shared scene-level system should provide:

```text
EnemyCoordinationSystem
EnemySpatialRegistry
EnemyGroupManager (optional)
```

These names are suggestions. The exact Unity class structure can differ, but the responsibilities should remain separated.

---

# 3. Enemy Configuration

Use data-driven configuration wherever possible.

A `ScriptableObject`-based configuration is recommended for prototype iteration.

Possible layout:

```text
EnemyMovementConfig
    SurfaceMovementSettings
    PerceptionSettings
    JumpSettings
    FlightSettings
    CrowdSettings
    CoordinationSettings
    GroupSettings
    DecisionSettings
```

Individual enemies should be able to disable entire locomotion systems.

Example:

```text
CanWalk = true
CanJump = true
CanFly = false
```

A flying creature may use:

```text
CanWalk = true
CanJump = false
CanFly = true
```

A long-leaping creature might use:

```text
CanWalk = true
CanJump = false
CanFly = true
Flight profile configured as short, straight, highly committed movement.
```

---

# 4. Shared Surface Representation

Walking, jumping, landing, flying, crowding, and reservations should all use the same conceptual surface representation.

Each valid attachment/landing location should provide:

```text
SurfaceCandidate
{
    SurfaceReference surface;
    Vector2 worldPosition;
    Vector2 localPosition;
    Vector2 surfaceNormal;
    Vector2 tangent;

    float distanceFromEnemy;
    float distanceFromPlayer;

    float crowdDensity;
    float predictedCrowdDensity;

    bool validForAttachment;
    bool validForJump;
    bool validForFlightLanding;

    float traversalDifficulty;
    float reservationPenalty;
    float surfacePreferenceScore;

    // Optional:
    int surfaceRegionId;
    float estimatedArrivalTime;
}
```

For moving platforms, always retain:

```text
surface reference + local position
```

rather than only a world-space target.

The world position can then be reconstructed every frame.

---

# 5. Surface Classification and Legality

Each enemy needs a configurable definition of what constitutes a legal attached surface.

Recommended settings:

```text
MaxSurfaceAngle
MaxCornerTraversalAngle
```

`MaxSurfaceAngle` defines which surface normals are legal.

Examples:

```text
0-45 degrees:
    Floor-only enemy.

0-100 degrees:
    Can handle steep ramps and some walls.

0-180 degrees:
    Can crawl around complete geometry including ceilings.
```

The exact angle convention should match the player movement implementation where possible.

A separate `MaxCornerTraversalAngle` is useful because:

- An enemy might be allowed to stand on walls.
- But it may not be able to smoothly wrap around an extremely sharp convex corner.

If the player movement system already has robust surface traversal logic, reuse or extract its surface-normal validation logic rather than independently reimplementing it.

---

# 6. Surface Motor

The `SurfaceMotor` handles all movement while attached.

It should maintain:

```text
CurrentSurface
CurrentAttachmentPoint
CurrentAttachmentLocalPoint
CurrentSurfaceNormal
CurrentSurfaceTangent
CurrentFacingDirection
CurrentSurfaceVelocity
```

If the surface is moving, the enemy should inherit or account for surface movement.

The motor should receive a desired tangent movement value such as:

```text
DesiredSurfaceVelocity
```

rather than deciding why the enemy wants to move.

---

## 6.1 Edge Behavior

When an attached enemy reaches a position it cannot legally traverse, expose a configurable edge policy.

Recommended options:

```text
Stop
TurnAround
Fall
AttemptTraversal
```

`AttemptTraversal` means:

1. Ask the navigation system for an alternate reachable surface.
2. Attempt a jump or flight if allowed.
3. If no valid traversal exists, fall back to another configured behavior.

This lets one enemy stop at ledges while another naturally hops gaps.

---

## 6.2 Surface Movement Tuning Parameters

Recommended levers:

| Parameter | Purpose |
|---|---|
| Walk Speed | Base attached speed |
| Acceleration | Time to reach desired velocity |
| Deceleration | Time to stop |
| Turn Responsiveness | How quickly direction reverses |
| Max Surface Angle | Legal attached orientation |
| Max Corner Angle | Sharpest traversable corner |
| Edge Behavior | Stop / turn / fall / attempt traversal |
| Surface Stick Strength | Resistance to accidental detachment |
| Wander Speed Multiplier | Speed while wandering |
| Aggro Speed Curve | Speed multiplier versus time since aggro |
| Target Overshoot | Allows intentionally aggressive movement past ideal point |
| Minimum Move Duration | Avoid tiny movement pulses |
| Surface Reattach Delay | Prevent immediate attach/detach loops |

---

# 7. Enemy Brain

The brain should produce high-level goals.

Recommended basic intent states:

```text
Idle
Wander
Pursue
Reposition
Retreat
```

The prototype may only need:

```text
Idle
Wander
Pursue
Reposition
```

The brain should output information such as:

```text
DesiredTarget
DesiredRange
Urgency
PreferredMovementMode
```

The brain should not directly apply velocity.

---

# 8. Idle and Wander Behavior

Wandering should deliberately include stochastic timing.

Recommended settings:

```text
IdleChance
IdleDurationMin
IdleDurationMax

WanderDurationMin
WanderDurationMax
WanderDirectionChangeMin
WanderDirectionChangeMax

WanderSpeedMultiplier

PreferredDistanceFromSpawn
ReturnToSpawnStrength
```

Avoid synchronized random updates.

Each enemy should sample its own timers.

Seeded randomness is recommended so behavior can be reproduced during debugging.

---

# 9. Perception and Aggro

Aggro acquisition and aggro retention should be separate systems.

Do not rely on one method such as:

```text
ShouldAggro()
```

Instead use:

```text
CanAcquireAggro()
ShouldRetainAggro()
```

This allows enemies that:

- Only notice the player when sharing a surface.
- Continue chasing after the player leaves the surface.
- Need line-of-sight to acquire but temporarily remember the player afterward.
- Detect the player in front but retain aggro in all directions.
- Wake up when the player touches a particular platform.

---

## 9.1 Perception Conditions

Potential conditions:

```text
Distance
FieldOfView
LineOfSight
360DegreeLineOfSight
SameSurfaceObject
SameSurfaceRegion
ConnectedSurface
PlayerAttached
```

Acquisition and retention should each independently compose these.

---

## 9.2 Same-Surface Semantics

Initially, define the simplest useful rule:

```text
SameSurfaceObject
```

This means the enemy and player are attached to the same logical surface/platform object.

Later, the system can optionally distinguish:

```text
SameSurfaceRegion
ConnectedSurfaceNetwork
```

Do not make this more complicated until needed.

---

## 9.3 Aggro Timing

Expose:

```text
AggroReactionDelay
AggroReactionDelayVariance
DeaggroDelay
MemoryTime
```

This allows cascading or imperfect reactions.

---

## 9.4 Aggro Speed Curves

Rather than one fixed aggro speed multiplier, use a time-based curve.

Example profiles:

### Burst chaser

```text
time 0: 3.0x
time 1: 1.6x
time 3: 1.0x
```

### Charger

```text
time 0: 0.5x
time 1: 0.5x
time 1.5: 3.0x
```

### Tiring pursuer

```text
time 0: 2.0x
time 2: 2.0x
time 5: 0.7x
```

Use a Unity `AnimationCurve` where practical.

---

# 10. Surface Candidate Query System

The `SurfaceQuery` system is one of the most important shared components.

Walking, jumping, flying, landing, and declumping should all use it.

Its responsibility is:

> Find plausible nearby legal surface destinations.

Potential query inputs:

```text
origin
search radius
allowed surface angle
maximum vertical offset
locomotion type
required clearance
preferred direction
preferred distance
```

Potential output:

```text
List<SurfaceCandidate>
```

Candidate generation should be separate from candidate scoring.

---

# 11. Candidate Destination Scoring

Each candidate receives a score.

Conceptually:

```text
Score =
    PlayerProgressWeight * PlayerProgress
  + TacticalPositionWeight * TacticalValue
  + SurfacePreferenceWeight * SurfacePreference
  + AlternateSurfaceWeight * SurfaceDiversity
  + RandomVariation
  - DistanceCost
  - TraversalDifficultyCost
  - CurrentCrowdPenalty
  - PredictedCrowdPenalty
  - ReservationPenalty
  - GroupSeparationPenalty
```

The exact terms should be configurable.

Not every enemy needs every term.

---

## 11.1 Player Progress

Useful metrics include:

```text
Reduction in distance to player
Reduction in path distance to player
Improvement in attack position
```

Do not assume Euclidean distance is always the best measure.

For the prototype, Euclidean distance improvement is acceptable if a full graph/path system does not yet exist.

---

## 11.2 Surface Preference

An enemy may prefer:

```text
Floor
Wall
Ceiling
Any
```

or simply use orientation curves.

For example:

```text
PreferredSurfaceNormalCurve
```

This can be used to make one enemy naturally favor ceilings while another strongly prefers floors.

---

## 11.3 Stay-Put Bias

Every destination system should include a configurable resistance to unnecessary relocation.

Example:

```text
CurrentPositionScore += StayPutBias
```

This is important because otherwise a perfectly rational scoring system may cause constant micro-repositioning.

---

# 12. Jump System

Jumping should be treated primarily as:

> Surface-to-surface traversal.

The jump planner:

1. Queries nearby candidate surfaces.
2. Filters impossible/illegal candidates.
3. Scores reachable candidates.
4. Selects a destination.
5. Requests a destination reservation.
6. Commits to the jump.
7. Executes movement.
8. Attaches on arrival.
9. Releases the reservation.

---

## 12.1 Jump Reachability

A jump candidate should be rejected if:

- Surface angle is illegal.
- Target is beyond jump range.
- Required vertical movement is beyond allowed height.
- Landing clearance is obstructed.
- A hard reservation blocks the destination.
- A valid trajectory cannot be produced.
- Target surface is otherwise forbidden.

---

## 12.2 Jumping to Moving Platforms

Do not lock the jump to a fixed world position.

Store:

```text
TargetSurface
TargetLocalPosition
```

Each frame:

```text
CurrentTargetWorldPosition =
    TargetSurface.TransformPoint(TargetLocalPosition)
```

For prototype reliability, favor a guided jump rather than a perfectly ballistic simulation.

Recommended behavior:

1. Begin with a believable jump impulse or arc.
2. Continue tracking the intended moving landing point.
3. Apply limited corrective acceleration.
4. Preserve the overall arc appearance.
5. Snap or attach only within a small landing tolerance.

Expose:

```text
AirControlStrength
TargetCorrectionStrength
MaxCorrectionAcceleration
```

This makes intentional jumps remain successful even when a platform moves after takeoff.

---

## 12.3 Jump Commitment

Once a jump begins:

- Do not retarget casually.
- Continue toward the reserved target.
- Retarget only if the target becomes invalid and emergency recovery is explicitly supported.

This prevents visibly unnatural midair indecision.

---

## 12.4 Jump Tuning Parameters

| Parameter | Purpose |
|---|---|
| Jump Range | Maximum reach |
| Jump Height | Maximum vertical gain |
| Jump Speed | Overall travel speed |
| Arc Bias | Flat versus high jump |
| Air Steering | Midair directional correction |
| Moving Target Correction | Tracking moving surfaces |
| Jump Cooldown | Frequency |
| Jump Windup | Telegraph duration |
| Windup Variance | Stagger identical enemies |
| Min Commitment Time | Prevent cancellation |
| Landing Claim Radius | Reserved landing area |
| Landing Soft Radius | Nearby discouraged area |
| Crowd Escape Weight | Jump willingness due to congestion |
| Player Progress Weight | Chase importance |
| Surface Preference Weight | Floor/wall/ceiling preference |
| Traversal Difficulty Cost | Preference for easy jumps |
| Landing Recovery Time | Delay after attachment |

---

# 13. Crowd and Dispersal System

Crowd behavior has two distinct responsibilities.

## 13.1 Hard Overlap Prevention

Enemies should not occupy physically overlapping positions.

This may use:

- Physics collision.
- Collider separation.
- Small emergency correction.
- Collision layers.

This is separate from aesthetic spacing.

---

## 13.2 Soft Personal Space

Enemies should prefer not to be overly close.

Each enemy should have a personal-space target.

At spawn, sample small persistent variations:

```text
PersonalSpaceRadius
CrowdingTolerance
CrowdPatience
```

Example:

```text
PersonalSpaceRadius = base 1.3 +/- 0.25
CrowdPatience = base 0.8 +/- 0.2
```

This prevents perfect lattice-like formations.

---

## 13.3 Surface-Projected Separation

For attached enemies:

1. Calculate a local separation vector from nearby agents.
2. Project the vector onto the current surface tangent.
3. Blend it with the desired movement vector.

Conceptually:

```text
DesiredMovement =
    PursuitVector
  + WanderVector
  + SeparationVector
```

with configurable weights.

The crowd system should not directly force every enemy away from every neighbor at all times.

---

## 13.4 Crowd Pressure

Each enemy should calculate a crowd pressure metric.

Inputs may include:

```text
Number of nearby enemies
Distance to neighbors
Personal-space violations
Surface region density
Predicted departures
Predicted arrivals
```

A simple normalized value is useful:

```text
CrowdPressure = 0..1
```

---

## 13.5 Crowd Patience

An enemy should not immediately relocate the first frame crowd pressure becomes high.

Use:

```text
CrowdPressureThreshold
CrowdPatience
CrowdPatienceVariance
```

Example:

```text
if CrowdPressure > threshold
    accumulate discomfort time

if discomfort time > CrowdPatience
    consider relocation
```

This produces delayed, staggered reactions.

---

# 14. Predictive Crowding

Crowd decisions should account for where enemies are **going**, not only where they currently are.

Suppose:

```text
Surface A:
    7 enemies currently

Surface B:
    1 enemy currently
```

but three enemies have already committed to leaving A and landing on B.

Future occupancy is approximately:

```text
Surface A: 4
Surface B: 4
```

A new agent should plan against the predicted distribution rather than concluding:

```text
Surface B is almost empty; everyone should go there.
```

The coordination system should therefore distinguish:

```text
CurrentOccupancy
IncomingReservations
OutgoingRelocationIntents
PredictedOccupancy
```

---

# 15. Declumping Relocation Intent

When an enemy decides to move specifically to relieve crowding, it should publish a relocation intention.

Conceptually:

```text
RelocationIntent
{
    enemyId
    originRegion
    destinationRegion
    estimatedDepartureTime
    estimatedArrivalTime
    expiryTime
}
```

Nearby enemies can then reduce their own urge to relocate because another agent is already addressing the congestion.

This avoids:

```text
Five enemies detect crowding.
All five jump away simultaneously.
```

Instead:

```text
Enemy A commits to relocation.
Enemy B sees predicted pressure decreasing and stays.
Enemy C still considers pressure too high and relocates later.
```

---

# 16. Encouraging Surface Diversity

Crowding should not only separate enemies locally.

It should also encourage use of alternate legal surfaces.

Example tunnel:

```text
=================== ceiling

      player

E E E E E E E
___________________ floor
```

For enemies capable of ceiling traversal, ceiling candidates should gain value because:

```text
floor density = high
ceiling density = low
```

A configurable `SurfaceDiversityWeight` or `AlternateSurfaceBonus` should influence candidate scoring.

This makes enemies surround the player organically without a special rule such as:

```text
if floorEnemyCount > 5:
    send one enemy to ceiling
```

---

# 17. Flight System

Flight should generally be treated as a temporary locomotion episode rather than unlimited free movement.

Recommended transition:

```text
Attached
    ->
Takeoff
    ->
Flying
    ->
Landing
    ->
Attached
```

Enemies may optionally fall if a landing fails.

---

## 17.1 Flight Duration

Expose:

```text
MinFlightTime
MaxFlightTime
FlightCooldown
```

This allows enemies that:

- Fly briefly between surfaces.
- Remain airborne for long periods.
- Must rest or crawl before flying again.

---

## 17.2 Direction Commitment

To avoid left/right flickering:

```text
MinHeadingCommitmentTime
TurnRate
RetargetThreshold
```

The enemy should not instantly reverse direction simply because the player's position changed slightly.

---

## 17.3 Flight Restrictions

Useful parameters include:

```text
MaxClimbAngle
MaxDiveAngle
TurnRate
Acceleration
MaxSpeed
PreferredAltitude
```

These restrictions define different flight characters.

---

# 18. Flight Targeting

A flying enemy should not simply point toward the player every frame.

Instead, it should ask:

> Which useful destination is reachable under my current flight restrictions?

A destination may be:

- The player.
- A surface near the player.
- An intermediate perch.
- A less crowded wall.
- A ceiling position.
- A safe landing spot before flight time expires.

---

## 18.1 Intermediate Perches

Example:

```text
                  PLAYER
                  _____

        ledge
        _____

enemy
_____
```

If the player is too high for the enemy's current climb restriction:

```text
enemy -> ledge -> player
```

is better than:

```text
enemy repeatedly flies upward, stalls, and oscillates
```

This should reuse the same `SurfaceQuery` and `SurfaceCandidate` architecture as jumping.

---

# 19. Flight Profiles

The same flight system should support multiple movement styles.

## 19.1 Balloon

Characteristics:

```text
Low acceleration
Low turn rate
Long flight time
Strong inertia
Can climb significantly
Loose player pursuit
Slow landing selection
```

---

## 19.2 Bird / Pterodactyl

Characteristics:

```text
High forward speed
Moderate/high acceleration
Limited turn rate
Limited climb angle
Meaningful heading commitment
Frequent perching
Swoop/glide behavior
```

Typical rhythm:

```text
perch -> takeoff -> swoop -> glide -> land
```

---

## 19.3 Glider

Characteristics:

```text
Low or zero upward capability
Strong downward/forward preference
Limited steering
Must reach progressively lower surfaces
```

---

## 19.4 Long-Jump / Launch Creature

Can use the flight framework with:

```text
Very short max flight time
High launch speed
Near-zero steering
High heading commitment
Strong landing requirement
```

Visually:

```text
launch -> straight/curved travel -> impact/landing
```

This avoids creating a completely separate locomotion implementation if unnecessary.

---

# 20. Flight Tuning Parameters

| Parameter | Purpose |
|---|---|
| Flight Speed | Base speed |
| Acceleration | Floaty versus responsive |
| Turn Rate | Maneuverability |
| Min Flight Time | Prevent immediate landing |
| Max Flight Time | Force eventual landing |
| Flight Cooldown | Required grounded interval |
| Min Heading Commitment | Prevent rapid reversal |
| Max Climb Angle | Limit upward movement |
| Max Dive Angle | Limit downward movement |
| Takeoff Windup | Telegraph |
| Takeoff Windup Variance | Stagger enemies |
| Preferred Flight Distance | Short hop versus long traversal |
| Landing Search Radius | Candidate discovery |
| Direct Player Weight | Importance of flying directly toward player |
| Perch Progress Weight | Importance of intermediate surfaces |
| Crowding Weight | Avoid dense landing areas |
| Airborne Crowd Weight | Avoid other flyers |
| Desired Altitude | Balloon-like behavior |
| Glide Bias | Favor preserving/downward motion |
| Landing Claim Radius | Destination reservation size |
| Landing Soft Radius | Surrounding spacing |
| Landing Commitment | Resistance to retargeting |

---

# 21. Inter-Enemy Coordination

Enemies should not maintain direct references to every other enemy.

Avoid all-to-all pair interactions.

Instead, create a shared:

```text
EnemyCoordinationSystem
```

backed by a spatially partitioned:

```text
EnemySpatialRegistry
```

Enemies publish small pieces of state.

Other enemies query only relevant nearby state.

---

# 22. Spatial Partitioning

Use a grid/spatial hash or equivalent local lookup structure.

Conceptually:

```text
Cell (3,4)
    Enemy 12
    Enemy 19
    LandingClaim 7

Cell (4,4)
    Enemy 24
    LandingClaim 8
```

Supported queries:

```text
GetNearbyAgents(position, radius)
GetNearbyClaims(position, radius)
GetNearbyActionEvents(position, radius)
GetCrowdDensity(surfaceRegion)
GetPredictedCrowdDensity(surfaceRegion)
```

Always cap expensive neighbor queries.

Example:

```text
MaxNeighborsConsidered = 12
```

There is usually little visible value in an enemy reasoning about dozens of distant enemies.

---

# 23. Destination Reservations

Before a jump or flight begins, the enemy should reserve its intended destination.

This prevents multiple enemies from independently choosing the same visually ideal spot.

Conceptual structure:

```text
DestinationClaim
{
    int ownerEnemyId;

    SurfaceReference surface;
    Vector2 localPosition;

    float hardRadius;
    float softRadius;

    float claimStrength;

    float expectedArrivalTime;
    float expectedReleaseTime;
    float expiryTime;

    LocomotionType locomotionType;
}
```

---

## 23.1 Hard Reservation Radius

Inside the hard radius:

```text
candidate is invalid
```

Use this to prevent direct overlap.

---

## 23.2 Soft Reservation Radius

Inside the soft radius:

```text
candidate remains legal
but receives a scoring penalty
```

Example:

```text
Landing point
    hard radius: 0.4 m
    soft radius: 1.5 m
```

This permits clustering without identical landing positions.

---

## 23.3 Reservation Lifetime

Claims must automatically expire.

Reasons include:

- Enemy dies.
- Enemy is interrupted.
- Enemy never begins movement.
- Destination becomes invalid.
- Travel takes longer than expected.

Recommended:

```text
expiry =
    estimatedStart
  + estimatedTravelTime
  + landingGracePeriod
```

Upon successful arrival:

```text
DestinationClaim
    ->
actual physical occupancy
```

The claim should then be removed.

---

## 23.4 Space-Time Reservations

The initial prototype may treat reservations purely spatially.

However, the structure should support arrival time.

Example:

```text
Enemy A reaches point X in 0.5 seconds.
Enemy B reaches point X in 4 seconds.
```

These may eventually be allowed to share a destination because their occupancy times do not conflict.

This does not need to be implemented initially, but `expectedArrivalTime` and `expectedReleaseTime` should be included if inexpensive.

---

# 24. Action Staggering

Enemies should not all:

- Jump.
- Fly.
- Take off.
- Relocate.
- Acquire aggro.
- Change direction.

on exactly the same frame.

Random delay alone is helpful but insufficient.

Use both:

1. Persistent per-enemy timing variation.
2. Local action-pressure signals.

---

# 25. Persistent Individual Rhythm

At spawn, sample stable timing/personality modifiers.

Examples:

```text
DecisionInterval
ActionHesitation
CrowdPatience
MovementBoldness
PersonalSpace
```

Example enemy A:

```text
DecisionInterval = 0.18
ActionHesitation = 0.12
CrowdPatience = 0.7
MovementBoldness = 1.15
```

Enemy B:

```text
DecisionInterval = 0.31
ActionHesitation = 0.40
CrowdPatience = 1.30
MovementBoldness = 0.80
```

These values should remain reasonably stable during the enemy's lifetime.

This prevents identical agents from behaving identically.

---

# 26. Local Action Pressure

When an enemy performs a visually major action, publish an action event.

Possible event types:

```text
JumpStarted
FlightStarted
RelocationStarted
AggroAcquired
LandingStarted
```

Nearby enemies query recent events.

Example:

```text
base JumpDesire = 0.85

three nearby jump starts occurred recently

ActionPressurePenalty = 0.40

effective JumpDesire = 0.45
```

As the recent action window expires:

```text
effective JumpDesire -> 0.85
```

This produces naturally staggered waves.

---

# 27. Soft Action Pressure vs Hard Quotas

Prefer:

```text
JumpPenaltyPerNearbyJump = 0.25
```

over:

```text
MaximumJumpers = 2
```

Soft pressure creates natural variation.

However, optional hard limits may be valuable for specific enemies.

Examples:

```text
MaxSimultaneousJumpers
MaxSimultaneousFlyers
```

These should be opt-in rather than the default coordination method.

---

# 28. Coordination Tuning Parameters

| Parameter | Purpose |
|---|---|
| Coordination Radius | Range of local coordination |
| Max Neighbors | CPU/perception cap |
| Hard Claim Radius | Exclusive landing zone |
| Soft Claim Radius | Discouraged landing zone |
| Claim Penalty | Soft avoidance strength |
| Claim Timeout | Stale claim safety |
| Action Stagger Strength | Avoid simultaneous actions |
| Action Stagger Radius | Local stagger neighborhood |
| Recent Action Window | Duration of action pressure |
| Jump Action Penalty | Suppression from nearby jumps |
| Flight Action Penalty | Suppression from nearby takeoffs |
| Relocation Action Penalty | Suppression from nearby declumping |
| Max Simultaneous Jumps | Optional hard limit |
| Max Simultaneous Flyers | Optional hard limit |
| Decision Interval | Planning frequency |
| Decision Interval Variance | Desynchronization |
| Commitment Time | Minimum action commitment |
| Retarget Threshold | Required improvement before switching |

---

# 29. Optional Enemy Groups / Swarms

Do not make the group the primary controller for normal enemies.

Individual enemies should remain independent agents.

An optional group should provide:

```text
shared context
shared biases
shared aggregate state
```

rather than issuing detailed commands.

---

# 30. Enemy Group Data

Possible group summary:

```text
EnemyGroupState
{
    Vector2 center;
    Vector2 averageVelocity;

    Transform currentTarget;

    int memberCount;
    int airborneCount;
    int jumpingCount;
    int relocatingCount;

    float averageCrowding;
    float currentSpread;

    Dictionary<SurfaceRegion, int> occupancy;
}
```

Individual enemies may query this state.

---

# 31. Group-Level Bias Examples

An enemy could reason:

```text
5 of 8 group members are airborne.
My flight desirability should decrease.
```

or:

```text
Almost everyone is on the floor.
Ceiling candidate should receive a surface-diversity bonus.
```

or:

```text
Two group members just launched.
Delay my takeoff.
```

The final decision still belongs to the individual.

---

# 32. Group Styles

The same enemy type could feel different using group parameters.

## 32.1 Horde

```text
CoordinationStrength = low
PreferredSpacing = low
ActionStaggerStrength = low
TargetDiversity = low
```

Result:

- Dense.
- Chaotic.
- Overlapping intentions are common.
- Visually overwhelming.

---

## 32.2 Pack

```text
CoordinationStrength = high
PreferredSpacing = medium
ActionStaggerStrength = medium
TargetDiversity = high
```

Result:

- Enemies naturally spread around the player.
- Less duplication.
- More deliberate traversal.

---

## 32.3 Synchronized Creatures

```text
CoordinationStrength = high
ActionStaggerStrength = near zero
SharedDecisionBias = high
```

This can intentionally create eerie synchronized movement.

---

## 32.4 Bird Flock / Perching Group

```text
PreferredAirborneFraction = 0.5
LaunchSpacing = high
PreferredPerchSpread = high
TargetDiversity = high
```

Result:

- Some birds remain perched.
- Others launch.
- Swoops happen in staggered groups.

---

# 33. Group Aggro Propagation

Aggro can optionally propagate through a group.

Expose:

```text
AggroPropagationEnabled
AggroPropagationRadius
AggroPropagationDelay
AggroPropagationDelayVariance
```

This allows:

```text
Enemy A notices player.
0.2 sec later nearby enemy B notices.
0.3 sec later enemy C notices.
```

rather than every enemy snapping to aggro on one frame.

---

# 34. Group Tuning Parameters

| Parameter | Purpose |
|---|---|
| Group Radius | Membership/proximity |
| Coordination Strength | Weight of group state |
| Preferred Group Spread | Tight versus loose swarm |
| Preferred Airborne Fraction | Desired portion flying |
| Preferred Relocating Fraction | Prevent entire group moving |
| Launch Spacing | Desired spacing between major actions |
| Surface Diversity Preference | Encourage distributed surfaces |
| Target Diversity | Avoid identical destinations |
| Aggro Propagation | Share alert state |
| Aggro Propagation Delay | Cascading reaction |
| Group Cohesion | Avoid excessive separation |
| Group Center Bias | Pull isolated agents inward |

---

# 35. Decision Update Frequencies

Not every system should run every frame.

Recommended approximate update frequencies:

| System | Frequency |
|---|---:|
| Physics motor | Every physics tick |
| Immediate collision handling | Every physics tick |
| Local separation | 5-15 Hz |
| Perception | 5-15 Hz |
| Target evaluation | 3-10 Hz |
| Crowd density update | 2-5 Hz |
| Group summary | 2-5 Hz |
| Long-range surface planning | On demand |
| Claim cleanup | 2-10 Hz or event driven |

Each enemy should have slight timing jitter.

Example:

```text
base decision interval = 0.20 sec
enemy-specific multiplier = 0.85-1.15
```

This both reduces CPU spikes and avoids synchronized decisions.

---

# 36. Suggested Decision Pipeline

A typical decision cycle:

```text
1. Update perception.
2. Determine behavioral intent.
3. Measure local crowd pressure.
4. Query nearby coordination state.
5. Determine whether current target/action remains acceptable.
6. If committed action is valid:
       continue.
7. Otherwise generate candidate destinations.
8. Filter illegal candidates.
9. Score candidates.
10. Add reservation and predicted occupancy penalties.
11. Add local action-pressure penalties.
12. Add group biases if applicable.
13. Compare best candidate against stay-put/current target.
14. Commit if improvement exceeds threshold.
15. Request destination claim.
16. Execute chosen locomotion mode.
```

---

# 37. Suggested Candidate Scoring Function

A prototype implementation may use:

```text
score =
      playerProgress       * playerProgressWeight
    + tacticalValue        * tacticalWeight
    + surfacePreference    * surfacePreferenceWeight
    + surfaceDiversity     * diversityWeight
    + groupPreference      * groupWeight
    + randomBias           * randomnessWeight

    - travelDistance       * distanceCost
    - traversalDifficulty  * difficultyCost
    - currentCrowding      * crowdWeight
    - predictedCrowding    * predictedCrowdWeight
    - reservationPenalty
    - actionPressurePenalty
```

Then:

```text
if candidate is current position:
    score += stayPutBias
```

Hard-invalid candidates should be removed before scoring.

---

# 38. Controlled Randomness

Randomness should be used carefully.

Good uses:

- Personal space variation.
- Wander timing.
- Reaction delay.
- Decision intervals.
- Small destination-score noise.
- Crowd patience.
- Action hesitation.

Bad uses:

- Completely replacing deterministic target selection.
- Rerolling direction every physics frame.
- Midair random retargeting.
- Large random changes that override obvious movement goals.

Randomness should primarily break symmetry.

---

# 39. Surface Regions

For large surfaces, consider dividing a surface into coarse logical regions.

Example:

```text
Long floor:
    region 0
    region 1
    region 2
    region 3
```

This can improve:

- Crowd density estimation.
- Reservation lookup.
- Destination diversity.
- Predictive occupancy.

A region does not need to correspond to a separate collider.

It can be a coarse navigation subdivision.

Do not require this for the first prototype if simple local spatial queries are sufficient.

---

# 40. Moving Surfaces

All systems must remain compatible with moving platforms.

For any attachment, target, or reservation on a moving object, prefer:

```text
SurfaceReference
LocalPosition
```

over:

```text
WorldPosition only
```

Affected systems include:

- Enemy attachment.
- Jump destination.
- Flight landing destination.
- Reservation position.
- Crowd occupancy.
- Surface candidate debug visualization.

---

# 41. Failure Recovery

Locomotion actions should fail gracefully.

## Jump failure

Possible responses:

```text
fall
attempt emergency reattachment
attempt another nearby landing
```

## Flight destination invalidated

Possible responses:

```text
find emergency landing
continue current heading briefly
fall
```

## Claimed target disappears

Immediately release or invalidate the claim.

## Surface destroyed

Remove occupancy and claims associated with that surface.

---

# 42. Performance Goals

The design should scale primarily by limiting local work.

Avoid:

```text
for every enemy:
    compare against every other enemy
```

Prefer:

```text
for enemy:
    query nearby spatial cells
    inspect nearest/relevant N agents
```

Recommended limits:

```text
MaxNeighborsConsidered
MaxClaimsConsidered
MaxSurfaceCandidates
MaxGroupMembersSampled
```

Candidate generation should also be bounded.

Example:

```text
Generate up to 16 plausible landing points.
Score them.
```

Do not evaluate hundreds of surface points per enemy every few frames.

---

# 43. Recommended Spatial Registry Responsibilities

`EnemySpatialRegistry` should support:

```text
RegisterEnemy
UnregisterEnemy
UpdateEnemyPosition

AddClaim
RemoveClaim
QueryClaims

PublishActionEvent
QueryRecentActionEvents

PublishRelocationIntent
RemoveRelocationIntent

QueryNearbyEnemies
EstimateCrowdDensity
EstimatePredictedCrowdDensity
```

Internally, use:

```text
spatial hash
uniform grid
quadtree
```

A simple spatial hash/grid is probably sufficient and easiest to debug.

---

# 44. Recommended Data Flow

```text
EnemyBrain
    |
    v
Desired Intent
    |
    v
DestinationEvaluator <------ CrowdAgent
    |                         ^
    |                         |
    |                 EnemyCoordinationAgent
    |                         |
    v                         |
SurfaceQuery -----------------+
    |
    v
Chosen SurfaceCandidate
    |
    v
EnemyLocomotionController
    |
    +--> SurfaceMotor
    +--> JumpMotor
    +--> FlightMotor
```

Shared services:

```text
EnemySpatialRegistry
EnemyCoordinationSystem
EnemyGroupManager
```

---

# 45. Debug Visualization

The prototype must make AI reasoning visible.

This is extremely important.

Recommended visual overlays:

```text
Green circle:
    personal-space radius

Red circle:
    hard destination reservation

Yellow circle:
    soft reservation radius

Blue arrow:
    desired movement direction

Purple arrow:
    separation vector

White dots:
    valid candidate destinations

Gray dots:
    rejected candidate destinations

Bright highlighted dot:
    selected destination

Surface overlay:
    current crowd density

Secondary surface overlay:
    predicted crowd density
```

---

# 46. Enemy Debug Text

Optional world-space debug label:

```text
Intent: CHASE
Locomotion: ATTACHED

Aggro: TRUE
Crowd: 0.72

CurrentScore: 4.8
BestCandidate: 6.3

JumpDesire: 0.64
FlightDesire: 0.00

Claimed: TRUE
```

The exact UI can be simplified, but developers should be able to answer:

> Why did this enemy choose this movement?

without guessing.

---

# 47. Candidate Debugging

When an enemy is selected in the Unity editor or debug sandbox, show each candidate and its score breakdown.

Example:

```text
Candidate 4 - Ceiling

Total: 6.3

Player Progress:     +3.2
Surface Diversity:   +2.0
Surface Preference:  +0.8
Travel Cost:         -0.7
Crowding:            -0.2
Reservation:          0.0
Random Bias:         +0.2
```

This will make tuning dramatically easier.

---

# 48. Sandbox Controls

The movement sandbox should allow runtime editing of settings.

At minimum, support changing:

```text
Walk speed
Surface angle
Aggro range
Aggro mode
Personal space
Crowding weight
Jump range
Jump height
Jump cooldown
Flight time
Flight speed
Flight climb limit
Reservation size
Stagger strength
Decision frequency
Surface diversity
```

Ideally settings should be exposed using:

- Inspector fields.
- ScriptableObjects.
- Optional runtime debug panel.
- Preset save/load.

---

# 49. Recommended Full Parameter Categories

## 49.1 Surface Movement

```text
WalkSpeed
Acceleration
Deceleration
TurnResponsiveness
MaxSurfaceAngle
MaxCornerAngle
EdgeBehavior
SurfaceStickStrength
WanderSpeedMultiplier
AggroSpeedCurve
TargetOvershoot
MinimumMoveDuration
SurfaceReattachDelay
```

---

## 49.2 Perception

```text
DetectionRange
LoseRange

FieldOfView
RequireLineOfSight
LineOfSightMode

SameSurfaceAcquire
SameSurfaceRetain

AggroDelay
AggroDelayVariance
DeaggroDelay
MemoryTime
```

---

## 49.3 Jumping

```text
Enabled

JumpRange
JumpHeight
JumpSpeed
ArcBias

AirSteering
MovingTargetCorrection

JumpCooldown

JumpWindup
JumpWindupVariance

MinCommitmentTime

LandingClaimRadius
LandingSoftRadius
ClaimPenalty

PlayerProgressWeight
CrowdEscapeWeight
SurfacePreferenceWeight
SurfaceDiversityWeight
DifficultyCost

LandingRecoveryTime
```

---

## 49.4 Crowding

```text
PersonalSpaceMean
PersonalSpaceVariance

HardCollisionRadius
SoftSeparationRadius
SeparationStrength

CrowdPressureThreshold
CrowdPatience
CrowdPatienceVariance

RelocationCooldown

CurrentCrowdWeight
PredictedCrowdWeight

AlternateSurfaceBonus
SurfaceDiversityWeight

SameTypeWeight
OtherTypeWeight

PredictedDepartureWeight

PositionRandomness
StayPutBias
```

---

## 49.5 Flight

```text
Enabled

FlightSpeed
Acceleration
TurnRate

MinFlightTime
MaxFlightTime
FlightCooldown

MinHeadingCommitment

MaxClimbAngle
MaxDiveAngle

TakeoffWindup
TakeoffWindupVariance

PreferredFlightDistance
LandingSearchRadius

DirectPlayerWeight
PerchProgressWeight

CrowdingWeight
AirborneCrowdWeight

DesiredAltitude
GlideBias

LandingClaimRadius
LandingSoftRadius
LandingCommitment
```

---

## 49.6 Coordination

```text
CoordinationRadius
MaxNeighbors

HardClaimRadius
SoftClaimRadius
ClaimPenalty
ClaimTimeout

ActionStaggerStrength
ActionStaggerRadius
RecentActionWindow

JumpActionPenalty
FlightActionPenalty
RelocationActionPenalty

MaxSimultaneousJumpers
MaxSimultaneousFlyers

DecisionInterval
DecisionIntervalVariance

CommitmentTime
RetargetThreshold
```

---

## 49.7 Group Settings

```text
Enabled

GroupRadius
CoordinationStrength

PreferredGroupSpread
GroupCohesion

PreferredAirborneFraction
PreferredRelocatingFraction

LaunchSpacing

SurfaceDiversityPreference
TargetDiversity

AggroPropagationEnabled
AggroPropagationRadius
AggroPropagationDelay
AggroPropagationDelayVariance
```

---

# 50. Recommended Prototype Presets

It may be useful to create a few artificial test presets before real enemies exist.

## Floor Walker

```text
CanWalk = true
CanJump = false
CanFly = false

MaxSurfaceAngle = low
EdgeBehavior = TurnAround
```

Purpose:

- Test basic walking.
- Test aggro.
- Test declumping.

---

## Spider

```text
CanWalk = true
CanJump = true
CanFly = false

MaxSurfaceAngle = full
High SurfaceDiversityWeight
Moderate CrowdEscapeWeight
```

Purpose:

- Test walls and ceilings.
- Test surface distribution.
- Test jump reservations.

---

## Hopper

```text
CanWalk = true
CanJump = true
CanFly = false

High JumpRange
High JumpActionStagger
Moderate PersonalSpace
```

Purpose:

- Test coordinated jumping.
- Test moving-platform correction.

---

## Balloon

```text
CanWalk = true
CanFly = true

Low acceleration
Low turn rate
Long flight duration
Low direct-player weight
Moderate altitude preference
```

Purpose:

- Test loose steering.
- Test landing selection.

---

## Pterodactyl

```text
CanWalk = true
CanFly = true

High flight speed
Moderate acceleration
Limited climb
Limited turn rate
High heading commitment
High perch-progress weight
```

Purpose:

- Test swoops.
- Test intermediate surfaces.
- Test staggered takeoffs.

---

# 51. Recommended Implementation Order

Build the system incrementally.

## Phase 1: Shared Surface Movement

Implement:

```text
Surface representation
Surface attachment
Surface tangent movement
Surface legality
Edge behavior
Moving-surface compatibility
```

Deliverable:

A single enemy can move around legal geometry reliably.

---

## Phase 2: Perception and Brain

Implement:

```text
Idle
Wander
Aggro acquisition
Aggro retention
Aggro timing
Aggro speed curves
```

Deliverable:

The enemy can patrol, notice the player, and pursue along its current surface.

---

## Phase 3: Spatial Registry and Separation

Implement:

```text
EnemySpatialRegistry
Nearby enemy query
Hard overlap prevention
Personal space
Surface-projected separation
Crowd pressure
```

Deliverable:

Groups no longer stack unnaturally.

---

## Phase 4: Surface Candidate System

Implement:

```text
SurfaceQuery
Candidate generation
Surface legality filtering
Candidate scoring
Stay-put bias
Debug visualization
```

Deliverable:

An enemy can identify desirable nearby surface positions even before it can traverse to them.

---

## Phase 5: Destination Claims

Implement:

```text
Hard claim
Soft claim
Claim lifetime
Moving-surface claims
Reservation penalties
Claim debug rendering
```

Deliverable:

Multiple enemies planning simultaneously choose different destinations.

---

## Phase 6: Jumping

Implement:

```text
Reachability
Jump windup
Guided jump
Moving-target correction
Landing
Claim release
Jump cooldown
```

Deliverable:

Enemies jump reliably between legal surfaces without stacking.

---

## Phase 7: Predictive Crowd Relocation

Implement:

```text
RelocationIntent
Predicted departures
Predicted arrivals
Predicted crowd density
Crowd relocation scoring
Alternate surface preference
```

Deliverable:

Crowds redistribute organically across surfaces.

---

## Phase 8: Action Staggering

Implement:

```text
Persistent individual timing
Action events
Local action pressure
Jump staggering
Relocation staggering
Aggro staggering
```

Deliverable:

Groups stop acting in lockstep.

---

## Phase 9: Flight

Implement:

```text
Takeoff
Flight physics
Flight restrictions
Landing search
Intermediate perch selection
Flight claims
Landing
Cooldown
```

Deliverable:

Balloon, bird, glider, and launch-style movement can all be prototyped.

---

## Phase 10: Groups

Implement only after local coordination works.

Add:

```text
Group membership
Group summaries
Airborne fraction
Surface diversity bias
Target diversity
Aggro propagation
Group cohesion
```

Deliverable:

Optional coordinated packs/swarms can be configured without replacing individual AI.

---

# 52. Technical Non-Goals for Initial Prototype

Do not overbuild the first implementation.

The first version does not need:

- Perfect global pathfinding.
- Full navigation meshes across arbitrary curved surfaces.
- Optimal multi-agent planning.
- Exact space-time reservation solving.
- Physically perfect ballistic interception of moving platforms.
- Global flocking algorithms.
- Central swarm commanders.
- Sophisticated attack tactics.

The architecture should permit future improvements without requiring them immediately.

---

# 53. Suggested Initial Simplifications

For the first implementation:

### Surface search

Use a bounded number of physics queries and sampled candidate points.

### Crowd density

Estimate from nearby enemy counts rather than constructing a global density field.

### Predicted density

Use:

```text
current enemies
+ incoming claims
- published outgoing relocations
```

### Reservations

Use simple radius-based hard/soft zones.

### Jumping

Use guided arcs instead of perfect ballistic simulation.

### Flight

Use steering constraints and candidate landing selection rather than full path planning.

### Group logic

Defer until local coordination is proven.

---

# 54. Recommended Coding Rules

## Avoid giant classes

Do not place:

```text
perception
walking
jumping
flight
crowding
group logic
reservations
```

inside one `EnemyMovement.cs`.

Use clearly separated responsibilities.

---

## Keep configuration separate from runtime state

Example:

```text
EnemyMovementConfig
```

contains authored parameters.

Runtime fields belong in components such as:

```text
EnemyBrainState
CrowdAgent
JumpMotor
FlightMotor
```

---

## Avoid hidden cross-component dependencies

Prefer explicit references/interfaces such as:

```text
ISurfaceMotor
ILocomotionMotor
ISurfaceQuery
ICoordinationAgent
```

where useful.

Do not require every component to search the hierarchy repeatedly with:

```text
GetComponent
FindObjectOfType
```

during gameplay.

Resolve dependencies once.

---

## Keep AI decisions deterministic when possible

Use controlled seeded randomness.

This is important for:

- Reproducing bugs.
- Comparing parameter changes.
- Testing group behavior.

---

# 55. Example Enemy Decision

Consider a crawling enemy pursuing the player in a tunnel.

Current situation:

```text
Player is ahead.
Six enemies occupy the floor.
Ceiling is legal.
Two enemies are already jumping toward ceiling positions.
One enemy is leaving the floor.
```

The enemy evaluates:

### Continue along floor

```text
Player progress:      +3.0
Travel cost:          -0.2
Current crowd:        -2.8
Predicted crowd:      -1.8
Stay put:             +0.8

Total:                -1.0
```

### Jump to ceiling candidate A

```text
Player progress:      +2.3
Surface diversity:    +1.8
Crowd reduction:      +2.0
Travel difficulty:    -0.8
Reservation penalty:  -3.0

Total:                +2.3
```

### Jump to ceiling candidate B

```text
Player progress:      +2.0
Surface diversity:    +1.8
Crowd reduction:      +2.0
Travel difficulty:    -0.9
Reservation penalty:   0.0

Total:                +4.9
```

The enemy selects candidate B.

It:

1. Claims the landing region.
2. Publishes a `JumpStarted` event.
3. Performs its windup.
4. Executes the jump.
5. Tracks the moving surface target if necessary.
6. Lands.
7. Releases the claim.
8. Becomes normal occupancy on the ceiling.

Nearby enemies see:

- Another ceiling position is now predicted occupied.
- Another jump just started.
- Floor density is slightly lower.

They may therefore remain grounded or choose different destinations.

This is the desired emergent behavior.

---

# 56. Final Architectural Summary

The enemy movement framework should answer three questions for every agent:

```text
1. Where do I want to be?
2. How can I legally get there?
3. What are the other enemies already doing?
```

The system should achieve this through:

```text
behavioral intent
+
shared surface candidates
+
modular locomotion
+
local crowd pressure
+
destination reservations
+
predicted occupancy
+
local action staggering
+
small persistent randomness
+
commitment/hysteresis
+
optional group-level biases
```

Enemies should remain individually autonomous.

The shared coordination system should not micromanage them. It should change the information available to each agent:

```text
That landing location is claimed.

Three nearby enemies just jumped.

This surface will become less crowded soon.

That ceiling region is underoccupied.

Half of my group is already airborne.
```

Each enemy then makes its own locally informed decision.

This combination should provide the desired style:

- Dynamic.
- Slightly chaotic.
- Visually natural.
- Configurable.
- Scalable.
- Reusable across many enemy archetypes.
- Capable of producing interesting floor/wall/ceiling interactions.
- Resistant to unnatural synchronization and clumping.
- Easy to tune in a sandbox before individual enemy designs are finalized.
