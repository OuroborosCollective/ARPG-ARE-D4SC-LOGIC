# Grenzregion · visuelle Abnahme am 20. November 2026

Die native Abnahme lief auf `f9b29edca0c985d129877c622ac0317324ae0bbd`, Git-Tree `c59135098c0d09c1aacf78822680cdf4b9dbfedc`. Der Merge übernahm die drei vorausgehenden Krypta- und Aschenwelt-Commits aus #98 mit ihrem ursprünglichen Verlauf. Editor und Build verwendeten Unity 6000.6.8f1 und URP 17.6.0. [Änderung #104](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/pull/104)

## Was im Spiel sichtbar ist

- 26 eigene Blender-Modelle mit UVs, drei LOD-Stufen, gemeinsam benutzten Mobilmaterialien und passiven Prefabs. Die Box am Prefab dient der Größenprüfung im Editor. Die Spielprojektion übernimmt Darstellung und Bodenanker. Die Gebiets- und Bewegungsregeln liegen im Core. 95 Platzierungen liegen in zehn bestehenden Bauplänen. Zusätzlich werden Tiefen, Reisehöhlen und die Umgebung der Reiseportale aus ihren vorhandenen Daten eingekleidet.
- Die vorhandenen Glutpilze von 0,72 m und 0,75 m Höhe bildeten die Größen- und Farbreferenz. Die private Materialprüfung umfasste 1.215 Prefabs. Die eingecheckten Modelle und die neue Schieferquelle sind Eigenproduktionen. Die Originale fremder Pakete und die Aufnahmen mit privaten Paketen liegen beim Owner.
- Asche setzt sich auf den Oberseiten des Schiefers ab. Fugen, Bruchkanten, Holz und freie Trittflächen bleiben lesbar. Die Glut sitzt an Pilzen, einzelnen Grashalmen und Stammrissen. Alte und neue Feldflora folgen „Reduzierte Effekte“. Die Außendarstellung nutzt den gespeicherten 14.400-Tick-Tag. Innenräume behalten ihre eigene Beleuchtung. Die Lichter in der Krypta sind auf vier, bei reduzierten Effekten auf zwei begrenzt. Der Bodenshader berücksichtigt höchstens zwei zusätzliche Fragmentlichter.
- Die Zustände verfallen, umkämpft und gesichert verwenden bestehende Core-Daten. Wald, Bärenhöhle und die Route über Aschenzoll enthalten Kiefern, Felsschichten, Nest und bodennahe Glut. Die vier Gegnerrollen haben eigene lesbare Anbauten. Sichtbare Münzen und Beutel folgen den vorhandenen Beutebuchungen und verschwinden bei tatsächlicher Entnahme. Die Beuteregeln verwenden weiterhin `LootCatalog.Version1`.

## Gebietsmaß und Kompatibilität

| Gebackene Außenkarte | Inhaltshash | Verwendung |
| --- | --- | --- |
| Aschengrenze | `8DD026A40C546148` | 256 m Außenkulisse um das bestehende Dorf |
| Aschenstraße | `E7AC39E87A219974` | 256 m Außenkulisse um Straße und Feld |
| Alte Mühle | `B6E438669DB864E7` | 256 m Außenkulisse um den Mühlplatz |
| Waldweg | `9782D9DB9B3E0A43` | 256 m Außenkulisse um den bisherigen Waldpfad |
| Aschenzoll | `4C2154F82338E7D3` | 256 m Außenkulisse um den Grenzposten |
| Reise hinter Aschenzoll | `7A2D88930B55FC31` | begehbare 256-m-Karte, Tore 224 m auseinander |

Zwei native WorldForge-Bakes ergaben dieselben fünf Außenkarten. Die fünf historischen Gebiete verwenden ihre bisherigen Spielgrenzen und Torpositionen. Speicher, versteckte Krypta, Rattlernest und Höhlen verwenden die bestehenden Raum- und Übergangsverträge. Ein vor dieser Änderung gesicherter Spielstand lud wieder mit der 128-m-Karte `82B0FA9F7D342123`, auf derselben Höhlentiefe, und ließ sich bytegleich wieder speichern. Der native Reise-Smoke durchlief die neue Karte mit Kampf, vier Höhlenetagen und Speichern/Laden.

## Ausgeführte Prüfungen

