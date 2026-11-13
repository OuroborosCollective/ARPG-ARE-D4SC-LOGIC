# NPC Memory Forge

## Authority and current integration

`AreArpg.Core` remains the only gameplay authority. `SocialLedger.Forge` owns the archive; `ArenaSimulation.Step` advances its processed-tick counter exactly once per existing 100-ms step. The pure `AshenCrypt.MemoryForge` assembly has no clock, world queries, Unity dependency, economy or save file. Unity displays accepted Core behavior. No network model or runtime training is used.

Canonical construction binds Ilyan (`traveler`), Mara (`merchant`), Dorn (`guard`), the rescued hunter (`companion`), the road traveler and actual creature spawns. Mill patients and bear cubs receive identities when their authored region is present. Creature IDs include region, run seed and depth; the offscreen road bear retains its road identity. Actual accepted greeting, provision help, donation, assault and theft produce episodes. Failed transactions produce no episode. A victim must be conscious during the action, or actually receive treatment, to acquire direct knowledge. Crime witnesses require the existing distance and geometry checks, including village building occlusion. Help is private to its recipient.

Ilyan's help produces trust and gratitude; near a trusted player he may select `Observe` and turn toward them. His later greeting refers to the provision help. He still rests, eats actual stock, travels and responds to danger through existing Core effects. The released game defaults use utility policies. Optional integer neural artifacts share the same permission, admission, version and persistence contracts; the offline experiment did not justify replacing the simpler utility policies.

Existing `NpcMemory` BigInteger action accounts are preserved and updated once by their original transaction. Forge relationships interpret the accepted receipt; they do not issue another reward or another `Record`. Older accounts remain available for disposition and dialogue. Migration does not fabricate dated historical episodes from aggregate counters.

## Contracts

- `AshenCrypt.NpcArchetypeControllerRegistry.v1`: immutable archetype/action/observation manifests, controller ID/version/hash, runtime/memory compatibility and real evaluator regression receipts. Registration rejects mismatched hashes and failed suites. Historical restore requires the exact tuple; the latest release is only for new spawns. Unknown spawn roles bind the admitted `fallback` controller.
- `AshenCrypt.NpcMemory.v1`: immutable event history plus per-person acquisition history and relationships. Origins distinguish self, witnessed, reported, suspected and refuted. Reports require a source's direct knowledge and preserve its acquisition reference; relayed rumors cannot become direct facts. Duplicate event acquisition does not duplicate a relationship delta.
- Short-term observations are the last actual decision frame; episodic history is retained; social dimensions are derived from acquisitions. Semantic claims use typed events and provenance. Essential quest/death references never expire from recall. Story memories record actual conversations and choices; remembering a player's report does not assert that the reported claim is objectively true. A nearby companion witnesses only encounters permitted by actual distance and sight. No new beggar or healer actor was invented where the current Core has none.

Utility uses scale 10,000, checked signed integer multiplication and nearest rounding with exact halves away from zero. Input observations are bounded 0–10,000; weights ±40,000. The host's hard legal mask intersects the archetype whitelist before scoring. Unique versioned rule order breaks ties. Decision intervals, minimum durations and hysteresis use Core ticks; an illegal current action is interrupted immediately. Traces show actual input values and individual weighted contributions.

Recall relevance multiplies importance, personal impact, credibility, social proximity, contextual relevance and tick retention. Ordinary relevance fades over 36,000 ticks; the immutable history and deduplication records remain. Active recall considers the latest64 ordinary memories plus essential facts. This bounds ordinary query work, not total archive storage. Archive decode has an explicit configurable16-MiB payload budget; large-world retention/storage remains part of the performance work.

## Persistence

AC17 wraps the complete previously validated save (including AC16 equipment and AC15/14 world state). Original AC1/2/3 and later accepted formats remain readable; AC6/13 remain reserved. Nested AC17 and AC16 wrappers are rejected. The existing CharacterProgression checksum and PlayerPrefs readback still apply.

`NMF1` stores canonical events, exact controller bindings, acquisition provenance and decision continuity with SHA-256 checksums. `NMF2` adds explicit controller migration receipts and validates the entire source/target chain against retained historical admissions. Existing NMF1 bytes are unchanged until a migration occurs. Load reconstructs relationships once and compares canonical reserialization. No silent controller migration occurs. Village checkpoints additionally preserve player/NPC positions, previous positions, input, RNG, cooldowns, patrol state and Core tick; other regions retain their established save owners. The entire village world hash and subsequent tick sequence are tested across restore.

