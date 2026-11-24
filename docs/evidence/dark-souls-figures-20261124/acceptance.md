# Dark-Souls looks for the figures, the Ember Warden's own figure and the village mood · 2026-11-24

Owner brief of 2026-11-24 ("Komplettes Dark-Souls-Szenen- & Charakter-Design, 6 Rollen"): the bright anime colours become
dark, desaturated, rough materials with wear; six roles are restyled step by step with a preview after every step
(hero, hunter, armoured village guard, Mara, Orsel, villagers), the village gets cold moonlight, warm light islands and
ground fog. Design and style only: no quest changes, every figure stays at its quest place. On the same day the owner
gave his own model "Darkfire" to the game as the Ember Warden ("Glutwächter") at the end of the crypt.

This block is stacked on the crypt enemies (PR #136) and contains their commits.

## What changed

### The six roles

| Role | Visual | Look |
|---|---|---|
| Hero | `Hero` → `EntryHero`, `EntryCombatHero` | charcoal coat with tarnished steel plates, soldier's pauldrons of tarnished steel, faded crimson hooded cape, dark trousers, tall blackened boots; face and hair as before |
| Hunter | `RoadHunter` | hood and cape of worn blackened leather, leather vest, quiver on the back |
| Village guard | `Guard0`, `Guard1` | full tarnished plate, closed helm, chain skirt, tabard shreds; 2.0 m, broader than the hero |
| Mara | `EntryNpc1`, `Npc1` | cut of 2026-11-23 kept (owner decision); black linen blouse with faded gold edges, wine-red bodice; amulet on a neck chain, slung belt with brass buckle, coin purse, two faintly glowing flasks |
| Orsel | `Orsel0` | cut of 2026-11-23 kept; grey coarse linen, moss-green hood and sleeves; crook across the back, burning lantern in the left hand, herb pouch and flask at the belt |
| Villagers | `Villager0..2` and the trades | dark earth tones with a weave; a sack on the back, a wooden bucket, a walking stick |

Ilyan and Dorn keep their design and wear it darker (grade 0.7 / 0.8). The ten named village and trade figures and all
180 residents of the pool wear the same worn look.

### New in the figure tools

- **Things a figure carries** (`FigureProps`, recipe field `props`): a model, a humanoid bone and a place on the
  standing figure. When the game visual is built, a passive copy (plain meshes, a private worn copy of the material,
  optional glow of the bright, saturated texels) is hung on that bone, so it follows every animation without game code.
  A prop whose private model is not installed is left out with a warning. `PassiveArtVisuals.DropCarried` takes the
  things off a stand-in that borrows a story figure's body (the bell bearer of acts 3 and 4 wears Mara's body).
- **Hair under a hood** (`FigureHairTuck`, recipe flag `tuckHair`): the pack lays every hair part over the bare head, so
  tufts stuck out through Orsel's hood. Seen from the middle of the head, hair behind the hat's cloth and away from its
  rim has gone through the cloth; those triangles are left out (145 on Orsel). Hair leaving the hat at its rim stays.
- **Worn look without a new bake** (outfit fields `gain`, `saturation`): a grade over one part's pixels, laid on with the
  fabric when the visual is built. The resident pool has one `finish` for all (grade 0.62 / 0.8, linen weave on cloth,
  leather weave on leather, none on metal): 180 residents were rebuilt in about six minutes instead of baked for hours.
- **Own models** (`Tools/Art/props`): `make_amulet.py` and `make_belt.py` build Mara's amulet and belt in Blender from
  loops measured on her built body (`mara_amulet.json`, `mara_belt.json`); no pack content.

### Wearables follow the new wardrobes

The field pauldron (the healer knight's drop) and the regional outfits are fitted to the hero's and to Mara's built
body. Both fits were baked from the wardrobes of before: with the new hero the field pauldron no longer sat on a
shoulder plate, and Mara's regional fit had been stale since her redesign of 2026-11-23. The hero therefore keeps his
soldier's pauldrons (the field pauldron replaces the left one), `RegionalBodyFitBaker` names today's parts (hero: coat,
pauldrons, hooded cape; Mara: blouse), long hair that stays visible no longer counts as face or torso when the body is
measured, and both fits were baked again (private). Any later change of the hero's or Mara's parts needs this bake.

### The Ember Warden's own figure

`CryptEnemyArtBuilder` builds `EntryEnemy3` from the owner's model: imported as a humanoid, 3.0 m, with its own idle,
walk, sword slash and death; the flinch comes from the licensed humanoid takes the hero uses, because the model has
none. Its hand is empty in the slash, so it holds the game's `sword.demonic` (1.9 m; the blade runs along the hand's
forward axis, read from the slash). The ember runes of its texture glow (emission mask of the bright red texels). The
blow lands at 0.57 s of the slash; with the boss's windup of 9 ticks the clip runs at 0.63 ×. The blockout crown over
the boss is skipped for a figure with clips. Kind 3 is the boss of the crypt's depth runs; the entry crypt's boss stays
the demon (kind 2). Pedestal, the king's gem and the warden as a companion are a later block.

### Village mood

- Ground fog in the village and on the ash road: the existing mist emitter in wide, low, level banks at knee height
  (cold blue-grey, the mist budget of 24 particles is kept); the ash snow stays.
