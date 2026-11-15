# Ashen Crypt — Projekt- und Verlaufshandoff

Stand: 15.11.2026. Besitzerauftrag: aktuelle Spiel-APK erstellen, gesamten **Verlaufsstand** und konkrete Fortsetzung sichern; offene PRs bei ausreichender Kapazität prüfen und mergen. „Verkaufsstand“ war eine Spracherkennungsverwechslung. Dieses Dokument trennt vorhandenes Spiel, geprüfte Ergebnisse und noch geplante Arbeit.

## 1. Sofortüberblick für den nächsten Arbeitsbeginn

- Repository: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC
- Unity-Pin: **6000.6.8f1**. Ziel: Android ARM64 / IL2CPP, insbesondere Samsung Galaxy Tab A9.
- Kanonische Regeln: **AreArpg.Core**, bestehender **100-ms-Tick**, ganzzahlige/deterministische Simulation. Keine zweite Gameplay-Authority.
- Neuester vollständig gesicherter Spielcheckpoint: **9741e1d1fa44a76511109c18267061ce805eec0a**, Remote-Branch `checkpoint/worldforge-game-pause-20261115`.
- Tatsächlich nativ geprüfter und für die neue APK verwendeter Quellstand: **2f296b26767ad0746a2ca26468ef299f91e43b57**. Produktionscode identisch zu **603a30fe7f71fc594309ae8606d8d9441f265f4f**; 2f ergänzt nur Prüftreiberzeitlimit und Test-Save-Aufzeichnung.
- Der neue Wald-/Höhlenpilot ist **nicht gleichbedeutend mit einer abgeschlossenen Reise bis Immergrün**. Diese Reise, zwei Folgegebiete und Berufe bleiben unvollständig.
- Erste geplante Veröffentlichung bleibt die vom Besitzer gewählte **Prolog-Demo bis Aschenzoll**. Spätere Reiseerweiterung ist zusätzlicher Entwicklungsauftrag.
- GitHub-Abfrage am 15.11.: **keine offenen Pull Requests**. Keine PR blind erzeugen oder ungeprüfte Zweige mergen, um eine vermeintliche Warteschlange abzuarbeiten.
- Höchstens **ein aktiver Unity-Haupteditor**. Gemeinsames Unity-CPU-Budget derzeit **1 Kern** (`cpu.max = 100000 100000`). Frühere 2,5-Kern-Idee nicht als Erlaubnis interpretieren, den aktuellen Schutz bei Hoster-Drosselung aufzuheben.

## 2. Bisheriger Entwicklungsverlauf

### Spiel, Kampf und Erkundung

Der bestehende Prolog verbindet Einstieg/Dorf, Jägerrettung, alte Mühle, einen dreistöckigen verzweigten Dungeon mit Rattler, den prozeduralen Verbindungswald, die feste Bärenfamilienhöhle und Aschenzoll. Gegner, Truhen, Beute, Erfahrung, Ausrüstung und klassenloses Learning by Doing sind bereits Bestandteil des Spiels. 36 vorhandene Waffen-/Schildmodelle wurden mit sichtbar ausrüstbarer Beute verbunden.

Der Rattler ist nach dem ersten Storysieg erneut farmbar. Neue Kampfruns dürfen Gegner und Beute erneuern; die Brandmalentdeckung, Ilyans Bericht und abgeschlossene Entscheidungen werden dadurch nicht zurückgesetzt. Das Brandmal ist ein Lore-Hinweis, kein obligatorisches Tattoo-Modell und kein Beweis für einen persönlichen Befehl des Königs.

Der Wald wurde mit größeren und unterschiedlich skalierten Bäumen, Sträuchern, roten Pilzen und unebenem Gelände gestaltet. Ausgewachsene Bäume sollen mindestens ungefähr doppelte Menschenhöhe erreichen. Die gelbe Kapsel in früheren Designansichten war nur eine Größenreferenz.

### Jäger, Bär und Begleiter

Die Rettung des Jägers hat einen dauerhaften Ausgang. Ein lebender geretteter Jäger kann begleiten, warten, wieder aufgenommen werden, eigene Ausrüstung tragen und durch tatsächlich wirksame Aktionen trainieren. Seine Alleinkills vergeben **nur ihm** Charakter-XP; der Spieler trainiert durch eigene Aktionen. Beute bleibt einsammelbar. Tote werden durch normales Laden nicht wiederbelebt.

Die feste Höhle mit Bärenjungen und die dauerhafte Entscheidung sind Teil der bestehenden Route. Die spätere Idee, verschonte Tiere könnten einer Fraktion Kriegsbären ermöglichen, ist **Endgame-Design**, kein bereits umgesetzter Fraktionskrieg. Die Entscheidung darf später nicht durch einen erneuerten Kampfrun verschwinden.

