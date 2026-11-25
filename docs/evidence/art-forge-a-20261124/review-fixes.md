# Art Forge A — Reviewkorrekturen zu PR #137

Stand: 24. November 2026, Europe/Berlin. Dieser Nachtrag ergänzt den unveränderten [ursprünglichen Prüfbericht](acceptance.md). Dessen Angaben zu 49 Tests und dem damaligen Stand vor Veröffentlichung sind historische Evidence. Der aktuelle native Implementierungsstand ist `5b4602f466352f57de4611eaa907530203299451`.

## Ergebnis und Umfang

Die drei konkreten Reviewbefunde sind korrigiert: Datei-/Git-Lesezugriffe blockieren den Art-Dispatcher nicht mehr synchron, tatsächlich gelesene Bytes bleiben innerhalb der geprüften Reservierung, und eine verspätete `409` für die noch angezeigte Graphrevision wird auch nach einem Auswahlwechsel wirksam. Zusätzlich prüft die Hash-Drift-Regression jetzt tatsächlich gleich lange Dateien bei unverändertem Receipt und wiederhergestellter Änderungszeit.

Die bestehende Bau-/Fadenwerk-Extension, Authentifizierung und World-Forge-Quelle bleiben die Integrationsgrenzen. `ArtGraphContract.cs`, Core-, Save- und Tick-Quellen sind gegenüber dem bereits freigegebenen Stand unverändert. Es gibt keinen Import, keine Freigabe und keinen neuen Listener. Unity Studio bleibt ein [separater optionaler Entwurf](../../specs/ashen-art-forge-studio-option.md).

**60 native ArtForge-Tests bestehen. Die vollständige native Suite bleibt fehlgeschlagen:** 284 PASS, 6 FAIL, 52 SKIP; Unity Exit 2 und Director `TRACKED_WORKTREE_DIRTY`. Diese Befunde werden nicht durch die erfolgreichen Teilsuiten ersetzt.

## Revision und Veröffentlichung

| Stufe | Tatsächlich lokal geprüfter Commit | Entsprechendes GitHub-Commitobjekt | Identischer Git-Baum |
| --- | --- | --- | --- |
| Freigegebener Ausgangspunkt | `ef4a270e94b28271388f5ff177faebaa0a284673` | `0f1720fdf59698bd4988b92cc59be9daadbc45ee` | `6f2d510d776fae457b21e65b04ae7d1366e3104c` |
| Native RED-Regressionen und Browser-Race-Fix | `d4ccaf6118a8c16f914b6ae962432d5f8917df8c` | `a90aa9ad052ad0caefe9d5df33f71efd0a233e22` | `997c09f95b139e0aceb302ffc6cd4e39d422edf9` |
| Native GREEN-Implementierung | `5b4602f466352f57de4611eaa907530203299451` | `0488e5c4c0406bd041a014a6563288a257fe8d0a` | `004ff051687fc1cc004afff67184d57c62619d1d` |

Die GitHub-Git-Data-Schnittstelle erzeugt neue Commit-Metadaten. Die Git-Bäume wurden vor dem Bewegen der Feature-Branch exakt verglichen und die Commitobjekte unabhängig zurückgelesen. Die originalen Unity-Belege behalten ihre tatsächlich ausgeführte lokale Quellrevision. Dieser Nachtrag, der maschinenlesbare [Review-Readback](review-readback.json), Memory und Übergabe folgen als reine Dokumentationsänderung. Ein späterer Lauf auf einem anderen Commit wird daraus nicht behauptet.

