# 2D Art & Animation Pipeline Setup

## Purpose

This document defines the intended 2D art and animation pipeline for the project.

It is primarily a **one-time handoff document for Claude Code**. Claude should use it to:

1. Create the initial folder structure.
2. Add any small helper files/scripts needed for the workflow.
3. Create a concise permanent reference document for itself describing the final pipeline.
4. Verify that the project structure matches the intent of this document.
5. **Delete this larger handoff document after the permanent reference document has been created.**

Do **not** keep this document around as long-term documentation. Its purpose is to communicate the design of the pipeline during initial setup.

The permanent documentation created by Claude should be substantially shorter and should contain only the rules and conventions that are useful for future work.

---

# 1. Goals

The art pipeline should support a small 2D game with:

- AI-assisted concept art.
- AI-assisted vector asset generation.
- Human cleanup and approval.
- Layered/vector source artwork.
- Unity-ready raster exports where appropriate.
- Rigged and procedural 2D animation in Unity.
- Easy experimentation with expressions, deformation, shaders, and character motion.
- Clean separation between:
  - concept/reference art,
  - editable source assets,
  - temporary AI-generated work,
  - approved production assets,
  - Unity runtime assets,
  - prototyping content.

The project should be organized so that Claude Code can help with art-related technical work without accidentally polluting or overwriting approved production assets.

The intended pipeline is roughly:

```text
Concept Art
    ↓
Approved Visual Reference
    ↓
Break Character/Object Into Production Pieces
    ↓
AI-Assisted SVG / Vector Generation
    ↓
Human Review / Cleanup
    ↓
Approved Vector Masters
    ↓
Raster Export When Needed
    ↓
Unity Rig / Animation / Shaders
    ↓
Production Prefab
```

For simple assets, the process can be shorter:

```text
Concept / Description
    ↓
Claude Generates SVG
    ↓
Review
    ↓
Approved Vector Master
    ↓
Unity Export
```

---

# 2. General Philosophy

## 2.1 Source Art and Runtime Art Are Different Things

The source asset should prioritize:

- editability,
- resolution independence,
- clean shape organization,
- reuse,
- future revisions.

The runtime asset should prioritize:

- compatibility with Unity,
- performance,
- animation requirements,
- predictable rendering.

For many assets, this means:

```text
SVG/vector source
        ↓
high-resolution PNG parts
        ↓
Unity
```

The fact that Unity may ultimately use rasterized sprites does **not** mean the source art should be raster-only.

Keeping vectors as the editable master makes it easy to:

- resize assets,
- adjust outlines,
- recolor,
- change proportions,
- export larger versions later,
- generate alternate variants,
- maintain clean animation pieces.

---

## 2.2 AI Should Work on Structured Assets

AI tools are much better at manipulating structured assets than traditional frame-by-frame animation.

Prefer workflows where Claude can reason about:

- SVG shapes,
- named layers,
- bones,
- transforms,
- animation parameters,
- procedural animation code,
- file organization.

Avoid relying on Claude to directly create large numbers of hand-drawn animation frames.

Example:

```text
hungry_brow_L
hungry_brow_R
hungry_eye_L
hungry_eye_R
hungry_pupil_L
hungry_pupil_R
hungry_nose
hungry_mouth
hungry_face
```

is much easier for an AI agent to work with than:

```text
hungry_angry_frame_001.png
hungry_angry_frame_002.png
hungry_angry_frame_003.png
...
```

---

# 3. Recommended Project Organization

The exact root structure may need to adapt to the existing repository, but the conceptual separation should remain.

Recommended layout:

