# Story figures with their own look and living faces · 2026-11-23

Owner remark of 2026-11-23, after seeing Orsel at the mill: the main characters had "lifeless standard figures".
Story figures should have unique, beautiful designs. The owner's follow-ups the same day set the direction:

- use the whole BoZo pack;
- aim for the dark fantasy / Dark Souls look, with self-made parts where needed;
- "Viel Beine und junge Gesichter sind gut" and "Sexy ist immer besser";
- more textures, for example coarse heavy linen;
- for Orsel, the garter-shorts look (owner's pick among three renders).

Everything stays within the 16+ rating: no nudity.

## What changed

### Expressions kept

Story recipes now carry `"expressions": true`. The baker then keeps the pack's `Expression_*` blend shapes in the game
mesh: 45 shapes, and 62 for Dorn, whose beard brings its own. Every other slider is still baked flat. Figures without the
flag stay as they were.

### Living faces

Faces come alive in `FaceLifePresentation` (presentation only; the Core knows nothing of faces):

- Every story figure blinks: the lids close for 0.13 s every 2.4 to 6 s.
- Each figure keeps a mood that fits its role:
  - Ilyan: sad brows.
  - Mara: a slight smile.
  - Dorn: grim, lowered brows.
  - Orsel: stern.
  - The road hunter and the companion: watchful, slightly lowered brows.
- The mouth opens and closes while the figure's line is heard (the atmosphere speaker) and while Orsel answers in her
  panel. It pulses evenly; there is no lip sync to a recording.
- Parts with mouth shapes of their own follow the face. Dorn's beard brings 17 mouth and jaw shapes named `…_1`; they
  move with his mouth when he speaks.
- Faces off screen rest.
- The road hunter and the companion now register their faces too, as the camp figures and Orsel already did.

### Own fabrics

`Tools/Art/fabric/make_fabric.py` writes tileable grey height maps. The output is deterministic: the same seed gives the
same file.

- `AC_LinenCoarse`: coarse heavy linen. A plain weave of 40 uneven threads with slubs, fibre noise and grime
  (seed 20261123).
- `AC_LeatherWorn`: worn leather. A pebbled grain, soft dents, and creases and scratches (seed 20261124).

A recipe outfit names a fabric with `fabric`, `fabricStrength` (0 to 1) and `fabricScale` (repeats per atlas, above 0
and at most 64). The baker lays the fabric over that part's pixels in the colour atlas, after the brightness lift:

- **Colour:** the weave scales each pixel by `lerp(1, 0.72 + 0.42·h, strength)`.
- **Normals:** the normal map leans with the fabric's slopes, by `1.1·strength`.

The part's pixels are the atlas texels its UV triangles cover, widened by 2 px. Skin is then taken out in two steps:

- Texels on the body's or the head's own UV islands are removed. This covers skin pieces that outfits borrow from the
  body.
- Texels that look like the figure's skin are removed too. Tight outfits share the body's atlas layout and are painted
  over its skin, so where they are open (a cut-out, a bare shoulder) their own triangles carry skin. Such a texel has the
  hue and saturation of the recipe's skin tones (tone, shade and highlight of body and head) and is at least half as
  bright as the brightest tone. Cloth colours are pre-darkened and stay below that.

The texture stamp names the fabric settings and the mask version (`fabric=v3`), so older derived textures are rebuilt.

### New looks

Each look is a recipe in `figures.json`. Cloth colours are pre-darkened for the baker's lift (brightness to the power
0.6), so they show muted and dark in game; skin stays as given.

- **Orsel** (owner's pick):
  - short battle dress of coarse linen with an open front skirt;
  - laced garter shorts with thigh-high stockings;
  - dark hood with a blood-red lining, linen wraps on her hands;
  - long ash-silver hair.
- **Ilyan:** a young, gaunt wanderer.
  - Ash-grey hooded cape of coarse linen down to the ankles.
  - Soot-brown linen tunic with a leather belt, knee-length linen trousers over bare shins, laced boots.
  - Linen wraps on his hands, dark hair with a side parting.
- **Mara:** a young woman who holds the camp together.
  - Off-shoulder blouse of pale coarse linen with a cut-out, laced oxblood leather bodice.
  - Short, frayed leather shorts, tall fencing boots, leather arm guards.
  - Dark red hair in a high ponytail.
- **Dorn:** a young, broad guard, taller than the villagers.
  - Oxblood surcoat under a blackened breastplate with a bronze trim.
  - Fur-trimmed shoulder pieces.
  - Blackened greaves, gauntlets and sabatons; red belt.
  - Kettle hat and short dark beard, as before.
- **The road hunter** (also the companion): a young ranger.
  - Moss-grey linen hood and a short oxblood scarf.
  - Leather jerkin with a blackened chest guard and faded linen sleeves.
  - Dark travel trousers, tall forest boots, archer's gloves.
  - Short dark hair with a wolf tail.

During the work two figures first read as women: Ilyan, with long locks and bare thighs, and the hunter, with long bangs
under the hood. Both now have male haircuts and builds.

### Not changed

- **The hero.** The player figure is built by the combat art chain, and armour fits hang on it.
- **Residents and patients.** They keep the resident pool.

## Checks

All Unity runs used Unity 6000.6.8f1 with the Unity CLI in the live editor, not a Director job.

| Run | Code | Result |
|---|---|---|
| PlayMode `OrselBlinksKeepsHerMoodAndMovesHerLips` | `f07ea76d108c06c8b61f03d118ed36939ff2ff86` | PASS (39 s) |
| PlayMode `RescuedCompanionHasRealModelControlsCombatAndSavedIndividualProgress`, with the companion's face | `f07ea76d108c06c8b61f03d118ed36939ff2ff86` | PASS (21 s) |
| EditMode `BozoFigure*` | `f07ea76d108c06c8b61f03d118ed36939ff2ff86` | 20 / 20 PASS |
| PlayMode `CampFiguresWearTheMoodOfTheirRole` (new), with Dorn's beard following his mouth | `9720e977ce6979d4dcf740956677d9cccb0eaf5d` | PASS (18 s) |
| Full PlayMode suite | `9720e977ce6979d4dcf740956677d9cccb0eaf5d` | 181 PASS / 0 FAIL / 28 ignored |
| Full EditMode suite | `9720e977ce6979d4dcf740956677d9cccb0eaf5d` | 254 PASS / 0 FAIL / 28 ignored |

The Core is unchanged, so `Tests/CoreVerification` was not run again.

`BozoFigureVisualTests` now accepts kept shapes only when all of them are `Expression_*` and include the blink shape;
every other slider must still be baked.

The renders of the five figures (face, front, side) are private, because they show purchased pack art. The owner
received them as a page.

## Limits

- The mouth does not follow real speech.
- The pack's decal and pattern layers and its face details (scars) are not used. The fabrics are our own maps.
- The skin test for fabrics works by colour. A cloth part painted in the figure's own skin tone and brightness would lose
  its fabric where it matches.
- The hero keeps his look until his combat figure and armour fits are rebuilt.
- These looks are an interim. Later the same day the owner said the original vision is a realistic, misty dark-fantasy
  look, and that the stylized packs pulled the game toward a cartoon style. The owner's order: first the quality epic
  #48 with the current packs, reworked; the design change comes after it.
