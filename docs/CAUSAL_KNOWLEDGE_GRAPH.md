# Ashen Crypt: source-only causal knowledge graph (first slice, Issue #111)

## Identity and authority
`AshenCrypt.MemoryForge.CausalKnowledgeGraph` is an **opt-in derived inspection** of existing `MemoryArchive.Events` and `MemoryArchive.Acquisitions`. It must never be called as a replacement for the owning `ArenaSimulation`, `MemoryArchive`, `CharacterProgression`, Fadenwerk, NPC controller or Unity Editor. No new save format, clock, runtime queue, graph database or world authority is created.

The current contract is `ashen.knowledge-graph.v1`. `Build(archive, sourceRevision)` accepts an explicitly supplied, full lowercase 40-hex Git SHA. This is *source identity asserted by the caller*, **not independent verification** that a deployed editor or runtime actually uses that SHA. External callers must independently verify the source checkout/receipt before making stronger claims. `CanonicalText` uses ordinal-sorted, length-prefixed nodes/edges and `Digest` is lowercase SHA-256 of its UTF-8 bytes. Graph generation never mutates the owning archive, save, tick or world hash.

## Available graph relations
- `action:<eventId> --archived-as--> event:<eventId>`: accepted canonical `camp.theft` or `camp.donation` transaction from the existing Forge archive, not an inferred player intention
- `event:<parentId> --causes--> event:<childId>`: only the existing `CauseEventId` with an earlier parent and nondecreasing event tick, no invented consequence
- `event:<id> --learned-<origin>--> knowledge:<sequence> --known-by--> npc:<stableId>`: only an actual archive acquisition; `Self/Witnessed/Reported/Suspected/Refuted` retain their original distinctions. `Reported` and `Suspected` are not promoted to observed facts

Historical acquisitions remain in the index for audit, but only the **currently admitted** `NpcMind.Knowledge(eventId)` entry may have a `known-by` edge. A `Refuted` current acquisition has **no active `known-by` edge**; superseded `Suspected`/`Reported` beliefs cannot reappear as current knowledge. No `Why` path may promote a refutation to an NPC fact. This edge case was reproduced with failing RED Core CI `44993338918` and fixed with a new Core and Unity NUnit regression.

The minimal `Why(fromId, toId)` returns a deterministic shortest path, or the empty path if no chain exists. Nodes representing NPC knowledge are **not emitted** merely because the NPC has spawned or was near an event; a canonical acquisition is necessary. The graph never synthesizes an NPC reaction, controller choice, quest advancement or Unity rendering. Each edge is `SOURCE_ONLY`, including when the input arose from a Core test. `SOURCE_ONLY` is not evidence of active deployed gameplay.

## Failure and volume limits
Missing source SHA, parent event, a forward/self parent, future knowledge, orphan source or conflicting graph identity fails closed. The first inspection version refuses more than 20,000 events or 40,000 acquisitions. The index is rebuilt on demand and is not in the 100-ms runtime tick.

## Acceptance and evidence
`Tests/CoreVerification/CausalKnowledgeGraphTests.cs` executes the existing Core camp theft against real `ArenaSimulation` and then inspects its canonical archive; it also exercises a separate canonical Forge acquisition and negative no-witness case. Tests assert replay-/save-byte stability, world hash unchanged by queries, deterministic canonical graph/digest and invalid revision rejection. The separate Forge acquisition is **test data from real canonical code**, not a claim that a deployed NPC reacted.

Unity 6000.6.8f1 Editor/PlayMode, private assets, APK, physical tablet, production runtime/Director receipts and cross-runtime performance measurements remain separate and **UNVERIFIED** until independently exercised on the final source SHA. A successful .NET Core CI job does not supersede these gates. Full issue #111 (Director `why` API, static architecture index, quest/world integration, runtime receipt binding) remains open.
