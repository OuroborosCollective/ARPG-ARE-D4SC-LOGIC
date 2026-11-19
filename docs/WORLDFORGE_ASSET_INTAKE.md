# Ashen Crypt – World Forge Asset-Eingang (#62)

**Status:** Code in isolated Draft PR #95. Not merged or production activated until real Unity 6000.6.8f1 EditMode acceptance and asset readback. No game authority outside AreArpg.Core.

## Owner workflow

1. Open **Tools → World Forge → Asset-Eingang** in Unity 6000.6.8f1.
2. Select a previously saved `BiomeProfile` and `WorldProfile`; select an imported private asset package folder under `Assets/PrivateArt`.
3. Click **Paket/PrivateArt jetzt einmalig scannen**. The first scan may be large; no scan is performed on imports or every GUI repaint.
4. Search for the prefab. Review its preview thumbnail, actual local mesh size, reason for its class and proposed clearance. `Protected` never offers a decor button; `ReviewRequired` requires a deliberate policy/code review first.
5. Click **Als Spawn bestätigen** or **Als POI bestätigen**. Only that selected existing profile is modified, with Undo and `AssetDatabase.SaveAssetIfDirty`. Approvals persist as source GUIDs in the existing `BiomeProfile.spawnRules` or `WorldProfile.pois`. Repeating approval is idempotent. Rejections are stored in the same `BiomeProfile.rejectedAssetGuids`, not a new external catalog.
6. New POIs are saved **with `intakePlacementPending=true`**. Set their coordinates in the existing `WorldProfile` inspector, inspect the proposed horizontal-diagonal-based flatten/clear radii, then **explicitly uncheck** pending before generating a world. A merely accepted POI is not spawned at zero coordinates.
7. The existing World Forge generation path independently revalidates every active approved POI GUID and classification **before** modifying the POI scene graph. Missing private asset references remain in the source profile with their GUID so importing them later does not silently erase approvals.

## Safety and policy

The source path is never a policy. Only known **per-package** roots produce candidates: `VisualPass1/VikingVillage`, `DesignAssets/StylizedFruitProps`, `DesignAssets/AlchemyLabProps`, `DesignAssets/SimpleMedievalProps`, `OwnerForestReview/Prefabs`, and `FantasyKingdom3D/Prefabs`. These allow only *proposals*, not automatic imports.

`WoodlandWeapons`, `CombatIntake`, `HunterBear`, and `Resources` are protected roots. Animator, Animation, skinned meshes, particles, VisualEffect, gameplay MonoBehaviours, missing components, and runtime-linked object identities block decoration. Unity C# source `GameObject.Find`/`transform.Find` names are additional negative protections, gathered **only on explicit scan**.

Geometry is derived from `MeshFilter.sharedMesh.bounds` transformed into root-local coordinates; a prefab asset's arbitrary scene/world origin is never used as spatial truth. POI flatten/clearance derives from half the measured horizontal diagonal plus a safety margin. No undeclared image classifier, no Wolfram inference as automatic authority, no auto-install, no AI runtime invocation.

An `AssetPostprocessor` pushes **only changed prefab/model paths** into an open review window. It never scans the whole private package on each import or writes a game asset. Unknown packages/geometry abstain. No entry is inserted in `Resources`, no second item/quest/Save registry, and no automatic terrain build occurs on approval.

## Acceptance

- Execute `AshenCrypt.WorldForge.Tests.AssetIntakeEditModeTests` on a real, isolated Unity 6000.6.8f1 project with the licensed private files locally available. It contains **real asset** count assertions (`AssetDatabase.FindAssets("t:GameObject", new[]{"Assets/PrivateArt"})`, at least 791 imported assets), not a synthetic proxy for the owner's asset inventory.
- Prove actor/skinned/Animator, effects, script-bound identities, and weapon-package roots are never eligible for decorative spawns. Confirm small static safe props and large POIs are only proposals until a human explicitly approves them. Verify duplicate approval is idempotent and GUIDs survive a missing private file.
- Validate the incremental changed-only path with actual Editor imports, no unauthorized scene dirtiness, no duplicate Unity owner and exact workspace SHA.
- Report total real GameObject count, protected/review/candidate counts and category distribution. **Do not report a classification accuracy percentage** without a labelled independent ground-truth set.
- Execute native EditMode and adjacent WorldForge tests plus real `Tests/CoreVerification` and required CI before merge. An Actions job that skips licensed Unity is **not** Unity evidence.

## Cross-project dependency

The isolated Neural Hub read bridge in PR #93 can suggest issue work, but it cannot approve assets, edit the `BiomeProfile`, or claim Unity authority. The canonical Director and an owner confirmation govern any Unity changes. There is no second MCP, game state or editor controller.
