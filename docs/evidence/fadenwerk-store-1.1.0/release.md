# Fadenwerk 1.1.0 – Store-Update (Asset Store Paket 430392)

Grundlage: eingereichter Entwurf 1.0.0 (`Fadenwerk-1.0.0-draft.unitypackage`, SHA-256
`8c7d509d13362478f520b0f1e4e6067d95b4fb7324ef84722c912bbf365195ce`, Paket 430392 / Version 1558990, Stand
15.11.2026 „draft“). Der Entwurf ist datei- und GUID-gleich mit `Packages/com.ouroboros.fadenwerk` auf `main`
`488567f`.

Update-Paket: `Fadenwerk-1.1.0.unitypackage`, 87 788 Bytes, SHA-256
`2ebf11c232a78aa5bef2275ed86de8e33fa17ab9486faf69dc9b00083edd3ee2`, 40 Einträge unter `Assets/Fadenwerk`
(gebaut aus dem Entwurf + Repo-Stand `7400c94`, alle bisherigen GUIDs erhalten; privat auf dem VPS unter
`/var/lib/unityarpg/.local/share/are-arpg/store-drafts-20261119/`).

Geändert: `Editor/FadenwerkApi.cs`, `Editor/FadenwerkAuthoringBridge.cs`, `Editor/FadenwerkSample.cs`,
`Editor/FadenwerkServer.cs`, `README.md`, `package.json` (1.1.0), `Tests/Editor/Fadenwerk.Editor.SafetyTests.asmdef`,
`Tests/Editor/FadenwerkAuthoringSceneSafetyTests.cs`. Neu: `CHANGELOG.md`,
`Tests/Editor/FadenwerkExtensionTests.cs`.

Prüfung:
- `unity assets inspect` liest alle 40 Einträge.
- Sauberer Import in ein leeres Unity-6000.6.8f1-Projekt (temporär, danach gelöscht): 0 Kompilierfehler, alle 34
  Dateien byte-gleich und alle 39 GUIDs gleich dem Repo.
- Fadenwerk-Paket-Tests: 7/7 in der Store-Struktur (`Assets/Fadenwerk`) und 7/7 als eingebettetes Paket.
- Asset Store Validator (Asset Store Tools 12.0.0): 34 Prüfungen, 0 Fehler, 1 Warnung „Check Demo Scenes“
  (Fadenwerk legt seine Beispielszene erst über das Menü an).

Gefunden und behoben: In der Store-Struktur schlug die Beispielszene fehl, weil die Vorlage über den festen Pfad
`Packages/com.ouroboros.fadenwerk/…` kopiert wurde (Paket-Test im leeren Projekt rot, 6/7). Das betrifft auch die
eingereichte Version 1.0.0. Die Vorlage wird jetzt über ihre GUID gefunden.

## Release Notes (für das Publisher-Portal)

**English:** Version 1.1.0 adds project extensions: register your own browser page (`/?your-key`) and your own API
calls (`/api/x/…`) on the built-in Fadenwerk server through `FadenwerkAuthoringBridge`. They share the server, access
token and reverse proxy with the Fadenwerk interface and run on the Unity main thread, also in Play Mode. Also new:
`ServerRunning`/`StartServer()`, a changelog and package tests. Data format, runtime components and the existing
interface are unchanged.

**Deutsch:** Version 1.1.0 bringt Projekt-Erweiterungen: eigene Browser-Seite (`/?dein-key`) und eigene API-Aufrufe
(`/api/x/…`) auf dem eingebauten Fadenwerk-Server über `FadenwerkAuthoringBridge` – gleicher Server, gleicher
Zugangsschlüssel, gleicher Proxy, Ausführung im Unity-Hauptthread, auch im Play-Modus. Außerdem neu:
`ServerRunning`/`StartServer()`, Changelog und Paket-Tests. Datenformat, Laufzeit und bestehende Oberfläche bleiben
unverändert.
Fix: the sample authoring scene now also works for Asset Store installs under `Assets/Fadenwerk`.

Status 19.11.2026 (Uploader-Liste, angemeldet mit dem Owner-Konto): Fadenwerk 430392 / Version 1558990 ist
„pendingReview“, ebenso Memory Forge (429860) und World Forge (430394); nur „Ouroboros Dungeon Blueprints“ (428468)
ist „draft“. Der Uploader nimmt nur Entwürfe an, daher ist 1.1.0 noch nicht hochgeladen. Weg: entweder die
Prüfung von 1.0.0 im Publisher-Portal zurückziehen (dann 1.1.0 in den Entwurf laden und neu einreichen) oder nach
der Freigabe eine neue Version anlegen und 1.1.0 als Update hochladen.

Hinweis World Forge 1.0.0 (in Prüfung, nicht geändert): Tests und das Beispielmenü greifen auf
`Assets/Scenes/Arena.unity` und das private `Assets/Studio/Nature/AshenKrypt` zu, die nicht im Paket sind.

Owner-Entscheidung 19.11.2026 (C): Die laufende Prüfung von 1.0.0 bleibt bestehen. Nach der Freigabe legt der Owner im
Publisher-Portal eine neue Version an; dann wird 1.1.0 hochgeladen.
