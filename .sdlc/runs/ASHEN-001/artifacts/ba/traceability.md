---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 5
---

# Traceability matrix

| REQ ID | Acceptance criteria | Claim IDs | Contract | Implementation | Test cases | Defects |
| --- | --- | --- | --- | --- | --- | --- |
| REQ-001 | AC-001 | CLAIM-001 | draft; request.md; scope.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-002 | AC-002 | CLAIM-002, CLAIM-003 | draft; request.md; docs/specs/immergruen-journey/owner-decisions.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-003 | AC-003, AC-004 | CLAIM-004, CLAIM-005 | draft; request.md; docs/ASCHENZOLL_FREIGHT.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-004 | AC-005, AC-006 | CLAIM-006, CLAIM-007 | draft; scope.md; CryptDungeonLayout.cs; WoodlandJourney.cs | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-005 | AC-007, AC-008 | CLAIM-008 | draft; CryptDungeonLayout.cs; CryptDungeon.cs; LootCatalog.cs | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-006 | AC-009, AC-010, AC-011 | CLAIM-009, CLAIM-019 | draft; WoodlandState.cs; CryptDungeonState.cs; AschenzollFreight.cs | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-007 | AC-012, AC-013, AC-024 | CLAIM-010 | draft; CharacterProgression.cs; HungerJourney.cs; ArpgSimulation.cs; AshenFadenwerkEditor.cs | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-008 | AC-014, AC-015 | CLAIM-011 | draft; NpcForgeCompanion.cs; NpcForgeCompanionCombat.cs; ArpgSimulation.cs | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-009 | AC-016 | CLAIM-012 | draft; docs/RATTLER_IMMERGRUEN.md; request.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-010 | AC-017 | CLAIM-013 | draft; scope.md; facts.yaml | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-011 | AC-018, AC-019 | CLAIM-014 | draft; AGENTS.md; docs/SPEC_DIRECTOR_WORKFLOW.md; scope.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-012 | AC-020 | CLAIM-015 | draft; request.md; facts.yaml | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-013 | AC-021 | CLAIM-016 | draft; FACT-016 approved; Q-001 answered; docs/specs/immergruen-journey/owner-decisions.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-014 | AC-022 | CLAIM-017 | draft; FACT-017 approved; Q-002 answered; docs/specs/immergruen-journey/owner-decisions.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-015 | AC-023 | CLAIM-018 | draft; FACT-018 approved; Q-003 answered; docs/specs/immergruen-journey/owner-decisions.md | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-016 | AC-025, AC-033 | CLAIM-020, CLAIM-022 | draft; FACT-020/022; Q-004 answered; docs/specs/immergruen-journey/owner-decisions.md | pending; no new witness/profession implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-017 | AC-026, AC-027 | CLAIM-010, CLAIM-021 | draft; FACT-021; CharacterProgression.cs; LootItem.cs; scope.md | pending; no new witness/profession implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-018 | AC-028, AC-029 | CLAIM-010, CLAIM-011, CLAIM-021 | draft; FACT-021; PracticeRules.cs; CharacterProgression.cs; NpcForgeCompanion.cs | pending; no new witness/profession implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-019 | AC-030, AC-031, AC-032 | CLAIM-010, CLAIM-021 | draft; FACT-021; CharacterProgression.cs; ArpgSimulation.cs; scope.md | pending; no new witness/profession implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |

Source paths abbreviated above resolve Core files under `Assets/Scripts/Core/` and `AshenFadenwerkEditor.cs` under `Assets/Editor/`. They establish existing behavior and integration risks, not evidence that the expansion is implemented. Existing numeric EntryAct bounds (1–10), crypt depth bounds (1–3), fixed chest bonus 12, save validation and companion area/seed/depth binding require explicit extension and regression coverage. No product source was changed by BA. The cited source remains a baseline for integration, not proof of new feature delivery. `CharacterProgression` currently stores four fixed combat-practice slots; `PracticeRules` binds blade/force/guard/ruin identities, and existing `LootItem` receipts encode kill ordinals and weapon upgrades. Herbalism/alchemy therefore need distinct compatible progression/resource/recipe/effect contracts within the existing owner, without reinterpreting combat slots or treating herbs/potions as weapon-upgrade kill receipts. Recipe costs, herb placement/renewal, training rates and healing/buff lifecycle values are autonomous design to document and test; none is added as an owner-approved numeric fact.

