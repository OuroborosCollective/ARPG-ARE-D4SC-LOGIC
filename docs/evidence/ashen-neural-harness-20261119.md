# Ashen Neural Director – harness-aware MCP evidence (2026-11-19 UTC)

## Scope and distinct identities

- Repository: `OuroborosCollective/ARPG-ARE-D4SC-LOGIC`.
- Source feature branch: `feat/director-harness-aware-evals-20261119`.
- Exact GitHub/isolated-container source SHA: `13faae919bd570dcec33d64a8f0e092664884c30`. This is the tested source before adding this evidence note; documentation commits have different identities.
- GitHub main base: `488567fddcc48b04bb8c39871da4acc05d7316bf`; latest check before this report.
- Existing, separately configured Unity Director workspace: `/var/lib/unityarpg/workspaces/are-arpg-village-20261116`, SHA `860128f7cae256aefed8b874e2ab01bc01b6b3b6`. **This is not** the PR SHA.
- Unity pin (source and workspace): `6000.6.8f1`.

## Evidence and executed checks

| Boundary | Actual observation | What this does NOT prove |
| --- | --- | --- |
| GitHub native CI on source SHA `13faae919bd570dcec33d64a8f0e092664884c30` | [Core deterministic verification run 44427342825](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44427342825): success | No licensed Unity run: [run 44427342761](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44427342761) skipped |
| VPS isolated GitHub clone at same source SHA | Python Director unittests: **59/59** pass. Node Neural Hub unittests: **31/31** pass. Clean git tree before runtime canary | No game Core or Unity execution occurred in this VPS unittest command |
| Docker build from that checkout | `ashen-neural-hub:harness-13faae919bd5`, exact immutable image ID `sha256:095d6a88fd3995d160478d31c20ebd091b6fd5b69de4b5b377b9473c0178c747`; verified image includes `HARNESS_GUIDANCE` | Not a deployed or externally accessible MCP service |
| Isolated localhost MCP protocol canary with `--network none --read-only --cap-drop ALL --security-opt no-new-privileges` | `initialize` with guidance; `tools/list` **32**; `prompts/list` **28**; `director_snapshot`, `director_sources`, `director_source_discover`, `director_consult`; **each of the 28 `advice_*` endpoints executed over actual JSON-RPC**; finished PASS | No ChatGPT/Codex harness, production authentication, Unity Editor or Android play proof |
| Issue input | 17 open GitHub Issues searched through authenticated GitHub connector, normalized to number/title/state/canonical URL; JSON SHA-256 `5e365d7b9e0e79ba3dd47dbad780503fa36b5ce75d2c1b420c0de7f1a3a92a34` | MCP advisor itself receives caller-supplied issues and explicitly labels them **not independently verified** |
| Canary actual observed result | 28 advisor calls: **22 PROPOSE**, **6 ABSTAIN**. Council `THREE_PATHS_READY_FOR_DIRECTOR_REVIEW`. Private VPS canary output SHA-256 `722e747a4f97a8b6b2e5bc1c0ef107f80aeef352f3b1d883515dfebc399ef69e` | Proposals are deterministic Issue-catalog review pointers, **not** 28 generated model designs |
| Canonical Director readback | `project_status` executed as the configured workspace owner `unityarpg`: `ok:true`, source SHA `860128f7...`, clean tracked state, no untracked game inputs, `editor_process_running:false`, Unity pipeline reports 0 instances | It does not prove production MCP-to-Unity linkage or current Unity play |
| Initial failed Director probe as SSH root | `COMMAND_FAILED:git:128`: Git refused the different file owner as dubious; corrected **only process user** to `unityarpg`, and the identical Director CLI succeeded | No Git repository recreation or relaxed Git security configured |
| Initial failed runtime canary | `EACCES` opening temporary issue JSON from nonroot container; corrected **only read permission of this public temporary issue file**, then reran same source+image; success | No game data altered |

## Proposed review priorities grounded in current issues

The actual `director_consult` selected exactly three architecture tracks:

1. **Game design:** [#60](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/60), owner-chosen connected border-region structure. Do not revert to open world or a second story authority.
2. **Story/Lore:** [#77](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/77), one new chapter/region after existing story; three owner-facing lore/encounter choices must precede content authoring.
3. **Dungeon/World:** [#71](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/71), old storage and crypt as a depth region; preserve seal-break/inspection, Rattler outcome and old-save migration.

Other notable advisors: asset pipeline [#62](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/62), WorldForge [#66](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/66), village hub [#69](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/69), fast tests [#64](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/64), and graphics/Android quality [#48](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/48).

**Dependency-driven implementable order**, checked as a DAG in Wolfram Language: #62 → #66 → #69 → #71 → #77. Closed prerequisites #65, #67, #68, #74 need independent regression evidence before relying on their completion for current code; their GitHub status alone is not a test.

## Boundaries, next safe integration

- Product-state difference: The actual Unity-workspace SHA `860128f7...` is **not** the same as the isolated MCP canary or the public GitHub base. A running plugin must require explicit verified revision matching; do not equate them or overwrite Unity workspaces to make them match.
- The existing Docker image tag `ashen-neural-hub:review-afa3d6572577` predates this run. The newly tested image is intentionally an isolated test image, **not** a replacement for any production container. No always-on Neural Hub process was present when inspected.
- A full working service needs a **single** authenticated read-only broker path through existing Sovott/Director authority, security review, strict snapshot and issue-source identity, owner-approved actions, and independent live readback. The current advisor returns `execution:NONE`, `runtimeVerified:false`, `sourceRevisionVerified:false`.
- Model-level standalone L2 and Codex/ChatGPT harness L3 evaluations are **not** yet run. Offline trace grading must not be represented as an actual model/host evaluation. No OpenAI paid model calls, remote promotion, or Unity scene modifications were made.
- Continue in this Draft PR with authorized broker/Unity revision bridge and a separate actual Unity Editor/Android verification lane, without adding a second authority.

## File classification

- Production tooling: `Tools/Director/neural_hub/server.mjs` (MCP instructions only; no Unity effects).
- Test/evidence: `mcp_canary.mjs`, `harness_eval*.mjs`, `harness_eval_cases.json`, this note.
- Documents: `docs/DIRECTOR_NEURAL_CORTEX.md`, `Memory.md`, handoff (no source execution).
- Core/persistence/Unity projection/game effects: untouched by the harness feature.
