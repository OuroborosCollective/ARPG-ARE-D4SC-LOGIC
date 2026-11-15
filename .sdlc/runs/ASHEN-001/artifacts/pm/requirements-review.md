---
run_id: ASHEN-001
task_id: PM-002
producer: pm
status: approved
revision: 1
---

# Requirements review

## Coverage

Reviewed all seven BA artifacts revision 5 against facts revision 6. REQ-001–019 and AC-001–033 are accepted as the delivery contract. All 22 fact/claim pairs match subject, relation, value and status exactly, with one claim per fact; all requirement/acceptance links resolve. The four owner questions are answered in owner-decisions.md. This review approves requirements, not implementation or platform success.

- REQ-001–006 / AC-001–011: full distant journey, two subsequent areas, spy geography, military-information risk, optional 3–6-floor caves, depth progression, permanent story and regenerating runs.
- REQ-007–012 / AC-012–020,024: one Core authority, existing saves and inventory, causal optional hunter, royal-brand evidence limits, preserved HUD/camera/assets, Fadenwerk prerequisite and distinct platform evidence.
- REQ-013–016 / AC-021–025,033: unverified intermediary promise; Immergrün gate closing and expelled people; witness search then independent verification; coerced master alchemist needs actual protection before help/teaching.
- REQ-017–019 / AC-026–032: actual harvestable herbs, separate learning-by-doing branches, atomic recipes, real potion effects and saved progression/effect lifecycle. Numeric coefficients and concrete protection-success mechanics are implementation decisions, not fabricated owner facts.

## Scope decision

Full journey plus both areas and professions remains mandatory. First playable World Forge use is a vertical checkpoint, not completion of ASHEN-001. Existing working location labels may remain descriptive. Do not silently assign a kingdom, transport culprit, royal guilt or fate of missing people. The earlier outside-kingdom spy and later master alchemist are distinct people and encounters. The spy's military intelligence remains deliberately disclosed, never telepathically shared.

Q-001–004 owner references: docs/specs/immergruen-journey/owner-decisions.md. Existing assumptions ASM-003/004 are confirmed; ASM-001/002 are working-label constraints, not blockers to independent technical work. New consequential choices still require three owner proposals. Technical decisions need no renewed permission.

## Existing API / persistence review

This is the existing Unity/C# application, no backend/API service. Existing Core APIs are the approved integration boundary: ArenaSimulation inputs/travel, CharacterProgression/AC1–19, causal combat/companion rewards and catalog equipment. Authoring creates immutable configuration. World Forge's ForgedWorldMap is integer-only and hash-bound but not yet used by the game. The implementation must explicitly bind map versions and preserve legacy geometry; missing/mismatched configuration cannot silently use the latest map. Editor generation is distinct from the runtime layout/run generator.

Inspect and cover current EntryAct 1–10 assumptions, three-floor story crypt, companion area/seed/depth binding, four combat practice slots, and weapon-only loot receipts before extension. Herbs/potions require compatible Core-owned resource transactions; do not reinterpret old equipment or combat XP. Game rules remain 100 ms. Native terrain/rendered feet must use the same canonical surface.

## Prerequisite and evidence

Fadenwerk PR43 is merged at abc9eb01e8e654eb73e7656b9d5c00ff14a51190. Review source 49b83578e52ec70f2d4b674facf440b4209ddbf7 has Core CI43660401550/Job132648223613 success. Retained native proof:79/79 Edit and3/3 targeted Play, zero skips, plus real protected browser roundtrip. The full130 Play run was cancelled and is not claimed passed. See docs/evidence/fadenwerk-connect-20261114/final-review.json and resumed-native-20261115.json. Browser requires a running Editor; current Editor closure is the idle CPU policy.

Collector evidence/commands/EVD-93f8b6551dbcf715/evidence.json executed actual validate-run under an unshare network namespace. It establishes schema/run consistency; substantive requirement coverage comes from the review above. It is not a gameplay test. Native filesystem readback confirms AlchemyLabProps and120 total prefabs including mortar, retort, potions and books/furniture; material/visual/binding acceptance remains pending.

## Handoff readiness and order

Ready for bounded implementation under MOB-001. Start with the World Forge terrain/map adapter, one playable connection and one four-floor side cave with combat/loot/save/re-entry. Return an intermediate checkpoint for native proof before broadening content. Keep the full task running until all assigned capabilities are actually implemented and checked; no partial success may become a full handoff. Follow with route/story coverage, protected alchemist and professions, then integrated QC. Parent coordinates the single native Editor under the shared one-core budget and LP_NUM_THREADS=1; implementation agents must not launch Editors or heavy VPS tasks. No new asset bulk import, purchases or store work during this game block.
