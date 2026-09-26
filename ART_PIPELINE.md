# Art Pipeline

Concept art → Claude-authored SVG parts → Unity sprites → rigged/procedural animation.

## Folder Roles

| Path | Role | In git |
|---|---|---|
| `ArtSource/Concepts/<Category>/<Asset>/` | `references/`, `iterations/`, `approved/` concept art, optional `SPEC.md` | only `.md` |
| `ArtSource/Vector/<Category>/<Asset>/` | Approved SVG masters + optional `export.json` | only `.md`, `export.json` |
| `ArtSource/Raster/<Category>/` | Raster masters (grime, noise, painterly textures) | only `.md` |
| `ArtSource/AI_Workspace/` | Scratch: `input/`, `generated/`, `previews/`, `rejected/` | no |
| `Assets/Art/<Category>/<Asset>/` | Unity production: `Sprites/`, `Materials/`, `Animations/`, `Prefabs/` | yes |
| `Assets/Art/Editor/` | `SpriteLayoutPostprocessor` (import settings + assemble menu) | yes |
| `Assets/Prototyping/Animation/` | Animation Lab scene and experiments | yes |
| `Tools/ArtPipeline/` | Node scripts: `render.mjs`, `export.mjs` | yes (not `node_modules`) |

Categories: `Characters`, `Environment`, `Props`, `UI`, `Branding`. Create folders when they get content.
`ArtSource/` sits outside `Assets/` so Unity never imports it. It is local-only: back it up separately.

## Asset Lifecycle

1. Concept: `approved/` images are the authoritative visual reference. `SPEC.md` lists pieces and animation needs.
2. Generate: write SVG parts to `ArtSource/AI_Workspace/generated/`. Render and look at them (below).
3. Review: the user approves. Then move approved parts to `ArtSource/Vector/<Category>/<Asset>/`.
4. Export: `node Tools/ArtPipeline/export.mjs <Category>/<Asset>` → `Assets/Art/.../Sprites/`.
5. Rig/animate in the Animation Lab, then build the production prefab in `Assets/Art/.../Prefabs/`.

Simple assets can skip the concept step.

## AI Rules

- Experimental output goes only in `AI_Workspace`.
- Don't overwrite `approved/` concepts, vector masters, or exported sprites unless asked. When unsure, make a new
  candidate next to the old one.
- Run `export.mjs` only when asked, because it replaces production sprites. Use `--dry` to show what would change.
- Don't invent visual details that the concept or SPEC doesn't give. Ask instead.

## SVG Authoring

- One file per independently animated/recolored/swapped piece. Shapes that always move together stay combined.
- **Every part of an asset uses the same canvas** (identical `viewBox`, e.g. `0 0 1024 1024`) and is drawn in
  place, so stacking the files reproduces the assembled asset. The exporter crops each part and records its offset.
- Pivot: `data-pivot="x y"` on the root `<svg>`, in canvas units (brow: inner end, pupil: centre, glove: wrist).
  Default is the centre of the cropped part.
- Hand-authored paths: few meaningful Bézier points, named `id`s on groups, no auto-tracing, no raster embeds,
  no text (convert to paths). Filters and gradients are OK when needed.
- Colors must match the approved concept. Render a stacked preview and compare it against the concept before
  proposing parts.

## Tools (`Tools/ArtPipeline`, run `npm install` there once)

```
node Tools/ArtPipeline/render.mjs <svg|dir>... [--scale n] [--bg "#777"] [--out dir]
    PNG per SVG, default into AI_Workspace/previews. Use this to look at your own SVGs.
node Tools/ArtPipeline/render.mjs --stack <out.png> <svg|dir>... [--scale n] [--bg color]
    Assembled preview. A directory's parts stack in its export.json `layers` order.
node Tools/ArtPipeline/export.mjs <Category>/<Asset> [part...] [--dry]
    Cropped PNGs + sprite_layout.json into Assets/Art/<Category>/<Asset>/Sprites/.
```

`export.json` (optional, in the vector folder): `scale` (px per canvas unit, default 2),
`unitsPerWorldUnit` (canvas units per Unity unit, default 100), `padding` (default 2), `origin` (canvas point for
local 0,0, default centre), `layers` (part names bottom→top, sets sorting order). Raise `scale` for more
resolution. World size stays the same because PPU = scale × unitsPerWorldUnit.

In Unity, `SpriteLayoutPostprocessor` applies pivot and PPU from `sprite_layout.json` on import. Don't edit those
settings by hand, because they're overwritten. **Assets ▸ Art Pipeline ▸ Assemble Parts In Scene** (select the
layout json) creates one positioned, sorted SpriteRenderer per part. Parent them (pupil under eye) afterwards.

## Naming

`<asset>_<component>[_<side>][_<variant>]`: lowercase asset and component, `_L`/`_R` sides, e.g. `hungry_brow_L`,
`hungry_mouth_open`. The same name is used for the SVG, PNG, Sprite and GameObject/bone. Never `Layer 1`, `FaceFinal2`.
Refer to parts by name or serialized reference, not by child index.

## Animation

- Prefer layered, procedural animation on reusable parts (transforms, Sprite Skin bones, shaders, sprite swaps)
  over drawn frames. Expose named parameters (`BrowAnger`, `Squint`, `MouthWidth`…).
- Use Unity 2D Animation (installed) before considering Spine or similar.
- Start with the simplest mechanism that works. Don't build a general expression framework before it's needed.
- Experiments go in the single Animation Lab (**Prototyping ▸ Animation ▸ Open Animation Lab**), under `Stage`.
  Its builder is `Assets/Prototyping/Animation/Scripts/Editor/AnimationLabBuilder.cs`. Rebuilding wipes the scene,
  so extend the builder for anything that should persist. Add lab UI only when an experiment needs it.

## Documentation

No per-folder READMEs. Use `SPEC.md` for important characters and `DECISIONS.md` only for non-obvious choices
(odd pivots, shader workarounds, fixed scales).
