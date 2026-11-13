# Bug Fix: Select the moving BowShot take

- **Slug**: hunter-bow-animation (resolved from the existing assessment)
- **Fixed**: 2026-11-13
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

The private FBX contains both `tpose` (one frame) and `BowShot` (the actual shot).
The importer selected the first non-preview clip, exporting the T-pose. The isolated
bow rebuild now requires the exact reviewed humanoid take and retains the existing
derived asset identity. Native verification is recorded separately in test.md.

## Changes

| File | Change | Notes |
|---|---|---|
| Assets/Editor/WoodlandArtBuilder.cs | Exact take selection; isolated BuildBowAnimation | No source FBX edit or broad woodland rebuild; strips animation events, disables looping and sword grip weight |
| Assets/Tests/PlayMode/CompanionRuntimeTests.cs | Preserve original reproduction; add native frame capture | Actual rescue, recruitment, Core hit, attack/recovery and independent arm samples |
| Assets/Tests/PlayMode/PlayerBowRuntimeTests.cs | Player regression | Real inventory/UI, rejected/accepted attacks, arm motion and melee re-equipping |
| Assets/Tests/PlayMode/EntryJourneyRuntimeTests.cs | Optional capture focus | Test camera is restored in finally; production camera unchanged |
| Assets/Tests/EditMode/BowAnimationEditModeTests.cs | Three native authoring tests | Exact source curves, invalid take rejection, repeatability, GUID/source preservation |
| Assets/Scripts/Runtime/HumanoidBowGrip.cs | Bow-only palm-frame adapter | Read actual humanoid IK orientation once; rotate only the live weapon, preserve the reviewed player grip |
| Assets/Scripts/Runtime/EquipmentPresentation.cs, CompanionPresentation.cs | Bind or clear bow grip on equipment change | Sword/shield attachments and Core actions unchanged |

## Tests Added or Updated

- Both `BowShotMoves...` PlayMode tests inspect the real bound clip on real humanoids,
  independently of transition blends, and protect Core, save, prefs and actor transforms.
- `ExportedBowContainsTheReviewedShotInsteadOfTheSourceTPose` compares the exported
  duration and forearm curve with the actual source, and rejects animation events/looping.
- `BowSelectionRejectsMissingDuplicateAndRenamedStillTakes` checks failure cases.
- `RebuildingOnlyTheBowPreservesIdentityAndPrivateSourceBytes` checks repeatable
  output and preserved original FBX, importer metadata and output GUID; restores its fixture.

## Local Verification

- `git diff --check`: pass.
- Native pre-fix PlayMode at `4d068e4551e84c98266d8a368682cd17ad1638ca`:
  0/2 passed, both fail at 0 degrees arm motion; no skips. XML SHA256
  `41b6e57af8ce4c4e3d72bb19fc2c5db4b5d8e967eab0660cfe422b3c8b2402b9`.
- Further native runs, visual inspection and Core CI are pending at report creation;
  final results belong in test.md and the revision-bound evidence dossier.

## Deviations from Assessment

The existing named animation is sufficient; no authored replacement, Core timing
change, new runtime controller or gameplay events are needed. The required native
visual check then exposed a second presentation defect: the hunter's wrist axes hold
the bow almost horizontally (86.46529 degrees from vertical), whereas the player's
bow is vertical. `12bc3bd29292706cb2eeb9f695dba24f5138b849` reproduces this after the
clip repair. Its player-angle assertion also failed because it treated the bow's
undirected long axis as a directed vector (172.5169 degrees means 7.4831 degrees
from vertical); the assertion now correctly accepts either sign of that axis.

The added grip adapter uses Unity's canonical left-palm frame, with the authored
bow orientation measured on the existing player rig, to accommodate each avatar's
different wrist axes. It reads IK but writes only weapon-local rotation; source
rigs, clips, Core and saves stay untouched. The original diagnosis remains valid.
Repeated actual companion hits at ticks 62,68,74 already confirm the unchanged
six-tick cadence and moving production controller; final grip and full regression
results remain subject to the verification report. Test-only JPEG captures record
the same native camera output with smaller transfer size; legacy PNG captures remain.

## Follow-ups

The road encounter's hunter Animator and new Inari model integration remain separate.
This repair does not establish Android device execution or frame-time performance.
