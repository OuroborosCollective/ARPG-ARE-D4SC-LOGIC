# Bounded plan

One phase, three tasks, one coherent PR batch spec-director; integration branch
feature/ashen-spec-director-20261113. Tier0 implementation: context held and machine-verifiable.
Explicit SPD analysis/review instructions authorize focused read-only reviewers.

| Task | Issue | Priority / effort | Dependencies | Drivers | Acceptance |
|---|---|---|---|---|---|
| Install | #36 | P1 / M | baseline | S/E/R | pinned CLI, 15 skills/resources, discovery, no events |
| Route | #37 | P1 / M | install | S/U/P/E/R | RED/GREEN, missing/changed, no writes, host readback, CI |
| Assess | #38 | P1 / M | install; route final readback | S/P/R | native XML hash, shared clip/road gap, no false fix |

One editing lane and one rollback/review boundary. Tests: Director, 40 Preflight/static,
real Core CI, host hash/readback, diff excludes Unity gameplay/assets.
Governance impact: workflow AGENTS link and one final append-only Memory entry.
Gameplay repair remains separate. No new product decision is needed.
