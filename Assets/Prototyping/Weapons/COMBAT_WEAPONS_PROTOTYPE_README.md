# Combat and Weapons Prototype — Technical Design Specification

## 1. Purpose

The movement prototype is now mature enough that the next major prototyping goal is **combat**.

The purpose of this prototype is **not** to build the final combat system, finalize balance, construct upgrade trees, or create production-quality enemies.

The purpose is to answer:

> **What kinds of attacks are fun when combined with the game's unusual surface-crawling, jumping, grappling, and momentum-based movement?**

The player is an eyeball capable of:

- crawling across arbitrary surfaces,
- walking on walls and ceilings,
- jumping between surfaces,
- moving through the air,
- optionally double-jumping,
- grappling,
- aiming independently toward the cursor.

Combat should be tested specifically in combination with those mechanics.

The prototype should therefore favor:

- rapid tuning,
- clear debug information,
- easy weapon switching,
- easy enemy reset and respawning,
- diverse geometry,
- easy comparison between weapon concepts,
- readable damage behavior,
- simple implementation,

over:

- final balance,
- final art,
- polished enemy AI,
- progression systems,
- inventory systems,
- upgrade trees,
- procedural encounters,
- production-ready abstraction.

---

# 2. Core Combat Concepts

There are two fundamental ways for the player to deal damage.

## 2.1 Ranged / Weapon Damage

The eye can use a variety of weapons.

Current prototype weapon list:

1. Eye Beam
2. Heavy Shot
3. Burst Shot
4. Sniper Shot
5. Flamethrower
6. Holy Beam
7. Four-Directional Holy Beam variant
8. Flail
9. Chain / Skewer weapon
10. Short-Ranged Eye Pulse

There should deliberately **not** be a generic semiautomatic "normal shot" weapon or a traditional full-auto gun in the initial prototype.

The weapon set should test substantially different attack geometries and movement interactions.

## 2.2 Body Impact Damage

The eyeball itself is also a weapon.

When the player collides with an enemy:

```text
enemy damage =
    base body-contact damage
    + damage based on relative impact speed
```

The player may simultaneously receive contact damage from the enemy.

This mechanic should be tunable independently from ordinary enemy contact damage.

The goal is to investigate whether movement itself can serve as the game's melee system rather than giving the player a conventional sword or melee weapon.

Potential future upgrades could therefore revolve around:

- movement speed,
- bursts of speed,
- impact damage,
- contact survivability,
- jump strength,
- momentum,
- air movement,
- grapple movement,

rather than traditional melee weapons.

Do not build those upgrade systems yet.

---

# 3. Integration With Existing Movement Prototype

Combat must remain modular.

Do **not** put weapon logic or combat formulas directly into `PlayerMotor2D`.

Combat systems may read information from movement systems, including:

```text
player position
player velocity
movement state
attached / airborne state
surface normal
current surface
jump state
grapple state
aim direction
```

but movement should not depend on combat.

Likewise, keep these systems independent wherever practical:

```text
leg animation
eye animation
surface attachment
combat
enemy logic
damage calculations
```

The player root position should remain the authoritative location for:

- projectile spawning,
- beam origins,
- pulse origins,
- flail attachment,
- chain origin,
- collision-based damage.

The pupil / eye aiming system should determine cursor-facing direction for weapons that aim toward the mouse.

---

# 4. Recommended Folder Structure

Use the existing shared prototype infrastructure where appropriate.

Combat-specific files should remain together.

Recommended layout:

```text
Assets/
└── Prototyping/
    ├── Shared/
    │   ├── existing movement/player/surface systems
    │   └── optionally truly shared combat primitives
    │
    └── Combat/
        ├── README.md
        ├── Scenes/
        │   └── CombatSandbox.unity
        │
        ├── Scripts/
        │   ├── Player/
        │   ├── Weapons/
        │   │   ├── Core/
        │   │   ├── Beam/
        │   │   ├── HeavyShot/
        │   │   ├── Burst/
        │   │   ├── Sniper/
        │   │   ├── Flamethrower/
        │   │   ├── HolyBeam/
        │   │   ├── Flail/
        │   │   ├── Chain/
        │   │   └── Pulse/
        │   │
        │   ├── Damage/
        │   ├── Enemies/
        │   ├── Debug/
        │   └── Editor/
        │
        ├── Prefabs/
        │   ├── Enemies/
        │   ├── Projectiles/
        │   └── Effects/
        │
        └── Settings/
```

Avoid scattering combat prototype files throughout the production project.

---

# 5. Combat Sandbox Scene

Create:

```text
Assets/Prototyping/Combat/Scenes/CombatSandbox.unity
```

This should be a **large sandbox/laboratory**, not a polished level.

It should reuse the same general philosophy as the movement sandbox:

- floors,
- walls,
- ceilings,
- circles,
- blobs,
- irregular geometry,
- gaps,
- platforms,
- vertical spaces,
- enclosed spaces,
- moving geometry where useful.

The important difference is that the space should now contain enemy groups specifically arranged for combat testing.

---

# 6. Recommended Sandbox Layout

Use one large scene divided visually/spatially into several test regions.

Exact dimensions are not important.

## Zone A — Open Shooting Range

Large mostly-open area.

