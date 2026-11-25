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


## Director harness integration verification – 2026-11-19

- Dedicated Draft PR #93 extends the **existing** 28-specialist Ashen Neural Hub with MCP `initialize.instructions`, a shared 6-case language corpus, offline trace grader and true JSON-RPC canary calling all 28 `advice_*` endpoints. The Canary source commit `13faae919bd570dcec33d64a8f0e092664884c30` passed GitHub Core CI run 44427342825; licensed Unity workflow 44427342761 was SKIPPED.
- Independently fetched the exact source SHA into a clean, isolated VPS checkout and executed 31 Node tests plus 59 Python Director tests. Built isolated Docker image `sha256:095d6a88fd3995d160478d31c20ebd091b6fd5b69de4b5b377b9473c0178c747`, ran one ephemeral, no-network, read-only localhost MCP canary. 17 GitHub-open issues input SHA-256 `5e365d7b9e0e79ba3dd47dbad780503fa36b5ce75d2c1b420c0de7f1a3a92a34`; output log SHA-256 `722e747a4f97a8b6b2e5bc1c0ef107f80aeef352f3b1d883515dfebc399ef69e`. 28 endpoints completed, 22 proposed and 6 abstained; three paths #60, #77, #71 were selected.
- Existing VPS Unity Game Director `project_status` succeeded when executed as the actual workspace owner `unityarpg` on **different** workspace SHA `860128f7cae256aefed8b874e2ab01bc01b6b3b6` (Unity 6000.6.8f1, no dirty tracked assets, no current editor instance). The first SSH-root probe gave Git dubious-ownership `git:128`; no permission bypass or Git reconstruction, only correct process identity.
- Product integration status **PARTIAL**: Hub is a proven isolated advisory MCP protocol but is not permanently running or attached to the canonical Director broker, GitHub source and active Unity revision differ, caller-supplied issue records remain distinguished from independent sources, and no new PlayMode/device or L2/L3 model-harness run was executed. No game Core, Save, assets, shaders, active Unity scene or runtime modified.
- Full sanitized evidence and source boundaries: [docs/evidence/ashen-neural-harness-20261119.md](../ashen-neural-harness-20261119.md). Next: separate authenticated read-only Director/Sovott bridge acceptance and independent Unity/editor verification, **never** a second authority.

## PR #93 Review-Reparatur — 20.11.2026

