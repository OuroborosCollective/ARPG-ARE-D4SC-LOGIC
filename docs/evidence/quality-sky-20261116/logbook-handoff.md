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