```text
ProjectRoot/
│
├─ CLAUDE.md
├─ ARCHITECTURE.md
├─ ART_PIPELINE.md              # Permanent concise reference created by Claude
│
├─ ArtSource/                   # Source art; NOT Unity runtime content
│  │
│  ├─ Concepts/
│  │  ├─ Characters/
│  │  │  ├─ Hungry/
│  │  │  │  ├─ SPEC.md
│  │  │  │  ├─ references/
│  │  │  │  ├─ iterations/
│  │  │  │  └─ approved/
│  │  │  │
│  │  │  └─ EyePlayer/
│  │  │     ├─ SPEC.md
│  │  │     ├─ references/
│  │  │     ├─ iterations/
│  │  │     └─ approved/
│  │  │
│  │  ├─ Environment/
│  │  ├─ Props/
│  │  ├─ UI/
│  │  └─ Branding/
│  │
│  ├─ Vector/
│  │  ├─ Characters/
│  │  │  ├─ Hungry/
│  │  │  └─ EyePlayer/
│  │  ├─ Environment/
│  │  ├─ Props/
│  │  ├─ UI/
│  │  └─ Branding/
│  │
│  ├─ Raster/
│  │  ├─ Characters/
│  │  ├─ Environment/
│  │  ├─ Props/
│  │  ├─ Textures/
│  │  └─ Effects/
│  │
│  └─ AI_Workspace/
│     ├─ input/
│     ├─ generated/
│     ├─ previews/
│     └─ rejected/
│
└─ Assets/
   │
   ├─ Art/
   │  ├─ Characters/
   │  │  ├─ Hungry/
   │  │  │  ├─ Sprites/
   │  │  │  ├─ Materials/
   │  │  │  ├─ Animations/
   │  │  │  └─ Prefabs/
   │  │  │
   │  │  └─ EyePlayer/
   │  │
   │  ├─ Environment/
   │  ├─ Props/
   │  ├─ UI/
   │  ├─ Branding/
   │  ├─ Materials/
   │  └─ Shaders/
   │
   └─ Prototyping/
      └─ Animation/
         ├─ Scenes/
         │  └─ AnimationLab.unity
         ├─ Scripts/
         ├─ Prefabs/
         ├─ Materials/
         └─ TestAssets/
```

Do not create empty directory trees purely for completeness if the project does not yet use those categories. Create the major structure and add subfolders as they become useful.

The important conceptual boundaries are:

```text
ArtSource/Concepts/
    Human-facing visual exploration and approved references.

ArtSource/Vector/
    Editable vector masters.

ArtSource/Raster/
    Editable/source raster artwork and textures.

ArtSource/AI_Workspace/
    Temporary AI inputs and outputs.

Assets/Art/
    Production-ready Unity assets.

Assets/Prototyping/
    Experiments and test scenes.
```

---

# 4. ArtSource Should Not Be Treated As Runtime Unity Content

`ArtSource/` should ideally live outside Unity's `Assets/` directory.

This avoids:

- Unity importing every concept image.
- `.meta` files appearing for temporary AI work.
- unnecessary reimports.
- source artwork cluttering runtime asset searches.
- experimental assets accidentally being referenced by scenes.

Only assets that Unity actually needs should be promoted into `Assets/`.

---

# 5. Concept Art Organization

Concept art should be grouped by asset or character.

Example:

```text
ArtSource/Concepts/Characters/Hungry/
│
├─ SPEC.md
├─ references/
├─ iterations/
└─ approved/
```

## `references/`

External or internal visual references.

Examples:

- inspiration images,
- screenshots,
- style references,
- older drawings,
- photographs.

These are not necessarily candidates for production.

---

## `iterations/`

Generated or hand-edited concept attempts.

Examples:

```text
hungry_concept_001.png
hungry_concept_002.png
hungry_no_hair.png
hungry_bushy_brows.png
hungry_triangular_brows.png
```

Do not overengineer version naming.

The purpose of this folder is simply to preserve useful iteration history when desired.

---

## `approved/`

The currently accepted visual references.

This should remain small.

Example:

```text
Hungry_Front.png
Hungry_ExpressionReference.png
Hungry_GloveReference.png
```

Claude should treat files in `approved/` as authoritative visual references unless explicitly instructed otherwise.

