# Aschenwacht: the place between the old crypt and the village, and the beginning of the story · 2026-11-25

Owner order of 2026-11-25: import the archive "Aschenwacht · Der Glockenhof" with all its models and set it up as the
entrance to the old crypt; part 2, the forecourt ("Der entweihte Vorhof", at least four times larger, "mit allem Drum und
Dran"), follows. Every texture, item, stone and grass tuft is to be taken over. Asked where, the owner explained that the
game grew out of a tech demo and has no real beginning: the hero needs a past (how he came into the crypt, why he fights
there, where he comes from, where he belongs), with a free hand for the story; the place between crypt and village is free
to tell it, the player walks through it, explores it and only then goes on to the village. The owner also allowed starting the
editor at once.

## What changed

### A place of its own between crypt and village

- The cleared crypt's passage no longer leads straight to the village: `TryTraversePassage` in the entry's first act goes
  into Aschenwacht (`CharacterProgression.InCourt`, `ArenaSimulation.InCourt`, `TryEnterCourt`). The wanderer steps out in
  front of the crypt portal; nothing fights there; the bell tower's door is the passage on to the village, which arrives
  exactly as before (arrival scene, `1,2`).
- The save carries the place in the entry token: `1,1,c` plus the mask of the traces read (`1,1,c37`); older `1,1` and
  `1,2` saves read as before. `TryAdvanceCrypt` still goes straight on for the older checks; the real passage never skips.
- Walk and ground: `CourtLayout` (generated, 348 x 474 cells of 250 mm, heights on 500 mm cells) knows where the wanderer
  may stand and how high the ground is. It is baked from the forecourt's own collision surface, its buildings, graves,
  sculptures, liturgical pieces and trees, the connector and the bell court's scenery
  (`Tools/Art/aschenwacht/station_grid.py`): the floor is the top of the low stack of upward faces, anything between 0.40
  and 1.85 m above it blocks, neighbouring floors differ by at most 0.40 m, the wanderer's 0.5 m radius is part of every
  obstacle, and only what the spawn reaches counts. The westwork's walls are missing from the delivered collision surface;
  its geometry and a closed strip behind it keep the wanderer in the court. `CourtLayout.Route` finds the way for tests.
- The runtime shows the place (`EntryCourtWorld`, or a plain floor without the private sets), stands the hero on the
  ground of the rules (`GroundHeight`), puts the passage light at the tower door, opens the village's sky, ash and fog
  for it, and names it "Aschenwacht – Der entweihte Vorhof" / "– Der Glockenhof".
- The camera: a wall longer than 30 m but at most 8 m thick (the 77 m westwork) now counts as occluder pieces, so the
  camera moves in front of it instead of looking at the hero through it.

### The beginning of the story

`CourtLore` (Core, authored for this block): the bell of Aschenwacht kept the ash rain beyond the ridge while it rang. The
bell order died out; the Aschbeter, who pray to the ash, desecrated the forecourt and bolted the crypt from outside. The
order had laid its last ringer not in a grave but into the ash sleep, to wake when the bell falls silent. It has fallen
silent: the wanderer wakes in the crypt without memory, among the risen dead, and fights his way out. In Aschenwacht he
learns who he was; the pilgrim path leads down to Aschengrenze, the village the order once protected. Eight traces tell it
(portal, banner, baptismal font, empty grave, the Aschbeter, the stone guardians, the last watch, the silent bell): near one
the story action "SPUR" reads it, the objective counts them, nothing depends on them. The act's first line and the court's
arrival line come from the same file. The arrival in the village ("Du kommst aus der alten Krypta?") stays as it was.

### The picture

`AschenwachtCourtBuilder` builds the private prefab from both sets: 2569 placements (every building, grave,
stone, plant and grass tuft of the forecourt scene and its terraced floor, the connector, all 122 pieces of the bell court,
the distant slate ridge, the Aschbeter on the procession path, turned to face the crypt portal he guards). The models are
reduced for the tablet with `Tools/Art/aschenwacht/convert.py` (Blender): together 1,210,838 triangles instead of
7,696,946; grass keeps whole blades, a share of them. Materials are URP/Lit from the sets' own maps (colour,
normal, a mask of metal, occlusion and smoothness from the ORM map, emission for embers and garnet); the distant ridge
wears a matte, darker copy of its stone. Grass, leaves, candles, embers, ash drifts and the ridge cast no shadow (decided
per renderer: URP's material validation turns a material's shadow pass back on). The prefab stays passive, as the art
binding demands (no light, no collider, no script); the runtime lights nine fire baskets and lays an ash plain under the
place so the open sky never shows beneath the forecourt or the bell court's island. The Aschbeter plays his rest pose;
the bell court's guard and pilgrim stand still, as in the set. The full-quality originals of both sets stay in the editor
as the parallel session imported them. Nothing of the sets is part of this repository.

## Checks

Unity 6000.6.8f1; Unity CLI in the live editor, not a Director job.

| Run | Code | Result |
|---|---|---|
| Core verification (`Tests/CoreVerification`, Unity's .NET SDK) with the new `CourtTests` | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | ALL PASS, 23,200 checks (57 of them for Aschenwacht) |
| `AschenwachtCourtArtTests` (2) in the full EditMode suite | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | passed |
| `AschenwachtLiesBetweenCryptAndVillageAndIsWalkedOnFoot` in the full PlayMode suite | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | passed |
| `CryptLeadsToVillageAndResumesItsCheckpoint` in the full PlayMode suite | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | passed |
| `APlayerFinishesTheMillChapterWithStickAndTapsAlone` in the full PlayMode suite | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | failed: it still expected the crypt to open straight into the village |
| `APlayerFinishesTheMillChapterWithStickAndTapsAlone` after teaching its walker Aschenwacht's route (test files only) | `1e6326e100b99c96d7e6233ebd9fc48f7da02fa4` | passed alone (727 s) |
| Full EditMode suite | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | 371 tests: 343 passed, 0 failed, 28 skipped |
| Full PlayMode suite with the private overlay | `39cd6cd63cf592c016c5179159d2ab89e6371d57` | 215 tests: 210 passed, 1 failed, 4 skipped (the long journey run, see above) |

Pictures of the place in the running game by day and by night were looked at; they are on the owner's page, not in the
repository.

## Limits

- Frame cost on the tablet is not measured. 2,572 renderers stand in the place; the camera sees a part of them.
- The heights of the rules follow the baked floor on 500 mm cells; on stairs the hero rises smoothly, not step by step.
- The Aschbeter does not fight; whether he becomes an enemy, and whether the sets' armour and claw staffs become loot, is
  asked of the owner.
- The story was written with the owner's free hand; it can still be changed.
- The full PlayMode suite was not repeated after the test-only fix of the journey run; that run passed alone on `1e6326e`.
