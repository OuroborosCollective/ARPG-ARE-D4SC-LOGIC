# ARE Ashen Crypt — Projektzustand

Stand: 2026-11-09T20:57:00Z. Repository: github.com/ouroboroscollective/arpg-are-d4sc-logic; PR #1 bleibt Draft.

- phase: VERTICAL_SLICE / CONTENT_INTEGRATION
- current_goal: Vorhandenen Slice mit den ausgewählten Unity-Account-Assets weiterentwickeln. Zuerst passive Gegnergrafik, danach passende Umgebung und geprüfte Inventar-/Skill-Adapter.
- approved: Eigenständiges Unity-Android-ARPG; Unity 6000.6.8f1 und offizielle Pipeline 0.10.0-exp.1; integerbasierter 100-ms-Core; Unity projiziert; bestehende AC1/AC2/AC3-Persistenz erhalten. Keine Aurion-Anbindung. Owner erlaubt am 09.11.2026 dauerhaft, seine ausgewählten Account-Pakete bei Bedarf ohne erneute Paketfreigabe zu installieren und zu integrieren.
- direction: Director führt technische Planung, Implementierung, Integration und Prüfung aus. Owner entscheidet über prägende Produktfragen, Lore und Art Direction anhand konkreter Alternativen. Die Paketfreigabe ist keine Kauf-, Lizenzzustimmungs- oder Releasefreigabe.
- open_decisions: Endgültige Art Direction und Lore-Erweiterungen bei konkretem Bedarf; physische Spielerabnahme steht aus.
- blocked: Reale Android-Geräteabnahme vor dem bestehenden Handoff-Mergegate weiterhin offen. Grafik-/Toolentwicklung wird deshalb nicht auf einen erneuten APK-Handoff verschoben.
- next: Skeleton-Ghost-Originalmodelle privat über die bestehende PassiveArtVisuals-Brücke als reine Gegnergrafik integrieren und in Unity prüfen; anschließend Umgebungsassets auswählen sowie Inventar und Skills gegen den bestehenden Core-/Save-Vertrag prüfen. Keine parallele Spielstandsautorität aus Paketdemos übernehmen.

## Account-Inhalte und aktuelle Arbeit

Im privaten Unity-6-Intake sind Monster04 (72 Prefabs, 72 PNG) und Skeleton Ghost (4 Prefabs, 19 PNG) tatsächlich importiert. Das ist keine Aussage über fertige Animationen oder Android-Leistung. Die Skelettdateien wurden zusätzlich in den gitignorierten Visual-Root des eigentlichen Spiels übernommen; ihre Physikkomponenten werden nur in separaten Grafik-Kopien entfernt. Vier separate passive Bindings Enemy0 bis Enemy3 wurden mit Unity 6000.6.8f1 erstellt und durch den unveränderten Binder geprüft. Je Vorlage wurden Rigidbody und Collider entfernt, lokale Modellhöhe auf 1,5 Einheiten und Füße auf den Boden der bisherigen Projektion gesetzt. Materialien und Shaderreferenzen bestanden die native Importprüfung. Alle 5 vorhandenen PlayMode-Tests bestanden mit dem privaten Overlay (0 Fehler/Skips). Eine sichtbare Art-Abnahme, abgestimmte Lauf-/Kampfanimationen und Android-Leistung bleiben ungeprüft.

Neun Pakete liegen im privaten Downloadcache, darunter Smart Inventory System, Skill System, 3D Game Kit, Map Tile Grid Creator und Runtime Performance Monitor Lite. Cache, Projektimport und funktionierende Gameplay-Integration sind unterschiedliche Zustände. Inventar-/Skill-Pakete sind hier noch nicht mit dem bestehenden Spielstand integriert. Rohassets bleiben außerhalb dieses öffentlichen Repositorys.

Der erste grafische Intake wurde beim Import durch sein eigenes 4-GB-Speicherlimit beendet. Die Dateien blieben erhalten; der Desktop wurde mit 6-GB-Limit wiederhergestellt. Ein sichtbarer alter Unity-2022-Intake ist kein Nachweis, dass unser Spiel mit 2022 entwickelt wird. Der aktuelle native Grafik-Import nutzt ausschließlich 6000.6.8f1. Alte Installation und Test-APK bleiben erhalten.

## Historischer Prüfabschnitt vom 09.11.2026, 18:47 UTC

Die folgenden Nachweise gelten für ihre ausdrücklich genannten Revisionen. Sie bestätigen nicht automatisch die laufende private Account-Integration.

## Abgeschlossener Schritt und Flächen

Der Director-Teststart am Ausgangshead `178275892d44557e74b6952fa94a15e183eac6e3` wurde real mit `UNTRACKED_GAME_INPUT:Assets/Scenes.meta` blockiert (Exit 2). Die vorhandenen, vom kanonischen `PrepareDemoScene` erzeugten Eingaben wurden geprüft und unter `455bb78079c9f851fec5ca0165c2e5ccd0f6e439` versioniert. Es wurden keine Dateien gelöscht und keine Guard-Ausnahmen hinzugefügt.

