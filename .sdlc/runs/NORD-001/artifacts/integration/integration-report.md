---
run_id: NORD-001
task_id: INT-001
producer: pm
status: reviewed
revision: 1
---

# Nordwechsel von Aschenzoll in die Glutklamm

## Integration und Herkunft

Der Produktstand `72510b0c3b4325f3a8928e51bedfdb64262bf7fc` hat den Git-Baum `0407a314e9c77df9aefdcbaed8a3e3cf29ef0a04`. Die 28 Produktdateien des kumulierten Diffs zum gemeinsamen Ausgangscommit `0cf480c6f97e089f23981169d377530d9f06d943` wurden aus Git in einem separaten Arbeitsbaum für den Collector bereitgestellt. Für jede Datei wurden Arbeitskopie, Index und Git-Objekt des Produktstands abgeglichen. Paketmanifest, Paket-Lock und Projekteinstellungen stimmen mit der Ausgangsrevision überein. Das Basisprojekt bestand im gleichen SDK vor der Änderung 23.602 Core-Assertions.

| Fläche | Verhalten und Aufteilung |
| --- | --- |
| Übergang | Ein westlicher Nordbogen bei (−9,5 m, 18 m) ergänzt die bestehende Station. Der alte Waldweg bleibt bei (0 m, 18 m). Der Architekturbogen, die Feuerschale, der Schiefer und der Straßenstein stammen aus dem vorhandenen Bestand. |
| Core und Welt | Der zweite Ausgang setzt den gemeldeten Rattler-Sieg, eine der beiden persönlich abgeschlossenen Frachtentscheidungen und vollständig aufgenommene Beute voraus. Die bestehende Kapiteldefinition, der Warden, Steg, Ritualdais, vier Basaltsäulen und sieben Fissuren tragen die Zielszene. Vier randständige Schieferformationen ergänzen die Silhouette außerhalb der Spielfläche. |
| Sicherung | Die Passage erzeugt den Zielzustand aus einer Kopie. Die Runtime schreibt ihn in den vorhandenen primären PlayerPrefs-Slot, liest ihn zurück, parst ihn mit dem Kartenkatalog und vergleicht seinen stabilen Hash. Erst dann wird die Zielwelt installiert. Schlägt der Lesevergleich fehl, stellt sie den vorherigen primären und den gegebenenfalls rotierten Backup-Slot wieder her. |
| Kampf | Der bestehende Warden nutzt einen 4-m-Puls. Sein Ausholen läuft nach dem Anlaufen auch außerhalb der Aggroreichweite bis Tick acht weiter. Boss und Wächter liefern echte Ausrüstungs- und Versorgungsbeute. Am nördlichen Ausgang öffnet sich Tiefe 3 erst nach dem letzten Bodenbeutel. |
| Kamera und Darstellung | Kampfkamera, manueller Zoom und das vorhandene URP-Volume verwenden den Kapitelkontext. Die Sichtlinie durch den offenen Torbogen bleibt frei. Sieben aus den vorhandenen Meshmaßen abgeleitete Teilvolumen erfassen die massiven Pfeiler und das Gewölbe. |

## Reproduzierbarer Welthash

Der netzisolierte Release-Lauf im echten .NET SDK 8.0.428 ergab 23.657 Core-Assertions. Bei Seed 71 erreichten zwei aus demselben Stationscheckpoint getrennt initialisierte Läufe denselben Wechsel. Beide Ausgangshashes lauteten `2AC47DE508663AE4`, beide Zielhashes `7703E8B020A8EB81`. Die ursprüngliche Welt blieb erneut lesbar. Der Bericht prüft diesen Core-Ausgangsfall getrennt vom Unity-Kaltstart mit dem Spielseed und dem ausgelieferten Katalog.

## Reparaturlauf RPR-001

In der ersten nativen Integration auf `4300ee522de4917bc025f429b12fa721af5e05d1` bestand der überprüfte Kaltstartvergleich nicht. Der Test verglich eine vorbereitete Station mit Seed 71 und älterem Katalog mit dem echten AshenEntry-Neustart unter dem Spielseed und dem V2-Katalog. Die daraus entstehenden drei zusätzlichen Forge-Kreatur-IDs machten die verglichenen Fortschrittsbytes verschieden. Auf `168a5a7bfdb0734beb0810ed1383bc3b60386581` wurde auch die Ausgangsstation tatsächlich durch AshenEntry und PlayerPrefs geladen. Der unabhängige Vergleich umfasst die vollständige serialisierte Fortschrittszeichenfolge. Seit `4d7fe150` lässt die optionale Bildaufnahme die Spielkamera im tatsächlichen Kontext einschwingen. Das gespeicherte Reparaturprotokoll bewahrt die ursprüngliche Lieferung und deren Collector-Records.

## Native Unity-Prüfungen

Auf Unity 6000.6.8f1 (237b6593792b), Linux OpenGLCore mit Android als aktivem Editor-Buildziel, wurden die folgenden originalen NUnit-XML-Dateien und ihre SHA-256-Werte nach der Übertragung gelesen. Der Ziellauf rekonstruiert die ArenaRuntime im gelöschten und neu aufgebauten AshenEntry-Szenenkontext. Er benutzt echte PlayerPrefs, den regulären Reiseaufruf und den ausgelieferten Katalog.

