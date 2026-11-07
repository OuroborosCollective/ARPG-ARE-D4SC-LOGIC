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