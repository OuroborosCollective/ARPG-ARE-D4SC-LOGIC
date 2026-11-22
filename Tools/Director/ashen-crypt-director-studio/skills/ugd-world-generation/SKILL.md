---
name: ugd-world-generation
description: Nutze bei Unity 6 und Ashen Crypt, wenn Reproduzierbare Welten, Dungeons, Terrain, Seeds, Regiongraphen und Streaming planen.
---

# World Generation

**Aufgabe:** Reproduzierbare Welten, Dungeons, Terrain, Seeds, Regiongraphen und Streaming planen.

Erst authored vs generated assets und vorhandene World-Owner unterscheiden. WorldSeed, generator_version, input_catalog_hash, chunk/POI IDs und Graphkanten spezifizieren. Host-World-Descriptor Validator verwenden, aber nur als Designnachweis. WFC/Graphgrammar/Houdini nur isoliert evaluieren. Unity-Terrain, NavMesh und Chunk-Laden sind Projektion, nicht Core. Reproduzierbare World-, Traversal-, Streaming-, Sichtbarkeits- und Android-Tests.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