Include:

- isolated enemies at multiple distances,
- several enemies in a straight line,
- several enemies offset slightly from one another,
- dense enemy clusters.

Tests:

- range,
- damage falloff,
- projectile speed,
- piercing,
- piercing damage falloff,
- spread,
- autoaim,
- sniper behavior,
- beam behavior.

Distance markers or simple floor markings would be useful.

Example distances:

```text
5 units
10 units
15 units
20 units
```

## Zone B — Dense Crowd Area

Place many enemies close together.

Tests:

- flamethrower,
- pulse,
- beams,
- piercing,
- wide attack shapes,
- chain/skewer,
- flail,
- knockback,
- multiple simultaneous hits.

## Zone C — Vertical Chamber

Tall room with:

- floor,
- ceiling,
- wall surfaces,
- enemies at multiple heights,
- enemies above and below likely player positions.

Tests:

- holy beam,
- four-direction holy beam,
- shooting while crawling vertically,
- jumping while firing,
- aiming while attached to walls.

## Zone D — Surface-Crawling Combat Course

Use:

- circles,
- blobs,
- ceilings,
- overhangs,
- pillars,
- narrow transitions.

Populate with enemies so the player must actively move while attacking.

Tests:

- movement + aim coordination,
- beam usage while crawling,
- burst aim assist,
- body impacts,
- flail behavior,
- flamethrower close-range maneuvering.

## Zone E — Momentum / Impact Course

Construct areas that allow the player to:

- jump from height,
- accelerate horizontally,
- fall long distances,
- grapple into enemies,
- launch between surfaces.

Place durable enemies as impact targets.

Tests:

- body-contact damage,
- relative-speed scaling,
- self-damage,
- knockback,
- momentum-heavy attacks,
- flail momentum.

Include debug readouts for impact velocity.

## Zone F — Moving Enemy / Moving Geometry Area

Use a smaller number of:

- moving platforms,
- moving enemies,
- seeking enemies.

Tests:

- predictive aiming,
- burst autoaim,
- heavy projectile arcs,
- moving-player accuracy,
- relative impact velocity,
- chain behavior,
- flail behavior.

---

# 7. Enemy Prototype System

Enemies are test instruments, not final game enemies.

They should be simple, obvious, and highly configurable.

---

# 8. Base Enemy Components

Recommended:

```text
DummyEnemy
Health
EnemyMovementController
EnemyContactDamage
EnemyRespawnHandle
EnemyDebugDisplay
```

A dummy enemy should expose at least:

```text
Max Health
Current Health

Contact Damage
Contact Damage Cooldown

Movement Mode
Movement Speed

Respawn Enabled
Respawn Delay

Invulnerable
```

---

# 9. Enemy Health

Each enemy needs visible health information.

At minimum show:

```text
Current HP / Max HP
```

Example:

```text
73 / 100
```

This may be rendered:

- above the enemy,
- in a small world-space label,
- with an optional health bar.

---

# 10. Damage Numbers

Every hit should generate a readable floating damage number.

Example:

```text
42
```

Optionally distinguish:

```text
normal damage
large damage
body impact damage
```

but color polish is not important.

The purpose is rapid tuning.

---

# 11. Detailed Hit Debug Information

Optionally display temporary expanded information after a hit:

```text
Damage: 42.3
Weapon: Sniper
Distance: 16.2
Distance Multiplier: 1.00
Pierce Index: 2
Pierce Multiplier: 0.72
Final Damage: 30.5
```

For body impacts:

```text
Impact Damage: 35.2
Relative Speed: 12.4
Base Damage: 8
Speed Damage: +27.2
Player Damage Taken: 10
```

This should be a debug toggle rather than always visible.

---

# 12. Enemy Movement Modes

Use one configurable enemy prefab where possible.

Movement behavior dropdown:

```text
Static
Patrol
Wander
Seek Player
```

## Static

Does not move.

Primary use:

- weapon DPS testing,
- distance testing,
- piercing testing.

## Patrol

Moves back and forth along a simple line.

Parameters:

```text
Patrol Distance
Patrol Speed
Patrol Axis / Endpoints
```

## Wander

Moves in simple changing directions.

No sophisticated pathfinding is needed.

This exists only to create less predictable targets.

## Seek Player

Moves toward the player.

Keep implementation intentionally simple.

The point is to create pressure for:

- melee/body impact testing,
- flamethrower,
- pulse,
- flail,
- movement while firing.

Do not build advanced enemy navigation unless required.

---

# 13. Global Enemy Controls

The debug UI should include:

```text
Respawn All
Kill All
Reset All

Auto Respawn: On/Off
Respawn Delay

Freeze Enemies: On/Off

Global Enemy Health Multiplier
Global Enemy Contact Damage Multiplier

Enemy Movement:
    Individual
    Force Static
    Force Patrol
    Force Wander
    Force Seek
```

A global movement override is useful for quickly comparing weapon behavior.

---

# 14. Player Health

Add a simple player health system.

Recommended:

```text
PlayerHealth
```

Parameters:

```text
Max Health
Current Health

Invulnerability Duration After Damage
Indestructible
```

The sandbox UI must contain:

```text
Indestructible Player: On/Off
```

When enabled:

