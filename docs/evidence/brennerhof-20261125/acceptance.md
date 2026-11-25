# The Brennerhof: Aschenwacht's side yard · 2026-11-25

Owner order of 2026-11-25: load the newest asset pack ("Der verschleierte Brennerhof") completely into the game's editor
and build the place in. It has no enemies, but vases with sounds, two weapons (halberd and spear) as new drop models, a
horizon, houses, ruins and walls, fog with embers, one puzzle and a hidden secret place. Asked (A/B/C), the owner chose:
the yard is an optional side way of Aschenwacht, through a side gate in the forecourt, before the village; the two
weapons are fixed finds like the existing regional gear, so the frozen random weapon catalog and every old save stay
as they are.

## What changed

### The place

- A side gate in the east wall of Aschenwacht's forecourt (`KilnYard.SideGateXmm/Zmm`) is a passage of its own: it leads
  into the Brennerhof (`CharacterProgression.KilnState`, `ArenaSimulation.InKilnYard`, `TryEnterKilnYard`), the yard's
  south gate leads back to the side gate (`TryLeaveKilnYard`). The bell tower's door still leads on to the village; there
  the yard is forgotten, the weapons stay. Nothing fights in the yard.
- The save carries the yard after the court's traces: `1,1,c<traces>,b<state>` (inside, visited, gate bolt drawn, eight
  vases); older tokens read as before. Once visited, Aschenwacht resumes at the side gate.
- Walk and ground: `KilnLayout` (generated, 250 mm cells, heights on 500 mm cells) is baked from the colliders of the pack's
  own scene (`Tools/Art/brennerhof/kiln_grid.py`, the same rules as Aschenwacht's map), with the gate of the kiln passage
  left open; the rules close it (`KilnYard.InGate`) until the bolt is drawn.
- The puzzle: the gate of the covered kiln passage is bolted on the passage side. From the Topfhof it only says so; the
  wanderer has to find another way in: through the alleys round the kilns, or through the secret.
- The secret: the Scherbenschlupf, a crawl way under a collapsed roof behind the west kiln, into the kiln passage. The
  halberd ("Scherbenhellebarde", two-handed polearm, 175) lies there; the spear ("Brennerspeer", two-handed, 140) lies at
  the weapon bench by the south gate. Both are regional finds (`RegionalRelic.B_*`, `Unlock.Kiln`), taken with the story
  action like every other find, and only in the yard.
- The vases: eight, each breaks once with the story action "VASE" and stays broken while the wanderer is in Aschenwacht.

### The picture and the sound

`BrennerhofBuilder` builds the private pictures from the pack's own scene, every piece where the pack puts it: the yard
with houses, kilns, walls, ruins, rubble, reeds, the abbey, the broken aqueduct and the two ridges on the horizon and its
six ground-fog volumes (321 still pieces, 342,919 triangles); the eight vases and their sixteen shards
each; the gate; the side gate's arch for Aschenwacht's wall (whose wall piece there gives way:
`Tools/Art/aschenwacht/station_placements.py`); the two weapons. The runtime (`KilnYardPresentation`) lights the eight
ember niches and lets embers and ash rise from them, lifts the gate when its bolt is drawn, bursts a breaking vase into its
shards (they fly from the side of the blow and come to rest on the ground of the rules) with one of the pack's four
breaking sounds heard from the vase, and plays the pack's draught through the alleys. Nothing of the pack is part of this
repository; its own scripts were read before the import and run only in the editor (its mesh importer).

## Checks

Unity 6000.6.8f1; Unity CLI in the live editor.

| Run | Code | Result |
|---|---|---|
| Core verification (`Tests/CoreVerification`, Unity's .NET SDK) with the new `KilnYardTests` | `50e97348588ee4c90abdb5dce5ce49beee75e4b4` | ALL PASS, 23,297 checks (97 of them for the Brennerhof) |
| `BrennerhofArtTests` and `AschenwachtCourtArtTests` after the builds | `ee19f2396662b36990f32a44c1ea744eac8628f3` | passed (1 and 2) |
| Full EditMode suite | `50e97348588ee4c90abdb5dce5ce49beee75e4b4` | 372 tests: 336 passed, 8 failed, 28 skipped. The 8 failures (ArtForge reads, Fadenwerk revision) got 503 "Unity importiert oder kompiliert gerade" from the busy editor |
| `ArtForgeAsyncReadTests`, `ArtForgeReadOnlyTests`, `FadenwerkRevisionTests` again with the editor idle | `50e97348588ee4c90abdb5dce5ce49beee75e4b4` | 21 of 21 passed |
| Full PlayMode suite with the private overlay | `50e97348588ee4c90abdb5dce5ce49beee75e4b4` | 216 tests: 209 passed, 3 failed, 4 skipped. `TheBrennerhofOpensBesideAschenwachtAndItsVasesBreak` and the long journey run passed; failed: Aschenwacht counted the side gate's glow as a tenth fire light, and two arsenal checks expected 23 regional finds |
| After the fix (the glow is a passage marker of its own; the arsenal check keeps the two yard finds apart): the 3 failed tests and `TheBrennerhofOpensBesideAschenwachtAndItsVasesBreak` | `7eb1f31e2e42ad53dcdd4e3051cd6e44fe061c08` | 4 of 4 passed |

Pictures of the yard in the running game by day and by night were looked at; they are on the owner's page, not in the
repository.

## Limits

- Frame cost on the tablet is not measured.
- Importing the pack makes Unity import every texture of the project once more: the pack brings a texture postprocessor of
  its own (it only changes the pack's textures). Removing it later would cost the same again.
- The vases give nothing; they only break. The weapons' values are set within the band of the existing regional weapons.
- The two weapons have no inventory picture yet; the inventory shows their names.
- The full suites were not repeated after the last fix (`7eb1f31`: one runtime line, one test file); the affected tests passed alone there.