| Prüfung | Ausführung | Beleg |
| --- | --- | --- |
| Headless Blender 4.5.16 LTS | 26 gepackte Eingaben, 78 Exporte in FBX/GLB/glTF. Alle 130 Ausgabedateien und 26 übernommenen FBX-Hashes geprüft | `complete-exports.json` · `d0e1b24f3ab9efdb486bc1699b4d604bb13e86ab4c1330d1447e6db49a56083c` |
| Core | `dotnet test` und vier ausführbare Prüfprojekte. 21.549 deterministische Assertions, vier FwJson- und 45 Fadenwerk-Assertions sowie WorldForge-Prüfung bestanden | `complete-core.log` · `fb56c3cda50e95cd980c240a2c0eef7b514476468999e42d325c418692a9e40e` |
| Repository und Director | 54/54 Vorprüfungen am vollständigen Git-Export mit der geprüften Revision, sechs Director-Bridge-Tests | `complete-preflight.log`, `complete-neural.log` |
| Unity EditMode | 225 von 229 im Gesamtlauf. Alle vier umgebungsabhängigen Fälle danach mit echtem WorldForge-Auftrag beziehungsweise freiem Bauplatz bestanden | `complete-edit.xml` · `0c47e94aecf2f9b62562d27330e2e5b37086edc0265fb15882702852ce26ce1c`, `complete-extra-edit.xml` · `622e1fe261269b7c13e727f2944d896a93bc031f20dcc5f092a4f7db722df51e` |
| Unity PlayMode | 181/181 mit aktiver Spielszene und privater Grafik. Darunter Bodenanker, Beute, Übergänge, Bosse, Kamera, Speichern/Laden, Licht und reduzierte Effekte. Keine Compiler-, Shader- oder NullReference-Fehler in den drei Unity-Testlogs | `complete-play.xml` · `c682e42029c15d4e963c1074a6ea92b725c98079f6f483e04c721072a39867b9` |

Die Positionstests erkannten unsichtbare Kiefern zunächst nicht. Ein Vergleich mit ausgeblendeten Bäumen ergab im 240 × 135 großen Kamerabild zunächst 0, nach der Korrektur 4.834 unterschiedliche Pixel. Höchstens vier gemeinsame Renderer zeichnen die Fernkiefern. Ein weiterer PlayMode-Test reproduzierte den abweichenden Wegrandschader. Nach der Korrektur bestanden alle 21 direkt betroffenen Prüfungen. In einem unveränderten 390 × 200 Pixel großen Tagesausschnitt wechselte der Median von sRGB (166, 157, 135) auf (162, 163, 162).

Die 105 nativen PNGs, XML-Dateien, vollständigen Exportbelege und das APK-Protokoll sind auf dem privaten VPS unter `~unityarpg/.local/share/are-arpg/borderland-art-20261120/` hinterlegt. Die Bilder `complete/glutlicht-tag.png`, `complete/glutlicht-nacht.png` und `complete/glutlicht-reduziert.png` verwenden dieselbe Kamera. SHA-256 der Nachtaufnahme `dea71b60eff89014c79e2caa2ad9961d5b06f5959e212b992ca89652a9da9257`. Der reine Software-Renderer des VPS lieferte die Bildvergleiche. Eine Bildrate auf dem Tablet lässt sich daraus nicht ableiten. [Core-CI](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44756398137)

## Android und installierte Privatpakete

Der reguläre Director-Auftrag `20261120T145328Z-a85cac8b97a5` meldete `passed` mit unverändertem Quell-Tree. Das ARM64-IL2CPP-APK ist 174.360.569 Byte groß. Der unabhängige SHA-256-Abgleich ergab `b0a4d06efe6964b8bf757c8b3ce2b3aa8795e7cb957ec6072451b1d23bcf589f`, der Director-Receipt `197edaa41108927b0e4b24b16a6bb71195ea1d41da6be67b64b109d632b5dc4a`. Android Build Tools prüften die v2-Debugsignatur und das 16-KiB-ZIP-Alignment. Alle sieben ARM64-Bibliotheken wiesen mindestens 16 KiB ELF-LOAD-Alignment auf. Die gebackenen Privatfiguren waren im Build enthalten. Nur die Authoring-/Demo-Skripte des Figurenpakets und der zugehörige lokale Compiler-Schalter waren während dieses Headless-Builds ausgeblendet. Danach wurden die Originale zurückgelegt. Die vier vom Editor automatisch serialisierten URP- und Projekteinstellungen waren für den Director als unveränderte Eingaben eingebunden.

Im tatsächlich installierten privaten Figurenpaket enthielten vier Script-Metadateien nur die GUID. Der vollständige Asset-Test meldete den fehlenden Importerblock. Unity hat die vier vorhandenen `MonoImporter` neu serialisiert. Anschließend stimmten alle vier vorherigen GUIDs und Script-Hashes, und die gleichen 54/54 Vorprüfungen bestanden auch im Arbeitsordner mit den Privatpaketen. Die gesicherten Vorherstände, `private-importer-unity.log` und `complete-preflight-private-fixed.log` liegen im privaten Belegverzeichnis.

Das Owner-DOCX war unter seiner hinterlegten Datei-ID nicht lesbar. Die Runde ist zusätzlich im bestehenden privaten Markdown-Evidenzbuch und in der Projektübergabe dokumentiert. Die Geräteprüfung auf dem Samsung Tab A9 bleibt offen.