Die gemeinsame Bogenanimation von Spieler und Jägerbegleiter wurde repariert und nativ regressiert. Der erworbene Inari-Hunter als neuer Straßenjäger/Begleiter, seine Materialien und die separate Straßenjäger-Animatorbindung bleiben ein eigener Integrationsblock. Ritter, Priester, Orks, Horseman und Borshin/Lavafaust-Bossmodelle wurden im Assetbestand geprüft; Import bedeutet noch keine fertige Gegnerrolle, Animation oder Bossintegration.

### NPC Memory Forge

Vorhanden: versionierte Archetyp-Controller, deterministische Utility-Bewertung, individuelle Erinnerungen und Beziehungen, Wahrnehmungs-/Zeugenherkunft, Save/Load und Begleiteranbindung. Das Spiel bleibt Eigentümer von Bewegung, Kampf, Inventar, Quests und Zeit.

Offline-Gewichtsexperimente wurden gegen einfache Utility-Policies verglichen. Ein nur auf Trainingsdaten erfolgreicher Kandidat wurde verworfen. Utility bleibt der Spielstandard; keine unkontrollierte neuronale Runtime-Lernschleife, keine zusätzliche C++-Authority. Native C++-Optimierung ist weiterhin nur bei gemessenem Nutzen und exakter Parität zulässig. Keine erfundenen Android-FPS- oder NPC-Skalierungswerte.

### Aschenzoll und Fadenwerk

Aschenzoll enthält den gewählten **leeren Tiertransport**. „Originalbeleg sichern“ und „Fracht tatsächlich verfolgen“ sind getrennte, gespeicherte Beweiszweige. Lebende Gefangene wurden dort nicht ohne Besitzerentscheidung hinzugefügt. Ilyans Bericht und lokale Wissensstände bleiben kausal.

Fadenwerks erste Browser-/Ilyan-Anbindung wurde mit PR **43** gemergt: `abc9eb01e8e654eb73e7656b9d5c00ff14a51190`. Browserautorisierung, Revisionsschutz und begrenzte Storytext-Anbindung sind vorhanden. Freie Autorenquests und Belohnungskits werden dadurch **nicht automatisch** zu kanonischen Ashen-Crypt-Quests. Das Spiel benötigt weiter seinen eigenen geprüften Adapter; keine parallele FadenwerkManager-Authority in den Spielstand einschleusen.

### World-Forge-Pilot

World Forge wurde tatsächlich für eine **128 × 128 m** große Waldverbindung eingesetzt: **16.641 Höhenknoten, fünf Wege, neun POIs**. Seeds A/B/A erzeugten Variation und die exakte Wiederholung der ersten exportierten Karte. Texturierung wurde im Autorenschritt wegen fehlender Regeln übersprungen; die Spielprojektion verwendet das vorhandene Bodenmaterial.

Die Spielanbindung verwendet gebundene, unveränderliche Ganzzahlkarten. Es existiert ein begrenzter Katalog gebackener Karten; die Core-Run-Generierung erneuert Wege/Begegnungen und die vierstöckige Seitenhöhle. Das ist **keine Zusage beliebiger neuer Unity-Terrainlandschaften bei jedem Runtime-Eintritt**. Dieser Unterschied zwischen Editorautorenschaft, Kartenkatalog und Laufzeitrun ist zu erhalten und für weitere Gebiete bewusst auszugestalten.

Die neue optionale Höhle hat vier Ebenen innerhalb der geforderten Spanne 3–6. Ein echter Unity-Test durchlief Kampf, Beute, Truhe, Speichern/Laden, alle vier Ebenen, Rückweg und neuen Run. Der Prüftreiber setzt früheren Storyfortschritt und hohe Waffenkraft voraus: Das ist ein Funktionsbeleg, **kein natürlich durchgespielter Balancingnachweis**.

## 3. Verbindliche geplante Handlung und Gameplay-Ziele

Die folgenden Entscheidungen sind festgelegt und müssen nicht nochmals abgefragt werden:

1. Immergrün liegt weit entfernt. Vorher trifft die Gruppe im nächsten Handelsposten/Dorf **außerhalb des Königreichs** einen vermeintlichen Frachtprüfer.
2. Er ist ein Spion eines anderen Königreichs. Er verlangt **Truppenstärke und Marschpläne** eines außerhalb stationierten Immergrüner Feldlagers. Diese Angaben könnten einen Angriff ermöglichen.
3. Als Gegenleistung verspricht er **Name und nächsten Übergabeort eines Transportvermittlers**. Das Versprechen und seine einzelnen Aussagen sind nicht automatisch bewiesene Hinweise.
4. In Immergrün begegnet die Gruppe **bei Torschluss hinausgeworfenen Menschen**, die auf einen möglichen Transportzeugen hinweisen.
5. Im ersten Gebiet nach Immergrün wird dieser Zeuge gesucht; im zweiten werden seine Angaben an einem weiteren Transportort überprüft.
6. Der Zeuge ist ein örtlicher **Meisteralchemist**, der zur Herstellung eines Betäubungsmittels für einen übergroßen Rattler gezwungen wurde. Er hilft und unterrichtet erst nach tatsächlichem Schutz vor seinen Auftraggebern.
7. Einführung zweier eigenständiger, steigerbarer Learning-by-Doing-Zweige: **Kräutersammeln und Alchemie**. Echte erreichbare Kräuterquellen, Zutaten im bestehenden Inventar, Verbrauch beim Brauen, Heil-/Bufftränke mit Core-Wirkungen und persistiertem Übungsfortschritt. Keine Umdeutung der vier bestehenden Kampf-Übungsfelder zu Berufen.
8. Jede Verbindungswelt soll mindestens einen erneuerbaren Seitendungeon/eine Höhle mit **3–6 Ebenen**, schwereren Gegnern und wertvollerer Beute in größerer Tiefe erhalten. Der bisherige Pilot erfüllt nur einen begrenzten Teil davon.
9. Feste Storyorte, Entscheidungen, Jägertod/Wartestatus und historische Controller-/Kartenbindung bleiben erhalten; äußere Wiedereintritte erneuern Kampfruns. Ebenenwechsel und Save/Load setzen denselben Run fort.
10. Erst die geplanten Gebiete spielbar vervollständigen, dann umfassend Gestaltung, Feinarbeit und Balancing abnehmen. Die Prolog-Demo hat trotzdem ihre eigene kleinere Release-Abnahme.

Offen bleiben insbesondere konkrete Namen/Herkunftskönigreich des Spions, Identität der Hintermänner, Schicksal der Vermissten und königliche Verantwortung. Bei neuen folgenreichen Lore-/Gameplay-Fragen den Besitzer mit **drei konkreten Vorschlägen** einbeziehen. Technische Routineentscheidungen autonom treffen.

## 4. Revisionsgebundene Nachweise und Grenzen

| Bereich | Tatsächlicher Beleg |
|---|---|
| Ältere vollständige Spielregression | Bogenstand `6b989e7a5cd85465933688d6f80c7a9d5d4b4846`: 129 PlayMode / 55 EditMode, 0 Skips; 8.112 Core-Assertions. Das ist nicht die vollständige Regression des neuen Piloten. |
| Fadenwerk | 79 EditMode auf `becf6e84ba3dbf311b6518a794a1d53de7dc64fd`; drei gezielte native Storyfälle auf `30e63df00f4251dba76509f239a479ca1e826cc9`. Früherer voller 130er-Lauf unterbrochen, nicht als bestanden zählen. |
| World-Forge-Core | 19.650 Assertions, 200 Runs; CI **43665081454**, Job **132662547800**, erfolgreich auf `603a30fe7f71fc594309ae8606d8d9441f265f4f`. |
| Karten-EditMode | 1/1 bestanden. |
| Begrenztes Terrain-Speichern | Echter RED und 17/17 GREEN auf `4c914a2d4efc145c33235ed5e1c6eeb38cdd4888`; keine globale Speicherung fremder schmutziger Assets/Szenen mehr. |
| Neuer nativer Gesamtpilot | 1/1 auf `2f296b26767ad0746a2ca26468ef299f91e43b57`, 192,3 s, 0 Skips. XML SHA256 `dfd5e6591657d7724e2bc1bcc184783a8d730936a71193248ea24e813feddf34`. |
| Save-Wiederherstellung | Separater vorbereiteter Test-Save lädt unabhängig exakt; 100 Core-Ticks reproduziert. Worldhash `7B865D4FAC38E01F`. |
| Neues Spiel-APK | Erfolgreich gebaut und geprüft: Quelle 2f296b26767ad0746a2ca26468ef299f91e43b57, ARM64/IL2CPP, gültige Signatur, ZIP-CRC und 16-KiB-ZIP-Ausrichtung. Native Builddauer 1781,71 s (29 min 42 s); kein Gerätebeleg. |
| Reales Tablet | Neuer Tab-A9-Installationstest, Touchprüfung, Performance, längere Kämpfe und Save/Load bleiben nötig. |

