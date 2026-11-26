# ARE / Ashen Crypt - architecture contract (v0.1)

**Scope:** standalone, local Unity action RPG experiment. It does not import or control any Echoes_of_Aurion runtime, server, world state or API. The 100 ms core step was chosen as a simple fixed tick for this prototype; it does not purport to reproduce any Diablo IV engine timing. A future production architecture may use a different tick after evidence-based profiling.

## Ownership boundaries

| Surface | Authority | Responsibility |
| --- | --- | --- |
| `Assets/Scripts/Core/ArpgSimulation.cs` | Local game simulation | Tick, HP, movement, collision, damage, AI, drops, resource, cooldown and hash. No `UnityEngine`, clocks or unseeded RNG. |
| `Assets/Scripts/Runtime/ArenaRuntime.cs` | Projection and input | Camera, scene geometry, HUD, rendering, keyboard/touch collection and queueing commands for the next tick. Never overwrites authoritative coordinates. |
| `Assets/Editor/ArpgBuildTools.cs` | Editor/build | Scene preparation and fail-closed Android build; needs licensed Unity Editor for execution. |
| `Tests/CoreVerification/` | Regression, not runtime | Exercises the exact shared simulation C# file via .NET. Not evidence of rendering, actual device input, Android APK, or networking. |
| `.github/workflows/core-checks.yml` | CI test receipt | Executes the C# core tests. It does not assert Unity scene loading. |
| `.github/workflows/unity-android-build.yml` | Manual gated build | Opt-in Unity Android artifact attempt; requires Unity license/build environment, separate artifact/readback. |

## Determinism

All simulation game-state transitions use integers, millimeter positions, explicit seed/PRNG, and a fixed number of simulation steps. Rendering uses floats and wall-clock delta time ONLY to interpolate positions and camera. `ComputeWorldHash()` serializes the state fields that currently affect the simulation in stable order; avoid runtime string hashes. Replay verification exercises two independent real core instances with the same seeded input schedule, without mocks. A cross-CPU/compiler reproducibility claim additionally requires tests across those environments and remains **unverified**.

## Known constraints

- No high-frequency contact physics or proper pathfinding yet; pillar avoidance is intentionally simplistic.
- The runtime consumes keyboard/touch commands at the next simulation step, but no network protocol, anti-cheat authority or real multiplayer exists.
- No original commercial assets are bundled. Programmatically constructed primitive geometry is prototype art, not a reconstructed proprietary pipeline.
- GPU hardware-specific optimizations, including AMD GPU/CPU measurements, are not claimed without benchmark hardware and receipts.

## Play Mode verification gate

`Assets/Tests/PlayMode/ArenaBootSmokeTests.cs` compiles against the real Core and Unity Runtime assemblies and checks that the runtime actually builds a scene with hero, boss, and camera. The manually dispatched `.github/workflows/unity-playmode-smoke.yml` runs the official Unity play-mode test runner through GameCI and **fails if valid Unity credentials are unavailable**. It must not be mistaken for an Android-device test.

The Android build lane remains separate: a successful APK compilation would need artifact hashing and installation on a physical Android device before any mobile-runtime claim.

## Permanent character boundary

- `CharacterProgression` is the persistent character owner: arbitrary-precision total XP, level derived deterministically using binary search over `75 * (L-1) * L`, unspent mastery points, lifetime kills and collected loot, finite weapon upgrade bonus. No season resets or forced rebirth.
- `ArenaSimulation` owns one transient battle run, receives the progression object explicitly and awards XP *once* at monster death; returning to arena never resets progression. World hash mixes the canonical progression hash, avoiding platform-dependent `GetHashCode()` semantics.
- `ArenaRuntime` owns Android/desktop `PlayerPrefs` persistence only; Unity presentation does not calculate XP. Saves use versioned `AC1` canonical decimal fields and FNV-1a validation with a valid prior backup and readback, not a cryptographic signature. Missing save means first-run; corrupt primary/backup means gameplay stops instead of overwriting unknown state.
- Numeric ability coefficients and physical statistics are intentionally finite. Arbitrary-precision XP does not imply physically infinite damage/HP, nor an automatic online database or cloud account. Device deletion and data loss are not yet covered by a remote backup service.

