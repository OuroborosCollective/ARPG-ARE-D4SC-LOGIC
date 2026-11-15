---
run_id: "ASHEN-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 5
---

# Acceptance criteria

These are planned acceptance checks, not executed test evidence. All 22 current facts are approved and Q-001–Q-004 are answered; approval of story direction does not establish witness reliability, implementation completion or passing execution evidence.

## AC-001

requirement_id: REQ-001
claim_ids: [CLAIM-001]

Given a profile has reached the current Aschenzoll endpoint
When the complete expansion is evaluated
Then a playable route reaches the distant Immergrün and continues into two distinct further areas, with the connecting areas, quests, NPCs, scenery and equipment loot accounted for; only delivering the first trading post does not satisfy the scope

evidence_expected: A route and content coverage inventory plus revision-bound traversal evidence for the complete journey and both subsequent areas; any unimplemented content is pending, never passed. A successful first World Forge pilot alone does not satisfy full journey acceptance.

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

Given the group reaches distant Immergrün after the long journey
When it encounters people being expelled at gate closing and receives their account
Then those people point to a possible transport witness, whose reliability remains unverified; receiving the lead alone does not establish the witness’s truthfulness or verify any transport claim

evidence_expected: Core and native interaction/journal checks at the Immergrün gate-closing encounter, comparing unknown, received-lead and unverified-witness states before and after contact and save/load. Route evidence locates this encounter in Immergrün rather than at the earlier trading post. Q-002 is recorded as answered; no witness reliability is inferred from owner approval of the encounter.

## AC-023

requirement_id: REQ-015
claim_ids: [CLAIM-018]

Given the Immergrün encounter has supplied the possible transport-witness lead
When the player explores the two subsequent areas in order
Then the first area supports seeking the possible witness and the second supports verifying their claims at a second transport site; reported claims, actual observations and supported conclusions remain distinct, with neither the culprit nor missing people’s fates prejudged

evidence_expected: Revision-bound traversal and native quest/interaction checks for both separate areas, plus Core evidence-state checks distinguishing the witness’s claims from observations at the second transport site. Merely reaching a location, hearing an account or reloading cannot automatically verify claims; no culprit or missing-person outcome is invented. Q-003 is recorded as answered and both areas remain mandatory for full journey coverage.

## AC-024

requirement_id: REQ-007
claim_ids: [CLAIM-010]

Given a supported journey run has catalog-backed loot, equipment and accumulated progression
When the player collects a reward, equips it, travels, reloads or attempts to repeat the same rewarded action
Then the existing progression/inventory authority retains the actual item and its effects, grants each reward once per valid causal event, and avoids loss or duplicate receipts

evidence_expected: Core/native receipt, inventory and equipment-effect checks across collect/save/travel boundaries, duplicate-action rejection and BigInteger progression regression.


## AC-025

requirement_id: REQ-016
claim_ids: [CLAIM-020, CLAIM-022]

Given the player follows the possible-witness lead in the first area after Immergrün and the group has actually protected the alchemist from his commissioners
When the player receives his assistance and hears his account
Then the witness is the local master alchemist coerced to prepare a sedative for an oversized Rattler; his account is recorded separately from observations and corroboration obtained at the second transport site, and hearing him alone neither verifies his account nor identifies the client, culprit or missing people’s fates

evidence_expected: Core and native dialogue/journal checks distinguishing unknown, personally heard account and corroborated evidence; second-site interaction checks for actual evidence acquisition. No assertion is made that the sedative was delivered or used, or that a playable sedative recipe exists.

## AC-026

requirement_id: REQ-017
claim_ids: [CLAIM-010, CLAIM-021]

Given herb gathering has been introduced with the master alchemist and a reachable world herb source is available
When the player completes an eligible harvest through the normal interaction path
Then an actual herb quantity is collected into the existing Core-owned resource inventory and the corresponding harvest state is recorded; the herbs are usable as recipe ingredients rather than merely scenery or dialogue

evidence_expected: Core and native gather-to-inventory checks at reachable sources, with before/after source and resource quantities; subsequent recipe eligibility demonstrates that collected herbs are usable ingredients. Species, quantities, placement and renewal policy are documented technical design, not invented owner-approved values.

## AC-027

requirement_id: REQ-017
claim_ids: [CLAIM-010, CLAIM-021]

Given a harvest event has already been applied or the requested source/action is invalid
When the same event is retried, replayed, reloaded or invoked without meeting its configured interaction conditions
Then the same harvest cannot grant duplicate herbs or practice, invalid actions leave resource/progression state unchanged, and save/load preserves the applicable source identity and collected inventory without creating another inventory authority

evidence_expected: Core duplicate-event and invalid-source/interaction checks, replay and native save/load tests around a completed harvest; any renewal is a distinct event under the declared resource lifecycle, not an accidental duplicate caused by reloading.