Wichtig: Ein Git-Checkout aktualisierte zuvor Unitys geladene Testassembly nicht zuverlässig. Vor neuen nativen Belegen explizit importieren/rekompilieren und die tatsächlich geladene Revision bzw. Testattribute lesen. Der erste Pilot scheiterte am 180-s-Zeitlimit; ein späterer Lauf mit alter geladener Assembly wurde korrekt als revisionsungebunden abgelegt. Beide Befunde sind erhalten und dürfen nicht nachträglich umetikettiert werden.

## 5. Lokale Arbeitsbereiche

| Pfad | Zweck und sichere Verwendung |
|---|---|
| `/workspace/ashen-apk-handoff-20261115` | Neuer isolierter Handoff-/APK-Dokumentationszweig `release/game-apk-handoff-20261115`, basiert auf dem vollständigen Checkpoint. Einstieg für diese Übergabe. |
| `/workspace/ashen-journey-20261114` | Ursprünglicher Reise-Arbeitsbereich; absichtlich umfangreicher uncommitteter Diff. Nicht resetten/cleanen. Der Inhalt ist im Checkpoint gesichert. Vor Fortsetzung gegen diesen vergleichen. |
| `/workspace/ashen-tools-drafts-20261115` | Store-Drafts, Produkttexte, Bilder und Uploadbelege; Branch `release/unity-tools-drafts-20261115`, Stand `fa714ab52a64a14642336809420846fed57475a3`. Kein Ersatz für die aktuelle Spielauthority. |
| `/workspace/artifacts/forge-store-presentation-20261115` | Offline-Vorschau `index.html`, Produkttexte, Anleitungen und Store-Bilder. |
| `/workspace/artifacts/Fadenwerk-WorldForge-Store-Drafts-20261115.zip` | Vorbereitetes Store-Übergabepaket. |
| `/workspace/artifacts/ashen-store-presentation/originals` | Echte Spielaufnahmen des Wald-/Höhlenpiloten; nicht mit den illustrierten Store-Covern verwechseln. |
| `/workspace/ashen-forge-release-1.0.3-20261113` | Separates Memory-Forge-Verkaufspaket und zugehörige Präsentation. Nicht dessen Beispiel-APK als Spiel-APK ausgeben. |

## 6. VPS-Verzeichnisplan

VPS: **46.202.154.25**. Unity-Dateieigentümer: **unityarpg**. Als root nur für erforderliche Infrastruktur arbeiten; Git/Unity regulär als unityarpg. Keine Kennwörter, Tokens, Schlüssel oder Session-Argumente in Ausgaben/Handoffs kopieren.

