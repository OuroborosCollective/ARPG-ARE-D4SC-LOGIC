---
name: ugd-quality-lab
description: Nutze bei Unity 6 und Ashen Crypt, wenn Unity-EditMode/PlayMode, Core Replay, Graphics Regression und Android-Performance belegen.
---

# Quality Lab

**Aufgabe:** Unity-EditMode/PlayMode, Core Replay, Graphics Regression und Android-Performance belegen.

L0 Source/Compile, L1 Core, L2 EditMode, L3 PlayMode, L4 Graphics, L5 Performance, L6 Build, L7 Android Automation, L8 Real Device separat. Framework-Kandidaten Graphics Test Framework, Performance Testing, AltTester vor Installation gegen Pin und Lizenz pruefen. Bench p50/p95, GC, CPU/GPU/RAM, Testanzahl, Nulltests und Baseline-SHA dokumentieren. Skips/QUEUED sind nicht PASS. Geraetetests ersetzen keine Desktop-Simulation.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