Delivery coordination context from `docs/specs/immergruen-journey/owner-decisions.md` and PM discovery revision 5: the owner resumed CPU-bounded game development, retaining the shared Unity CPU cap and at most one editor for bounded checks. A real playable World Forge use and measured benefit precede a world/save backup with recovery verification and later separate tool releases. A bounded pilot specification owns detailed acceptance for that sequence; this BA revision adds no typed facts or acceptance criteria for it. Tool marketing, packaging and Asset Store submission remain outside ASHEN-001; the recorded later one-time prices are Fadenwerk USD 6 and World Forge USD 35. This note records coordination only and claims no executed pilot, measured benefit, backup or release. The full long journey, Immergrün encounter, both following areas and the added gathering/alchemy systems remain the acceptance scope; a first pilot cannot close ASHEN-001. PM discovery records Fadenwerk PR43 accepted and merged with native/browser references under `docs/evidence/fadenwerk-connect-20261114`; this BA revision does not rerun or manufacture that evidence. PM follow-up reports native filesystem presence at `Assets/PrivateArt/DesignAssets/AlchemyLabProps`: 120 prefabs, including `Mortar_01`, `Retort_Preset_01`, `Potion_Red_01`, `Potion_Blue_01` and books/furniture. This establishes reported presence only; native visual/material validation and usable-binding checks remain pending, and no pack integration is claimed.

## Assumptions

- ASM-001: Nordwechsel is an existing lead and candidate name, not confirmed encounter geography. Impact if false: REQ-002 display/location content needs adjustment before approval.
- ASM-002: Route/biome display labels are working labels until owner direction; no new name is canon. Impact if false: REQ-001, REQ-002, REQ-014 and REQ-015 need content review.
- ASM-003: The earlier owner direction already confirms external cave re-entry regeneration and floor/save continuity; this is now FACT-019, not an open assumption or a question to ask again. Impact of contradictory later direction: PM must revise the approved fact and REQ-006.
- ASM-004: Repository evidence describes one single-player Unity/C# game without a backend or database application in this run. Impact if false: ownership and integration scope in REQ-007 and REQ-011 need review.

## Open questions for Product Owner

Q-001–Q-004 are answered by the owner and reflected in approved FACT-016–018 and FACT-022; none of these four questions remains open. FACT-020/021 approve the witness/profession direction. PM should record all four answers and the assumption statuses in run authority; BA does not edit the manifest. The alchemist’s coercion and protection prerequisite are now canon, while witness reliability, an actual attack, commissioner identity, royal guilt and missing people’s fates are not established by those answers. The spy’s exact name, home kingdom and identity-reveal order remain unset; any new consequential lore choice follows the existing three-proposal owner process rather than being silently approved.

- Q-001 — answered (FACT-016, CLAIM-016, REQ-013): The owner selected the name and next handover location of a transport intermediary in exchange for troop strength and march plans. It begins as the spy’s unverified promise, not already received or proven information. Freight-register access and contact with a surviving transport companion were not selected. The answer does not establish who ordered the transports or what happened to the missing people. Source: `docs/specs/immergruen-journey/owner-decisions.md`, “Angebot des Spions – ausdrückliche Besitzerwahl”; decision retained in `facts.yaml` revision 6.
- Q-002 — answered (FACT-017, CLAIM-017, REQ-014): At gate closing in distant Immergrün, expelled people point to a possible transport witness. The possible witness’s reliability is unverified. This encounter occurs after the long journey, separate from the earlier foreign trading-post spy encounter. Source: `docs/specs/immergruen-journey/owner-decisions.md`, “Immergrün bei Torschluss – ausdrückliche Besitzerwahl vom 15.11.2026”; `facts.yaml` revision 6.
- Q-003 — answered (FACT-018, CLAIM-018, REQ-015): The first area after Immergrün seeks the possible transport witness; the second verifies their claims at a second transport site. Claims, actual observations and supported conclusions stay separate. The culprit and missing people’s fates are not prejudged; supply-chain/independent-contact and spy-patron alternatives were not selected. Source: `docs/specs/immergruen-journey/owner-decisions.md`, “Zwei Gebiete nach Immergrün – ausdrückliche Besitzerwahl vom 15.11.2026”; `facts.yaml` revision 6.

- Q-004 — answered (FACT-022, CLAIM-022, REQ-016): The owner selected coercion. The alchemist helps only after the group protects him from his commissioners; actual protection in the first post-Immergrün area precedes his assistance and teaching. Ordinary animal transport and knowingly supplying for money were not selected. The answer names no commissioner and proves no royal order. Source: `docs/specs/immergruen-journey/owner-decisions.md`, “Alchemist unter Zwang – ausdrückliche Besitzerwahl”; `facts.yaml` revision 6.

## Required output paths

- `artifacts/ba/user-stories.md`
- `artifacts/ba/acceptance-criteria.md`
- `artifacts/ba/business-rules.md`
- `artifacts/ba/validation-rules.md`
- `artifacts/ba/edge-cases.md`
- `artifacts/ba/traceability.md`
- `artifacts/ba/semantic-claims.yaml`

## Transition request

- BA-001 `running -> awaiting_review`; PM review required. BA does not approve or complete this task.