| Absoluter VPS-Pfad | Inhalt / Verwendung |
|---|---|
| `/var/lib/unityarpg/Unity/Hub/Editor/6000.6.8f1/Editor/Unity` | Gepinnter Editor. Kein stilles Versionsupgrade. |
| `/var/lib/unityarpg/Unity/Hub/Editor/6000.6.8f1/Editor/Data/PlaybackEngines/AndroidPlayer` | Android SDK, NDK, OpenJDK und Build Tools 36.0.0. Für echte Unity-APK-Builds verwenden. |
| `/var/lib/unityarpg/.local/bin/unity` | Offizielle Unity CLI. Aufrufe als unityarpg mit dessen HOME und explizitem `--project-path`; nicht versehentlich zweiten Editor starten. |
| `/var/lib/unityarpg/workspaces/are-arpg-forges-20261114` | Aktuelles natives Spiel-/World-Forge-Projekt, Quellstand 2f296b2, private Spielassets und Library-Cache. Für den aktuellen APK-Build verwendet. Vor jedem anderen Lauf prüfen, ob der Build/Editor noch aktiv ist. |
| `/var/lib/unityarpg/.local/share/are-arpg/game-apk-handoff-20261115` | Neuer APK-Job: `build.py`, `state.json`, `unity-build.log`, `runner.log`, `before.json`, `build-result.json`, APK und Verifikationsdateien. `owner-prefs.private` und `settings-before/` sind private Sicherungen, nicht verteilen. |
| `/var/lib/unityarpg/.local/share/are-arpg/world-forge-pilot-20261115` | Native World-Forge-Erzeugung, Test-XMLs, Skripte, Logs und `game-screenshots/`. `display.private` ist X-Authentifizierung, nicht veröffentlichen. |
| `/var/lib/unityarpg/.local/share/are-arpg/checkpoints/worldforge-pilot-20261115` | Vollständiges privates Projekt-/Spielstandbackup, Manifest, Git-Bundle, restaurierte Prüfdaten. Details unten. |
| `/var/lib/unityarpg/workspaces/ashen-tools-upload-20261115` | Separates minimales Projekt nur für Fadenwerk-/World-Forge-Store-Exports und offiziellen Uploader. Enthält keinen vollständigen Spielstand. |
| `/var/lib/unityarpg/.local/share/are-arpg/store-drafts-20261115` | Exportdateien, Uploadskripte, Publisher-Readbacks und Screenshot. Dateinamen: `Fadenwerk-1.0.0-draft.unitypackage`, `WorldForge-1.0.0-draft.unitypackage`. |
| `/var/lib/unityarpg/.local/share/are-arpg/asset-store-tools-12.0.0` | Inspektierte Quelle des offiziellen Uploaders; lokale Unity-6.6-API-Korrekturen sind in den Uploadbelegen dokumentiert. Store bot 12.0.6 an; 12.0.0 nicht als neueste Version ausgeben. |
| `/var/lib/unityarpg/.local/share/are-arpg/fadenwerk-web` | Private Editor-Verbindung und Token-Datei des Fadenwerk-Gateways. Token niemals in URL-Screenshots oder Logs veröffentlichen. |
| `/opt/ashen-fadenwerk/gateway.py` | Installationsziel des begrenzten Browser-Gateways laut Servicevertrag. Service `ashen-fadenwerk-web`, extern `https://arelogic.space/fadenwerk/`, intern Gateway 8096; Editorserver typischerweise 8095. Livezustand vor Nutzung erneut prüfen. |
| `/var/lib/unityarpg/.local/share/are-arpg/new-owned-assets-20261113` und `design-assets-20261114` | Inventar-/Importnachweise neuer gekaufter Modelle und Gestaltungspakete; Importbelege sind keine Gameplay-Abnahme. |
| `/var/lib/unityarpg/workspaces/are-arpg-asset-intake-unity6-20261109` | Alter großer Asset-Intake. Sein Editorbaum war pausiert, nicht als zweiten Editor aufwecken. Er ist nicht das aktuelle Spielprojekt. |
| `/var/lib/unityarpg/.config/unity3d/OuroborosCollective/ARE Ashen Crypt/prefs` | Besitzer-Spielprefs/Spielstand. Vor Test-/PlayMode-Läufen sichern und danach exakt wiederherstellen. Nicht mit vorbereitetem Test-Save ersetzen. |
| `/sys/fs/cgroup/system.slice/ashen-unity-budget.scope` | Gemeinsames Unity-CPU-Budget. Alle Editor-/Compiler-Kinder müssen darunter bleiben. Nicht die gesamte Scope stoppen: darin können erhaltene pausierte Prozesse liegen. |
| `/var/lib/unityarpg/.local/share/are-arpg/private-apk-server.py` | Ältere private APK-Auslieferung mit Revisions-/Hashkontrolle. Bestehende Routen nicht blind auf einen ungeprüften Build umbiegen. |

### Wiederherstellung des gesicherten Spielstands

Archiv: `project-and-saves.tar`, **3.411.281.920 Bytes**, SHA256 `8a35a06e7cf2ba697cb920e489b7bf2c697c300ea35779104e38e93e7502e98b`.

Git-Bundle SHA256: `6e53f9b4ffebdb1c69154b72c6dbb8542b01a100baaccd2097eff2d45683163e`. Manifest SHA256: `2426cf518e632176867aa62f3abfac83681c581fbd7995e7c486de9a73c1227e`.

6980 Dateien wurden isoliert restauriert und nach Größe/Hash verglichen. Die restaurierte Unity-Kopie wurde dabei nicht geöffnet; der restaurierte Test-Save wurde separat im Core gelesen. Erst Archiv prüfen, dann in einen **neuen** Ordner entpacken und Manifest vergleichen. Niemals über das aktive Projekt oder Besitzerprefs entpacken. Das Archiv enthält lizenzierte private Assets und darf nicht als Store-Download verteilt werden.

## 7. Maßgebliche Dateien im Repository

