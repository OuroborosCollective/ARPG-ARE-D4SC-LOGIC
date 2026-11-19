# Ashen Crypt – World Forge Asset-Eingang (#62)

**Status:** Code in isolated Draft PR #95. Not merged or production activated until real Unity 6000.6.8f1 EditMode acceptance and asset readback. No game authority outside AreArpg.Core.

## Owner workflow

1. Open **Tools → World Forge → Asset-Eingang** in Unity 6000.6.8f1.
2. Select a previously saved `BiomeProfile` and `WorldProfile`; select an imported private asset package folder under `Assets/PrivateArt`.
3. Click **Paket/PrivateArt jetzt einmalig scannen**. The first scan may be large; no scan is performed on imports or every GUI repaint.
4. Search for the prefab. Review its preview thumbnail, actual local mesh size, reason for its class and proposed clearance. `Protected` never offers a decor button; `ReviewRequired` requires a deliberate policy/code review first.
5. Click **Als Spawn bestätigen** or **Als POI bestätigen**. Only that selected existing profile is modified, with Undo and `AssetDatabase.SaveAssetIfDirty`. Approvals persist as source GUIDs in the existing `BiomeProfile.spawnRules` or `WorldProfile.pois`. Repeating approval is idempotent. Rejections are stored in the same `BiomeProfile.rejectedAssetGuids`, not a new external catalog.
6. New POIs are saved **with `intakePlacementPending=true`**, **flattenRadius=0** and **clearSpawnRadius=0**. Only after setting verified coordinates may the owner configure a deliberate flatten/clear radius and explicitly uncheck pending. Even legacy pending entries with positive radii are ignored by the height layer; approval alone never flattens the origin.
7. Every generation/respawn/height-only entry point revalidates active approved POI GUIDs and classification **inside Validate(p), before changing terrain, scene roots, or profile settings**. Disabling or removing all POIs clears the previous `WF_POIs` root. Missing private references retain their GUID for future re-import.

## Safety and policy

The source path is never a policy. Only known **per-package** roots produce candidates: `VisualPass1/VikingVillage`, `DesignAssets/StylizedFruitProps`, `DesignAssets/AlchemyLabProps`, `DesignAssets/SimpleMedievalProps`, `OwnerForestReview/Prefabs`, and `FantasyKingdom3D/Prefabs`. These allow only *proposals*, not automatic imports.

`WoodlandWeapons`, `CombatIntake`, `HunterBear`, and `Resources` are protected roots. Animator, Animation, skinned meshes, particles, VisualEffect, gameplay MonoBehaviours, missing components, and runtime-linked object identities block decoration. Unity C# source `GameObject.Find`/`transform.Find` names are negative protections. The exact same scanner runs on explicit scan, **again at ConfirmSpawn/ConfirmPoi** (never trust caller-provided verdicts), and before using active approved POI visuals. Missing or unreadable runtime source fails closed.

Geometry is derived from `MeshFilter.sharedMesh.bounds` transformed into root-local coordinates; a prefab asset's arbitrary scene/world origin is never used as spatial truth. Proposed POI geometry/clearance is measured from half the horizontal diagonal plus a safety margin; a pending POI has **zero** effective terrain and spawn-clearance authority until the owner explicitly configures placement. No undeclared image classifier, no Wolfram inference as automatic authority, no auto-install, no AI runtime invocation.

An `AssetPostprocessor` pushes **only changed prefab/model paths** into an open review window. It never scans the whole private package on each import or writes a game asset. Unknown packages/geometry abstain. No entry is inserted in `Resources`, no second item/quest/Save registry, and no automatic terrain build occurs on approval.

## Acceptance

- Execute `AshenCrypt.WorldForge.Tests.AssetIntakeEditModeTests` on a real, isolated Unity 6000.6.8f1 project with the licensed private files locally available. It contains **real asset** count assertions (`AssetDatabase.FindAssets("t:GameObject", new[]{"Assets/PrivateArt"})`, at least 791 imported assets), not a synthetic proxy for the owner's asset inventory.
- Prove actor/skinned/Animator, effects, script-bound identities, and weapon-package roots are never eligible for decorative spawns. Confirm small static safe props and large POIs are only proposals until a human explicitly approves them. Verify duplicate approval is idempotent and GUIDs survive a missing private file.
- Validate the incremental changed-only path with actual Editor imports, no unauthorized scene dirtiness, no duplicate Unity owner and exact workspace SHA.
- Report total real GameObject count, protected/review/candidate counts and category distribution. **Do not report a classification accuracy percentage** without a labelled independent ground-truth set.
- Execute native EditMode and adjacent WorldForge tests plus real `Tests/CoreVerification` and required CI before merge. An Actions job that skips licensed Unity is **not** Unity evidence.

## Cross-project dependency

The isolated Neural Hub read bridge in PR #93 can suggest issue work, but it cannot approve assets, edit the `BiomeProfile`, or claim Unity authority. The canonical Director and an owner confirmation govern any Unity changes. There is no second MCP, game state or editor controller.

## Pending POI authority boundary — 19 November 2026

An approved intake asset is **not yet a placed world point**. The existing profile retains its GUID and the owner can choose coordinates later, but `intakePlacementPending=true` excludes the record at **all three consumers**:

- `WFFlattenPoiLayer`: zero terrain-height influence, including legacy serialized positive flatten radii.
- `WFTerrainBuilder.Spawn`: no Poisson/vegetation spawn reservation, including legacy positive clearance radii.
- `WFCoreMapExporter.BuildActivePoiRecords`: no `ForgedPoiRecord` in the authoritative integer map. The original placed story/settlement POIs remain exported unchanged.

TDD: static source RED on `5340b4ca1869ea8d2345c501b677cbd6be968bab` (GitHub core workflow `44580735675` failure); native C# behavior regression added in `AssetIntakeEditModeTests.PendingPoiDoesNotEnterAuthoritativeCoreMap`. The actual licensed Unity EditMode execution remains **UNVERIFIED** pending restored owner VPS access. These source changes do **not** establish a live scene or Android result.

## Safety regression checkpoint — 19 November 2026

- Six source-contract tests in `Tests/Preflight/test_worldforge_asset_intake_release_guard.py` were introduced before production fixes. Real CI [RED run 44569143089](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44569143089) failed six preflight assertions; independently re-run [GREEN 44570147837](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44570147837) passed at code commit `a5647ca2a7acb12090c15e126c0391ac00b7c92e`.
- Editor-side native regression coverage additionally checks pending POI height-grid immutability, zero radii on approval, and wrong GUID rejection before a new world root can be created. The test prefab helper now cleans the private test directory even when prefab creation fails.
- **Evidence boundary:** GitHub Core/Preflight is real for that commit, but Unity 6000.6.8f1 EditMode, 791 private assets, real scene output, Android device and owner DOCX append remain **UNVERIFIED**. No production scene, Core, Save, permissions or live broker changed; PR #95 stays Draft until native readback.
