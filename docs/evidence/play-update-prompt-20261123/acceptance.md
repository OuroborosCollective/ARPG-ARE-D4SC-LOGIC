# Update check at game start · 2026-11-23

## Owner request

The game should check for updates when it starts. Updates are distributed through Google Play (owner decision 2026-11-22), so new content still reaches players as a new bundle on a Play track; the game makes players notice it and installs it from inside the game.

## What changed

- `PlayUpdatePolicy` (runtime): offers an update only when Google Play reports one, resumes an update the player already started, and does nothing without an answer from Play (sideloaded build, offline, no Play services).
- `AndroidPlayUpdateSource` (runtime, Android only): Google Play In-App Updates `com.google.android.play:app-update:2.1.0` through Unity 6's `AndroidApplication`. Play's callbacks arrive on the Java thread; their values are read there and handed to Unity's main thread. "Jetzt aktualisieren" starts Play's immediate update (download, install, restart). The game already saves itself when Play pauses it (`ArenaRuntime.OnApplicationPause`).
- `PlayUpdatePrompt` (runtime): one check per start (`RuntimeInitializeOnLoadMethod`, Android player only), a panel in the style of the game's menus above HUD and menus, and a full-screen shade so nothing behind it reacts.
- `PlayAppUpdateGradleStep` (editor): adds the library to the generated `unityLibrary/build.gradle`; only active declarations count, a commented-out line does not; the build fails loudly when the dependencies block is missing.
- Launcher icon (editor): `ArpgBuildTools.ApplyAppIcon` sets Unity 6.6's adaptive icon (both layers) from `Assets/PrivateArt/Branding/AshenCryptIcon.png`. The image is rendered from licensed in-game art and stays with the private art; checkouts without it keep Unity's default icon.
- `StoreScreenshotCapture` (PlayMode, opt-in with `ASHEN_STORE_OUTPUT`): Google Play screenshots from the real chapter journey at 1920×1080, gameplay camera plus one staged view per area.

## Evidence

Unity 6000.6.8f1, Unity CLI runs in the live editor (not a Director job):

- EditMode `PlayUpdateTests` 3/3 (policy; panel buttons with a fake Play source; Gradle step incl. idempotence, other versions, commented-out lines, missing block, CRLF). The open scene stays clean.
- EditMode `AndroidPlayBundleTests` 6/6 including the launcher icon case.
- Panel checked in play mode with a simulated update.
- Store capture: both PlayMode capture tests produced all 12 frames (6 areas × 2 views).

Signed bundle built by `ArpgBuildTools.BuildAndroidAppBundle` from this branch:

- SHA-256 `34712b90fb9cf68f94e6cf1536e8130be65f6cff13312ce15876277d3d8e0e69`, 276,263,986 bytes, version code 28170992, targetSdk 36; `jarsigner` verified with the Ashen Crypt upload certificate (SHA-1 `ED:77:84:28:CF:42:EF:26:F5:50:10:4F:73:83:62:65:FD:E9:29:9F`).
- The generated `unityLibrary/build.gradle` declares `com.google.android.play:app-update:2.1.0`; `base/dex/classes.dex` contains `com/google/android/play/core/appupdate/AppUpdateManagerFactory` and `com/google/android/gms/tasks/OnSuccessListener`.
- No key password remained in the generated Gradle project; Gradle's compiled-script cache was cleaned by the private post-build tool (second search 0).
- Uploaded to Google Play as the draft release of the closed testing track (Play confirmed the same SHA-256). The first publication of a new app has to be sent for review in the Play Console by the owner.

## Not verified

The in-app update flow on a physical device needs a build installed from a Play track and a newer version on that track. It is **UNVERIFIED** here; the next bundle uploaded after this one is the first that can trigger the panel on testers' devices.
