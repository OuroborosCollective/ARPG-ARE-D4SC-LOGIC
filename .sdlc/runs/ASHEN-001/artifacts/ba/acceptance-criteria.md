---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 2
---

# Acceptance criteria

These are planned acceptance checks, not executed test evidence. Requirements depending on unresolved owner decisions cannot pass on the strength of the guard checks alone.

## AC-001

requirement_id: REQ-001
claim_ids: [CLAIM-001]

Given a profile has reached the current Aschenzoll endpoint
When the complete expansion is evaluated
Then a playable route reaches the distant Immergrün and continues into two distinct further areas, with the connecting areas, quests, NPCs, scenery and equipment loot accounted for; only delivering the first trading post does not satisfy the scope

evidence_expected: A route and content coverage inventory plus revision-bound traversal evidence for the complete journey; unresolved story content is identified as pending, never passed.

## AC-002

requirement_id: REQ-002
claim_ids: [CLAIM-002, CLAIM-003]

Given the group follows the onward route before reaching Immergrün
When the first freight-inspector encounter occurs
Then it occurs at the next trading post or village outside the kingdom and its canon identifies the apparent inspector as a spy of another kingdom; the exact name, home kingdom and reveal sequence are not silently selected

evidence_expected: Narrative contract review against request.md and native interaction/travel checks for the chosen encounter location once its content is approved.

## AC-003

requirement_id: REQ-003
claim_ids: [CLAIM-004, CLAIM-005]

Given the spy offers investigative help in return for information about the external Immergrüner field camp
When the player receives, investigates and potentially reports the request
Then the requested information is troop strength and march plans; actual observation or acquisition precedes knowledge or possession as applicable, and personal disclosure is a separate deliberate action; other NPCs do not learn it automatically

evidence_expected: Core state-transition tests for unknown, observed/acquired and personally disclosed states, including attempts before acquisition and without direct contact; native interaction/journal evidence. The exact acquisition scenario depends on approved narrative detail.

## AC-004

requirement_id: REQ-003
claim_ids: [CLAIM-004, CLAIM-005]

Given the military information is requested or disclosed
When dialogue and journal consequences are reviewed
Then they communicate that the information could enable an attack without asserting that an attack happens or choosing its targets or outcome; supply records or a wounded register do not substitute for the chosen request

evidence_expected: Dialogue/state review and regression checks distinguishing risk, allegation, disclosure and a confirmed event.

## AC-005

requirement_id: REQ-004
claim_ids: [CLAIM-006, CLAIM-007]

Given the route inventory lists every connecting area in the requested journey, including retained connecting areas
When cave coverage and each supported cave run are inspected
Then each connecting area has access to at least one optional procedural cave and every cave has an integer floor count from 3 through 6 inclusive; fixed bear-family story space is not counted as a resetting combat cave

evidence_expected: Inventory-to-entry mapping, boundary tests at 3 and 6, rejection tests at 2 and 7, and native entry/exit checks.

## AC-006

requirement_id: REQ-004
claim_ids: [CLAIM-006, CLAIM-007]

Given a supported cave layout is generated at any valid floor count
When the player enters, traverses floors, reaches available treasure and retreats
Then entry and return routes, floor transitions and reachable content are usable without spawning inside solid geometry or placing mandatory progression behind unreachable geometry

evidence_expected: Deterministic reachability and spawn-clearance checks over a declared seed corpus and all supported floor counts, plus native traversal and interaction checks at both bounds.

## AC-007

requirement_id: REQ-005
claim_ids: [CLAIM-008]

Given the same cave’s successive floors are compared under comparable combat conditions
When the player descends
Then the deeper-floor enemy configuration provides increasing challenge; simply relabelling a floor does not satisfy the rule

evidence_expected: Documented difficulty measure and deterministic adjacent-floor comparisons, plus native combat checks; exact coefficients remain autonomous balancing work.

## AC-008

requirement_id: REQ-005
claim_ids: [CLAIM-008]

