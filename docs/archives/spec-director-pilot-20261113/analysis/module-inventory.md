# Relevant module inventory

Qualitative S/U/P/E/R review, not a measured code-quality benchmark.

| Module | Responsibility | S/U/P/E/R | Consequence |
|---|---|---|---|
| official_unity | Source/resource hash inventory | pass/pass/pass/pass/pass | Reuse generic catalog |
| game_dev | Project pin and animation guidance | pass/pass/pass/pass/pass | Preserve |
| spec_workflow | New advisory JSON workflow selection | new | Behavior tests first |
| CompanionPresentation / EquipmentPresentation | Project shared clip from Core | pass/pass/partial/host-bound/pass | Both consumers need regression |
| WoodlandArtBuilder | Broad private art import, including bow | partial/pass/partial/host-bound/partial | Avoid full importer for isolated repair |
| Core companion | Canonical hit/progression/clock | unchanged | No Core change justified |

Exact code paths and native reproduction live in the Hunter assessment.
