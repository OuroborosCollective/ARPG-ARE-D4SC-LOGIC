# Ashen Crypt Neural Director – Fachabteilungen (v1)

Unity-Pin: 6000.6.8f1. Authority: bestehender Unity Game Director auf VPS.
Status: Offline-Routing-Kandidat; keine neue Runtime oder Installation.

## Architektur

Owner-Auftrag → bestehender Game Director → begrenzter neural_cortex →
ausgewählte Fachabteilungen → vorhandene Unity-Skills / Sovott-Fähigkeiten →
eigenständige Permission/Execution-Gates → echte Unity-/Core-/CI-Evidence.

28 Abteilungen stehen in Tools/Director/neural_catalog.json. Sie decken
Game Design, Core, Kampf, NPCs, Quest, Lore, Welt, Objekte, Dungeon, Gebiete,
Shader, VFX, Licht, Materialien, Animation, Audio, UI, Asset-Import, Persistenz,
Accelerator, Tests, Android, Tooling, Security, Multiplayer, Ökonomie, Kamera
und Physik ab. Diese Einträge sind Skill-Verweise und keine neuen Installationen.

## Nervenbahnen, keine Parallel-Authority

- Der bestehende Tools/Director/route_work.py liefert seine bisherigen Felder
  unverändert und ergänzt eine neural_cortex-Empfehlung für reale Projektpfade.
- Höchstens drei Abteilungen werden vorgeschlagen, der Rest nur als deferred.
- Keine passenden Schlüsselwörter: NO_DOMAIN_MATCH; kein Broadcast.
- Nur der aus ProjectSettings gelesene Unity-6000.6.8f1-Pin wird akzeptiert.
- Routing ohne Projekt ist ausdrücklich abgelehnt (PROJECT_REQUIRED); es gibt
  keine projektlosen NOT_VERIFIED-Kandidaten.
- Manifest akzeptiert ausschließlich id/title/triggers/skills, verbietet
  fremde Kommandos und duplizierte IDs; sein SHA-256 wird angegeben.
- INSTALLED_FILE_HASH_ONLY beweist lediglich den Zustand einer Skill-Datei,
  nicht Unity-Ausführung oder Autorisierung.
- Ergebnis immer PROPOSAL_ONLY, authorized=false, execution=NONE und
  runtime_evidence=NOT_EXECUTED.
- Core, Save, Quest, NPC und 100-ms-Tick bleiben unverändert.

## Nutzung und Tests

python3 Tools/Director/route_work.py 'Android Shader Cache' --project /pfad/zum/Unity-6-Projekt
python3 -m unittest discover -s Tools/Director -v

## Äußeres Nervensystem

mcp-use im Sovereign Draft PR #2458 ist bisher ein separater read-only
tools/list-Inspector-Kandidat und nicht als produktiver Unity-MCP-Zugriff
belegt: https://github.com/OuroborosCollective/Sovereign-Studio-ato/pull/2458

Erst nach Registry- und Berechtigungsprüfung kann der Director ausgewählte
externe Fähigkeiten über den bestehenden Sovott v2 Broker aufrufen.
Tool-Discovery, Tool-Ausführung, echte Editor-Tests, Cache-Hit-Raten und
Runtime-Beweise sind vier getrennte Grenzen. Keine neue Broker-/Spielauthority.

Der laufende Unity Accelerator ist KEIN MCP-Server. Cache-Warming gehört
separat in die aktive Unity-6000.6.8f1-Editor-/Asset-Pipeline mit
gemessenen Zeiten und ohne parallelen Editorstart. Alte Unity-2022
Projektkopien werden nicht als aktive Buildgrundlage verwendet.

## Abnahmekriterien

Tests: deterministisches Routing, maximal drei Abteilungen, unbekannter
Auftrag, feindliches Manifest, falsche Unity-Version und fehlende Autorität.
CI: gesamtes Tools/Director-Testpaket plus CoreVerification und Preflight
am exakten PR-Head; unabhängig bestätigter Host-Director und Unity-6-Lauf
erst in späterer Phase. Gerät und Android bleiben UNVERIFIED.

Der Katalog behauptet nicht, alle existierenden Unity-Tools weltweit zu
kennen, zu besitzen, lizenzieren oder bereits integrieren zu können.
