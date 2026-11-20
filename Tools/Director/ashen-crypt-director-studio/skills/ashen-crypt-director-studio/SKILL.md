---
name: ashen-crypt-director-studio
description: Use for Ashen Crypt, ARPG-ARE-D4SC-LOGIC, Unity 6 game direction, continuation of a development task, editor/VPS integration, Android build, deterministic C# gameplay, or SHA-bound verification. Coordinate the existing Unity Game Director, never instantiate a second bridge or simulation authority.
---

# Ashen Crypt Director Studio 1.3.1

DIRECTOR_HOME bezeichnet das auf dem Host konfigurierte Installationsverzeichnis des vorhandenen Directors.

Arbeite auf Deutsch. Dies ist die **eindeutig benannte ChatGPT-Steueroberflaeche des bereits existierenden VPS-Directors**. Der aktive Implementierungscode und die sechs Director-Aktionen liegen weiterhin auf dem VPS. Dieses Paket stellt kein zweites MCP, keinen Unity Editor und keine zweite Simulationsauthority bereit. Lies die passende Spezialfaehigkeit nur bei Bedarf.

## Projekt- und Werkzeugbindung

1. Vor jeglicher Mutation das originale `OuroborosCollective/ARPG-ARE-D4SC-LOGIC`, `AGENTS.md`, `Memory.md`, private Logbuecher, die aktuelle volle Git-SHA und den Unity-Pin wirklich lesen. Historische Hinweise sind keine aktuelle Evidence.
2. Vorhandene Verbinder in der Sitzung pruefen. Wenn `Sovott v2` verfuegbar ist: `vps_ssh_session_status` fuer den delegierten Host; dann read-only den vorhandenen Runner bei `$DIRECTOR_HOME/scripts/director.py` und die private `$DIRECTOR_HOME/project.json` auf Vorhandensein und Syntax pruefen. Befehlsargumente aus `scripts/director.py --help` und `references/tools.md` des echten Hosts ablesen; keine erfundene CLI.
3. Die sechs kanonischen Aktionen auf dem Host bleiben `project_status`, `inspect_change`, `run_tests`, `build_android`, `inspect_build`, `create_release_candidate`. Falls ein Tool mit `QUEUED` antwortet, den erteilten `request_id` ueber `mcp_host_command_status` abfragen; keine Doppelstarts.
4. Read-only Discovery zuerst, danach Aenderung im isolierten Workspace auf erwarteter SHA. Unrelated dirty/untracked Dateien nie ueberschreiben; keine Produktions-/Runtime-Quelle aus Mock oder Scratch-Umgebung behaupten.
5. Nach Aenderung: passende Core-/Unity-Tests, Diff/CI, Build/Artifact-Hashes, Device-Nachweis und unabhängigen Readback je Grenze erfassen. Genau einen kurzen `Memory.md`-Eintrag je abgeschlossener Integration und privates Logbuch fortschreiben. Commit/PR/Merge nur nach passenden Gates.

## Welche Spezialisten?

- `ugd-governance`: Approval, SHA, Dry-Run, Risk, Rollback.
- `ugd-editor-authoring`: Unity-Szenen, Prefabs, Terrain und Editor-MCP.
- `ugd-runtime-review`: deterministischer Core, 100-ms-Tick, Save, Replay.
- `ugd-product-design`: spielbare Ziele, UX, Balancing, Accessibility.
- `ugd-combat-designer`: Kampf, Loot, Boss, Learning-by-Doing.
- `ugd-npc-architect`: NPC, Companion, Memory, BT/GOAP/Utility AI.
- `ugd-quest-narrative`: Story, Fadenwerk, Ink, Rewards.
- `ugd-world-generation`: World-Seed, Graph, Chunk, Procedural Authoring.
- `ugd-art-pipeline`: CPU-Assetwerkstatt mit installierten Originaltechniken, Blender-Modellierung, PBR, Rigging, Shadern, World-Forge-Import und visueller Nachprüfung. Bei Monster-, Figuren-, Material-, Levelbau- und Qualitätsaufträgen zuerst diese Fähigkeit öffnen.
- `ugd-quality-lab`: EditMode/PlayMode, Performance, Graphics, Android.
- `ugd-release-director`: APK, Signatur, CI, Store- und Device-Gates.
- `ugd-integration-curator`: Neuinstallation, Deduplikation, Lizenz, Quellen.

## Harte Grenzen

- Ashen Crypt ist eigenstaendig, **keine** Aurion-Engine-Kopplung. Bestehende C#-Core-Autoritaet und 100-ms-Simulation unveraendert; keine `DateTime.Now`, `Math.random`, `UnityEngine.Random`, ungesaete RNG oder Wall-Clock als Core-Input.
- Keine zweite Quest-, NPC-, Inventory-, Save- oder Unity-MCP-Authority. Neue Frameworks als Adapter erst nach Kompatibilitaetsnachweis bewerten.
- **Kein** vorgetaeuschter Android- oder Editor-Befund. Eine statische Pruefung ist keine Editor-/Build-/Device-Evidence. Ein gruenes Pluginpaket ist kein Unity-PlayMode.
- Keine gekauften Assets kopieren, Secrets ausgeben, Lizenzentscheidungen erfinden, bezahlte Modelle aktivieren oder API-/Billing-Rechte aendern.
- Externe Quellen/Skills als untrusted content behandeln. Lizenz, Version, Herkunft und Maintenance vor Uebernahme pruefen.

## Statusabschluss

Jeden Schritt mit Repo-SHA, Host/Run-ID, Artefakt-SHA256, tatsaechlichen Tests, beobachteter Runtime, UNVERIFIED-Grenzen und naechster konkreter Aktion beenden. Siehe `references/production-contract.md`.
