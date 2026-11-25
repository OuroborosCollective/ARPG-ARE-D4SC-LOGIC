# Ashen Crypt Art Forge — erster Canvas-Patch und Prüfbericht

Stand: 24. November 2026, Europe/Berlin. Bezug: [Issue #132](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/132).

## Ergebnis und Freigabestatus

Die erste lesende Teilstufe von Lieferblock A ist implementiert und in einer eigenen Unity-Projektkopie geprüft. Der versionierte Vertrag `ashen.art-graph.v1`, der Anschluss an den vorhandenen Bau-Modus und eine mobile Browseransicht liegen als lokaler Patch vor. Der vollständige Lieferblock A ist damit noch nicht abgeschlossen.

49 neue native Unity-EditMode-Tests bestehen. Die vorhandene Core-Suite besteht mit 23.143 deterministischen Assertions. Die gesamte EditMode-Suite bleibt wegen sechs bereits im Ausgangsstand reproduzierten Fehlern und 52 Skips ohne Gesamtfreigabe. Diese Grenzen sind unten vollständig ausgewiesen.

Kein Push, Pull Request, Merge, externer KI-Auftrag oder Eingriff in den aktiven Unity-Editor wurde durchgeführt. Die zusätzliche Unity-Studio-Option ist ein Architekturentwurf; es gibt keinen installierten Studio-Provider und keinen vorgetäuschten Aktivierungsschalter.

## Revisionen und parallele Arbeit

| Bezug | Identität |
| --- | --- |
| Geprüfter GitHub-main | `489a3120915a5fdcec4bae83dc1f9afac6a2aa22` |
| Nativer Implementierungskandidat | `c089fc97ed957fdc4784be35d47892add23c3531` |
| Implementierungsbaum | `36d04d0ade938e91429cf32dd864b4395f629cb2` |
| Lokaler Review-Branch | `local/art-forge-a-review` |
| Parallel offener PR #136 | `667d6777e97cd66a94def5227910f954fdae71cc` |

AGENTS.md, Memory.md, die Director-Verträge und die vorhandene Übergabe wurden vor der Integration gelesen. PRs #133–#135 waren bereits integriert. PR #136 betrifft Gegner und Clips; die technischen Art-Forge-Flächen überschneiden sich nicht. Memory.md und die Übergabe sind gemeinsame Dokumentationsflächen und müssen bei einer späteren Veröffentlichung erneut abgeglichen werden.

Die Ausführungsevidence bindet an den Implementierungskandidaten oben. Ein anschließender lokaler Dokumentationscommit ergänzt diesen Bericht, die Studio-Skizze sowie genau einen Memory-Eintrag und die Übergabe; er ändert keine getesteten Produkt- oder Testquellen. Sein vollständiger SHA und der vollständige Patchdigest stehen im Reviewpaket.

## Konkreter Anschluss

Die bestehende Registrierung bleibt genau einmal bestehen:

```csharp
FadenwerkAuthoringBridge.Extension = Handle;
```

Im vorhandenen `BauModusBridge.Handle` stehen zwei neue Zeilen vor dem Bau-Area-Parsing:

```csharp
if (path != null && path.StartsWith("art/", StringComparison.Ordinal))
    return AshenCrypt.ArtForge.ArtForgeCanvasController.Handle(method, path, body, query);
```

Der Aufrufweg ist die vorhandene Bau-Modus-Seite, der vorhandene Fadenwerk-Server mit Tokenprüfung, der bestehende Bau-Dispatcher und der neue lesende Adapter zu `WFBlenderIntake.Root`. Es entsteht kein zweiter Dispatcher, Listener, Assetbestand oder Freigabedienst.

| Route unter `/api/x/` | Verhalten |
| --- | --- |
| `GET art/state` | Initialer Projekt- und Graphstand mit Knotenanzahl und Diagnosen |
| `GET art/graph` | Verlangt vollständige `expectedProjectSha` und `expectedGraphRevision` |
| `GET art/asset` | Verlangt dieselben Revisionen plus sichere Knoten-ID |
| Jede Art-Schreibmethode | `405 READ_ONLY`, vor Body- und Dateizugriff |
| Unbekannte Art-GET-Route | `404` |
| Fehlende Revision | `428` |
| Projekt- oder Graphdrift | `409`; die Oberfläche verwirft ihren alten Stand |

Der Einstieg `?bau&art` zweigt vor dem normalen Bau-Boot ab. Preview-, Sprite-, Kartenrender- und Bau-Schreibaktionen werden nicht gestartet. Alte Bau-Tastenkürzel dürfen im Art-Modus keine Anfragen senden. Die vorhandene Authentifizierung wurde wiederverwendet; ein eigener Origin-/Session-Schutz wird nicht behauptet.

## Vertrag, Herkunft und Lizenz

`parentIds` ist die einzige Kantenquelle. Doppelte IDs, doppelte Eltern, fehlende Eltern, Selbstschleifen und längere Zyklen werden abgewiesen. Eine ordinal geordnete Kahn-Sortierung liefert reproduzierbare Topologie. Der SHA-256 der kanonischen Graphbytes bindet Schema, vollständigen Projekt-SHA und sämtliche fachlichen Knotenfelder einschließlich Herkunft, Lizenz und Prüfzustand. Eingabereihenfolge und Locale ändern ihn nicht.

Die Kanonisierung verwendet UTF-8 mit vorangestellten 32-Bit-Big-Endian-Längen und festen Feldfolgen. Der unabhängige synthetische Testvektor umfasst 761 Bytes und ergibt `71e0dd3a54226f855f6136ae057f35cf9a22302dd1b850491fa5d09cd4538522`. Das ist ein Vertragsvektor, kein realer Assetbeleg.

Der World-Forge-Adapter liest vorhandene Exportbelege und misst die tatsächlichen Ergebnisbytes. Größe und SHA-256 müssen zum Beleg passen; das Receipt wird am Ende erneut geprüft. Ein fehlerhafter Job wird vollständig ausgelassen. Traversal und symbolische Links werden abgewiesen; API-Eingaben werden nicht zu Dateipfaden oder Shellargumenten.

Der Elternknoten bezeichnet den gelesenen Exportbeleg. Der ursprüngliche Quelldateihash ist ausdrücklich eine Angabe aus diesem Beleg; die ursprüngliche Quelldatei wurde nicht zusätzlich gelesen. Ein passender Bytehash beweist weder Rechte noch künstlerische Qualität oder eine Owner-Freigabe.

Weil der bestehende Exportvertrag keine Lizenzbelege enthält, zeigt die reale Projektion für alle Knoten `license.state = unverified` und `reviewState = review-required`. Der allgemeine Vertrag kennt zusätzlich dokumentierte Lizenzen mit Kennung und Beleg sowie `protected`. `owner-approved`, `imported` und ein `accepted`-Knotentyp sind in dieser lesenden Teilstufe unzulässig. Ein Client kann keine Freigabe einschmuggeln.

## Einordnung der getrackten Flächen

| Fläche | Änderung und Wirkung |
| --- | --- |
| Produktions-/Authoringfläche | Bau-Dispatcher und bestehende HTML-Seite; neuer Editor-Controller und World-Forge-Reader |
| Testfläche | Gemeinsame C#-Verhaltensfälle, .NET-8-Runner, Unity-EditMode-Tests, acht zusätzliche Python-Vorprüfungen |
| Persistenzfläche | Vorhandene Receipts und Artefakte werden gelesen; keine neue Asset-Datenbank, Save-Migration oder Graph-Schreibpersistenz |
| Effektfläche | Begrenzte Datei- und Git-Lesezugriffe sowie Browserdarstellung; kein Import, Spawn, Publish oder KI-Auftrag |
| Corefläche | Kein Core-/Tick-/Save-Quellcode im Patch; vorhandene Core-Regressionssuite ausgeführt |
| Runtime-Projektion | Tatsächlicher Unity-Editor-Readback; UI-Wiedergabe gesondert von Live-HTTP und Spielruntime ausgewiesen |

## Tatsächlich ausgeführte Prüfungen

| Prüfung | Ergebnis | Aussagegrenze |
| --- | --- | --- |
| .NET-8-Vertragsprojekt | 35 bestanden, 0 Fehler | Echte C#-Ausführung, synthetische Graphdaten |
| Vorhandener Mono/C#-Compiler | Dieselben 35 bestanden | Kein Ersatzbeleg für Unity-Runtime |
| Python-Preflight | 62 bestanden, Exit 0 | Architektur- und Integrationsgrenzen; acht neue Fälle |
| Bestehende CoreVerification | 23.143 Assertions bestanden, Exit 0 | Reale gemeinsame Simulation einschließlich bestehender Save-/Replay-Regressionen |
| Neue native ArtForge-Assembly | 49 bestanden, 0 Fehler, 0 Skips | 35 Vertragsfälle und 14 Datei-/Dispatcher-Integrationen |
| Vollständige native Kandidatensuite | 331 gesamt: 273 bestanden, 6 Fehler, 52 Skips | Gesamtlauf fehlgeschlagen |
| Vollständige native Baseline | 282 gesamt: 224 bestanden, 6 Fehler, 52 Skips | Identische Ergebnisse aller 282 gemeinsamen Tests |
| Synthetischer Tablet-Browserlauf | 20/20 Prüfgruppen | 1007×601 und 601×1007; keine echte Unity-HTTP-Verbindung |
| Browser-Replay echter Unity-Daten | 16/16 Tabletgruppen und 8/8 Smartphonegruppen | Aufgezeichnete State-/Graph-Antworten; 390×844 zusätzlich |
| Unabhängige Wolfram-Rechnung | DAG, Zyklus, Selbstschleife, fehlender Elternknoten und sechs Kantenreihenfolgen wie erwartet | Kleine synthetische Mathematikprüfung; keine C#-/Unity-Ausführung |

Die 282 gemeinsamen nativen Tests stimmen in Ergebnis, Label, Fehlertext und Skip-Grund überein. Es wurden keine Tests entfernt. Die Differenz besteht aus genau 49 neuen bestandenen ArtForge-Tests.

### RED/GREEN ohne erfundene Vergangenheit

Vor der Vertragsimplementierung scheiterte der neue C#-Testlauf beim Kompilieren an den noch fehlenden Vertragstypen. Nach der Implementierung bestehen die 35 Fälle. Zusätzlich wurden nachträglich vier Schutzprüfungen ausschließlich in Kopien außerhalb des Repositorys entfernt. Diese Fault-Injection-Läufe sind ausdrücklich keine behaupteten historischen Vor-Fix-Läufe.

| Entfernte Schutzprüfung in der Testkopie | Tatsächlicher RED-Lauf |
| --- | --- |
| Zyklusprüfung | 33 PASS, 2 FAIL: Zyklus und Selbstschleife |
| Elternprüfung | 34 PASS, 1 FAIL: fehlender Elternknoten |
| Bytehashvergleich | 34 PASS, 1 FAIL: abweichende Artefaktbytes gleicher Länge |
| Freigabeprüfung | 33 PASS, 2 FAIL: Owner-Freigabe und Imported-Behauptung |

Alle vier Kopien kompilierten erfolgreich und endeten mit Test-Exit 1. Die 35 Testfälle blieben unverändert. Ohne Zyklus-/Freigabeguard fehlt der vorgeschriebene Fehlercode, während die verbleibende Revisionsprüfung noch ablehnt; ohne Elternprüfung entsteht eine unkontrollierte Ausnahme; ohne Bytehashprüfung werden falsche Bytes angenommen. Die Originalquellen blieben bytegleich.

Der Browser-RED auf der unveränderten Basis belegte die fehlende Art-Ansicht und den normalen Bau-Boot. Der erste GREEN-Versuch wurde durch einen Fehler im Testfokus unterbrochen: Browser-Undo fokussierte den Suchinput und folgende Tastentests schrieben dort Text. Nach Korrektur der Testvorbedingungen bestanden alle Prüfungen. Keine fachliche Assertion wurde entfernt, das Produkt-HTML blieb unverändert. Beide Fehlversuche sind im Paket dokumentiert.

Ein anfänglicher Voll-Core-Versuch mit altem Mono-C#-Compiler scheiterte an vorhandener C#-9-Syntax. Danach wurde das bereits installierte Unity-.NET-8-SDK gefunden und das tatsächliche Core-Projekt erfolgreich ausgeführt. Es wurde keine Dependency installiert. Die SDLC-Konfigurationsvalidierung blieb wegen fehlender installierter SDLC-Abhängigkeit nicht ausführbar.

## Echter Unity-Readback

Unity 6000.6.8f1 führte die Tests über den bestehenden Director in einer separaten Projektkopie aus. Ein eigener Konfigurations- und Datenpfad isolierte den Lauf. Der aktive Editor wurde nicht zum Ausführen der Tests benutzt.

| Beleg | Wert |
| --- | --- |
| Core-Job | `20261124T072940Z-decc024aff3c` |
| Kandidaten-EditMode-Job | `20261124T073236Z-f6aef030b69a` |
| Baseline-EditMode-Job | `20261124T073829Z-d12081810eb3` |
| Gemessene Graphrevision | `a0d9da28064ab1e74925d1aa9ce6fb4473eaf55f9bb7c6d8f21ad9478b921053` |
| Snapshot-SHA-256 | `58e2e09c220aa29c62214ee60ab12ab89a63edc59dc5abd2fa877fc0f1bad177` |

Der echte Dispatcher lieferte 163 Knoten aus 25 vorhandenen Exportjobs: 50 Source-, 75 Mesh- und 38 Materialknoten, darunter 25 Receipt-Eltern. 38 weitere Jobs wurden mit `READ_BUDGET_EXCEEDED` ausgelassen. Das ist eine begrenzte Erstansicht, keine vollständige Inventur. Die API behält alle Diagnosen; die UI zeigt sie in einem begrenzten Scrollbereich.

Der native Test prüfte State, Graph, einen vorhandenen Asset-Knoten sowie veraltete Projekt- und Graphrevisionen. Anzahl vorhandener GameObjects und der Dirty-Zustand der aktiven Testszene blieben gleich. Import-/Approval-Routen wurden nicht ausgeführt.

### Bestehende Fehler und erzeugte Einstellungen

Alle sechs Fehler liegen in bestehenden `AssetIntakeEditModeTests`: Ein Test verlangt mindestens 791 reale private Assets und findet in der frischen Kopie null; fünf weitere verlangen das private VikingVillage-Paket. Derselbe Baseline-Lauf reproduziert exakt dieselben Fehlertexte. Keine Ersatzassets wurden erzeugt und keine Assertions abgeschaltet.

Die 52 Skips betreffen 48 private Art-/Modell-/Clip-Voraussetzungen, ein freies Baugebiet und drei nicht konfigurierte echte Blender-Prüfjobs. Namen und Gründe stehen vollständig in den XML- und Vergleichsberichten.

Unity serialisierte in beiden Läufen ProjectSettings.asset automatisch neu. Deshalb blieb auch der Director auf `failed / TRACKED_WORKTREE_DIRTY`; der Bericht deutet dies nicht als Erfolg um. Nach Sicherung des redigierten Befunds wurde ausschließlich diese erzeugte getrackte Änderung in unserer Testkopie zurückgesetzt. Generierte Zugangswertfelder wurden nicht in Git übernommen. Die 18 ungetrackten generierten ProjectSettings blieben erhalten. Der getrackte Bereich der Kopie ist wieder sauber auf dem Kandidaten, beide eigenen Testeditoren sind beendet.

Der aktive Editor blieb vorhanden. Sein HEAD, getrackter Diff und Git-Statusliste stimmen mit der Anfangserfassung überein. Damit werden keine ungeprüften Aussagen über alle Bytes ignorierter oder ungetrackter Privatassets getroffen.

## Mobile Bedienung und Unity-Studio-Option

Die Canvas-Ansicht bietet Suche, Kategorien, lesende Varianten, Herkunftskanten und Details mit vollständigen Hashes. Die sichtbaren Kopf-Bedienelemente sind mindestens 44 px groß. Querformat, Hochformat und Smartphone-Größe zeigen keinen horizontalen Dokumentüberlauf. Der große Graph und der Detailbereich scrollen getrennt; eine emulierte Touch-Geste bewegte den Graph auf allen drei Größen.

Die Browser-Replays verwenden den gespeicherten echten Unity-State und Graph. Detailantworten werden für den UI-Test aus echten Graphknoten aufgebaut und Konfliktantworten gezielt eingespeist. Das ist kein Live-HTTP-/Authentifizierungs-End-to-End-Test und kein physischer Tab-A9-Lauf. Interne Screenshots wurden visuell geprüft; Bilddateien sind nicht Bestandteil des ausgelieferten Text-/Code-Prüfpakets.

Für Unity Studio liegt ein [separater Architekturentwurf](../../specs/ashen-art-forge-studio-option.md) vor. Nach den am 24.11.2026 geprüften Unity-Primärquellen unterstützt Studio selbst keine mobilen Browser. Unsere mobile Canvas-Oberfläche kann unabhängig davon nützlich bleiben; eine spätere PWA würde dieselbe Oberfläche öffnen. Studio wäre eine zusätzliche abschaltbare Quelle für visuelle Entwürfe. Der belegte Studio-Importer erzeugt auch C# und benötigt weitere Projekteinstellungen, weshalb ein Pilot in ein eigenes Importprojekt gehört. Paketinstallation, Cloudzugang, Upload, automatisierter Provider und App-Paket sind nicht umgesetzt.

## Noch nicht belegt oder geliefert

GitHub-CI, PlayMode, Android-APK, physisches Tab A9, Live-Browser-HTTP zu Unity und Unity-Studio-Import wurden nicht ausgeführt. Eine volle native Suite mit allen realen privaten Asset-Voraussetzungen steht aus. Die bestehende CI wird um den echten .NET-Vertragstest ergänzt; aus der YAML-Änderung folgt noch kein ausgeführter GitHub-Workflow.

3D-/Material-/Animationsvorschau, Variantenänderung, Drag-and-drop-Authoring, Importfreigaben, Retexturing, externe KI-Aufträge und der abschließende Integrationspilot gehören zu späteren Teilen von Issue #132. Die sieben Kategorien sind im Vertrag vorbereitet; dieser erste Reader bindet tatsächlich nur Props und Materialien aus World Forge an.

## Review und nächster erlaubter Schritt

Der vollständige Patch, seine Basisrevision und SHA-256, die Specs sowie die originalen Text-/JSON-/XML-Prüfberichte liegen im Reviewpaket. Der Patch wurde gegen die Basis mit einem separaten Git-Index auf Anwendbarkeit geprüft. Die Originalbelege der nativen Läufe bleiben erhalten; der öffentliche Repository-Bericht enthält keine privaten Assetdateien.

Vor Push oder Draft-PR ist die ausdrückliche Zustimmung zum konkreten Patch erforderlich. Ein Merge, eine Aktivierung im aktiven Unity-Projekt oder ein externer Studio-/KI-Auftrag benötigt jeweils eine entsprechende ausdrückliche Freigabe. Dieser Bericht erteilt keine solche Zustimmung.