Die bestehende Branch ist `feat/ashen-art-forge-a-readonly`, der Draft-PR [#137](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/137). Die Fortschreibung bleibt auf dieser Feature-Branch. GitHub-main war beim Vorab-Readback weiter `489a3120915a5fdcec4bae83dc1f9afac6a2aa22`; paralleler PR #136 weiter `667d6777e97cd66a94def5227910f954fdae71cc`. Seine gemeinsamen Anfügeflächen in Memory und Übergabe bleiben erhalten. Der Merge ist nicht Bestandteil dieses Nachtrags.

## Konkrete Korrekturen

### Hintergrundprüfung im vorhandenen Dispatcher

`BauModusBridge.Handle` ruft weiterhin denselben `ArtForgeCanvasController` auf. Der Controller prüft Methode, Route, Revisionen und Editorzustand auf dem Hauptthread. Die echten Git-Aufrufe und der bestehende World-Forge-Reader laufen anschließend über `Task.Run`; eine Semaphore lässt jeweils nur einen Reader arbeiten. Höchstens acht Prüfvorgänge sind offen.

Der Start liefert `202 ART_SCAN_PENDING` mit einer zufälligen, nicht kanonischen `scanId`. Der Browser fragt dieselbe Route mit denselben Revisionen und derselben Asset-ID weiter ab. Ein Ticket kann nicht auf eine andere Anfrage umgebunden werden. Eine terminale Antwort verbraucht es einmal; ein weiterer Abruf endet mit `410`. Ein neuer Start liest und hasht die tatsächlichen Dateien erneut. Ein Cache nach Receipt-Metadaten würde Hash-Drift verdecken und wurde nicht eingeführt.

Der Worker hat einen kooperativen Abbruch nach 30 Sekunden; Tickets werden nach 60 Sekunden beim nächsten gültigen Abruf entfernt. Assembly-Reload und Editorende lösen ebenfalls Abbruch aus. Einzelne Git-Prozesse haben ein Fünf-Sekunden-Limit und werden im Worker abbrechbar abgewartet. Diese Grenzwerte und Lifecycle-Hooks sind implementiert; separate native Zeitablauf- oder Domain-Reload-Prüfungen dafür wurden in dieser Runde nicht ausgeführt. Die vier neuen nativen Dispatcherfälle prüfen Pending-Antwort, echte Editorupdates, Routen-/Query-Bindung, Einmalverbrauch, neue Byteprüfung und Kapazitätsgrenze.

Die Browseroberfläche pollt ausschließlich den erwarteten Pending-Vertrag. Sie begrenzt das Intervall auf 100–1.000 ms, den gesamten Abruf auf 40 Sekunden und auf 150 Polls zusätzlich zum Start. Auch ein unbeantworteter Fetch wird abgebrochen. Endfehler starten keinen Ersatzscan. Ein Auswahlwechsel innerhalb desselben Graphen lässt ältere Antworten weiter beobachten, damit deren `409` den veralteten Graphen noch verwerfen kann; Antworten einer älteren Reload-Epoche werden ignoriert.

### Bytebudget und Hash-Drift

Die erste geprüfte Stream-Länge bestimmt Receipt-Allokation und reservierte Bytes. Chunkweises Lesen bleibt innerhalb dieser Reservierung. Ein begrenzender Stream schützt denselben Pfad bei Artefaktprüfungen; Größen-, Positions-, Änderungszeit- und SHA-256-Prüfungen bleiben erhalten. Wachstum, Schrumpfen und Cancellation werden abgewiesen beziehungsweise weitergereicht. Cancellation wird nicht in einen scheinbar erfolgreichen leeren Diagnosesnapshot umgewandelt.

Die Budgetfälle verwenden echte temporäre Dateien und echte Hashberechnung. Eine ausdrücklich als Testseam abgegrenzte Stream-Unterklasse verändert die Dateigröße an einer kontrollierten Längenbeobachtung. Das ist ein reproduzierter Reader-Grenzfall, keine Behauptung einer in Produktion beobachteten Dateirace. Derselbe reale Reader wird zusätzlich im nativen Dispatcherpfad auf den vorhandenen World-Forge-Dateien geprüft.

Der frühere Gleichlängentest hatte 18 gegen 17 Bytes verglichen. RED belegt diese falsche Vorbedingung. GREEN verändert exakt ein Byte, erhält Größe und Receiptbytes und setzt die Dateizeit wieder auf den vorherigen Wert. Der veränderte SHA-256 verwirft weiterhin den ganzen betroffenen Receipt-Job und ändert die Graphrevision.

## Tatsächlich ausgeführtes RED → GREEN

| Prüfung | RED | Abschließendes GREEN |
| --- | --- | --- |
| Native ArtForge-Tests | 49 PASS / 8 FAIL / 0 SKIP | 60 PASS / 0 FAIL / 0 SKIP |
| Vollständige native EditMode-Suite | 339: 273 PASS / 14 FAIL / 52 SKIP | 342: 284 PASS / 6 FAIL / 52 SKIP |
| Revisions-Races im Browser | 6 erwartete FAIL, 6 Kontrollen PASS | 12/12 mit Pending-Zwischenschritten |
| Browser-Pending-Protokoll | 10 erwartete FAIL, 3 Negativkontrollen PASS | 13/13 |
| Browser-Poll-, Zeit- und Netzwerkgrenze | Keine historischen RED-Läufe behauptet | 3/3 |
| Neuer Replay echter Unity-State-/Graphbytes | Kein RED behauptet | 27/27 Prüfgruppen, 9 je Viewport |
| Python-Preflight | Zwei eigene Meta-Dateien zunächst unvollständig | 62/62 nach Ergänzung regulärer MonoImporter-Metadaten |
| Deterministischer Core | Unveränderte Suite | 23.143 Assertions, Exit 0 |
| Separater reiner C#-Vertragsrunner | Dieselben bestehenden Vertragsfälle | 35/35, Exit 0 |

Alle acht im RED fehlgeschlagenen nativen Testfälle bestehen nach der Korrektur: vier erwarteten `202` und erhielten zuvor `200`; zwei lasen 128 statt reservierter 64 Bytes; ein Fall akzeptierte ein geschrumpftes Receipt; einer belegte die falsche Gleichlängen-Vorbedingung. Drei zusätzliche Cancellation-Fälle erhöhen die ArtForge-Zahl von 57 auf 60. Die 35 separat ausgeführten C#-Fälle sind keine zusätzlichen unabhängigen nativen Fälle.

Alle 282 gemeinsamen Tests stimmen mit der tatsächlich ausgeführten Baseline einschließlich Fehlertexten und Skip-Gründen überein. Keine Tests wurden entfernt. Die sechs Fehler verlangen dieselben fehlenden privaten WorldForge-/VikingVillage-Assets; die 52 Skips sind unverändert. Es wurden keine Ersatzassets eingesetzt und keine Assertions abgeschaltet. Die ursprünglichen fehlgeschlagenen Preflight-Logs bleiben im Evidence-Paket.

Die 28 Browserfälle am finalen HTML bestehen aus 13 Protokoll-, 12 Race- und drei Grenzfällen. Der frühere direkte Race-GREEN mit zwölf Fällen gehört zu einem anderen HTML-Zwischenstand und wird nicht hinzugezählt. Die zusätzlichen 27 Replay-Prüfgruppen benutzen den neu erfassten Unity-Datensatz, synthetische `202`-Transportantworten und gezielt eingespeiste Fehler. Der Capture enthält keine gespeicherte `art/asset`-Antwort; deren Testhülle enthält einen unveränderten echten Graphknoten. Das ist kein Live-HTTP- oder Authentifizierungsbeleg.

Die dauerhaften Browserharnesses stehen unter `Tests/Browser/`. Gegenüber den ausgeführten Originalen wurden ausschließlich Argumentprüfung und lokale Dependency-Pfade portabel gemacht; die Rücktransformation belegt identische Test-/Assertionlogik. Syntax und Fehlaufruf wurden zusätzlich geprüft. Die langen Browserläufe wurden wegen dieser reinen Pfadänderung nicht wiederholt. Das fehlende installierte SDLC-Modul blieb als echter CLI-Fehler dokumentiert; es wurde kein SDLC-Erfolgsmanifest erfunden.

## Native Ausführung, Snapshot und Reaktionszeit

| Beleg | Identität |
| --- | --- |
| Native RED-Director-Ausführung | `20261124T161321Z-635878a4be06` |
| Native GREEN-Director-Ausführung | `20261124T162742Z-9018f545595a` |
| GREEN-Core-Director-Ausführung | `20261124T163220Z-b29c8f2da8e5` |
| Unity | 6000.6.8f1, eigene isolierte Projektkopie |
| GREEN-XML SHA-256 | `f5c10f9135dc0d6a08d6e5e8b1788935060564d7f028838741571867110de9f6` |
| GREEN-Core-Volltext SHA-256 | `f58810f94f987a7c03f23580b164d664173ee789877837ede87d43702a23bb8d` |
| GREEN-Snapshot SHA-256 | `4fe5aa8fbf85fbab203f26dcf3dd7b4b864257aa04bc1a0d9f98bfc2220ab6ae` |
| GREEN-Graphrevision | `8b8eef5ce7d2703fe68c457fbd6b6dacbdef2170c7f020014147604ee098e9f8` |

Der echte Dispatcher liest 163 Knoten: 50 `source`, 75 `mesh`, 38 `material`. Alle 38 Diagnosen sind `READ_BUDGET_EXCEEDED`; der Snapshot bleibt damit eine begrenzte Inventur. Diese Diagnose allein beweist weder die Ausschöpfung des gesamten 64-MiB-Budgets noch eine bestimmte Wartezeit. Die native Szenenprüfung blieb unverändert.

Vier protokollierte synchrone RED-Dispatches dauerten 1.672,590 / 1.292,331 / 1.380,689 / 1.293,058 ms. Der asynchrone GREEN-State-Test beobachtete 10.202 Editorupdates während des echten Lesens und 8,409 ms als größten Dispatch dieses Tests. Der größte in allen Startzeilen protokollierte kumulative Dispatch-Maximalwert war 41,889 ms. Das sind Messwerte verschiedener VPS-Läufe, kein kontrollierter Benchmark, keine universelle Latenzgrenze und kein FPS-/Gerätenachweis. Die vollständige Byteprüfung kostet weiterhin Zeit; sie liegt jetzt außerhalb des synchronen Dispatcher-Aufrufs.

## Mobile Ansicht und verbleibende Grenzen

Chromium prüfte 1007×601, 601×1007 und 390×844. Die realen 163 Knoten und alle 38 Diagnosetexte werden im Replay erhalten. Hauptbedienelemente sind mindestens 44 px hoch, Dokument und Body laufen nicht horizontal über. Der 24.312 px hohe Graph reagierte in allen drei Größen auf die geprüfte Touch-Geste. Die gemessenen Verschiebungen waren 156 / 149 / 112 px. Interne Screenshots wurden gesichtet, sind aber kein physischer Galaxy-Tab-A9-Nachweis.

Live-Browser-HTTP samt Authentifizierung, physisches Tab A9/Smartphone, PlayMode, Android-APK, Studio-Import und eine vollständige Suite mit den echten privaten Asset-Voraussetzungen stehen aus. Backend-Timeout, Ticketablauf und Domain-Reload wurden nicht separat nativ zeitlich ausgelöst. Es gibt weiterhin keine Variantenmutation, 3D-Vorschau, Retexturing- oder externe KI-Buchung.

## Abschluss der isolierten Ausführung

Der unabhängige Readback um 17:34:10 Uhr Europe/Berlin bestätigt lokalen GREEN-HEAD und Tree, saubere getrackte Dateien, 18 erhaltene ungetrackte Unity-Settings sowie keine eigenen Unity-Prozesse oder belegten Locks. Ausschließlich die von unserem beendeten Testeditor neu serialisierte eigene `ProjectSettings.asset` wurde nach Sicherung des Befunds wiederhergestellt. Der Director-Fehler bleibt im Originalreceipt erhalten.

Der aktive Editor war weiter vorhanden. Sein HEAD und getrackter Diff-Hash stimmen mit der unmittelbar vor GREEN erfassten Baseline überein; seine Statusliste änderte sich während paralleler Arbeit. Dafür wird keine Ursache behauptet. Der aktive Editor wurde durch diese Arbeit weder gestartet, gesteuert noch verändert.

GitHub-CI und Reviewstatus nach der Branch-Aktualisierung werden im PR und im separaten Veröffentlichungsreadback festgehalten. Dieser dokumentierte native Lauf wird nicht rückwirkend zu einem GitHub-, PlayMode- oder Gerätelauf erklärt.