---

# 6. Character SPEC.md Files

Important characters should have a small `SPEC.md`.

These files should be concise. They are not general design documents.

Their purpose is to communicate details that are difficult to infer from filenames or images.

Example:

```markdown
# Hungry

Creepy fast-food clown mascot.

## Production Pieces

- face
- nose
- left eye
- right eye
- left pupil
- right pupil
- left eyebrow
- right eyebrow
- mouth
- left glove
- right glove

## Visual Notes

- No hair.
- Large red nose.
- Thick triangular eyebrows.
- Eyes should feel empty/shark-like.
- Smile should be exaggerated and threatening.
- Cartoon shapes rather than realistic anatomy.

## Animation Requirements

Eyebrows:
- move independently,
- rotate,
- move inward/outward,
- squash/stretch.

Eyes:
- pupils track independently,
- support squinting,
- support pupil scaling.

Mouth:
- change width,
- squash/stretch vertically,
- support expressions ranging from friendly to threatening.
```

Only create a `SPEC.md` when useful.

Do not create one for every trivial prop.

---

# 7. AI Workspace

Claude should have a dedicated scratch area:

```text
ArtSource/AI_Workspace/
├─ input/
├─ generated/
├─ previews/
└─ rejected/
```

This area is intentionally disposable.

## `input/`

Contains the specific files being used for a current AI task.

Examples:

- copied approved concept image,
- temporary crop of an eyebrow,
- brief text instructions,
- reference palette,
- sample SVG.

Do not require every task to copy files here manually if Claude can directly read the approved source files safely.

The key principle is that the AI should have a clear set of inputs.

---

## `generated/`

All newly generated art should initially go here.

Examples:

```text
hungry_brow_L.svg
hungry_brow_R.svg
hungry_mouth.svg
hungry_face.svg
```

Claude must **not** directly overwrite approved vector masters during exploratory generation unless explicitly instructed.

---

## `previews/`

Optional combined previews showing generated pieces in context.

For example:

```text
Hungry_VectorPreview.svg
Hungry_VectorPreview.png
```

This is useful because individual SVG pieces may look correct alone but wrong when assembled.

---

## `rejected/`

Optional.

Use this only if retaining failed outputs is useful.

Otherwise failed experiments may simply be deleted.

Do not accumulate hundreds of useless iterations.

---

# 8. Asset Promotion

Generated assets should follow a simple promotion lifecycle:

```text
AI generated
    ↓
ArtSource/AI_Workspace/generated/
    ↓
review
    ↓
cleanup if necessary
    ↓
ArtSource/Vector/...   OR   ArtSource/Raster/...
    ↓
runtime export
    ↓
Assets/Art/...
```

The AI workspace should never be treated as production truth.

The source master directories should only contain accepted assets.

---

# 9. Vector Asset Organization

For characters, use one directory per character:

```text
ArtSource/Vector/Characters/Hungry/
│
├─ hungry_face.svg
├─ hungry_nose.svg
├─ hungry_eye_L.svg
├─ hungry_eye_R.svg
├─ hungry_pupil_L.svg
├─ hungry_pupil_R.svg
├─ hungry_brow_L.svg
├─ hungry_brow_R.svg
├─ hungry_mouth.svg
├─ hungry_glove_L.svg
└─ hungry_glove_R.svg
```

Prefer separate files when pieces need to:

- animate independently,
- deform independently,
- be recolored independently,
- be swapped,
- be reused.

If multiple shapes always move together and have no reason to be separated, they can remain combined.

Do not fragment every drawing into dozens of meaningless pieces.

---

# 10. Naming Conventions

Use stable semantic naming.

Recommended pattern:

```text
<asset>_<component>_<side>
```

Examples:

```text
hungry_face
hungry_eye_L
hungry_eye_R
hungry_pupil_L
hungry_pupil_R
hungry_brow_L
hungry_brow_R
hungry_mouth
hungry_nose
```

