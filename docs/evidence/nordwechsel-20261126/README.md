# Nordwechsel und Glutklamm

Der westliche Nordbogen in Aschenzoll führt in die Glutklamm. Der Bericht an Ilyan zählt nach dem Sichern des Originalbelegs ebenso wie nach der Verfolgung der Fracht und der Untersuchung der oberen Rampe. Der Spieler muss den Sieg im Rattlernest gemeldet und die am Standort ausstehenden Beutestücke aufgenommen haben. Geradeaus bleibt der Waldweg erreichbar.

## Was bereits vorhanden war

Auf dem Ausgangsstand `0cf480c6f97e089f23981169d377530d9f06d943` existierten die Basaltarena, Fissuren, Damm, Ritualpodest und der Ember Warden im Kapitelmodell. In der Geschichtenkampagne fehlte der Eingang. Der Umbau verwendet den bestehenden Spielraum in `AshenEntry`, die vorhandene Kampfkamera und das vorhandene URP-Volume. Der Bogen verwendet das mitgelieferte Ossuarmodell, der Klammrand die vorhandenen Schiefer-LODs.

## Geprüfte Revision

[GitHub-Produktcommit](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/commit/2d027d58aee729e537072f4d667a4bee0272b54d) und lokale Unity-Prüfrevision `72510b0c3b4325f3a8928e51bedfdb64262bf7fc` enthalten denselben vollständigen Git-Baum `0407a314e9c77df9aefdcbaed8a3e3cf29ef0a04`. Der echte .NET-8-Lauf bestand 23.657 Assertions. Die [GitHub-Core-CI](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45879074532) schloss erfolgreich ab. Der native PlayMode-Lauf auf dem VPS in Unity 6000.6.8f1 mit Android als Bauziel bestand alle 16 ausgewählten Fälle. [NUnit-Ergebnis](unity-72510b0c.xml)

## Spiel- und Speicherprüfung

| Grenze | Nachweis |
| --- | --- |
| Ausgangsstand | Auf dem ursprünglichen Produktcode scheiterten die beiden neuen Unity-Tests. Das Begehen des vorgesehenen Nordbogens ließ das Spiel in Kapitel 1. Der vorgesehene Speicherfehler wurde auf diesem Weg nie erreicht. [Originales RED-XML](entry-red.xml) |
| Freigabe und Replay | Zwei unabhängige Core-Läufe mit Seed 71 und gleicher Eingabefolge ergaben vor dem Wechsel `2AC47DE508663AE4` und danach `7703E8B020A8EB81`. Der Hash der weiter referenzierten Quellwelt blieb erhalten. Ein begonnener Warden-Impuls endet auch nach Verlassen seiner Aggroreichweite nach acht Ticks. |
| Kaltstart | Das PlayMode-Szenario beginnt mit einer vorbereiteten, gemeldeten Stationsgeschichte. Es lädt diese zunächst über den echten Spieleinstieg, läuft an den Bogen, wechselt das Gebiet und lädt den Zielspielstand in einer neuen Unity-Szene. Es vergleicht die gespeicherten AC22-Bytes, Tiefe, Frachtentscheidung und Fortschritt. Bei fehlendem Kartenkatalog auf dem unabhängigen Lesepfad bleiben Stationswelt und beide alten Speicherstände erhalten. |
| Kampf | Die Unity-Figur kann den Warden mit einer regulären Fähigkeit treffen. Ein vollständiger Core-Kampf prüft Warden und Wächter, die Sperre durch den letzten Beutel und den erneuten Einstieg auf Tiefe 3. Der Warnkreis folgt dem Schadensradius von 4 m. Acht Simulationstakte entsprechen 0,8 s. |
| Kamera | Auf dem ersten Entwurf stoppte die Kamera unter dem freien Bogen schon bei 0,8776 m. Mit der Öffnung zwischen den beiden Pfeilern bleibt die Sichtachse frei, am Stein greift die Verdeckung. Die beiden Kampfkamera-Profile und der Wechsel des Farbprofils sind eigene PlayMode-Fälle. |
| Mobile | Die Geometrie nutzt Unity-Primitiven und bereits importierte LODs. Der Editor-Test lief mit Android als aktivem Bauziel. Eine Messung auf einem physischen Galaxy Tab A9 steht aus. |

Der erste Kaltstarttest verglich eine mit Seed 71 und Beuteregel V1 erzeugte Station mit einem Kaltstart unter den echten Einstellungen der App. Der unterschiedliche Seed erzeugte andere Forge-Gegneridentitäten. Die abschließende Prüfung verwendet den tatsächlichen Spieleinstieg an beiden Enden. Auf der korrigierten Zwischenrevision liefen alle 13 Fälle erfolgreich (XML-SHA-256 `515ef826ed9cad0da5d2d316db550ae14fbcc3375ef8d9ec5c3f59525bea5588`).

## Nachbarprüfungen und Fachberatung

Die native Kontrollgruppe auf unveränderter Basis zeigte bereits einen unabhängigen Fehler beim Prüfen der privaten Dorfhalle in der öffentlichen Projektkopie (`CryptLeadsToVillageAndResumesItsCheckpoint`). Die ausgeführten Aschenzoll-Nachbartests bestanden. Für den Nordwechsel wurden die Fachbereiche Regionen, Core, Persistenz, Kampf, Weltgestaltung, Platzierung, Shader, Kamera, Prüfung und Android befragt. Der Neuro-Director-Konsilstand `588b81344ac427520724e6da390bb20e30c8b0b9` erzeugte Beratung zu 28 Fachbereichen. Native Prüfnachweise stammen vom gesonderten Spielprojekt.


## Originalbilder der öffentlichen Ausführung

[Nordbogen in Aschenzoll](aschenzoll-nordbogen-72510b0c.png) · [Basaltdamm und Ritualpodest](glutklamm-causeway-72510b0c.png) · [Warden mit aktivem Impuls](glutklamm-warden-72510b0c.png) · [Dateigrößen und SHA-256](native-public-72510b0c.json)

Die Bilder zeigen die reguläre Spielkamera bei 960 × 600 Pixeln. Der öffentliche Checkout verwendet Ersatzkörper für privat bereitgestellte Charaktermodelle.

[Unity-Erstlauf mit abweichendem Seed](unity-4300ee52.xml) · [Korrigierter Kaltstart](unity-4d7fe150.xml) · [Damm vor der Materialkorrektur](glutklamm-causeway-4d7fe150.png)

## Studio-Prüfung

In einem getrennten Editorprojekt mit den vorhandenen privaten Helden- und Warden-Modellen bestanden auf demselben Produktbaum fünf von fünf PlayMode-Fällen. Der Warden trug das tatsächlich an seine rechte Hand gebundene Schwert; der Ring blieb vollständig sichtbar. Die dunkle Rüstung hebt sich in diesem Licht nur schwach vom Basalt ab. Das gesonderte Studio-NUnit-XML trägt SHA-256 `9703e10145a9fd934436ffc0f8fe9547a498365433ae1e7565dd91383a1183da`. [Fachliche Abnahme](../../../.sdlc/runs/NORD-001/final-report.md)
