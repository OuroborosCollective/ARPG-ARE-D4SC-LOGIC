# Ashen Crypt evidence logbook handoff

Owner instruction of 2026-11-16: maintain one persistent document named `Ashen_Crypt_Evidenz_Logbuch.docx` using Design Report. Append the last concrete steps after each completed work round so the next agent can continue. Preserve old entries; corrections are new dated entries. This repository is the independent Unity ARPG, not Echoes of Aurion.

The document contains five rounds: selected material intake; tick-projected sky and import limits; moonlight and same-tick rebuild fixes; readable twilight and final native tests; publication and documentation. All eight pages were rendered and inspected. Private file identity and version are held on the authorized VPS at `/var/lib/unityarpg/.local/share/are-arpg/sky-native-20261116/logbook-reference.json`; keep that record private and replace the same document identity on each update. Do not create a new unrelated logbook each round.

## Verified publication checkpoint

- Base main: `193c1466e9256185b6980f9ee6a62c3853676813`.
- Native-tested code: `24c4fdd13f896afd18c54bc342aa3ab8ae483937`.
- VPS delivery with evidence: `b28c8585ee7de493a5e35a9a8657a562bc99ec6b`.
- GitHub API publication: `d7cc0645bd2f9577da2a7e9d04539f676e5123b0`.
- Exact identical full delivery tree: `3a6aaed2035ad91b7b77c740b90856a106f91f90`.
- Draft PR: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/49 ; quality epic #48 stays open.
- Source of test counts and independent hashes: `acceptance.json` beside this file. Native 6/6 PlayMode and 1/1 EditMode on 24c4fdd1, zero skips. Four actual phase-camera fixtures reviewed. Core 19,739 on 673c3bec with Core/test trees byte-identical to 24c4fdd1; 40 preflights passed.
- GitHub Core run https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/43809797562 succeeded for the published head association. Job 133108668799 executed the real C# simulation, Fadenwerk roundtrip, map immutability, static preflight and policy checks.
- GitHub licensed Unity run https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/43809797557 was skipped. It is not native success.

The VPS had no credentials for canonical HTTPS push. API publication therefore has a different commit identity. The tree was independently fetched back on the VPS and matched exactly. Never relabel the API SHA as the SHA natively executed. This follow-up changes only AGENTS.md, Memory.md and this handoff.

## Continue from here

Inspect the current owner document, Memory.md, ProjectState.md and acceptance.json before edits. Use the prepared isolated code workspace and private assets. The documentation workspace `/var/lib/unityarpg/workspaces/are-arpg-logbook-20261116` starts from the published d7cc0645 tree; it is not the native Unity project with private art.

Next visual block: define the village arrival hero view, place selected native-verified Viking props, refine terrain/path materials and spatial composition, then inspect real native images. Keep Fable HUD, camera contracts, save owner and 100-ms Core. WorldSkyClock is read-only; shader/cloud phase follows existing persisted ticks.

Remaining acceptance gates: village composition and placement; forest/mill/caves; broader visual pass; a new Android build; physical Tab A9 readability and performance. Do not claim these are complete. Native tests here were targeted, not the entire Unity test suite. No merge or release is authorized by this checkpoint.

## Round entry contract

Each new round records UTC date, incoming and resulting revisions, exact changed production/test/persistence/presentation surfaces, causal finding, real commands or native run labels, independent readback/hashes, failures and corrections, explicit limits, next step, and PR/CI state when relevant. Keep test fixtures distinct from owner runtime and device evidence. After saving the same document, update its private version record and a concise repository handoff. If document access is unavailable, preserve the round in the repository and report that document writeback is pending; never silently claim it was updated.
