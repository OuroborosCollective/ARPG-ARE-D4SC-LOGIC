# Spec Kit + Spec-Driven Develop + Unity Game Director

Bounded pilot requested by the owner on 2026-11-13. Existing technical authorization
applies; unresolved consequential story/gameplay choices still belong to the owner.

| Surface | Responsibility |
|---|---|
| AGENTS.md / Memory.md / ProjectState.md | Existing rules, durable findings, current game state |
| Spec Kit | One change contract or bug assessment, actual fix and verification reports |
| Spec-Driven Develop (SPD) | Dependencies, coherent delivery batch, review and progress index |
| Unity Game Director | Existing six execution actions and revision-bound evidence |

Ashen's mandatory bug reproduction and regression rules override Spec Kit's optional-testing
template. The constitution adapts existing rules. No automatic Spec Kit events/hooks,
nested model execution, gameplay authority replacement or publication is enabled.

## Sources and setup

| Source | Exact pin | Installed |
|---|---|---|
| [Spec-Driven Develop](https://github.com/zhu1090093659/spec_driven_develop) | 14f8c0f2be4421d11b8eb7474cde3ee00fb52b14 | spec-driven-develop 1.15.0, deep-discuss, review-spd |
| [GitHub Spec Kit](https://github.com/github/spec-kit) | 2ee2b1e8005905db05f5717300a469b3068b908f | CLI 1.4.1.dev0; 9 core + 3 bug skills |

Both are MIT. Spec Kit is an explicitly pinned development snapshot, not a claimed stable
release. Generated project files include .specify/LICENSE and provenance.
SPD source/license remains in the host/user skill directory. Python 3.11+ is required;
the VPS uses separate uv-managed Python 3.12 and retains its system Python.

For a fresh tools environment, clone both sources and check out those exact SHAs.
Install the CLI with `uv tool install --python 3.12 <inspected-spec-kit-checkout>`.
Copy the three SPD folders from plugins/spec-driven-develop/skills into the user's
Codex skills directory with the MIT license; compare before replacing existing copies.
Fresh Codex skills/list readback verifies discovery. An arbitrary parent directory may
not be searched inside a Git worktree.

The checkout already contains generated Spec Kit project files. Do not rerun init --force
over current reports/guardrails. Initial generation used:

```sh
specify init --here --force --non-interactive --integration codex --integration-options='--events false' --script sh --extension bug
```

After inspecting source pins, build the machine-local hash catalog:

```sh
python3 Tools/Director/spec_catalog.py --project . --spd-skills /absolute/path/to/spd/skills --output Tools/spec-workflow-catalog.json
python3 Tools/Director/route_work.py 'Jäger Bogenschuss Animation reparieren' --project .
```

Catalog creation records an inspected baseline, not a signature. Do not regenerate merely
to clear CHANGED. The private host uses these same scripts and a catalog beside its existing
catalogs. Routing reads SKILL.md/resource hashes; it executes no tools. Ordinary unrelated
tasks remain lightweight.

## Pilot and evidence boundaries

The [Hunter assessment](../.specify/bugs/hunter-bow-animation/assessment.md) applies
`$speckit-bug-assess`, SPD analysis/planning/review and
Director readback of actual native RED. Original test: zero arm movement. Shared
player/companion clip and separate road-hunter Animator gap are explicit acceptance gates.
The pilot now has a real [fix report](../.specify/bugs/hunter-bow-animation/fix.md)
and [native verification](../.specify/bugs/hunter-bow-animation/test.md).
The exact moving source take repairs both actors; native frame review then caught
the hunter's different wrist axes, leading to a bow-only palm binding. Full suites
pass129PlayMode/55EditMode with0skips, plus8112Core assertions. Initial failures and
the two neighboring fixture corrections remain in the evidence. Road-hunter Animator
and Inari model work are still separate. The original assessment remains unchanged.

One tooling batch: [installation #36](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/36),
[routing #37](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/37),
[assessment #38](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/38).
GITHUB_STANDARD detected: issues available; project-board permission absent.
Completing adoption does not close the gameplay defect.

Observed benefit: explicit original reproduction, shared-clip regression and distinct
evidence boundaries. No comparable before/after development-time baseline exists;
no percentage speedup is measured.