- `AGENTS.md`, `Memory.md`: Regeln und append-only Erkenntnisse. `ProjectState.md` enthält ältere Übersichten; für den Piloten die neueren Evidence-/Checkpointdaten verwenden.
- `Assets/Scripts/Core/ArpgSimulation.cs`: bestehende Gameplay-Authority; `JourneyLayout.cs`, `JourneySimulation.cs`, `JourneyState.cs`: neuer Reise-/Run-/Save-Bereich, einschließlich AC20. Ältere AC1–AC19-Verträge erhalten.
- `Assets/Scripts/Runtime/ArenaRuntime.cs`, `JourneyPresentation.cs`, `WoodlandPresentation.cs`, `CompanionPresentation.cs`: Unity-Projektion. Nicht hier eine konkurrierende Wahrheit erfinden.
- `Assets/Resources/ARE/JourneyMaps/`: gebundene Core-Karten; Hashbindung beim Laden nicht durch „neueste Karte“ ersetzen.
- `Assets/Editor/JourneyWorldAuthoring.cs`: begrenzte Autorenanbindung; `Assets/AshenCrypt/WorldForge/`: Weltwerkzeug; `Packages/com.ouroboros.fadenwerk/`: Autoren-/Runtimewerkzeug.
- `Assets/Editor/ArpgBuildTools.cs`: tatsächlicher APK-Buildweg `AreArpg.EditorTools.ArpgBuildTools.BuildAndroidApk`, Einstieg `Assets/Scenes/AshenEntry.unity` plus Legacy `Arena.unity`.
- `Assets/Tests/PlayMode/EntryJourneyRuntimeTests.cs`, `JourneyPilotRuntimeTests.cs`; `Assets/Tests/EditMode/JourneyMapEditModeTests.cs`; `Tests/CoreVerification/JourneyPilotTests.cs`: gezielte neue Regressionen.
- `docs/evidence/worldforge-pilot-20261115/`: echte Testresultate, fehlgeschlagene Anläufe, Revisionen, Backup-/Save-Readbacks.
- `.sdlc/runs/ASHEN-001/manifest.yaml`: Workflowauthority. Aktuell `blocked`, MOB-001 wegen früherer Besitzerpriorität pausiert; nicht handschriftlich auf completed setzen.
- `.sdlc/runs/ASHEN-001/artifacts/ba/`: 19 Requirements/33 Acceptance Criteria und Traceability. `facts.yaml`/semantische Claims und `docs/specs/immergruen-journey/owner-decisions.md` bewahren den Besitzerkanon.
- `docs/SPEC_DIRECTOR_WORKFLOW.md`, `Tools/Director/route_work.py`: Spec Kit definiert Vertrag, SPD plant/reviewt, Director routet und bindet Ausführung/Evidence. Keine unbeaufsichtigten Modellsitzungen oder neue Authority daraus ableiten.
- `Tools/Fadenwerk/`: Gateway, Service-/Routervorlagen und Startskript. Vor Anbindung Editorserver starten; geschlossener Editor bedeutet bewusst offline/503.
- `.github/workflows/core-checks.yml`, `unity-playmode-smoke.yml`, `unity-android-build.yml`: getrennte CI-Wege. Cloud-Unity benötigt gültige Lizenzkonfiguration und private Assets; vorhandener Workflow allein beweist keine Baubarkeit des vollständigen Spiels auf GitHub.

## 8. Konkrete nächste Schritte

1. **GitHub-gehosteten APK-Build einrichten:** aktuelle APK/Übergabe ist abgeschlossen. Lizenz, private Assetinputs, Signierung und gepinnte Toolchain gemäß Abschnitt unten prüfen, danach ersten tatsächlichen Cloud-Build nachweisen. Den VPS nicht als Self-hosted Runner verwenden.
2. **Auf dem Tablet abnehmen:** vorhandenen Spielstand sichern; APK installieren/aktualisieren; Einstieg, Touch, Kamera/Zoom, HUD-Sicherheitsflächen, Kampf, Inventar, Jäger, Save/Load und Rückkehr in erneuerte Runs prüfen. Bei Signaturkonflikt nicht ungefragt deinstallieren. Tatsächliche Logcat-Ausgabe für Fehler nutzen.
3. **Arbeitsauthority bewusst wieder aufnehmen:** aktuelle Besitzerpriorität jetzt APK/Handoff. Für weiteren Featureausbau nach Fortsetzungsauftrag den MOB-001-Blocker mit `node .sdlc/runtime.cjs` über die unterstützten Lifecycle-Befehle lösen (zuerst `--help` lesen). Manifest nicht manuell umschreiben; keine doppelte Reise-Spezifikation erzeugen.
4. **Piloten vollständig integrieren:** Grenzen der gebackenen Karten und erneuerten Runs prüfen; nativen Missing-Map-Fall nachholen, vollständige relevante Regression an der endgültigen Integrationsrevision ausführen, finalen Diff reviewen. Der Checkpointbranch ist kein Main-Merge. Danach gezielt einen überprüfbaren Integrations-PR erstellen.
5. **Reise in spielbaren Abschnitten ausbauen:** Verbindung → ausländischer Handelsposten/Spion → Lageraufklärung → tatsächliche Übergabeentscheidung/Transportspur → weitere Reise → Immergrün bei Torschluss. Pro Verbindungsgebiet eigene erreichbare 3–6-Ebenen-Höhle mit skalierenden Gegnern/Beute; Save-/Run-Grenzen beweisen.
6. **Zwei Folgegebiete:** Meisteralchemist finden und schützen; dann Angaben am zweiten Transportort verifizieren. Keine Täter-/Königsschuld vorwegnehmen.
7. **Berufe:** eigenständige Herb-/Alchemy-Datenverträge und Progression, erreichbare Kräuter, atomarer Zutatenverbrauch, Trankausgabe und tickgebundene Heil-/Buffwirkung. Save/Load, Wiederholung und Begleiterabgrenzung testen. AlchemyLabProps-Innenraumset (120 Prefabs laut Inventar) gezielt einsetzen, nicht sämtliche Proppakete laden.
8. **Darstellung und Release:** Inari-Jäger sowie geeignete Ritter/Priester/Orks/Bossrollen separat integrieren; Fable-HUD/Kamera erhalten. Danach Balancing, Audio/Animation, Gerätelast, Onboarding und Prolog-Release-Gates abschließen.

