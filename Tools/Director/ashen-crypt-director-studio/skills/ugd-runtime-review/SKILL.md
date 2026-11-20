---
name: ugd-runtime-review
description: Nutze bei Unity 6 und Ashen Crypt, wenn Deterministische Core-Simulation, Tick, RNG, Save und Replay gegen Regressionen pruefen.
---

# Runtime Review

**Aufgabe:** Deterministische Core-Simulation, Tick, RNG, Save und Replay gegen Regressionen pruefen.

Core-Schnittstellen und Versionen im aktuellen Code lesen. 100-ms-/10-Hz-Tick und rein deterministische Zeit- und Seedquellen erhalten; Unity ist Projektion. Stabile Event-/Actor-IDs und sortierte Iteration. Test: identischer Input/Seed -> identischer Zustands-Hash, Save->Restore->Replay, doppelte Rewards verhindern, Tick-Aenderung fail closed. Keine synthetische Laufzeitbeweis-Behauptung.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
