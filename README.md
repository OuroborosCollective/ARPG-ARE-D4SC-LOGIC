# ARE / Ashen Crypt

**An original dark-fantasy action-RPG vertical slice for Unity.** Not a copy of Blizzard source code, proprietary assets, or the Diablo IV/Fenris engine.

This repository starts from the design ideas in the user-provided `bbbgai_studio_code (1).txt` experiment. Rather than presenting a giant untested engine reconstruction as complete, it implements the smallest accountable gameplay loop: explore, fight, survive, pick up loot, upgrade, clear the crypt.

## What's actually implemented

- Standalone C# integer simulation shared between Unity and the .NET verification runner.
- 100 ms fixed simulation steps with independently interpolated Unity presentation. This is a local prototype choice, **not** a connection to the Aurion server.
- Movement and obstacle sliding; pursuit and attacks from two monsters plus a boss.
- Four actions: Strike, Heavy Strike, Guard/Heal, Ruin Storm. Real cooldown and resource checks.
- Monster death, one-time ground loot, explicit pickup (E / PICK UP), weapon bonus, player death and restart.
- Procedural Unity primitive arena, basic colored materials, lighting, isometric follow camera and mobile-friendly control zones.
- A separate C# regression runner, plus workflows for core tests and an opt-in licensed Unity Android build.

## How to run in Unity

1. Open this repository's root folder as a project in **Unity 2022.3.62f1** (Android Build Support + IL2CPP + SDK/NDK only needed for APK builds).
2. Wait for source import. Use **ARE ARPG > 1. Prepare Demo Scene** to generate `Assets/Scenes/Arena.unity` and add it to Build Settings.
3. Press **Play**. `ArenaRuntime` creates the demo world on loading an empty scene; nothing is imported from proprietary games.
4. Move with **WASD/arrows**, attack with **1–4**, collect dropped loot using **E**, restart with **R**. On Android use bottom-left movement area and four bottom-right skills.
5. Select **ARE ARPG > 2. Build Android APK** (with Android modules installed) to produce `Builds/ARE-Ashen-Crypt.apk`. This command fails if Unity reports a failed build.

> No one has yet executed the Unity Editor, Play Mode, or Android build for this repository. **The presence of build instructions or CI workflow is not evidence of a successful APK.**

## Core tests (no Unity license required)

```bash
dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release
```

The test runner links the **actual `Assets/Scripts/Core/ArpgSimulation.cs`** used by Unity (not a parallel mock engine). It checks integer math against a Wolfram-derived reference, per-tick replay hashes, movement, collisions, combat, loot, cooldowns, monster pursuit and defeat. The CI workflow reports execution evidence on the commit.

## Scope / roadmap

This is **not** a Diablo IV replica and **not** an MMO. Missing: animations, art, music, quests, inventory UI, persistent saves, class-specific trees, dungeons, production netcode, multiplayer, professional PBR assets, true Unity Editor runtime verification and a signed/install-tested APK. Start with correctness and feel, then expand system-by-system; never declare mocked/synthetic tests to be live runtime proof.

For the authority boundary and verification status, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/EVIDENCE.md](docs/EVIDENCE.md).

## Development controlled from an Android tablet

The owner does not need a Unity Editor installed on Android. Development is done through GitHub changes and chat instructions; the owner reviews gameplay changes and eventually installs build artifacts on their tablet. To keep this honest, three checks are separate:

1. **Core** – automatic .NET tests on pull requests, including real state and replay checks.
2. **Unity Play Mode** – run the `Unity PlayMode smoke (licensed runner)` workflow after provisioning a Unity-compatible license; it creates actual Unity objects and checks hero/boss/camera.
3. **Android APK/device** – trigger `Unity Android build (manual, Unity license required)`, inspect the APK artifact and checksum, and install/test on Android. Neither step is currently verified.

The next creative decisions can come through chat: describe location, combat feeling, enemy behavior, mobile HUD or reference media, and we implement them as reviewable source changes.