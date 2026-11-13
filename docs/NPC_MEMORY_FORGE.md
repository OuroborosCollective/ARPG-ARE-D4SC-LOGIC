# NPC Memory Forge

## Authority and current integration

`AreArpg.Core` remains the only gameplay authority. `SocialLedger.Forge` owns the archive; `ArenaSimulation.Step` advances its processed-tick counter exactly once per existing 100-ms step. The pure `AshenCrypt.MemoryForge` assembly has no clock, world queries, Unity dependency, economy or save file. Unity displays accepted Core behavior. No network model or runtime training is used.

The first slice binds Ilyan (`traveler`), Mara (`merchant`) and Dorn (`guard`) at canonical construction. Actual accepted greeting, provision help, donation, assault and theft produce episodes. Failed transactions produce no episode. A victim must be conscious during the action, or actually receive treatment, to acquire direct knowledge. Crime witnesses require the existing distance and geometry checks. The ration-theft path retains its original Dorn perception rule. Help is private to its recipient.

Ilyan's help produces trust and gratitude; near a trusted player he may select `Observe` and turn toward them. His later greeting refers to the provision help. He still rests, eats actual stock, travels and responds to danger through existing Core effects. This is a weighted local controller, not a neural model. Neural policy experiments and other population bindings are subsequent work.

Existing `NpcMemory` BigInteger action accounts are preserved and updated once by their original transaction. Forge relationships interpret the accepted receipt; they do not issue another reward or another `Record`. Older accounts remain available for disposition and dialogue. Migration does not fabricate dated historical episodes from aggregate counters.

## Contracts

- `AshenCrypt.NpcArchetypeControllerRegistry.v1`: immutable archetype/action/observation manifests, controller ID/version/hash, runtime/memory compatibility and real evaluator regression receipts. Registration rejects mismatched hashes and failed suites. Historical restore requires the exact tuple; the latest release is only for new spawns. Unknown spawn roles bind the admitted `fallback` controller.
- `AshenCrypt.NpcMemory.v1`: immutable event history plus per-person acquisition history and relationships. Origins distinguish self, witnessed, reported, suspected and refuted. Reports require a source's direct knowledge and preserve its acquisition reference; relayed rumors cannot become direct facts. Duplicate event acquisition does not duplicate a relationship delta.
- Short-term observations are the last actual decision frame; episodic history is retained; social dimensions are derived from the acquisitions. Semantic claims use typed events and provenance. Essential quest/death references never expire from recall. Individual story integrations beyond the three village NPCs are not yet complete.

Utility uses scale 10,000, checked signed integer multiplication and nearest rounding with exact halves away from zero. Input observations are bounded 0–10,000; weights ±40,000. The host's hard legal mask intersects the archetype whitelist before scoring. Unique versioned rule order breaks ties. Decision intervals, minimum durations and hysteresis use Core ticks; an illegal current action is interrupted immediately. Traces show actual input values and individual weighted contributions.

Recall relevance multiplies importance, personal impact, credibility, social proximity, contextual relevance and tick retention. Ordinary relevance fades over 36,000 ticks; the immutable history and deduplication records remain. Active recall considers the latest64 ordinary memories plus essential facts. This bounds ordinary query work, not total archive storage. Archive decode has an explicit configurable16-MiB payload budget; large-world retention/storage remains part of the performance work.

## Persistence

AC17 wraps the complete previously validated save (including AC16 equipment and AC15/14 world state). Original AC1/2/3 and later accepted formats remain readable; AC6/13 remain reserved. Nested AC17 and AC16 wrappers are rejected. The existing CharacterProgression checksum and PlayerPrefs readback still apply.

`NMF1` stores canonical events, exact controller bindings, acquisition provenance and decision continuity with SHA-256 checksums. Load reconstructs relationships once and compares a canonical reserialization. No silent controller migration occurs. Village checkpoints additionally preserve player/NPC positions, previous positions, input, RNG, cooldowns, patrol state and Core tick; other regions retain their established save owners. The entire village world hash and subsequent tick sequence are tested across restore.

## Editor and verification

Open **ARE ARPG → NPC Memory Forge** during Play Mode in the village. Select an actual NPC to inspect its bound artifact, observations, excluded actions, utility contributions, relationship dimensions and episode sources. The candidate control evaluates a separate immutable artifact against the same observations; it does not mutate the active NPC or publish a policy.

Core regression command:

```sh
dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release
python3 -m unittest discover -s Tests/Preflight -q
```

`Tests/ForgeBenchmark` measures the actual evaluator, archive and three-NPC village on its current managed host. Its batch-mean timings and allocations are not Android frame-rate evidence. Native Unity test `IlyanRemembersActualHelpAndReactsAfterNativeSaveReload` uses the real input queue, private character models, PlayerPrefs and projection. Final SHA-bound results belong in Memory.md only after native acceptance.

## File ownership

| Area | Files and responsibility |
|---|---|
| Core | `Assets/AshenCrypt/MemoryForge/Core`: pure contracts and calculations; host owns every instance |
| Production | `Assets/Scripts/Core/NpcForgeBehavior.cs`: canonical observations, hard permissions, existing effects |
| Persistence | `NpcForgeState.cs`, `NpcForgeVillageCheckpoint.cs`: existing SocialLedger/CharacterProgression ownership |
| Effect / Unity projection | `ArenaRuntime`: accepted social facing and real scene initialization |
| Editor | `NpcForgeInspectorWindow`: readback and isolated candidate comparison |
| Test | `NpcForgeTests`, `NpcForgeIntegrationTests`, `NpcForgeRuntimeTests`, `ForgeBenchmark` |

The sale package must contain only independently owned Forge code, documentation and original sample content. Licensed Ashen Crypt art, game saves and private authoring inputs are not redistributable package dependencies.