- incoming damage may still be displayed/logged,
- current HP should not decrease,
- player should not die.

This allows melee testing without constant resets.

---

# 15. Player Death / Respawn

For prototype purposes:

```text
HP <= 0
→ respawn player at current/default spawn
→ restore full health
```

Optional:

```text
auto respawn delay
```

Do not build death screens or game-over logic.

---

# 16. Enemy Contact Damage

Enemies deal damage when touching the player.

This should not occur every frame.

Use something like:

```text
contactDamage
contactDamageCooldown
```

Each enemy may maintain its own cooldown against the player.

Example:

```text
Enemy touches player
→ 10 damage
→ cannot damage same player again for 0.5 sec
```

---

# 17. Body Impact Damage

The player's body may damage enemies on physical contact.

This is independent from enemy contact damage.

Recommended basic model:

```text
relativeVelocity =
    playerVelocity - enemyVelocity
```

Calculate impact speed primarily along the collision direction when possible.

Conceptually:

```text
approachSpeed =
    max(0, dot(relativeVelocity, directionIntoEnemy))
```

Then:

```text
enemyDamage =
    baseImpactDamage
    + max(0, approachSpeed - minimumDamageSpeed)
      * impactSpeedScale
```

Optional clamp:

```text
enemyDamage =
    min(enemyDamage, maxImpactDamage)
```

Expose:

```text
Base Impact Damage
Minimum Impact Speed
Impact Speed Damage Scale
Maximum Impact Damage
Impact Damage Cooldown Per Enemy
```

Body impact should only trigger when the player is actually approaching the enemy, not merely remaining overlapped.

---

# 18. Body Impact Self-Damage

Keep player damage independently configurable.

At minimum support:

```text
Enemy Contact Damage Only
```

and optionally:

```text
Enemy Contact Damage + Additional Impact Self-Damage
```

Expose:

```text
Additional Impact Self Damage
Impact Self Damage Scale
```

Do not assume self-damage is necessary for balance.

The prototype exists partly to determine whether ordinary enemy contact risk is already sufficient.

---

# 19. Knockback

Knockback should be a common combat concept.

A damage event may contain:

```text
knockback magnitude
knockback direction
```

Potential directions:

```text
AwayFromSource
AlongProjectileVelocity
RadialFromImpact
Custom
PullTowardSource
```

Enemies should have enough movement response that knockback is visible.

Do not overbuild physics.

A simple temporary velocity/impulse is sufficient.

For enemies using scripted movement, knockback should temporarily override or add to their normal movement rather than being instantly cancelled on the next frame.

---

# 20. Common Attack Geometry

Do not reduce every attack to "projectile + radius."

Weapons should support different geometric damage shapes.

Useful categories:

```text
Projectile Collider
Ray / Line
Wide Line / Beam
Circle
Cone
Box / Column
Physical Collider
Chain Segment / Swept Line
```

The attack geometry determines which enemies are candidates for damage.

Piercing determines how targets are processed after intersection.

---

# 21. Range

Every ranged weapon should have a meaningful maximum range.

Range should be visible to the player.

For visual feedback:

> **Weapons/projectiles should lose opacity near the end of their range.**

Expose:

```text
Max Range
Fade Start Fraction
```

Example:

```text
Max Range = 20
Fade Start Fraction = 0.75
```

Then:

```text
0–15 units:
    full opacity

15–20 units:
    fade toward transparent
```

This should apply where appropriate to:

- projectiles,
- beam effects,
- flame effects,
- chain visuals,
- other ranged effects.

The visual fade should communicate remaining range; it should not itself determine damage unless the weapon's distance-falloff settings happen to match it.

---

# 22. Projectile Range Measurement

For moving projectiles, range should normally be based on:

```text
total distance traveled
```

not:

```text
straight-line distance from current player position
```

This matters especially for gravity-affected projectiles.

Track something like:

```text
distanceTraveled += distance(previousPosition, currentPosition)
```

Destroy/end the projectile when:

```text
distanceTraveled >= maxRange
```

---

# 23. Distance-Based Damage Falloff

Distance falloff is separate from range.

A weapon may:

- stop at 20 units but maintain full damage for all 20 units, or
- stop at 10 units while rapidly losing damage over those 10 units.

Represent:

```text
distanceFraction =
    distanceTraveled / maxRange
```

Then:

```text
damage *= DistanceFalloff(distanceFraction)
```

For radial attacks:

```text
distanceFromAttackCenter / attackRadius
```

may instead be used.

Support at least:

```text
None
Linear
Custom Curve
```

For line/beam-style attacks, use distance from the weapon origin to the target hit position.

For projectiles, use projectile distance traveled.

---

# 24. Piercing

Piercing describes how many enemies an attack may continue through.

Do not treat it as simply:

```text
true / false
```

Use a count.

Recommended conceptual representation:

```text
0 = attack stops after first enemy
1 = may pass through one additional enemy
2 = may pass through two additional enemies
...
Infinite = enemy hits never consume piercing
```

Alternatively use:

```text
MaxTargetsHit
```

if implementation is simpler.

Enemy hits should consume piercing; environment collisions should be handled separately by each weapon's behavior.

---

# 25. Piercing Damage Falloff

Piercing damage falloff is independent from distance falloff.

