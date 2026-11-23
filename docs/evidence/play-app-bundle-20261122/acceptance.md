# Signed Google Play App Bundle · 2026-11-22

## Owner decisions

- Updates for the public beta go through a Google Play testing track.
- Ashen Crypt gets its own new upload key. The keys of the other project exist only as write-only GitHub secrets and are not reused.
- Bundle size: up to 3 GB is acceptable, so textures and meshes stay as they are.

## What the build does

`ArpgBuildTools.BuildAndroidAppBundle`, menu **ARE ARPG/3. Build Signed App Bundle for Google Play**:

- The upload key and its passwords come only from the signing file named by `ARE_ANDROID_SIGNING`. The file and the key must lie outside the project, so neither can be committed; missing or incomplete data refuses the build.
- Version code = seconds since 2026-01-01 UTC, so every later bundle carries a higher one, also when two builds start in the same minute (changed after review; the two bundles below were built while it still counted minutes, and every code counted in seconds is higher).
- App Bundle with split application binary: the game data travels in the install-time asset pack `UnityDataAssetPack`, the base module stays small.
- Unity's internal bundle size warning is raised to 3,072 MB; above 200 MB it used to stop the build with a modal question.
- Afterwards the key fields, the version code and the bundle flags are as before, and both passwords are removed from the generated Gradle project (Unity writes them there as plain text), whether the build succeeded or not.

## Evidence

Unity 6000.6.8f1, Unity CLI run in the live editor (not a Director job), project at `main` `38a45c23e102d64cbd8f91afe4d8a60f8756d70a` plus this branch.

Second signed bundle, built by the final build method without any manual step (no size question appeared). This is the bundle to upload:

- SHA-256 `c0d1deaeb4aae89adbf1fb8cb986a14638209cc631ca0604b60a96b21341e967`, 276,237,823 bytes, version code 469396 (higher than the first bundle's), version name 1.0, `com.ouroboroscollective.areashencrypt`, minSdk 26, targetSdk 36
- `jarsigner -verify`: verified, signer is the upload key (same SHA-1 and SHA-256 as below)
- `bundletool validate`: feature module `base` and asset pack `UnityDataAssetPack`
- Download size per device: 275,341,837 bytes (SDK 26–28) and 275,359,997 bytes (SDK 29+), arm64-v8a
- Player settings read back after the build: no custom key, empty key name, alias and passwords, version code 1, bundle and split flags off, size warning at 3,072 MB
- Key password search: generated Gradle project 0 files (the build removed them), editor log 0. Gradle's compiled-script cache in the build user's home held 2 entries; the post-build tool stopped the Gradle daemon and removed them, a second search found 0

First signed bundle (built before the size and cleanup fixes; the build stopped at the size question until it was answered by hand):

- SHA-256 `2a031986d8a8dc9fb2ed01c7de71d68c0f42015e30a04751fd73b4a472b1fb6e`, 276,237,828 bytes, version code 469318, version name 1.0, `com.ouroboroscollective.areashencrypt`, minSdk 26, targetSdk 36
- `jarsigner -verify`: verified. Signer `CN=Ashen Crypt Upload, OU=Ashen Crypt, O=OuroborosCollective, C=DE`, SHA-1 `ED:77:84:28:CF:42:EF:26:F5:50:10:4F:73:83:62:65:FD:E9:29:9F`, SHA-256 `69:05:7F:1A:23:32:D2:BE:09:30:D0:7B:6C:B7:03:45:03:85:BA:B6:F7:4E:C6:38:72:10:94:F7:51:A9:B0:A0` (the upload key)
- `bundletool validate`: feature module `base` and asset pack `UnityDataAssetPack`
- Download size per device (`bundletool get-size total`): 275,341,695 bytes (SDK 26–28) and 275,359,863 bytes (SDK 29+), arm64-v8a. Unity's own estimate: base module 31 MB, base plus install-time asset pack 262 MB
- Player settings read back after the build: no custom key, empty key name, alias and passwords, version code 1, bundle and split flags off
- Key password search (only paths and counts printed): editor log 0 files; generated Gradle project 1 file (`launcher/build.gradle`, both passwords) and Gradle's compiled-script cache in the build user's home 3 files. All four were cleaned; a second search found 0. The build method now cleans the Gradle project itself; the home cache is cleaned by a private post-build tool outside the repository.

EditMode `AndroidPlayBundleTests` in the live editor on the final code: 5/5 passed, 0 skipped (rising version codes also within one minute, signing file only from outside the project, project path recognised in any case where the file system ignores case, password removal from Gradle files, 3 GB size warning); Unity XML SHA-256 `67c924bbd653ab43346253557183a49c531398309f0ee7be35f95be0e1e9699e`. Static Unity/Android contract preflight and the 54 preflight tests pass.

## Not verified

- Upload to the Play Console and installation through a testing track: owner steps, not done here.
- Physical device: install, cold start, touch, save/restore **UNVERIFIED**.
