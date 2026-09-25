# Movement Playtest Guide

This playtest is meant to find a strong starting point: which basic movement modes and features to keep
investigating and tuning for the final game.

- **Scene:** `Scenes/PlaytestLevel.unity`, with nine courses: General, Double jump and Grapple, each at Easy, Medium and Hard. To build or open it: **Prototyping ▸ Movement ▸ Build / Open
  Playtest Level**.
- **Code:** `Scripts/Playtest/`. The configs are defined in `PlaytestConfigCatalog.cs`.
- **Results:** `Playtest/Results/`, which contains `ratings.csv`, `summary.md` and a `sessions/` folder.

---

## 1. Design-space analysis

Every movement setting was sorted into one of three groups:

- **Fixed:** the same in every config, because the decision has already been made.
- **Minor:** tuned once for comfort in the shared baseline. These aren't something the test varies.
- **Major:** the settings the test varies.

| Lever | Class | Treatment in this test |
|---|---|---|
| WASD input + `ScreenRelativeLocked` | Fixed | Always on (the most comfortable, per earlier testing) |
| Player size (radius 0.5) and crawl speed (5) | Fixed or minor | Constant, except in the momentum config |
| Grapple miss behavior, grapple range (9), aim assist, cooldowns, jump buffer, landing grace, snap strength, normal smoothing and sample spacing, reattach cooldown | Minor | Tuned once in `ApplyBaseline` |
| **Jump direction mode** (6 modes) | **Major** | All 6 tested, plus 2 extra clamp angles |
| **Attachment mode** (Strict / Nearest / Magnetic) | **Major** | All 3 tested |
| **Candidate selection** (NearestGap / WeightedScore) | **Major** | Both tested, with cursor-led and velocity-led weights |
| **Air control** (none / default / strong) | Major feel lever | 3 levels |
| **Jump arc** (gravity and jump speed) | Major feel lever | Heavy, default and floaty |
| **Crawl momentum** (acceleration, and jumps inheriting crawl speed) | Major feel lever | 1 config |
| **Speed-scaled attach range** | Major feel lever | 1 config: large radius when slow, small when fast |
| **Double jump** | Feature | 2 configs |
| **Grapple:** Pull / Swing / SwingPull | Feature | 2 configs per mode |

**How the configs are built.** The 20 core configs have neither double jump nor grapple. Each changes one
major lever away from the baseline (C01), except for two deliberate "extreme combination" configs.

**Feature configs** use two fixed bases, so a feature can be judged without testing every combination
again:
- **Base A = C01:** clamped cursor jump, the aim-driven option.
- **Base B = C03:** smoothed surface-normal jump, with no aiming.

Comparing a feature config with its base shows what the feature adds. Comparing A with B shows whether
the feature suits aimed or surface-driven jumping.

## 2. Configurations (28)

Unless a row says otherwise, every config uses the baseline:
- Movement: WASD + ScreenRelativeLocked, crawl speed 5 (fast acceleration)
- Jumping: jump speed 9, gravity 16, air control 0.25
- Attachment: Nearest, attach distance 0.5, NearestGap selection