Bei Bugs zuerst echten fehlschlagenden Verhaltenstest, danach Fix und benachbarte Regression. Nach jedem abgeschlossenen Integrationsblock genau einen kurzen Memory-Eintrag mit Änderung, Erkenntnis und Evidence. Keine erfundenen Runtime-Beweise oder prozentualen Beschleunigungsversprechen.

## 9. Verkaufsmodule — nur zur Vollständigkeit

- Memory Forge: zuletzt tatsächlich im Publisher-Readback **pendingReview**, Produkt 429860 / Version 1557618; Preisvorgabe 9,50 USD. Kein beobachteter Freigabe-/Liveverkaufbeleg.
- Fadenwerk: Produkt 430392 / Version 1558990; exakter Export 85.364 Bytes, offiziell hochgeladen und nach Serverrefresh **Draft**; Preisvorgabe 6 USD.
- World Forge: Produkt 430394 / Version 1558994; Export 820.669 Bytes, hochgeladen und **Draft**; Preisvorgabe 35 USD.
- Besitzer meldet inzwischen „alles erledigt soweit“ nach manueller Pflege der Storetexte/Bilder. Das ist **Besitzerrückmeldung**, kein neuer unabhängiger Portal-Readback. Nicht daraus automatisch „veröffentlicht“ ableiten.
- Beide Pakete haben weiter Release-Gates: eigenständige Beispiele ohne private Spielassets, Fadenwerk-UPM-Beispielpfad/Anleitung/Lizenzvorlage, World-Forge-Spieltestabhängigkeit und Modul-Setup, frische Import-/Funktionsprüfung und finale Storevalidierung. Upload als Draft ist keine technische Verkaufsabnahme.
- Store-Evidence liegt im separaten Zweig `release/unity-tools-drafts-20261115`, SHA `fa714ab52a64a14642336809420846fed57475a3`.

## Neueste Besitzersteuerung: künftige APKs auf GitHub bauen

Der Besitzer fragt während des laufenden APK-Builds ausdrücklich nach GitHub-Runnern, um die VPS-CPU zu entlasten. **Die Umstellung auf einen GitHub-gehosteten Android-Build ist der nächste Infrastrukturpunkt.** Ein selbst gehosteter Runner auf demselben VPS würde das Lastproblem nicht lösen. Der bereits laufende VPS-Build wurde erfolgreich beendet; kein zweiter Build wurde gestartet.

Die aktuelle GitHub-Integration darf Actions-Secrets und Repository-Variablen nicht einmal auflisten (HTTP403). Ihre Existenz ist deshalb **unbekannt**, nicht als fehlend belegt. Für die Einrichtung ist geeigneter Repository-Settings-Zugriff nötig.

Vorhanden ist `.github/workflows/unity-android-build.yml` mit `ubuntu-latest`, Unity6000.6.8f1, Android/IL2CPP-Buildmethode und APK-Artifact-Upload. Ein erfolgreicher Lauf dieses vollständigen Spielworkflows ist bisher nicht belegt. Die Voraussetzungen sind gezielt herzustellen:

