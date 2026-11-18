# Ashen Crypt – externe Unity-6-Toolquellen: Quellenaudit

**Datum:** 2026-11-18 (Europe/Berlin). **Unity-Projektpin:** `6000.6.8f1`.
**Scope:** Quellcode-/README-/Lizenz-/Git-Tree-Inventur; **kein Unity-Editor-Paketimport, keine Ausführung der Upstream-Tools und keine neu autorisierte MCP-Bridge.**
**Änderungsfläche:** `Tools/Director/neural_hub/upstream_sources.json`, `sources.mjs`, `sources.test.mjs`, bestehender Neural Hub im Draft-PR [#87](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/87).

Die 13 Quellen wurden jeweils gegen einen **exakten GitHub-Commit**, die GitHub-Lizenzdatei samt Blob-SHA und einen **vollständig gelesenen Git-Tree** abgeglichen. Einzelne Toolzahlen aus README und Nutzerrecherche sind **Anbieterangaben**, keine unabhängigen 6000.6.8f1-Tooltests. Alle Einträge bleiben `SOURCE_INSPECTED_ONLY`; `allowExecution=false` und `unityBuildVerified=false`.

| Quelle | Angebotsumfang (nicht getestet) | SKILL.md im inspizierten Tree | Root-Lizenz | Integrationsentscheidung |
| --- | --- | ---: | --- | --- |
| [Besty0728/Unity-Skills](https://github.com/Besty0728/Unity-Skills) | **873** REST-Skills laut aktuellem README (ältere 839 überholt) | 2 (REST-Definitionen überwiegend in C#) | MIT | **S:** Sicherheits-/Permission-/Rollback-Muster, ausgewählte Inspector-Skills isoliert prüfen. Kein Bypass-Modus. |
| [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server) | 347 benannte Operationen; README nennt separat 80 exponierte MCP-Werkzeuge und 269 erweiterte Abrufe (nicht addieren) | 0 | **AnkleBreaker Open License v1.0 – keine allgemeine MIT-Lizenz**, attribution-/kommerziell eingeschränkt | **B/gesperrt:** Lizenzprüfung und nur Design-/Queue-/Audit-Ideen, kein fremder Code im ausgelieferten Plugin. |
| [AlexeyPerov/Unity-Open-MCP](https://github.com/AlexeyPerov/Unity-Open-MCP) | 270+ typisierte Tools | 23 | MIT | **S:** höchster Pilotnutzen für GameView/Device Simulator, Touch-Simulation, Checkpoints und visuelle Regression. Editor-Test isoliert, kein Android-Gerätenachweis aus Device Simulator. |
| [german-krasnikov/unity-biome-mcp](https://github.com/german-krasnikov/unity-biome-mcp) | 160 MCP-Tools, 12 fachliche Skills, 4 Agenten | 12 | MIT + Third-Party Notices | **A:** PlayTest-DSL, Screenshot-Diffs, Batch-Validierung. Nur in isoliertem Unity-6-Testprojekt. |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) | 70+ Tools und RQL/Erweiterungen | 0 im Tree (Skills können erzeugt werden) | Apache-2.0, transitiv gesondert | **S:** RQL, eigene `[AiTool]`-Adapter, Extension-Discovery; Script-Execute und Runtime-MCP **nicht** ungeprüft im produktiven Wahrheitspfad. |
| [AndreySkyFoxSidorov/UnifiedUnityMCP](https://github.com/AndreySkyFoxSidorov/UnifiedUnityMCP) | ca. 52 Tools | 17 | MIT | **B:** native C#-Editor-HTTP-Bridge nur als alternativ evaluierbarer Adapter, keine zweite aktive Bridge. |
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | ca. 50 gruppierte MCP-Einstiegspunkte | 4 | MIT | **S:** Editorsteuerung, Assets, Skripte, Playtests, Toolgruppen; nur einen Bridge-Provider nach unabhängiger Kompatibilitätsprüfung auswählen. |
| [Unity-Technologies/skills](https://github.com/Unity-Technologies/skills) | **37** entdeckte `SKILL.md` am inspizierten Commit | 37 | **Unity Companion License** | **S/bereits katalogisiert:** mit lokalen 44 offiziellen Unity-Skillnamen abgleichen, keine Doppelinstallation oder pauschale Weiterverteilung. |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) | ca. 30 Tools + 7 Resources laut README | 0 | MIT | **B:** Ressourcen-Lesen und Menü-/Package-Audits als Referenz, Editor-Mutationsfunktionen gesperrt. |
| [mflRevan/unity-control-protocol](https://github.com/mflRevan/unity-control-protocol) | ca. 30 Rust-CLI-Kommandos | 17 | MIT (Copyright-Platzhalter im Root-Lizenztext vor Redistribuierung klären) | **A:** UI Toolkit, Screenshots, Profiler und Blockade-/Modal-Dialogdiagnose; nie Dialoge ohne überprüfte Policy automatisch bestätigen. |
| [hatayama/unity-cli-loop](https://github.com/hatayama/unity-cli-loop) | ca. 20+ primäre CLI-Kommandos | **72** im Tree (überlappende Client-/Versionenpfade) | MIT | **A:** CLI-Test-/Kompilierungsloop und Hot-Reload-Muster; keine doppelte Editorinstanz starten. |
| [hatayama/unity-ai-cli-bridge](https://github.com/hatayama/unity-ai-cli-bridge) | ca. 11 Direct-Bridge-Kommandos | 16 (teilweise uLoop-bezogen) | MIT | **B:** experimenteller Zugriff auf Unitys AI-MCP-Bridge, nur falls passendes Paket installiert und autorisiert ist. |
| [AccelByte/accelbyte-unity-mcp](https://github.com/AccelByte/accelbyte-unity-mcp) | ca. 8 fokussierte Tools | 0 | Apache-2.0 | **B:** uGUI-Prefab-Generator, nützlich nur bei vorhandenem AccelByte-UI-/Backend-Stack; kein pauschaler Import. |

### Integration in unsere 28 Fachabteilungen

Die vorhandenen 28 Director-Fachabteilungen bleiben bestehen; die **Anzahl der Fähigkeiten innerhalb jeder Abteilung ist nicht beschränkt**. Jeder der 13 Einträge enthält ein Array `departments` und beschriebene `capabilityAreas`. Der Director kann mit `director_source_discover` für etwa `testing`, `world_design`, `user_interface` oder `shaders` die passenden Quellen finden; `director_sources` meldet das gesamte Quelleninventar. Beide MCP-Funktionen sind **nur lesend**, auf das Architektur-Snapshot-ID-Gate gebunden und geben **niemals** installierte oder getestete Unity-Funktionen vor.

Der unabhängige VPS-Katalog hatte 88 eindeutige Skillnamen (44 offizielle Unity, 29 GameDev, 15 Spec); davon werden bereits 43 im Neural-Director-Fachkatalog referenziert, 45 sind noch keiner Fachabteilung zugeordnet. Diese 88 sind **keine** verfügbaren MCP-Werkzeuge und dürfen **nicht** zur Summe der externen Anbieterzahlen addiert werden. Offizielle Skilldateien und Upstream-Tree-Snapshots unterscheiden sich zeitlich.

### Aufnahme-/Freigabegrenzen

1. **Source**: unveränderlicher Repo-Commit, Root-Lizenzblob und unveränderliche Registry-SHA; branch- oder `latest`-Alias nicht als Beweis.
2. **Compliance**: Third-Party-Lizenzen, Unity Companion License, AnkleBreaker Open License und Asset-Store-EULA separat. Kein fremder Code im Plugin ohne passende Lizenznachweise.
3. **Discovery**: Ein Skill-/Toolname ist nur ein Kandidat; Toolzahl, SDK-Version und angebliche Unity-6-Kompatibilität bis zum lokalen Compile-/Editor-Test als ungeprüft kennzeichnen.
4. **Routing**: Spiel-Core, Save, Quest-/NPC-Authority und 100-ms-Tick unverändert. Alle externen Bridges sind austauschbare **Adapter hinter dem einzigen Director/Sovott-Sicherheitsgate**.
5. **Mutation**: Kein Roslyn/`script-execute`, REST-Bypass, Auto-Dialogfreigabe, Asset-Kauf, automatischer Package-Import oder Runtime-MCP in der Spielautorität ohne eigenen autorisierten Plan, Revision-Check, Approval, Tests und Readback.
6. **Evidence**: `SOURCE_INSPECTED_ONLY` ≠ `INSTALLED` ≠ `EDITMODE_VERIFIED` ≠ `PLAYMODE_VERIFIED` ≠ `ANDROID_DEVICE_VERIFIED`. Ein Container-Protokolltest belegt keines dieser Produkt-Level.
7. **Aktivierung**: Erst ein enger Prototyp des Editor-/Device-Inspektors, dann einen Anbieter nachweislich auswählen. Keine 13 gleichzeitigen Editor-MCP-Server.

### Offene Arbeit

- Volle individuelle REST-/C#-Toolnamen und alle 873 Besty-Funktionen sind **noch nicht einzeln in ausführbare Director-Verträge übersetzt**. Das ist eine eigenständige statische API-Extraction mit Lizenz-/Dependency- und Risikoklassifikation, danach Review.
- Unterschiedliche Upstream-Funktionen anhand Signatur/Semantik deduplizieren; aktuelle Unity-6-URP-/Android-Abhängigkeiten und `Packages/packages-lock.json` nachweisen; sichere Probe-API wählen.
- Draft-PR #87 gegen fortgeschrittenes `main` konfliktfrei abgleichen; neueste GitHub-CI am exakten Head und unabhängige Readbacks, erst danach Integration/Promotion.
- Nachweis echter Unity Editor-, PlayMode-, Asset-, Android-Geräte- und Laufzeitwirkung separat; diese Runde verändert keine Runtime.

**Primärbelege:** Zu jedem Repository steht die genaue volle Git-SHA, Lizenz-Blob-SHA, Anzahl tatsächlich gefundener Skill-Pfade, erlaubter Fachbereich und unfreigegebene Effektklasse in `Tools/Director/neural_hub/upstream_sources.json`. `sources.mjs` pinnt alle 13 inspizierten SHAs und verweigert Drift. Die JSON-Metadaten enthalten keine externen Quellcodekopien oder Ausführungsanweisungen.
