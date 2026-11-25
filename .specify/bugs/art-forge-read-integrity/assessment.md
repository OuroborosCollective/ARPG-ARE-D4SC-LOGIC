# Art Forge: Lesebudget, Editor-Responsivität und Revisionskonflikte

Bezug: Issue #132, Draft-PR #137. Ausgangspunkt ist der veröffentlichte
Head `0f1720fdf59698bd4988b92cc59be9daadbc45ee`, dessen Git-Baum dem lokal
freigegebenen `ef4a270e94b28271388f5ff177faebaa0a284673` entspricht.

## Verifizierte Defekte

- Der bestehende Fadenwerk-Dispatcher läuft auf dem Unity-Hauptthread.
  Der bisherige Art-Handler startet dort vier Git-Prozesse und hasht den
  gesamten begrenzten Katalog bei jedem erfolgreichen Abruf.
- Der Receipt-Pfad benutzt nach der Größenprüfung eine zweite Stream-Länge
  für die Allokation. Gleichzeitiges Wachstum oder Schrumpfen kann die
  geprüfte Reservierung umgehen. Der Artefakt-Reader konnte außerdem mehr
  als die je Datei reservierten Bytes lesen, bevor sein Hashfehler griff.
- Eine verspätete `409`-Antwort für Knoten A wurde bei inzwischen ausgewähltem
  Knoten B ignoriert. Ein anschließendes `503` für B ließ den veralteten Graphen
  stehen. Antworten einer früheren Reload-Epoche müssen dagegen ignoriert bleiben.
- Der Test für gleichlange Hash-Drift verwendete 18 und 17 Bytes. Damit belegte
  er nur Größenabweichung, nicht die benannte Byte-Hash-Grenze.

## Bereits ausgeführtes RED

Native Prüfrevision `d4ccaf6118a8c16f914b6ae962432d5f8917df8c`, Director-Job
`20261124T161321Z-635878a4be06`, eigene unbenutzte Unity-Projektkopie:
339 Tests, 273 PASS / 14 FAIL / 52 SKIP. Davon ArtForge 49 PASS / 8 FAIL / 0 SKIP.
Die vier Pending-/Dispatcher-Regressionen sehen `200` statt `202`; zwei
Bytebudgetfälle lesen 128 statt reservierter 64 Bytes; Schrumpfen wird vom
Receipt-Bytepfad akzeptiert; die Gleichlängen-Vorbedingung sieht 18 statt 17.
Die weiteren sechs Fehler und sämtliche 52 Skips entsprechen der bekannten
Asset-Baseline. Director meldet weiterhin die von Unity neu serialisierten
ProjectSettings als `TRACKED_WORKTREE_DIRTY`.

Die Bytebudget-Tests benutzen eine dokumentierte verhaltensneutrale Extraktion
der bisherigen produktiven Stream-Pfade. Ihr kontrolliertes Race-Harness
verändert echte temporäre Dateien an einer definierten Längenbeobachtung;
Dateibytes und SHA-Berechnung werden nicht ersetzt. Das belegt die Reader-Grenze,
keine in Produktion beobachtete gleichzeitige Dateimanipulation.

Browser-RED mit echtem Headless Chromium: sechs Fehler bei den beiden
409-/503-Reihenfolgen auf drei Viewports; sechs Kontrollfälle bleiben grün.
Zusätzlicher Pending-Protokoll-RED: zehn von 13 Fällen scheitern am fehlenden
Polling. Diese Browserfälle verwenden ausdrücklich synthetische API-Antworten.

## Korrekturvertrag und Abnahme

1. Bestehende Extension, Authentifizierung und World-Forge-Quelle erhalten.
2. Git und Byteprüfung über begrenzte Hintergrundoperationen ausführen;
   Pending-/Ticket-Protokoll gemäß `docs/specs/ashen-art-forge-v1.md`.
3. Jede neue Anfrage prüft echte Bytes; kein Receipt-/mtime-Cache ersetzt SHA-256.
4. Receipt-Allokation und Artefaktreads überschreiten ihre Reservierung nicht;
   Wachstum, Schrumpfen und Abbruch werden explizit behandelt.
5. Dieselben RED-Assertions müssen nach Korrektur bestehen. Der Gleichlängenfall
   verändert genau ein Byte bei gleicher Größe und wiederhergestellter mtime.
6. Native Editor-Updates, reale Git-/World-Forge-Abrufe, CoreVerification,
   vorhandene Preflights und Browsergrenzen ausführen und SHA-gebunden ausweisen.
7. Keine Core-, Save-, Tick-, aktive Editor-, Import-, Freigabe- oder Cloudmutation.

Die tatsächlichen GREEN-Ergebnisse und Grenzen werden nach Ausführung im
Review-Nachtrag zu `docs/evidence/art-forge-a-20261124/` festgehalten.
