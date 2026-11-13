# Memory Forge

Deterministic NPC behavior, individual memories and relationships for Unity 6.

Memory Forge is a source-code toolkit for games that already own their world, actions and simulation clock. It proposes an allowed action and explains its score. Your game validates perception and executes the action. There is no cloud service, language model, background learning loop or second simulation clock in the library.

Start with **Tools → Memory Forge → Open remembering-village example**, then press Play. Approach Nara, give her food, leave and return. Save, reset the scenario, then load: her memory and changed greeting return. Try taking the purse with the watch behind the wall, then repeat with the watch in view. These are real visibility checks, resource transfers and remembered events in the example host.

- [Quick start](Documentation/Quickstart.md)
- [Integration and API](Documentation/Integration.md)
- [Controllers and local neural policies](Documentation/Controllers.md)
- [Persistence, performance and limits](Documentation/Persistence-and-performance.md)
- [Deutscher Einstieg](Documentation/Schnellstart-DE.md)
- [License and third-party notices](LICENSE.md)
- [Changes](CHANGELOG.md)

Package layout: `Core` is pure C#; `Unity` contains configuration assets and a read-only NPC view; `Editor` contains authoring tools; `Samples` contains the original example host and scene; `Tests` contains optional Unity Test Framework tests. The sample is separate from your gameplay. No Ashen Crypt game assets, private licensed models, animations, save files or game code are included.

Unity editor baseline: **6000.6.8f1**. See the accompanying release evidence for executed checks and platform limitations. Use a source-control branch or project backup before any integration.
