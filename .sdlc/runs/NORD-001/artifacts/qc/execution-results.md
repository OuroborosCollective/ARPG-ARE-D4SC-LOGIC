---
run_id: "NORD-001"
task_id: "QC-001"
producer: qc
status: reviewed
revision: 1
---

# Ausführung auf dem Zielbaum

Produkt `72510b0c3b4325f3a8928e51bedfdb64262bf7fc`, Baum `0407a314e9c77df9aefdcbaed8a3e3cf29ef0a04`. QC verglich die 28 kumulativ geänderten Produktpfade mit den Git-Objekten. Arbeitsbaum und Index stimmten bei Assets, Tests, Tools, Packages und ProjectSettings mit dem Ziel überein.

## Eigenständig ausgeführte Kommandos

Alle drei liefen über `check-task NORD-001 QC-001` in einem eigenen Linux-Netznamespace. Zeitangaben gelten für den 26. November 2026 in UTC. Die drei Verzeichnisse enthalten die Originalausgaben und die jeweiligen Start-/Endzeitpunkte mit Exitcode.

| Record unter `evidence/commands/` | Kommando | Ende UTC | Ergebnis |
| --- | --- | --- | --- |
| `EVD-3dbba544d962f987` | .NET 8.0.428 Release, `dotnet run --project Tests/CoreVerification/CoreVerification.csproj --configuration Release` | 18:59:22.899 | passed · 23.657 Assertions, Exit 0 |
| `EVD-a88a90e75eccd378` | `python3 -m unittest discover -s Tests/Preflight -v` | 18:59:24.402 | passed · 64 Tests, Exit 0 |
| `EVD-5b94a16121327828` | `python3 -m unittest discover -s Tools/Director -v` | 18:59:25.289 | passed · 76 Tests, Exit 0 |

Die vollständige Core-Ausgabe `EVD-3dbba544d962f987/stdout.txt` und das ausgepackte Original `docs/evidence/nordwechsel-20261126/core-72510b0c.log.gz` haben unabhängig berechnet denselben SHA-256 `c4a6e96ceb568db79d5147ba8e409ae8e8cc570a8147238d5fe4dc63eea6ea18`.

## Native Unity- und Bildbelege

QC las jeden ausgewählten NUnit-Fall direkt aus `docs/evidence/nordwechsel-20261126/unity-72510b0c.xml`. Das XML meldet 16 bestanden, null fehlgeschlagen und null übersprungen. Unity 6000.6.8f1 (237b6593792b) lief im Linux-Editor mit Android als aktivem Bauziel, OpenGLCore und Mesa llvmpipe unter Xvfb. Dauer laut XML von 18:48:10 bis 18:48:19 UTC insgesamt 8,9069211 Sekunden. Die drei Bilder stammen aus der tatsächlichen Spielkamera, jeweils 960 × 600. Sie zeigen öffentliche Ersatzfiguren.

| Original unter `docs/evidence/nordwechsel-20261126/` | Unabhängig berechneter SHA-256 |
| --- | --- |
| `unity-72510b0c.xml` | `1d3492733c681f9ff1dcdc36f4c94ab76b380db0f5e3cf72fe1df9434cd07821` |
| `aschenzoll-nordbogen-72510b0c.png` | `9cbaf3db86fcf042dd8649190f845a1c5468d784b5ff0d3f6216b0d5241bda9b` |
| `glutklamm-causeway-72510b0c.png` | `581c3f691108e4b0cca0ba1eb0b4882b9e3767fafeba13f73743cc414df1bd77` |
| `glutklamm-warden-72510b0c.png` | `16883fbc440e70150209e748a27734a17ec245e8eb2b3788b67a0fef356b4038` |

## Ergebnisse der Fälle

