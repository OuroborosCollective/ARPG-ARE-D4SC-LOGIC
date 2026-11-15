# Fadenwerk — Quick start / Schnellstart

Status: documentation for the current development draft, not a release certificate.

1. Import `Fadenwerk-1.0.0-draft.unitypackage` into a backed-up Unity 6 project. The files appear under `Assets/Fadenwerk`.
2. Open **Window > Fadenwerk** or **Tools > Fadenwerk > Fenster öffnen**.
3. Click **Datenbank anlegen** to create your content database. Keep its asset with your project.
4. Leave the server bound to **Nur lokal**, then choose **Server starten** and open its local URL using the Editor’s **Öffnen** button. Keep the access token private.
5. Create an NPC, quest, dialogue and any required marker/item/reward records in the browser. Use stable IDs and resolve the Editor’s validation issues.
6. Add `FadenwerkManager` to a game object and assign the database. Add `FadenwerkNpc` and `FadenwerkMarker` components as appropriate for your scene.
7. From your own gameplay code, report actual events with `ReportTalk`, `ReportKill`, `ReportCollect`, `ReportDeliver`, `ReportReach` or `ReportCustom`. Do not count visual effects as confirmed gameplay events.
8. Integrate `FadenwerkDialogueRunner` with your own conversation UI. Subscribe to reward/quest events to apply inventory, currency and XP through your existing rules.
9. Save the JSON returned by `SaveToJson()` using your own persistence system; restore it with `LoadFromJson(json)`. Coordinate reward effects and saved quest state as one host-owned operation.
10. Stop the authoring server when finished. The player runtime does not depend on the authoring server.

## Remote authoring
Use an SSH tunnel or a separately configured HTTPS reverse proxy. The package does not provision hosting, DNS, TLS, accounts or firewalls. Never publish the token or enable an open Internet-facing HTTP endpoint.

## Draft limitations
The exported package still contains an authoring-scene helper with its original UPM sample path. Use your own scene rather than that helper until it is corrected. Editor licensing text is a template pending replacement with the appropriate store terms. Final standalone tests, sample content and full documentation remain release gates. The included README refers to an external ANLEITUNG.md that is not part of the current export; this guide supplies the available setup instructions for the draft handoff.

## Deutsch
Die Browseroberfläche läuft im geöffneten Unity-Editor. Datenbank anlegen, Server lokal starten, Inhalte erstellen und Prüfliste abarbeiten. Spielereignisse meldet dein Spiel an den Manager; Inventar, Erfahrung, UI und dauerhafte Speicherdateien bleiben Aufgaben deiner Integration. Diese Anleitung und das Verkaufsmotiv sind vorbereitet, aber noch nicht im Publisher-Portal gespeichert.
