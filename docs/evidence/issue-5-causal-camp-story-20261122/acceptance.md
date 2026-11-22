# Issue #5 · Existing camp/story causal vertical slice

## Ownership and scope

Base `main` `e22a8dac9ca37e2da3de3bc19911712a57dcaf11`. **No new quest loop, inventory authority, faction owner, archive, network authority, or tick source.**

The only production change is `Assets/Scripts/Core/ArpgSimulation.cs`: `InteractNpc` now rejects every direct NPC input while `ArrivalScenePending`, before social/reputation/resource effects or cooldown/RNG mutation. Unity already pauses the simulation, hides the HUD and drains input queues during that scene; Core now owns the same invariant. Older saves that have advanced past the scene, and vanilla non-Forge arena villages, do not gain an invented pending scene.

The existing owner chain remains:
`ArenaRuntime → ArenaSimulation → SocialLedger transfer → MemoryArchive.Append → RecordCampDecision(CauseEventId) → CharacterProgression Save`.
Successful ration donation, theft and Ilyan conversation are still authored by their existing methods. Rejected actions do not mutate the owned state or issue an event ID.

## RED → GREEN from real GitHub Actions

- **RED** `daa02452d3a9928f17927a45f9e5fa79d10064dc`, [Actions 45063318565](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45063318565), Core job 137333864839: `FAIL slice-intro-input-gate-Greet`, actual nonzero exit. A direct NPC greeting bypassed the pending first-arrival scene.
- The subsequent Core-only correction `616be6629fdb016f0337a849f34eb420ecdb08cf` blocked the attack/greeting/help/rob paths. Existing tests revealed previously implicit arrival prerequisites and one new test fixture saved the NPC's position before its explicit placement. The **fixture** was corrected without weakening the gameplay invariant; affected preexisting tests now explicitly complete the real one-time scene before their contact scenarios, without bypassing validations.
- **GREEN** code `f92416d1e612d2c42e0be0f6f80d7ed302ff823d`, [Actions 45063991119](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45063991119), Core job 137335946210: **22,702** real deterministic Core assertions. Four real Fadenwerk JSON assertions, 45 real Fadenwerk validator assertions, **54/54** static preflights, **76/76** Director/skill checks, **5/5** gateway checks. The separate read-only Neural Hub CI job passed.

## Concrete vertical-slice assertions

- `slice-refill-final-only`: the first accepted donation transfers exactly one player's ration, keeps the choice open, and does not create a `camp.decision.replaced`; the final donation creates exactly one decision whose parent is the actual final `camp.donation` event ID. Save/load retains the parent.
- `slice-rob-existing-witness`: one original theft debits one ration, credits one existing player ration, writes one `camp.theft` and one `camp.decision.stolen` using that theft's ID. Mara learns directly; Dorn learns that **same event ID** only when the actual Core line-of-sight and distance gates admit him, demonstrated with both visible and occluded physical paths. The actual persistent one-hop provenance survives save/load.
- `slice-intro-input-gate`: `Greet`, `Help`, `Attack`, `Rob`, `HearIlyan`, donation and theft are rejected without affecting saves/hashes while the fixed scene is pending. Completion adds exactly one root scene event; the genuine later conversation with Ilyan is a root event without a fabricated historical cause.
- `slice-gate-after-decision`: both ration choices are rejected after any completed branch, and the UI-facing Core panel flags are false; no resource or new event is minted by attempted repeats.
- `slice-legacy-no-retroactive-parent`: the original AC5/17/18/19/20 saved tokens round-trip unchanged; no links are manufactured for historical receipts. Existing combat, hunger, forge, economy, dungeon and equipment suites run in the same real CoreVerification project.

## Native projection and boundary

The pinned Unity version is **6000.6.8f1**. Source already has `ArrivalScenePresentation` and PlayMode tests `ArrivalSceneOwnsAllInputUntilItEnds`, `StealingFromMaraIsTheDecisionAndRemovesTheRationPanel`, and `AskingIlyanIsTheDecisionAndRemovesTheRationPanel`. Their assertions belong to Unity/PlayerPrefs/UI/animation projection; C# Core CI is **not** native PlayMode evidence.

**Native verification:** pending independently executed same-SHA Unity run (or clearly recorded blocker). GitHub licensed PlayMode CI is skipped; this is not a PASS. No Android APK built and no physical Samsung Tab A9 test in this slice. The device test is optional for issue completion but cannot be represented as a completed device run.

## Release and readback gates

1. Independently verify final PR HEAD and relevant checks after all documentation changes.
2. Require no unresolved review threads and expected base `main` SHA.
3. Exactly one concise `Memory.md` entry with code, finding and authentic evidence.
4. Only merge after applicable gates; compare merged `main` Git tree to final PR head, re-read issue closed/completed, and check post-merge CI separately. Owner's private DOCX must not be claimed updated without actual write/readback.