| ID | Title | Jump | Attach | Select | Other levers | Question it answers |
|---|---|---|---|---|---|---|
| C01 | Baseline | Clamped cursor 55° | Nearest | NearestGap | – | Reference point |
| C02 | Local normal | LocalSurfaceNormal | Nearest | NearestGap | – | Is aiming through the surface's curve fun or frustrating? |
| C03 | Smoothed normal | SmoothedSurfaceNormal | Nearest | NearestGap | – | Does smoothing noticeably improve on C02? |
| C04 | Free cursor | CursorDirection | Nearest | NearestGap | – | Full 360° aim: expressive or too demanding? |
| C05 | Tight cone | Clamped 30° | Nearest | NearestGap | – | Where is the clamp's sweet spot? |
| C06 | Wide cone | Clamped 80° | Nearest | NearestGap | – | Nearly free aim, but never into the surface |
| C07 | Auto-target | NearestSurface | Nearest | NearestGap | – | Does automatic targeting fight the player's intent? |
| C08 | Assisted | AssistedTarget (30°, 0.6) | Nearest | NearestGap | – | Does the assist help, or feel like losing control? |
| C09 | Strict | Clamped 55° | **Strict** | NearestGap | – | How much does losing the attach radius hurt? |
| C10 | Magnetic | Clamped 55° | **Magnetic** | NearestGap | – | Does magnetism feel good, or does it steal jumps? |
| C11 | Magnetic + cursor-weighted | Clamped 55° | Magnetic | **Weighted (cursor 1.2)** | – | Can cursor intent decide which surface grabs you? |
| C12 | Velocity-weighted | Clamped 55° | Nearest | **Weighted (velocity 1.2)** | – | Prefer the surface you're flying toward? |
| C13 | Speed-scaled attach | Clamped 55° | Nearest, 1.0 → ×0.35 at 10 u/s | NearestGap | speed-scaled attach | Forgiving landings without accidental fly-by grabs |
| C14 | Strong air steering | Smoothed normal | Nearest | NearestGap | air control 0.75 | Can air control replace aiming? |
| C15 | Committed cursor | CursorDirection | Nearest | NearestGap | air control 0 | Fully ballistic jumps: satisfying or punishing? |
| C16 | Momentum crawl | Clamped 55° | Nearest | NearestGap | speed 7, slow acceleration, jumps inherit 70% of crawl speed | Does momentum make movement feel alive, or less precise? |
| C17 | Heavy | Clamped 55° | Nearest | NearestGap | gravity 28, jump 12 | Are snappy arcs better for precision? |
| C18 | Floaty | Clamped 55° | Nearest | NearestGap | gravity 9, jump 7 | Relaxing, or sluggish? |
| C19 | Normal + strong magnet | LocalSurfaceNormal | Magnetic (2.0 / 35) | NearestGap | – | Can magnetism make up for having no aim? |
| C20 | Full auto | NearestSurface | Magnetic | NearestGap | – | With maximum automation, is there still a game here? |
| C21 | Double jump A | = C01 | | | + 1 air jump, cursor cone 45° | Is a mid-air correction a good addition to aimed jumps? |
| C22 | Double jump B | = C03 | | | + 1 air jump, WASD direction | Can the double jump be the aiming tool? |
| C23 | Grapple Pull A | = C01 | | | + grapple, Pull | Point-to-point zipping alongside aimed jumps |
| C24 | Grapple Pull B | = C03 | | | + grapple, Pull | The grapple as the only aimed movement tool |
| C25 | Grapple Swing A | = C01 | | | + grapple, Swing | Pendulum swinging: skillful, or hard to control? |
| C26 | Grapple Swing B | = C03 | | | + grapple, Swing | |
| C27 | Grapple SwingPull A | = C01 | | | + grapple, SwingPull | Controlled reel-in with the freedom to swing |
| C28 | Grapple SwingPull B | = C03 | | | + grapple, SwingPull | |

**Before playing, testers only see:**
- the config ID, and
- a neutral description of the controls, for example "Space jumps toward your cursor, but only within a
  cone."

Titles and hypotheses stay hidden until the results screen.

## 3. The courses

The scene holds **nine courses**: three course types × three difficulties.

| Course type | Played by | Purpose |
|---|---|---|
| **General** | Core configs C01–C20 (and feature configs if you turn feature courses off) | Diverse plain-jump platforming |
| **Double jump** | C21–C22 | Can't be finished without the double jump |
| **Grapple** | C23–C28 | Can't be finished without the grapple |

Every course runs left to right, has a checkpoint after each section, and ends at a gold goal. Red shapes
are hazards: touching one sends you back to your last checkpoint. There is lava below every course.

### Module library

Courses are assembled from modules. Each module is designed to **force one kind of movement**, so
sections can't be skipped by jumping over them. The modules are in `Editor/PlaytestLevelBuilder.cs`.