| Produkt | Auswahl und Ergebnis | XML SHA-256 |
| --- | --- | --- |
| `2ee799be` | Beide zuerst hinzugefügten Nordwechsel-Fälle schlugen vor Umsetzung fehl. Der Zielwert für die Kapitelnummer blieb 1. Der erwartete Readback-Fehlerpfad wurde noch nicht erreicht. | `695ec679efe96a10a7f987c540559e9bc3761b622a4ea8a0297af9a249c79b9d` |
| `4300ee52` | Elf von zwölf bestanden. Beim Vergleich nach Neustart erwartete der Test 12850526453339916025 und erhielt 17023350171700666581. | `8d6c912d150976bb64470915282270725f058a8648429804e2d9e44946b630b9` |
| `4d7fe150` | Alle dreizehn ausgeführten Nordwechsel-, Kamera-, URP- und Nachbarfälle bestanden. Null fehlgeschlagen oder übersprungen. Testzeit laut NUnit 7,9097733 Sekunden. | `515ef826ed9cad0da5d2d316db550ae14fbcc3375ef8d9ec5c3f59525bea5588` |
| `72510b0c` | Alle sechzehn ausgewählten Fälle bestanden. Null fehlgeschlagen oder übersprungen. Der Ergänzungslauf prüft die drei Bindungen des mitgelieferten Schiefers. Testzeit 8,9069211 Sekunden. | `1d3492733c681f9ff1dcdc36f4c94ab76b380db0f5e3cf72fe1df9434cd07821` |

Der reale Fehlerfall entzieht gezielt dem unabhängigen Speicherleser den Kartenkatalog, nachdem eine echte Waldreise die Sicherung an diesen Katalog gebunden hat. Die Prüfung bestätigt dieselbe laufende Stationswelt, Tiefe 1, die Sperre weiterer Schreibvorgänge und die exakten bisherigen Inhalte beider Slots. Der Erfolgsfall prüft die früheren und neuen Bytes, einen echten Warden-Treffer sowie vier Meter Radius am nativen geschlossenen Warnring. Im netzisolierten Core-Lauf wurden beide Frachtzweige, beide Loot-Arten, lebender und ungemeldeter Rattler, Warden-Treffer und Ausweichen, stationäre und ausgelaufene Aggro sowie die dauerhafte nächste Tiefe geprüft.

Ein bei der Basisauswahl zusätzlich ausgeführter älterer Dorftest scheiterte bereits auf `0cf480c6` bei der Suche nach dem im öffentlichen Checkout fehlenden Asset `Aschengrenze_Supply_Hall`. Baseline-XML `04f4d0e6b0f57631616307931e30ec9303b8e788866a1f0beeb718fd2581da5b` enthält diesen Fall und zwei erfolgreiche alte Frachtfälle. Für Android-Geräte-Bildrate, Installierbarkeit oder APK-Deployment wurde kein Ergebnis erhoben.

## Ausgelieferte Kapiteloberflächen

Die Aufnahme auf `4d7fe150` zeigte im öffentlichen Checkout, dass die bestehenden Basaltflächen ohne regionale Materialüberlagerung auf das einfache Unity-Material zurückfielen. RPR-002 führt bei fehlendem Material die im Projekt enthaltene Schiefersurface mit ihrer vorhandenen Maske auf Boden, Steg und Säulen. Bereits vorhandene regionale Materialien behalten Vorrang. Die drei regionalen Texturskalen betragen 5,4 m für den groben Boden, 1,65 m für den Steg und 2,8 m für die Säulen. Der Shader ergänzt einen neutral vorbelegten Schieferton. Eine neue Paketabhängigkeit, neue Geometrie oder eine zusätzliche Texturladung pro Frame war für den Ausbau nicht erforderlich.

## Nachweise im Repository

Der Produktcommit auf GitHub `2d027d58aee729e537072f4d667a4bee0272b54d` und die Unity-Prüfrevision teilen den Git-Baum `0407a314e9c77df9aefdcbaed8a3e3cf29ef0a04`. Der Bericht unter `docs/evidence/nordwechsel-20261126/README.md` ordnet Original-XML und Bilder derselben Projektion zu. Nachdem die beiden ernsthaften Befunde separat retestet waren, wurden die vier neuen gelieferten Nachweise in RPR-002 geprüft. Das Release-Build hatte null Warnungen und null Fehler. 64 Preflight- und 76 Director-Tests bestanden im netzisolierten Produktabzug.

Die zur Integration erneut ausgeführten nativen Collector-Aufträge sind durch `EVD-239b6c76eae11a35` (Core), `EVD-2d81e2c7c80e8a86` (Preflight) und `EVD-601a899ee8a3f19e` (Director) im Run belegt. Die Produktlieferung enthält die vier eigenen Records `EVD-bf6a1fea18c3b5be`, `EVD-ecd8f8dfab83a53b`, `EVD-38afec9a1bd91137` und `EVD-2c653fee80f69a30`.
