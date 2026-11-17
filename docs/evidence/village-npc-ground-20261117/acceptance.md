# Villagers no longer stand waist-deep in the ground (2026-11-17)

Base: main `507c0f4d884a8584d352528f4726d5eb097965ca`. Presentation only: no Core, save, HUD or camera change.

## Cause, measured in the Unity 6000.6.8f1 editor

| Actor | lowest visible vertex before | after | animator before → after |
| --- | --- | --- | --- |
| Mara (NPC 0) | −0.911 m | +0.005 m | `AshenHumanoidMovement`, no clip → `EntryMelee`, `EntryIdle` |
| Ilyan (NPC 1) | −0.921 m | +0.002 m | `AshenHumanoidMovement`, no clip → `EntryMelee`, `EntryIdle` |
| Dorn (NPC 2) | −0.002 m | +0.002 m | unchanged (`EntryMelee`) |
| Hero | 0.000 m | 0.000 m | unchanged (`EntryMelee`) |

The private villager prefabs reference the private controller `AshenHumanoidMovement`. Its two clips
(idle and run from a purchased animation pack) are only present in the authoring workspace, not in the
validation project. The controller therefore loads but plays nothing; a humanoid Animator without a clip
keeps the default muscle pose with the hips at the object origin (hips measured at −0.001 m and −0.015 m),
so the body sinks by its hip height. The runtime replaced the controller only when it was `null`.

## Fix

`PassiveArtVisuals.HasPlayableClip` treats a controller without an existing clip like a missing one.
`EnsurePlayableHumanoidLocomotion` runs for every grounded binding and gives a humanoid Animator without a
playable clip the reviewed shared locomotion controller. Working controllers and generic rigs are left alone.
This also covers the road witness on the ash road, which uses Mara's prefab.

## Evidence

- EditMode `HumanoidLocomotionFallbackTests` 3/3 (clipless controller, real clip, generic rig untouched).
- PlayMode `PassiveArtVisualsTests` 9/9 (existing fallback/material/host-transform contracts).
- PlayMode `VillageArtAndVillagersStandOnTheVisibleGround`: before, Mara and Ilyan failed ("no visible ground
  underneath", bottoms −0.91/−0.92 m); after, both pass the ±0.05 m contact check. A temporary diagnostic
  (not committed) recorded root, hips, foot, clip and lowest vertex before and after.
- Core 19866/19866, preflight 40/40.

## Still open (not caused or fixed here)

- The same test still reports "Wanderer sinks 0.11 m below FloorPanel2x1_LOD00": it compares the hero's feet with
  the tallest stone of the raised road panel. Measured locally, the paving under the hero has a median top of
  0.077 m, so the feet dip up to about 8 cm between raised stones. The Core walks at y = 0 without terrain height.
- In the validation project the wooden practice sword renders as a large untextured plank, most likely a model
  missing from its reduced private asset set. Not investigated in this change.
- Android build and Tab A9 not run.
