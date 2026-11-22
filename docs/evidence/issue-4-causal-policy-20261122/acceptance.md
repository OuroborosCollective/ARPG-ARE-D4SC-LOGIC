# Issue #4 · Causal NPC policy · revision-bound acceptance

**Scope:** Native code in `Assets/Scripts/Core/NpcForgeCalm.cs` and `NpcForgeBehavior.cs`; regression source in `Tests/CoreVerification/CausalPolicyTests.cs` and its existing runner. No new policy engine, controller version, event store, tick source, save format, item counter, or Unity presentation.

## Causal defect and RED

Exact base `main`: `8f0cd525d52ec0a1924af6f93bb635250208cc47`.

On test-only head `a1927734dab84e787b7d007438f66701cfb4bc59`, GitHub Actions [run 45062225471](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062225471), job `137330476799`, executed the actual .NET 8 project `Tests/CoreVerification`. The compiler succeeded and `policy-known-vs-unknown` passed; `policy-indirect-no-hostile-attack` failed with a real exception at `CausalPolicyTests.cs:115` and exit code 1.

**Cause:** `NpcAgent.Mood` and `StepForgeNpc` previously translated the aggregate `Relationship.Hostility` into hostility/legal `Attack` without checking the known memory's origin. A directly witnessed act and a one-hop report can have identical hostility totals but different evidence for combat authorization.

## GREEN

Code fix head `112e6ad36f93fd35236b857ffdd2f283dbac34f5`.
GitHub Actions [run 45062322997](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062322997), job `137330772444`, compiled and ran the actual shared simulation against that head:

- **22,668** deterministic Core assertions passed, including all six requested families: `policy-known-vs-unknown`, `policy-indirect-no-hostile-attack`, `policy-danger-and-needs-precedence`, `policy-no-created-resources`, `policy-controller-exact-version-load`, `policy-10000tick-replay`. Additional provenance, legal-mask and downed-state assertions passed.
- **54/54** Python preflights, **76/76** Director/skill tests, **5/5** gateway tests, four actual Fadenwerk JSON assertions and 45 actual Fadenwerk rules assertions passed.
- The [Neural Hub contracts job](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062322997) also passed.
- The licensed GitHub Unity PlayMode job [45062323011](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062323011) was **skipped**; it is not Unity Runtime evidence.
- Wolfram independently checked the integer threshold: trust 450, gratitude 771, Observe 1497 without direct association versus 1502 with direct association, Travel 1500. This math is a boundary check, not a claim of native Unity execution.

## Boundary and regression notes

Only `KnowledgeOrigin.Self` / `KnowledgeOrigin.Witnessed` facts about an actual harmful player act are eligible to authorize threat escalation from the Forge relationship. Reported or suspected knowledge still contributes to remembered relationships and dialogue; neither may independently authorize `Attack`. The existing legal action mask, autonomous movement, health, existing active controller hashes and AC save readers remain unchanged. Historical `NpcSocialTests` verify direct guard retaliation. Non-Forge legacy behavior remains the existing path.

**Truth classes:** source/readback + real CI are verified for the stated SHA. Native Unity Editor/PlayMode, Android APK and physical Tab A9 are **NOT VERIFIED in this change**; a passed C# Core job is not a deployed runtime receipt. The issue's acceptance contract is Core- and provenance-focused. No production state or private Unity assets were changed.

**Merge condition:** final documented head must independently pass CI, all requested review threads resolved, merge into `main` read back with the actual commit SHA, then Issue #4 close as completed.