For variants:

```text
hungry_mouth_smile
hungry_mouth_open
hungry_mouth_frown
```

Avoid:

```text
Layer 1
Shape 5
FaceNew
FaceFinal
FaceFinal2
SpriteCopy
```

Stable semantic naming is especially important because the same name should ideally survive across:

```text
SVG file
    ↓
exported PNG
    ↓
Unity Sprite
    ↓
GameObject
    ↓
bone / transform
    ↓
animation logic
```

Claude should preserve these names whenever possible.

---

# 11. Vector Art Philosophy

Vector source art is preferred for assets built primarily from:

- simple shapes,
- clean outlines,
- curves,
- logos,
- signs,
- UI,
- cartoon faces,
- stylized characters,
- geometric props.

Raster source art is preferred for:

- grime,
- noise,
- stains,
- painterly texture,
- organic surface detail,
- complex shading,
- smoke,
- hand-painted backgrounds,
- photographic or heavily textured elements.

These may be combined.

Example:

```text
clean vector character
        +
raster grain overlay
        +
Unity shader distortion
        ↓
final stylized appearance
```

Do not force every texture into vector form.

---

# 12. AI-Generated SVG Workflow

Claude Code can directly create SVG files for sufficiently simple art.

This is encouraged.

SVG is text-based and therefore works well with coding agents.

Good candidates include:

- eyebrows,
- eyes,
- pupils,
- mouths,
- signs,
- logos,
- UI shapes,
- simple props,
- weapon silhouettes,
- basic environmental decals.

Claude should prefer:

- clean Bézier paths,
- a small number of meaningful control points,
- logical grouping,
- stable viewboxes,
- clean transforms,
- understandable SVG structure.

Avoid automatic raster tracing that creates hundreds or thousands of tiny points unless no better option exists.

The goal is **editable production vectors**, not merely images that happen to be stored as SVG.

---

# 13. Complex Concept-to-Vector Workflow

For visually complex assets:

```text
Concept image
    ↓
identify independently animated pieces
    ↓
create/vectorize each piece separately
    ↓
clean shapes
    ↓
assemble preview
    ↓
approve
```

For example, do **not** necessarily vectorize an entire flattened Hungry face and then attempt to cut it apart afterward.

Instead create:

```text
face
nose
eye L
eye R
pupil L
pupil R
brow L
brow R
mouth
```

as intentionally separated assets.

This tends to produce much cleaner animation-ready artwork.

---

# 14. Raster Export

Even when source art is vector-based, Unity may use raster exports.

Example:

```text
ArtSource/Vector/Characters/Hungry/hungry_brow_L.svg
        ↓
export
        ↓
Assets/Art/Characters/Hungry/Sprites/hungry_brow_L.png
```

Exports should have enough resolution for the maximum expected camera zoom.

Do not prematurely optimize texture resolution.

The source vectors allow larger exports later without redrawing.

---

# 15. Runtime Asset Structure

Example:

```text
Assets/Art/Characters/Hungry/
│
├─ Sprites/
├─ Materials/
├─ Animations/
└─ Prefabs/
```

Possible runtime contents:

```text
Sprites/
    hungry_face.png
    hungry_eye_L.png
    hungry_eye_R.png
    hungry_brow_L.png
    hungry_brow_R.png
    ...

Materials/
    HungryFace.mat

Animations/
    Hungry_Idle.anim
    Hungry_Friendly.anim
    Hungry_Menacing.anim

Prefabs/
    Hungry.prefab
```

Do not copy concept images into this folder.

---

# 16. Animation Strategy

The project should favor **layered and procedural animation** rather than requiring a separate fully drawn image for every expression.

For example, Hungry should ideally have independently controllable:

```text
face
eyes
pupils
eyebrows
nose
mouth
gloves
```

This makes it possible to smoothly animate values such as:

