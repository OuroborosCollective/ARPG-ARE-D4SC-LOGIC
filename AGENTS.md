# Agent instructions: ARE / Ashen Crypt

- This repository is an independent Unity ARPG prototype, not Echoes_of_Aurion. Never connect it to Aurion live authority or copy proprietary game engine code/assets.
- Read `Memory.md` before editing. Preserve the core simulation/presentation boundary.
- Consult the relevant Ashen Crypt Neuro Director departments for each work area and again when a new design or integration question arises. Record the advice and verify the decision against the current project.
- Owner instruction 2026-11-16: maintain the persistent `Ashen_Crypt_Evidenz_Logbuch.docx` (Design Report). Read `docs/evidence/quality-sky-20261116/logbook-handoff.md` before resuming. After each completed work round append revision-bound changes, executed evidence, limits and next steps; replace the same owner document identity, preserve earlier rounds, and update the repository handoff.
- Implement a failing behavioral test before repairing a bug; run the real `Tests/CoreVerification` project and preserve CI evidence.
- Do not claim Unity Editor, APK, runtime, GPU or multiplayer success without execution against the pinned revision and artifact.
- Physical tablet testing is optional for issue completion. Record the device result separately from the APK build and note when the tablet test was not run.
- Before merges, update `Memory.md` with exactly one concise entry giving change, finding and evidence. Maintain SHA-bound independent readbacks and explicit blockers.
- No `DateTime.Now`, `UnityEngine.Random`, `Time.deltaTime`, `string.GetHashCode()` or nondeterministic iteration in authoritative core logic. Unity rendering may use delta time for interpolation only.
- For requested spec-driven work, read `docs/SPEC_DIRECTOR_WORKFLOW.md`. Spec Kit owns the change contract, SPD plans/reviews, and the existing Director executes checks. These workflows retain the rules above and existing user decisions; upstream optional-testing defaults do not apply to bug repairs. Ask the owner about unresolved story/gameplay choices, not already authorized technical phase transitions.

- For new or updated engineering Skills, read `docs/agents/engineering-skills-v132.md` and the pinned local `SKILL.md` bytes; route only through the existing Director and keep source/catalog/runtime evidence distinct.

<!-- codex-sdlc:start -->
# codex-sdlc

Use the installed codex-sdlc plugin for complete feature delivery. Treat .sdlc/runs/<run-id>/manifest.yaml as run authority. Use node .sdlc/runtime.cjs for lifecycle operations, preserve native command failures, and do not hand-write runtime evidence.
<!-- codex-sdlc:end -->
