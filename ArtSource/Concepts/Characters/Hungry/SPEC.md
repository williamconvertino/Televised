# Hungry

Boss. Creepy fast-food clown mascot: a floating head with two disembodied floating gloves (no body).
Reference: `approved/Hungry_ConceptSheet.png` (main pose, three expression variants, glove poses).

## Production Pieces

- face (pointed, shield-shaped mask; off-white)
- nose
- eye_L, eye_R (black sockets)
- pupil_L, pupil_R (thin white rings)
- brow_L, brow_R
- mouth (jagged teeth; red curled corners)
- glove_L, glove_R (white, dark cuff opening, red inner cuff). The concept shows several poses (claw, point,
  open, reach), so pose variants will likely be sprite swaps: `hungry_glove_<pose>`.

## Visual Notes

- No hair.
- Large red nose.
- Thick triangular eyebrows, very dark brown.
- Eyes should feel empty/shark-like.
- Smile should be exaggerated and threatening.
- Cartoon shapes rather than realistic anatomy.
- Just a face and two gloves, nothing else.
- Very expressive, with a wide range of expressions (mostly evil). The mouth and brows carry the expression.
  The eyes stay mostly empty black sockets, and the pupils are faint rings that shrink to pinpricks.

## Animation Requirements

Eyebrows: move independently, rotate, move inward/outward, squash/stretch.

Eyes: pupils track independently, support squinting, support pupil scaling.

Mouth: change width, squash/stretch vertically, range from friendly to threatening.

## Parts (as built)

Canvas 1200x1000, 150 canvas units per world unit. `_L`/`_R` mean screen left/right.

- `hungry_face`: the mask, with soft socket rims baked in.
- `hungry_eye_L/R`: black sockets. Each one is also the SpriteMask for its pupil, lid and cheek.
- `hungry_pupil_L/R` (ring) and `hungry_pupil_L/R_arc` (the concept's gleeful closed-eye arc).
- `hungry_lid_L/R` and `hungry_cheek_L/R`: skin-coloured upper and lower lids that slide into the masked socket to squint.
- `hungry_brow_L/R` (sharp, as in the concept) and `hungry_brow_L/R_soft` (arched, for sad, happy and surprised).
- `hungry_mouth_<shape>`: grin, grinwide, smirk, smirkside, smile, frown, frownopen, snarl, roar, wavy, o. Each one is
  a full sprite swap with its corner curls included, and they all share the pivot 600,580.
- `hungry_nose`.
- `hungry_glove_<pose>`: open, claw, point, fist. They are drawn once as the screen-right hand, and the left glove
  is the same sprite mirrored.

## Resolved Questions

- Mouth corners are part of each mouth sprite, so they follow width and open scaling.
- Gloves are mirrored, not drawn separately.