Given the same cave’s successive floors are compared using the implemented loot-value measure
When loot and treasure are generated and collected
Then deeper floors offer more valuable rewards under that measure, using actual obtainable loot; the existing depth-independent chest fragments alone do not establish this requirement

evidence_expected: Documented value measure, adjacent-floor loot-table/receipt comparisons, item collection and equipment-effect checks, and native loot interaction. Random rarity variance is evaluated against the declared value measure, not an invented guarantee that every individual drop is superior.

## AC-009

requirement_id: REQ-006
claim_ids: [CLAIM-009]

Given a save has resolved mill, brand, bear-family or Aschenzoll choices
When the player travels, saves, reloads and repeats combat
Then fixed story sites and completed choices remain, including both original-collected and arrival-inspected Aschenzoll branches and their personal reports; the bear-family nest never becomes a respawning source of cubs or pelts

evidence_expected: Regression matrix over both freight branches, bear-family outcomes, mill state, brand discovery/report and their saved histories, using existing Core and native travel/save paths.

## AC-010

requirement_id: REQ-006
claim_ids: [CLAIM-009, CLAIM-019]

Given a cave run contains explored floors, defeated enemies and loot state
When the player either changes floor, saves and loads, or leaves the cave and re-enters from outside
Then floor transitions and save/load retain that run; external re-entry starts a new seeded layout with renewed enemies and loot while permanent story and already-earned progress remain

evidence_expected: Tests comparing seed/run identity, floor states, reward receipts and permanent facts across each boundary, plus native exit/re-entry and reload checks.

## AC-011

requirement_id: REQ-006
claim_ids: [CLAIM-009]

Given the Rattler has been defeated and its brand or report already recorded
When the player starts another valid combat run and defeats it again
Then the Rattler remains farmable while its already-discovered brand and completed personal report stay completed; repeatable combat rewards do not duplicate once-only story outcomes

evidence_expected: Repeated-run Core and native scenarios comparing combat reward receipts and permanent brand/report flags before and after each run.

## AC-012

requirement_id: REQ-007
claim_ids: [CLAIM-010]

Given two copies start from identical valid state, seed and player inputs
When both advance the same number of 100 ms Core ticks across travel and combat
Then authoritative results and serialized state agree; rendering, Fadenwerk text and World Forge authoring do not independently award loot, advance quest state or create another simulation clock

evidence_expected: Deterministic replay/state-hash and adapter-boundary tests; native frame/render checks showing Core ownership is preserved.

## AC-013

requirement_id: REQ-007
claim_ids: [CLAIM-010]

Given historical supported AC1–AC19 save fixtures and new journey/cave saves are available
When they are loaded, migrated when explicitly required and saved again, including attempts with corrupted or impossible state
Then valid existing progress and story history remain readable with historical semantics, unused legacy state remains unchanged where currently promised, and malformed input cannot partially replace valid progression

evidence_expected: Legacy fixture regression, exact round trips, new format validation and native save/restore checks using isolated profiles; no invented migration success.

## AC-014

requirement_id: REQ-008
claim_ids: [CLAIM-011]

Given the hunter is absent, following, waiting or permanently dead
When the player traverses the journey, enters caves and saves/reloads
Then the journey remains playable without requiring the hunter; a dead hunter stays dead and a waiting hunter is not automatically levelled or silently recruited

evidence_expected: Core/native companion-state matrix covering area, seed and floor transitions plus restart/load.

## AC-015

requirement_id: REQ-008
claim_ids: [CLAIM-011]

Given a hunter acts alone or the player performs their own effective actions
When kills and training are resolved
Then hunter-only kills grant only the hunter’s causal progression and do not grant player kill XP or player training; player training follows the player’s own actions and a paused hunter gains none

evidence_expected: Before/after XP, practice, kill-counter and loot-receipt comparisons, including large BigInteger values and save/replay duplication attempts.

## AC-016

requirement_id: REQ-009
claim_ids: [CLAIM-012]

Given the player has examined the royal brand or hears a spy’s interpretation of it
When the investigation journal and dialogue report its meaning
Then they link the Rattler to Immergrün but do not present a personal royal order as proved solely by the brand or by the spy’s assertion

