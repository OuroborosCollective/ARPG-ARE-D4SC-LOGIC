# Persistence, performance and limits

## Save ownership

Embed `MemoryArchive.Save()` in the host's existing save transaction. Save the host tick, RNG/seed, world facts, actor needs/positions, cooldowns, equipment and event receipts in that same transaction. The archive does not replace them. Restore with `MemoryArchive.TryLoad(token, registry, out archive)`, then bind presentation views to the restored minds. On failure keep the previous live state and expose a recovery choice; never silently create a fresh history.

`NMF1` preserves event history, individual acquisition provenance, exact controller bindings and decision continuity. `NMF2` adds explicit migration receipts while retaining support for NMF1. Checksums cover canonical serialized values. Unknown schemas, missing historical controllers, tampered hashes, illegal continuity or invalid provenance are rejected. Checksums detect corruption; they do not prevent a malicious user who can recompute them from editing an offline save.

The default decode budget is 16 MiB of binary archive data and is configurable through `maximumBytes`. This is an input-allocation guard, not automatic history compaction. Essential history and deduplication receipts are retained. An indefinitely running game must budget archive growth and implement host-owned archival/persistence before exceeding its configured save limit. Measure the resulting save size; do not drop event IDs or shared references opportunistically. Ordinary relevance queries inspect the most recent 64 ordinary memories plus all relevant essential memories.

## Determinism

Identical host state, controller artifacts, event IDs/order, seeds and tick inputs yield identical decisions and serialized archives. Controllers do not read wall-clock time, Unity random state or frame time. The example's MonoBehaviour converts elapsed presentation time into fixed 100-ms sample-host steps. For a networked game, supply ticks and commands from your own authoritative simulation; networking and rollback transport are not included.

## Performance practice

Build and share immutable archetype artifacts once. Keep each mind's memories separate. Stagger host decision opportunities by canonical tick if required; do not use random frame scheduling. Reevaluate promptly when hard permissions change. Do not call diagnostic formatting, full `Known` snapshots or full save serialization every frame.

Decision evaluation allocates managed result objects. This is not a zero-allocation/DOTS/Burst library. The included source supports profiling and targeted optimization. The measured allocation improvement in the project removed eager trace formatting; it does not eliminate all allocations. Save/load and long histories are more expensive than an individual utility decision.

No separate native C++ plugin is shipped. Unity IL2CPP compiles the managed implementation for supported targets. A custom native path was not justified by the available measurements and would require exact parity and measured device benefit before adoption.

Use the release evidence to distinguish .NET benchmarks, Unity Editor tests, Android ARM64 builds and physical device tests. Editor microseconds do not establish Galaxy Tab A9 frame rate. No universal NPC-count or FPS guarantee is made. Profile your action adapters, navigation, animation, memory retention and save transport as well as this evaluator.

## Troubleshooting

- **NPC waits:** inspect its legal-action mask, supplied observations, selected role, cooldown and action adapter. A registry fallback deliberately does not improvise capabilities.
- **NPC knows too much:** audit where your host calls `Learn`. The library cannot validate scene visibility on behalf of the host.
- **Old save fails:** restore all exact historical artifacts and their admissions. Do not replace them with a newer version.
- **Approved asset is rejected:** its bytes or regression receipt changed. Fork a new version, test it, then select or migrate explicitly.
- **Sample is pink:** open the included scene and verify the active pipeline and its serialized shader references. The helper selects URP Lit/Unlit only when a Universal pipeline is active; otherwise it uses Standard/Unlit Color. Merely installing URP does not switch this selection. HDRP is not a validated sample presentation target.
- **No test assemblies:** install Unity Test Framework. Production assemblies do not require NUnit.