Track:

```text
hitIndex
```

Example:

```text
first enemy:  hitIndex = 0
second enemy: hitIndex = 1
third enemy:  hitIndex = 2
```

Possible modes:

```text
None
Flat Subtraction
Multiplicative
Custom Curve
```

Recommended default:

```text
damage *= pow(pierceDamageMultiplier, hitIndex)
```

Example:

```text
base damage = 100
pierce multiplier = 0.8

Enemy 1 = 100
Enemy 2 = 80
Enemy 3 = 64
Enemy 4 = 51.2
```

For continuous infinite-piercing effects such as the flamethrower, piercing index usually should **not** accumulate simply because many enemies occupy the same attack volume unless that weapon explicitly enables pierce falloff.

---

# 26. Damage Calculation Pipeline

Use a consistent damage pipeline.

Conceptually:

```text
Base Damage
    ↓
Distance Falloff
    ↓
Pierce Falloff
    ↓
Other Weapon-Specific Modifiers
    ↓
Final Damage
```

Example:

```text
finalDamage =
    baseDamage
    * distanceMultiplier
    * pierceMultiplier
```

Then apply:

```text
damage
knockback
debug information
```

Do not bake falloff formulas separately into every weapon if shared utilities can handle them.

---

# 27. Area of Effect vs Piercing

These are separate concepts.

## Piercing

Answers:

> How many targets can an attack continue through?

Example:

```text
Sniper
```

may hit several enemies exactly along its line.

## Area / Attack Shape

Answers:

> What physical region can damage enemies?

Examples:

```text
Flamethrower → cone
Eye Pulse → circle
Holy Beam → wide column
Flail → physical circle
```

An attack may therefore have:

```text
large area + infinite piercing
```

or:

```text
tiny line + high piercing
```

or:

```text
large explosion + no piercing
```

Do not combine these concepts.

---

# 28. Damage Tick / Repeated Hit Rules

Continuous weapons require per-target hit handling.

Each attack should be able to specify:

```text
Hit Once
Repeated Damage
Repeated Damage Interval
```

Examples:

```text
Sniper:
    hit once

Beam:
    repeated damage every X seconds

Flamethrower:
    repeated damage ticks

Flail:
    may hit again after per-target cooldown
```

Use per-target hit cooldowns where appropriate.

Repeated-hit bookkeeping should be scoped to the active attack instance where possible so a newly fired attack is not incorrectly blocked by an old attack's cooldown.

---

# 29. Weapon Selection

Do **not** use number keys.

Provide a weapon selection menu/dropdown.

Additionally:

```text
[ = previous weapon
] = next weapon
```

Weapon order should be stable and visible.

Display the current weapon prominently:

```text
Weapon: Sniper
```

Changing weapons should immediately update the weapon tuning panel.

Weapon switching should cleanly cancel or retract any active weapon state that cannot safely persist after deselection.

Examples:

- active beam stops,
- holy beam lock clears,
- chain retracts/cancels,
- flail is despawned or disabled unless intentionally designed to persist.

---

# 30. Weapon UI

Create a dedicated weapon panel.

The panel should only display settings relevant to the currently selected weapon.

Avoid one enormous menu showing all parameters for all weapons.

Suggested sections:

```text
Weapon Selection

General
Damage
Range / Falloff
Piercing
Knockback
Weapon-Specific Settings
Debug
```

---

# 31. Main Prototype UI Layout

Recommended tabs/panels:

```text
PLAYER
ENEMIES
WEAPON
IMPACT
DEBUG
```

## Player Panel

Include:

```text
Current HP
Max HP

Indestructible Player

Respawn Player
Restore Health
```

Existing movement tuning may remain in its existing UI rather than being duplicated.

## Enemies Panel

Include:

```text
Respawn All
Kill All

Auto Respawn
Respawn Delay

Freeze Enemies

Global Health Multiplier
Global Contact Damage Multiplier

Movement Override
```

## Weapon Panel

Include:

```text
Weapon Dropdown
Previous Weapon
Next Weapon

Current weapon's tunable parameters
```

## Impact Panel

Include:

```text
Enable Body Damage

Base Damage
Minimum Impact Speed
Speed Damage Scaling
Maximum Damage

Impact Cooldown

Enemy Contact Damage
Self-Damage Options

Impact Knockback
```

## Debug Panel

Include:

```text
Damage Numbers
Enemy Health Labels
Detailed Hit Information
Projectile Paths
Weapon Range
Attack Geometry
Aim Assist Region
Knockback Vectors
Impact Velocity
```

---

# 32. Weapon Architecture

Recommended broad structure:

```text
WeaponController
WeaponDefinition / Settings
WeaponRuntime
```

Possible interface:

```csharp
public interface IPrototypeWeapon
{
    string DisplayName { get; }

    void OnSelected();
    void OnDeselected();

    void BeginFire();
    void ContinueFire();
    void EndFire();

    void Tick(float deltaTime);
}
```

Exact architecture is flexible.

Do not create excessive abstraction if it slows iteration.

A ScriptableObject per weapon or per weapon tuning preset is reasonable if it makes runtime editing and resetting easy.

---

# 33. Shared Weapon Data

Common properties where appropriate:

