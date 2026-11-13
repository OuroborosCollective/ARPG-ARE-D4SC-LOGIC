# Game-Development-Skills für Ashen Crypt

Der Besitzer hat das Repository `gamedev-skills/awesome-gamedev-agent-skills`,
den festen Commit **25eec57919aec2cfd87d4ef9ae5661832302585e** und einen eigenen
Animations-Skill zur Installation vorgegeben. Der Stand ist fest ausgecheckt;
die Installation führt keine mitgelieferten Gameplay- oder Installationsskripte aus.

Installiert sind **29 zusätzliche Skills mit 88 Dateien**: neun Unity-Skills,
15 Disziplinen, RPG, die Workflows Game Docs und Prototype Fast, der Community-Router
sowie der Besitzer-Anhang `game-development`. Der Ordner `unity-animation` enthält
auch seine Referenz zu Blend Trees, Avatar Masks und humanoider IK. Lizenzen und
NOTICE des Community-Repositories bleiben erhalten; der Besitzer-Anhang wird privat
verwendet und nicht im öffentlichen Spielrepository weiterverteilt.

Lokaler gemeinsamer Pfad: `/workspace/.agents/skills/`. Auf dem VPS liegen die
versionierten Quellen unter dem privaten Game-Development-Tooling-Verzeichnis und
sind über die Skills des Unity-Arbeitskontos erreichbar. Das ergänzt die vorhandenen
44 offiziellen Unity-/UVCS-Skills. Es installiert keinen neuen Unity-Editor.

## Einbindung in den Director

`Tools/Director/game_dev.py` ergänzt den vorhandenen Director. Er verlangt einen
echten Unity-Projektpfad und liest dessen Version, statt die Engine zu raten. Sein
Katalog enthält Quellrevisionen und Prüfsummen sämtlicher mitgelieferter Dateien.
Veränderte oder fehlende Referenzen werden als `CHANGED` beziehungsweise `MISSING`
angezeigt. Bis zu drei passende Skills werden vorgeschlagen; weitere Treffer bleiben
sichtbar. Das Lesen der ausgewählten Anleitung und die echte Ausführung bleiben
getrennte Schritte.

```sh
python3 Tools/Director/game_dev.py --catalog /path/to/gamedev-catalog.json \
  --project /path/to/AshenCrypt --task "Jäger Bogenschuss Animation"
python3 -m unittest discover -s Tools/Director -v
```

Der installierte Director unterstützt denselben Projektbezug:
`python3 scripts/route_work.py "Prozeduraler Wald Dungeon Leveldesign" --project <Pfad>`.
Die echten Readbacks wählen `unity-animation` und `game-development` für den Jäger
sowie `level-design` und `procedural-gen` für den Wald. Die sechs bisherigen
Director-Aktionen bleiben bestehen. Die Community-Skills ergänzen Gestaltung und
Implementierung; die offiziellen Unity-Werkzeuge bedienen den Editor.

## Anwendung auf das bestehende Spiel

Die Community-Anleitungen basieren auf Unity6000.3.28f1. Ashen Crypt bleibt auf
**6000.6.8f1**; verwendete APIs werden gegen diesen Pin geprüft. Ein installierter
Skill belegt keine ausgeführte Animation, kein Android-Verhalten und keinen Zeitgewinn.

Der Anhang beschreibt zwölf Animationsprinzipien, darunter Ausholen, klare Silhouetten,
Nachschwingen und zeitlich passendes Trefferfeedback. Seine Beispielwerte sind keine
freigegebenen Spielparameter. Insbesondere wird sein globales `Time.timeScale`-Hitstop
nicht übernommen: der deterministische 100-ms-Core und seine Ereignisse bleiben
maßgeblich. Animator, IK und Effekte stellen diese Ereignisse dar. NavMesh oder
ScriptableObjects ersetzen weder kanonische Bewegung noch Spielstandwahrheit.
Bestehende Kamera, Fable-UI und Tablet-Steuerung bleiben erhalten. Bedeutende neue
Story- und Gameplayentscheidungen werden weiterhin mit dem Besitzer besprochen.

## Leveldesign-Artikel und Lizenzstand

Der [verlinkte Unity-Artikel](https://discussions.unity.com/t/i-tested-unity-level-design-tools-these-are-my-favorites/1737408)
nennt acht Asset-Store-Erweiterungen. Er ist eine gesponserte Auswahlbeschreibung,
kein Installationspaket und kein Lizenznachweis.

| Erweiterung | Möglicher Einsatz; noch nicht im Projekt geprüft |
| --- | --- |
| GSpawn | Platzierung von Prefabs |
| Prefab World Builder | Gestaltung modularer Umgebung |
| TileWorldCreator 4 | Erzeugung und Gestaltung von Kachelwelten |
| DunGen | Modulare Dungeon-Erzeugung |
| Gaia Pro VS | Gelände und Vegetation |
| GeNa Pro | Straßen, Flüsse und Umgebungsgestaltung |
| Digger PRO | Höhlen und Geländeöffnungen |
| Spline Architect | Gestaltung entlang von Splines |

Im verfügbaren Katalog von 219 erworbenen Paketen und im Paketcache wurde keines
dieser acht Basistools gefunden. Vorhanden ist **Book of the Dead: Biome for Gaia Pro VS**
(Produkt345752); dieser Zusatz ist kein Nachweis einer Gaia-Pro-Lizenz. Die acht
Tools wurden daher nicht installiert und es wurde kein Kauf ausgelöst. Vor einer
Übernahme werden Lizenz, konkreter Nutzen, Unity-/URP-Kompatibilität und Androidkosten
geprüft. Ein externer Dungeon-Generator würde unsere bestehenden Seeds, Storyanker
und Save-/Replay-Verträge berücksichtigen müssen.

Nachweise: [Tooling-Abnahme](evidence/unity-tooling-20261113/acceptance.json).
