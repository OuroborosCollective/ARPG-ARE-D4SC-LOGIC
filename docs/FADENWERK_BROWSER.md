# Fadenwerk im Browser

Öffnen: **https://arelogic.space/fadenwerk/**. Der Zugang verwendet dieselbe
Besitzer-Anmeldung wie Unity Live. Die Seite nutzt normale HTML-Eingabefelder und
damit die Bildschirmtastatur des Tablets. Der Unity-Editor des Autorenprojekts
muss geöffnet sein; bei Kompilierung, Tests oder geschlossenem Editor kann die
Seite vorübergehend offline sein.

## Texte bearbeiten und im Spiel verwenden

1. Unter **Quests** die Quest **HUNGER HINTER DEN MAUERN** öffnen. Titel und
   Zieltexte bearbeiten und speichern.
2. Unter **Dialoge** den Dialog **Ilyan / Mühle – gebundene Handlungen** öffnen.
   Die vorhandenen IDs und die sieben Handlungen beibehalten.
3. Unter **Mehr / Einstellungen → Ashen Crypt – Spielanbindung** die Texte
   prüfen und veröffentlichen. Der angezeigte Hash bezeichnet den übernommenen
   Textstand. Ein Fehler lässt den zuvor veröffentlichten Stand bestehen.
4. Das Spiel anschließend neu starten. Ein laufendes Spiel lädt seine Texte
   beim Start; normale Save-Dateien enthalten weiterhin den bestehenden
   kanonischen Questfortschritt.

Diese erste Anbindung umfasst Ilyans Geheimnis, die Spur zur Mühle und das
Verletztenlager. Titel, Ziele und die ersten drei Untersuchungsrückmeldungen
werden übernommen. Kosten-, Entscheidungs- und Ereignisrückmeldungen bleiben
beim Core; nicht jeder frei editierbare Dialogtext ersetzt eine Spielmeldung.
Der Datenbankeditor zeigt Ilyan gegebenenfalls als nicht in der aktuell geladenen
Szene vorhanden an. Das ist keine Aussage über seinen gespeicherten Spielzustand.

Neue Quests, NPCs, Kits und freie Belohnungen lassen sich als Entwurf erstellen.
Sie werden erst nach einer eigenen geprüften Core-Anbindung spielwirksam. Die
Veröffentlichung weist Änderungen an der gebundenen Queststruktur zurück,
beispielsweise zusätzliche Belohnungen oder Wiederholbarkeit. Fadenwerk führt
keine zweite Questsimulation und verwaltet keine Spielstände.

Bei einem Bearbeitungskonflikt werden neuere Änderungen nicht überschrieben.
Den eigenen Text zunächst sichern, den aktuellen Datensatz neu laden und die
Änderung erneut einarbeiten. Ungespeicherte Eingaben bleiben nur im offenen Tab;
das Schließen oder Neuladen kann sie verlieren.

## Technische Zuständigkeiten

| Bereich | Zuständigkeit |
|---|---|
| Core | `AuthoredHungerBinding` delegiert ausschließlich an bestehende Hunger-Aktionen. |
| Autorenproduktion | `AshenFadenwerkEditor` prüft die feste Struktur und veröffentlicht Texte. |
| Persistenz | Bestehende AC-Verträge; keine Änderung am Save-Schema. |
| Unity-Projektion | `AshenFadenwerkText`, `HungerJourneyPresentation`, bestehendes `ArenaRuntime`. |
| Effekt | Textdarstellung; Entscheidungen, Kosten, XP und Erinnerungen bleiben im Core. |
| Tests | CoreVerification, Fadenwerk Edit-/PlayMode, Gateway-Policy und Browser-Negativtest. |

Autorenquelle: `Assets/FadenwerkData/AshenCryptDatabase.asset`.
Build-Ressource: `Assets/Resources/AshenFadenwerkText.asset`.
Spielvertrag: `AshenCrypt.Fadenwerk.Hunger.v1`.
Fable-HUD, Kamera und private Originalassets bleiben bestehen.

## Betrieb und Wiederherstellung

Die eingecheckten Dateien unter `Tools/Fadenwerk` sind die Betriebsvorlage.
Traefik stellt HTTPS und die vorhandene Besitzer-Authentifizierung bereit.
Der Dienst `ashen-fadenwerk-web.service` lauscht ausschließlich auf
`127.0.0.1:8096`; Fadenwerk im Editor auf `127.0.0.1:8095`.
Der Browser erhält keinen Editor-Schlüssel. Gleichursprüngliche JSON-Anfragen,
Größenlimits und die Autorenrevision werden vor Änderungen geprüft.

Der private Schlüssel liegt außerhalb des Repositorys in
`/var/lib/unityarpg/.local/share/are-arpg/fadenwerk-web/editor-token`
(Datei 0600, Ordner 0700, Besitzer unityarpg). Nicht veröffentlichen oder in
Logs ausgeben. Nach einer Schlüsselrotation den Editor und diese Datei durch
`Tools/Fadenwerk/start-owner-web.cs` erneut verbinden. Das Skript bindet die
vorhandene Ashen-Datenbank an und veröffentlicht deren gültigen Textstand.

Zur Diagnose zuerst `systemctl status ashen-fadenwerk-web` sowie den nativen
Unity-Status prüfen. Ein anonymer HTTPS-Aufruf muss 401 liefern; das ist der
erwartete Zugangsschutz. 503 nach Anmeldung bedeutet, dass der Editor gerade
nicht antwortet. Für einen Rückbau nur diesen Dienst stoppen und seine eigene
Traefik-Datei entfernen; andere Dienste und Router bleiben bestehen.

Die Infrastruktur teilt lediglich den Zugangsschutz mit Unity Live. Es gibt
keine Verbindung zu einer anderen Spielsimulation oder deren Datenbank.

## Prüfungen

`python3 -m unittest discover -s Tests/FadenwerkGateway -v` prüft die
Anfragepolitik. `python3 Tools/Fadenwerk/check_browser_offline.py` benötigt
Playwright und Chromium (optional `CHROMIUM_PATH`) und prüft die Offline-Anzeige
mit kontrollierten Browserantworten. Dieser Negativtest ist ausdrücklich kein
Unity- oder Android-Nachweis. Native Ausführungen und der tatsächliche
HTTPS-Schreib-/Publikationsablauf werden separat im Evidence-Dossier erfasst.
