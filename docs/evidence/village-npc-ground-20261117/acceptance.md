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

Code under test: commit `29120c45b9c5144e2a7f492dd39050f82170af82` (tree `6f865defc9c6aada4047208fcd00d1d2e6f4a9a5`) on top of
main `507c0f4d884a8584d352528f4726d5eb097965ca`. The seven changed runtime and test files in the validation project were
compared byte for byte (SHA-256) with this commit before the runs below; its other tracked files were main. Runs went
through the live Unity 6000.6.8f1 editor of the validation project (`unity command test run`, PlayMode asynchronous);
the saved command outputs are private on the VPS under
`.local/share/are-arpg/weapon-mastery-20261117/runs/`.

| Run | Result | Output SHA-256 |
| --- | --- | --- |
| RED: road witness bound at y = 0, then moved to y = 1 (old order) | 0/1, witness bottom 1.0 m | `bb8c33aac68bb35e2a0ce54451e98e9dd8eeef13ea32945d71d1b45ab9d8b253` |
| EditMode `HumanoidLocomotionFallbackTests` | 4/4 | `b65e4e66f14d6d328d77b1fc8f5f0cdcc1d078410d2660506f4885b8c3b414ad` |
| PlayMode `RoadWitnessStaysOnTheGroundWhileFollowingTheCorePosition` | 1/1 | `2b47a4afc16e8f5eb2bbd0e50a3d0b5dbc6e18f2dd01363ef0de4437bccf8fb3` |
| PlayMode `PassiveArtVisualsTests` | 9/9 | `44140a53d4ff0a3df90fceb00bf92016182a07285db4c3a41e9e054019c77202` |
| PlayMode `VillageArtAndVillagersStandOnTheVisibleGround` | Mara and Ilyan pass; fails only on the hero/paving line below | `4c35affd5ed0942313c555628f75af726f3957632edbcc29de86f467d58921c3` |
| Core verification | 19866/19866 | `eeb695e9db1d29ae9a10948775ed902557d8dbaf42030c7a80d6c0a2fd83a3e6` |
| Python preflight | 40/40 | `8b0b4a74610c7af2079b244709ea03277c9057e0911b30a349f324a7753e3a74` |

Before the fix the same grounding test failed for Mara and Ilyan ("no visible ground underneath", lowest visible vertex
−0.911 m and −0.921 m); after it their bottoms are +0.005 m and +0.002 m. A temporary diagnostic test (not committed)
recorded root, hips, foot, controller, clip and lowest vertex for each actor before and after.

Review fixes (PR #58): the road witness host is created at its projected height before binding, so it does not float
1.0 m; `NeedsSharedLocomotion` keeps a generic rig's own controller and only replaces clipless controllers on humanoid rigs.

## Still open (not caused or fixed here)

- The same test still reports "Wanderer sinks 0.11 m below FloorPanel2x1_LOD00": it compares the hero's feet with
  the tallest stone of the raised road panel. Measured locally, the paving under the hero has a median top of
  0.077 m, so the feet dip up to about 8 cm between raised stones. The Core walks at y = 0 without terrain height.
- In the validation project the wooden practice sword renders as a large untextured plank, most likely a model
  missing from its reduced private asset set. Not investigated in this change.
- Android build and Tab A9 not run.