| Fall | Ergebnis | Direkt beobachteter Nachweis |
| --- | --- | --- |
| TC-001 · AC-001 | passed | Core-Ausgabe ab den Fällen `nordwechsel-reported-freight-opens-ember-rift-False/True`, getrennte Trigger und `nordwechsel-optional-woodland-remains-separate-and-returnable`. Im XML erreicht `NordwechselCommitsChapterTwoBeforeSwitchAndRestoresItInAshenEntry` die bestehende Arena. Beide persönlichen Frachtentscheidungen bestanden als eigene native Nachbarfälle. |
| TC-002 · AC-002 | passed | Core prüfte den lebenden Rattler, den ungemeldeten Sieg und den fehlenden Frachtbericht, eine gefallene Spielfigur sowie den letzten Ausrüstungsdrop und Vorratsbeutel. Beide wirklichen Aufnahmepfade gaben die Passage erst mit erfüllten Bedingungen frei. |
| TC-003 · AC-003 | passed | Derselbe erfolgreiche Unity-Fall speicherte aus der real geladenen Station, beließ deren Bytes im Backup und lud den Ziel-Primärslot nach Entladen und Neuaufbau des AshenEntry-Szenenkontexts. Tiefe 2, Fracht und kompletter AC22-String stimmten. Der Core prüfte zusätzlich den Kaltstart auf Tiefe 3 nach Boss und Beute. |
| TC-004 · AC-004 | passed | `NordwechselReadbackFailureKeepsTheStationWorldActive` löste durch den entzogenen Lesekatalog den erwarteten Speicherfehler aus. Laufende Weltinstanz, vorherige Primär-/Backupbytes, Tiefe 1 und Schreibsperre wurden im Unity-Fall geprüft. Zwei native Nachbarfälle prüften die gültige passende Sicherung und den fehlenden Kartenbestand. |
| TC-005 · AC-005 | passed | Der eigenständige Core-Lauf gab für Seed 71 bei beiden Quellen `2AC47DE508663AE4` und bei beiden Zielen `7703E8B020A8EB81` aus. `nordwechsel-two-identical-destinations-and-sealed-source` überprüfte zusätzlich Quellenbytes und erhaltene Vorgängertiefe. |
| TC-006 · AC-006 | passed | `WardenFramingKeepsBothFightersInViewAndRespectsZoom` bestand mit beiden Profilen. `NordbogenCameraSeesThroughOpeningAndStopsAtStone` prüfte die Sichtachse, `ChapterChangeRegradesTheExistingVolume` den Wechsel des vorhandenen Volumes. Die drei `RiftBasaltKeepsAuthoredSurfacesOrUsesTexturedPublicSlate`-Fälle prüften Bindung, Maske und Skalen. QC sah im Stegbild vier gemaserte Säulen und im Kampfbild das Podest mit Pulsring, roten Fissuren und seitlichem Schiefer. |
| TC-007 · AC-007 | passed | Die Core-Fälle `nordwechsel-no-pulse-before-eighth-tick` und `nordwechsel-eighth-tick-pulse-respects-position` bestätigten beide Positionen. `nordwechsel-warden-pulse-expires-after-eight-ticks-outside-aggro` bestätigte den Ablauf außerhalb der Aggroreichweite. Der Core besiegte Warden und Wächter mit voller Boss-Ausgangsgesundheit bei erhöhter Testwaffenstärke, sammelte Beute und erreichte nach dem Ausgang Tiefe 3. Die Unity-Figur traf den Warden mit einer regulären Fähigkeit. |
| TC-008 · AC-008 | passed | In `0cf480c` enthielten `ArenaRuntime.cs` und `ArpgSimulation.cs` bereits Steg, Fissuren, Säulen, Dais und den Acht-Takt-Warden. Die neue Präsentation platziert CryptArch, ShaleLedge und Brazier am Bogen und vier bestehende LOD-Stücke außerhalb der Klammspielfläche. Alte Bauteilnamen erscheinen auch in Ziel-XML und Bildern. Git-Paketbaum auf Basis und Ziel identisch, Manifestblob `752f8225cf6500cd5b29611a540e8f912a4ce63d`, Lockblob `8ae196da0c55bd38c8b9269d889e7dd0537efc7c`. |

## Prüfgrenzen

Physische Android-Framezeit, Installation dieses Stands auf einem Tablet und die subjektive Spielabnahme haben Ergebnis `not_tested`. Die XML-Auswahl enthält 16 Fälle. Auf der unveränderten Basis (Kontrollgruppe 2/3) scheiterte der separate Dorfhallenfall `CryptLeadsToVillageAndResumesItsCheckpoint` in der öffentlichen Projektkopie mit fehlenden privaten visuellen Inhalten. Dieser Fall war außerhalb des Nordwechsel-Retests. Kein Nordwechsel-Fall blieb blockiert.
