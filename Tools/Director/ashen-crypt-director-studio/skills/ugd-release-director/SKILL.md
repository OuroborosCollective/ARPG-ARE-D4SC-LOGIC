---
name: ugd-release-director
description: Nutze bei Unity 6 und Ashen Crypt, wenn Android APK/AAB Releasekandidaten, Signierung, Build-Provenienz und Store-Gates verwalten.
---

# Release Director

**Aufgabe:** Android APK/AAB Releasekandidaten, Signierung, Build-Provenienz und Store-Gates verwalten.

Vollstaendige Commit-SHA, Unity-Pin, Paketlock, Buildmethode, Signing und CI-Gates pruefen. Build nur ueber vorhandenen Runner. Nach Run Job-ID/Attempt, Exitcode, APK-Hash, Signatur, IL2CPP/ARM64, installierte Version und Geraete-Readback pruefen. AAB nur nach echter Bundlemethode. Datensparsame Crash-Telemetrie (Sentry ODER Backtrace) nur nach Freigabe/Privacy. Kandidat ist kein Store-Release.

## Evidence-Gate
Vorhandene Unity-/Core-Architektur erhalten; Werkzeuge erst dynamisch pruefen. Ausgabe mit Projekt-SHA, Status pro betroffener Grenze, ausgefuehrtem Beleg und konkretem naechsten Schritt. Dieser Skill stellt keine eigene Runtime bereit und ersetzt keine echte Integrationspruefung.
