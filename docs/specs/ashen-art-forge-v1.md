# Ashen Art Forge v1 — erste lesende Canvas-Teilstufe

## Umfang und bestehende Zuständigkeiten

Diese Spezifikation beschreibt die erste technische Teilstufe von Lieferblock A aus
[Issue #132](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/132).
Sie erklärt weder Lieferblock A noch das gesamte Issue für abgeschlossen.
Ausgangsrevision der Umsetzung:
`489a3120915a5fdcec4bae83dc1f9afac6a2aa22`.

Die Asset-Werkstatt ist eine lesende Projektion bereits vorhandener World-Forge-
Exportbelege. Sie besitzt keine eigene Asset-Datenbank, Importwarteschlange,
Freigabe-Authority oder Spielpersistenz. `AreArpg.Core`, Spielstände, Loot,
Quests und der vorhandene 100-ms-Takt bleiben außerhalb ihres Schreibbereichs.

| Fläche | Bestehender oder neuer Anschluss |
| --- | --- |
| Browser | Bestehende `Assets/Editor/BauModus/Web/BauModus.html`, direkter Einstieg `?bau&art` |
| Authentifizierung | Bestehender Fadenwerk-Server und dessen `X-Fadenwerk-Token`-Prüfung; vorhandener Owner-Proxy unverändert |
| Dispatcher | Ausschließlich `BauModusBridge.Handle`, weiterhin registriert durch `FadenwerkAuthoringBridge.Extension = Handle` |
| Art-Routen | Neue `ArtForgeCanvasController.Handle`, vom vorhandenen Dispatcher vor Bau-Area-Parsing aufgerufen |
| Bestand | Bestehendes `WFBlenderIntake.Root`, ausschließlich dessen `results/<jobId>/receipt.json` und dort bezeichnete Artefakte |
| Vertrag | `Assets/Editor/ArtForge/Contracts/ArtGraphContract.cs`, Assembly `AshenCrypt.ArtForge.Contracts`, nur Editor, ohne Engine-Referenzen |
| Unity-Adapter | `ArtForgeWorldForgeReader.cs` und `ArtForgeCanvasController.cs`, Assembly `AshenCrypt.ArtForge.Editor`, nur Editor |

Der direkte Art-Einstieg beendet den normalen Bau-Boot vor `selectArea`,
Sprite-Queues und Kartenrendering. Die Art-Ansicht löst keine Bau-Vorschau aus.
Der Link zurück zum Bau-Modus öffnet ausdrücklich dessen vorhandene Oberfläche.
Die bestehende Authentifizierung wird wiederverwendet; eine zusätzliche
Session-, Origin- oder CSRF-Implementierung wird hier nicht behauptet.

## Vertrag `ashen.art-graph.v1`

Der Graph besteht aus folgenden Feldern. Alle Hashes sind kleingeschriebene
Hexadezimalzeichen; keine verkürzten SHAs, Ersatzhashes oder Zufallswerte.

| Feld | Bedeutung und Prüfung |
| --- | --- |
| `schema` | Genau `ashen.art-graph.v1`; andere Versionen werden abgewiesen |
| `projectSha` | Vollständige Git-HEAD-SHA mit 40 Hexzeichen |
| `graphRevision` | SHA-256 der unten festgelegten kanonischen Graphbytes, 64 Hexzeichen |
| `nodes` | Nicht-null Array, höchstens 512 Knoten; ein leerer Bestand ist zulässig |

### Knoten

| Feld | Bedeutung und Prüfung |
| --- | --- |
| `id` | Eindeutige ASCII-ID, 1–128 Zeichen; erstes Zeichen alphanumerisch, danach zusätzlich `-`, `_`, `.`; kein `..` |
| `label` | Nichtleerer Anzeigetext, höchstens 128 UTF-16-Codeeinheiten, keine Steuerzeichen oder ungültigen Surrogatpaare |
| `kind` | `source`, `concept`, `mesh`, `material`, `rig`, `animation` oder `preview`; `accepted` ist in dieser Teilstufe unzulässig |
| `category` | `figures`, `creatures`, `weapons`, `armor`, `props`, `materials`, `animations` |
| `variantGroup` | Leere Zeichenkette oder sichere ID; dient der lesenden Gruppierung |
| `parentIds` | Nicht-null Array mit höchstens 32 vorhandenen, eindeutigen Eltern-IDs |
| `artifactSha256` | Erwarteter SHA-256 der Artefaktbytes; die Strukturvalidierung allein bestätigt diese Bytes noch nicht |
| `provenance` | Herkunftsmetadaten gemäß nächster Tabelle |
| `license` | Lizenzmetadaten gemäß nächster Tabelle |
| `reviewState` | Ausschließlich `review-required` oder `protected` |

`parentIds` ist die einzige Kantenrepräsentation. Pfeile werden daraus abgeleitet.
Ein zweites unabhängig editierbares `edges`-Array existiert nicht. Fehlende Eltern,
doppelte Knoten, doppelte Elternbeziehungen, Selbstschleifen und längere Zyklen
werden abgewiesen. Kahn-Sortierung mit ordinaler ID-Reihenfolge liefert eine
stabile topologische Reihenfolge. Validierung und Kanonisierung verändern die
übergebenen Arrays nicht.

### Herkunft und Lizenz

| Feld | Bedeutung |
| --- | --- |
| `provenance.provider` | Sichere Anbieter-/Werkzeug-ID, in der aktuellen Projektion `worldforge-blender` |
| `provenance.sourceRef` | Logischer Belegverweis; aktuell `worldforge:<jobId>/<relative artifact path>` |
| `provenance.sourceSha256` | Optionaler SHA-256 laut Herkunftsbeleg; leer, falls nicht angegeben; keine Behauptung eines eigenen Quell-Datei-Readbacks |
| `provenance.receiptSha256` | Optionaler SHA-256 des Herkunftsbelegs; der World-Forge-Reader misst ihn selbst |
| `license.identifier` | Lizenzkennung bzw. Rechtebezeichnung, höchstens 128 Zeichen; bei unbekannter Lizenz leer |
| `license.evidenceRef` | Sicherer logischer oder öffentlicher HTTPS-Verweis; bei unbekanntem Beleg leer |
| `license.state` | `unverified` oder `documented`; `documented` verlangt Kennung und Belegverweis |

Eine dokumentierte Lizenz ist ein aufgezeichneter Rechtebeleg, keine automatische
juristische Prüfung und keine Owner-Freigabe. Der aktuelle World-Forge-
Exportvertrag enthält keine Lizenzinformationen. Deshalb emittiert der Reader
für alle seine Knoten `license.state = unverified` und
`reviewState = review-required`.

Logische Referenzen verwenden ausschließlich `worldforge:`, `asset:`, `license:`,
`receipt:`, `fixture:` oder `sha256:` und sichere, durch `/` getrennte ID-Segmente.
Referenzen sind höchstens 512 Zeichen lang. Dateisystempfade, Backslashes,
Traversal, Prozentkodierung, Zugangsdaten, Query und Fragment sind unzulässig.
HTTPS-Referenzen werden validiert, aber von diesem Vertrag niemals aufgerufen.
`fixture:` bezeichnet ausschließlich ausdrücklich synthetische Testdaten.

### Was genau als Herkunft belegt ist

Der erste Elternknoten eines World-Forge-Jobs bezeichnet dessen **Exportbeleg**,
nicht die ursprüngliche `.blend`-, `.fbx`- oder `.glb`-Quelldatei. Seine
Artefaktbytes sind die tatsächlich gelesenen `receipt.json`-Bytes. Die übrigen
Knoten bezeichnen die im Beleg aufgeführten Ergebnisdateien.

Der Reader vergleicht jede tatsächliche Ergebnisdatei mit Größe und SHA-256 aus
dem Receipt und liest das Receipt am Ende erneut. Erst nach erfolgreichem
Abgleich wird die gesamte Kette des Jobs sichtbar. Bei Fehlern wird der ganze
betroffene Job verworfen; ein halber Graph mit fehlenden Eltern wird nicht
veröffentlicht. Ein nachgewiesener Bytehash beweist weder Lizenz noch künstlerische
Qualität, Importfähigkeit, Ungefährlichkeit eines Modells oder Owner-Zustimmung.

`sourceSha256` bleibt eine Angabe aus dem vorhandenen Receipt. Der Reader öffnet
die ursprüngliche Quell-Datei nicht. Auch die Gruppierung
`source-<sourceSha256>` vergleicht diese Angaben; sie ist keine zusätzlich
beglaubigte Quellidentität.

## Kanonische Revision

`graphRevision` selbst wird aus der Kanonisierung ausgeschlossen, alle fachlichen
Graphfelder werden eingeschlossen. Das Format besteht aus binären Längenfeldern
und streng kodiertem UTF-8 ohne BOM:

1. Jede Zeichenkette: ihre UTF-8-Byteanzahl als vorzeichenloser 32-Bit-Wert in
   Big-Endian, danach genau diese Bytes.
2. Zuerst `schema`, dann `projectSha`, danach die Knotenanzahl als 32-Bit-Big-Endian.
3. Knoten nach `id` mit `StringComparer.Ordinal` sortieren.
4. Je Knoten diese Felder in fester Reihenfolge kodieren:
   `id`, `label`, `kind`, `category`, `variantGroup`, Elternanzahl,
   ordinal sortierte `parentIds`, `artifactSha256`, `provenance.provider`,
   `provenance.sourceRef`, `provenance.sourceSha256`,
   `provenance.receiptSha256`, `license.identifier`, `license.evidenceRef`,
   `license.state`, `reviewState`.
5. SHA-256 über alle resultierenden Bytes; Ausgabe als 64 kleine Hexzeichen.

Knoten-/Elternreihenfolge, Rechner-Locale und Systemuhr beeinflussen den Hash
nicht. Unicode wird nicht stillschweigend normalisiert. Geänderte Feldwerte
erzeugen eine andere Revision. `Validate` berechnet die Revision erneut und
vergleicht sie mit `graphRevision`. `ComputeRevision` prüft zuerst die Struktur;
es erzeugt keine Revision für einen ungültigen Graphen.

Der unabhängige synthetische Drei-Knoten-Testvektor in
`ArtGraphBehaviorCases.CanonicalDigest` umfasst 761 Bytes und hat den Digest
`71e0dd3a54226f855f6136ae057f35cf9a22302dd1b850491fa5d09cd4538522`.
Dieser Vektor beschreibt Testdaten, keine echten Spielassets.

## Lesende API und Revisionskontrolle

Alle Routen liegen unter dem vorhandenen `/api/x/`-Präfix. Die Tabelle zeigt
den Pfad ab diesem Präfix. Vor Zugriff prüft Fadenwerk weiterhin das vorhandene
Token. Der Controller eröffnet keinen Server und registriert keine Extension.

| Methode / Route | Anfrage | Antwort |
| --- | --- | --- |
| `GET art/state` | Initialer Abruf ohne erwartete Revision | `ok`, `readOnly`, `schema`, `projectSha`, `graphRevision`, `nodeCount`, `diagnostics` |
| `GET art/graph` | `expectedProjectSha` und `expectedGraphRevision` verpflichtend | `ok`, `readOnly`, `schema`, `projectSha`, `graphRevision`, `nodes`, `diagnostics` |
| `GET art/asset` | Beide erwarteten Revisionen und sichere `id` verpflichtend | `ok`, `readOnly`, `projectSha`, `graphRevision`, `node` |
| Jede andere Art-Route mit GET | Keine zusätzliche Funktion | `404 ART_ROUTE_UNKNOWN` |
| Jede Art-Route mit anderer Methode | Auch `art/propose`, `art/validate`, `art/request-export`, `art/approve-import`, `art/capture-review` | `405 READ_ONLY`, vor Body-, Datei- oder Assetzugriff |

Der Ablauf ist `art/state` → `art/graph` mit beiden erhaltenen Revisionen →
optionales `art/asset` mit derselben Identität. Nach einem Konflikt verwirft die
Oberfläche den alten Graphen und verlangt erneutes Laden. Es gibt keine
optimistische Freigabe mit einem veralteten Stand.

| HTTP / Code | Bedeutung |
| --- | --- |
| `401` | Bestehende Fadenwerk-Authentifizierung fehlt oder lehnt ab |
| `400 QUERY_INVALID` | Ein unbekannter Query-Schlüssel wurde übergeben; Pfade, Hosts und Shellargumente sind keine API-Eingaben |
| `400 ASSET_ID_INVALID` | Unsichere oder fehlende Asset-ID |
| `404 ASSET_NOT_FOUND` | ID gehört nicht zum aktuell geprüften Graphen |
| `428 EXPECTED_REVISION_REQUIRED` | Projekt-/Graphrevision fehlt oder ist formal ungültig |
| `409 PROJECT_REVISION_CONFLICT` | Projekt-SHA hat sich geändert, auch während des Lesevorgangs |
| `409 GRAPH_REVISION_CONFLICT` | Erwartete Graphrevision stimmt nicht mehr |
| `503 EDITOR_BUSY` | Unity importiert oder kompiliert |
| `503 PROVENANCE_UNAVAILABLE` | Projektidentität oder Herkunft ließ sich nicht sicher lesen |

Controller-Fehler haben die Form `{"ok":false,"code":"...","error":"..."}`.
Die Art-API akzeptiert nur die Query-Schlüssel `expectedProjectSha`,
`expectedGraphRevision` und `id`. Sie liest keinen frei übergebenen Dateipfad.

Der Projektbeleg stammt aus einer festen lesenden Git-Invocation im tatsächlichen
Unity-Projekt. Vor und nach dem Snapshot wird HEAD gelesen. Ungesicherte
Änderungen an ArtForge, Bau-Modus, Fadenwerk oder dem World-Forge-Editor verhindern
eine Antwort, die diese Änderungen fälschlich als saubere Commitrevision ausgeben
würde. Unabhängige Änderungen außerhalb dieser Flächen werden dadurch nicht als
geprüft behauptet. Git-Aufrufe haben ein festes Zeitlimit von fünf Sekunden.

Dies ist eine Revisionsprüfung für Lesen, kein gespeicherter Compare-and-Swap-
Schreibvertrag. `operationId`, persistierte Graphänderungen, Undo/Dry-Run für
Authoring und wiederholbare Importaufträge bleiben späteren Lieferteilen
vorbehalten. Identische unveränderte Eingangsbytes ergeben bei erneutem Lesen
denselben Graphdigest. Diagnosen sind separate Beobachtungen und gehören nicht
zu den kanonischen Graphfeldern.

## World-Forge-Lesebudget und Fehlerbehandlung

| Grenze | Wert |
| --- | --- |
| Allgemeiner Graph | 512 Knoten, 32 Eltern je Knoten |
| Jobs im bestehenden `results`-Eingang | 128; bei mehr Jobs `CATALOG_LIMIT` und kein willkürlicher Ausschnitt |
| Ergebnisdateien je Job | 16 |
| Einzelne Ergebnisdatei | 16 MiB |
| Receipt-Datei | 256 KiB |
| Gesamte gelesene Nutzbytes je Snapshot | 64 MiB, einschließlich beider Receipt-Lesezugriffe |
| Relativer Ergebnisdateipfad | 180 Zeichen, jedes Segment zusätzlich sichere ID mit höchstens 128 Zeichen |
| Allgemeiner Contract-Byteprüfer | Höchstens 256 MiB; der World-Forge-Adapter verwendet die engeren Grenzen oben |

Jobs und Dateien werden ordinal sortiert. Das globale Knotenlimit bleibt auch
bei 128 Jobs auf 512 begrenzt. Ein Auftrag, dessen vollständige Kette dieses
Limit überschreiten würde, wird mit `CATALOG_NODE_LIMIT` ausgelassen. Ebenso
kann das Bytebudget weitere vollständige Jobs ausschließen. Solche Snapshots
sind anhand ihrer Diagnosen ausdrücklich als unvollständige Ansichten erkennbar;
sie werden nicht als vollständiger Katalog ausgegeben. Seitenauswahl und
Fortsetzungstoken sind noch nicht implementiert.

Der Reader folgt keinen symbolischen
Links im Eingangspfad oder dessen Vorfahren. Absolute Pfade, Traversal, unsichere
Segmente, fehlende Dateien, widersprüchliche Größen und Hash-Drift führen zur
Ablehnung. Receipt-Größe und Attribute werden vor dem Öffnen geprüft; während
und nach dem tatsächlichen Lesen bleiben Größen-/Link-/Hashprüfungen aktiv.
Dateiänderungszeiten dienen ausschließlich dem Erkennen von IO-Änderungen;
sie fließen weder in den Graphdigest noch in die Spielsimulation ein.

Diagnosen enthalten feste Fehlermeldungen und Codes statt privater Pfade,
Receipt-Inhalte oder roher Exception-Texte. Ein fehlender Eingang liefert einen
ehrlich leeren Graphen mit `WORLD_FORGE_UNAVAILABLE`; der Reader legt weder
Verzeichnisse noch Beispielassets an. Weitere Diagnosecodes sind unter anderem
`PATH_REJECTED`, `HASH_DRIFT`, `ARTIFACT_MISSING`, `RECEIPT_INVALID`,
`READ_BUDGET_EXCEEDED`, `CATALOG_NODE_LIMIT`, `JOB_ID_INVALID` und `READ_UNAVAILABLE`.

## Canvas und Tablet-Umfang

Die Ansicht bietet Suche, Kategorien, Variantengruppen, lesende Beziehungen und
Details mit vollständigen Hash-/Herkunftsangaben. Bedienung ist für 1007 × 601
CSS-Pixel ausgelegt: Fingertipps statt notwendigem Hover, ausreichend große
Bedienflächen, verschiebbarer Graphbereich und bei schmaler Ansicht ein
zugänglicher Detailbereich. Ein Browserlauf in dieser Größe ersetzt keinen
physisch ausgeführten Samsung-Galaxy-Tab-A9-Test.

Der erste Reader ordnet nachgewiesene Exportdateien nur **Props** bzw.
**Materialien** zu. Figuren, Kreaturen, Waffen, Rüstungen und Animationen sind
als Kategorien im Vertrag und in der UI vorbereitet; ihre tatsächlichen
Katalogadapter und ihre vorhandenen fachlichen Owner werden hier noch nicht
angeschlossen. Ein Menüeintrag beweist keinen vorhandenen Assetbestand.

Noch nicht Bestandteil dieser Teilstufe sind 3D-/Material-/Animationsvorschau,
Drag-and-drop-Authoring, Variantenänderung, Owner-Freigabe, Import,
Retexturing, Blender-Submission, externe KI-Aufträge, Unity-Playtesting und
Abschlusspilot. Insbesondere werden `WFBlenderIntake.Submit`, `ImportFbx`,
`WFAssetIntakeApprovals.ConfirmSpawn` und `ConfirmPoi` nicht aufgerufen.
Eine geschützte Figur oder Waffe erhält keinen dekorativen Importweg.

## Regressionen und getrennte Nachweise

| Prüfung | Tatsächlich prüfbare Grenze |
| --- | --- |
| `Tests/ArtForgeVerification` | Reiner produktiver C#-Vertrag mit gemeinsam genutzten synthetischen Verhaltenstests; .NET-8-Konsolenprojekt ohne NuGet-Abhängigkeiten |
| C#-Compiler + Mono | Derselbe Vertrag und dieselben Console-Testquellen können mit tatsächlich vorhandenen Werkzeugen kompiliert und ausgeführt werden; daraus entsteht kein behaupteter .NET-8-SDK-Lauf |
| `ArtGraphContractTests` | Derselbe Fallkatalog innerhalb nativer Unity-EditMode-Tests |
| `ArtForgeReadOnlyTests` | Echte temporäre Datei-IO, Byte-/Pfad-/Symlink- und Dispatchergrenzen; ausdrücklich synthetische Testdateien |
| `Tests/Preflight/test_art_forge_contract.py` | Statische Architekturgrenzen; kein Beleg für Unity-Ausführung |
| Browserprüfung | Tatsächliche Bedienung, Requests, Layout und Fehlerbehandlung; Fixture-Antworten müssen von echten Unity-Antworten getrennt bleiben |
| Bestehende Core-Suite | Separater Nachweis für Determinismus und bestehende Save-/Replay-Verträge |

Die Verhaltenstests decken Zyklen, fehlende Eltern, gleiche Dateilänge bei
Hash-Drift, unzulässige Freigaben, Revisionskonflikte, Feldbindung und
reihenfolge-/kulturunabhängige Kanonisierung ab. RED-Nachweise, GREEN-Ausgaben,
native XML-Ergebnisse, Quell-/Patch-Digests und konkrete Einschränkungen gehören
in den zugehörigen Ausführungsbericht. Diese Spezifikation enthält keine
vorweggenommenen Testergebnisse. Unity, APK, Gerät und produktive Aktivierung
bleiben jeweils separat nachzuweisen.

Vor Push, Merge, externen KI-Aufträgen oder Änderungen am aktiven Unity-Editor
gilt die ausdrückliche Owner-Freigabe aus dem Auftrag. Das Testen einer eigenen
isolierten Kopie ist keine solche Veröffentlichung oder Aktivierung.
