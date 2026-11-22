# Issue #6 · Causal Forge regression, migration and runtime evidence

## Scope and ownership

Base `main` `5d94f238b6494395bdba8b5acef791c6ce53df41`. Commits: `5b12e89344cdb28b6ae00351c4ef8ba729b87cfe` (tests, fixture, benchmark), `f45f6a8456a0b6ca24ffea9f7d534516a809d3f1` (review follow-up), `d883ef9c1cf30d8eed2ae32b9a15547e4993347a` (benchmark records the host load). Tests and benchmark only: **no production change, no new authority, archive, save version or tick source; AC6 and AC13 stay reserved.**

The chain under test is the existing one:
`ArenaSimulation → SocialLedger transfer → MemoryArchive.Append (camp.donation / camp.theft) → RecordCampDecision(CauseEventId) → CharacterProgression.SaveString (AC22 wrapper, NMF ledger schema 3) → TryLoad → MemoryArchive.AddEvent (cause validated for ledger schema ≥ 3)`.
Recall stays bounded to 64 ordinary memories per mind; the archive itself is not recalled.

## The six named checks

All in `Tests/CoreVerification/CausalRegressionTests.cs`, run by the real CoreVerification program.

| Check | What it proves |
|---|---|
| `causal-parent-valid` | Theft and two-step donation: exactly one decision, and its `CauseEventId` names the final camp transaction (the theft, or the second donation, not the first). The parent is an earlier root event of the same actor, target and kind from the same tick; event IDs are dense, so no cycles, self-references or duplicates. Save/load keeps every event checksum and the save bytes. |
| `causal-corrupt-valid-checksum-reject` | Saves are rewritten so that the cause points at the first partial donation, an unrelated earlier event, the decision itself, a later or foreign event, nothing, or a cause appears on a root transaction. Event checksum, NMF token hash and save wrapper hash are recomputed, so every checksum is valid. Every case is refused on load; the unchanged control rewrite reproduces identical bytes. |
| `causal-single-transaction` | One donation moves exactly one ration. Mara tells Ilyan at a real meeting, the player hears it from Ilyan (the conversation names the donation's event ID), the game is saved, loaded and run for 400 more ticks: rations, camp stock, coins and the single Help/Theft event stay as they were. After the final donation decides, a further donation and then a theft are refused; after each one, save bytes, world hash and event count are compared with the state before it. |
| `causal-migration-current` | 28 real saves load: AC1–AC4 history, AC5 and AC17–AC20 from before causes, NMF1/NMF2 road reports, AC22 economy saves, and one current save for each of AC5, AC7–AC12, AC14–AC22. Historical saves upgrade without invented causes; current saves re-save byte-identically; every loaded cause graph is sound; world hashes match. The fixture must hold exactly one current save per still-written format (missing, duplicate or mislabelled formats fail by name). Reserved AC6/AC13 are refused even with a correct checksum. |
| `causal-replay-10000` | Two real `ArenaSimulation` instances, seed 6105, the same camp decision and the same Mara–Ilyan meeting: world hash identical after every one of 10,000 ticks; event/acquisition ledger and save bytes identical every 500 ticks; two cold loads (ticks 3333 and 6666) continue from saved bytes. The final archive holds the reported knowledge of the decision's cause. |
| `causal-reported-knowledge` | No report in 300 ticks while Mara and Ilyan are apart; one report at the real meeting (origin Reported, source Mara); no second mutation of events or rations; the archive itself refuses Ilyan's attempt to report his hearsay to Dorn (he can recall it, but it is no fact) and writes nothing. After 2,000 ticks with all three together: every knower holds the event once, every reported copy comes from a source that knows it as a fact (hearsay is never passed on), Ilyan stays Reported with the same sequence (never an eyewitness), and no event or ration changes. |

The fixture `Tests/CoreVerification/Fixtures/causal-migration-current.txt` holds the 16 current-format saves, captured from real CoreVerification flows (the capture hook was removed again).

**Graph audit and write version:** every format the game still writes passes the cause audit unchanged, so no new write version is needed. Writers stay AC22 and NMF ledger schema 3.

## RED → GREEN

The causal features of #2–#5 are already on `main`, so the new checks were proven against regressions by mutating the real production code, one mutation at a time, each followed by `git checkout` of the touched files. Run at `5b12e89` and again at `f45f6a8` (after the review follow-up below):

| Mutation | `5b12e89` | `f45f6a8` |
|---|---|---|
| M1 `MemoryPersistence.TryLoad` treats every loaded event as legacy (cause validation on load off) | **RED**, exit 1: `causal-corrupt-valid-checksum-reject-first-partial-donation` | **RED**, exit 1: same check |
| M2 only the village relay guard removed (`k.IsFact` in `TellCampNews`) | green | green |
| M3 both hearsay guards removed (`AcceptReport` and `TellCampNews`) | **RED**, exit 1: `causal-reported-knowledge` | **RED**, exit 1: `causal-reported-knowledge-hearsay-is-not-relayed` |
| Restored code | **GREEN**: 22,927 | **GREEN**: 22,930 |

M2 stays green by design: the archive's `AcceptReport` requires a fact, so the village filter is a redundant pre-check (defense in depth). Since `f45f6a8` the archive guard has its own isolated check (`causal-reported-knowledge-hearsay-is-not-relayed`), so removing it alone also turns the suite red.

Review follow-up in `f45f6a8` (CodeRabbit): the decision and its final transaction must share one tick, as the production validator requires; each refused camp action is compared with the state before it; the migration check names missing or duplicate current formats instead of counting saves; the hearsay guard is tested through `Report` rather than a generic exception from `Learn`.

GitHub Actions: on `5b12e89` [run 45072522522, job 137362453640](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45072522522/job/137362453640) with 22,927 Core assertions; on `d883ef9` [run 45073732076, job 137366232380](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45073732076/job/137366232380) with 22,930 Core assertions. Both also passed 4 FwJson, 10/10 world-map scenarios, 45 Fadenwerk, 54 preflight, 76 Director/Skill and 5 gateway tests, and both logs contain the six named checks. Local Core runs (Unity's bundled .NET 8 SDK, Release) on `5b12e89` and on `f45f6a8` report the same counts; `d883ef9` changes only the benchmark.

## Runtime and performance

`Tests/ForgeBenchmark` is an external harness around the real Core: wall-clock time is taken only outside the 100-ms simulation tick. Raw numbers: [forge-benchmark-d883ef9.json](forge-benchmark-d883ef9.json), measured at `d883ef9c1cf30d8eed2ae32b9a15547e4993347a`.

Host: VPS, AMD EPYC 9354P, 8 virtual cores, x64, .NET 8.0.21 (CoreCLR, Release). The host is shared (the live Unity editor and other services kept the load average at about 4.1–4.4 during the run, recorded in the JSON), so timings carry noise; counts, bytes, hashes and allocations are deterministic. These are managed host numbers, **not** Unity Mono, IL2CPP or Android timings.

**Supported world: the causal village with three NPCs**, seed 6107, 10,000 ticks (1,000 simulated seconds), real camp decision and real meeting:

- per tick: p50 0.94 µs, p95 2.12 µs, p99 2.91 µs, max 2,117 µs (tick 99, a one-off first pass through the report path), mean 1.43 µs
- CPU 10 ms (the process CPU clock has 10-ms resolution), wall 17.7 ms
- allocation 1,988 bytes per tick; managed heap 10.4 MB, process working set 77.3 MB (peak 81.3 MB)
- 5 events, 4 acquisitions (1 reported); save 5,071 bytes, archive token 3,202 bytes
- world hash `D610B87E4889B342`, archive checksum `f254e8d3d992b9637e30e86fd3510b5f9a968446ec7b275e73109419d450a084`; save → reload writes the same bytes and the same archive token

**Forge evaluator, labelled separately:** archive minds with 16 memories each, one utility decision plus recall per mind per sweep. These are **not** world NPCs; the game world holds three village NPCs, and no population API for more exists.

| Minds | Events | Sweep mean | Sweep p95 | Per mind | Allocation per sweep | Archive bytes | Save | Load |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 160 | 35.0 µs | 36.8 µs | 3.50 µs | 28,920 B | 64,318 | 0.6 ms | 2.8 ms |
| 100 | 1,600 | 400.4 µs | 423.7 µs | 4.00 µs | 289,200 B | 642,358 | 5.8 ms | 27.3 ms |
| 1,000 | 16,000 | 5.71 ms | 9.07 ms | 5.71 µs | 2,892,000 B | 6,422,758 | 53.0 ms | 281.4 ms |

Every evaluator archive reloads to the same token. Allocation figures come from `GC.GetAllocatedBytesForCurrentThread()` under CoreCLR, where the API measures. Under Unity Mono the same API returns no measurement, and the Unity suite below reports allocations as `unavailable` instead of a zero. Device timings on Android: **UNVERIFIED**.

## Native Unity 6000.6.8f1

Unity CLI run in the live editor on `main` `5d94f238b6494395bdba8b5acef791c6ce53df41` (not a Director job). This PR changes no file under `Assets/`, so the Unity surface under test is the same. Results are Unity's own NUnit XML (`TestRunnerApi.SaveResultToFile`), kept privately with their digests:

| Suite | Result | XML SHA-256 |
|---|---|---|
| EditMode: `NpcForgePerformanceTests`, `ForgePolicyEditModeTests`, `LivingChronicleHudTests` | **4/4 passed**, 0 skipped | `82b089c61adb5d88704c3b8c0fe620d9d2a8f08975dbef97a39b23a8e6a4296b` |
| PlayMode: arrival scene, theft from Mara, asking Ilyan, personal memory over three days, road report only at a village meeting, companion shortcut, village restart with valid NPC history, Ilyan after native save reload, chronicle report after conversation and reload | **9/9 passed**, 0 skipped, 196 s | `bb06894f4c25555b412b14408e5dfe8326342618c57a9edf378a395dc7a9e86c` |

Editor log of both runs (686 lines, no exception, no compiler error): SHA-256 `4e6a03bd8648a31033031c6256fec4b198af039df3192701234a4c83a0b8de4c`.

Unity Mono numbers from `NpcForgePerformanceTests` (editor, managed, not device timings), median and p95 batch in µs: guard decision 4.9 / 9.9; decision over 10 minds 92.4 / 123.7, 100 minds 89.7 / 6,804.0, 1,000 minds 1,262.6 / 9,172.9; recall 64 of 1,000 events 35.3 / 37.8; actual village host tick with three NPCs 5.1 / 7.9; save 1,000 events 10,006 / 29,246; restore 62,699 / 88,337. Allocation per operation: `unavailable` for every row.

## Android

APK built in the live Unity 6000.6.8f1 editor on `main` `5d94f238b6494395bdba8b5acef791c6ce53df41` (this PR changes no Unity or Android surface) with the canonical `ArpgBuildTools.BuildAndroidApk`, the private art of that workspace and its local editor-tool packages; inspected with the Unity Game Director's `inspect_build`, `apksigner`, `aapt2` and `zipalign`:

- SHA-256 `1ced0ebf4c815abd2792e4be94ac358fa38f087a01fc38a752abb4bcc0bed341`, 275,464,965 bytes
- ARM64 IL2CPP structure present (`libil2cpp.so`, `libunity.so`, `classes.dex`); `com.ouroboroscollective.areashencrypt`, minSdk 26, targetSdk 36, native code `arm64-v8a` only
- APK Signature Scheme v2 verified; **debug certificate** (SHA-256 `7b6c7bffa6cc5ff397b317fe10169ab9ba56a2440e90fa19d13308f490377f4a`), not a release signature
- ZIP entries and native libraries aligned to 16 KiB pages (`zipalign -c -P 16`)

Installation, cold start, touch input and save/restore on a **physical device: UNVERIFIED** (no device is attached to the build host).

## Release and readback gates

1. Final PR head checks green, no unresolved review threads, expected base `main`.
2. Exactly one concise `Memory.md` entry plus the logbook handoff section.
3. After merge: merged `main` tree equals the final PR head; world hash and archive checksum read back again on `main`; issue state re-read.
