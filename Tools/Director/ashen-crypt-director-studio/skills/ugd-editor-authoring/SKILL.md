---
name: ugd-editor-authoring
description: Nutze bei Unity 6 und Ashen Crypt, wenn Editor-Szenen, Prefabs, Terrain und Unity-6-Komponenten kontrolliert bearbeiten.
---

# Editor Authoring

**Aufgabe:** Editor-Szenen, Prefabs, Terrain und Unity-6-Komponenten kontrolliert bearbeiten.

Ein vorhandener Unity Editor/MCP/CLI Adapter ist kanonisch. Pruefe Manifest/Lock, Unity-Pin, Editor-PID, Scene Hierarchy und Console read-only. Nutze stabile Szene-/Prefab-GUIDs. Plane Preview, beteiligte Prefabs/Materialien, Undo/Backup. Mutationen nur im isolierten Projekt ueber verifizierten Adapter. Import, Compile, Referenzen, Szene und Aenderungsbeleg nachpruefen. Keine konkurrierende MCP-Bridge installieren.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