| Fläche | Betroffene Eingabe / Wirkung |
| --- | --- |
| Runtime-Projektion | `Assets/Scenes.meta`, `Assets/Scenes/Arena.unity` und deren Meta-Datei; bestehende GUIDs bleiben erhalten. Die leere Szene ist beabsichtigt: ArenaRuntime erzeugt Spielobjekte bei Play. |
| Produktion/Build | `ProjectSettings/EditorBuildSettings.asset` verweist aktiviert auf dieselbe Arena-GUID. |
| Core / Persistenz / Effekte | Keine Änderungen am C#-Core, Runtime-Adapter, Save-Vertrag oder Visual-Bindings. |
| Test | Bestehende 413 Core-Assertions, 21 Preflights und 5 echte PlayMode-Tests erneut ausgeführt. |

Andere Unity-generierte ProjectSettings bleiben außerhalb des versionierten Trees; die lokalen Director-Receipts erfassen ihre SHA-256-Werte. Das ist eine dokumentierte Hosteingabe, kein Nachweis vollständig identischer Einstellungen in jedem frischen Clone.

## Tatsächlich geprüfte Grenzen

| Grenze | Ergebnis und Revision | Quelle |
| --- | --- | --- |
| GitHub / VPS | Beide `455bb78079c9f851fec5ca0165c2e5ccd0f6e439`, Tree `8b57fc540141552518631220b0eec71c9b745269`; alle vier Dateien bytegleich. Tracked clean, untracked_game_inputs leer. | [Code-Commit](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/commit/455bb78079c9f851fec5ca0165c2e5ccd0f6e439) und unabhängiger VPS-Readback. |
| Statisch | 21/21 bestanden, Code-SHA oben. | Director `20261109T184337Z-75ed22c0b13f`; Log-SHA256 `49185373b169218058fa0d10a897e7988375f4fa29eb78afd81f8ba15aba9998`. |
| Core-CI | 413 Assertions + 21 Preflights bestanden; Run-head_sha = Code-SHA. PR-Checkout testet den Merge-SHA `093d6469a5ab5e7633e8deab273ea574d856b7f4` gegen main `259242996b9b9455b86040049b4ab76af4d9b37d`. | [Run 42676510462](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/42676510462), Attempt 1, Job 129359186830, Originaljoblog. |
| Unity-Editor | Unity 6000.6.8f1, 5/5 PlayMode bestanden, 0 Fehler/Skips; Ende 2026-11-09T18:45:05Z. Code-SHA oben. | Director `20261109T184435Z-6867860a60d4`; XML-SHA256 `083e26e28a3ee1ce0020aa2223c4b7020ea68aaba04a1596d60d0917c81caf99`, Unity-Log `c396c13b0e72636fef383d6b696bb519db3648e8af8bab5a67058b6fea5d4e96`. XML, Receipt und Dateien separat gelesen. |
| GitHub Unity | Run 42676510454 übersprungen; kein PASS. | [Run](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/42676510454). Der echte Editorlauf oben ist ein separater lizenzierter VPS-Lauf. |
| Vorhandenes APK / Signatur | Bestehendes ARM64/IL2CPP-APK erneut physisch geprüft: 16.346.460 Bytes, SHA256 `d535d9bc574c09a1a07fbac359acad47ca6f2af9720457b3ccf8fbbaced29a6e`; Debugzertifikat `7b6c7bffa6cc5ff397b317fe10169ab9ba56a2440e90fa19d13308f490377f4a`. | Historischer Build auf `91575837c560efaf4068a188631ab886ed391c8d`; private Buildbelege separat erhalten. Dateiprüfung allein liefert source_commit_sha=null. Kein neuer APK-Build für den Szenencommit und kein Release Candidate behauptet. |
| Gerät / Release | Ungeprüft; ADB-Liste leer. | Keine Tablet-Installation, Touch-/Kamera-Abnahme, Android-Save/Restore oder Store-Promotion aus diesem Arbeitsblock. |

Der eigene MCP-Editor wurde für den Test gezielt beendet und anschließend wieder gestartet (ActiveState active, SubState running). Keine zweite Editorinstanz wurde gestartet. Die separate Android-APK und Unity2022 bleiben erhalten. Headless-Tests liefern keinen visuellen Android- oder Leistungsnachweis.

Diese Datei ist der datierte Produktionszustand. Ältere Verifikationsaussagen in README und Memory sind historische Beobachtungen; der vollständige Dokumentationshead und dessen finale CI werden im privaten Übergabeprotokoll separat festgehalten.

## Private Grafik-Integration — geprüfte Grenze

Source: `8e4cfc6a1d27fe79760636b001c33fe10bad1b41`, dazu privates Overlaymanifest SHA256 `2aa5cf2c3b0490ff80dad8a56be041c64c17e7e9c7bf7072df5c8ec622ddbbc5`. Alle getrackten Source-Dateien waren beim nativen Test unverändert. Unity-PlayMode Ende 2026-11-09T20:55:46Z: 5 bestanden, 0 fehlgeschlagen, 0 übersprungen; XML SHA256 `60f07f12a41481307282bd8f97604a2edba1835b28a58217fc84d9a04a60bf7f`. Private Rezepte, Prefabs und Rohassets sind nicht Teil des öffentlichen Git-Trees. Die drei neuen Ordner-Meta-Dateien halten die GUIDs der vorhandenen Visual-Brücke stabil.

Smart Inventory enthält eigene JSON-/PlayerPrefs-Persistenz sowie automatische UI-Initialisierung. Diese Grenzen wurden im Quelltext gefunden; daraus folgt noch keine kompatible Integration. Der nächste Grafikschritt ist eine echte gerenderte Spielansicht mit den Skeletten und die Auswahl passender vorhandener Animationen/Umgebung. Die Android-Geräteabnahme bleibt ein späterer separater Handoff.
