# Ashen Crypt Director: engineering-skill gates v1.3.2

**Scope:** Read-only workflow routing and revision-bound verification metadata. No Unity
scene, C# Core, tick, RNG, NPC economy, Save, Quest, Editor bridge or Director runner changed.

## Source boundary

- Upstream: https://github.com/mattpocock/skills/releases/tag/v1.3.2
- Exact source commit: `d1478fcb140db0b93c0c8df17bcfd311844749d0`.
- MIT license by Matt Pocock (2026); preserve the copyright/license notice if installing.
- Files: `ask-matt`, `code-review`, `diagnosing-bugs`, `tdd`, `implement-spec`.
- `chief-of-staff` is under upstream `skills/in-progress/`: **reference only**;
  only the wiki's dated-claim principles are adapted, without background agents.
- Full source-byte SHA256 values are pinned inside
  `Tools/Director/engineering_skill_gates.py`, not claimed from a floating tag.
- No external source script is executed, no auto-update from skills.sh, no second MCP.

## Existing authority boundaries

1. `AGENTS.md`, `Memory.md`, Spec Kit's change contract and SPD's delivery
   planning keep their original precedence. `implement-spec` adds reviewable DAG/worktree
   **guidance**, but does not replace Spec Kit or SPD.
2. `Tools/Director/route_work.py` includes a read-only `engineering_skills`
   projection. If the machine-local catalog is missing, invalid or hashes differ,
   advice is **NOT_VERIFIED / NEEDS_REVIEW**, not skill execution.
3. The existing VPS Unity Game Director alone retains
   `project_status`, `inspect_change`, `run_tests`, `build_android`,
   `inspect_build`, `create_release_candidate`. No second Runner.
4. The deterministic C# Core remains at 100 ms / 10 Hz. NPC economy cycles are
   valid domain feedback; engineering delivery DAG cycles are rejected.
5. Review carries two independent reports: documented repository standards and
   the originating spec. Each must cite the same full code SHA, nonempty sources,
   independent report SHA256 and executed state PASS. The metadata gate does
   not itself establish the substantive correctness of those reports.
6. Mutation RED requires a byte-different pristine/mutated artifact and
   observed nonzero RED/zero GREEN exit codes. Metadata validation is explicitly
   **not** a native Unity or production truth claim.
7. The task graph requires OwnerApproval before DirectorApply and EvidenceReadback
   before MemoryEntry/Merge. Existing approval is reusable within its scope.
   The physical Samsung Tab A9 device test is optional for issue completion;
   it does not replace separate APK/build gates.
8. `GLOSSARY.md` contains stable vocabulary, `docs/agents/wiki/index.md`
   historical source-bound claims, and `Memory.md` remains the one per-round
   durable integration record. The wiki does not assert current runtime health.

## Install and operate in a trusted local Codex host

Do not overwrite an existing skill directory. Inspect upstream skills and their
MIT license first. The five non-experimental skill directories may be installed
only in an isolated host-owned skills root, with pinned bytes checked independently.
Keep `implement-spec` advisory until its broader workflow dependencies are present;
Ashen's existing SPD remains delivery authority.

```bash
python3 Tools/Director/engineering_skill_gates.py --skills-root ~/.codex/skills --output Tools/engineering-skill-catalog.json
python3 Tools/Director/route_work.py "NPC bug reparieren" --project .
python3 -m unittest discover -s Tools/Director -v
```

The Director checks the project-local catalog first, then the per-user
`~/.local/share/unity-game-director/engineering-skill-catalog.json` fallback.
The local catalog contains absolute host-specific paths: it must **not** be committed
to GitHub or copied into public Plugin packages. It proves only which checked
Skill bytes are present, not that a separate ChatGPT Plugin is updated.
Installed ChatGPT EngineeringSuite and Plugin Autopilot require their own
explicit versioned update and readback.

## Formal regression gates

`validate_graph(DEFAULT_EDGES)` checks a 14-node/15-edge acyclic task model,
approval dominance, EvidenceReadback dominance and nonblocking OptionalDevice.
`check_authorities` rejects competing canonical owners. Pass an explicitly
tagged `graph_kind="economy"` to allow economic feedback cycles.
Wolfram Language independently checked the equivalent conceptual graph; its
mathematical result is not a Unity test.

Use exact-source Python unit tests, CoreVerification and relevant Unity checks
for production effects. No green test is allowed to stand in for missed Core,
Editor, device, CI or deployed-runtime evidence.
