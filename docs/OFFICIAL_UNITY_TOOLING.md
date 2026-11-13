# Offizielle Unity-Werkzeuge im Director

Die vom Besitzer genannten Unity-Repositories ergänzen den bestehenden Arbeitsablauf.
Der Editor bleibt auf **6000.6.8f1**, Gameplay und die 100-ms-Simulation bleiben in
`AreArpg.Core`. Neue Skills werden bei Bedarf geladen; ihre Verfügbarkeit aktiviert
keine Cloud-Dienste, Werbung, Käufe oder Multiplayer-Funktionen.

| Quelle | Einsatz |
| --- | --- |
| Unity-Technologies/skills | 37 Skills für CLI, Pakete, URP, UI, Audio, Assets, Prüfung und weitere Unity-Aufgaben |
| Unity-Technologies/unity-agent-plugin | Offizielles Codex-Plugin 0.1.9-beta mit 35 gebündelten Skills; zwei zusätzliche Skills aus dem Skills-Repository |
| Unity-Technologies/uvcs-agent-plugin | Sieben zusammengehörige Skills; `uvcs-core` bleibt Voraussetzung. Ashen Crypt nutzt weiterhin Git/GitHub. |
| Unity-Technologies/com.unity.mobile.logcat | Android Logcat 1.4.7 aus der Unity Registry; Editor-Diagnose für Android |
| Unity-Technologies/com.unity.netcode.gameobjects | Kompatible Registry-Version 2.14.0 für isolierte Erprobung; kein Networking im Spiel |

Der untersuchte Logcat-Gitstand enthält 1.5.0, die Registry veröffentlicht 1.4.7.
Netcode `develop` enthält 3.3.0 und verlangt Unity 6000.7. Diese Version wird nicht in
das auf 6000.6.8f1 festgelegte Projekt übernommen. Netcode 2.14.0 verlangt Unity6000.0
und deklariert Unity Transport2.6.0. Unity 6000.6.8f1 löst im Prüfprojekt tatsächlich
das eingebaute Transportpaket6.6.0 auf; der reale Paket-Lock ist maßgeblich.
Eine spätere Multiplayer-Entscheidung betrifft Spielregeln,
Authority und Save/Replay und wird mit dem Besitzer besprochen.

## Director-Anbindung

`Tools/Director/official_unity.py` ergänzt den installierten Director-Router. Der
private Installationskatalog enthält echte Skillpfade, Quellrevisionen und SHA-256.
Der Router prüft die Dateien bei jedem Aufruf und unterscheidet `INSTALLED`,
`CHANGED` und `MISSING`. Er startet keine Programme und verändert keine Spielwelt.

```sh
python3 Tools/Director/official_unity.py inventory --catalog /path/to/installed-skills.json
python3 Tools/Director/official_unity.py route --catalog /path/to/installed-skills.json \
  --task "Android Logcat Tablet Performance" --vcs git
python3 -m unittest discover -s Tools/Director -v
```

Auf dem VPS bindet `unity-game-director/scripts/route_work.py` diesen Katalog ein.
Sein `references/official-unity-tooling.md` erläutert die Arbeitskonten und Ziele.
Vor Verwendung werden der ausgewählte Skill und seine benötigten Referenzen
gelesen; CLI-, Editor-, Paket- und Gerätestatus werden zusätzlich frisch ermittelt.
Die sechs bisherigen Director-Aktionen bleiben erhalten.

Die zusätzlich beauftragten Community-Skills und der Besitzer-Anhang sind über eine
eigene [Game-Development-Anbindung](GAMEDEV_SKILLS.md) erreichbar. Ihr Katalog bleibt
von den offiziellen Unity-Quellen unterscheidbar. Die zusammengeführten, echten
[Prüfergebnisse](evidence/unity-tooling-20261113/acceptance.json) dokumentieren
Installation, Unity-Editorprüfung und isolierten Netcode-Test getrennt.

Die offizielle Unity CLI **1.0.0** ist lokal und auf dem VPS installiert. Auf dem
VPS wird sie im bestehenden Unity-Arbeitskonto ausgeführt. Der UVCS-Client
**11.0.16.10371** steht für passende Aufgaben bereit; ein UVCS-Repository wurde
nicht angelegt. Das offizielle Codex-Plugin und lokale Skills sind Werkzeuge der
Entwicklungsumgebung, keine neu registrierten ChatGPT-Connectoren.

## Android-Diagnose

`ARE ARPG > Tools > Install Android Logcat` verwendet die offizielle asynchrone
Package-Manager-API. Der Editor muss bis zum Abschluss laufen; bei Batchbetrieb
beendet die Methode selbst den Prozess. `VerifyAndroidDiagnostics` prüft anschließend
die tatsächlich geladene Paketversion, Assembly und den Logcat-Fenstertyp.

Im Editor öffnet `Window > Analysis > Android Logcat` die Diagnose. Das Android-SDK
und ein tatsächlich verbundenes, autorisiertes Gerät sind Voraussetzung für Logs,
Speichermessungen und Bildschirmaufnahmen. Installation und ein geöffnetes Fenster
sind kein Gerätetest. Die Linux-Unterstützung einzelner Aufnahmefunktionen muss an
der installierten Version geprüft werden; Videoaufzeichnung wird nicht versprochen.

## Isolierte Netcode-Erprobung

Das separate Projekt `AshenOfficialToolingProbe` verwendet denselben Editorpin,
Netcode2.14.0 und Test Framework1.8.0, ohne Cloud-Verknüpfung. Die beiden Dateien
unter `Tools/Director/NetcodeProbe` werden nur dort nach `Assets/Tests/PlayMode`
kopiert. Der PlayMode-Test `NetcodeLoopbackTests` startet einen Host an
`127.0.0.1:17853`, prüft seinen lokalen Client, beendet ihn und wiederholt den
Ablauf. Teardown entfernt den Manager auch bei fehlgeschlagenen Assertions.
Dies prüft den lokalen Host-Lebenszyklus; es belegt weder eine Verbindung zwischen
zwei Geräten noch Netzwerk-Synchronisierung des Ashen-Crypt-Core.

## Nachvollziehbare Quellen

- Unity Skills: `e5956e7e34a125982550de15a9e87f8e66af7d86`
- Unity Agent Plugin: `2d68641b24055d9845cb2804bf5a331cc2ff98d9`
- UVCS Agent Plugin: `3da76cd3a0e8642b9e680440551efba268326c6a`
- Logcat-Repository: `eafc366824a2b8e638453f49781ff2a5c009ee2f`
- Netcode-Repository: `ae5f643c53c7d913590bf911b0618ae4028a9f80`

Die ursprünglichen Lizenzen und Referenzen bleiben bei den privaten Installationen.
Die öffentliche Spielquelle enthält die eigene Director-Anbindung und Paketpins,
keine kopierten Hersteller-Assets oder Zugangsdaten. Installation durch die CLI
kann einen anderen offiziellen Skill-Pin verwenden; der jeweilige Installationsbeleg
ist maßgeblich, nicht die Annahme, dass alle Quellen denselben Stand enthalten.