```text
Smile
BrowAnger
BrowHeight
BrowWidth
Squint
PupilSize
MouthWidth
MouthHeight
LookDirection
```

These values may be implemented using:

- Transform movement,
- rotation,
- scaling,
- Sprite Skin bones,
- Unity 2D skeletal deformation,
- shaders,
- sprite swaps where useful,
- procedural scripts.

Do not build a giant generalized facial-expression framework before it is necessary.

Prototype with the simplest mechanism that produces the desired effect.

---

# 17. Unity 2D Animation

Initially prefer Unity's built-in 2D animation tools unless they become insufficient.

Potential features include:

- bones,
- Sprite Skin,
- mesh weighting,
- skeletal deformation,
- Animator clips,
- Animation Curves,
- IK where useful,
- procedural transform control.

External systems such as Spine can be evaluated later if character authoring becomes cumbersome.

Do not design the initial pipeline around a paid external runtime unless there is a concrete need.

---

# 18. Animation Prototyping Scene

Create or reserve a dedicated animation prototyping environment:

```text
Assets/Prototyping/Animation/
├─ Scenes/
│  └─ AnimationLab.unity
├─ Scripts/
├─ Prefabs/
├─ Materials/
└─ TestAssets/
```

The Animation Lab should remain isolated from production gameplay scenes.

Its purpose is rapid iteration.

A useful long-term interface might provide:

```text
Character: [ Hungry ▼ ]

Expression
--------------------------------
Smile        [ slider ]
Brow Anger   [ slider ]
Brow Height  [ slider ]
Squint       [ slider ]
Pupil Size   [ slider ]
Mouth Width  [ slider ]

Animations
--------------------------------
[ Idle ]
[ Friendly ]
[ Menacing ]
[ Scare ]
[ Hurt ]

Procedural
--------------------------------
[ ] Look at mouse
[ ] Auto blink
[ ] Breathing
[ ] Random micro-expression
```

This does not need to be built in full immediately.

Build only enough UI to make current experiments convenient.

The important architectural point is that animation tests have a **single reusable lab scene** rather than creating a new random test scene for every experiment.

---

# 19. Character Prefabs

Production characters should eventually have a stable prefab hierarchy.

For example:

```text
Hungry
├─ Face
├─ Eye_L
│  └─ Pupil_L
├─ Eye_R
│  └─ Pupil_R
├─ Brow_L
├─ Brow_R
├─ Nose
├─ Mouth
├─ Glove_L
└─ Glove_R
```

The exact hierarchy may differ depending on the final rig.

The important feature is semantic, stable naming.

Claude should avoid relying on arbitrary transform index order.

Use named references/components where practical.

---

# 20. Source Control

Source art should be version controlled where practical.

However:

- avoid committing large amounts of disposable AI output,
- avoid retaining meaningless duplicates,
- use Git LFS for large binary source files if needed,
- SVG and Markdown should remain normal Git files whenever possible.

Consider ignoring some contents of:

```text
ArtSource/AI_Workspace/
```

depending on whether generated iterations are worth preserving.

A reasonable approach is to track:

```text
AI_Workspace/input/
AI_Workspace/previews/
```

only when useful, while treating many generated scratch outputs as disposable.

Do not make Git rules overly complex until file sizes justify it.

---

# 21. Claude's Permissions and Safety Rules

Claude may freely:

- inspect approved concept art,
- inspect vector masters,
- generate new SVGs in the AI workspace,
- create previews,
- create animation prototype scripts,
- build prototype prefabs,
- add Animation Lab tooling,
- suggest changes to source asset structure.

Claude should **not**, unless explicitly asked:

- overwrite approved concept art,
- overwrite vector masters during experimentation,
- silently replace production sprites,
- delete approved source assets,
- move experimental assets into production,
- redesign folder architecture without documenting the reason.

When uncertainty exists, preserve the existing approved asset and generate a new candidate.

---

# 22. Avoid Documentation Bloat

Do not generate README files in every folder.

