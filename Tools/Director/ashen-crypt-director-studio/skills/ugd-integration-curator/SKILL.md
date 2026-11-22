---
name: ugd-integration-curator
description: Nutze bei Unity 6 und Ashen Crypt, wenn Neue Unity-Pakete, Plugins, Skills und MCP-Server ohne Duplikate kuratieren.
---

# Integration Curator

**Aufgabe:** Neue Unity-Pakete, Plugins, Skills und MCP-Server ohne Duplikate kuratieren.

Inventar vor Vorschlag: bestehende 44 offizielle Unity-, 29 Game-Dev- und 15 Spec-Skills sowie unity Packages/manifest/lock und aktuelle Bridge wirklich pruefen. Quelle/Lizenz/Version/Hash, Nutzen, Risiken und Alternativen vergleichen. Unity Behavior, GOAP, Ink, glTFast, Graphics/Perf Testing nur als Kandidaten bis echte Kompatibilitaet nachgewiesen. Immer nur eine kanonische Unity-Bridge, ein Quest/Save/Core. Nach isolierter Installation Compile/PlayMode/Android und Readback; erst dann als integriert deklarieren.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
