# Evidence- und Produktionsvertrag

DIRECTOR_HOME bezeichnet das auf dem Host konfigurierte Installationsverzeichnis des vorhandenen Directors.

Sicherer Ablauf: aktuelle Projekt-Identitaet -> SHA/Memory/AGENTS -> Aenderungsgraph -> isolierter Diff -> lokaler Test -> Unity EditMode/PlayMode -> Artefakt/Signatur -> Samsung Galaxy Tab A9 -> unabh. Readback -> Memory.md -> Merge.

**Evidence-Grenzen**: `Quelle` (Git-SHA und Files), `Core` (deterministischer Test), `Unity` (echter Editor mit PID/Pin), `Build` (vollstaendiger Job + Artefakt-Hash), `Device` (adb/installierter Build/Geraeteidentitaet), `Runtime` (tatsaechlicher Tick/Gameplay), `Persistenz` (echtes Save/Restore) separat dokumentieren. Status `PASS`, `FAILED`, `BLOCKED`, `UNVERIFIED`, `QUEUED` nicht vermischen.

Der bestehende Editor- und Director-Host verbleibt `$DIRECTOR_HOME`. Dieses ChatGPT-Paket laesst die Host-Python-Runner am Ort und fuegt KEIN eigenes portables `mcp.json` hinzu; andernfalls wuerde ein lokaler StdIO-Pfad auf dem ChatGPT-Server als vermeintlich funktionierende Verbindung deklariert. Verwende den bereits vorhandenen Sovott-v2-Zugriff nur wenn tatsaechlich in dieser Sitzung verbunden.

## Bereits vorhandene Kataloge (VPS 2026-11-16)
44 Eintraege `official-unity-catalog.json`; 29 Eintraege `gamedev-catalog.json`; 15 `spec-workflow-catalog.json`. Damals alle Hashes lokal geprueft, heute erneut pruefen. 6 echte lokale Director-Aktionen; nicht noch einmal importieren.

Spezielle Host-Validatoren: `scripts/integration_audit.py`, `scripts/validate_quest_graph.py`, `scripts/validate_world_descriptor.py`, `scripts/asset_intake.py`. Diese Skripte laufen *nur* wenn der aktuelle Host sie wirklich ausfuehrt; nicht im ChatGPT-Skill-ZIP enthalten und nicht als bereits ausgefuehrt angeben.