Most of the folder structure should be self-explanatory.

Use:

```text
ART_PIPELINE.md
```

as the primary permanent reference.

Use character `SPEC.md` files when the art/animation requirements for a character are genuinely useful.

Use `DECISIONS.md` only in places where a non-obvious decision needs to be preserved.

Examples of things that **do not** deserve a decision entry:

- obvious file naming,
- normal Unity conventions,
- information visible from the code,
- comments better placed next to the implementation.

Examples that **may** deserve one:

- why a character uses three separate meshes instead of one,
- why a particular shader workaround exists,
- why an unusual pivot convention is required,
- why a generated asset must remain at a specific scale.

---

# 23. Recommended Initial Setup Tasks

Claude should perform the following during setup.

## A. Inspect Existing Structure

Do not blindly duplicate folders that already exist.

Integrate this pipeline cleanly with the current repository.

---

## B. Create Major ArtSource Structure

At minimum establish:

```text
ArtSource/
├─ Concepts/
├─ Vector/
├─ Raster/
└─ AI_Workspace/
```

with relevant initial character/category folders where they already have assets.

---

## C. Create Unity Animation Prototype Structure

Establish:

```text
Assets/Prototyping/Animation/
```

and prepare it for the future `AnimationLab.unity` scene.

If there is already an appropriate prototyping hierarchy, integrate with that instead.

---

## D. Add Initial Character Specs Where Useful

For known important characters such as Hungry and the player eye character, add small `SPEC.md` files if enough design information is already known.

Do not invent visual details that have not been specified.

---

## E. Document Naming Conventions

Ensure future assets consistently use semantic names.

---

## F. Preserve Existing Project Conventions

Do not reorganize unrelated assets merely to match this document.

This pipeline should improve organization without causing unnecessary churn.

---

# 24. Permanent Claude Reference

After implementing the initial structure, Claude must create:

```text
ART_PIPELINE.md
```

at the appropriate project documentation level.

This permanent document should be **much shorter than this handoff file**.

It should contain only the information Claude needs to remember in future sessions.

Recommended contents:

```text
# Art Pipeline

## Folder Roles
- ArtSource/Concepts: concepts and approved references
- ArtSource/Vector: editable vector masters
- ArtSource/Raster: editable raster masters
- ArtSource/AI_Workspace: temporary AI work
- Assets/Art: Unity production assets
- Assets/Prototyping/Animation: animation experiments

## Asset Lifecycle
Concept → approved reference → generated candidate → source master → Unity export.

## AI Rules
- Generate experimental art only in AI_Workspace.
- Never overwrite approved/source assets during iteration.
- Promote assets only after approval.

## Naming
Use stable semantic component names:
asset_component_side

## Animation
Prefer layered/procedural animation and reusable character parts.

## Documentation
Do not create per-folder READMEs.
Use SPEC.md for important characters.
Use DECISIONS.md only for genuinely non-obvious choices.
```

Claude should adapt this to the actual resulting repository structure rather than copying it literally.

---

# 25. Final Cleanup Instruction

This file is intentionally verbose because it is a one-time implementation handoff.

Once Claude has:

1. inspected the repository,
2. implemented the useful folder structure,
3. integrated it with existing project conventions,
4. created the concise permanent `ART_PIPELINE.md`,
5. verified that the permanent file accurately captures the resulting workflow,

Claude should **delete this handoff file**.

Do not preserve both documents.

The final project should retain the concise permanent reference, not this setup specification.

---

# 26. Guiding Principle

When making future art-pipeline decisions, optimize for:

```text
clear source-of-truth
        +
safe AI experimentation
        +
editable master assets
        +
stable semantic naming
        +
fast Unity prototyping
        +
minimal documentation overhead
```

The pipeline should remain lightweight.

Do not build an elaborate digital-asset-management system for a small game.

The organization exists to make iteration faster and safer, especially when Claude Code is generating or modifying assets and animation systems.
