# Ashen Crypt evidence logbook handoff

Owner instruction of 2026-11-16: maintain one persistent document named `Ashen_Crypt_Evidenz_Logbuch.docx` using Design Report. Append the last concrete steps after each completed work round so the next agent can continue. Preserve old entries; corrections are new dated entries. This repository is the independent Unity ARPG, not Echoes of Aurion.

The document contains five rounds: selected material intake; tick-projected sky and import limits; moonlight and same-tick rebuild fixes; readable twilight and final native tests; publication and documentation. All eight pages were rendered and inspected. Private file identity and version are held on the authorized VPS at `/var/lib/unityarpg/.local/share/are-arpg/sky-native-20261116/logbook-reference.json`; keep that record private and replace the same document identity on each update. Do not create a new unrelated logbook each round.

## Verified publication checkpoint

- Base main: `193c1466e9256185b6980f9ee6a62c3853676813`.
- Native-tested code: `24c4fdd13f896afd18c54bc342aa3ab8ae483937`.
- VPS delivery with evidence: `b28c8585ee7de493a5e35a9a8657a562bc99ec6b`.
- GitHub API publication: `d7cc0645bd2f9577da2a7e9d04539f676e5123b0`.
- Exact identical full delivery tree: `3a6aaed2035ad91b7b77c740b90856a106f91f90`.
- Draft PR: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/49 ; quality epic #48 stays open.
- Source of test counts and independent hashes: `acceptance.json` beside this file. Native 6/6 PlayMode and 1/1 EditMode on 24c4fdd1, zero skips. Four actual phase-camera fixtures reviewed. Core 19,739 on 673c3bec with Core/test trees byte-identical to 24c4fdd1; 40 preflights passed.
- GitHub Core run https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/43809797562 succeeded for the published head association. Job 133108668799 executed the real C# simulation, Fadenwerk roundtrip, map immutability, static preflight and policy checks.
- GitHub licensed Unity run https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/43809797557 was skipped. It is not native success.

The VPS had no credentials for canonical HTTPS push. API publication therefore has a different commit identity. The tree was independently fetched back on the VPS and matched exactly. Never relabel the API SHA as the SHA natively executed. This follow-up changes only AGENTS.md, Memory.md and this handoff.

## Continue from here

Inspect the current owner document, Memory.md, ProjectState.md and acceptance.json before edits. Use the prepared isolated code workspace and private assets. The documentation workspace `/var/lib/unityarpg/workspaces/are-arpg-logbook-20261116` starts from the published d7cc0645 tree; it is not the native Unity project with private art.

Next visual block: define the village arrival hero view, place selected native-verified Viking props, refine terrain/path materials and spatial composition, then inspect real native images. Keep Fable HUD, camera contracts, save owner and 100-ms Core. WorldSkyClock is read-only; shader/cloud phase follows existing persisted ticks.

Remaining acceptance gates: village composition and placement; forest/mill/caves; broader visual pass; a new Android build; physical Tab A9 readability and performance. Do not claim these are complete. Native tests here were targeted, not the entire Unity test suite. No merge or release is authorized by this checkpoint.

## Round entry contract

Each new round records UTC date, incoming and resulting revisions, exact changed production/test/persistence/presentation surfaces, causal finding, real commands or native run labels, independent readback/hashes, failures and corrections, explicit limits, next step, and PR/CI state when relevant. Keep test fixtures distinct from owner runtime and device evidence. After saving the same document, update its private version record and a concise repository handoff. If document access is unavailable, preserve the round in the repository and report that document writeback is pending; never silently claim it was updated.

## Village hero pass - native continuation 2026-11-16