1. Für den aktuellen Unity-Account zulässige CI-Lizenzierung/Activation prüfen und über GitHub Secrets konfigurieren. Native Hub-Anmeldung ist kein Beweis einer funktionierenden Cloud-Lizenz; keine Zugangsdaten oder Lizenzdateien in Git schreiben.
2. Die notwendigen privaten, lizenzierten Spielassets als revisionsgebundenes, hashgeprüftes Inputpaket geschützt für den privaten CI-Job verfügbar machen. Nicht das komplette VPS-Backup mit Spielständen und privaten Einstellungen hochladen. Speicher-/Transferkontingente berücksichtigen.
3. Unity6000.6.8f1 und tatsächliche Verfügbarkeit des passenden Android-Runner-/Container-Toolchains verifizieren. Library-/IL2CPP-/Gradle-Caches nach Unity-Version, Plattform, Packages und Asset-Input-Hash trennen.
4. Erst einen manuellen Cloud-Build der bekannten Spielrevision ausführen; APK-Herkunft, ARM64, IL2CPP, Signatur und Ausrichtung wie beim nativen Build prüfen. Den bisherigen App-Signierschlüssel nur über einen geschützten Secretweg übernehmen, damit Updates vorhandene Saves erhalten können.
5. Private APK-Artefakte mit begrenzter Aufbewahrung ausgeben; Dauer und VPS-Entlastung messen. Keine Beschleunigung in Prozent vorwegnehmen. Erst nach erfolgreichem Vergleich zum Standardweg machen.

## Abgeschlossener APK-Auftrag und Download

Download mit bestehendem privaten Zugang: https://arelogic.space/arpg-apk/Ashen-Crypt-2f296b2-20261115.apk

- Datei: **242.200.939 Bytes** (242,2 MB), SHA256 **a8cc3241790dbd8b55f74f5368e770ae0ea517dcdba4c9e550e2d3fd6497850f**.
- Quelle: **2f296b26767ad0746a2ca26468ef299f91e43b57**, Unity6000.6.8f1, ARM64/IL2CPP, App-ID `com.ouroboroscollective.areashencrypt`.
- Unity-Exit0; Builddauer **1781,71 Sekunden**. Signatur gültig und identisch mit der früheren HUD-APK; ZIP-CRC, IL2CPP-Inhalt und 16-KiB-ZIP-Ausrichtung geprüft. Kein physischer Tablet-, Performance- oder vollständiger ELF-/Gerätekompatibilitätsnachweis daraus ableiten.
- Private Assets und Besitzerprefs unverändert. ProjectSettings restauriert; drei vom Build erzeugte URP-Assetänderungen vor Wiederherstellung separat gesichert. Getrackter Spielbaum wieder sauber.
- Unabhängiger SHA-/Größenreadback stimmt mit APK-Beleg und vollständigem HTTP-Readback des lokalen Live-Dienstes überein. Bestehende Routen bleiben erhalten, anonymer HTTPS-Zugriff liefert wie vorgesehen401; Authentifizierung nicht verändert.
- `state.json` meldet completed; `finalization-status.json` complete. Unabhängige Prozessprüfung: kein Unity-Haupteditor mehr für das Spielprojekt. Alte pausierte Intake-Prozesse wurden nicht wieder aufgeweckt.
- Belege: `docs/evidence/apk-handoff-20261115/` mit Buildresultat, APK-Receipt, Auslieferungsreadback und unabhängiger Prüfung. Native Originale liegen in `/var/lib/unityarpg/.local/share/are-arpg/game-apk-handoff-20261115`.
- Ausgelieferte Datei: `/var/lib/unityarpg/.local/share/are-arpg/private-apk/Ashen-Crypt-2f296b2-20261115.apk`. Der abgeschlossene Finalizer darf nicht erneut gestartet werden; Auslieferung verweigert absichtlich ein Überschreiben vorhandener Dateien.

Die allgemeinen Android-Connectorprüfungen bezogen sich auf einen anderen, Capacitor-basierten Studio-Workspace und sind kein Ashen-Crypt-Buildbeleg. Für diese APK gelten ausschließlich Unity-Quellrevision, native Buildausgabe und APK-Inspektion.

## 10. Startprompt für eine neue Sitzung

> Setze Ashen Crypt anhand dieses Handoffs fort. Lies zuerst AGENTS.md, Memory.md, die aktuelle ASHEN-001-Authority, Besitzerentscheidungen und die neuesten APK-/World-Forge-Evidence-Dateien. Prüfe den tatsächlich vorhandenen Git-/VPS-/Editorzustand, bevor du etwas startest. Erhalte den 100-ms-Core, historische Saves, private Assets, Fable-HUD und Kamera. Höchstens ein aktiver Unity-Editor im gemeinsamen Ein-Kern-Budget. Berichte zunächst den belegten Stand und erledige den nächsten angeforderten Block; neue folgenschwere Lore-/Gameplayfragen mit drei Vorschlägen an den Besitzer. Keine bestehenden uncommitteten Arbeiten verwerfen und keinen Store-Draft ungefragt veröffentlichen.