| Module | What it forces |
|---|---|
| Gaps | Plain jumps between platforms, sometimes over hazard bumps |
| Steps | Crawling up and down walls (floor → wall → floor transfers) |
| Sparse blobs | Landing on **every** blob. The spacing makes skipping one longer than a jump. Harder versions add **spiked tops** (land on the sides and wrap underneath) and a **spiked sky** that stops floaty arcs |
| Tunnel | A low tunnel with a spiked roof top. **Pits** in the floor must be crossed by crawling the roof. **Roof spikes** after each pit force a drop back to the floor. A **mouth spike** forces crawling in rather than jumping in |
| Wrap circle | A big circle with a spiked top between two platforms: land on its side, press **S** to crawl underneath, and jump off the far side |
| Floating columns | Spiked tops: land on the left face, go under, and jump from the right face |
| Chimney | Climb a shaft where hazard rungs stick out from alternating walls. You must switch walls to pass each rung, and you can't fall straight through |
| Drop shaft | The same, going down. The far wall's top is spiked so you can't skip over it |
| Underside chain | Slabs with spiked tops: crawl their undersides and use hanging circles to get between them |
| Double-jump gap | 8–9.5 wide, past single-jump range (about 6.4) but within double-jump range (about 11) |
| Double-jump ledge | A cliff 4.2–4.7 high with a spiked face. Too tall for one jump (reach about 3); a double jump gets you onto it |
| Grapple gap | 9.5–11.5 wide, with an anchor circle above the middle. Pull to it, or swing from it |
| Grapple cliff | A spiked cliff face with a safe lip overhanging the approach, out of jump reach. Grapple the lip, then reel or pull in |
| Ceiling run | A long gap under a high ceiling: grapple up and crawl across. Spikes hanging from the ceiling (Medium and Hard) force a drop and a mid-air re-grapple |
| Grapple chain | Anchors with spiked tops, 8.6 apart. That's past plain-jump range but within grapple range, which the shared baseline sets to 9 |
| Ferry *(moving)* | A platform shuttling across a 16-wide gap, with spikes hanging just above ride height. Stand still and ride it; jumping while riding kills you |
| Pistons *(moving)* | A corridor with a spiked ceiling and hazard pistons rising and falling out of phase. Crawl under each piston while it's up |
| Windmill *(rotating)* | A 9-long rotating arm over lava with a deadly hub. Ride one half round to the far side (you'll end up underneath it), then drop to the exit |
| Bobbing stones *(moving)* | Stepping stones bobbing up and down out of phase: time each jump |
| Elevator *(moving)* | Ride a lift up beside a spiked cliff, then hop onto the ledge |

**Moving surfaces.** A surface that moves or rotates carries the player along. Jumping off one adds its
velocity to the jump (`inheritPlatformVelocity`, 1 in the baseline), so you get flung off a spinning
arm.

### Course recipes

| Course | Easy | Medium | Hard |
|---|---|---|---|
| General | Gaps & steps → sparse blobs (4) → tunnel with 1 pit → wrap circle → 2 columns → chimney with 3 rungs → underside chain of 2 | The former Medium and Hard merged, each section type once at the harder settings: tight gaps & steps → all-spiked blobs (6) + spiked sky → 24-long tunnel with 3 pits, roof spikes and mouth spike → wrap circle + 4 columns → 27-tall chimney with 7 rungs → underside chain of 4 → 18-deep drop shaft | **Dedicated skill sections:** long, precise jumps (gaps 3.4–3.8 onto 2.6–3-wide platforms with spikes) → tiny spiked targets (r 0.55, gap 2.8) → ferry → pistons → tight 31-tall chimney (width 3.4, 9 rungs) → windmill → ceiling marathon (5 slabs, gap 3.8, tiny circles) → bobbing stones → elevator + 22-deep drop shaft |
| Double jump | Gap 8 → ledge 4.2 → gap 8.2 (rising) | Gap 8.5 → ledge 4.5 → blobs → gap 8.8 → ledge → gap 9 | Gap 9.2 → ledge 4.7 → tunnel → gap 9.3 (rising) → ledge → wrap → gap 9.5 |
| Grapple | Cliff 6 → gap 9.5 → ceiling 11 | Gap 10.5 → cliff 6.5 → ceiling 14 with 1 spike → chain of 2 | Chain of 3 → cliff 7 → ceiling 16 with 2 spikes → gap 11.5 → spiked blobs → chain of 3 |

**Solvability:**
- **General courses** can be finished with plain jumps in every core config. Normal-only jump modes
  get angled launches from rounded corners, circles and wall faces.
- **Hard courses** are meant to be tough, and some combinations may be very hard. For example,
  NearestSurface auto-jumps inside a chimney may pick the floor instead of the opposite wall. That is
  useful data.
- **The skip button** keeps testers from getting stuck.

**Not play-tested yet.** The layouts were designed numerically, from jump range, reach, grapple range and
clearances. Before real sessions, the facilitator should do one pass of each course with C02 (normal-only
jumps), C09 (strict attachment) and one grapple mode. Every module's dimensions are in
`PlaytestLevelBuilder.cs`. After an edit, run **Prototyping ▸ Movement ▸ Build Playtest Level** again.

## 4. Running a session (facilitator checklist)

1. **Rebuild the scene if needed.** If the scene was built before courses existed, rebuild it first:
   **Build Playtest Level**. An out-of-date scene shows a message telling you so.
2. Open `PlaytestLevel.unity` and press Play. Maximize the Game view, which makes it easier to play.
3. On the setup screen, set these:
   - **Tester name:** use the same name for the same person across sessions. The "Δ" statistic depends
     on it.
   - **Configs:** choose categories and a count. Configs are played **in catalog order** by default:
     C01 Easy → C01 Medium → C01 Hard → C02 Easy, and so on.
     - **Shuffle config order** (off by default) randomizes the order of configs. Each config still goes
       through its difficulties easiest first.
     - When playing a subset, **Pick the least-rated configs** (off by default) chooses the configs with
       the fewest ratings so far, to even out coverage across testers. When it's off, the subset is the
       first N configs.
   - **Difficulty:** tick Easy, Medium and/or Hard. Configs are played **one at a time**, going through
     every ticked difficulty in order (Easy → Medium → Hard), with a rating after each level. A tester
     who doesn't want to go on to Medium or Hard can stop that config, and keeps the ratings already
     given.
   - **Feature courses:** on by default, so double-jump and grapple configs play the courses that
     require their feature. Turn it off to play them on the General course, where they can be compared
     with C01 and C03 on the same course.
4. For each level:
   1. The intro screen shows:
      - the config ID
      - the course and difficulty ("level 2 of 3 for this config")
      - the controls
   2. The tester presses **Enter** and plays until the goal, or presses **Esc** to stop early and rate.
   3. They rate it from 1 to 10, with an optional comment.

   **Skip options:**
   - **Skip this level (no rating):** on the intro screen, and in the HUD while playing. It moves on to
     the next level, and its rating is skipped automatically.
   - **Skip the rest of this config:** on the intro screen. It jumps to the next config.
   - **Skip rating:** on the rating screen, for a level that was played but that they don't want to
     rate. The level's metrics are still kept in the session JSON.
   - **Stop this config here:** a checkbox on the rating screen. After submitting or skipping the rating,
     the config's harder levels are skipped.
5. At the end, the tester can leave overall comments. **Save and start a new session** resets for the
   next person.

**Hotkeys are disabled on the setup, rating, finished and results screens,** so typing a name or
comment can't trigger R, N, C, the scroll-wheel zoom and so on.

**During play:**
- **R:** back to the last checkpoint
- **Backspace:** restart the course
- **N:** skip to the next checkpoint, or to the end if only the goal is left, which then goes to the
  rating screen. There's also a HUD button for this. Skips are recorded, and skipping the last section
  counts as **not finished**.
- **Skip level:** a HUD button that leaves the level without rating it.
- **Esc:** stop and rate
- **F5:** full debug overlay, for the facilitator only

**Facilitator tips:**
- Don't explain which config is "good".
- Do answer control questions.
- If a tester has been stuck for about 2 minutes, suggest **N**.

## 5. Data and analysis

- **`ratings.csv`:** one row per rated run. Its columns include **difficulty**, **course** and **skips**.
  An older CSV is migrated automatically (a `.bak` copy is kept); its rows are labelled
  difficulty `Easy(v1)`.
- **`sessions/*.json`:** the full record of each session. Besides the planned levels, ratings and
  overall comment, it includes:
  - `unratedRuns`: levels that were played but whose rating was skipped. Metrics are kept; rating is 0.
  - `skippedLevels`: levels skipped without being played to the end, with the reason.
- **`summary.md`:** regenerated after every rating. It contains:
  - a per-config table across all courses
  - a per-config × course/difficulty table
  - axis tables, including the raw mean per course and difficulty
  - every comment
  - The in-game **View results** screen shows the same tables.

**How to read it:**
- **Sort by Δ.** Δ is a rating minus that tester's average **on the same course and difficulty**. It
  corrects for harsh or generous raters, and stops "played on Hard" from counting against a config.
- **Look at course/difficulty separately.** The raw means per course and difficulty tell you whether a
  difficulty was fun overall. Δ within each one tells you which configs did best there.
- **Compare feature configs with their base:**
  - C21, C23, C25 and C27 against C01
  - C22, C24, C26 and C28 against C03

  On feature courses the comparison is indirect, because the bases can't finish those courses. To
  compare them directly, run some sessions with feature courses off.
- **Use the objective metrics** (finish rate, deaths, skips, time) to separate "felt bad" from "was
  hard".

**To start fresh,** delete or move `Playtest/Results/` between test rounds.
