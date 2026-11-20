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