evidence_expected: Branch-sensitive dialogue/journal review and knowledge-state regression; no new culprit, motive or missing-person outcome is inferred.

## AC-017

requirement_id: REQ-010
claim_ids: [CLAIM-013]

Given the player uses the existing interface in a new outdoor area, story site or cave
When travel, combat, investigation and loot interactions are exercised
Then the Fable HUD, menu behavior and camera are retained with licensed original assets; visible controls and objectives remain usable and basic tablet readability is reviewed

evidence_expected: Native screenshots and interaction checks with the existing interface, asset provenance review and separate physical-device evidence when actually available.

## AC-018

requirement_id: REQ-011
claim_ids: [CLAIM-014]

Given Fadenwerk acceptance is still in progress
When journey implementation is about to begin
Then the Fadenwerk connection and browser operation are accepted first; a BA document or Core-only run does not stand in for that prerequisite

evidence_expected: Parent delivery record and actual Fadenwerk native/browser acceptance references before product mutation for this expansion.

## AC-019

requirement_id: REQ-011
claim_ids: [CLAIM-014]

Given playable increments and final delivery are reviewed
When implementation, balancing and platform claims are reported
Then playable expansion coverage precedes final balancing, correctness checks apply to every increment, native Unity tests are required and Android build/device claims are explicitly tied to their own executed evidence

evidence_expected: Revision-bound Core/preflight/Director results and real Unity Edit/PlayMode results; Android IL2CPP/ARM64 build and physical tablet acceptance are recorded separately and remain unverified where not executed.

## AC-020

requirement_id: REQ-012
claim_ids: [CLAIM-015]

Given a material lore decision is unresolved
When PM asks the owner to choose the next necessary story direction
Then the owner receives three concrete proposals for that decision; no silence or technical approval selects one, and routine technical work proceeds within the authorized scope

evidence_expected: Recorded owner decision or still-open question; proposal count and approved/proposed status review.

## AC-021

requirement_id: REQ-013
claim_ids: [CLAIM-016]

Given the spy offers investigative help in exchange for troop strength and march plans
When the player hears the offer or later reviews its recorded terms
Then the promised information is both the name and the next handover location of a transport intermediary, initially recorded as an unverified promise; hearing that promise alone neither supplies the actual information nor verifies it, identifies who ordered the transports or establishes the missing people’s fate

evidence_expected: Core and native dialogue/journal checks for both promised information types and both exchange terms; compare knowledge/evidence state before the offer, after hearing it, and after save/load to show that a promise alone does not become received or verified information. Q-001 is recorded as answered; no fulfillment or corroboration is claimed without its own causal event.

## AC-022

requirement_id: REQ-014
claim_ids: [CLAIM-017]

Given FACT-017 has value null and status unresolved
When the main Immergrün encounter is needed
Then Q-002 remains open; the route still includes Immergrün, but no main encounter is invented or declared delivered

evidence_expected: PM reconciliation of CLAIM-017, Q-002 and full-route coverage; owner-approved encounter acceptance must be added after the decision.

## AC-023

requirement_id: REQ-015
claim_ids: [CLAIM-018]

Given FACT-018 has value null and status unresolved
When story content for the two post-Immergrün areas is needed
Then Q-003 remains open for both areas; both stay in scope and no guessed story purpose is treated as approved or complete

evidence_expected: PM reconciliation of CLAIM-018, Q-003 and both later areas; dependent implementation stays pending while independent technical planning may continue.

## AC-024

requirement_id: REQ-007
claim_ids: [CLAIM-010]

Given a supported journey run has catalog-backed loot, equipment and accumulated progression
When the player collects a reward, equips it, travels, reloads or attempts to repeat the same rewarded action
Then the existing progression/inventory authority retains the actual item and its effects, grants each reward once per valid causal event, and avoids loss or duplicate receipts

evidence_expected: Core/native receipt, inventory and equipment-effect checks across collect/save/travel boundaries, duplicate-action rejection and BigInteger progression regression.

