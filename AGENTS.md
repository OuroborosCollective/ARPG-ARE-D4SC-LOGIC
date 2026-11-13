# Agent instructions: ARE / Ashen Crypt

- This repository is an independent Unity ARPG prototype, not Echoes_of_Aurion. Never connect it to Aurion live authority or copy proprietary game engine code/assets.
- Read `Memory.md` before editing. Preserve the core simulation/presentation boundary.
- Implement a failing behavioral test before repairing a bug; run the real `Tests/CoreVerification` project and preserve CI evidence.
- Do not claim Unity Editor, APK, runtime, GPU or multiplayer success without execution against the pinned revision and artifact.
- Before merges, update `Memory.md` with exactly one concise entry giving change, finding and evidence. Maintain SHA-bound independent readbacks and explicit blockers.
- No `DateTime.Now`, `UnityEngine.Random`, `Time.deltaTime`, `string.GetHashCode()` or nondeterministic iteration in authoritative core logic. Unity rendering may use delta time for interpolation only.
- For requested spec-driven work, read `docs/SPEC_DIRECTOR_WORKFLOW.md`. Spec Kit owns the change contract, SPD plans/reviews, and the existing Director executes checks. These workflows retain the rules above and existing user decisions; upstream optional-testing defaults do not apply to bug repairs. Ask the owner about unresolved story/gameplay choices, not already authorized technical phase transitions.
