# Memory Forge — proposed Asset Store listing

This is the reviewable submission text. It is not a receipt of upload, submission, approval or publication.

**Title:** Memory Forge — Deterministic NPC AI  
**Category:** Tools / Behavior AI  
**Version:** 1.0.3<br>
**Price:** USD 9.50, one-time purchase, as explicitly selected by the owner. Regional currency conversion and taxes are controlled by the storefront.  
**Publisher:** use the owner's verified Unity publisher profile; the project identity is OuroborosCollective.  
**Automatic publication:** disabled. Read back Unity's review outcome before making the listing live.  
**Support contact/site:** use the existing verified publisher profile; no contact address has been invented.

## Short description

Give NPCs individual memories, relationships and explainable decisions. Local integer utility AI, optional neural profiles, exact-version saves and a playable Unity example. Source included.

## Description

A player helps an NPC. The NPC remembers who helped, trusts them more and greets them differently next time—even after loading a save. A guard behind a wall does not mysteriously learn about a theft.

Memory Forge connects those moments to your game's existing rules. It combines deterministic action scoring with individual, source-aware memories and relationships. You supply the world state, tick and legal actions; your game remains in charge of movement, combat, inventory and quests.

**Included**

- Pure C# integer utility evaluator with stable tie order, hard action permissions, minimum action duration and hysteresis.
- Nine archetype templates, immutable versioned controllers, hash checks, regression admission and explicit migration receipts.
- Individual event memories with direct, witnessed, reported, suspected and refuted origins; deduplication and eight relationship dimensions.
- Checksummed save/load with exact historical controller restoration and persisted decision continuity.
- Optional small quantized neural profiles evaluated locally. No runtime training loop or automatic weight changes.
- Controller and registry configuration assets, regression tools and a read-only live NPC Inspector.
- Original remembering-village example with actual ration/purse transfers, visibility-dependent witnessing, subsequent behavior and PlayerPrefs save/load.
- Complete source, offline English documentation, a German quick start and optional Unity tests.

There are no external AI services, API keys, subscriptions or inference fees. Your NPC history stays in your host-owned save.

**Requirements and boundaries**

Verified with Unity 6000.6.8f1, Built-in rendering and URP 17.6.0. The Unity-exported package matches all 99 source files and the actual import. The clean Built-in project was upgraded from 1.0.2 and passed 12 Editor and 3 PlayMode package tests. The URP verification project passed those cases plus 2 verification-driver failure tests (12 Editor, 5 PlayMode total). Its standalone Linux player ran with exact save restoration. Optional tests use Unity Test Framework 1.8.0. The Android ARM64/IL2CPP example built successfully and passed APK signature, ZIP integrity and 16-KiB alignment checks. Physical Android device execution and HDRP presentation have not been validated.

This is a programmer's integration toolkit. Your game supplies navigation, perception queries, action execution, quests, companion progression and inventory. The commercial package does not include Ashen Crypt's RPG systems or licensed character/weapon/environment art. It does not generate dialogue with a language model or infer facts from natural-language descriptions. The example uses original simple geometric characters.

The library allocates managed objects and retains event history. Long-running worlds must budget archive growth and save size; the configurable default decode budget is 16 MiB. It is not a zero-allocation/DOTS/Burst package. No universal NPC-count or device-FPS claim is made. HDRP example presentation has not been validated.

No third-party art or source library is bundled; see **Third-Party Notices.txt** in the package for platform and optional dependency information.

## AI description field

OpenAI Codex/ChatGPT assisted development of the C# source, tests, example and documentation. The implementation was adapted to explicit host ownership, fixed-point arithmetic, historical save compatibility, provenance and versioned admission requirements, then inspected and exercised through real .NET and Unity checks. The release dossier identifies the executed revisions and failures that were repaired. OpenAI image generation created the promotional cover and logo; the owner reviewed and approved the cover. Promotional artwork is separate from actual Unity screenshots. No generative service is called by the runtime library.

## Keywords

NPC, utility AI, memory, relationships, deterministic, behavior, local AI, neural controller, save load, simulation.

## Preview captions

1. **A favor remembered** — Actual standalone Unity example after helping Nara and restoring the PlayerPrefs save. Trust and personal memory come from the executed transaction.
2. **Why this action?** — Actual weighted priorities from the same running example, with hard permissions applied before scoring.
3. **Memory Forge** — Promotional cover illustration. This is branding, not a gameplay screenshot.

## Submission state and checks

- Owner-approved cover and matching logo exist in the release workspace. Final portal image dimensions must be checked against the live upload fields.
- Price authorization: USD9.50 one-time.
- Native game integration evidence is separate from package evidence; game screenshots are not used to imply included art.
- Version 1.0.3 is verified on source `dbe82124ac6d9ad42e874e1f584c4516f2c6c440`. The sight query now updates the actual unlit material; the separate evidence driver preserves existing preferences before a snapshot and restores them even if failure logging also fails. Three native RED regressions were reproduced before the fixes. Package, import, tests, player and Android receipts are in `docs/evidence/memory-forge-package-20261113/release-1.0.3`. Android APK SHA256 `34df034bf85116682f339616925546eb6d4ea21f3effd060b4beec84ed5fad44`; physical device execution remains untested.
- Owner handoff: the owner requested the downloadable package and will handle Unity Asset Store publication. Supply the `.unitypackage`, documentation, approved cover/logo, actual example screenshots and evidence. Resume Ashen Crypt development; do not request another remote-browser login.
- No upload, submission, review approval or live sale has been observed.

Official workflow references, read 2026-11-13: [Submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines), [validate and upload](https://docs.unity.com/en-us/asset-store/publishing/asset-packages/upload), [submit for review](https://docs.unity.com/en-us/asset-store/publishing/asset-packages/submit), [provider agreement](https://unity.com/legal/provider).
