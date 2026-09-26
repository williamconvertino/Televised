# Hungry: Decisions

- **Masked lids for squints.** Each eye socket sprite is also a SpriteMask. The lids, cheeks and pupils are
  masked to the socket. A squint slides a skin-coloured lid down into the socket (and rotates it for angry or
  droopy slants), so no per-expression eye art is needed.
- **One glove drawing.** Gloves are drawn as the screen-right hand. `Glove_L` is the same sprite under a root with
  x scale −1. Expression glove offsets are mirrored, so +x always means away from the face.
- **Mouths are whole swaps.** Mouth shapes change topology (open, closed, O, wavy), so each is its own sprite with a
  shared pivot. Width and open are transform scales. A short squash "pop" hides the swap.
- **Parts are generated.** The SVGs come from `ArtSource/AI_Workspace/input/hungry_generate.mjs` (teeth zigzags
  and glove sausages are computed). Edit the script and re-run it, or hand-edit the SVGs in Inkscape. Once
  hand-edited, don't regenerate them.
