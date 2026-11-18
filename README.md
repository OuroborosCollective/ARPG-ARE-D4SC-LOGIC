```markdown
# ARE / Ashen Crypt

**An original dark-fantasy action-RPG prototype built with Unity 6.** Explore the crypt, fight monsters, collect loot, and help shape the project.

> This is an independent prototype—not a Diablo IV replica, not an MMO, and not affiliated with Blizzard. It does not use Diablo IV/Fenris source code or proprietary game assets.

<p align="center">
  <a href="https://arelogic.space"><strong>🌐 Project website</strong></a> ·
  <a href="https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC"><strong>📦 Browse the source</strong></a> ·
  <a href="https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/stargazers"><strong>⭐ Star / vote for the project</strong></a> ·
  <a href="https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/fork"><strong>🍴 Fork and experiment</strong></a>
</p>

<p align="center">
  <img alt="Unity 6" src="https://img.shields.io/badge/Unity-6-black?logo=unity">
  <img alt="C sharp" src="https://img.shields.io/badge/language-C%23-512BD4">
  <img alt="Prototype" src="https://img.shields.io/badge/status-prototype-orange">
  <a href="https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/stargazers"><img alt="GitHub stars" src="https://img.shields.io/github/stars/OuroborosCollective/ARPG-ARE-D4SC-LOGIC?style=social"></a>
  <a href="https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/network/members"><img alt="GitHub forks" src="https://img.shields.io/github/forks/OuroborosCollective/ARPG-ARE-D4SC-LOGIC?style=social"></a>
</p>

## Help the project get noticed

- **Star the repository** to show your support and help others discover it.
- **Fork it** to explore the code and make your own non-commercial experiments.
- **Open an issue** to suggest a feature, report a reproducible problem, or share feedback:
  [Create an issue](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/new/choose).
- **Share the project website:** [arelogic.space](https://arelogic.space).
- Contributions and constructive feedback are welcome. Please open an issue before starting substantial changes so ideas can be discussed.

## What is implemented

- A standalone C# integer simulation shared by Unity and a .NET verification runner.
- A small single-player gameplay loop: movement, obstacle sliding, monster pursuit and attacks, combat actions, loot pickup, upgrades, death, and restart.
- A procedural demo arena, isometric follow camera, and mobile-friendly control zones.
- Persistent local character progression and mastery, repeatable crypt runs, and a second chapter prototype.
- Core regression tests and workflows for testing and opt-in Unity builds.

This is an evolving vertical slice. It is **not** a finished game or an online multiplayer/MORPG service. Unity Editor, Play Mode, and Android APK/device execution remain unverified; see [docs/EVIDENCE.md](docs/EVIDENCE.md).

## Try the core tests

You need the .NET SDK:

```bash
dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release
```

## Open in Unity

1. Open the repository root in **Unity 6000.6.8f1**.
2. Use **ARE ARPG > 1. Prepare Demo Scene** to create the demo scene.
3. Press **Play**. Move with **WASD / arrow keys**, use skills **1–4**, pick up loot with **E**, and restart with **R**.

Android builds require Unity Android modules and a valid Unity license.

## Contributing

Ideas, bug reports, documentation improvements, and code contributions are welcome. Start with [open issues](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues) or [create an issue](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/new/choose).

## Usage and permissions

The project owner permits **private, non-commercial experimentation and tinkering** and welcomes people who want to help improve the project. **Commercial use requires the owner's prior permission.** Contact the owner through [GitHub](https://github.com/OuroborosCollective) to discuss permission.

This repository currently has **no standard open-source license**. Public visibility alone does not grant permission to reuse, redistribute, or commercially exploit the code or other project materials. Permission for private tinkering is the owner's stated intent, but it is not a substitute for a reviewed license covering contributions and redistribution.

Check the terms of every asset and third-party dependency separately.

## Links

- Website: https://arelogic.space
- Repository: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC
- Issues: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues
- Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Verification: [docs/EVIDENCE.md](docs/EVIDENCE.md)
```