- TDD RED `d042ed1791ec5e82cfe9b315f3be987706e3b39e`, GitHub Actions [44653946585](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44653946585): vier absichtlich fehlschlagende Node-Regressionen.
- Code GREEN `d0d514deb3fa80f1f1a8aa717e25ec6f9bcf1ea9`, Actions [44654065733](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44654065733): Core und Neural-Hub erfolgreich, Node 40/40, reale localhost-MCP-Canaries Legacy/Modern, Ausgabegrenzen im Python-Code; anschliessend Python-Bridge-Test explizit in CI aufgenommen.
- Review-Fixes: `result.isError`, exakte `snapshotId`, Drei-Wiederholungs-Gate, Ressourcenidentitaet, Issues 69/70 und Council-Tracks, Action-Routing-Anweisungen, begrenzte Subprozessstreams und hostseitige Docker-Container-Cleanup-Pruefung.
- Freigabegrenze: [Issue #94](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/94) fordert echten vorhandenen authentifizierten Broker/Unity-Director-Readback vor Merge. Sovott-SSH zuletzt `disconnected`; kein Produkt-/Container-/Unity-/APK-/Device-Proof. Owner-DOCX bleibt aus dieser Sitzung nicht sicher verfuegbar und unveraendert.

## PR #93 Live-Broker-Readback – 20.11.2026

- Isolierter Code-HEAD `86f6b41bb21076fef6daab7c872ac28cacd4e55e` -> echtes VPS Docker-Image `sha256:059b93a96be4e65c6753ea0fa3900d3e30a5274f339017020730f2b9d948a8dd` (Source-Tree `38e0ae76052cee6dd55d92c081947d1c9c85382a`); Node **40/40** und MCP Legacy/Modern **PASS**, 32 Tools/28 Prompts/28 echte Advisor-Calls, 8 über GitHub geprüfte Issue-Records, Input SHA-256 `79fb32e432bdda2fee30bbd894f61e3788ec29238ffdbccfff15d67cdf5fa3c4`.
- Vorhandener Neuro-Director-STDIO-Client über Sovott-SSH gegen denselben source-pinned Hub: `initialize`, `tools/list`, `prompts/list`, `director_snapshot`, `advice_world_design`, `director_consult` erfolgreich. Kein neuer Port/Broker; aktive Claude-Pin-Datei blieb unverändert.
- Reale Docker-Timeout-Cleanup-Probe: dedizierter, isolierter Probecontainer entfernt, separater Docker-Readback sauber. Kanonischer Unity Director als `unityarpg` erreichbar, aber Workspace-HEAD `73337a89301f4b32924c1ffcb235138c88059089` weist 6 tracked / 5 untracked Spieleingaben auf; `director_bridge.py` blockiert korrekt mit `DIRTY_UNITY_PROJECT`.
- **Freigabegrenze:** Issue #94 bleibt offen, vollständiger source-matched Live-Director-Broker-Readback und Unity-/Device-Nachweise fehlen. Kein Merge/Deployment, kein Abbau existierender Unity-Änderungen, keine zweite Autorität. Umfang und Operation-IDs: [Neural PR93 Live-Gates](../ashen-neural-pr93-20261120-live-gates.md). Owner-DOCX nicht editiert.

## PR #93 · Abnahme über den bestehenden Sovott-Broker am 20.11.2026

Der kanonische `project_status` lief als `unityarpg` vor und nach dem echten Advisor-Aufruf. Beide Abfragen bestätigten den sauberen Unity-Stand `eb4a009f90ed0c11d7013246784239870234d897` und Unity 6000.6.8f1. Der Hub lief aus dem separat gepinnten PR-Commit `c5835ab766e2c114b2d899d6849169e7f6a5a4a0`, Image `sha256:ccebb3d238625a3f8c0226c20406906f444c1023a8417f3bb27a13f1ab7bb999`. Das aktive Unity-Checkout und der Produktions-Pin wurden nicht umgestellt.

- GitHub lieferte 18 einzeln als offen bestätigte Issues. Eingabe-SHA256 `44ee434f19c8afa03bf3ed34a8be7221d7a4ae415213aec9ca5ace8447b0c581`. Der Hub kennzeichnet die Daten selbst als übergeben.
- Zwei authentifizierte Durchläufe der Bridge lieferten bytegleiches JSON (SHA256 `3da673a29c14c60cfa1cba3f5645c8579178057c1cfe7afdf1287decf54021fa`), 32 Werkzeuge und eine World-Design-Empfehlung zu #66. Snapshot `87b7e9c88e3ad100ec11951a23b877cdd4042acf4b3980f99727ff9ec2e02bce`. Eine abweichende erwartete Unity-Revision löste `STALE_WORKTREE` mit Exit 2 aus. Der gesondert aufgerufene bestehende STDIO-Client lieferte 28 Advisor-Antworten mit demselben Snapshot und drei Vorschlagspfaden. Keine Unity-Aktion; nach dem Lauf keine Probecontainer.
- 40 Node-Tests, 59 Director-Tests und 6 Bridge-Tests bestanden. [Core- und MCP-CI](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44690809203) erfolgreich. Der optionale lizenzierte Unity-CI-Job wurde übersprungen. Diese Abnahme betrifft den lesenden Brokerpfad; Editor-Spielbetrieb, Android und eine Produktionsumstellung waren nicht Gegenstand dieses Laufs.
- Belege der SSH-Ausführungen: `7acc08de54b349ff9bc938ce1d7595f4`, `5cd6dbfe32904bce8bc9ca713ac09d59`, `15cceb0a3dba4cc783bc342f5087d5dc`. Das separat referenzierte Owner-DOCX war unter seiner kanonischen Datei-ID nicht zugänglich.


## WorldForge Asset Intake hardening — 2026-11-19 (Issue #62; PR #95)

- Ausgang: Draft-PR #95 HEAD `8ad088ebeb333c08139639d31c9dfc60f7536390`, Ashen main `488567fddcc48b04bb8c39871da4acc05d7316bf`. Review meldete sechs echte Regressions-/Sicherheitsrisiken: unplatzierte POI-Terrainwirkung, stale Root, zu spaeter Preflight, alte GUI-Scans, fehlende Runtime-Name-Validierung und privates Testordner-Leak.
- Source-/Testaenderungen: `Tests/Preflight/test_worldforge_asset_intake_release_guard.py` als sechs neue RED-Vertraege; `WFHeightLayers.cs` ueberspringt ausstehende POIs; `WFAssetIntakeEvaluator.cs` setzt pending Radii auf 0 und prueft echte Runtime-Bindings am Approval-Gate; `WFTerrainBuilder.cs` validiert alle aktiven POIs vor Build/Respawn/Height-Effect und entfernt alte POI-Roots bei Deaktivierung; `WFAssetIntakeWindow.cs` verwirft Scan bei Ordnerwechsel; `AssetIntakeEditModeTests.cs` ergaenzt echte Editor-Vertraege inkl. GUID-Abweisung vor Szenenmutation und cleanup-on-failure. Dokumentation an neuen Vertrag angepasst. Keine Veraenderung an Core, Save, Quest oder Laufzeittick.
- Evidence: RED GitHub Workflow `44569143089` (6/6 neue Preflight-Tests fehlgeschlagen, Core-Job failed), GREEN `44570147837` (Core und Preflight success, Neural-Hub-Job success) am getesteten Code-SHA `a5647ca2a7acb12090c15e126c0391ac00b7c92e`; Unity-Lizenz-PlayMode-Workflow `44570148037` skipped (NICHT PASS). GitHub https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/95 . Wolfram-Graph theoretisch azyklisch fuer #62 -> #66 -> #69 -> #71 -> #77; kein Runtime-Beweis.
- Grenzen: Native Unity 6000.6.8f1 EditMode gegen 791 private Assets, Weltbilder, Editor-Status, Android und realer Owner-DOCX-Readback fehlen; SSH-Owner-Delegation derzeit disconnected, somit aktiven Host nicht geaendert. Sovott Live-Broker 298 Werkzeuge, vier Ashen-Tools noch nicht registriert; Issue #94 weiterhin offen. Kein Merge und keine Produktiv- oder Weltwirkung behaupten.
- Fortsetzung: Aktuellen PR-Head/CI nach Dokumentationscommits unabhaengig bestaetigen; mit Owner-SSH den realen Unity-6-Editor und 791-Asset-Tests auf genau diesem PR-Head im isolierten Workspace ausfuehren; Sichtvergleich, Test-Zahl, SHA und ggf. Neuregessionen belegen. Erst danach Owner-`Ashen_Crypt_Evidenz_Logbuch.docx` unter bestaetigter Originalidentitaet fortschreiben und genau einen `Memory.md`-Integrations-Eintrag vor Merge setzen. PR #95 bleibt Draft.

## WorldForge Pending POI Core-map boundary — 2026-11-19

- Cross-surface finding in PR #95: `WFFlattenPoiLayer` ignored pending approved POIs, but `WFCoreMapExporter.Build` still serialized unplaced `WFPoiDefinition` into authoritative `ForgedPoiRecord`, and `WFTerrainBuilder.Spawn` could still reserve clearance from a legacy pending record with positive radius.
- Strict TDD RED: introduced two new preflight guards at commit `5340b4ca1869ea8d2345c501b677cbd6be968bab`; genuine GitHub workflow `44580735675` FAILED specifically the two new assertions (48 tests, 2 failures), other original checks not disabled. Added native Unity-EditMode behavior regression `PendingPoiDoesNotEnterAuthoritativeCoreMap` at `d4c1b01d9d20b4d5b41d420d0232bf48f31cc1d1`.
- Fix: `WFCoreMapExporter.BuildActivePoiRecords` includes only explicitly placed POIs; `WFTerrainBuilder.Spawn` ignores pending or zero-clearance POIs. Source edits `bf84a043a4adb5d5da622679e223873717146cb9`, `5c531230dca4c1c3b31fdcb8314066823e75607c`. Documentation at `b6b91a25bb88f1d7db6f75a7f3064005be64c984`. Current PR HEAD and final GitHub CI are to be independently read back after this handoff write.
- Effects and authority: no change to 100-ms C# simulation, saved game records, real scene or Unity Editor; the adapter only prevents a not-yet-placed metadata record being promoted into an authoritative world POI. Already placed legacy story POIs remain included. No merge/promotion.
- Blocked/UNVERIFIED: original Unity Editor 6000.6.8f1 run against 791 private assets, native EditMode behavior, live image and game rendering, Android Tab A9, and persistent owner `Ashen_Crypt_Evidenz_Logbuch.docx` identity (SSH owner delegation disconnected). Native checks MUST NOT be inferred from Python preflight.
- Next: reread final PR-HEAD CI; restore existing owner-only Sovott v2 SSH, compare active Unity workspace SHA, isolate unchanged private assets, run native EditMode acceptance and independent readback, then append the original DOCX and one Memory entry before any approved merge.

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:9438b726b3e560299c39b63d06f60199620268d52e66eacb7ea2f438ae698bc5 -->

## WorldForge review repairs — 2026-11-20

- Base: `fdb0554eceab5eec464b77eb096160753c442c40`; result is the uncommitted task patch. Evaluator now measures root-centered horizontal mesh-bounds radius plus one metre of clearance, feeding both spawn and Core obstacle radii. Weapon markers in paths/hierarchy require review within allowed packages. Window includes GLB/GLTF changes. Disabled POI visuals permit cleanup; enabled visuals keep GUID validation.
- Pending POI export finding skipped: current BuildActivePoiRecords already excludes pending entries, with existing regression coverage. Core/save authority unchanged.
- Added behavioral tests before repairs for offset geometry/confirmed radii, allowed-package weapons, GLTF queueing and disabled cleanup versus enabled GUID rejection. Unity RED attempt exited 127 (no Editor); native RED/GREEN remains unverified, and private fixtures are unavailable.
- Executed: SDK 8.0.428 CoreVerification 21,239 assertions passed; WorldForgeCoreVerification 10/10 passed; Python Preflight 48/48 passed; git diff --check passed. Initial .NET execution failed due to missing ICU; installed libicu78 and reran successfully. Exact commands/input hashes are in workspace-context/validation-record.md outside Git.
- SDLC dependencies restored; start attempts rejected missing request / invalid run ID; no lifecycle completion claimed. No new CI, publication, merge, Unity, visual or Android acceptance.
- Owner Ashen_Crypt_Evidenz_Logbuch.docx inaccessible; writeback pending. Next: execute Unity 6000.6.8f1 EditMode fixture with private art, then append results to the same owner document.


<!-- /coderabbit-ai-generated:v1 -->

## 20 November 2026 — World Forge #95 native acceptance

The original substring rule treated Bowl_01, Pipe_Elbow, Balance_Scale in Rainbow/, and Clubhouse as weapons. Unity 6000.6.8f1 reproduced all four false exclusions on `86b5a21d481c168cdd493c1c3dc1ead54efd22cc`. RED XML SHA-256 `0e3a2070ef66d3d62eca4a7915f9de0ff860725892e2505901308e0d9b10e461`. Matching complete tokens and recognized compounds still excludes weapon folders and actual weapon names. Code `9838a3319d5a816e8118ac574426b6326c56c3db` passed 43/43 native WorldForge tests, XML SHA-256 `612c6e4b4a3b98035e99c5e734f198d113eb17fc7a86394525b0827e30c7f052`.

An additional fixture on `79c6e6f0ae17659d9dd8689c9af418ef98d2e8c5` imported, moved and deleted a temporary prefab using Unity's AssetDatabase. The review window retained the GUID at its new path and removed the deleted entry. The full WorldForge EditMode namespace passed 44/44 with zero skips. XML SHA-256 `322c306a7f44cc7acee31a1cf0fe9ea0d965681d6bb28411c11226fbb964c54e`. The editor classified 1,365 real private GameObjects (237 protected, 745 requiring manual review, 383 candidates). The tests also covered root-offset clearance, pending POI export, GLB/GLTF queueing and disabled-visual cleanup. The isolated VPS receipt files are `review95-{red-86b5a21,green-9838a33,realimport-79c6e6f}.xml` in the existing asset-intake-20261119 evidence directory.

[Core workflow 44702047124](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44702047124) passed 21,239 Core assertions, 10 world-map scenarios and 48 static preflight tests. Licensed CI PlayMode was skipped. Device and Android build evidence are outside this editor acceptance. The owner DOCX reference could not be opened for writeback. Recheck CI and native tests after integrating #93.
## Einheitliches Figurensystem aus dem BoZo-Paket – 20.11.2026 [bozo-characters-20261119]

- Branch `feat/bozo-characters-20261119` auf `main` `488567f`. Neu: `Assets/Editor/BozoCharacters/` (Rezepte `figures.json`, `BozoFigureRecipes`, `BozoFigureBaker`, `BozoPartColours`, `BozoBuildGuard`), Tests `BozoFigureRecipeTests` und `BozoFigureVisualTests`, Anleitung `docs/BOZO_CHARACTERS.md`; angepasst: `EntryWorldArtBuilder`, `EntryCombatArtBuilder`, `MillPatientAuthoring`, `.gitignore`. Kein Core- oder Laufzeitcode geändert.
- Live-Workspace B: Paket 361388 (2.6.5) privat installiert, zwölf Figuren gebacken, bisherige Menschenfiguren ersetzt (Sicherung der vorherigen Visuals privat), Held und Verletzte neu gebaut, Waffenlage in der Hand neu bestimmt.
- Owner-Befund „Figuren stehen im Dunkeln“ behoben (Normal-Textur neu geschrieben, Farben aufgehellt); Neuro-Director-Fachbereiche `shaders` und `lighting` befragt, Ursache per Messung gefunden.
- Evidence: EditMode `BozoFigure*` 9/9, EditMode 135/135, PlayMode 171/171, Core 21239, Preflight 40, Android-Skriptkompilierung fehlerfrei; Details in `docs/evidence/bozo-characters-20261119/acceptance.md`.
- Grenzen: APK und Gerät ungeprüft (Bildrate mit 22 000–28 000 Dreiecken je Figur, Kodierung der Normal-Textur); nicht gepusht; Owner-DOCX nicht bearbeitet.
<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:bbd0a7f168701e2666ad5d7a7cee2aaa159696d1423e6fa9120b293d176ab75c -->

- Nächster Schritt: gekauftes Paket „Stylized Dark Fantasy Asset“ (135244) einbinden, Dungeon-Umgebung überarbeiten und die Aschenwelt der Oberwelt gestalten (Owner-Auftrag 19./20.11.); Bau-Modus-Bewohner auf `Villager0–2`/`Merchant0` umstellen, sobald beide Branches zusammenkommen.

## BoZo Reviewkorrekturen – 20.11.2026

Ausgangsrevision `b75c02e36f8f4bcb5fc2ff716de14de5ef06d5bc`, lokaler Patch: verschachtelte Resources-Umbenennung, temporärer Bake mit GUID-Erhaltung/Rücksicherung, vollständiges Szenen-Setup und SaveScene-Abbruch, Ressourcenfreigabe, Report-Verzeichnis, MayWrite-Vorprüfung, Farbfolgen und flächengewichtete Farbstichproben, Hero-Clip-Vorbedingung sowie NPC-Fallback und gezieltes Test-Ignore korrigiert. Core 21239 Assertions, 40 Preflights und statischer Unity-Kontrakt bestanden; Unity-Editor und privates Paket hier nicht vorhanden, Editor-Regressionen und echter BuildGuard-/APK-Lauf offen. Patch-Hashes im workspace-context/validation-record.md (Arbeitskontext). Historischer getesteter SHA samt unabhängigem Readback fehlt; Implementations-SHA allein ersetzt ihn nicht. Owner-DOCX mangels verifizierter bearbeitbarer Dokumentidentität weiterhin offen. Nächster Schritt: Editor-Tests mit Paket einschließlich Fehlerfällen und SHA-gebundene unabhängige Readbacks.

<!-- /coderabbit-ai-generated:v1 -->


## PR #96 – unabhängiger nativer Readback

Commit `a169f3af7c0b2c5990c207f43a9192ae1af1a204` integriert #93 und #95. Eine fehlerhafte Materialkonvertierung vor dem ersten Kamerabild und die sichtbarkeitsabhängige Hand-IK der Spielfigur wurden anhand von zwei nativen Regressionen behoben. EditMode 11/11, PlayMode 2/2, jeweils ohne Skips, Core 21 239, Preflight 48/48, GitHub Core 44703565340 erfolgreich. Der unabhängige negative Rebake-Test bestätigte zwei wiederhergestellte Szenen und 25 unveränderte SHA-256-Einträge. Einzelbelege, privater Metadatenbefund und verbleibende APK-/Gerätegrenzen stehen in `docs/evidence/bozo-characters-20261119/acceptance.md`.

## Bau-Modus: Welt bauen wie in einer Aufbausimulation – 19.11.2026 [bau-modus-20261119]

- Branch `feat/bau-modus-20261119` auf `main` `488567f`: `6bbf539` (Bau-Modus) plus Doku-/Evidence-Commit. Neu: `BuildLayout.cs` (Katalog, Baugebiete, Regeln, Wald-Generator), `BuildLayer.cs` (passive Darstellung, Live-Kanal), `BuildLayerPresentation.cs` (ArenaRuntime lädt den Bauplan nach jedem Szenenaufbau), `Assets/Editor/BauModus/` (API, Vorschau, isometrische Sprites/Karten, Speicher mit Revision und Rückgängig, Browser-Seite), Fadenwerk-Erweiterungspunkt (`/?<key>`, `/api/x/`), Anleitung `docs/BAU_MODUS.md`.
- Live-Workspace B auf `main` + Bau-Modus abgeglichen (Sicherung der überschriebenen Dateien privat), Fadenwerk-Server gestartet, Zugangsschlüssel an die vorhandene Gateway-Schlüsseldatei angeglichen; `https://arelogic.space/fadenwerk/?bau` funktioniert hinter der Owner-Anmeldung. In B ist jetzt `AshenEntry.unity` geöffnet.
- Evidence: EditMode 126/126, PlayMode 171/171, Core 21239, Preflight 40, Gateway 5/5, Playwright-E2E und Live-Check im Play-Modus; Details und SHA-256 in `docs/evidence/bau-modus-20261119/acceptance.md`.
- Grenzen: Gebautes ist Kulisse (keine Kollision/Wege/KI); Editor-Vorschau mit echter Umgebung nur für Aschengrenze und Krypta; APK erst mit dem nächsten Build; nicht gepusht (keine Zugangsdaten); Owner-DOCX nicht bearbeitet.
- Nächster Schritt: Owner-Entscheidung, ob gebaute Häuser im Spiel blockieren sollen (Core-Regel), danach Push/PR.

## 20.11.2026 · PR #97 nach dem Merge des Figurensystems

Getesteter gemeinsamer Code `40eb8c7ecac79db1cbfa457734cfa3bfa2cbbe76`, Tree `f42a3d75f0d51fb1d2f966e7bdb5820d42a246c5`. Die Prüfung am Bauplan schützt gegen veraltete Browser und den Verlauf extern ersetzter Dateien. Bestehende Bewohner-Schlüssel und der neue Händler wurden sowohl ohne Kunstpaket als auch mit den gebackenen privaten Figuren in Unity geprüft. Chromium, zweite Schreibanforderung und anschließender Dateireadback bestätigten HTTP 409 und danach eine erfolgreiche bewusste Wiederholung. Unity-Kulisse ohne Kollision. Der native Bericht, XML-Hashes, Core-Lauf und GitHub-Checks stehen in `docs/evidence/bau-modus-20261119/acceptance.md`. Android-Geräteprüfung und Asset-Store-Upload sind offen.

## 20.11.2026 · Issue #2 / PR #101 · Ursächliche Lagerentscheidung

- Ausgangs-main `a9006a3adc354cca8422cd452298adfcc7a593e1`. Echter RED auf `8ab74b9d2862fc549073371ad4c8871285836471`: der alte Core schrieb die Entscheidung vor dem Diebstahl. [Run 44717091020, Versuch 1, Job 136223142095](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44717091020/job/136223142095).
- Geprüfter Code `612ae0f1beec62b03d6d94a657ba6a51e2fba11e`, Tree `3430361a5c7f6e8cd863b2360548e8386360fb62`. `SocialLedger.Forge` behält die Ereignishoheit; nur die bereits akzeptierte Transaktion liefert den Parent für `camp.decision.stolen` oder `camp.decision.replaced`. Der separate Nachweis des Vertrauensverlusts identifizierte einen zweiten Fehler: ein mit gültigen Prüfsummen manipulierter Kind-Eintrag konnte Vertrauen nochmals um 2.000 verringern. Der korrigierte Reader weist den Eintrag vor dem Einfügen ab.
- Vom Owner ausdrücklich freigegebenes NMF3 nur für tatsächlich verknüpfte Entscheidungen. Historische AC5/17/18/19/20-Bytes aus dem realen Ausgangs-Core sowie NMF1/NMF2 einschließlich einer Controller-Migration wurden unverändert gelesen und geschrieben. Die sechs historischen AC-Testvektoren liegen in `Tests/CoreVerification/Fixtures/`. Vorherige Reader können neu entstandene NMF3-Saves nicht laden.
- [Core-CI 44718441625, Versuch 1, Job 136227686485](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44718441625/job/136227686485): 21.316 Assertions und 48 Preflights bestanden. Lokales Core-Log SHA-256 `fad10c27de4a8fdc8a4a9c69bbb0909c9eb063566d6abe43d99885b705004d55`. Statischer Unity-/Android-Kontrakt bestanden.
- Native Unity 6000.6.8f1 auf demselben Code-SHA: EditMode 7/7, XML SHA-256 `750bfba74c549c4276e3d497b764891e835892113829951cfd478530844f2818`; PlayMode 7/7, XML SHA-256 `85c0be0bf9e11658b86bd4165105aeee545a438ac9fd325984f6f99cc61ec93a`; jeweils null Skips. Der HUD-Test verglich die finale Spenden-ID vor und nach dem Laden. Native XMLs liegen privat unter `/var/lib/unityarpg/.local/share/are-arpg/pr101-native-20261120/612ae0f1beec62b03d6d94a657ba6a51e2fba11e/{EditMode,PlayMode}/results.xml`. Der optionale lizenzierte GitHub-Job 44718441623 wurde übersprungen. APK und physisches Gerät wurden nicht geprüft.
- Separates Owner-DOCX unter seiner hinterlegten Dateiidentität nicht zugänglich. Dieser Repository-Eintrag hält die Abnahme fest. Folge-Issues der Causal-Memory-Reihe sind separat zu bearbeiten.

<a id="issue-3"></a>

## 20.11.2026 · Issue #3 / PR #102 · Berichte des Straßenreisenden

- Ausgangsrevision `9f8d436a32efe4852293e75c1e7cd5c483f5e4e7`. Bei derselben Begegnung mit drei Dorfbewohnern wich die Erwerbsreihenfolge mit der Aufzählung der Figuren ab. RED auf `53f833dd02561481bb22c8da7b2744e37f3016b2`. [Run 44720750407, Versuch 1, Job 136235533763](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44720750407/job/136235533763)
- Zweiter RED auf `7f0d2dc8c3466de05e40907baab6892b166d6066`. Nach dem Laden eines echten Vor-Forge-Spielstands sprach Dorn über einen Vorfall, für den das neue Archiv keinen Augenzeugenbeleg hatte. [Run 44721818120, Versuch 1, Job 136239207232](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44721818120/job/136239207232)
- Geprüfter Code `2e1a3c857fb866a5fb91a67b91c8feac1696da6e`, Tree `e0399fde1e902b18e325eb1947432a92bb91e22b`. Der bestehende 100-ms-Core prüft Dorfregion, Bewusstsein, 2.400 mm Reichweite und Sicht an der realen Gebäudekante. Bei mehreren Zuhörern gilt aufsteigende NPC-ID. Der Dialog übernimmt einen neuen Bericht, sobald das Archiv die direkte Kenntnis des Reisenden und ihre Erwerbssequenz bestätigt. Bereits gehörte Altgerüchte bleiben erhalten.
- 21.549 Core-Assertions und 48 Python-Preflights bestanden. Zusätzlich bestanden 4 FwJson- und 45 Fadenwerk-Prüfungen. NMF1 und NMF2 mit zugelassener Controller-Migration wurden jeweils vor und nach der Begegnung bytegleich geladen. Beim nächsten Kontakt blieben Beziehung, Gesundheitswirkung, XP, Münzen und Proviant frei von Doppelwirkung. [Core 44722947151, Versuch 1, Job 136243059615](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44722947151/job/136243059615) · [Director-Verträge, Job 136243059392](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44722947151/job/136243059392)
- Unabhängiger Readback mit Seed `123`. Mara erwarb im Forge-Tick `531` das Ereignis `2` als `Reported`, Quelle `road.traveler`, SourceSequence `3`, eigene Sequence `4`. Der Welt-Hash nach dem Laden war `73D09BDFF4E19AB3`, der SHA-256 des serialisierten Archivtokens `37f7715c145c71a941dc037e7ccdd99512a347464055a8053e4cc061395e687e`. Nach weiteren 100 Ticks stimmten die Save-Bytes und der Welt-Hash `52B191FC30A62BDB` überein.
- Unity 6000.6.8f1 am geprüften Code-SHA. EditMode 3/3, XML-SHA-256 `8a992671db772151f56eb07a085d48d56d9c5eca3e3884d91fd24f48f0c032be`. PlayMode ohne private Grafikpakete 1/1, XML-SHA-256 `5162a7b0bd0f975f5c7ae5b1f3bcf2496993a003ee3726b47cd1100c22010182`. Der native Test verfolgt den Tod des Jägers bis zu Maras Bericht und liest ihre Kenntnis aus dem gespeicherten PlayerPrefs-Spielstand zurück. Keine übersprungenen Tests in diesen Läufen. Der lizenzierte GitHub-Smoke 44722947260 wurde übersprungen.
- Der Owner hat physische Tablet-Tests für den Abschluss von Issues als optional festgelegt. Das Tab A9 war in Benutzung. Der separate Owner-DOCX-Verweis lieferte beim Zugriff HTTP 404. Die aktuelle Abnahme steht in diesem Handoff und im bestehenden Repository-Logbuch.
- Die nativen Protokolle liegen unter `/var/lib/unityarpg/.local/share/are-arpg/issue3-native-20261120/2e1a3c857fb866a5fb91a67b91c8feac1696da6e/`. Die Verzeichnisse `edit` und `publicplay` enthalten jeweils `input.txt`, `unity.log` und `results.xml`.
- Im privaten Unity-Projekt bestanden außerdem 3/3 PlayMode-Tests mit denselben Code- und Tree-SHAs. Der Lauf prüfte die sichtbare Jägerrettung, die Bodenposition des Reisenden und den gespeicherten Bericht an Mara. `play/results.xml` hat SHA-256 `d6dea02fdb3e0c7f23eb697b319b41926986812447ce24305a68e08060e5af72`. Der Gerüchte-Test lief zusätzlich bereits einzeln im Projekt ohne private Grafikpakete.
- Android wurde auf demselben Code- und Tree-Stand mit Unity 6000.6.8f1 gebaut, Exit 0. Die private Datei `apk/Ashen-Crypt-2e1a3c857fb866a5fb91a67b91c8feac1696da6e.apk` (165.882.511 Byte) hat SHA-256 `825c1169cbc050801e27f2a7d87ff6905ada1f8503ab8ecabb5773d671e71c96`. Paket `com.ouroboroscollective.areashencrypt`, Version 1.0 (1), minSDK 26, targetSDK 36. ARM64-IL2CPP, DEX, ZIP-CRC und 16-KiB-Ausrichtung wurden am APK geprüft. APK-Signatur v2 mit Android-Debugzertifikat verifiziert. `apk/vps-receipt.json` hat SHA-256 `8ae437deb1b2725c3b68a9aa9d5505cef0f8926b1033bc136681d061058e514c`, `apk/unity.log` hat SHA-256 `c2682ebbdfcbce1a1093401a9f7176de406bc38e928ceadb2aa665450e65d8a7`. Das APK liegt privat auf dem VPS. Für diesen Lauf gab es keine Geräteinstallation.

<a id="worldforge-blender-103"></a>

## 20.11.2026 · World Forge · Headless-Blender auf dem VPS

- [PR #103](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/103). Geprüfter Code `4d0ac1216a06209ce8a64db6a1911e1fb2ce88c7`, Git-Tree `f9c9ae21d1318180a2606b19e3cbd20040c7cac1`. Offizielles Linux-x64-Archiv Blender 4.5.16 LTS, SHA-256 `23432b064977908beb1ab4b7f32f024e7331105c875c187dce002aff84b10c11`. Headless-Start auf dem VPS unter `unityarpg` mit deaktivierter Skriptausführung bestätigt. Das installierte Exportskript hat SHA-256 `60d2ded0475ae5d3ce14dcec51d0baa1f5f1bd6fa4441df3c783a99581a53fd0`.
- Der aktivierte systemd-Zeitgeber verarbeitet private Aufträge unabhängig von Unity. Der SSH-Befehl `worldforge-blender submit` und der Asset-Eingang verwenden denselben Dienst. Die getrennte Desktop-Verknüpfung „Ashen Crypt – World Forge Asset-Eingang“ und das Director-Profil `project-worldforge.json` öffnen das bereits vorhandene private Unity-Projekt. Prüfergebnisse und Quellen bleiben auf dem VPS. Eine manuelle FBX-Übernahme geht an die bestehende Einzelprüfung.
- Ein animiertes Original-Prüfmodell mit zwei Materialien und gleichnamigen Ausgangstexturen durchlief alle neun Kombinationen aus drei Eingaben und drei Zielformaten. Jede Ausgabe wurde separat mit ihren eigenen Abhängigkeiten geöffnet. Hierarchie, Abmessungen, UVs, acht Farbwerte und drei Einheiten Bewegung stimmten. Beleg SHA-256 `02464eae876294cb78473f74dce28f21a25f61dac52fc0de32a69f55c41fa1e8`, Auftrag `6ccbfb73805655061246618fb7a33495646da99e88e6dc8a19b9140487ece50b`.
- Beim Import eines GLB-Skeletts stellte Blender eine zusätzliche Hilfskugel ins FBX. Das korrigierte Rezept liefert für alle drei Formate genau ein skinned Mesh mit fester Basis und 1,5 Einheiten animierter Spitzenbewegung. Beleg SHA-256 `5198160f4a56a2d2b6e47dc8d2dd512e563e9406cecdbd9436fb621472d6dba9`. Zusätzlich ergab der private Viking-Village-Wandabschnitt in allen drei Rückimporten dieselben 1.300 Dreiecke und Weltgrenzen wie seine FBX-Quelle. Beleg `c00dfc431389210c7563378cd7b60d9c5f3e18400af3e003e6a8978e6b0733e5`.
- Eine fehlende Textur und eine reine Kameraszene wurden als fehlgeschlagen ausgewiesen. Bei einem dedizierten Testauftrag ergab ein entnommener Binärpuffer `damaged`. Erneutes Einreichen derselben Quelle stellte das vollständige Paket wieder her und bewahrte den vorherigen Stand. Ein separater systemd-Lauf mit 96 MiB Speichergrenze beendete Blender nachweislich mit Exit 137. Der Worker stellte den Fehler zu. Mit der regulären Grenze von 5 GiB exportierte er denselben Auftrag erfolgreich. Protokoll-Hashes `d7d155859e3f265c74534deb23a167335c64e09bb933e993798711e324d3a33c` und `51399abfb3a300111cfb3516413700c1ea1775d948fc58eaccf688f847d554f6`.
- Unity 6000.6.8f1 auf dem geprüften Commit bestand 12/12 EditMode-Tests bei Exit 0. Die native Prüfung umfasst Einreichung mit Sonderzeichen, Erkennung fehlender Beilagen, echte Wiederholung eines fehlgeschlagenen Auftrags, FBX-Import und GUID-Erhalt beim erneuten Import. XML SHA-256 `11ea2f991c88b97d09a3657ebcf79eca5fc2d0ccbb7b7418ee517f71dfa71d90`, Editorlog `efcdb8f70d627e081241167c018f1f734e4e592de4896aa90daf3a46987ab9f6`. 21.549 deterministische Core-Assertions und 54 Python-Preflights bestanden. Der lizenzierte PlayMode-Job wurde übersprungen. Ein Android-Build oder Tablet-Test gehörte nicht zur Prüfung dieses Editor-/VPS-Werkzeugs. [CI](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44733017688)
- Die privaten Exportbelege liegen unter `~unityarpg/.local/share/are-arpg/worldforge-blender/results`, die Messprotokolle unter `worldforge-blender-evidence-20261120` und das Unity-XML unter `worldforge-blender-editor-proof/4d0ac1216a06209ce8a64db6a1911e1fb2ce88c7` im selben Datenverzeichnis. Der separate Owner-DOCX-Verweis ist weiterhin nicht zugänglich.
- Ein echter, vor dem Versionswechsel eingereichter Auftrag lag beim Anhalten des Zeitgebers in der Warteschlange. Nach der Installation beendete er den Export mit dem ursprünglichen Rezept `77e670e7…`. SHA-256 des produzierten Belegs `9caf11c3e4860315fd70229a275e22414940f7b862f94ede3c73dd9e199f30bf`. Neu eingelieferte Dateien erhielten das Rezept `60d2ded0…`. Der Installer bewahrt die vertrauenswürdigen Exportrezepte und Blender-Bundles unter ihren ursprünglichen Prüfsummen und Versionen auf.

## Dunkle-Fantasy-Krypta und Aschenwelt – Zwischenstand 20.11.2026, Pause auf Owner-Wunsch [dark-fantasy-dungeon-20261120] [ashen-world-20261120]

- Branch `feat/dark-fantasy-dungeon-20261120` (aufbauend auf `feat/bozo-characters-20261119`), Draft-PR #98. Beide Blöcke sind noch nicht abgeschlossen: kein `Memory.md`-Eintrag, keine `acceptance.md`; dieser Abschnitt ist die Übergabe.
- Krypta (Stufe 1, Commit `73337a8`): prozedurale Krypta und Reisehöhlen mit dem gekauften Paket „Stylized Dark Fantasy Asset“ neu eingekleidet (`DarkFantasyCryptArt`, `CryptDungeonDressing`, Raumlaterne statt Schrein-Licht). Offen: Eingangskrypta (`EntryWorldArtBuilder.Crypt`), versteckte Krypta, Rasslerbau.
- Aschenwelt (Commits `05b8a9a` und folgender): Owner-Vorgabe „Ascheschnee, rot glühende Fäden in der Asche, Glutpilze – keine Welt im Dunst“. `Tools/Art/generate_ashen_ground.py` malt den Boden (16 m je Wiederholung, Körnung für die Nähe), `Tools/Art/generate_ember_flora.py` die Glutpilz-Texturen; `AshenWorldArt` baut daraus `OutdoorGround`/`GrassVerge` und acht Pflanzen-Visuals (je ein Netz, höchstens drei Zeichenaufrufe). `AshFloraScatter` verteilt die Pflanzen im Feld von Aschenstraße und alter Mühle; Straße, Unterstand und Mühlenhaus bleiben frei. Ferne setzt den Boden nahtlos fort (gleiches Muster, Licht je Bild neu). Ascheschnee fällt beim Betreten bereits; Partikelbudget unverändert.
- Evidence (Live-Workspace, private Kunst installiert): EditMode `AshFloraScatterTests` 5/5, `AshenWorldVisualTests` 5/5; PlayMode `AshFieldGrowsEmberPlants…` 1/1, `HungerJourneyReturnsAndPersistsReplacement` 1/1, `WorldSurfacePresentationTests` 19/19, Partikel-/Atmosphäretests 3/3; Preflight 40. Nicht gelaufen für diesen Stand: volle EditMode-/PlayMode-Suiten, Core.
- Grenzen: APK und Gerät ungeprüft (Bodenmaterial mit Körnung und Glühkarte, rund 85 000 Pflanzen-Dreiecke im Feld); Dorf (Akt 2) hat noch seinen alten Boden; die Begrenzungsmauer `AshStoneWall` ist noch grün; Blender ist auf dem Server nicht installiert (Installation braucht ausdrückliche Owner-Freigabe).
- Nächste Schritte: Owner sichtet die Bilder; danach Dorfboden und Mauer an die Aschenwelt angleichen, Krypta-Reste umstellen, volle Suiten und Core, `acceptance.md` mit Vorher/Nachher, je Block ein `Memory.md`-Eintrag.

## 20. November 2026 · Grenzregion, Schneeasche und Glut

- [Abnahme mit Gebietsmaßen, Bildvergleich und Prüfsummen](../borderland-art-20261120/acceptance.md). Die Änderungen aus #98 sind in #104 enthalten. Auf Code `f9b29edca0c985d129877c622ac0317324ae0bbd` wurden 26 neue LOD-Prefabs aus dem privaten Headless-Blender-Eingang, zehn Baupläne, fünf Außenkulissen und die 256-m-Reisekarte im nativen Unity-Projekt überprüft.
- Der Kameravergleich zählte 4.834 Kiefernpixel. Boden, Wegrand und alte wie neue Glutflora folgen dem gleichen Aschebild und der Einstellung „Reduzierte Effekte“. Core 21.549, EditMode 225 plus vier separat ausgeführte Fälle, PlayMode 181/181. Android ARM64, Signatur und Kartenkompatibilität stehen im verlinkten Beleg. Die fünf historischen Kapitel behalten ihre Laufgrenzen. Der physische Gerätetest ist offen.

## 20. November 2026 · Aschengrenzer Markt

Der Rollout auf die privaten Dorfmeshes ist mit #105 belegt. Das Originalmaterial des begehbaren Platzes war beim ersten Landschaftsdurchlauf separat geblieben. Im gleichen Kamerablick liegt nun graue Asche am Wegrand und zwischen den Ständen. Code `1448807`, 21.549 Core-Assertions, 230 EditMode- und 181 PlayMode-Fälle sowie verifiziertes ARM64-APK. [Vorher/Nachher und Bildgrenzen](../borderland-art-20261120/acceptance.md#aschengrenze-dorfplatz-105)

## Regionale Weltkunst am 20. November 2026

Der private Kunstzweig enthält den auf `c600181d42da666c4ac0aa6ade4489ef4903e24a` ausgeführten Durchgang. Die auf dem VPS erneut gelesenen Nutzdateien ergeben Kunststand `2afd38070f8915ad2f1f3e060cbb0ff30cc5172a`. Sechs Stationen auf dem Hauptweg und vier Nebenorte wurden in der Projektkamera aufgenommen. 26 neue Platzierungen, acht Regional-Prefabs, 11 gezielte PlayMode-Fälle, 21.549 Core-Prüfungen und zwölf CPU-LOD-Projektionen sind erfasst. Das Unity-Paket enthält 38 Nutzdateien mit geprüfter GUID und Dateiprüfsumme. 15 Projektabhängigkeiten stimmen mit der Basisrevision überein. Der physische Android-Lauf und ein abschließendes künstlerisches Urteil stehen aus. Die private DOCX-Identität war nicht erreichbar. Das aktuelle Markdown-Projektlogbuch führt die Runde. [Native Belege](../regionale-weltkunst-20261120/readback.json)

## Feldschulter am 20. November 2026

Privater Quellstand `ff1d1ce4331f69ef5646f236e7b7960292c4ea0a`. Im regulären Spiel fiel die Feldschulter beim Kryptaritter, wurde aufgehoben, im Inventar angelegt und nach einem Szenenneustart wieder geladen. 21.574 Core-Prüfungen und acht gezielte Unity-Spieltests bestanden. Das native Unity-Paket enthält sieben selbst erstellte Nutzdateien mit geprüfter GUID und identischen Bytes. Drei bestehende private Projektprüfungen verhindern die generelle Releasefreigabe. Es gab keinen physischen Tablet-Lauf. Das Markdown-Projektlogbuch führt den Nachtrag, solange das private Word-Original nicht erreichbar ist. [Aufnahmen und Messwerte](../feldschulter-20261120/readback.json)

## Mühlwehr, Werkarsenal und Tiefscharrer am 20. November 2026

Die öffentliche Projektfassung enthält 23 feste Fundorte, die eigenen Spielmodelle, neun editierbare Blender-Dateien und Director Studio 1.3.1. Die Materialien und Meshes des Aschepasses liegen unter `Assets/AshenCrypt/Aschepass`. Ihre Unity-GUIDs stimmen mit dem im Spiel geprüften Export überein. Die lizenzabhängige Figurenpassform lässt sich im eingerichteten Projekt unter **Ashen Crypt → Ausrüstung → Regionale Arbeitsmonturen backen** erzeugen.

Auf dem privaten Kunststand `cbe67fea` bestanden sechs Unity-Spieltests und 21.919 Core-Prüfungen. Der vollständige private Beleg hat die Revision `bc1915fd`. Die ursprüngliche XML-Datei liegt im privaten Belegstand. Die [Ergebnisse je Spieltest](../muehlwehr-20261120/playmode-cases.json), die [23 Fundorte](../muehlwehr-20261120/fundorte.json) und die [Prüfsummen der nativen Aufnahmen](../muehlwehr-20261120/readback.json) sind im Projekt verfügbar. Der physische Android-Test steht aus.

## 21. November 2026 · Persönliches NPC-Gedächtnis · PR 106

Auf `d5d4ee20685110b36de3c82e87378cf5e536c3f3` reagiert Ilyan nach einer tatsächlich angenommenen Ration im laufenden Unity-Player mit einem persönlichen Untertitel. Die Beobachtung fehlt einem unbeteiligten Dorfbewohner. Beim dritten Tageswechsel verschwinden selbst nach Wiederabruf die letzten privaten Episodendetails. Der Core-Lauf über 43.200 Spielticks bestätigt identische Spielstandbytes und denselben Welthash nach Wiederaufnahme.

21.843 Core-Prüfungen, 54 Preflight- und 59 Director-Tests waren erfolgreich. Im lizenzierten Unity-Editor 6000.6.8f1 liefen 11/11 ausgewählte PlayMode- und 15/15 Memory-Forge-EditMode-Fälle. Der erste Compile meldete eine Assembly-Grenze. Der erweiterte Dorf-Test benötigte zusätzlich die vorhandenen privaten Figurenmodelle für die Bildabnahme. Ein weiterer Regressionstest reproduzierte einen Ladefehler beim späteren direkten Bestätigen eines gehörten Ereignisses. Die neue Erkenntnis ersetzt dessen Herkunft und entfernt fremde Sinnesdetails. Derselbe Spielstand lädt im Core und im Unity-Editor bytegleich. Eine zweite Prüfung bindet angekündigte Aktionen an ihre gemessene Wirkung. Treffer nach der Vorwarnung trainieren einmal, Ausweichen ergibt keinen Erfolg. Das gilt auch über einen Speicherstandwechsel hinweg. Ein Regressionstest verfolgt Bär, Klapperer und Glutwächter vom Ausholen bis zur Wirkung. Auch mit einem Begleiter als Ziel bleibt die ursprüngliche Beobachtung erhalten. Der frühere Word-Dateiverweis ist unter der hinterlegten Identität aktuell nicht erreichbar. Ein APK- oder Gerätelauf gehörte nicht zu dieser Runde.

- [Unity-Fälle, XML-Prüfsummen und Prüfsumme des Originalbilds](../npc-personal-memory-20261121/native.json)
- [Anschlüsse für künftige Berufe und Bedürfnisse](../../NPC_PERSONAL_MEMORY.md)
- [GitHub-Core-Lauf zum geprüften Code](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44856474920)

## 21. November 2026 · Dorfwirtschaft · PR 107

Auf `7fa2e707bd3fceb4a36071d11b9140c70a3ca2e0` arbeiten die Bewohner von Aschengrenze im laufenden Unity-Player auf echten Beständen. Die Werkplätze zeigen genau die Stückzahlen des Kerns, die Figuren stehen dort, wo der Kern sie führt, und die Dorftafel übergibt Material aus dem Gepäck der Spielerin. Zeigen, Verstecken und Anzünden ändert keinen Bestand.

Fünf Hinweise aus der Prüfung des Pull Requests wurden mit roten Tests nachgestellt und behoben: ein hungernder Bewohner mit Auftrag an der Mühle, zwei Berichte desselben Ticks, ein verlorener Scrollfinger an der Tafel, die Gutschrift für das bloße Ankommen und die Dorfarbeit im Tick der Niederlage. 22.154 Core-Prüfungen und 54 Preflight-Fälle waren erfolgreich. Im lizenzierten Unity-Editor 6000.6.8f1 liefen 238 von 242 EditMode-Fällen und 189 von 189 PlayMode-Fällen; die vier übersprungenen brauchen einen freien Bauplatz oder echte Blender-Aufträge. Ein Android-Gerät wurde nicht gemessen.

- [Abnahmefälle, Prüfsummen und Messwerte](../npc-economy-20261121/acceptance.md)
- [Regeln und Orte der Dorfwirtschaft](../../VILLAGE_ECONOMY.md)
- [GitHub-Core-Lauf zum geprüften Code](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44923695452)

## 22. November 2026 - Engineering Skill Gate v1.3.2

Revisionsgebundener, isolierter Workflow-Slice ohne C#-Core-, Save-, Unity-Asset-, Editor-, NPC- oder Spiel-Runtime-Mutation. Fuenf MIT-lizenzierte Matt-Pocock-Skills vom Upstream-Tag v1.3.2 (Commit `d1478fcb140db0b93c0c8df17bcfd311844749d0`) wurden unter der bestehenden VPS-Codex-Skill-Root bytegebunden installiert, ohne alte Skill-Roots zu ueberschreiben. Der experimentelle `chief-of-staff` ist nur datierte Wiki-Referenz, kein Agent, keine zweite Authority. Das lokale Skill-Katalog-Readback meldet `UPSTREAM_PIN_MATCH`. Die vorhandenen Spec-Kit-, SPD- und Unity-Director-Grenzen bleiben kanonisch.

Base-main `3c5bb4cabc558795312a0b699e1b73ccd17b7f1f`; auf dem VPS nativ getesteter Code-Commit `b822ff549003dedd526ec5d590d74e09dfe0274c`; GitHub per API publizierter Code-Commit `dcdd15545e5d7b2041f6324f3799c18ca415f2c4`. **Derselbe Git-Tree** `a14636cae126ecdabc038ad7c16c51bb5a1c3b4f` wurde unabhaengig ueber GitHub zurueckgelesen. Die Ursprungs-REDs fuer ein fehlendes Engineering-Modul und fehlende Route wurden tatsaechlich ausgefuehrt; nach Implementierung und nach dem Rebase sind die Tests gruen. Letzter Lauf am VPS auf exakt `b822ff54...`: 73/73 Director, 54/54 Preflight, statischer Unity-Kontrakt, 5/5 Fadenwerk-Gateway, 6/6 Neural-Bridge, 21/21 Node, 22.635 echte C#-Core-Assertions, 4 Fadenwerk-JSON, 10 WorldForge- und 45 Fadenwerk-Validator-Assertions. Git `diff --check` und isolierter Arbeitsbaum ohne Fremddiff bestanden.

Die Wolfram-pruefbaren Gate-Invarianten wurden in deterministische lesende Python-Pruefungen umgesetzt: Aufgaben-DAG zyklusfrei, Approval/Evidence-Dominator vorhanden, Device-Test nicht Merge-Blocker, NPC-Oekonomie-Domainkreise zulaessig und zweite kanonische NPC-Authority unzulaessig. Mutations-RED-Bytepruefung ist **nur Test-Evidence-Metadatenkontrolle**, kein Ersatz fuer native Unity-Pruefung. Keine Unity-/PlayMode-/Android-/Tablet-Runtime dieser Runde ausgefuehrt, weil keine Gameplay-/Editor-Aenderungen. GitHub-PR, exact-head CI und Merge waren bei Eintrag noch offen.

**Gitar-Review #113 — reproduzierte und reparierte Fehler.** Review-Fund 1: ungeschuetzter Host-Katalog (absoluter Maschinenpfad) wird per Root-`.gitignore` ausgeschlossen, mit echtem `git check-ignore` pruefbar. Review-Fund 2: leerer Routingauftrag verursachte einen CLI-Traceback; neue RED-Tests fuer `plan('')`, Leerzeichen und CLI, jetzt fail-closed mit `REFUSED/TASK_REQUIRED`. GREEN auf dem isolierten VPS: 76 Director, 54 Preflight, 6 Neural-Bridge, 5 Gateway, 21 Node, 22.635 C#-Core, 4 Fadenwerk-JSON, 10 WorldForge, 45 Validator; GitHub-CI weiterhin separat zu pruefen. Das persistente Owner-DOCX `Ashen_Crypt_Evidenz_Logbuch.docx` hat laut bisherigem Handoff einen derzeit nicht erreichbaren Dateiverweis; daher dort kein Schreib-/Readback-Erfolg behauptet. Dieser Repo-Handoff bleibt der explizite Nachtrag; die private DOCX-Fortschreibung steht aus.

## Issue #111 initial graph slice (2026-11-22)

- **Code basis:** GitHub main `374fb0641fc720b31e8da21e2149d75f7703ca2e`. Draft PR #114 `feat/ashen-causal-graph-slice-20261122`, bounded read-only canonical `MemoryArchive` graph + Core regression; no new simulation, save or Unity authority.
- **Real RED:** PR HEAD `2577429053c89f83f4e270462b248801920cc4fe`; Actions `44992125683` failed compilation with CS0103 because `CausalKnowledgeGraph` did not yet exist.
- **Real GREEN:** Code + Unity GUID HEAD `ea5796fb0c976ea1db2a3ce2f3ba35cfd75e796b`; Actions `44992232558` core and Neural Hub succeeded after `.meta` repair. Gitar review APPROVED, zero unresolved threads at observed readback. Newer docs/Memory commits still require their **own final exact-head CI verification**.
- **Source-only boundary:** `SOURCE_ONLY` is not a verified runtime effect; the existing VPS Editor reports Unity 6000.6.8f1 but an older project SHA `3c5bb4cabc558795312a0b699e1b73ccd17b7f1f` (verified by project_status), so no Unity Editor/PlayMode/Android/device evidence is asserted for PR #114. SourceRevision is explicit caller-supplied metadata and independently needs checkout binding.
- **Logbook and next gates:** `Memory.md` has exactly one entry `ashen-causal-graph-slice-111`; private ChatGPT Library Nachtrag / original DOCX identity needs independent file-level readback. No final merge/production promotion without exact-head green, independent readback and project evidence gates. Full Issue #111 remains open (Director why API, world/quest index, runtime receipt).

## PR #114 late review correction (22 November 2026)

- **Gitar review source:** [PR #114 review thread](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/114) found an actual source-only graph correctness defect: archived `Refuted` or superseded `KnownMemory` acquisitions still yielded `known-by` proof paths. **Real RED** exact commit `42e2861b2c95e865c601db35bc1969f9ee80c14f` / [CI 44993338918](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44993338918): `FAIL knowledge-graph-refutation-cannot-pretend-current-knowledge`. Fix: only currently admitted `NpcMind.Knowledge(eventId)`, excluding `Refuted`, emits an active `known-by` edge. Historic acquisitions remain source records. Tests also cover replacing suspicion with direct fact. GREEN `5f943d8d4b6a2b5c423e606ca1e4734b0927460d` / [CI 44993403847](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44993403847) completed/success, Core + Neural Hub. Owner replied with RED/GREEN and resolved the Gitar thread; re-read thread state before merge.
- **Native Unity first run, NOT final-source evidence:** Existing VPS Director job `20261122T061827Z-7d34d8da66da` on earlier SHA `1bc8f5c06bcc335ae2de203973bf4cba800c800e`, Unity `6000.6.8f1`, real `EditMode` XML SHA-256 `542dc9e60de60ea883c019b23e0b2c7e7956a203f77d9cfa5793238cc65ea426`: **205/251 pass, 0 fail, 46 skipped** due to uninstalled/unprepared reviewed private art or optional Blender export job IDs. The **3 new graph NUnit tests passed**, but Director correctly marked overall test status `failed` because any skips fail closed. This is evidence of **three actual native passing cases on an earlier SHA**, not approval of the final PR Head.
- **Next gates:** Exact final PR HEAD must pass real C# GitHub CI and **targeted native EditMode Graph NUnit without skips**, and for the source-only pilot collect native PlayMode scoped evidence if available. No physical tablet requirement for this source-only work. Perform SHA/Diff/review and Merge/main readback separately. No new authority or gameplay edits; Issue #111 remains open beyond this slice.

## 22. November 2026 · Figurenvorrat und Bewohner · PR 116, 117, 120

Auf `639a74b03383ebf3eacb3f68f64553b027eab964` (Code von PR 120 ohne den Memory-Eintrag) tragen die Bewohner von Aschengrenze Figuren aus einem Vorrat von 180 Menschen: 30 Arten mit je sechs Aussehen aus `pool.json`, alle in zwei Teilen gebaut (PR 116), Farbvarianten mit Modell, Normal-Textur und Avatar ihres Vorbilds (PR 117). Die Figuren werden Art für Art ausgeteilt; ab der zweiten Runde tragen Bewohner dieselben Körper mit dem Kopf einer anderen Figur gleicher Haut (888 Arbeiter). Core und Spielstand sind unberührt. Die Figuren selbst und ihr Index sind private Kunst und liegen nicht im Repository.

Unity 6000.6.8f1 im Live-Editor mit installiertem Paket, Testläufe über die Unity-CLI (kein Director-Job, keine XML-Prüfsumme): Vollauf auf `ee05e31e4054a7a41d6be881ecfd6573f5e5b0b8` EditMode 261/265 bestanden, 0 Fehler, 4 übersprungen (sie brauchen ein Bauplatz-Projekt oder Blender-Export-IDs), PlayMode 201/203 bestanden, 0 Fehler, 2 übersprungen (lizenzierte Regionsprüfung mit Ausgabeordner). Gezielt auf `8d24b76c97ddf0168b1df7bf6728414d12bea3db` (PR 117 final) Figuren 20/20, Kopftausch 4/4, Bewohner-Aussehen 3/3, Bewohner im Spiel 2/2; auf `639a74b` Bewohner-Aussehen 4/4, Figuren 20/20 über alle 204 gebackenen Figuren, Kopftausch 4/4 über alle Modellpaare, Bewohner im Spiel 2/2. Unterwegs gefunden und behoben: Ein beim Neubacken überschriebenes Modell zeichnete der Editor nicht mehr (Figur ohne Kopf oder unsichtbar), obwohl die Datei stimmte; zwei Figuren lagen über 32.000 Dreiecken; die erste Austeil-Reihenfolge brachte sechs Försterinnen hintereinander. Der Neuro Director hing seit dem Morgen auf einem älteren Stand, weil ein Director-Test Git brauchte (PR 118, 119); `neuro-director sync` auf `d07aa80c4dd5eaaa6523f337c833979818ea5064` bestand alle 76 Prüfungen. Grenzen: Android-Größe nur geschätzt (rund 75–80 MB mehr vor der Kompression), kein Gerätetest, Handwerk, Wachen und Krieger noch an keine Stelle gebunden. Nächste Schritte: APK messen und Mesh-Kompression prüfen, Rollen binden, sobald das Spiel Stellen hat. Ebenfalls heute gemergt: PR 108 (Dorfhäuser), 109 (Kunst Aschepass/Mühlwehr), 110 (Bauplätze), 112 (Figuren-Versuch).

- [Figurensystem, Vorrat und Bewohner](../../BOZO_CHARACTERS.md)
- [GitHub-Core-Lauf zu PR 117](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45009814555)
- [GitHub-Core-Lauf zu PR 120](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45029333784)


## Issue #4 · Kausale Utility-Policies · 2026-11-22

- Ausgangs-`main` `8f0cd525d52ec0a1924af6f93bb635250208cc47`; erster testgebundener SHA `a1927734dab84e787b7d007438f66701cfb4bc59`, Produktionsfix `112e6ad36f93fd35236b857ffdd2f283dbac34f5`, PR #121. Ursache: ein berichtetes Gewalt-Ereignis konnte allein über die aggregierte Feindseligkeit `Attack` erlauben; der wirklich ausgeführte RED-CI-Job 137330476799 schlug bei `policy-indirect-no-hostile-attack` fehl.
- Nur Core-Entscheidungsgrenzen `NpcForgeCalm.cs` / `NpcForgeBehavior.cs` und echte `CausalPolicyTests.cs` verändert: direkte Erinnerung (`Self/Witnessed`) erforderlich, um berichtete Feindseligkeit zu gewaltsamer Bedrohung hochzustufen. Vorhandener Forge-Entscheider, LegalMask, Save und 100-ms-Tick unverändert.
- GREEN-CI-Job 137330772444 auf `112e6ad36f93fd35236b857ffdd2f283dbac34f5`: 22.668 Core-Assertions, 54 Preflights, 76 Director/Skill-Tests und 5 Gateway-Tests; 10.000 Simulationsticks wiederholt. [RED](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062225471) · [GREEN](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45062322997) · [ausführliche Abnahme](../issue-4-causal-policy-20261122/acceptance.md).
- Grenzen: GitHub Unity-PlayMode-Workflow wurde übersprungen; kein nativer Unity-/APK-/Gerätetest in dieser Runde. Das Owner-DOCX wurde aus dieser Verbindung nicht editiert; dieses repositorygebundene Handoff ist der bewusste, als solcher gekennzeichnete Nachtrag, kein Ersatzbeleg für DOCX-Writeback.


## Issue #5 · Kausale Lager-/Story-Folgen · 2026-11-22

- Ausgangs-main `e22a8dac9ca37e2da3de3bc19911712a57dcaf11`, PR #122. Ein echter RED-Test auf `daa02452d3a9928f17927a45f9e5fa79d10064dc` scheiterte bei `slice-intro-input-gate-Greet`: direkte `InteractNpc`-Eingaben konnten die laufende erste Dorfszene umgehen ([CI 45063318565](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45063318565)).
- Einzige produktive Änderung in `ArpgSimulation.cs`: während `ArrivalScenePending` alle unmittelbaren NPC-Kontakte vor Ressourcen-, RNG- und Reputationseffekten verwerfen. Die vorhandenen Test-Szenarien für spätere Kontakte beenden jetzt ausdrücklich zunächst die reale Einstiegsszene. Neue `CausalCampSliceTests` prüfen finale Spende→Parent, Diebstahl→Parent, tatsächliche Sichtlinie Dorn/Mara, erneute Eingaben nach Entscheidung, Ilyan als Root und historische AC5/17/18/19/20-Saves. Keine neue Quest-, NPC-, Save- oder Unity-Authority.
- GREEN Code `f92416d1e612d2c42e0be0f6f80d7ed302ff823d`: [CI 45063991119](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45063991119), **22.702 Core-Assertions**, 54 Preflights, 76 Director/Skill- und 5 Gateway-Tests bestanden. [Technische Abnahme](../issue-5-causal-camp-story-20261122/acceptance.md).
- Native Unity 6000.6.8f1: auf Code-SHA `f92416d1e612d2c42e0be0f6f80d7ed302ff823d` wurden drei echte gezielte PlayMode-Fälle (Ankunft, Lagerdiebstahl, Ilyan) jeweils 1/1 ohne Skips unter Xvfb bestanden; XML-SHA256 `9d72025d7d27ffcf14d1001751d46ac097eed1c27bc4bd4c697de80520570e6e`, `2e790f99ef4077da1808891882d2d3552f4eb97839efb32868501b26c397d8e7`, `9b160c2a8d28fd42dbd2a83eeac654ee2a8ee2539c15a7186c4e169d17a39593`. Bei letzten beiden `taskset -c 0-7` (acht vCPUs) ausschließlich im isolierten Testprozess. GitHub-Unity-CI blieb übersprungen, APK/physisches Gerät nicht getestet. Private DOCX-Identität ohne eigenständigen Writeback-Nachweis unverändert.

## 22. November 2026 · Lebendige Chronik · PR 115 (Issue #99)

Auf `6f0bbf0` (PR-Kopf `10cee1dc932ce5640395e538dac79ca92ed95e01` mit main `8f0cd525d52ec0a1924af6f93bb635250208cc47` zusammengeführt, ohne Konflikte) zeigt die Aschenchronik im Hunger-Journal nur belegte Erlebnisse: eigene, vom Core angenommene Taten am Lager und in der Hunger-Geschichte sowie Aussagen, die Ilyan, Mara oder Dorn in einem vom Core zugelassenen Gruß gemacht haben, jeweils mit Herkunft (selbst erlebt, Augenzeuge, nur berichtet). Augenzeugen geben eine Lager-Nachricht bei echten Treffen im Dorf weiter, Gerüchte wandern keinen Schritt weiter. Gehörte Aussagen stehen als höchstens 64 Verweise im Spielstandsabschnitt `chronicle.v1`, der beim Laden streng geprüft wird; Spielstände ohne den Abschnitt laden unverändert. Die Texte stammen aus elf prüfsummengebundenen Fadenwerk-Dialogen ohne Spielwirkung (nur die Ashen-Anbindung, nicht das verkaufte Plugin). Verhaltensänderung: Dorfgespräche laufen jetzt auch, bevor die Dorfwirtschaft startet.

Nachweise: Core 22.792 Prüfungen erfolgreich, darunter 136 Chronik-Fälle (abgelehnte Aktion, Speichern und Laden, wiederholte Tat, 10.000 Ticks über zwei Kaltstarts, Ablehnung manipulierter Verweise). Unity 6000.6.8f1 im Live-Editor über die Unity-CLI (kein Director-Job): Chronik-EditMode 3/3, Fadenwerk-Anbindung 9/9, Chronik-PlayMode 1/1; Vollauf EditMode 267 von 271, PlayMode 202 von 204, 0 Fehler (übersprungen: Bauplatz-Projekt, Blender-Export-IDs, lizenzierte Regionsprüfung). GitHub-CI auf `10cee1d`: Core und Neural Hub erfolgreich, Gitar freigegeben, beide Gitar-Hinweise erledigt. Grenzen: Die Chronik zeigt Tick-Zahlen und Sprecher-IDs in Kleinschrift („mara“), die Fadenwerk-Texte schreiben Umlaute als ae/oe/ue wie die bestehenden Hunger-Texte; kein Gerätetest. Nach dem Merge von PR 121 und 122 in main schlug auf dem aktualisierten Branch (`ff4bb00`) der Core-Fall `chronicle-suspected-only-is-never-player-fact` fehl: Seit PR 122 antwortet im Dorf niemand vor der Ankunftsszene, und das Testdorf dieses Falls hatte sie nicht abgeschlossen. Der Testaufbau holt das nach; auf `062f6af6ab2b842888f15b79156a0401ea6cb6a5` Core 22.839 Prüfungen erfolgreich, Chronik-PlayMode 1/1. Nächste Schritte: Darstellung glätten (Tag statt Tick, Namen), weitere Chronik-Quellen erst mit eigenem Fadenwerk-Schlüssel.

- [GitHub-Core-Lauf zum PR-Kopf](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/115/checks)


## Issue #6 · Kausal-Regression, Migration und Laufzeitbelege · PR 123 · 2026-11-22

- Ausgangs-main `5d94f238b6494395bdba8b5acef791c6ce53df41`; Commits `5b12e89` (Tests, Fixture, Messlauf), `f45f6a8` (CodeRabbit-Nachbesserung: gleicher Tick, Ablehnungen einzeln geprüft, vollständige Formatmenge, Archivsperre über `Report`), `d883ef9` (Messlauf schreibt die Rechnerlast mit). Keine Produktionsänderung, keine neue Autorität oder Speicherversion.
- Gegenprobe am echten Produktionscode: Laden ohne Ursachenprüfung → rot; beide Gerüchte-Sperren entfernt → rot; nur die Dorf-Sperre entfernt → grün (die Archivsperre genügt); zurückgesetzt → grün.
- CI auf `d883ef9`: Core-Job 137366232380 mit 22.930 Core-Assertions, 54 Preflights, 76 Director/Skill- und 5 Gateway-Tests.
- Unity 6000.6.8f1, Unity-CLI-Lauf im Live-Editor auf main `5d94f23` (kein Director-Job; der PR ändert nichts unter `Assets/`): EditMode 4/4 (XML `82b089c6…`), PlayMode 9/9 (XML `bb06894f…`), Protokollausschnitt ohne Fehler (`4e6a03bd…`).
- APK (Auftrag A): 275.464.965 Byte, SHA-256 `1ced0ebf…`, debug-signiert, ARM64/IL2CPP, 16-KiB-ausgerichtet, targetSdk 36. Das sind 101 MB mehr als am 20.11. (174.366.707 Byte). Haupttreiber: die 180 Figuren des Vorrats (221 MB unkomprimierter Build-Inhalt, davon 171 MB Texturen mit 3 × 512² je Figur und 44,7 MB Meshes) sowie 37,7 MB Inference-Engine-Ressourcen, die nur der Live-Editor als Werkzeugpaket mitbringt (nicht im Repo-Paketstand). Physisches Gerät offen.
- [Technische Abnahme](../issue-6-causal-regression-20261122/acceptance.md)


## 23. November 2026 · Signiertes Google-Play-Paket · PR 124

- Owner-Entscheidungen vom 22.11.: Beta-Updates über eine Google-Play-Testspur, eigener neuer Upload-Schlüssel für Ashen Crypt, Paketgröße bis 3 GB in Ordnung (keine Textur- oder Mesh-Verkleinerung).
- Neuer Menüpunkt „ARE ARPG/3. Build Signed App Bundle for Google Play“: Schlüssel und Passwörter nur aus der Signierdatei, die `ARE_ANDROID_SIGNING` nennt (Datei und Schlüssel außerhalb des Projekts), Versionscode = Sekunden seit 2026-01-01, Spieldaten im Install-time-Asset-Pack, Größenwarnung erst ab 3 GB, danach Projekteinstellungen wie vorher und keine Passwörter mehr im erzeugten Gradle-Projekt.
- Zweites AAB ohne Handgriff gebaut: SHA-256 `c0d1deae…`, Versionscode 469396, `jarsigner` verifiziert mit dem Upload-Zertifikat, `bundletool validate` mit Grundmodul und `UnityDataAssetPack`, Download je Gerät ≈ 275 MB (Grundpaket 31 MB). Gradles Script-Cache im Benutzerordner hielt danach noch Passwort-Kopien; ein privates Werkzeug neben dem Schlüssel entfernt sie nach jedem signierten Bau (Nachsuche 0).
- Unity 6000.6.8f1, Unity-CLI-Lauf im Live-Editor (kein Director-Job): `AndroidPlayBundleTests` 5/5; statische Unity/Android-Prüfung und 54 Preflight-Tests grün. CodeRabbit-Hinweis zur Groß-/Kleinschreibung von Pfaden unter Windows/macOS umgesetzt.
- Offen (Owner): App in der Play Console anlegen und das AAB in eine Testspur hochladen; Gerätetest.
- [Technische Abnahme](../play-app-bundle-20261122/acceptance.md)


## 23. November 2026 · Update-Hinweis, Play-Store-Eintrag · PR 125

- Update-Hinweis beim Spielstart über Google Play In-App Updates: Prüfung einmal pro Start, Fenster im Menüstil, Googles Vollbild-Update auf Wunsch; Gradle-Schritt bindet die Bibliothek ein und schlägt laut fehl, wenn das nicht geht. Nachweis im signierten Paket 28170992 (SHA-256 `34712b90…`).
- Play-Store-Eintrag (außerhalb des Repos, über das Dienstkonto): Texte auf Deutsch und Englisch, App-Symbol und Vorstellungsgrafik aus einer Nahaufnahme der Glutschale bzw. dem Gruft-Tor, 6 Bildschirmfotos aus der echten Spielreise; Datenschutzerklärung unter arelogic.space; Pflichtangaben hat der Owner in der Play Console eingetragen.
- Dienstkonto `play-upload@ashen-crypt-play-108d3c.iam.gserviceaccount.com` mit Rechten nur für Testspuren; die App ist noch ein Entwurf bei Google, darum darf die Schnittstelle nur Entwürfe anlegen. Das Paket liegt als Entwurf im geschlossenen Test, deine Versionshinweise unverändert plus englische Übersetzung.
- Offen (Owner): Tester eintragen und den ersten Release in der Play Console zur Prüfung senden; Produktion verlangt bei neuen Konten 14 Tage geschlossenen Test mit mindestens 12 Testern. Update-Ablauf auf einem Gerät UNVERIFIED.
- [Technische Abnahme](../play-update-prompt-20261123/acceptance.md)

## Unite 2026 Pipeline PR #126 - native acceptance, 2026-11-23

- Scope: source SHA `c64483baf42dbe69d6dc1267d0e83bf0668254a8` (main base `0f21629775670ec2aa8527327ef207d356973c17`), Unity `6000.6.8f1`, Pipeline `1.0.0` in manifest/lock. No production Core, save, replay or world-tick changes. Obsolete forced-overflow village board test removed at owner's express request; other checks retained.
- CI: [Run 45105548253](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45105548253) attempt 1 completed/success on exact source head, 22,930 Core assertions, 54 Python preflights and Neural Hub. Unity CI was SKIPPED, not passed. Gitar review APPROVED that head.
- Native on isolated VPS checkout `/var/lib/unityarpg/workspaces/ashen-unite2026-native-20261123`, with the owner's genuine licensed assets locally present and excluded from Git: Unity 6000.6.8f1: EditMode 252 PASS / 0 FAIL / 28 ignored (XML SHA-256 f396e61273c55724fa75fedaf560a82007ea6a1321155d1bca4d9850db5c13db); PlayMode 177 PASS / 0 FAIL / 28 ignored (XML SHA-256 0c2f7f717fbe1cfeed7e546f067b35171cf2edd7188a832690eee42b7a648a95). Project verification `unity projects verify --strict --expect-editor 6000.6.8f1`: 15,682 files, 0 errors, 0 warnings. Editor import accepted Pipeline 1.0.0; lock remained 1.0.0. The native working tree contains Unity-generated local `ProjectSettings` differences and untracked settings; these were not committed and are not claimed identical to the Git tree.
- Native XML files in private evidence root `/var/lib/unityarpg/.local/share/are-arpg/unite2026-pr126-native-20261123` named `editmode-pr126-after-board-removal.xml` and `playmode-pr126-after-board-removal.xml`. Ignored tests remain explicitly ignored; no APK/AAB, Android device or runtime GPU benchmark claimed.
- Owner persistent `Ashen_Crypt_Evidenz_Logbuch.docx` reference is `libfile_7d3d5793c0048191ac60b4e2d38937bf`, version noted as 1 in the owner host record; it is not visible/editable through the available Library surface, so it was NOT overwritten. This repository handoff and a private Cloud evidence addendum preserve the new round pending authorized document access.
- Next: recheck final documentation commit, merge guards, exact-head CI and reviews. Merge only with passing gates, then independently read `main` and the PR. Issue #48 and Android/Tab A9 validation remain separate.


## 23. November 2026 · Spielerdurchlauf Krypta bis Mühle

- Prüfung der eingefügten Qualitätsdiagnose gegen main `dac396ca3ecc816288e9948fda6783005d94497a`: NPC-Folgen (Issues #2–#6) und Begleiter sind umgesetzt; offen waren ein echter Spielerdurchlauf, Orsels Heilkunst im Spiel (Regeln im Core vorhanden, kein Knopf), die Aschestraße als Rohbau und eine eigene Abmischung.
- Neu: `PlayerDevice` als einzige Eingabestelle der Laufzeit (zehn Dateien mit `Input`-Alias, Verhalten unverändert) und ein PlayMode-Lauf, der das Kapitel nur mit Steuerkreuz und Tipps auf sichtbare Knöpfe spielt und die App nach jedem Durchgang neu öffnet.
- Unity 6000.6.8f1, Unity-CLI im Live-Editor (kein Director-Job), Revision `e85ec0854f1a7c3c064ca3371ac891c5df4e1758`: Lauf PASS in 210 s (972 Frames, 0 Niederlagen, 8 Neustarts), `PlayerDeviceTests` 2/2. Erster Lauf in voller Bildgröße: ganzes Kapitel geschafft, nur die letzte Prüfung (Neustart mitten im Gebiet) war im Test falsch erwartet.
- Grenzen: Gerätetest, Ton, Bildrate, die Entscheidungen Offenlegen/Schweigen und alles hinter der Mühle sind nicht Teil des Laufs.
- Nächste Schritte: Orsels Lehren im Spiel erreichbar machen (Owner-Entscheidung nötig), Aschestraße und Mühlenumgebung aus dem Rohbau, eigene Abmischung.
- [Technische Abnahme](../player-journey-run-20261123/acceptance.md)

## 23. November 2026 · Kampf v2 Fundament und Rüstung (Core), Issue #127

- Owner-Entscheidungen: erst der Spielerkern aus #127, dann Orsels voller Lehrgang nach dem Heilkunst-Entwurf, dann Gegner/Bosse, dann 52 Waffenprofile; freie Heilung auf Knopf 2 entfällt; Rüstung aus Handwerk und Belohnung; Schutz 0/15/30/45 %, Heilwirkung 100/75/50/25 %.
- Neu im Core, nur für Charaktere mit Kampfversion 2: Aktionen mit Ausholen/Treffer/Erholung, Ausdauer, Ausweichen, gehaltener Schutz, Trank mit Wirk-Tick, Ausholen der Nahkampfgegner, Rüstungsplätze und -werte mit eigenem Spielstand-Abschnitt.
- Evidence: `Tests/CoreVerification` ALL PASS 22.994 (64 neu), Unity 6000.6.8f1 kompiliert im Live-Editor (Unity-CLI, kein Director-Job), geprüfter Inhalt `5a2b7020ac9a0a7f5d78acb1c51fa8eea1b1a24d`.
- Grenzen: im Spiel noch nicht sichtbar; weitere Gegnerphasen, Waffenprofile, Kombos, Haltung und Balance offen.
- Nächster Schritt: Touch-Steuerung (Ausweichen, Schutz halten, Trank, Ausdauerbalken), neue Spiele und alte Spielstände auf Kampfversion 2, Testspieler mit Ausweichen.
- [Technische Abnahme](../combat-v2-foundation-20261123/acceptance.md)

## 23. November 2026 · Kampf v2 im Spiel: Touch-Steuerung und Umschaltung, Issue #127

- Neu im Spiel: Neue Spiele im Einstiegsprofil starten mit Kampfversion 2, alte Spielstände dieses Profils wechseln einmal beim Laden (das Legacy-Profil bleibt bei Version 1). SCHUTZ wird gehalten, AUSWEICHEN und TRANK sind neue Knöpfe auf einem äußeren Ring, ein Ausdauerbalken sitzt zwischen Leben und Fokus; Schwung, Treffer und Effekte folgen den Ticks des Core.
- Gefunden und behoben: Ein Ghul mit einem Lebenspunkt darf während Ruins Ausholen fliehen (Test-Vorbereitung angepasst, 27 Fälle); AUSWEICHEN lag auf der NPC-Gesprächskarte (Karte schmaler, neuer HUD-Test auf sechs Bildformaten).
- Evidence: volle PlayMode-Reihe auf `09790f9` 177 PASS / 1 FAIL / 28 ignoriert (Fehlschlag in `acdbed0` behoben, einzeln PASS), volle EditMode-Reihe auf `d8f4ecc` 254 PASS / 0 FAIL / 28 ignoriert, Spielerlauf unter Kampf v2 auf `d8f4ecc` PASS in 268 s.
- Grenzen: Handgefühl auf dem Gerät, Animationen für Ausweichen und Trank, Gegnerphasen und Waffenprofile offen.
- Nächster Schritt: Orsels Heilkunst-Lehrgang mit den Owner-Werten (2 Rationen, 12 Münzen oder 3 Aschenflieder; Wundschluss 180 LP, 15 Fokus, 8 s; Einhandstab 80 %; nach dem Lager, vor den Betten).
- [Technische Abnahme](../combat-v2-touch-20261123/acceptance.md)

## 23. November 2026 · Orsels Heilkunst-Lehrgang und Wundschluss, Issue #127 mit dem Heilkunst-Entwurf

- Owner-Werte: 2 Rationen, 12 Münzen oder 3 Aschenflieder als Zugang; Wundschluss 180 LP plus Boni, 15 Fokus, 8 s Abklingzeit; Orsels Einhandstab 80 %, Beute-Stäbe 100 %; Lehre nach dem Lager, vor den Betten.
- Neu im Spiel: Orsel steht westlich des Krankenunterstands an der Mühle (im Dorf vor der fertigen Krankenstube, falls sie schon umgezogen ist). Sie nimmt eine der drei Hilfen an, lehrt Wundschluss, gibt ihren Stab und ihre Robe. Der Knopf WUNDSCHLUSS heilt den anteilig am schwersten Verletzten (Spieler, Begleiter, Verletzte in den Betten), Rüstung senkt die Heilung, Übung zählt in der Heilkunst.
- Evidence: `Tests/CoreVerification` ALL PASS 23.067 (78 neu), volle PlayMode-Reihe 179 PASS / 0 FAIL / 28 ignoriert, volle EditMode-Reihe 254 PASS / 0 FAIL / 28 ignoriert, Orsel-Spieltest mit echten Tipps PASS, Spielerlauf Mühlenkapitel PASS.
- Grenzen: keine gesprochenen Sätze für Orsel, ihr Stab leiht das Modell des Wanderstabs, Handgefühl und Fokus-Balance (Fokus nur durch Treffer) ungeprüft.
- Nächste Schritte: Gegnerphasen und Bosse (#127 Phase 4), die Waffenprofile (Phase 2), Rüstung aus den Dorfwerkstätten, eigenes Stabmodell und Sprachaufnahmen für Orsel.
- [Technische Abnahme](../orsel-healing-course-20261123/acceptance.md)

## 23. November 2026 · Story-Figuren mit eigenem Look und lebenden Gesichtern

- Owner-Wünsche: einzigartige, schöne Story-Figuren statt Standardfiguren; Dark-Souls-Look aus dem ganzen BoZo-Paket; junge Gesichter, Beine zeigen, sexy innerhalb von 16+; grobes Leinen und mehr Texturen; Orsel in Strumpfhalter-Shorts.
- Neu im Spiel: Ilyan, Mara, Dorn, der Jäger und Orsel tragen eigene Looks mit grobem Leinen und abgenutztem Leder. Sie blinzeln, zeigen eine Miene ihrer Rolle und bewegen beim Sprechen den Mund; der Jäger als Begleiter ebenso.
- Evidence: volle PlayMode-Reihe 181 PASS / 0 FAIL / 28 ignoriert, volle EditMode-Reihe 254 PASS / 0 FAIL / 28 ignoriert (beide auf `9720e977ce6979d4dcf740956677d9cccb0eaf5d`), Figurentests 20/20, Gesichtstests für Lager, Orsel, Begleiter und den Dank des Jägers PASS.
- Grenzen: kein Lippensync zu echter Sprache, keine Narben oder Gesichtsdetails, der Held unverändert; die Hauterkennung der Stoffe arbeitet über die Farbe.
- Owner-Richtung vom selben Tag: Ziel ist ein realistischer, nebliger Dark-Fantasy-Look; diese Looks sind ein Zwischenstand. Reihenfolge: zuerst Issue #48 mit den jetzigen Paketen, danach der Designwechsel.
- Nächste Schritte: offene Blöcke von #48 (Beute v2 mit Vorratsbeuteln, Kampfanimationen, Welt- und Figurenqualität, Audio, Android-Profiling).
- [Technische Abnahme](../story-figures-20261123/acceptance.md)

## 23. November 2026 · Beute v2 und Vorratsbeutel, Issue #48 Block 2

- Owner-Werte: Waffenbeute „Mittel“ (25/45/75 %, Bosse immer; 65/28/7 bis 30/45/25; Bosse 35/50/15; tiefe Champions 2 % Orange), Vorratsbeutel von jedem vierten Gegner mit Münzen nach Gegnerklasse, jeder dritte mit Ration.
- Neu im Spiel: Nicht mehr jeder Gegner lässt eine Waffe fallen. Waffen haben vier Stufen in Weiß, Blau, Gelb und Orange. Leuchtende Vorratsbeutel bringen Münzen und manchmal eine Ration. Wer etwas liegen lässt, kommt nicht aus dem Gebiet.
- Evidence auf `758a66767800319b6ac32d2ee2ffb43f7eba7456`: `Tests/CoreVerification` ALL PASS 23.139 (71 neu), volle EditMode-Reihe 254 PASS / 0 FAIL / 28 ignored, volle PlayMode-Reihe 182 PASS / 0 FAIL / 28 ignored.
- Grenzen: Raten nur gezählt, nicht erspielt; Rationen sind viel seltener (Begleiter beobachten); Münzmodelle aus der privaten Asset Registry nicht verwendet; kein Gerätetest.
- Nächste Schritte: #48 Block 3 (echte Kampfanimationen aus dem vorhandenen Animationspaket: Abwehr, Zauber, Treffer, Tod, Ausweichen, Trank), danach Welt- und Figurenqualität, Audio, Android-Profiling.
- [Technische Abnahme](../loot-v2-20261123/acceptance.md)

## 24. November 2026 · Echte Kampfclips für den Helden, Issue #48 Block 3

- Neu im Spiel: Der Held rollt beim Ausweichen, hebt die Waffe zur Abwehr und hält sie oben, solange gedrückt wird, zuckt bei einem Treffer, hebt beim Trank die Arme und fällt bei der Niederlage um. Alles kommt aus dem vorhandenen lizenzierten Animationspaket und läuft im Takt der Kampf-v2-Aktionen.
- Evidence auf `ae194be53a197c8269b0baa8e92fdf0766937724`: neuer Test `HeroClipsFollowTheCoresActions` PASS, volle EditMode-Reihe 254 PASS / 0 FAIL / 28 ignored, volle PlayMode-Reihe 183 PASS / 0 FAIL / 28 ignored, sieben Animationstests mit privatem Overlay 7/7 PASS auf dem Vorgänger `d423a44`.
- Grenzen: keine echte Trinkbewegung im Paket (Buff-Geste als Ersatz), kein bildgenauer Abgleich von Zauber und Wirkung, nur Standbilder gesichtet, kein Gerätetest; Begleiter und Gegner unverändert.
- Nächste Schritte: #48 Block 4 (Welt- und Figurenqualität), Block 5 (Audio/Stimmen), Block 6 (Android-Profiling auf dem Tab A9, braucht das Gerät des Owners).
- [Technische Abnahme](../combat-clips-20261124/acceptance.md)

## 2026-11-24 — WF-TREE-SOURCE-IDENTITY-20261124

### Change and causal native evidence

`WFEditorUtil.CreateTreeWrapper` previously reused the first wrapper with the same sanitized model name even when the actual prefab source differed. The wrapper now verifies its first nested prefab's immediate source, and uses the source GUID plus invariant local file ID when names collide. Correct existing legacy wrappers keep their path and GUID. A foreign prefab occupying the fallback is explicitly refused.

Independent review found a second defect: `LoadAssetAtPath<GameObject>() == null` does not establish that a destination is free. A real Editor RED replaced two 48-byte raw files with 4428/4480 bytes of prefab data. The added file/directory/meta guard runs before root creation and `SaveAsPrefabAsset`. Negative tests check preserved content and .meta bytes before asserting the explicit exception type. No errors or assertions were globally suppressed.

### Revisions and outcomes

- Original public source blob: `84716dd958879b5a37e1a82ebb2fb5f30ba13a65` on public base `489a3120915a5fdcec4bae83dc1f9afac6a2aa22`.
- Final source blob: `a475b469b3b27b2ffe32c0663682dcba85d83e7f`; SHA256 `a44e44cf53b13e3ea334f5ade4411edec42e829aa5f8e136850cedf1df891189`.
- Final test SHA256: `0f217408de19f7881b62270bbc8360b5661d40ffe75d48e7c4d41d78ff657ede`.
- Identity RED / GREEN: local `c98f014c676818ba147e2ee3343a9b2cd6221d17` / `da661d7e3d86adc8caad8c7158b71c5983fe24bd`; two target failures become two passes; nine other failures remain.
- Occupancy RED: `3b9e16ec7f634ca2bd9be57a2dda22e892dbc02f`, job `20261124T170720Z-f5c65e823d00`; four new target failures, three target passes; full 228 passed / 13 failed / 55 skipped out of 296.
- Final GREEN: executed local `53ddee458092f21cf5a564bbb4c232e460f3be0e`, tree `4289c3d3e08ee2055906de62eb2c45daba807d9b`, job `20261124T171135Z-6e1c21532bc2`, Unity `6000.6.8f1`; all seven target cases pass. Full suite: 232 passed / 9 failed / 55 skipped out of 296. Test bytes are identical between the occupancy RED and GREEN; only the three guard lines change.
- Original final XML SHA256: `2c26655a390dbd4b4a53025bcdac304cda5242cb93f4dc1ae476f48974f00f84`.
- Original final Director receipt SHA256: `9e2f642fd69185cd89c553fd12647cd15c4f62391e8149c5f8d5b08f6977a263`.
- Compact private evidence SHA256: `78b7813ace9d4b5d3d1d42852c53247cb66bc21b02ac25c6011435111abc7b9d`; source/test-only patch SHA256 `3131b8aa4fb8e51706e5eaa116e88baa1aaef16116a090629df145d96a87ce51`.

### Explicit boundaries and next gate

These native runs executed in isolated local clones based on `b5b9fd37401a59c01603035fc30d508c9f65b52e`, with six deterministic fixture script-meta files. That local commit ancestry is not the public PR and must not be pushed. Only the listed source/test/meta bytes are transferred onto the actual public base. The public PR's full tree is not claimed to be the already executed native tree.

The same six private AssetIntake fixture failures and three VillageRetexture failures remain in both occupancy runs. Both Director receipts correctly report `failed / TRACKED_WORKTREE_DIRTY` because Unity auto-serialized ProjectSettings. No Settings values, licensed private assets, generated passcodes or local fixture commits are included in this change. The final readback confirms the Editor exited, zero remaining owned test paths and no untracked game inputs.

Independent source/test review has zero open findings and independently parsed both final XMLs, but this is Draft readiness only. Persistent prefab/model asset roots at the canonical sample caller are covered; variants and child subassets remain unproven. Complete native/private-fixture and exact published-head Core/CI gates remain required before merge. No device, APK, game-frame or deployment success is asserted.

The canonical owner DOCX reference was not visible through the connected file service on this round. Per this handoff's existing fallback, the complete round is retained in repository/private evidence; no unrelated document was created or overwritten. DOCX writeback is not claimed.

### Review follow-up: retain the surviving source wrapper

The repair checks for an existing wrapper with matching source identity even after the earlier legacy wrapper is deleted. It preserves the surviving wrapper's path, GUID and bytes, while retaining the occupied-target guard. Two new deletion regression cases failed before the repair; all nine targeted cases passed afterward with unchanged test code. The complete native acceptance gate and final published-head CI remain separate pending checks.

## 24. November 2026 · Art Forge A, Issue #132

- Lokaler Code `c089fc97ed957fdc4784be35d47892add23c3531`: lesender Vertrag/Canvas über vorhandenen Bau-Modus, Fadenwerk und World Forge; Unity-Studio-Option ausschließlich dokumentiert.
- Tatsächlich geprüft: 35 C#-Fälle, 62 Preflights, 23.143 Core-Assertions, 49/49 neue native Unity-Fälle sowie 20 synthetische und 24 echte JSON-Replay-UI-Gruppen. Native Gesamtsuite Kandidat 273 PASS / 6 FAIL / 52 SKIP, Baseline 224 / 6 / 52; alle 282 gemeinsamen Testergebnisse einschließlich Meldungen identisch.
- Echter Snapshot: 163 Knoten aus 25 Jobs, weitere 38 Budgetdiagnosen. Lizenz ungeprüft, Originalquellhash nur laut Receipt. Kein Import oder Approval.
- Getrennte Testkopie wieder auf Kandidat; generierte getrackte Unity-Einstellungen zurückgesetzt, aktive Editor-Git-Metadaten unverändert. Keine vollständige private Asset-Suite, kein Live-HTTP-/PlayMode-/APK-/Tab-A9-Nachweis.
- Push/Draft-PR erst nach ausdrücklicher Zustimmung; Merge, aktive Editoränderung und externe Aufträge weiter gesperrt. PR #136 bleibt parallel; vor Veröffentlichung Memory/Übergabe erneut abgleichen.
- [Prüfbericht und Grenzen](../art-forge-a-20261124/acceptance.md) · [Maschinenlesbarer Readback](../art-forge-a-20261124/readback.json)

## 24. November 2026 · Art Forge A, Reviewkorrekturen PR #137

- Ausdrücklich freigegebener Ausgangsstand `ef4a270…` wurde bereits auf eigener Branch als baumgleiches `0f1720…` mit Draft-PR #137 veröffentlicht. Die anschließende Fortsetzung korrigiert die drei Reviewbefunde und den unzutreffenden Gleichlängen-Testaufbau; der ältere Vor-Push-Bericht bleibt als historischer Beleg erhalten.
- Native RED-Revision `d4ccaf6118a8c16f914b6ae962432d5f8917df8c`: 49 ArtForge PASS / 8 FAIL. Native GREEN-Revision `5b4602f466352f57de4611eaa907530203299451`, Tree `004ff051687fc1cc004afff67184d57c62619d1d`: 60/60 ArtForge, Core 23.143 und separater C#-Runner 35/35. GitHub-Codeobjekt `0488e5c4c0406bd041a014a6563288a257fe8d0a` hat denselben Tree; Evidence bleibt an die tatsächlich ausgeführte lokale Revision gebunden.
- Zusätzlich 62 Preflights, 28 finale synthetische Browserfälle und 27 Prüfgruppen mit aufgezeichneten echten Unity-State-/Graphbytes bestanden. Budget-/Cancellation-Tests verwenden ausgewiesene Stream-Testseams; Browser-Replay ist kein Live-HTTP-/Authentifizierungs-/Gerätenachweis.
- Gesamte native Suite weiter 284 PASS / 6 FAIL / 52 SKIP; alle 282 gemeinsamen Baselineergebnisse samt Meldungen identisch. Unity Exit 2 und Director `TRACKED_WORKTREE_DIRTY` bleiben erhalten. Eigene erzeugte getrackte Settings erst nach terminalem Testende wiederhergestellt, 18 ungetrackte Settings erhalten, keine eigenen Unity-Prozesse/Locks zurückgelassen.
- Echter Dispatcher: 163 Knoten, 38 Budgetdiagnosen; 10.202 Editorupdates während State-Prüfung. Gemessener maximaler Dispatch in diesem Test 8,409 ms, größter protokollierter kumulativer Startwert aller Tests 41,889 ms. Keine FPS-/Tab-A9-Leistungsbehauptung. Aktiver Editor nur lesend erfasst; HEAD und Diff-Hash gleich GREEN-Baseline, Statusliste parallel verändert ohne Ursachenzuordnung.
- Nächster Schritt: Feature-Branch mit Dokumentationsnachtrag veröffentlichen, GitHub-CI und tatsächlich behobene Reviewthreads zurücklesen. Merge weiter offen; Live-HTTP/Auth, PlayMode, vollständiges privates Overlay, physisches Gerät und Studio-Pilot brauchen eigene Evidence. PR #136 bleibt getrennt; beide Memory-/Übergabeeinträge bei späterer Integration erhalten.
- [Review-Nachtrag](../art-forge-a-20261124/review-fixes.md) · [Review-Readback](../art-forge-a-20261124/review-readback.json)

### 2026-11-25 · PR #137

Art Forge gibt Detailplätze beim Graphwechsel frei. Sechs Browserfälle reproduzierten die Blockade. Nach der Korrektur bestanden 73/73 Browserfälle und 66/66 native EditMode-Tests. Nativer Prüftree `401a076888ca615959191f52ea44fc137a32e9bb`, Director `20261125T004520Z-abd5af1043bb`, XML SHA-256 `bd3919c556099d1194c8aad4c062c05c172d7cf5d9941307de7befbf08c91956`. Chromium prüfte eine gesteuerte HTTP-Testumgebung. Die physische Tablet-Prüfung steht aus.
