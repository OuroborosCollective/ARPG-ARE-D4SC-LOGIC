---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 1
---

# Edge cases

| Edge ID | Related REQ | Scenario | Expected behavior | Risk | Claim IDs |
| --- | --- | --- | --- | --- | --- |
| EDGE-001 | REQ-001 | The first trading-post increment works but Immergrün or either later area is absent. | Report an incomplete increment and preserve the remaining scope. | Premature whole-feature completion. | CLAIM-001 |
| EDGE-002 | REQ-002 | An existing Nordwechsel reference is mistaken for approval of the encounter’s exact name/location or reveal order. | Preserve the geographical/spy facts and keep unselected story details pending owner direction. | Working labels silently become canon. | CLAIM-002, CLAIM-003 |
| EDGE-003 | REQ-003 | The player has only one military fact, reads without obtaining an artifact, leaves without reporting, or discloses to someone else. | Preserve separate acquisition/possession/disclosure state; only actual supported information is conveyed to the contacted recipient. | False evidence, telepathic knowledge or unapproved war consequences. | CLAIM-004, CLAIM-005 |
| EDGE-004 | REQ-004 | A 3- or 6-floor run produces a blocked exit, invalid spawn, unreachable treasure or no side-cave entry in a retained connector. | Treat coverage/reachability as failing until corrected; keep the fixed story nest separate. | Softlock or incomplete route coverage. | CLAIM-006, CLAIM-007 |
| EDGE-005 | REQ-005 | A high-variance random drop appears weaker on a deeper floor, or all chests still give the same fragments. | Assess the declared floor-value measure and actual obtainable loot; require a measurable increasing value contract, not labels alone. | False progression claim or arbitrary every-drop superiority rule. | CLAIM-008 |
| EDGE-006 | REQ-006 | Reload occurs mid-run, the player moves back one floor, or a repeat Rattler run follows an already-reported brand. | Keep the current run for reload/floor change and preserve permanent facts during renewed combat. | Reroll exploits, duplicated story rewards or erased choices. | CLAIM-009, CLAIM-019 |
| EDGE-007 | REQ-007 | A corrupted save recomputes its checksum, a duplicate reward is retried, or a large BigInteger value exceeds machine integers. | Validate semantic invariants as well as integrity, retain valid state and preserve the existing unbounded progression semantics. | Save loss, item duplication, overflow or divergent replays. | CLAIM-010 |
| EDGE-008 | REQ-008 | The hunter dies in a new cave, waits elsewhere, or kills without player contribution. | Persist death/waiting and correct area/seed/floor association; give only causal hunter progression for hunter-only kills. | Unwanted revival, catch-up levelling or cross-area participation. | CLAIM-011 |
| EDGE-009 | REQ-009 | A spy cites the royal brand as sufficient proof against the king. | Keep the assertion attributed and the personal royal-order claim unproved. | Unsupported canonical culprit. | CLAIM-012 |
| EDGE-010 | REQ-010 | A crowded cave or long objective obscures existing controls at tablet dimensions. | Resolve layout/readability within the preserved HUD/menu/camera contract and review the native result. | Unusable touch interaction despite correct Core state. | CLAIM-013 |
| EDGE-011 | REQ-011 | Core checks pass but native Unity, Android build or physical tablet checks have not run. | Report only the executed result for its pinned revision; keep absent platform evidence explicitly pending. | Fabricated acceptance or concealed prerequisites. | CLAIM-014 |
| EDGE-012 | REQ-012 | The owner has not answered a proposal while routine generation or save work can proceed. | Keep the lore decision unresolved and continue authorized independent technical work. | Invented approval or avoidable technical stoppage. | CLAIM-015 |
| EDGE-013 | REQ-013 | A developer needs the spy’s promised revelation to finish a quest. | Refer to Q-001 and leave dependent revelation content pending. | Invented transport culprit, motive or missing-person fate. | CLAIM-016 |
| EDGE-014 | REQ-014 | Route geometry can reach Immergrün before its main story encounter is chosen. | Keep geometry planning distinct from unresolved encounter acceptance under Q-002. | A location shell is mistaken for full story delivery. | CLAIM-017 |
| EDGE-015 | REQ-015 | One or both post-Immergrün areas have provisional biome names but no approved purpose. | Retain both areas in scope and keep story purposes unresolved under Q-003. | Working labels imply approved later canon or reduced scope. | CLAIM-018 |
