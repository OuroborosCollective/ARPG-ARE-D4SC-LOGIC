# Ashen Neural Hub – isolierter mcp-use-Pilot

28 Fachberater-Schnittstellen + `director_consult` und `director_snapshot`, dazu 28 Beratungs-Prompts. Nur lesende, regelbasierte Entwicklungsvorschläge anhand bereitgestellter GitHub-Issues; **keine 28 laufenden KI-Modelle**. mcp-use 2.12.7 und Zod 4.6.7. Unity 6000.6.8f1.

**Abnahmegrenzen:** `tools/list`, `tools/call`, `prompts/list` nur im isolierten MCP-Test; keine Unity-Projektausführung, kein externer Netzport, keine produktive Autorität. Der Server prüft die erwarteten Byte-Hashes gegen `docs/ARCHITECTURE.md` und `neural_catalog.json`. Git-Revision bleibt ein *claim* bis der Director sie selbst mit dem kanonischen Git-HEAD abgleicht. Der Server behandelt übergebene Issue-Daten als NICHT unabhängig verifiziert.

## Container

Buildkontext ist das **Repository-Root**, nicht dieses Verzeichnis.

```sh
docker build -f Tools/Director/neural_hub/Dockerfile -t ashen-neural-hub:pilot .
docker run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges \
  -e ASHEN_REPOSITORY_SHA="<EXAKTER_GIT_SHA>" \
  -e ASHEN_ARCHITECTURE_SHA256="<TATSAECHLICHER_DATEI_SHA256>" \
  -e ASHEN_CATALOG_SHA256="<TATSAECHLICHER_KATALOG_SHA256>" \
  ashen-neural-hub:pilot
```

Die Konfiguration bindet **ausschließlich 127.0.0.1 innerhalb des Containers**. Kein `-p`, keine weitere Docker-Bridge und kein Sovott-Token. Über `docker exec` kann ein autorisierter lokaler Testclient in demselben Container laufen; ein Docker-Netzwerk-Tunnel wäre eine separate Sicherheits-/Auth-Aufgabe. Paketinstallation ist nur im Docker-Build gestattet. Der Dockerfile verwendet vorerst npm ohne versioniertes vollständiges Lockfile und ist damit **nicht produktionsfreigegeben**; vor Promotion müssen Lockfile, Transitiven, CVEs und ein unveränderlicher Image-Digest geprüft werden.

```sh
cd Tools/Director/neural_hub
npm install --ignore-scripts
npm test
```

`director_consult` liefert bestenfalls Vorschlagskandidaten, keine autorisierten Umsetzungen. Die nächsten notwendigen Schichten: echte repo-revisionsgebundene GitHub-Issue-Abfrage über autorisierte Sovott-Tools; zusätzliche technische Source/Skill-Provenienz und unabhängige Director-Review; aktualisierter Owner-Architekturvorschlag mit strengem CAS und Evidence.
