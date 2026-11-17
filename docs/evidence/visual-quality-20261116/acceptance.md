# Visual quality round — atmosphere, torch islands, sky, image finish (2026-11-16)

Base: `4b0b91d8af7b1964a71ad9ef14768e8200f90d9e` (regional VFX WIP, Unity pending). Presentation only:
no Core, save, HUD or camera-rig change. Core tree `2e2ed41b1362a996c436f86977d2439798d66cb6` unchanged.

## What changed

- **Regional particles** (`AshenAtmosphere`): particles fade in and out instead of popping, ash flutters and mist
  curls (one low-quality noise octave), mist sprites rotate and grow, rift embers are born at the ground, rise,
  shrink while cooling and glow additively. Still the three existing emitters and one material. The shader fades
  sprites near the lens and outputs premultiplied colour (ordinary haze blends as before).
- **Village torch islands** (`VillageLightPresentation`, `AshenFlame.shader`): four torch posts (gate pair, camp,
  market side) with a procedural billboard flame and an unshadowed point light. Brightness follows the daylight
  the sky derives from the persisted tick; unlit by day. Reduced effects keep only the gate pair lit.
- **Sky** (`AshenWorldSky.shader`): the sine blobs are replaced by a three-octave cloud deck in perspective with
  shaded cores, lit rims, dusk tint and moon halo; a warm twilight band sits on the horizon. Same properties.
- **Night tint**: the unlit road dressing (`AREVillageApproachFade.shader`) follows a global world tint, so it
  no longer glows at night.
- **Image finish** (`PostProcessPresentation`): one global URP volume — neutral tonemapping, bloom (quarter
  resolution, 3 iterations, no high-quality filtering), light vignette, small contrast and saturation lift.
  Off with reduced effects. No depth of field, motion blur or film grain.

## Evidence (Unity 6000.6.8f1, live editor of the validation project, software OpenGL)

Sources in the source workspace and the validation project compared byte for byte before the runs below.

| Boundary | Result |
| --- | --- |
| Core | 19857/19857 assertions |
| Preflight | 40/40 |
| EditMode, focused | `AshenRegionalVfxShaderTests` 1/1, `VillageLightShaderTests` 2/2, village hero/approach/palisade regressions 1/1 each |
| PlayMode, focused | regional particles 1/1, torch islands 1/1 (after a real failure: primitive colliders lived one frame; now removed immediately), image finish 1/1 |
| PlayMode, full suite | 142 of 156 passed, 14 failed, 0 skipped |
| Baseline of the 14 | all 14 also fail on the unchanged `529797c` sources in the same project |
| Images | day, dusk and night village captures and 80 further suite captures inspected privately |

The 14 failing cases are therefore not caused by this round. They concern content missing from the reduced
private asset set of the validation project (bear, rattler, audio library, one villager body, mill refuge) and
timing-sensitive presentation checks in the windowed software-rendered editor. They are **not** passed and
stay open until the suite runs against the full private asset set.

## Not verified

Android build, device frame time, GPU cost of bloom and four point lights on the Tab A9, the full PlayMode
suite with complete private assets, GitHub CI. No merge or release.

## Findings for the next visual round

The crypt dungeon is the weakest area in the captures: no ceiling, no light sources, no dressing. Some
roadside bushes stay bright at night.
