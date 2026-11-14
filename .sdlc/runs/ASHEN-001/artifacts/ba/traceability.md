---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 2
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
| REQ-014 | AC-022 | CLAIM-017 | draft; FACT-017; Q-002 | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |
| REQ-015 | AC-023 | CLAIM-018 | draft; FACT-018; Q-003 | pending; no journey implementation claimed | pending QC cases; planned evidence in linked ACs | not assessed; no execution in BA |

Source paths abbreviated above resolve Core files under `Assets/Scripts/Core/` and `AshenFadenwerkEditor.cs` under `Assets/Editor/`. They establish existing behavior and integration risks, not evidence that the expansion is implemented. Existing numeric EntryAct bounds (1–10), crypt depth bounds (1–3), fixed chest bonus 12, save validation and companion area/seed/depth binding require explicit extension and regression coverage. No product source was changed by BA.

Delivery coordination context from `docs/specs/immergruen-journey/owner-decisions.md` and PM discovery revision 2: the owner resumed CPU-bounded game development, retaining the shared Unity CPU cap and at most one editor for bounded checks. A real playable World Forge use and measured benefit precede a world/save backup with recovery verification and later separate tool releases. A bounded pilot specification owns detailed acceptance for that sequence; this BA revision adds no typed facts or acceptance criteria for it. Tool marketing, packaging and Asset Store submission remain outside ASHEN-001; the recorded later one-time prices are Fadenwerk USD 6 and World Forge USD 35. This note records coordination only and claims no executed pilot, measured benefit, backup or release.

## Assumptions

- ASM-001: Nordwechsel is an existing lead and candidate name, not confirmed encounter geography. Impact if false: REQ-002 display/location content needs adjustment before approval.
- ASM-002: Route/biome display labels are working labels until owner direction; no new name is canon. Impact if false: REQ-001, REQ-002, REQ-014 and REQ-015 need content review.
- ASM-003: The earlier owner direction already confirms external cave re-entry regeneration and floor/save continuity; this is now FACT-019, not an open assumption or a question to ask again. Impact of contradictory later direction: PM must revise the approved fact and REQ-006.
- ASM-004: Repository evidence describes one single-player Unity/C# game without a backend or database application in this run. Impact if false: ownership and integration scope in REQ-007 and REQ-011 need review.

## Open questions for Product Owner

Q-001 is answered by the owner and reflected in approved FACT-016. Only Q-002 and Q-003 remain open; their three options each are concrete, unselected proposals for sequential owner review, not canon or authoritative gameplay rules. PM should record Q-001 as answered, retain Q-002–Q-003 as open and record the assumption statuses in run authority; BA does not edit the manifest. The spy’s exact name, home kingdom, identity-reveal order and later meetings also remain unset and must receive the same three-proposal owner process when needed; no extra approved facts are inferred.

- Q-001 — answered (FACT-016, CLAIM-016, REQ-013): The owner selected the name and next handover location of a transport intermediary in exchange for troop strength and march plans. It begins as the spy’s unverified promise, not already received or proven information. Freight-register access and contact with a surviving transport companion were not selected. The answer does not establish who ordered the transports or what happened to the missing people. Source: `docs/specs/immergruen-journey/owner-decisions.md`, “Angebot des Spions – ausdrückliche Besitzerwahl”; `facts.yaml` revision 3.
- Q-002 (FACT-017, CLAIM-017, REQ-014): What is the main encounter on reaching Immergrün? Proposal A: investigate a disputed freight entry with a trade official. Proposal B: meet people expelled at closing time who can point to a witness. Proposal C: follow the transport trail to a receiving warehouse and confront its keeper. These proposals do not approve a culprit, battle or missing-person outcome.
- Q-003 (FACT-018, CLAIM-018, REQ-015): What story purposes should the two areas after Immergrün serve? Proposal A: follow the supply trail to its source, then bring independently corroborated evidence to a neutral contact. Proposal B: seek a missing transport witness, then investigate that witness’s account at a second location. Proposal C: investigate the foreign spy’s military patrons, then seek a separate source to test their version of events. Both area purposes, identities and outcomes remain undecided.

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