## AC-028

requirement_id: REQ-018
claim_ids: [CLAIM-010, CLAIM-011, CLAIM-021]

Given the master alchemist has introduced herbalism and alchemy as distinct classless skill branches
When the player performs successful herb gathering and successful potion brewing, then opens menus, repeats a rejected action or observes a companion’s action
Then gathering advances the player’s herbalism and brewing advances the player’s alchemy through their own effective actions; each branch retains its own progress, and menu activity, rejected/duplicate events, passive waiting or companion-only actions grant no player profession training

evidence_expected: Independent before/after practice ledgers for each successful and rejected action; checks show distinct branch identities and no cross-credit between gathering and brewing, with no new class selection or overwrite of blade/force/guard/ruin progress. Exact XP formulas and progression thresholds are documented technical choices.

## AC-029

requirement_id: REQ-018
claim_ids: [CLAIM-010, CLAIM-011, CLAIM-021]

Given existing save fixtures and profiles with distinct herbalism/alchemy progress, including values beyond fixed-width integer limits, are available
When the player saves, loads, travels and replays a previously credited profession event
Then the two branch values remain separate and persistent, existing combat practice and companion attribution retain their prior meaning, and repeating the same credited event cannot grant additional profession progress

evidence_expected: Legacy/current save migration and exact round-trip checks, deterministic replay, large BigInteger progression tests and native profile reload; migrations preserve established behavior rather than reusing one of the four existing combat-practice slots.

## AC-030

requirement_id: REQ-019
claim_ids: [CLAIM-010, CLAIM-021]

Given healing and buff potion recipes have been introduced with the master alchemist and the player has gathered their configured ingredients
When the player successfully brews each supported potion family or attempts a recipe with invalid identity, quantity, missing ingredients or unmet configured conditions
Then a successful recipe consumes exactly its configured ingredient quantities and creates its configured potion output once as one Core-owned transaction; a rejected recipe causes no partial ingredient consumption, output or practice gain

evidence_expected: Core and native gather-to-brew-to-inventory scenarios for healing and buff potions; stock-boundary, invalid-recipe and repeated-event tests compare input/output quantities and practice. Recipe catalogs, costs and unlock/interaction details must be documented and tested as technical design without inventing additional story requirements.

## AC-031

requirement_id: REQ-019
claim_ids: [CLAIM-010, CLAIM-021]

Given the player owns a crafted healing potion or buff potion and the configured use conditions are met
When the player consumes the potion through the normal input path
Then the valid use consumes the configured inventory quantity once and applies a real deterministic Core effect: healing changes actual health within its limits, and a buff changes its declared gameplay value for its declared lifecycle; a rejected use produces no consumption, effect or practice credit

evidence_expected: Native input/HUD and Core state checks for both potion families, including health limits, invalid use, empty stock, equipment interaction and the documented buff stacking/refresh/expiry behavior. Rendering or dialogue alone cannot implement effects; durations and coefficients are technical choices under the existing 100 ms tick.

## AC-032

requirement_id: REQ-019
claim_ids: [CLAIM-010, CLAIM-021]

Given a profile contains herbs, crafted potions, spent ingredients and an active potion effect
When the player saves/loads, changes area or floor, restarts after death, or retries an already committed craft/use event
Then inventory, spent resources, potion receipts and effects follow the documented lifecycle consistently under the existing Core/save owner; already committed transactions are not repeated, consumed items do not reappear, effects do not accidentally stack or restart, and existing death, equipment and companion behavior remains intact

evidence_expected: Deterministic snapshot/replay and legacy/new-save checks spanning harvest, craft and use boundaries, active-effect reload/travel and explicit death/restart behavior, plus native save restoration. The technical contract specifies effect duration, stacking and death handling; no particular coefficient or death-expiry policy is implied by this requirement.

## AC-033

requirement_id: REQ-016
claim_ids: [CLAIM-022]

Given the group has encountered the coerced alchemist but has not yet protected him from his commissioners
When the player requests assistance or teaching, accepts or attempts the protection task, and eventually completes the implemented in-world protection objective
Then only actual completed protection makes his assistance and the introduction of his alchemy teaching eligible; dialogue, task acceptance, an incomplete attempt, elapsed presentation time or save/load alone cannot satisfy the prerequisite, and repeated completion cannot duplicate progression or rewards

evidence_expected: Core tests for unprotected, accepted, attempted and protected states; native interaction and gameplay tests exercise the actual protection-success path before help/teaching, then verify its save/load and repeated-action behavior. The technical assignment must describe observable protection-success conditions without inventing the commissioners’ identities. Protection does not itself corroborate the witness’s account or prove royal guilt.