- The night's moon is colder and stronger (0.66 instead of 0.40), so dark worn clothes do not sink into black; the
  torches stay the warm light islands.
- A soft warm light goes with the wanderer at night (owner decision 2026-11-24, `HeroLightPresentation`): one point
  light without shadows at shoulder height, a step towards the camera, so it falls on the side the player sees. It
  comes up with the dusk exactly as the village torches do and is off by day and in places with authored light.
- Day and night stay. The owner asked for a world that is always night and withdrew it within the hour; that build
  (a moon that wanders and never sets) is in this branch's history (`8d44e00`, `65fd020`) and taken out again by
  `8b0f790`.

Nothing of the packs or of the owner's model is part of this repository: models, textures, baked figures, the derived
meshes and materials of the props and the built prefabs are private.

## Checks

Unity 6000.6.8f1; Unity CLI in the live editor, not a Director job. The Core is unchanged.

| Run | Code | Result |
|---|---|---|
| EditMode `BozoFigureRecipeTests` (new case `WhatAFigureCarriesNamesAModelABoneAndAPlace`) | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| EditMode `BozoFigureVisualTests` (new case `WhatAFigureCarriesHangsOnItsBonesAsPassiveMeshes`: every prop of a built figure hangs on its bone as plain meshes in a private material at its size; a stand-in drops them all and keeps the body) | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| EditMode `CryptEnemyArtTests.BuiltEnemyFiguresKeepTheClipContract`, now also for kind 3 and for what an enemy holds | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| PlayMode `EnemyClipsFollowTheCoresWindupHitsAndDefeat`, now also with the Ember Warden's figure: no blockout crown, sword in the right hand, attack paced by the Core's windup, flinch, death clip, removal | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| PlayMode `ForestMillCryptAndEmberRiftReuseBoundedNativeParticles`, now also: village and road have ground fog in wide, low, cold banks on the mist budget | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| PlayMode `VillageSkyProjectsCoreTicksWithoutWritingAnyRules` and `SkyRebindsAfterSameAreaRebuildAndKeepsInteriorLight`, now also: no hero light by day, a warm point light without shadows at the wanderer's shoulder on the camera's side at night, none in a place with authored light | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| PlayMode, private art: `AshWardensAndMillRingerAppearWearAndPersistThroughTheRoad`, `TiefscharrerKeepsHisBrandAndDropsAnEquipableGrubenlicht`, `RegionalArsenalModelsAndLiveHandsMatchTheCatalog` (regional wearables on today's hero and on Mara's body) | `dda2f387daf580e4f1f922df41458d55d30e3066` | part of the full suite below |
| Full EditMode suite | `dda2f387daf580e4f1f922df41458d55d30e3066` | 285 total, 257 passed, 0 failed, 28 skipped (cases that want private sources or a vacant building area) |
| Full PlayMode suite with the private overlay (`ARE_ENTRY_EVIDENCE_DIR` set in the editor, so the 28 overlay cases run too) | `dda2f387daf580e4f1f922df41458d55d30e3066` | 213 total, 208 passed, 1 failed, 4 skipped (capture runs that want an output folder) |
| PlayMode `PlayerBowShotMovesAfterAcceptedHitAndInventoryRestoresMelee`, the one failure of that run, after its fix | `10c49fbe5b125e90173509a6c16c6e0a9a8ff770` | 1 / 1 PASS |

Two rounds were needed. The first full PlayMode run (`8b0f790`, without the overlay: 213 total, 184 passed, 1 failed,
28 skipped) failed `FieldHealerKnightDropFitsThroughMotionAndFreshSave`: the field pauldron's fit came from the old
hero. That is the wearables section above; the case passes in the second round. The second round's one failure is an
overlay case that had been stale since combat v2: the bow test expected a shot and a sword strike to land in the tick
they are queued, while combat v2 winds both up first, and it replayed the animator before the hit was accepted. The
test now waits for the windup and leaves the animator alone until then; the game code is unchanged. Between `dda2f38`
and the head only that test file differs.

Previews (studio renders of the built visuals from all sides, standing and walking, and in-game frames) were looked at
after every step; they are on the owner's design page, not in the repository.

## Limits

- Judged on still frames, not in motion and not on the tablet. Frame cost of the props and of the fog is not measured.
- A prop is rigid: Orsel's lantern follows her hand and does not swing freely; hand-held things were turned so that
  they hang upright while standing.
- Amulet and belt are fitted to Mara's body as it is built now; they must be measured again when her body, height or
  clothes change.
- The halberd of the guard and an ornate sword for the hero are open: the owner delivers the models. The hunter's
  drawn bow is not part of the standing figure.
- The village itself (houses, fence, road) is unchanged. The owner ordered its texture rework as the next block
  (2026-11-24: every surface new, not everything stone, the ground adapted to ash and embers).
- The hero's light is one more dynamic light at night next to the four village torches; its cost on the tablet is not
  measured.
- The Ember Warden's flinch is retargeted from another rig onto a model whose rest pose has hanging arms; it was only
  seen in single frames.
- The scene image on the design page is staged: the figures' places and three extra warm lights exist for the picture
  only.
