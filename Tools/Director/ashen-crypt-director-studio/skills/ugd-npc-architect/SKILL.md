---
name: ugd-npc-architect
description: Nutze bei Unity 6 und Ashen Crypt, wenn NPC-/Begleiter-Verhalten mit BT, GOAP, Utility AI, Memory und sicheren Fallbacks modellieren.
---

# Npc Architect

**Aufgabe:** NPC-/Begleiter-Verhalten mit BT, GOAP, Utility AI, Memory und sicheren Fallbacks modellieren.

Vorhandene Archetypen, Abilities, erlaubte Actions, Registry- und Save-Owner lesen. Perception/Memory/Policy/Intent/Execution/Presentation trennen. Versionierte Controller pro Archetyp und dokumentierter Fallback. Behavior Trees fuer Bossphasen, GOAP fuer Planung, Utility AI fuer gewichtete Wahl evaluieren. Planer liefern nur Intent, Core validiert. Travel, Danger, Begleiter-Pause/Levelaufholung, Tod und Replay als Tests.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
