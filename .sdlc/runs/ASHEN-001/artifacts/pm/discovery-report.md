---
run_id: ASHEN-001
task_id: PM-001
producer: pm
status: draft
revision: 1
---

# Discovery report

## Existing patterns

Source baseline: 6699652371a6755c58c69dc8578dd724e1206f8e (Fadenwerk code inputs match native becf6e84ba3dbf311b6518a794a1d53de7dc64fd). Unity pin ProjectSettings/ProjectVersion.txt: 6000.6.8f1. Read AGENTS.md, Memory.md and docs/SPEC_DIRECTOR_WORKFLOW.md.

Core: WoodlandJourney.cs, WoodlandState.cs, CryptDungeon.cs, CryptDungeonState.cs, CryptDungeonLayout.cs, AschenzollFreight.cs, HungerJourney.cs and CharacterProgression/ArpgSimulation partials. Existing forest is act 8, bear nest 9, Aschenzoll 10. Side-cave floor machinery is currently tied to the three-floor story crypt. Inspect every save/travel/companion/input caller before extending acts or formats. Use existing loot catalog and equipment receipts; do not invent an independent inventory.

Canon sources: docs/ASCHENZOLL_FREIGHT.md, docs/RATTLER_IMMERGRUEN.md, docs/specs/immergruen-journey/owner-decisions.md. World Forge is an authoring data generator; Fadenwerk's first production binding is validated Hunger text, not a general runtime quest authority. Existing Assets/Editor/AshenFadenwerkEditor.cs demonstrates the boundary.

## Affected applications

Only existing Unity mobile game, including its C# Core, Unity presentation, editor authoring, tests and save contracts. No web/backend application, network service, SQL store or Redis in this run. The earlier Fadenwerk gateway is a separate accepted integration block. The generic mobile preset describes Unity in SDLC; it does not introduce Flutter.

## Delivery risks

The owner corrected the geography and selected espionage with politically risky military information. Lore unknowns FACT-016–018 must remain explicit questions with three proposals each, answered in sequence as useful. Requirements may not treat guesses as approved facts. Plan technical interfaces and independent dungeon generation while those decisions are open.

Runtime checks are distinct: local C# tests/build, Python static preflight and Director checks; native Edit/PlayMode on exact VPS revision; IL2CPP/ARM64 build; physical tablet acceptance. The generic SDLC checks are configured to real existing tools. Offline collector calls run inside an actual unshare network namespace before setting the runtime policy attestation. No unverified network restriction or synthetic evidence.

The SDLC plugin supplies role instructions; the pinned npm CLI 1.0.1 owns .sdlc bookkeeping. Existing Director/Spec Kit/SPD remain the execution/contract sources, not competing product authorities. The new run remains open until its full scope and required owner decisions are satisfied.