```text
Display Name

Base Damage

Cooldown

Range
Fade Start Fraction

Distance Falloff Mode
Distance Falloff Curve

Pierce Count
Pierce Falloff Mode
Pierce Damage Multiplier

Knockback

Hit Cooldown / Tick Rate
```

Weapons may ignore irrelevant fields.

Do not force every weapon into a property model that makes special-case weapons harder to understand.

---

# 34. Aim Direction

Cursor-aimed weapons should derive direction from:

```text
aimDirection =
    normalize(mouseWorld - playerRootPosition)
```

Do not depend on body rotation.

The eye may continue visually tracking the cursor.

---

# 35. Weapon 1 — Eye Beam

## Concept

A straight beam originating from the eye and aimed directly toward the cursor.

The beam continuously follows:

- player movement,
- cursor movement.

This should feel like a deliberate sustained beam rather than a normal automatic gun.

## Behavior

Suggested cycle:

```text
hold fire
→ beam activates
→ beam remains active for up to Active Duration
→ forced cooldown
→ may fire again
```

Releasing early may end the beam.

## Damage Geometry

```text
Wide line / beam
```

Hits every enemy intersecting the beam.

Recommended:

```text
Piercing = Infinite
```

## Parameters

```text
Base Damage / DPS
Damage Tick Interval

Beam Width

Max Range
Fade Start Fraction

Active Duration
Cooldown

Distance Falloff
Pierce Falloff

Knockback
```

Likely starting behavior:

```text
Distance falloff: none or light
Piercing falloff: none or light
```

---

# 36. Weapon 2 — Heavy Shot

## Concept

Large, powerful physical projectile.

Characteristics:

```text
slow
gravity affected
high damage
long cooldown
```

## Behavior

Projectile should:

- launch toward cursor,
- travel physically,
- fall under gravity,
- have a relatively large visible body,
- disappear after maximum traveled range.

## Parameters

```text
Base Damage

Projectile Speed
Projectile Gravity
Projectile Radius

Cooldown

Max Range
Fade Start Fraction

Distance Falloff

Pierce Count
Pierce Falloff

Knockback
```

Initial expectation:

```text
Piercing = none / low
Knockback = high
```

AoE/explosive damage is not currently required, but architecture should not make it impossible later.

---

# 37. Weapon 3 — Burst Shot

## Concept

A burst of several medium-strength projectiles.

Characteristics:

```text
medium damage
medium cooldown
no gravity
spread
light autoaim
```

This should not feel like a standard semiautomatic gun.

## Behavior

One trigger activation:

```text
fire projectile
wait Burst Interval
fire projectile
wait
...
```

for a configurable number of shots.

## Parameters

```text
Damage Per Projectile

Projectile Speed

Burst Count
Burst Interval

Cooldown

Spread Angle

Aim Assist Angle
Aim Assist Strength
Aim Assist Range

Max Range
Fade Start Fraction

Distance Falloff

Pierce Count
Pierce Falloff

Knockback
```

Expected:

```text
Distance falloff = moderate
Piercing = little or none
```

The spread + autoaim combination should be highly tunable.

Aim assist should bias projectile direction toward a nearby valid target rather than teleporting shots or guaranteeing hits.

---

# 38. Weapon 4 — Sniper Shot

## Concept

Extremely fast, precise, high-damage shot.

Characteristics:

```text
very fast
no gravity
high damage
high cooldown
strong piercing
```

## Behavior

May be:

```text
very fast projectile
```

or effectively hitscan if needed.

Prefer preserving projectile behavior if this remains reliable.

## Parameters

```text
Base Damage

Projectile Speed

Cooldown

Max Range
Fade Start Fraction

Pierce Count

Pierce Damage Multiplier

Knockback
```

Expected identity:

```text
Distance Falloff = None
Pierce Falloff = Moderate
```

Example:

```text
100
→ 80
→ 64
→ 51
```

while retaining identical first-target damage at short or long range.

---

# 39. Weapon 5 — Flamethrower

## Concept

Very short-range sustained attack.

The player must remain close to enemies.

## Damage Geometry

Use gameplay geometry such as:

```text
cone
```

or a series of overlapping circles.

Visual particles should not define gameplay collision.

## Behavior

While firing:

```text
short cone follows cursor
→ all enemies in cone may receive periodic damage
```

Recommended:

```text
Piercing = Infinite
```

because every valid enemy inside the flame volume may be hit.

## Parameters

```text
DPS / Damage Per Tick
Tick Interval

Range
Cone Angle / Width

Fade Start Fraction

Active Duration or Heat
Cooldown

Distance Falloff

Knockback
```

Expected:

```text
Distance Falloff = Strong
Pierce Falloff = None
```

Enemies close to the eye should take substantially more damage than enemies at the tip of the flame.

---

# 40. Weapon 6 — Holy Beam

## Concept

A large world-space beam extending vertically through the player's position.

This is primarily a **positioning weapon**, not an aiming weapon.

The player must carefully move into alignment before firing.

## Direction

Initial version:

```text
world up/down
```

Do not rotate with the surface normal.

The spatial rule should remain stable regardless of whether the player is:

- on the floor,
- on a wall,
- on a ceiling.

## Firing Sequence

Recommended:

```text
button pressed
→ windup
→ movement locks
→ beam activates
→ beam remains briefly
→ recovery / cooldown
→ movement restored
```

Expose whether movement lock applies during:

```text
windup
active
recovery
```

## Damage Geometry

```text
wide vertical column through player
```

Recommended:

```text
Piercing = Infinite
```

## Parameters

```text
Base Damage

Windup Duration
Active Duration
Recovery Duration
Cooldown

Beam Width
Maximum Extent / Range

Movement Lock Options

Distance Falloff
Pierce Falloff

Knockback
```

Likely:

```text
Distance falloff = None
Pierce falloff = None
```

Movement locking should use a clean external control/lock mechanism rather than modifying the movement tuning values themselves.

---

# 41. Weapon 7 — Four-Directional Holy Beam

This can be implemented as a configuration/evolution of Holy Beam rather than completely separate code.

At activation:

```text
vertical beam
+
horizontal beam
```

forming:

```text
+
```

centered on the player.

The same movement-lock mechanics should apply.

Expose:

```text
Mode:
Vertical
Horizontal
Cross
```

if useful.

The prototype primarily needs to compare:

```text
vertical only
vs.
four-direction / cross burst
```

---

# 42. Weapon 8 — Flail

## Concept

A damaging weight attached to the eye by a rope/constraint.

It follows the player's movement and gains useful motion from:

- jumping,
- falling,
- crawling,
- changing direction,
- grappling,
- moving around curved surfaces.

This weapon should strongly reward maneuvering.

## Attachment

Attach to:

```text
PlayerRoot
```

not:

- pupil,
- eye visual rotation,
- procedural legs.

## Physics

Prototype two broad behaviors if practical:

```text
mostly physical
```

and:

```text
arcade-assisted
```

Pure physics may cause the flail to become inert or hang beneath the player too often.

Allow tunable damping / assistance.

## Damage

Damage may scale with flail-head speed.

Example:

```text
damage =
    baseDamage
    + flailSpeed * velocityDamageScale
```

with:

```text
minimum damaging speed
```

if desired.

## Repeated Hits

A flail should not damage the same enemy every physics frame.

Use:

```text
Per Target Hit Cooldown
```

## Parameters

```text
Rope Length

Flail Radius
Flail Mass / Inertia

Constraint Strength
Damping
Arcade Assistance

Base Damage
Minimum Damage Speed
Velocity Damage Scale
Maximum Damage

Per Target Hit Cooldown

Knockback
```

Traditional piercing is not especially meaningful for the flail.

It may simply hit every enemy its physical head intersects.

---

# 43. Weapon 9 — Chain / Skewer

## Concept

A grapple-like offensive chain.

The chain:

```text
fires toward cursor
→ passes through / skewers enemies
→ reaches maximum distance or endpoint
→ retracts
→ pulls skewered enemies toward player
```

This is an offensive weapon and should remain conceptually separate from the player's movement grapple.

Shared helper logic is acceptable.

## Extension Phase

Chain travels outward at configurable speed.

Enemies intersected become:

```text
Skewered
```

Store references to them.

Recommended:

```text
Piercing = Infinite or configurable
```

for testing.

## Retraction Phase

When the chain retracts:

- skewered enemies are pulled toward the eye,
- they should remain associated with the chain until released.

Do not overbuild physical constraints.

A strong interpolated/force-based pull is acceptable.

## Parameters

```text
Base Damage

Range

Extension Speed
Retraction Speed

Chain Width

Max Skewered Enemies

Outgoing Damage
Optional Retract Damage

Pull Strength
Pull Speed

Pierce Falloff

Knockback / Pull Behavior
```

## Optional Pull Split

If easy to implement, expose:

```text
Pull Split
```

where:

```text
0.0 = enemies move entirely
0.5 = player and enemies move toward each other
1.0 = player moves entirely
```

However, enemy-only pulling should be the default initial implementation.

If the chain intersects terrain, its terrain behavior should be explicit and simple. For the first prototype, stopping/retracting on terrain is preferable to introducing movement-grapple behavior.

---

# 44. Weapon 10 — Short-Ranged Eye Pulse

## Concept

A very short-range radial burst directly outward from the eyeball.

This is a simple close-range defensive/offensive option.

It can act as a:

```text
"get away from me"
```

attack.

## Damage Geometry

```text
circle centered on PlayerRoot
```

All enemies within the radius are independently considered.

Piercing does not meaningfully apply because the attack does not travel through targets.

## Parameters

```text
Base Damage

Radius

Cooldown

Radial Distance Falloff

Knockback

Windup
Active Duration
```

Expected:

```text
very short range
strong knockback
```

Potential damage behavior:

```text
enemy touching eye = full damage
enemy at edge = reduced damage
```

This is a useful clean test of radial distance falloff.

---

# 45. Initial Weapon Identity Summary

