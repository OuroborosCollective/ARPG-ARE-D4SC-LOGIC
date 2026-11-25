# Optionale Unity-Studio-Anbindung an Ashen Art Forge

**Status: Architekturentwurf — nicht implementiert, nicht aktiviert.**
Stand: 24. November 2026. Bezug: [Issue #132](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/132).
Dieser Entwurf ergänzt die [erste lesende Canvas-Teilstufe](ashen-art-forge-v1.md).
Er ändert weder den Produktcode des Kandidaten `c089fc97ed957fdc4784be35d47892add23c3531`
noch dessen Freigabestatus. Eine Studio-Integration und ein Merge sind damit nicht freigegeben.

## Entscheidung und belegte Grenzen

Unity Studio kann als zusätzliche, abschaltbare Herkunft für visuelle Entwürfe vorgesehen werden.
Smartphone und Samsung Galaxy Tab A9 bedienen dafür unsere Fadenwerk-/Bau-Modus-Oberfläche.
Die Studio-Oberfläche selbst setzt WebGPU voraus; Unity empfiehlt Desktop-Chrome oder -Edge
und unterstützt mobile Browser derzeit ausdrücklich nicht. Ein Unity-Konto ist erforderlich. [U1]
Eine spätere installierbare PWA wäre eine Hülle für unsere Oberfläche, kein Android-Unity-Editor.
Sie macht Studio weder mobil unterstützt noch offline verfügbar; beides wäre gesondert nachzuweisen.

Belegt ist der Export von Studio-Entwürfen in den Unity Editor einschließlich Szene, Assets
und aus Studio-Logic erzeugten C#-Skripten. [U2] Ein Rückkanal vom Editor zu Studio steht laut
Produktseite auf der Roadmap; eine bidirektionale Synchronisation wird nicht vorausgesetzt. [U4]
Die geprüften Quellen belegen keinen für dieses Projekt nutzbaren öffentlichen Headless-API-Vertrag.
Ein automatisch ansprechbarer Studio-Provider ist deshalb eine offene Integrationsfrage.

## Mobile Bedienung und Zuständigkeiten

| Anwendungsfall | Oberfläche und Nutzen | Grenze dieser Planung |
| --- | --- | --- |
| Bestand, Herkunft und Lizenzbelege prüfen | Bestehende lesende Art-Ansicht im mobilen Browser | Studio muss dafür nicht aktiviert sein |
| Varianten auswählen und Beziehungen lesen | Vorhandene Variantengruppen und Detailansicht | Keine neue Variante erzeugen oder speichern |
| Studio-Entwurf ansehen | Später geprüfter Vorschaubeleg oder Desktop-Link | Kein eingebetteter Studio-Editor auf dem Tab A9 zugesagt |
| Importstatus und Prüfbericht lesen | Spätere Projektion realer World-Forge-Belege | Kein erfundener Status und keine simulierte Fertigstellung |
| Import oder Freigabe anfordern | Erst nach gesondert umgesetztem, authentifiziertem Workflow | Die aktuelle Art-API bleibt lesend; keine Freigabe per Clientfeld |
| Als App öffnen | Optionale spätere PWA für dieselbe Oberfläche | Keine native Editor-App und kein belegter Offlinebetrieb |

Für eine mobile Erweiterung gelten große Touch-Ziele, lesbare Details im Hochformat,
Bedienung ohne Hover oder zwingendes Drag-and-drop und klar erkennbare Lade-/Fehlerzustände.
Abnahme erfordert echte Browserprüfungen sowie einen eigenen Test auf dem Galaxy Tab A9.
Ein emulierter Viewport belegt weder Gerätelaufzeit noch Studio-Kompatibilität.

## Ein bestehender Integrationsweg

Alle eigenen Browseraktionen bleiben beim bestehenden Fadenwerk-Zugang und Bau-Modus-Dispatcher.
Die Registrierung `FadenwerkAuthoringBridge.Extension = Handle` bleibt dessen einziger Anschluss.
Es entsteht weder ein zweiter Listener/Dispatcher noch eine zweite Asset- oder Freigabe-Authority.
World Forge behält Intake, Belege und die vorhandenen Importzuständigkeiten.
Der Graph `ashen.art-graph.v1` bleibt deren lesende, revisionsgebundene Projektion.

Ein späterer Provider ist standardmäßig aus und lässt sich unabhängig vom Canvas abschalten.
Seine serverseitig geprüfte Verfügbarkeit muss tatsächliche Fähigkeiten und Zugang widerspiegeln.
Ein UI-Schalter allein erteilt keine Berechtigung und behauptet keine funktionierende Integration.
Bei Deaktivierung werden keine neuen Aufträge oder Uploads begonnen; vorhandene lokale Belege
bleiben lesbar. Laufende externe Aufträge dürfen nur als beendet gelten, wenn das bestätigt ist.
Die bestehende Authentifizierung bleibt zuständig; Tokens gehören nicht in Links oder Vorschauen.

## Isolierter Importpilot

Der erste Pilot verwendet ein eigenes, unbenutztes Unity-Importprojekt außerhalb des Spielprojekts.
Das Studio-Importer-Paket und seine Einstellungen werden ausschließlich dort erprobt.
Unity nennt mindestens `6000.3.2f1`, das Paket `com.unity.studio`, ein Universal-3D-Projekt,
URP, aktivierten Legacy Input über „Enable both“, TextMeshPro Essentials und Unity-Anmeldung. [U3]
Diese Voraussetzungen rechtfertigen keine Änderung am aktiven Editor oder Hauptprojekt.
Eine passende Versionsnummer allein beweist die Paket- und Projektkompatibilität noch nicht.

| Prüfschritt | Erforderlicher Beleg vor Weitergabe |
| --- | --- |
| Export identifizieren | Tatsächlicher Studio-Entwurf, verfügbare Exportrevision und lokaler Importbericht |
| Dateien prüfen | Größen-/Formatgrenzen, echte SHA-256 der gelesenen Bytes und vollständige Artefaktliste |
| Herkunft und Rechte prüfen | Quellverweise, Providerangaben und Lizenzbelege; Unbekanntes bleibt ausdrücklich unbekannt |
| Generiertes C# prüfen | Review und isolierte Compile-/Runtime-Ergebnisse vor einer Verwendung; keine automatische Spielintegration |
| World-Forge-Intake prüfen | Nur nachweislich unterstützte Artefakte über den bestehenden Intake und dessen Belegformat |
| Übergabe entscheiden | Eindeutiger Diff, Artefakthashes, Projekt- und Graphrevision sowie ausdrückliche Owner-Freigabe |

Das Importprojekt erhält keinen Schreibzugriff auf das aktive Spielprojekt oder dessen Spielstände.
Generierte Skripte werden vor einer Ausführung geprüft; der Pilot enthält keine Produktionsgeheimnisse.
Ein Studio-Szenenexport ist nicht automatisch ein von World Forge akzeptiertes Modellartefakt.
Eine erforderliche Konvertierung muss eigene reale Eingaben, Werkzeugversionen und Ausgabehashes belegen.
Nicht unterstützte Szenen, Pakete und Skripte bleiben außerhalb des Asset-Intakes und werden separat geprüft.

Providerangaben sind zunächst Herkunftsbehauptungen; Byte-Hashes bestätigen nur die tatsächlich gelesenen Dateien.
Ein vorhandener Lizenzbeleg beweist keine rechtliche Freigabe. Der lesende Vertrag behält ausschließlich
`review-required` und `protected`; `owner-approved` und `imported` werden dadurch nicht zu gültigen Clientwerten.
Eine spätere Übernahme prüft die erwartete Revision erneut und bricht bei Drift vor jeder Mutation ab.

## Aktivierung in drei prüfbaren Stufen

1. **Vorschau/Link:** Freigegebene Belege und ein geprüftes Desktop-Ziel dokumentieren; keine automatische
   Kontoanlage, kein Upload und kein Providerauftrag. Ein Link ist noch keine technische Integration.
2. **Lokaler Importpilot:** Nach ausdrücklicher Zustimmung das isolierte Projekt und die benötigten Pakete
   einrichten; einen konkreten Export importieren, prüfen und mit echten Logs zur Entscheidung vorlegen.
3. **Belegter Providerbetrieb:** Erst nach geklärtem Zugang und dokumentierter Schnittstelle einen Adapter
   im bestehenden Integrationsweg umsetzen; Authentifizierung, Fehlerfälle, Abschaltung und Revisionen testen.

Vor Stufe 2 müssen Projektpfad, Unity-/Paketversion, Kontozuständigkeit, erlaubte Daten und mögliche Kosten
feststehen. Vor Stufe 3 müssen zusätzlich API-/Lizenzbedingungen, minimale Berechtigungen, Ressourcenlimits,
Protokollierung, Wiederholungs-/Abbruchverhalten und eine Rücknahme der Integration konkret prüfbar sein.
Fehlender Zugang oder fehlende Nachweise lassen die Funktion deaktiviert; es gibt keinen Ersatz durch Mocks.
Ein realer Pilot ersetzt weder die Prüfung im Zielprojekt noch dessen vorhandene Integrations- und Regressionstests.

## Unveränderte Wahrheitsgrenzen

Studio-Logic übernimmt keine Simulation, keinen deterministischen Core, keinen 100-ms-Takt und keine Spielstände.
Darstellungs- oder Prototyp-Logik aus einem Export erhält durch einen Asset-Import keine Gameplay-Autorität.
Diese Dokumentation installiert keine Pakete, eröffnet kein Cloud-Konto, lädt keine Assets hoch und bucht keine KI-Aufträge.
Sie verändert keinen aktiven Unity-Editor und erteilt weder Push-, Merge- noch Importfreigaben.
Bis zur gesonderten Umsetzung bleibt die Art-Ansicht die erste lesende Teilstufe von Lieferblock A.

## Primärquellen

[U1]: https://docs.unity.com/en-us/unity-studio/intro/unity-studio-requirements
[U2]: https://docs.unity.com/en-us/unity-studio/studio-importer/studio-importer
[U3]: https://docs.unity.com/en-us/unity-studio/studio-importer/studio-importer-instructions
[U4]: https://unity.com/products/unity-studio

- [U1 — Browser- und Kontovoraussetzungen][U1]
- [U2 — Importer, Exportinhalt und erzeugtes C#][U2]
- [U3 — Voraussetzungen des isolierten Importprojekts][U3]
- [U4 — Produktseite, Editor-Export und Roadmap][U4]