- Current local branch: feat/village-hero-20261116, actual code commit 0e7db89fc8f2f14bbdb311193dbfb1a4a6014b8c and exact test code tree b614948de863d7c671aa2693f0594cdd62e7eb37. Not pushed and not merged. Source/test private asset boundaries unchanged.
- Eight native Viking decorative groups selected using integer millimetre placement, written to git-ignored private EntryVillageWorld.prefab; actual SHA-256 b96348497beec31ea8cea3c95e10fdca5de7279e376367456526e5b7606d2f77. Original Unity Store sources and GUIDs not committed.
- Unity6000.6.8f1 native build hero-build-sparse-02 passed; EditMode hero-edit-sparse-03 1/1 and PlayMode hero-play-sparse-08 1/1, no errors/skips, real 1280x720 camera captures and unchanged world hash/save assertions. Tor dimensions after correction 4.893 m visible width, 5.400 m height, 0.545 m depth. RED findings included edge-on 0.223 m width, missing real NPC animation controller in test overlay, and camera override reset on LateUpdate. Replaced insufficient green checks with real origin, dimensions, pixel-diff and passive-component constraints.
- Core retested on committed source: 19,857 deterministic assertions; 40/40 Python preflight checks passed including real Unity MonoImporter .meta integrity. No changes to Core authority, runtime Fable camera/HUD or ProjectSettings. Full hashes and run receipt IDs: docs/evidence/village-hero-20261116/checkpoint.json.
- Unity shared CPU guard currently 300 percent; old paused editor PID 98453 untouched. Owner persistent DOCX identity Ashen_Crypt_Evidenz_Logbuch.docx unavailable from this workspace, and is NOT claimed updated. This repo handoff and private Cloud checkpoint are current extension, preserving older rounds.
- Next: refine real village entrance ground texture blending, foliage distribution, market/wall silhouettes and lighting, then native screenshots plus separate Android/Tab-A9 device. Forest/mill/caves still open, no APK/PR release claim.
\n## Village Approach / Erd-Pflaster-Federung – 16.11.2026 (Folgerunde)\n
- Lokaler isolierter Branch `feat/village-approach-20261116`, native Code-Revision `9a0a259179e048bcb651f9a5fbd2cd3ccb9f85f3`, Tree `b2e2c8caea7edf696fb7ac1da1a37496e106a875`; basiert auf Dorf-Hero-Head `512733d6ac0560bd425ea7d8630f272cc3175759`. Kein Core-, Save-, Gameplay-, Fable-HUD- oder Runtime-Kamera-Diff.
- Realer erster Unity-Prüfversuch scheiterte **vor** Teststart an einem Log-Verzeichnis (Exit127); isolierte Berechtigung repariert. RED `approach-edit-red-02`: 0/1, XML `9c04e4fbdd130c348656fb91d306d93af65100d2c615c169883b568740e1e31f`, konkrete Ursache fehlt `VillageApproachPass1`.
- Native Builder `approach-build-01` Exit0; privates Prefab SHA256 `57c451d2cf2e53996755ceeb8f47978f92494c81622137cec3f179ed56074ab1`. GREEN EditMode1/1 XML `57cd8934a19522a5c010c9d9567afc4749abe0cd6201ae229b14e6fb61fe8625`; GREEN PlayMode1/1 XML `e96dccf5ad7bd2e4572343725628def463ba7ee022a231999a1afc73cd46feb3`; alte Hero-EditRegression1/1 XML `9bd22b62f70f6e379d6e0401cfdec770de76eacc7b8c372d40d9c0a25250d9ab`. Keine Skips. Einstellungen/Prefs in allen abschließenden Läufen wiederhergestellt.
- 19.857 Core-Assertions/40 Preflights auf Code-SHA grün. Vorher-/Nachher-GameView jeweils 1280x720; neuer Bild-SHA256 `25c1dbddcd961a03d982fd7a2bd8b43cd9ee507020fb6be114eab0fb0b17bb14`, Straßen-ROI-RGB-Differenz 6,8849% größer 16. Kein Android-Build oder Tab-A9-Performancebeleg.
- Siehe `docs/evidence/village-approach-20261116/checkpoint.json` und `docs/visual-pass-1/village-approach-20261116.md`. Der Owner-DOCX `Ashen_Crypt_Evidenz_Logbuch.docx` war weiterhin nicht editierbar zugänglich und wird **nicht** als aktualisiert ausgegeben. Nächster Slice: tickgebundene Dorf-Lichtinseln mit begrenztem Lichtbudget und nativer Nacht-/Dämmerungs-Gegenprobe; Review/PR bleibt vom erfolgreichen neuen GitHub-CI-Readback abhängig.

