# Bounded forest checkpoint — intermediate WIP

MOB-001 remains running and incomplete. Owner requested a pause after the forest and a switch to standalone Asset Store work. No expansion to further travel, Immergrün story, spy, alchemist or professions has been implemented. This is not a delivery report or task handoff.

## Current source

Uncommitted code in `/workspace/ashen-journey-20261114`; local HEAD/index were not changed by the implementation agent. Exact current owned Assets/Tests paths and SHA256 values are in `pilot-source-files.json`. Parent owns all SDLC/PM/BA/facts/docs/AGENTS/Memory and all native operations.

- Editor helper `AreArpg.EditorTools.JourneyWorldAuthoring.BuildPilot` authors one 128m / 129-node World Forge profile for seeds 731904, 731905, 731904. It preserves other scenes, saves only its owned assets/scene, exports immutable versioned maps, and records actual timings/hashes/warnings.
- World Forge generation saves only owned TerrainData instead of every dirty project asset. A real native behavioral RED proved an unrelated asset was written. Native GREEN source 4c914a2d4efc145c33235ed5e1c6eeb38cdd4888 passed 17/17 EditMode tests with disk bytes, RAM 999 and dirty-state assertions. Earlier first-generation dirty-flag failure has no proven production cause beyond fixture ordering; the fixture now settles its own import before mutating the sentinel.
- Core binds the exact authored map content hash, adds a seeded route beyond Aschenzoll and one four-floor optional cave, and keeps legacy EntryAct 1–10 / three-floor story crypt intact. Separate layout, encounter and decoration streams; route generation is bounded. Reentry regenerates run state; internal cave transitions and loads preserve it.
- Real combat produces existing catalog loot receipts and inventory/equipment; guaranteed chest fragments and more enemies raise obtainable value by depth. No claim that rarity probabilities increase. No fake kill ordinals or combat-practice reuse.
- AC20 wraps canonical legacy state plus current Journey and companion state. Missing historical hashes fail closed, with no latest-map substitution or older-backup overwrite. BigInteger, causal hunter XP, waiting/following/dead companion state, death restart and equipped-item retention are covered by Core verification.
- Runtime projects actual authored heights, mature trees, bounded red mushrooms/ferns/shrubs and visible route boundaries; Core remains collision authority. Existing HUD/camera stays in place, with new current-area titles/minimap/atmosphere bindings and immediate grounded travel arrivals.

## Verification so far

- Real .NET CoreVerification: 19,650 assertions pass in each of two independent processes against the actual native World Forge export. Both complete logs have identical SHA256 `1d585fb55f13f0a02e7fd475abcdaf103170cf29abd6211182364116e2d3821f`. 200 seeds; 450mm actor reachability to exits/treasures/enemies; enemy spacing, safe arrival/rest, supported 3–6 cave floors, depth value/health, save/replay/corruption and companion tests. Final sweep digest now includes layouts, enemy position/health and catalog receipts; actual-map digest `0348A18904A209D6`, map `82B0FA9F7D342123`; all 200 routes differ. Comparison and exact log paths are in `pilot-authored-core-comparison.json`. Earlier synthetic fixture runs were unit checks only.
- Preflight 40/40 and Director 20/20 pass; `git diff --check` passes.
- Native World Forge repair regression: 17/17, 0 skips, 41.53 sec under Unity 6000.6.8f1; XML SHA256 bd09f22f47b27923344fe7d3eeb1274af12305e4b8ac9b0c4c27274e6f6ae351. Parent retains source/XML evidence.
- Native pilot source tests require actual resources/private visuals without skips. `JourneyMapEditModeTests` verifies map binding/bounds. Two tests in `JourneyPilotRuntimeTests` cover actual runtime queued combat/pickup, fixed ticks, terrain/visual checks, PlayerPrefs save/readback, four cave floors/reentry and missing-map protection. They reuse fixture-owned scenes and restore both original save keys.

Actual native A/B/A authoring completed on source `4c914a2d4efc145c33235ed5e1c6eeb38cdd4888`. Both transferred maps match native SHA256: A `a6fc8fafb852d0b51bb0e2da0ac445f12ece5a263cfe9cecb8f65e7a8b948415`; B `5d774ced0df653b6f1573b9bb2ac7e64011b8158516824b00997236669e33188`. A repeated identical bytes; B differs. Generation-only timings 2.86 / 2.71 / 1.91 sec. Texturing warning is explicit because existing runtime WoodlandGround supplies the game material. No percentage time-saving claim.

## Remaining bounded work before pause

1. Parent snapshots the frozen product diff and runs native EditMode/PlayMode pilot; review entry/four-cave screenshots, grounded visuals, save preservation and prior HUD/camera behavior. Fix any demonstrated failures, then leave WIP preserved.

No Android/device, performance, natural progression balancing or full 19-requirement completion is claimed. The combat test uses an explicit +2000 weapon-power fixture. No collector attestation or final runtime report has been submitted. The eventual collector must run in the actual `unshare -Urn` network namespace, not merely set an environment flag.

The owner requested sales screenshots. Parent sets `ARE_ENTRY_EVIDENCE_DIR`; native test originals are `journey-worldforge-entry.png` and `journey-cave-floor1.png` through `journey-cave-floor4.png`. Retain originals with hashes. Any sales usage must distinguish World Forge tooling from separately supplied game art/combat.
