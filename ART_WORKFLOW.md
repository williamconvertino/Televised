# Art Workflow: Concept to Animated Prototype

A step-by-step guide for going from concept art to an animated character in the Animation Lab.
Claude's detailed rules and tool reference are in `ART_PIPELINE.md`.

```
Concept art → SVG parts (Claude) → your approval → export to sprites → assemble + rig → animate in the Animation Lab
```

## Steps

1. **Concept (you).** Put the concept image in `ArtSource/Concepts/Characters/<Name>/approved/`. Tell Claude anything
   the image doesn't show, such as how it should move or which parts need to animate. Claude records that in a short
   `SPEC.md` next to it.

2. **Parts list (Claude, you confirm).** Claude proposes how to split the character into pieces, e.g. brows, eyes,
   pupils, mouth, gloves. Only pieces that need to move, swap or recolour on their own get split out. You sign off
   on the list and answer any open questions in the spec.

3. **Drawing (Claude).** Claude writes the SVG parts into `ArtSource/AI_Workspace/generated/`. It renders a preview
   of the assembled character, compares it to the concept, and iterates before showing you.

4. **Review (you).** Look at the stacked preview and individual parts. Ask for changes, or edit the SVGs yourself in
   Inkscape or Illustrator.

5. **Promote and export (Claude, on your go-ahead).** Approved parts move to `ArtSource/Vector/Characters/<Name>/`.
   The export then creates the sprites and `sprite_layout.json` in `Assets/Art/Characters/<Name>/Sprites/`.

6. **Assemble and rig (Claude).** In the Animation Lab (**Prototyping ▸ Animation ▸ Open Animation Lab**), Claude
   runs **Assets ▸ Art Pipeline ▸ Assemble Parts In Scene**. It parents the pieces (e.g. pupils under eyes) and adds
   bones or Sprite Skin wherever a part needs to bend rather than just move.

7. **Animate (Claude, you steer).** Claude writes a controller script with named parameters like `BrowAnger`,
   `Squint`, `MouthWidth` and `LookDirection`. It can drive procedural motion like blinking, breathing and
   look-at-mouse, plus a few expression presets or clips. You press Play in the lab, react, and Claude tunes.

8. **Production (later).** Once you're happy with it, it's saved as a prefab in
   `Assets/Art/Characters/<Name>/Prefabs/`.

## Your part, in short

- Provide the concept and answer questions (steps 1–2).
- Approve the art (step 4).
- Playtest and give feedback (step 7).

## Notes

- Concept art, SVG masters and AI scratch work under `ArtSource/` are not in git, so back that folder up separately.
  Exported sprites in `Assets/Art/` are tracked.
- On a new machine, run `npm install` in `Tools/ArtPipeline` once.