| Weapon | Geometry | Range | Distance Falloff | Piercing | Pierce Falloff | Main Distinction |
|---|---|---:|---|---|---|---|
| Eye Beam | Wide line | Medium/Long | None/Light | Infinite | None/Light | Sustained aimed beam |
| Heavy Shot | Projectile | Medium | Optional Light | Low/None | Usually None | Slow ballistic power |
| Burst | Projectiles | Medium | Moderate | Low/None | Usually N/A | Spread + aim assist |
| Sniper | Line/Fast projectile | Long | None | High | Moderate | Precision and penetration |
| Flamethrower | Cone | Short | Strong | Infinite | None | Close sustained damage |
| Holy Beam | Column | Very Long | None | Infinite | None | Positioning + commitment |
| Cross Holy Beam | Cross | Very Long | None | Infinite | None | Multi-axis burst |
| Flail | Physical collider | Rope-limited | Velocity based | N/A | N/A | Movement-driven damage |
| Chain | Swept line | Medium/Long | Likely None | High/Infinite | Configurable | Skewer + pull |
| Eye Pulse | Circle | Very Short | Radial | N/A | N/A | Close radial burst |

These are prototype defaults, not final balance decisions.

---

# 46. Weapon Visual Feedback

Do not spend excessive time creating final art.

However, each weapon needs clear readable prototype visuals.

Requirements:

```text
Beam:
    visible line with width

Heavy:
    large obvious projectile

Burst:
    small visible projectiles

Sniper:
    fast projectile/trail

Flamethrower:
    flame particles / cone visualization

Holy Beam:
    large column

Flail:
    visible rope + head

Chain:
    visible chain/line

Pulse:
    expanding ring / circle
```

Opacity near range limit should be visible where applicable.

---

# 47. Weapon Cooldown HUD

Show current weapon cooldown clearly.

Possible simple display:

```text
Weapon: Heavy Shot
READY
```

or:

```text
Weapon: Heavy Shot
Cooldown: 1.3s
```

Continuous weapons may display:

```text
Active
Cooling Down
Ready
```

No polished UI is necessary.

---

# 48. Aim Assist Debugging

Burst uses light autoaim.

Debug rendering should optionally display:

```text
raw cursor direction
aim assist cone
candidate targets
selected assisted direction
```

Aim assist should be configurable enough to test whether it improves airborne firing without feeling automatic.

---

# 49. Range Debugging

Optional debug overlay should show current weapon range.

Examples:

```text
circle around player
line to cursor
beam endpoint
flamethrower cone
pulse radius
```

This is valuable when tuning:

- range,
- falloff,
- fade.

---

# 50. Damage Telemetry

Track simple session statistics.

Recommended:

```text
Damage Dealt
Damage Taken

Kills

Shots / Activations
Hits

Body Impact Hits
Body Impact Damage

Self Damage

Damage By Weapon
```

For appropriate weapons:

```text
Accuracy
```

Do not over-interpret these metrics.

They primarily exist to support tuning and comparison.

---

# 51. Prototype Input

Preserve existing movement controls.

Combat-specific inputs:

```text
Mouse / Cursor:
    weapon aim

Primary Attack:
    fire current weapon

[:
    previous weapon

]:
    next weapon
```

Use UI selection as the primary direct weapon-selection mechanism.

Do not reserve `1–9` for weapons.

Because the existing movement grapple may already use a mouse button, combat firing input must be configurable or deliberately assigned to avoid conflicts.

The combat sandbox should make it possible to test weapons while the movement grapple is enabled.

UI clicks must never trigger attacks.

---

# 52. Interaction With Movement

The prototype must explicitly test firing while:

```text
crawling
jumping
falling
double jumping
grappling
moving quickly
standing still
attached to walls
attached to ceilings
```

Weapons should generally not modify movement unless that modification is part of the weapon's intended behavior.

Examples:

```text
Holy Beam:
    deliberately freezes movement

Flail:
    movement drives weapon physics

Body Impact:
    movement determines damage
```

Most other weapons should simply coexist with movement.

---

# 53. Respawn / Reset Tools

Provide convenient sandbox controls:

```text
Respawn Player
Restore Player Health

Respawn All Enemies
Kill All Enemies

Clear All Projectiles
Clear Weapon Effects

Reset Entire Sandbox
```

Rapid resetting is important.

A full reset should also clear transient state such as:

- active chain/skewer references,
- beam state,
- flail state,
- active damage cooldown bookkeeping,
- knockback impulses,
- dead-enemy respawn timers.

---

# 54. Enemy Spawn Groups

Rather than manually managing every dummy enemy separately, use logical groups.

Example:

```text
RangeTargets
PiercingLine
DenseCluster
VerticalTargets
MovingTargets
MeleeTargets
```

Each group may have:

```text
Enabled
Auto Respawn
Movement Override
```

This will make specific tests easier.

---

# 55. Suggested Core Classes

Names may vary.

## `PlayerHealth`

Responsible for:

```text
HP
damage reception
indestructible mode
death
respawn
```

## `Health`

Generic enemy health component.

Responsible for:

```text
current/max HP
damage
death event
```

## `DamageEvent`

Useful data structure:

```csharp
public struct DamageEvent
{
    public float baseDamage;
    public float finalDamage;

    public Vector2 hitPoint;
    public Vector2 hitDirection;

    public GameObject source;

    public string damageSourceName;

    public float distance;
    public int pierceIndex;

    public float knockback;
}
```

Exact fields may differ.

## `WeaponController`

Responsible for:

```text
selected weapon
weapon switching
input forwarding
weapon UI connection
clean weapon cancellation on deselect/reset
```

