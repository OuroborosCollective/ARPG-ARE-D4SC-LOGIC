# Asset-Store-Pipeline (Unity ARPG)

The private Unity account, purchase catalog and actual paid/free `.unitypackage` files must **never** be committed to this public repository. The project deliberately separates **selection** from **entitlement**, **download**, **import** and **Android runtime proof**.

## Verified architecture

- The production-independent Unity project targets Editor 2022.3.62f1.
- A private, **partial** 25-title catalog is present under `/var/lib/unityarpg/.local/share/are-arpg/private-asset-catalog.json`. It originated in the owner's My Assets excerpt, **not** an authenticated Unity Store inventory endpoint.
- Unity Personal and the Unity Editor have been verified in the `unityarpg` VPS environment; these account observations are not a guarantee that any specific Store package is available or compatible.
- Downloads of Unity Asset Store packages occur only through Unity-authorized interfaces (Hub/Editor > Window > Package Manager > My Assets). We do **not** scrape account endpoints, automate the private Store UI, bypass authentication or accept legal terms on the owner's behalf.

## Reproducible, offline choices

Select an area-specific **candidate list** without contacting the Asset Store:

```bash
python3 -m scripts.assets.plan_asset_choices \
  --catalog /var/lib/unityarpg/.local/share/are-arpg/private-asset-catalog.json \
  --profile ashen-crypt
```

Profiles: `ashen-crypt`, `ember-rift` and `mobile-performance`. The first Ashen Crypt choices are Lite Dungeon Pack, Skeleton Ghost and Low Poly Fantasy Sword Pack. Ember Rift prioritizes Stylized Rocks with Magic Rune. Do not mix inventory systems or assume that packages marketed for Unity 6 work with Unity 2022.

## Private import gate

After a package has been acquired and downloaded using the **official** Unity interface, identify the actual `.unitypackage` in the *private* Linux cache `~/.local/share/unity3d/Asset Store-5.x`. Do not trust a package name alone.

1. Verify **publisher, license/EULA, entitlements, version, exact file SHA-256**, dependencies and Unity version.
2. Use the official `unity assets inspect /absolute/path/package.unitypackage` command to inspect the actual downloaded file before import.
3. If the package contains C# or plugins, require source review; execute its import **in a separate private Unity test project**, never blindly in the canonical game branch.
4. Run real Unity import/compile, PlayMode and Android performance validation. Only then promote reviewed allowed scene references and receipts into the Unity ARPG project. Do not redistribute raw purchased assets or credentials.

The Python planner and its synthetic test fixtures prove **selection logic only**, not access to purchased assets, correctness of their content, a Unity import, or a device build.

Unity legal reference: https://unity.com/legal/terms-of-service and https://unity.com/legal/as-terms (2026 restrictions on automated asset-store access).


## Reviewed visual bindings (VPS-only licensed content)

The Unity runtime can now prefer reviewed, passive mesh prefabs in private
`Assets/Resources/ARE/Visuals/*.prefab` while retaining its original
primitive visuals as an explicit, visible fallback. This directory is ignored
by Git because store-owned content must not be redistributed publicly.

- `Hero`, `Sword`, `Enemy0`, `Enemy1`, `Enemy2`, `Enemy3`
- `CryptFloor`, `CryptPillar`, `CryptPillarCapital`, `CryptAltar`
- `EmberFloor`, `EmberCauseway`, `EmberBasalt`, `EmberBossDais`, `EmberGate`

Only explicitly reviewed prefabs containing transforms, meshes, mesh renderers,
LOD groups or animators are accepted. Anything with scripts, physics,
cameras, lights or broken component references is rejected **before**
instantiation. Animator root motion is forcibly disabled. The existing
placeholder remains the owner of the server-independent deterministic
single-player coordinates; materials and animation are cosmetic only.
Artists must align scale and pivot inside each private visual prefab because
the host transform retains its gameplay position and scale.

A Unity Store package is **not** proven imported just because a slot exists.
The explicit gates remain: authenticated official My Assets entitlement,
license review, package SHA256, clean private project import, visual review,
Unity PlayMode, Android APK and physical-device capture. These PlayMode
binding tests use test-only generated mesh fixtures, not real store packages.
