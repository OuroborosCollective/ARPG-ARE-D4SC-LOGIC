# Bug Verification: Moving, correctly held player and hunter bows

- **Slug**: hunter-bow-animation (resolved from the existing assessment/fix context)
- **Tested**: 2026-11-14
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The original frozen-shot symptom no longer reproduces on either real humanoid.
Native visual review also found and corrected the hunter's sideways weapon attachment.
The final complete suites pass with the private overlay; Core, save, source assets,
UI and production camera remain unchanged.

## Checks Performed

| Check | Command / Action | Result | Notes |
|---|---|---|---|
| Original reproduction | Unity PlayMode `-testFilter BowShot` on `4d068e4551e84c98266d8a368682cd17ad1638ca` | expected fail | Both actors measured 0 degrees arm movement; 0/2 passed, no skips |
| Authoring reproduction | EditMode `-testFilter BowAnimationEditModeTests` on `9d8e05f7722f2adca220436c84e332ee0af2127b` | expected fail | One-frame export and missing exact-take/rebuild API; 0/3 passed |
| Isolated artifact rebuild | `-executeMethod AreArpg.EditorTools.WoodlandArtBuilder.BuildBowAnimation` on `d3942fad21a5760ba7f30330fb21512971068012` | pass | Only derived WeaponBowShot changed; GUID and source FBX/meta preserved |
| Grip and repeated hits | PlayMode `-testFilter BowShot` on `497413a0384fd2a4d9565ac4aef090719b5c07b7` | pass | 2/2; 50.10859-degree clip motion; actual companion hits six ticks apart; player UI equip and melee restore |
| Native visual review | Nine actual Unity JPEGs at the preceding source; identical runtime/editor trees at final source | pass | Hunter/player 20/50/80-percent poses plus genuine repeat-cycle 100/300/600-ms frames; recorded hashes |
| Complete final PlayMode | `-runTests -testPlatform PlayMode` on `6b989e7a5cd85465933688d6f80c7a9d5d4b4846` | pass | 129/129, exit0, no skips; includes rescue, individual companion progress, equipment, save/load and package examples |
| Complete final EditMode | `-runTests -testPlatform EditMode` on the same source | pass | 55/55, exit0, no skips; exact source curves, rejection cases and repeatable artifact/GUID preservation |
| Real Core | `dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release` | pass | 8112 assertions; CI43530269464/job132245928906 at the same source |
| Static checks / Director | `python3 -m unittest discover -s Tests/Preflight -v`; `python3 -m unittest discover -s Tools/Director -v`; `git diff --check` | pass | 40 preflight +20 Director; separate from engine execution |
| Independent readback | Reparse native XML; rehash logs, XML, private overlay and inspected frames; compare owner prefs | pass | All receipts match; clean tracked workspace, prefs/settings restored |
| Android device | New APK and physical Tab A9 execution | not-run | This bounded animation repair has native Editor proof, not device proof |

Native commands use Unity6000.6.8f1 with `-batchmode -force-glcore -buildTarget Android`,
Xvfb1280x720 and `ARE_ENTRY_EVIDENCE_DIR`/`FORGE_EVIDENCE_DIR` set. Selecting an Android
build target is not Android execution. Exact commands, revisions, dates, outputs and
SHA256 values are in [acceptance.json](../../../docs/evidence/bow-repair-20261113/acceptance.json).
The engine fixture was rerun; the original assessment was retained as historical evidence.
No before/after verification hooks are configured in `.specify/extensions.yml`.

## Output Excerpts

```text
Original hunter and player: Expected greater than 0.100000001f; actual 0.0f.
Corrected shared clip: WeaponBowShot, length=0.9666668s, arm motion=50.10859 degrees.
Final PlayMode: total=129 passed=129 failed=0 skipped=0.
Final EditMode: total=55 passed=55 failed=0 skipped=0.
ALL PASS: 8112 deterministic core assertions
```

PlayMode XML SHA256: `8585ddf9cabd35c12b99ae8a9fb9979f4afbe3525460d4b238af98e382c3678d`.
EditMode XML SHA256: `3f9366f7506b9a2b3fbc1ac4fb232238d9f1e39e8b187cb0c941ed2b5b21ff07`.

## Neighboring Regression Findings

The first full run on497413a passed127/129 and retained both failures. The material
test compared native URP colors bit-for-bit: diagnostic cbbe523 measured at most
5.96e-8 component error. It now compares all four channels within1e-6 and retains
the real visibility/material ownership assertions. The particle lifetime test
passed in isolation with an invisible renderer and Automatic culling. Its initial
failure cause remains unproven; the fixture now isolates pause/resume from culling,
checks preserved lifetime while paused and actual resumed playback/native aging.
There is no manual ParticleSystem.Simulate and no production effect/sample change.
Both repaired fixtures and the full suite pass at the final source. The fixture
changes are separately integrated through PR42 following the scope review; this
report retains their combined execution evidence.

The intermediate grip RED's player-angle assertion also treated the bow long axis
as directed:172.5169 degrees meant only7.4831 degrees from upright. The corrected
assertion accepts either sign; this was a test error, not a player grip defect.

## Residual Risks

- The road encounter's separate hunter Animator and the new Inari Hunter model remain future integration.
- Static captures do not prove bowstring deformation, physical-device frame pacing or full art polish.
- No new game APK, Android execution or measured development-time improvement is claimed.
- The separately handed-over Memory Forge1.0.3 package is unchanged.

## Recommendation

Close the shared bow-animation defect after the final PR-head CI/readback gate.
The source bound above is fully verified for this scope. Subsequent documentation
commits may reuse its evidence only while the recorded runtime/editor/test trees match.
