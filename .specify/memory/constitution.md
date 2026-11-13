# Ashen Crypt — Spec Kit project guardrails

Version 1.0.0, adopted 2026-11-13 for the bounded Director pilot.

This is an adapter to existing authority, not another history or simulation.
Read [AGENTS.md](../../AGENTS.md), [Memory.md](../../Memory.md),
[ProjectState.md](../../ProjectState.md) and
[the workflow agreement](../../docs/SPEC_DIRECTOR_WORKFLOW.md) from the repository root.

- AreArpg.Core owns deterministic gameplay, the 100-ms clock, progression and saves.
- Unity 6000.6.8f1, established scenes, models and licensed private overlays remain intact.
- Reproduce a bug before fixing it. Original reproduction, affected regressions and real
  CoreVerification are mandatory even where upstream templates call tests optional.
- Record exact source revisions and real evidence; static, Core, Editor, build and device
  results are distinct. A skipped check or installed skill is no runtime proof.
- Spec Kit describes one bounded change. SPD indexes planning and review. The Director
  retains its six runtime actions. Memory.md remains the append-only project memory.
- Existing owner decisions and technical authorization apply across workflow phases.
  Ask the owner only for genuinely unresolved consequential story/gameplay/product choices.
- No automatic hooks, nested model runs, new services, publishing or private-asset exports.

The linked canonical rules take precedence. Update this adapter only when their meaning
changes; keep current progress in the existing game state and linked task/PR records.
