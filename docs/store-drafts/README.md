# Draft-only upload checkpoint — 2026-11-15

Owner explicitly prioritizes uploading the existing Fadenwerk and World Forge
modules through the Unity Editor and retaining them as Asset Store drafts.
Prices: USD 6 and USD 35 respectively, one-time. Do not submit for review or publish.
Descriptions, visual presentation and commercial release refinements follow later.

Source snapshot: `2f296b26767ad0746a2ca26468ef299f91e43b57`.
Use a separate minimal upload project. Export only the explicit `Assets/Fadenwerk`
and `Assets/WorldForge` roots, individually, without dependency expansion.
No Ashen Crypt game, licensed characters/vegetation, saves, private settings,
credentials, or the official Unity uploading tools may enter either sale package.

These are work-in-progress drafts, not release-approved products. Known remaining
work includes standalone sample paths, independent examples/documentation,
license text, fresh-import regressions and final Publisher validation.

Official uploader source inspected:
`Unity-Technologies/com.unity.asset-store-tools`, tag `12.0.0`,
commit `944773fcec4d9e1551bc8058523e5ccb649ab87e`.
The Store advertises newer `12.0.6`; do not claim the Git tag is the latest.
Use the official updater/Package Manager if the service requires the newer version.

Unity 6000.6.8f1 initially rejected the upstream 12.0.0 uploader at compile time:
legacy `GUID`, `GetInstanceID`, integer `GetAssetPath`/preview overloads. A local
three-file compatibility patch uses `System.Guid.NewGuid().ToString("N")`, the
existing Object overload of `GetAssetPath`, and `GetEntityId` for preview lookup.
This is a patched local installation, not an unmodified latest Unity release.
No authentication, validation or upload rules are bypassed. Native compilation,
both isolated exports and existing-account login succeeded. All 148 supplied
module files remained unchanged. Local download hashes and archive paths were
independently verified; see `export-readback.json`.

Upload is blocked before any target mutation: the authenticated publisher list
contains no Fadenwerk or World Forge draft. The unrelated Dungeon Blueprints
draft and pending-review Memory Forge package are left untouched. New products
must first be created in the Publisher Portal; the available portal browser
failed to initialize. The owner has been asked to create the two named drafts.
No package has been uploaded, submitted for review or published by this block.
Prices are requested values, not verified portal settings.