## Loot C / Teil 1 – Münzanzeige Kupfer/Silber/Gold – 17.11.2026

- Branch `feat/loot-c-currency-display-20261117` (PR #55). Code-Commits `2bb20a3`..`b7c57b3`; nach dem Merge der PRs #49–#54, #56 und #57 nach `main` zusätzlich Merge-Commit `787af9e8fca9f68d70d69a5ec255cea9e8dbbdcd`, der nur den Anhänge-Konflikt in `Memory.md` löst (beide Einträge `fable-vfx-recovery-20261117` und `loot-c-coins-20261117` bleiben erhalten).
- Geändert: neue reine Projektion `CoinDenominations` (100 Kupfer = 1 Silber, 100 Silber = 1 Gold) für den vorhandenen BigInteger-Münzbestand; Anzeige im Inventar-Beutel und im NPC-HUD. Kein Save-Format-, Core-Autoritäts- oder Gameplay-Regel-Wechsel; der AC-Checkpoint bleibt bytegleich.
- Evidence: Core TDD-RED `22acf027897c4f9aa76100b1348be531f3531702` (Compilerfehler der fehlenden Projektion), GREEN `3d864070dd54c8dccfba7cd4a345d302300780cb` mit 19.866/19.866 Assertions; Unity 6000.6.8f1 PlayMode 9/9, 0 Skips, XML-SHA256 `91a794c3462586ec2ccba4a2f752908cbf8ef55bb52d3a90f6d8f6eb8925a660`. GitHub-CI auf `787af9e8`: Core erfolgreich, Spacefast-Build erfolgreich, Unity-PlayMode-Job übersprungen (keine Lizenz in der CI, kein PASS).
- Grenzen: echte Loot-C-Vorratsbeutel, private Münz-/Beutel-Prefabs, deren Persistenz, Android-Build und Tab-A9 sind nicht umgesetzt bzw. nicht geprüft. Der Owner-DOCX `Ashen_Crypt_Evidenz_Logbuch.docx` ist aus dieser Umgebung weiterhin nicht editierbar zugänglich; das Zurückschreiben dorthin ist **ausstehend** und wird nicht als erledigt ausgegeben.
- Nächster Schritt: Loot C / Teil 2 (sichtbare Vorratsbeutel als eigener Drop, getrennt von Waffenbeute) erst nach Abgleich mit Maras Rationen-Ökonomie; parallel Bodenkontakt der Dorfbewohner (Folge von PR #57) nativ abschließen.

## Dorfbewohner im Boden – 17.11.2026

- Branch `fix/village-npc-ground-20261117` von `main` `507c0f4d884a8584d352528f4726d5eb097965ca`. Nur Darstellung: kein Core-, Save-, HUD- oder Kamera-Diff.
- Ursache: privater Controller `AshenHumanoidMovement` ohne importierte Clips; humanoider Animator setzt die Hüfte auf den Ursprung, Mara/Ilyan stehen gemessen −0,91/−0,92 m tief. Ersatz-Controller griff nur bei `null`.
- Fix: `PassiveArtVisuals.HasPlayableClip` und `EnsurePlayableHumanoidLocomotion` für alle geerdeten Bindungen.
- Review-Korrekturen (PR #58): Straßenzeuge wird in Projektionshöhe erzeugt (RED: 1,0 m schwebend), generische Rigs behalten ihren eigenen Controller.
- Evidence an Commit `29120c45b9c5144e2a7f492dd39050f82170af82` gebunden: Unity 6000.6.8f1 Live-Editor des Prüfprojekts, EditMode 4/4, Straßenzeuge PlayMode 1/1, PassiveArtVisuals PlayMode 9/9, NPC-Bodenkontakt grün (+0,005/+0,002 m); Core 19866, Preflight 40. Details: `docs/evidence/village-npc-ground-20261117/acceptance.md`.
- Grenzen: Held geht lokal bis ~8 cm zwischen erhöhten Pflastersteinen ein (Bodentest meldet das weiter); Übungsschwert erscheint im Prüfprojekt als Brett; Android/Tab A9 ungeprüft. Owner-DOCX weiterhin nicht editierbar zugänglich, Zurückschreiben ausstehend.
- Nächster Schritt: Waffenarten-Fähigkeiten im RuneScape-Stil (Owner-Auftrag 17.11.2026).

## Waffenkünste im RuneScape-Stil – 17.11.2026

- Branch `feat/weapon-mastery-20261117` von `main` `c839b68aaace5e14288f9969b956c3705ec3c033`, Feature-Commit `60a664b4a4a23e91aaaa507ee271a0d8c43b3b63`.
- Geändert: Core-Regeln und -Zustand (`WeaponMastery.cs`), Training im kanonischen Spieler-Schadenspfad, Wertepunkte über `TrySpendMastery` (getrennt gespeichert), AC21-Spielstandhülle, exakte Schadensvorschau `CombatRules.PreviewDamage`; Menüreiter FÄHIGKEITEN (Taste P).
- Evidence: Core 19899/19899 (RED vorher: Build-Fehler ohne Implementierung), Preflight 40/40, Unity 6000.6.8f1 Menütests 2/2, EditMode 107 (83 bestanden, 0 fehlgeschlagen, 24 übersprungen), PlayMode 165 (151 bestanden, 14 fehlgeschlagen; alle 14 auch auf unverändertem `main` im Prüfprojekt). Details und SHA-256: `docs/evidence/weapon-mastery-20261117/acceptance.md`.
- Prüfumgebung: Die Testsuite des lokal eingebundenen UnitySkills-Pakets (2866 Tests) lief ungewollt mit und legte Dateien im Prüfprojekt an; Lauf beendet, Rückstände gesichert und entfernt, Paket-Tests in der privaten Paketkopie ausgeblendet (`Tests~`).
- Grenzen: Schilde ohne Fähigkeit (offene Owner-Entscheidung), Android/Tab A9 ungeprüft, Owner-DOCX weiterhin nicht editierbar zugänglich, Zurückschreiben ausstehend.
- Nächster Schritt: Owner-Entscheidung zur Schildfähigkeit, danach Umsetzung.


## Waffenarten, Schild und Bogen; Review-Fixes; auf main rote Tests repariert – 18.11.2026

- Branch `feat/weapon-mastery-20261117` auf `main` `c839b68`: `0262d1c` (Profile, Schild, Bogen, Review-Fixes), `788c064` (Merge der CodeRabbit-Tests `c29f0e7`), `27e8c9b` (auf main rote Tests), `c2364e4` (unvollständige Figuren verworfen), `7f834a8` (Lager- und Ankunftstests mit fester Frame-Zeit), `a0162e5` (zweite Review-Runde: Steinplatte, Schleichen, AC21-Kopf).
- Geändert: Core (`WeaponProfiles`, AC21-Ladekette mit vorab gelesenen Waffenrängen, `MayHoldJourneyProgress`), Runtime (Speichern bei Stufenaufstieg, Lauffläche im Dorf, Lesezeit-Sperre und Diebstahl-Bestätigung auf der Spieluhr, Binder lehnt Figuren mit ungelösten Skinned Meshes ab), Editor (Pilotprofil vor dem Biome-Asset speichern), Tests (Review-RED/GREEN, feste Frame-Zeit für die Zeit-Tests, `IncompleteCharacterArtTests`).
- Prüfprojekt B: 338 fehlende private Dateien aus C ergänzt (nichts überschrieben, nicht im Repository). Ein modaler „Scene(s) Have Been Modified“-Dialog nach einem abgebrochenen Lauf wurde mit „Don't Save“ geschlossen.
- Evidence: Core 20911, Preflight 40, EditMode 109/109, PlayMode 169/169 auf `a0162e5`; Vergleich aller main-roten Tests auf `c839b68`; Details und SHA-256 in `docs/evidence/weapon-families-20261117/acceptance.md`.
- Grenzen: Dorns privates Modell bleibt unvollständig (das vollständige Ersatzmodell ist sichtbar); Android/Tab A9 ungeprüft; ein vierter Workspace `are-arpg-pr52-review-20261117` existiert (nicht von dieser Runde angelegt); Owner-DOCX nicht beschreibbar, Zurückschreiben ausstehend.
- Nächster Schritt: Owner-Entscheidung zum Charakter des Stabs, danach Loot v2.
## Neural Director: gemeinsamer Architektur-Snapshot – 18.11.2026 [shared-architecture-council-20261118]

- **Scope:** Keine Unity-/Core-/Asset-Mutation, kein zweiter Editor oder MCP-Broker. Die bestehende `docs/ARCHITECTURE.md` um den markergebundenen Shared-Architecture-Abschnitt ergänzt; alte Architekturbeschreibung unverändert bewahrt. `Tools/Director/shared_architecture.py` friert Dokument-/Katalog-SHA256, Git-SHA-Behauptung und Unity-6000.6.8f1-Pin als einen 28-Pakete-Snapshot ein. `test_shared_architecture.py` prüft Gleichheit, Drift, Manipulation, Begrenzung und drei getrennte Vorschläge.
- **Erkenntnis:** Gleichzeitiges Lesen desselben Snapshots ist sicher; gleichzeitiges Überschreiben der kanonischen Markdown-Datei wäre Last-Writer-Wins und daher verboten. Alle 28 beraten nur und geben `ABSTAIN` oder referenzierte Vorschläge ab. Einzelausführung/AI-Dispatch wird noch **nicht** behauptet; Git-Schreibrechte bleiben beim autorisierten Director mit Blob-SHA-CAS und Readback.
- **Evidence:** Ausgangs-PR-Head `40a8211049472f7e3dbf665c429e1c8f4549ce80`; neuer Code/Tests/Architektur in Draft-PR #87. Lokale reine Python-TDD-RED (fehlendes Modul, später Paket-Manipulation) und GREEN 10/10; exact-head CI, VPS-Router, Editor und Android bleiben bis zum separaten Readback ungeprüft.
- **Grenzen:** Owner-`Ashen_Crypt_Evidenz_Logbuch.docx` noch nicht sicher schreibbar identifiziert und unverändert. Bestehender Core, UI und Game Director bleiben Authority. Nach CI: echten hostgebundenen Advisory-Dispatcher mit vorhandener Sovott-Authentifizierung pilotieren, externe Maven-/UPM-/MCP-Effektgrenzen separat.
## Neural Hub MCP Test – 2026-11-18 [neural-mcp-hub-pilot-20261118]

- **Umfang:** Im PR #87 existiert ein einzelner mcp-use-v2.12.7-Server mit 28 spezialisierten `advice_*`-Tools, 28 Domain-Prompts und zwei Director-Tools (read-only). Gemeinsame `docs/ARCHITECTURE.md` und `neural_catalog.json` werden byteweise SHA-256-geprueft. Nur bekannter Unity-Pin `6000.6.8f1`; keine laufenden 28 KI-Modelle, keine externe Editorbridge.
- **Evidence:** Isolierter Git-Checkout `3d6fd8882301a948d0855e1d928d96423b6a658e`; node:test 10/10, Python-Director 48/48, echter MCP-JSONRPC/SSE-Canary (`initialize`, `tools/list`, `prompts/list`, `tools/call` fuer Snapshot/World/Director), 30 Tools + 28 Prompts erfolgreich. Paket-Lock SHA256 `bea2aed228c53f4e1eab9d587e06c863268ef7cf02edf132e49dddbbfc9a8e83`, Audit 0 bekannte Advisories zum Pruefzeitpunkt. Docker Image `sha256:d95ebc34e8d2ce1e30b9aeebfba757f13e23db06826731fbb4f48af52f308f33`, 387058824 Bytes; Container-Canary `--network none --read-only --cap-drop ALL` PASS, keine dauerhaft laufenden Neural-Container. Separater Broker `sovereign-chatgpt-mcp` weiter RUNNING.
- **Vertrag und Erkenntnis:** 28 gleiche Architektursnapshots und fachlich begrenzte Beraterantworten statt 28 Ausfuehrungsautoritaeten; eingereichte Issue-Snapshots werden trotz erlaubter GitHub-URLs als `SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED` bezeichnet. Nur drei unterscheidbare, belegte Tracks werden gezeigt. Ausfuehrung `NONE`, Unity/Android/Runtime `NOT_EXECUTED`.
- **Status:** Erstes isoliertes Image und MCP-Protokoll PASS; neue `neural-readonly-mcp`-GitHub-Jobdefinition geschrieben, aber finaler PR-Head-Workflow noch UNVERIFIED. PR #87 ist Draft, keine Merge-/Deploymentfreigabe; main inzwischen fortgeschritten. Bestehender Unity-Editor, C#-Core, Spielstaende und private Assets unveraendert.
- **Weiter:** PR gegen aktuellen main und exact-head CI pruefen; Issue-Status abfragen, externe MCP-Auth/Sovott-Dispatcher als getrennte reversible Stufe; Owner-DOCX (nicht sicher bearbeitbare Identitaet) nachtraeglich pflegen. Keine Live-LLM-Kosten/automatischen Paid-Retries.
## Unity-6-Upstream-Quellenaudit – 2026-11-18 [unity-13-upstream-capability-audit-20261118]

- **Umfang:** Alle 13 vom Owner genannten externen GitHub-Quellen gegen Commit-SHA, Git-Tree, README und Originallizenz verifiziert. Versionierte `Tools/Director/neural_hub/upstream_sources.json` (SHA256 `82a24bfb1772de8b0bbea5fc8a874fe945a6174911507deba32a04ad996d96e9`) erfasst die gefundenen `SKILL.md`-Pfade, gemeldete Tool-Zahlen als ungeprüfte Anbieterangaben, Lizenzen und 28 fachliche Zuordnungen. Der `sources.mjs`-Validator pinnt 13 exakte Source-Commits und Lizenzblob-SHAs und verweigert alternative 40-Zeichen-SHAs, automatische Installationen oder Rechteerweiterungen.
- **MCP-Integration:** Zwei neue lesende Werkzeuge `director_sources` und `director_source_discover` im vorhandenen mcp-use-Server; jetzt 32 MCP-Tools, 28 Prompts, 13 Quellen. Kein zweiter Unity-Broker und keine Fremd-Plugin-Installation. Der volle Kriterien-/Lizenzbericht steht in `docs/DIRECTOR_EXTERNAL_UNITY_SOURCES_20261118.md`.
- **Evidence:** isoliertes VPS-Checkout Code-Stand `ea9962d6cf7f37295b6d34831dd6704174c2a33e`: 19/19 Node-Tests und 48/48 Python-Director-Tests PASS, echter MCP-JSONRPC/SSE-Canary PASS. Docker-Build und fluechtiger Runtime-Canary `--network none --read-only --cap-drop ALL` PASS, Image-ID `sha256:0c4826e70787989a967737cb4aaab5a19d9c2c27b50eade698a52e8a483b4d17` (387214035 Bytes); danach kein laufender Neural-Container. Dieser Docker-Nachweis ist nicht Unity-Editor-/Android-/PlayMode-Evidence.
- **Erkenntnisse:** Besty meldet jetzt 873 REST-Skills, nicht 839. AnkleBreaker besitzt eine eigene Lizenz mit kommerziellen Einschränkungen. Unity Official Skills verwenden Unity Companion License. Die 200 gefundenen `SKILL.md`-Pfadtreffer ueber die 13 Trees sind nicht 200 neue unabhängige Unity-Tools und enthalten mehrfach gespiegelte Skills.
- **Offen/UNVERIFIED:** PR #87 ist Draft, `main` fortgeschritten und keine exact-head GitHub Actions belegt. Wirkliche Editor-Kompatibilitaet mit Unity 6000.6.8f1, Lizenzfreigabe der Abhaengigkeiten, granularer Tool-API-Export und der authentifizierte Sovott-zu-Hub-Dispatcher fehlen. Das bestehende ChatGPT-Plugin `Ashen Crypt Director Studio` wurde mangels verifizierter editierbarer Backend-ID nicht ersetzt oder dupliziert. Owner-DOCX hier nicht bearbeitet.
- **Next:** PR #87 konfliktfrei mit aktuellem `main` abgleichen, exakte CI und unabhängigen Readback pruefen, danach nur einen freigegebenen Editor-Adapter in einem isolierten Unity-6-Testprojekt testen. Keine Runtime-/Core-Mutation vor gesonderter Abnahme.
## Einheitliches Figurensystem aus dem BoZo-Paket – 20.11.2026 [bozo-characters-20261119]

- Branch `feat/bozo-characters-20261119` auf `main` `488567f`. Neu: `Assets/Editor/BozoCharacters/` (Rezepte `figures.json`, `BozoFigureRecipes`, `BozoFigureBaker`, `BozoPartColours`, `BozoBuildGuard`), Tests `BozoFigureRecipeTests` und `BozoFigureVisualTests`, Anleitung `docs/BOZO_CHARACTERS.md`; angepasst: `EntryWorldArtBuilder`, `EntryCombatArtBuilder`, `MillPatientAuthoring`, `.gitignore`. Kein Core- oder Laufzeitcode geändert.
- Live-Workspace B: Paket 361388 (2.6.5) privat installiert, zwölf Figuren gebacken, bisherige Menschenfiguren ersetzt (Sicherung der vorherigen Visuals privat), Held und Verletzte neu gebaut, Waffenlage in der Hand neu bestimmt.
- Owner-Befund „Figuren stehen im Dunkeln“ behoben (Normal-Textur neu geschrieben, Farben aufgehellt); Neuro-Director-Fachbereiche `shaders` und `lighting` befragt, Ursache per Messung gefunden.
- Evidence: EditMode `BozoFigure*` 9/9, EditMode 135/135, PlayMode 171/171, Core 21239, Preflight 40, Android-Skriptkompilierung fehlerfrei; Details in `docs/evidence/bozo-characters-20261119/acceptance.md`.
- Grenzen: APK und Gerät ungeprüft (Bildrate mit 22 000–28 000 Dreiecken je Figur, Kodierung der Normal-Textur); nicht gepusht; Owner-DOCX nicht bearbeitet.
- Nächster Schritt: gekauftes Paket „Stylized Dark Fantasy Asset“ (135244) einbinden, Dungeon-Umgebung überarbeiten und die Aschenwelt der Oberwelt gestalten (Owner-Auftrag 19./20.11.); Bau-Modus-Bewohner auf `Villager0–2`/`Merchant0` umstellen, sobald beide Branches zusammenkommen.
