# Ashen Neural Hub – isolierter mcp-use-Pilot

28 Fachberater-Schnittstellen + `director_consult`, `director_snapshot`, `director_sources` und `director_source_discover`, dazu 28 Beratungs-Prompts. Nur lesende, regelbasierte Entwicklungsvorschläge anhand bereitgestellter GitHub-Issues; **keine 28 laufenden KI-Modelle**. mcp-use 2.12.7 und Zod 4.6.7. Unity 6000.6.8f1.

**Abnahmegrenzen:** `tools/list`, `tools/call`, `prompts/list` nur im isolierten MCP-Test; keine Unity-Projektausführung, kein externer Netzport, keine produktive Autorität. Der Server prüft die erwarteten Byte-Hashes gegen `docs/ARCHITECTURE.md` und `neural_catalog.json`. Git-Revision bleibt ein *claim* bis der Director sie selbst mit dem kanonischen Git-HEAD abgleicht. Der Server behandelt übergebene Issue-Daten als NICHT unabhängig verifiziert.

## Container

Buildkontext ist das **Repository-Root**, nicht dieses Verzeichnis.

```sh
docker build -f Tools/Director/neural_hub/Dockerfile -t ashen-neural-hub:pilot .
docker run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges \
  -e ASHEN_REPOSITORY_SHA="<EXAKTER_GIT_SHA>" \
  -e ASHEN_ARCHITECTURE_SHA256="<TATSAECHLICHER_DATEI_SHA256>" \
  -e ASHEN_CATALOG_SHA256="<TATSAECHLICHER_KATALOG_SHA256>" \
  -e ASHEN_SOURCES_SHA256="<TATSAECHLICHER_QUELLENREGISTER_SHA256>" \
  ashen-neural-hub:pilot
```

Die Konfiguration bindet **ausschließlich 127.0.0.1 innerhalb des Containers**. Kein `-p`, keine weitere Docker-Bridge und kein Sovott-Token. Über `docker exec` kann ein autorisierter lokaler Testclient in demselben Container laufen; ein Docker-Netzwerk-Tunnel wäre eine separate Sicherheits-/Auth-Aufgabe. Paketinstallation ist nur im Docker-Build gestattet. `package-lock.json` pinnt den vollständigen npm-Auflösungsbaum; der Dockerfile verwendet `npm ci --omit=optional --ignore-scripts`. Der getrennte Audit vom 18.11.2026 ergab **0 bekannte npm-Advisories** für den geprüften Stand, was keine allgemeine Sicherheitsgarantie ist. Das erste unveränderliche Pilot-Image wurde als `sha256:d95ebc34e8d2ce1e30b9aeebfba757f13e23db06826731fbb4f48af52f308f33` zu Git-SHA `3d6fd8882301a948d0855e1d928d96423b6a658e` geprüft. Produktivfreigabe, Authentifizierung und spätere Artefakt-/Versionsgleichheit sind noch offen.

Paketinstallation ist nur im Docker-Build gestattet; auf dem Host werden **keine** Abhängigkeiten installiert. `advisory.test.mjs` läuft im bereits gebauten Image mit denselben Container-Schutzoptionen (kein Host-`npm install`).

```sh
docker run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges \
  ashen-neural-hub:pilot node --test advisory.test.mjs
```

`director_consult` liefert bestenfalls Vorschlagskandidaten, keine autorisierten Umsetzungen. Die nächsten notwendigen Schichten: echte repo-revisionsgebundene GitHub-Issue-Abfrage über autorisierte Sovott-Tools; zusätzliche technische Source/Skill-Provenienz und unabhängige Director-Review; aktualisierter Owner-Architekturvorschlag mit strengem CAS und Evidence.

### Echter isolierter Protokoll-Canary

`mcp_canary.mjs` eröffnet einen flüchtigen localhost-MCP-Server und überprüft echte JSON-RPC/SSE-Antworten: `initialize`, `tools/list` (30 Werkzeuge), `prompts/list` (28 Prompts), `tools/call` auf `director_snapshot`, `advice_world_design` und `director_consult`. Er hinterlässt keinen dauerhaften Container. Die Beratungsantwort bewertet GitHub-Issues nur als vom Director **gelieferte, nicht unabhängig verifizierte** Quelldaten. Das Image startet auch im Test mit `--network none`; keine Veröffentlichung mit `-p`.

### Externe Werkzeugquellen (13 separate Upstreams)

Die versionierte Datei `upstream_sources.json` bindet alle 13 vom Owner gelieferten Unity-Repositories an exakte Git-Commit-SHAs, originale Lizenz-Blob-SHAs, entdeckte `SKILL.md`-Pfade und bereits existierende Director-Fachgebiete. `director_sources` gibt den Katalogstatus aus; `director_source_discover` liefert eine begrenzte Empfehlungsliste für ein Fachgebiet. Keine Tool-Ausführung, kein Editor-Mutationstool und keine fremde KI-Autorität werden aktiviert. Die Quellenangaben und Toolzahlen stammen aus README-/Lizenz-/Tree-Inspektionen, die angeblichen Toolzahlen bleiben ausdrücklich **UNVERIFIED** als voll funktionierender Unity-6000.6.8f1-Toolumfang.

Besty gibt aktuell 873 REST-Skills an, nicht 839. AnkleBreaker unterscheidet 347 benannte Operationen, 80 MCP-Werkzeuge und 269 On-Demand-Funktionen und benutzt eine restriktive Sonderlizenz (keine ungeprüfte Übernahme in ein redistribuierbares Plugin). Offizielle Unity-Skills stehen unter Unity Companion License. Die 37 im offiziellen GitHub-Baum sichtbaren Skill-Dateien und die 44 lokal inventarisierten offiziellen Skillnamen gehören unterschiedlichen Snapshots an und dürfen nicht einfach doppelt addiert werden. Das Abgleichen von 88 lokalen Skills mit externen Inhalten ist eine getrennte Eignungsprüfung.

**Abnahme:** `node --test *.test.mjs` plus `mcp_canary.mjs` prüfen die echte Discovery; der CI-Job erzwingt dazu die SHA-256-Bindung über `ASHEN_SOURCES_SHA256`. Der Hub selbst hat weder Credentials noch Host-/Unity-Zugang. Beim Dockerbau kopiert die Allowlist ausschließlich Metadaten und eigenen Code, keine fremden Upstream-Pakete.
