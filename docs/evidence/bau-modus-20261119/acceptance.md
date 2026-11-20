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

## Integrationsabnahme vom 20.11.2026

Geprüfter Code `40eb8c7ecac79db1cbfa457734cfa3bfa2cbbe76`, Tree `f42a3d75f0d51fb1d2f966e7bdb5820d42a246c5`. Eingebundener Hauptbranch `14e3b31b87b38476a38eb814c4be6cda1b551740` enthält #93, #95 und #96. Unity 6000.6.8f1 lief auf dem VPS.

| Prüfung | Beobachtung | SHA-256 des Belegs |
| --- | --- | --- |
| EditMode-RED auf `b801dc91d52d4b3917645f6a2c15ea23308d31cb` | Veraltetes Undo erhielt 200, erwartet war 409. | `00d3921997197cb16ce1bfa3443330e43c3aef3a32753234d607847b80194dd8` |
| EditMode ohne BoZo-Paket | 15/15, keine Skips. Bauplan, veralteter Undo-Aufruf, extern ersetzte Datei gleicher Revision, vier passive Ersatzfiguren und zwei Fadenwerk-Erweiterungstests. | `5695c7db1d51330401b65f0cb61bf3560d4df616e0a9a27b469172c2fa47dd7c` |
| PlayMode | 1/1. Der Bauplan erreicht das laufende Dorf. | `4ec1ec0efac24b3ec764ff621c03f0bbebea870e6eead50f7d5f3c0e59314fca` |
| Private BoZo-Figuren in Unity | 4/4 ohne Skips. Villager0, Villager1, Villager2 und Merchant0 luden jeweils ihr gebackenes Prefab. | `87bdf475c61166cd6eba4d750bd12c7f7469c1c8b7c7dac703ccffac53c3973f` |
| Unabhängige private Projektion über `BuildLayer.CreatePiece` | Ursprüngliches Mesh und verfasstes Material je Figur anhand der Asset-Referenzen und GUIDs bestätigt. Kein Collider oder MonoBehaviour an den erzeugten Figuren. | Manifest `fa149cc53cdebace63ced6a6efbb4a193390f13105c781984d588729fec2b91a` |
| Chromium/Playwright am echten Fadenwerk-Port | Zweiter Bearbeiter speicherte Revision 2. Klick des ersten mit Revision 1 erhielt HTTP 409 und zeigte beide Teile. Zweiter Klick mit Revision 2 erhielt 200. Die Datei auf Platte enthielt danach Revision 3 und nur das ursprüngliche Haus. | Ablauf `183316a6bd4b1ed62364f6dce746284a0309d0a593fa6136722c0fa0468fd022`, Bauplan `f980160376fc8972d8e456f96c69709b92741fec4af6158a457d24b8b3a9fdbc` |
| Geteilter C#-Core | 21 239 Assertions bestanden. Preflight 48/48, Gateway 5/5 und statischer Unity-Vertrag separat bestanden. | Core `bafba6f747bd3486a576eef3fc8dac473b21978497177819f63d499b5c3fa99e` |

[Core-CI 44704743520](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44704743520) war erfolgreich. [Lizenzierter Unity-CI-Lauf 44704743481](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44704743481) wurde übersprungen. Die Unity-Ergebnisse in der Tabelle stammen aus den separaten lokalen Läufen. Im Browsertest wurde die periodische Aktualisierung eines Clients verzögert, um den konkurrierenden Benutzerablauf reproduzierbar auszulösen. Der Server lief im isolierten Projekt auf Loopback-Port 18997. Testpläne und Server wurden anschließend entfernt bzw. beendet. Rohbelege liegen auf dem VPS in `~unityarpg/.local/share/are-arpg/pr97-reruns/` und `pr97-private-residents/`. Android/APK und Store-Upload wurden in diesem Durchlauf nicht geprüft.