## Mastery contract

The integer-only authoritative core owns mastery allocation. `CharacterProgression.TrySpendMastery` rejects invalid paths and insufficient earned points without mutating state. Earned points = level - 1. AC1 legacy files load without allocations, AC2 persists three arbitrary-precision ranks and rejects overspending even with a matching checksum. Mastery effects are bounded to 100 ranks for Might and 1000 ranks for Vitality/Resolve so finite hitpoint/damage calculations cannot silently overflow. UI only queues decisions for application during the next 100ms simulation step; the resulting progress is locally persisted by the Unity adapter. No seasonal reset implementation exists.

## Crypt continuation state

The immutable run identity is the explicit positive `BigInteger CryptDepth`, included in the canonical `ComputeWorldHash()` as length-prefixed little-endian bytes. `TryAdvanceCrypt()` is fail-closed unless the run is won and all spawned loot has been collected, returning a fresh simulation reusing the same `CharacterProgression`. Enemy HP grows by a depth multiplier capped at 10,000; arithmetic remains within int limits at arbitrarily large depth. This singleplayer run depth is currently **session-local**; `CharacterProgression` remains the only saved character state. No asynchronous or network authority is implied.

## AC3 chapter checkpoint boundary

`CharacterProgression` now owns a positive arbitrary-precision unlocked chapter depth, included as field 9 (before the checksum) in AC3 records; AC1/AC2 load with depth 1. The chapter transition deep-copies canonical character state, checks consecutive depth and writes the next checkpoint through Unity's existing validated PlayerPrefs path *before* swapping the active world. Prior world receipts are left unchanged. This is a local checkpoint, not an online account, atomic filesystem transaction or entire simulation snapshot. Android cold-start persistence remains unverified.

The Aschenzoll Nordwechsel uses this boundary with the current story, catalog and equipment save wrappers. In the station, the western arch leads to Ember Rift after the reported Rattler victory, either personally reported freight outcome and collection of outstanding loot. The central arch still leads to the forest road. The client reads back the exact saved text, parses it with the bound map catalog and compares its progression hash before installing the destination. If this verification fails, it restores the prior checkpoint slots and keeps the active world. A successful entry stores story act 13 and depth 2. Later depths reuse the same Warden rules and require a cleared encounter and collected loot at the northern exit.

---

## Neural Game Director: gemeinsame Architektur für 28 Fachberater

<!-- ashen.shared-architecture.v1 -->

**Neuere Ergänzung: 18. November 2026, Unity 6000.6.8f1.** Der obenstehende ältere Architekturtext bleibt als historischer Vertrag erhalten. Bei widersprüchlichen Aussagen gelten die tatsächlich aktive Code-/Save-Authority, `AGENTS.md`, `Memory.md` und die neuesten freigegebenen, SHA-gebundenen Projektverträge. Ein Wechsel des bestehenden **100-ms-/10-Hz-Ticks** ist nicht stillschweigend erlaubt.

### Rollen und gemeinsame Lesesicht

Der bestehende Unity Game Director ist **die einzige Entwicklungszentrale**. Die 28 Berater in `Tools/Director/neural_catalog.json` sind fachlich getrennte, beratende Module für Gameplay, Combat, NPCs, World/Level, Lore/Quests, Dungeons, Shader/VFX, Assets, Android/QA, Performance und die übrigen Bereiche. Sie erhalten im Strategiemodus denselben Byte-identischen Architektur-Snapshot. Im Arbeitsmodus bleiben höchstens drei konkret relevante Module ausgewählt.

Der Snapshot besteht aus **exakter Git-Revision + SHA-256 dieser Markdown-Datei + SHA-256 des Fachkatalogs + Unity-Pin**. `Tools/Director/shared_architecture.py` erstellt 28 Berater-Pakete mit identischen Architekturbytes und einer gemeinsamen Snapshot-ID. Jede Abteilung antwortet genau einmal mit `PROPOSE` oder `ABSTAIN`; Beiträge müssen ihren Bereich, dieselbe Snapshot-ID, erlaubte Skill-Verweise und überprüfbare Projektbezüge enthalten. Bei fehlenden oder veralteten Antworten, manipulierten Daten und unbekannten Werkzeugen wird geschlossen abgelehnt. **Derzeit handelt es sich um einen Offline-Vertragsprüfer, nicht um 28 laufende KI-Instanzen und nicht um einen tatsächlichen MCP-Dispatcher.**