## `ProjectileBase`

Responsible for common projectile behavior:

```text
movement
distance traveled
range expiration
opacity fade
collision
piercing count
distance falloff
pierce falloff
```

Specific projectiles may extend or compose this behavior.

## `DamageFalloff`

Reusable helper for:

```text
distance multiplier
pierce multiplier
```

## `DummyEnemy`

Responsible for coordinating:

```text
enemy movement
health
respawn
contact damage
debug state
```

## `EnemyRespawnManager`

Responsible for:

```text
respawn all
auto respawn
spawn-state restoration
spawn groups
```

## `BodyImpactDamage`

Separate player combat component.

Responsible for:

```text
relative velocity calculation
impact damage
impact cooldown
impact debug data
```

Do not place this inside `PlayerMotor2D`.

## `CombatDebugPanel`

Responsible for:

```text
player controls
enemy controls
weapon controls
impact tuning
debug toggles
```

---

# 56. Prototype Development Priorities

Implement in roughly this order.

## Phase 1 — Damage Infrastructure

Implement:

```text
Health
PlayerHealth
DamageEvent
Enemy contact damage
Damage numbers
Enemy health display
Respawning
```

Success condition:

> Player and enemies can reliably damage each other, die, and reset.

## Phase 2 — Weapon Framework

Implement:

```text
WeaponController
weapon dropdown
[ / ] switching
cooldowns
common range
range fade
distance falloff
piercing
pierce falloff
knockback
```

Success condition:

> Weapons can share general damage behavior without tightly coupling their unique mechanics.

## Phase 3 — Basic Dummy Enemies

Implement:

```text
Static
Patrol
Wander
Seek
```

plus:

```text
freeze
respawn
global tuning
```

Success condition:

> The sandbox can immediately test attacks against stationary and moving targets.

## Phase 4 — Core Ranged Weapons

Implement:

```text
Beam
Heavy
Burst
Sniper
```

Success condition:

> Four substantially different ranged attack styles are playable.

## Phase 5 — Close / Area Weapons

Implement:

```text
Flamethrower
Holy Beam
Cross Holy Beam
Eye Pulse
```

Success condition:

> Short-range, positioning-based, continuous, and radial attacks can be compared.

## Phase 6 — Movement-Dependent Weapons

Implement:

```text
Flail
Chain
Body Impact
```

Success condition:

> The prototype can test attacks fundamentally tied to movement and positioning.

## Phase 7 — Sandbox Expansion

Construct all major testing zones and spawn groups.

Success condition:

> A player can rapidly move between scenarios and understand how each weapon performs under very different movement/combat conditions.

---

# 57. Explicitly Out of Scope

Do not spend prototype time on:

```text
weapon rarity
weapon inventory
loot
shops
final upgrades
skill trees
final enemy art
final enemy AI
bosses
procedural encounter generation
final VFX
final audio
save systems
progression
final balancing
networking
production UI
```

Do not build additional weapon concepts beyond those in this document unless necessary for infrastructure testing.

---

# 58. Primary Questions This Prototype Should Answer

## Ranged Combat

- Is aiming while crawling and jumping enjoyable?
- How demanding is mouse aiming combined with surface movement?
- Which weapon geometries work best?
- Does light autoaim improve burst firing?
- Does the lack of a generic automatic gun make the arsenal feel more distinctive?

## Range

- How long should ranged weapons reach?
- Does opacity fading clearly communicate maximum range?
- How much distance falloff feels fair?
- Should beam-style weapons lose damage over distance?

## Piercing

- Is piercing satisfying and readable?
- How many enemies should sniper shots penetrate?
- Does damage falloff after each pierced target create good positioning decisions?
- Which weapons should ignore piercing limits entirely?

## Close Combat

- Is the flamethrower viable given the danger of close range?
- Is the short eye pulse useful as a defensive tool?
- How much knockback feels useful without trivializing enemies?

## Positioning Weapons

- Is the Holy Beam fun when movement temporarily locks?
- How long should windup/commitment last?
- Is the vertical beam more interesting than the cross version?

## Movement-Based Combat

- Is body impact damage fun enough to act as the game's melee system?
- Should body impact cause extra self-damage?
- How strongly should damage scale with velocity?
- Does the flail naturally create interesting movement?
- Does the chain feel better primarily as damage, control, or movement manipulation?

## Enemy Pressure

- Does simple seeking behavior create enough pressure for useful combat testing?
- How much contact damage is necessary?
- Is player invulnerability useful enough to leave as a permanent prototype tool?

---

# 59. Development Philosophy

This is a **combat laboratory**.

Prefer:

```text
easy to tune
easy to understand
easy to reset
easy to compare
easy to visualize
easy to extend
```

over:

```text
architecturally perfect
production-ready
beautiful
fully generalized
heavily optimized
```

However, maintain clean conceptual boundaries between:

```text
movement
aiming
weapon behavior
attack geometry
damage calculation
falloff
piercing
knockback
health
enemy behavior
visual effects
debug UI
```

The ideal outcome is that someone can enter Play Mode, switch weapons with `[` and `]`, alter a few sliders, respawn all enemies, and within a few minutes answer:

> **Does this weapon become more interesting because of the eyeball's unusual movement system?**
