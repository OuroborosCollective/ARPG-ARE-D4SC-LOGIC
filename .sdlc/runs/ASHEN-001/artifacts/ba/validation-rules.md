---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 2
---

# Validation rules

| Validation ID | Related REQ | Input | Valid behavior | Invalid behavior | Claim IDs |
| --- | --- | --- | --- | --- | --- |
| VAL-001 | REQ-001 | Route/content inventory | Accounts for a distant Immergrün and two further playable areas. | First-increment-only coverage cannot be marked complete. | CLAIM-001 |
| VAL-002 | REQ-002 | First encounter location and character contract | Matches next out-of-realm post/village and true foreign-spy identity. | Direct first encounter in Immergrün or an invented approved kingdom/name is rejected in review. | CLAIM-002, CLAIM-003 |
| VAL-003 | REQ-003 | Military information state and disclosure action | Both requested information types are causally obtained before deliberate personal disclosure; the attack remains possible. | Unknown information, automatic global knowledge or unapproved attack consequences cannot advance the approved evidence state. | CLAIM-004, CLAIM-005 |
| VAL-004 | REQ-004 | Connecting-area inventory, cave descriptor and floor index | Every connection maps to one or more optional cave entries; count is 3–6 inclusive and the requested floor exists and is reachable. | Missing cave, count outside 3–6, invalid floor or impassable required route is a coverage/validation failure. | CLAIM-006, CLAIM-007 |
| VAL-005 | REQ-005 | Adjacent-floor difficulty and reward data | The declared technical measures show increasing challenge and loot value with actual collectible rewards. | Floor labels, unchanged fragment value or unreachable treasure alone cannot establish progression. | CLAIM-008 |
| VAL-006 | REQ-006 | External re-entry, floor-change and save/load events | Re-entry generates a new run; floor/load events retain one; permanent world facts and earned progress persist. | Treating a floor change or reload as re-entry, resetting choices or replaying one-time story rewards is invalid. | CLAIM-009, CLAIM-019 |
| VAL-007 | REQ-007 | Legacy/current saves, deterministic inputs and reward receipts | Valid saves preserve semantics and deterministic replay; one existing inventory/progression owner validates rewards. | Malformed/impossible state, duplicate receipt or a second authoritative quest/reward clock cannot mutate valid saved progress. | CLAIM-010 |
| VAL-008 | REQ-008 | Hunter availability/status and causal action ownership | Absent, following, waiting and dead states remain meaningful and XP/training follows the actual actor. | Auto-recruitment, resurrection, waiting XP or player XP from hunter-only kills is invalid. | CLAIM-011 |
| VAL-009 | REQ-009 | Evidence wording and proof state | Brand yields an Immergrün link with personal royal-order guilt unproved. | Brand or spy assertion alone cannot mark the king’s personal command proved. | CLAIM-012 |
| VAL-010 | REQ-010 | Interface and asset changes | Retain Fable interface/camera behavior and use available licensed assets with readable projected objectives. | Unauthorized redesign or asset substitution cannot be labelled compliance with preservation. | CLAIM-013 |
| VAL-011 | REQ-011 | Prerequisite and platform evidence | Fadenwerk acceptance precedes expansion mutation; each test/build claim cites its actual revision and platform. | Missing native evidence, inherited historical APK evidence or fabricated device claims fail review. | CLAIM-014 |
| VAL-012 | REQ-012 | Material lore proposal and owner response | Three concrete proposals are presented; an explicit response determines the canonical choice. | Silence, a technical go-ahead or BA prose cannot approve a lore option. | CLAIM-015 |
| VAL-013 | REQ-013 | Spy offer terms and knowledge/evidence state under FACT-016 | Offer both the intermediary’s name and next handover location for troop strength and march plans; record the initial state as an unverified promise and Q-001 as answered. | A missing promised information type, substituted register/witness offer, automatic receipt/verification or inferred transport culprit/missing-person fate is invalid. | CLAIM-016 |
| VAL-014 | REQ-014 | FACT-017 / Q-002 | Keep equals/null/unresolved while preserving the Immergrün destination requirement. | Do not substitute a guessed encounter or remove the destination. | CLAIM-017 |
| VAL-015 | REQ-015 | FACT-018 / Q-003 | Keep equals/null/unresolved while preserving two later areas. | Do not substitute guessed story purposes or silently defer either area out of scope. | CLAIM-018 |