### Viele Leser, ein kontrollierter Schreiber

1. Alle Berater lesen parallel eine **eingefrorene Revision**, nicht eine während der Beratung mutierende Datei. Beiträge enthalten diesen gemeinsamen Ursprung und begründete Verbesserungsvorschläge.
2. Fachberater **schreiben nicht direkt** in diese Datei. Änderungen gehen als gesonderte Vorschläge in PRs oder Issues: Bereich, Ausgangs-SHA, Architektur-Hash, Auswirkungen auf Core/Save/UI/Assets, Tests, Risiken und Quellen.
3. Der Director führt fachübergreifende Vorschläge zusammen, erkennt Überschneidungen und Konflikte und gibt niemals eigenmächtig Lore- oder Produktentscheidungen als akzeptiert aus.
4. Nur ein autorisierter Git-Schreibvorgang mit **Compare-and-Swap auf die aktuelle Blob-SHA** aktualisiert dieses Dokument; bei konkurrierenden Änderungen ist ein neuer Readback und Rebase erforderlich, kein Last-Writer-Wins.
5. Nach Review, CI und Merge lesen alle Berater ab der neuen **exakten** Revision. Bereits laufende Beratungsrunden mit alter Snapshot-ID sind **stale**, bis sie neu bewertet werden.

### Director: „Was machen wir als Nächstes?“

Für eine fachübergreifende Strategieberatung sendet der Director denselben Stand an **alle 28 Fachberater**. Jeder kann einen fachlichen Vorschlag oder eine begründete Enthaltung liefern. Der Director stellt anschließend nach validierten Abhängigkeiten **drei unterscheidbare Entwicklungswege** zusammen, beispielsweise Gameplay, Story und Welt, jeweils mit Architekturwirkung, passenden bestehenden Werkzeugen, Risiken, Prüfplan und Quellen. Wenn nicht drei belegbare Wege vorliegen, lautet der Zustand **INSUFFICIENT_DISTINCT_PATHS**; niemals drei Vorschläge erfinden.

Relevante vorhandene Entscheidungen und Aufgaben: [Grenzregion-Epic #60](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/60), [Quality-Epic #48](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/48). **Der aktuelle Status** dieser und weiterer Issues wird pro Beratungsrunde neu gelesen; ein Link oder ein alter Stand beweist keine Erfüllung. `Memory.md` und das private Evidenz-Logbuch bleiben getrennte Verlaufsquellen; die Architekturdatei ist weder eine Spielstandsdatei noch ein Live-Beweis.

### Unverrückbare Grenzen

- **Core**: deterministische 100-ms-Schritte, Seed, NPC- und Kampflogik, Questfortschritt, Save/Replay verbleiben in der bestehenden Core-Authority. Unity ist Authoring/Projektion; kein zweiter Simulationseigentümer.
- **Tools**: Die Skills sind Wissen und Vorschläge. Der externe mcp-use-Hub ist lediglich eine künftige periphere Verbindung zum bestehenden Sovott-v2-Broker, keine neue Freigabe- oder Ausführungs-Authority.
- **Review & Evidence**: Keine KI-Empfehlung darf ohne Tool-Vertrag, Revisionsabgleich, erforderliche Owner-Freigabe, realen Test und unabhängigen Ziel-Readback zum Produktionsstatus werden.
- **Status**: Die hier beschriebene Beratungsrunde ist **nicht deployed**. `shared_architecture.py` ruft keine KI auf, startet keinen Container, verändert kein Unity-Projekt und schreibt nicht in Git. Echte Unity-EditMode-/PlayMode-, Android-/Tab-A9- und MCP-Runtime-Nachweise bleiben eigenständig zu erbringen.
