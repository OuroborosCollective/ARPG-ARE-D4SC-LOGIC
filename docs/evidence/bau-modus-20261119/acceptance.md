# Bau-Modus – Abnahme 19.11.2026

Basis `main` `488567f`; Code-Commit `6bbf539` (Branch `feat/bau-modus-20261119`). Native Prüfung im Live-Workspace B
(Unity 6000.6.8f1, Software-GL) nach Abgleich von B auf `main` + Bau-Modus; Python/Core im Source-Workspace A.

| Prüfung | Ergebnis |
| --- | --- |
| Kompilierung im offenen Editor | fehlerfrei |
| EditMode `BuildLayoutEditModeTests` | 8/8 |
| EditMode gesamt | 126/126 |
| PlayMode `BauModusPlanReachesTheRunningVillageLive` | 1/1 (39,9 s) |
| PlayMode gesamt | 171/171 (2180 s) |
| Core (`Tests/CoreVerification`) | 21 239 Assertions PASS (Core-Diff leer) |
| Preflight (`Tests/Preflight`) + `unity_contract.py preflight` | 40/40, PASS |
| Gateway-Regeln (`Tests/FadenwerkGateway`) | 5/5 (neu: Bau-Modus-Routen); Repo-Gateway = installierter Gateway |
| Browser-E2E (Playwright, echter Editor-Server) | Haus, Baum, Bewohner, Pflasterfläche, Wald (14), Verschieben, Rückgängig, Echtbild; 0 Seitenfehler; jede Antwort „in Unity sichtbar ✓“ |
| Readback Editor | 1 Vorschau-Wurzel (DontSave), 18 Teile, 0 aktive Collider, Szene nicht „geändert“ |
| Live ins laufende Spiel (HTTP, Play-Modus, Krypta) | 4 Teile nacheinander, Antwort jeweils `mode=play, shown=true, inUnity=1…4`, Spielkamera zeigt Säulen und Kiste |
| Öffentlicher Link ohne Anmeldung | 401 (Seite und API) |

Private Belege (Screenshots aus echten Renders, Demo-Pläne, E2E-Skript) liegen nur auf dem VPS unter
`/var/lib/unityarpg/.local/share/are-arpg/bau-modus-20261119/`:

```
b1f3f22f72ddb2c6b542e8a18e5f2850a657f1f7cd9db05aef5046d09fb7224e  e2e-screens/02-uebersicht.png
2135173a83d586d2f5d743c9b21b3503aebfd84f7ed0056bf85454b20b6ee26d  e2e-screens/06-gebaut.png
e131847b52ca64b1282e01e77f0a312108ce2675240b0c99336b7b06f3f1dcf5  e2e-screens/07-echtbild.png
44acd44a93a45d09d80660ff9f3cd0da351fd1f94af5f7a49aa0a0de80f4b986  game-view-krypta.png
43470b56eb3dbddf17d9427b335a0fd109a664ed15076bd269460ce75292fb70  scene-view.png
ecd9106673d849bff4c3fa3c0cddf72dd791aada027b8ebcda8ddd236782ce26  e2e-screens/summary.json
```

Nebenwirkungen und Aufräumen: Der Play-Modus-Check hat den Editor-Spielstand (Niederlage in der Krypta) verändert;
beide Entry-Schlüssel wurden aus der vorherigen Sicherung zurückgeschrieben. Demo-Pläne wurden aus B entfernt
(Kopien privat). In B ist jetzt `AshenEntry.unity` geöffnet (vorher eine unbenannte leere Szene). B's
Fadenwerk-Zugangsschlüssel entspricht jetzt der Schlüsseldatei des vorhandenen Gateways.

Nicht geprüft: Android/APK, Tablet, GitHub-CI für diesen Commit (nicht gepusht), Gebiete außer Aschengrenze und
Krypta im Play-Modus.
