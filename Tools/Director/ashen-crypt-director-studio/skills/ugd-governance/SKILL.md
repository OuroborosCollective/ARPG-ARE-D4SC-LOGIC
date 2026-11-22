---
name: ugd-governance
description: Nutze bei Unity 6 und Ashen Crypt, wenn Aenderungen und Release-Freigaben am Unity-Projekt mit SHA-Bindung, Dry-Run und Rueckbaupruefung steuern.
---

# Governance

**Aufgabe:** Aenderungen und Release-Freigaben am Unity-Projekt mit SHA-Bindung, Dry-Run und Rueckbaupruefung steuern.

Vor jeder Mutation aktuellen vollstaendigen SHA, Editor-PID, Dirty Paths, Eigentuemermodule und Risikooberflaechen lesen. Plane einen kleinen reversiblen Slice. Trenne Read-only, reversible Write und destructive Actions. Nutze bestehende Genehmigung nur fuer die konkret beauftragte Aenderung; fuehre keine OpenAI-Rollen- oder Kostenupdates aus. Nach Mutation Diff, SHA, Tests und echten Readback pruefen. Kein gruenes Testergebnis aus Mock/Skip.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