AC18 adds the recruited companion around the complete AC17 save. Its v2 companion token keeps area seed and dungeon floor separate, preventing identity collisions. A following companion is positioned only after the passage determines the player's final entrance. Waiting/dead companions remain in their own area. Controller migration resets decision continuity explicitly; its migration tick remains part of restore validation.

## Companion and creature effects

The rescued living hunter can join, wait in a safe authored area, rejoin, receive actual owned equipment and consume an actual provision for needed care. The BEGLEITER tab uses the existing game menu theme. The owned hunter model, walking clip, bow attack clip and equipped weapon visuals project Core state; there is no duplicate road hunter after recruitment.

The companion owns health, hunger, fatigue, BigInteger experience and practice accounts. Only an effective lawful hit awards practice; waiting, replay, loading and player level increases do not. Gear receipts have one equipped owner. Death remains permanent under the current rules and cannot use village recovery to revive. The historical road rescue outcome is retained separately from the recruited companion's live health.

Ordinary enemies can attack the nearer visible living party member. The Rattler retains sound/scent perception and telegraphed strikes, including companion targets and damage. Authored animal encounters honor admitted controller decisions when beginning pursuit/attacks; an already committed telegraph resolves under its existing combat rules. Surviving creatures remember actual assaults individually. Existing story progress and farm-run loot remain owned by their original systems.

## Offline research and performance

`Tests/ForgeResearch` compares an explicitly faulty guard network, an offline weight search, a held-out/domain sweep, a specification-derived integer correction and a simple utility alternative against a behavior-tree oracle. The training-selected weight passed 12/12 training cases but failed 3/24 held-out cases and 315/1681 broader cases; it was rejected. The corrected neural and utility policies each pass 1722 finite admission cases. This is not proof over every possible input and not evidence of superior neural gameplay. The measured utility alternative was faster, so default game policies remain utility-based.

Primary research informed the experiment, not its runtime acceptance: [Weight-Space Programming](https://rikiyat.github.io/blog/weight-space-programming/), [SCBT](https://arxiv.org/html/2610.25455), [verified policy research](https://arxiv.org/abs/2611.11604). No runtime LLM, network service, online weight mutation or paid inference service is used.

Diagnostic score contributions and traces are formatted on demand. The actual .NET8 allocation test fell from 4976 to 480 bytes per restricted guard evaluation. Unity Editor timing is measured separately by `NpcForgePerformanceTests`, including 10/100/1000 minds, recall, serialization and the real three-NPC village tick. Unity Mono returned zero for its allocation-counter API in the initial measurement; those zeroes are **unavailable measurements**, not evidence of zero allocation. The corrected report labels them accordingly. Shared-VPS timing is noisy and does not establish Android frame rates. Save/restore work dominates the measured utility calculation; no separate C++ path has a demonstrated benefit, so none is introduced.

## Editor and verification

Open **ARE ARPG → NPC Memory Forge** during Play Mode. Select a host-owned mind to inspect its bound artifact, observations, excluded actions, utility contributions, relationships, episode sources, approval receipt and optional network. The candidate control evaluates a separate immutable artifact against the same observations. A distinct explicit migration control accepts only an already admitted exact target and saves the resulting receipt. It cannot promote an unreviewed slider candidate.

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
| Production | `NpcForgeBehavior.cs`, `NpcForgePopulation.cs`, `NpcForgeStory.cs`, `NpcForgeCompanion*.cs`: observations, permissions and existing/newly tested effects |
| Persistence | `NpcForgeState.cs`, `NpcForgeVillageCheckpoint.cs`, AC18 companion token: existing SocialLedger/CharacterProgression ownership |
| Effect / Unity projection | `ArenaRuntime`, `CompanionPresentation.cs`: accepted behavior, actual controls/models/animations and scene initialization |
| Editor | `NpcForgeInspectorWindow`: readback and isolated candidate comparison |
| Test | `NpcForgeTests`, `NpcForgeIntegrationTests`, `NpcForgeRuntimeTests`, `ForgeBenchmark` |

The sale package must contain only independently owned Forge code, documentation and original sample content. Licensed Ashen Crypt art, game saves and private authoring inputs are not redistributable package dependencies.
