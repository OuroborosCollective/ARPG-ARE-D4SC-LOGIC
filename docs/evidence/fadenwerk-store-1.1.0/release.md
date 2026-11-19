# Fadenwerk 1.1.0 – Store-Update (Asset Store Paket 430392)

Grundlage: eingereichter Entwurf 1.0.0 (`Fadenwerk-1.0.0-draft.unitypackage`, SHA-256
`8c7d509d13362478f520b0f1e4e6067d95b4fb7324ef84722c912bbf365195ce`, Paket 430392 / Version 1558990, Stand
15.11.2026 „draft“). Der Entwurf ist datei- und GUID-gleich mit `Packages/com.ouroboros.fadenwerk` auf `main`
`488567f`.

Update-Paket: `Fadenwerk-1.1.0.unitypackage`, 87 161 Bytes, SHA-256
`d53e8dc1dc925569f7bc7915d7e3f17bd64fb6fe216dc8836d84376322b38e08`, 40 Einträge unter `Assets/Fadenwerk`
(gebaut aus dem Entwurf + Repo-Stand `648fe9c`, alle bisherigen GUIDs erhalten; privat auf dem VPS unter
`/var/lib/unityarpg/.local/share/are-arpg/store-drafts-20261119/`).

Geändert: `Editor/FadenwerkApi.cs`, `Editor/FadenwerkAuthoringBridge.cs`, `Editor/FadenwerkServer.cs`, `README.md`,
`package.json` (1.1.0), `Tests/Editor/Fadenwerk.Editor.SafetyTests.asmdef`. Neu: `CHANGELOG.md`,
`Tests/Editor/FadenwerkExtensionTests.cs`.

Prüfung: `unity assets inspect` liest alle 40 Einträge; Fadenwerk-Paket-Tests im Unity-6000.6.8f1-Editor 7/7
(davon 2 neu); im Spielprojekt EditMode 126/126 und PlayMode 171/171 mit identischen Quelltexten.
Nicht geprüft: Import in ein leeres Projekt und Asset-Store-Validator (brauchen ein eigenes Upload-Projekt).

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

Status: hochgeladen/eingereicht ist 1.1.0 noch nicht.
