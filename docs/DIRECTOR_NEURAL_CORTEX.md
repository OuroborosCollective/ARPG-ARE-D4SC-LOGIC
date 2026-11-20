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


## Harness-aware Plugin-Evaluations (isolierter Pilot, 2026-11-19)

Quelle: [OpenAI Cookbook – Harness-aware evaluation of plugins](https://github.com/openai/openai-cookbook/blob/c3bb658dfa413a0b5d9c0bf77c8059dd06b9998b/examples/partners/harness_aware_plugin_evals/harness_aware_plugin_evals.ipynb). Das kleine BLS-Beispiel nutzt einen gemeinsamen Testkorpus und trennt direkten Tool-Test, eigenständigen Modellloop und Codex-Produktharness. Die Beispiel-Passraten sind **keine** Ashen-Crypt-Messungen.

**Eine bestehende Authority, drei voneinander getrennte Evidenzschichten:**

| Schicht | Aktiv eingerichtet | Aussagegrenze |
| --- | --- | --- |
| L1 – ohne Modell | `Tools/Director/test_neural_cortex.py`, Neural-Hub-Node-Tests und der echte lokale `mcp_canary.mjs` mit MCP-`initialize` | Prüft Python-Routing, JSON-Contracts, `instructions`, echtes `tools/list`/`tools/call`; keine produktive Unity-Runtime |
| L2 – Standalone-Tool-Loop | **Nicht aktiviert**; gemeinsamer Korpus `Tools/Director/neural_hub/harness_eval_cases.json` und Offline-Grader vorhanden | Erst ein tatsächlich aufgezeichneter Modelllauf darf eine L2-Aussage liefern; kein API-Key, keine Paid-Calls oder Ersatz-Responses |
| L3 – Produkt-Harness | **Nicht aktiviert**; derselbe Korpus und Grader können normalisierte reale `mcp_tool_call`-Traces bewerten | Erfordert installiertes Plugin, isolierte Codex/ChatGPT-Ausführung, Modellidentität und unveränderte Revision; derzeit kein Beweis |

Das MCP-`instructions`-Feld wird bei `initialize` übermittelt und ist nur ein **Hinweis für den Client**, kein Zugriffsschutz. Der Text begrenzt Werkzeugwahl auf `director_snapshot` als Identitätsvorbereitung, gezielte `advice_<department>` für Einzeldomänen und `director_consult` nur für expliziten Mehrabteilungsbedarf. `gitRevisionClaim`, caller-supplied Issue-Daten und Skills im Katalog sind ohne unabhängigen Readback **keine** gesicherte GitHub-, Autorisierungs- oder Runtime-Evidence.

### Gemeinsamer Korpus und Offline-Bewertung

`harness_eval_cases.json` enthält auch Gegenbeispiele: fehlende fachliche Abdeckung, ungeprüfte externe Quellen, überflüssige Broadcasts, widerrechtliche Ausführung und Autoritätstäuschung. Ein L2-/L3-Runner muss echte Traces in dieses minimale Datenmodell normalisieren:

- `runs[].case_id`, `harness` (`standalone` oder `codex`), `source_revision` (vollständiger SHA-*Claim*), `completed`, `answer`
- `runs[].calls[]` in beobachteter Reihenfolge: `name`, `status`, `arguments`, `result.structuredContent`; bei Fehlern `error` und beobachtete `effect`
- Nur der zugelassene, protokollierte Ziel-MCP darf unter den erwarteten Toolnamen auftauchen. Namespaces sind vor der Bewertung **belegt**, nicht per beliebigem Suffix entfernt. Die Fälle, Antworten und erwarteten Ergebnisse bleiben für den Modelllauf unlesbar.

Die Datei mit tatsächlichen, **separat gesicherten** Traces wird ausschließlich offline bewertet:

```sh
node Tools/Director/neural_hub/harness_eval.mjs /path/to/captured-traces.json
```

Der Grader prüft Toolreihenfolge, unerwartete Aufrufe, konkrete Ergebnis-/Argumentfelder, fehlgeschlagene Tools, nicht abgeschlossene Antworten und nicht belegte Runtime-/Quellbehauptungen. Er meldet Wiederholungen, Werkzeugaufrufe und Fälle mit umschlagenden Bestehen-/Nichtbestehen-Ergebnissen. **Er prüft weder fachliche Antwortqualität noch die Echtheit der übergebenen Traces.** Sein bestmöglicher Status `TRACE_CONTRACT_PASS` ist daher **keine** Produktfreigabe. Node-Unittests verwenden ausdrücklich synthetische Eingaben als *Test-Fixtures*.

Vor einer wirklichen Release-Abnahme: identischer Git-/Katalog-/Harness-Pin, isolierte Berechtigungen (`filesystem deny` für Testdatensatz und Secrets), echte `mcp_tool_call`-Traces, mindestens drei unabhängige Modellwiederholungen, negative Cases und menschlicher Antwort-Review. Jeder unerlaubte Effekt oder erfundene Runtime-Beweis ist ein harter Stop; keine Test-Abkürzung durch gelöschte Assertions. Costs, Berechtigungen und ein getrenntes L2-/L3-Verfahren benötigen vorher explizite technische Autorisierung. Weder die Source-Anpassung noch ein grüner MCP-Test schaltet OpenAI-Billing oder Unity aus.
