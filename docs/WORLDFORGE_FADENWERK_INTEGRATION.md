# World Forge / Fadenwerk: isolated authoring integration

Base: `4622c94866f45e129a7d60e3734538c45dd1465f`, Unity `6000.6.8f1`.
Owner continuation on 14 November 2026. Imported from the exact private owner bundle
SHA256 `75ad8f8479c7c2d4b9f98a8d5d6d0cac32713829a49fa05abb8cad6d23b69fb2`.

## Scope and authority

This slice installs and validates authoring tools in an isolated checkout. It does
not activate FadenwerkManager, change the game scene, replace woodland generation,
introduce a quest/reward authority, migrate saves, expose an HTTP gateway, publish
an APK, or claim Android-device acceptance. The original dirty Director checkout
and existing Fable UI/camera are preserved. AreArpg.Core remains the game owner.

| Surface | Classification | Responsibility |
|---|---|---|
| Assets/AshenCrypt/WorldForge/Core | core candidate | Immutable exported map format and queries; not bound to active simulation |
| Assets/AshenCrypt/WorldForge/Runtime | projection / authoring | Seeded terrain profile and visual generation |
| Assets/AshenCrypt/WorldForge/Editor | effect / production tooling | Explicit editor generation/export; no gameplay activation |
| Packages/com.ouroboros.fadenwerk/Editor | production tooling / effect | Editor and optional loopback browser authoring; auto-start remains false |
| Packages/com.ouroboros.fadenwerk/Runtime | runtime candidate | Standalone package runtime; no manager instantiated by this integration |
| WorldForge and Fadenwerk Tests | test | Real native package checks, not game authority |
| Tests/FadenwerkJsonVerification | test | Links the real JSON writer; parser-based Unicode/control/HTML regression |
| Packages/manifest.json and packages-lock.json | production configuration | Required built-in Terrain modules and explicit UPM package test discovery |

## Imported inputs and privacy

Fadenwerk is embedded once under `Packages/com.ouroboros.fadenwerk`; no second
Assets copy or unitypackage import is used. World Forge sources preserve supplied
GUIDs. The supplied nature pack is a private input at
`Assets/Studio/Nature/AshenKrypt`; its raw models/textures are not published.
The exact owner bundle and private source manifest are required to reconstruct it.
The adapted Editor/AshenKryptAssetSetup.cs and folder/script GUID metadata are
tracked code. Restore raw nature inputs without replacing this adapted Editor helper.
Existing private inputs were copied and hashed (4,348 files, 2,401,501,653 bytes).
All 73 supplied package GUIDs were distinct from the existing project inventory.

The source candidate names say 1.0.1 while the embedded Fadenwerk package.json still
identifies its upstream runtime as 1.0.0. Hashes and the integration diff identify
this candidate; no unverified release version is implied.

## Reproduced integration defects

1. FwJson contained raw U+2028/U+2029 characters inside C# literals. Actual Unity
   compilation and a linked .NET regression failed with CS1010/CS1011. Explicit
   C# Unicode escapes preserve the intended characters. All four JSON roundtrip,
   control-character and inline-HTML assertions now pass.
2. The bundled nature setup collided with the pre-existing private global
   AshenKryptAssetSetup class. The newly supplied helper has its own namespace,
   unique World Forge menu and exact own-root lookup. The sample resolver follows
   that type. The existing helper remains unchanged. A native editor regression
   checks the new root and menu binding.

## Validation and open gates

The unchanged existing Core passed 8,112 real .NET assertions; its source and test
hash inventory is bound in the private core-checks receipt. Native package import,
EditMode and PlayMode results are recorded separately after execution. Initial
failed Unity logs are retained. An absent/failed/skipped result is not a pass.

Before gameplay activation: compile stable Fadenwerk content IDs/conditions/rewards
into AreArpg.Core, implement one atomic completion/inventory/progression/save
transaction with replay and reload idempotency, validate legacy AC1–AC19 loading,
and bind World Forge maps to WoodlandLayout run/seed/revision ownership. Audit
map bounds, resource limits and large-coordinate arithmetic before accepting
untrusted map data. Standalone Fadenwerk local duplicate suppression is not proof
of the game transaction. Browser access from the tablet needs a separately verified
authenticated gateway; no public forwarding was created.

The formal SDLC and AI DevKit runtimes are not initialized in this repository.
Existing Director/spec workflows remain canonical. No synthetic run, passing gate,
model setting, or approval record was created for those absent runtimes.

The fresh asset import exceeded its 900-second budget after successful script
compilation (TIMEOUT, not PASS). Residual processes for this isolated job were
reconciled before further operations. Derived cache copying is tied to the exact
base; no old test receipt is reused as new runtime evidence.

The metadata preflight fails identically in the original private baseline and this
workspace on the pre-existing OwnerForestReview Editor script meta (missing
MonoImporter). The assertion remains intact. Repository-only checks and native
private-overlay checks are reported separately; this is not a clean full gate.

## Independent review corrections

Read-only standards review identified backing-array escapes behind IReadOnlyList;
real map mutation tests failed 0/2 before cached read-only views. Scope review
identified the sample command creating a competing manager in the active scene.
It now creates its own database and additive authoring scene, with no Manager or
DebugHud and no instruction to activate gameplay. A native scene-sentinel
regression failed at the missing safe entry point before implementation; its
first compile-error fixture is retained separately and is not behavioral RED.

Owner: Thomas Markgraf, using the artist pseudonym OuroborosCollective (one person).

The Editor cannot create a second untitled scene additively. Instead, authoring
uses an empty native scene template (derived from the pinned Arena scene, with
zero GameObjects and zero MonoBehaviours), copies it to a unique owned asset and
opens that copy additively. Existing unsaved scenes retain their objects, path and
dirty flag; no caller scene is saved or closed. The native regression checks this.
The initial fixture setup failures remain recorded separately from production bugs.

The isolated copy upgrades the pre-existing OwnerForestReview Editor script meta
to an explicit native MonoImporter while preserving its GUID and all source/raw
asset bytes. The original workspace is unchanged; the exact metadata delta is in
the private input recipe.
