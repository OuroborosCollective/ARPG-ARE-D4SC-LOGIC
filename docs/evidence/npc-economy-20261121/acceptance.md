# Dorfwirtschaft · Abnahme am 21. November 2026

Geprüft ist der Spielcode `7fa2e707bd3fceb4a36071d11b9140c70a3ca2e0` auf `main` `369cd58d80438e7c37f66feaefd32897f1c6f9fd`.
Die späteren Commits des Pull Requests ändern nur das Core-Prüfprojekt (Bericht auf Anforderung) und Dokumente; der
Ordner `Assets` ist bytegleich. Unity lief als lizenzierter Editor 6000.6.8f1 mit den privaten Modellen.

## Die zehn Abnahmefälle des Konzepts

| Fall | Was geprüft wird | Nachweis |
| --- | --- | --- |
| 1 Kleine Holzkette | Ein gefällter Baum verringert den Bestand. Zwei Holz und eine Dornbeere werden genau einmal zu einem Bogen; fehlt ein Material, entsteht keiner. | Core `SmallWoodChain`, `MissingMaterialStopsTheBow` |
| 2 Wiederaufforstung | Gefällte Bäume kommen nicht zurück. Nur gepflanzte Setzlinge wachsen, erst nach drei Spieltagen. | Core `Reforestation` |
| 3 Mangel und Ausfall | Eine defekte Axt und ihr Auftrag warten, bis wirklich Metall da ist; die letzte Axtnutzung überfüllt die Hände nicht. | Core `BrokenAxeWaitsForMetal`, `ALastAxeReturnsBeforeOverfilling`, `VillageEconomyMigrationTests` |
| 4 Lagergrenze | Maras Lager hält seine Grenze. Wiederholte Aufrufe, Laden und abgelehnte Übergaben verdoppeln oder verlieren nichts. | Core `StoreLimitAndRepeatedCalls`, `OneCarriesTheRations`, `DeliveryUsesItsOwnReservedLoad`, `AFullStockRefusesTheWholeGift` |
| 5 Jagd und Ernährung | Ein Tier und seine Teile werden einmal verbucht. Eine Ration senkt den richtigen Bestand und den Hunger der Person, die isst. | Core `HuntAndMeal`, `EatBeforeLeavingForCare` |
| 6 Wissen | Eine Übergabe kennen nur die Beteiligten und wer danebenstand. Ein Bericht bleibt ein Bericht mit Quelle. | Core `OnlyThoseInvolvedKnow`, `GiftAndSource`, `StoreDelivery`, `ReportsComeHome`, `TheMillSendsItsOwnReport` |
| 7 Beruf und Aufenthalt | Verletzte arbeiten nicht; wer im Wald ist, arbeitet nicht im Dorf. Die Arbeit wartet oder ein anderer übernimmt. Bei Gefahr am Werkplatz ruht die Arbeit. | Core `InjuredResidentsDoNotWork`, `NightAndNeeds`, `OrselsCare`, `WaterAtHome`, `LocalGuard`, `StarvingAtTheMill` |
| 8 Persistenz und Wiederholung | Gleicher Start und gleiche Ticks ergeben dasselbe Dorf, auch über Speichern und Laden. Alte Spielstände laden; gefälschte Wirtschaftsabschnitte werden abgelehnt. | Core `ReplayAndSaveLoad`, `LedgerOverTenDays`, `WholeSaves`, `TamperedSavesAreRejected`, `VillageEconomyMigrationTests`, `VillageEconomyJourneyTests` |
| 9 Grenzen der Darstellung | Was gezeigt wird, entspricht dem Kern. Zeigen, Verstecken und Anzünden ändert keinen Bestand; die Tafel sagt, was der Kern sagt. | PlayMode `VillageWorkShowsTheCoresStockAndNeverChangesIt`, `ResidentsAreSeenWhereTheCoreHasThemAndHiddenFiguresChangeNothing`, `WorkplacesStandOnFreeGround`, `FirstVillageVisitShowsTheActualStoreAndItsUnstaffedWork`, `ScrollingTheBoardReachesTheLastLineWithoutDonating`, `ALostScrollFingerDoesNotLockTheBoard` |
| 10 Reale Leistung | Tickkosten, Spielstandgröße und Speicherbedarf bei ein, drei und acht Bewohnern. | EditMode `VillageEconomyPerformanceTests`, Core-Bericht auf Anforderung; Werte unten |

Der vorhandene Jäger ist kein Arbeiter des Dorfs. Ob er jagt, solange er nicht begleitet, gehört zu den offenen
Gestaltungsfragen.

## Hinweise aus der Prüfung des Pull Requests

Jeder Hinweis wurde zuerst mit einem fehlschlagenden Test nachgestellt.

| Hinweis | Befund | Nachweis nach der Änderung |
| --- | --- | --- |
| Hungernder Bewohner mit Auftrag bleibt an der Mühle | bestätigt: stand dort, auch nachdem Mara wieder Rationen hatte | Core `StarvingAtTheMill`, 7 Prüfungen |
| Zwei Berichte desselben Ticks, der neuere geht verloren | bestätigt für Mühle und Wald | Core `NewerMillReportOfTheSameTick`, `NewerForestReportOfTheSameTick` |
| Verlorener Scrollfinger sperrt die Dorftafel | bestätigt: Finger blieb gebunden, Schließen unmöglich | PlayMode `ALostScrollFingerDoesNotLockTheBoard` |
| Ankunft mit Auftrag zählt als Erfolg | teilweise bestätigt: Der Ertrag kam auch bisher bei der Entscheidung am Ort an; gutgeschrieben wurde aber schon das Ankommen | Core `ErrandCreditFollowsItsYield` |
| Dorf arbeitet im Tick der Niederlage weiter | bestätigt | Core `NoVillageWorkInTheTickThePlayerFalls` |

Die vier früheren Hinweise (überfüllte Hände nach dem letzten Axthieb, Verletzte können nicht essen, Überlauf bei
der Abgabe, Tafeltext unter den Schaltflächen) sind mit `47a4776` behoben und durch die oben genannten Tests gedeckt.

## Ausgeführte Prüfungen

| Prüfung | Ergebnis | Beleg (SHA-256 der Ausgabe) |
| --- | --- | --- |
| Core `Tests/CoreVerification`, Release | 22.154 deterministische Prüfungen bestanden; die Dorfwirtschaft hat sechs eigene Prüfdateien | `283ab51920f18efc45793815e1b70f41cf479e096ec79c3e0ce4c7f81a0fc68f` |
| GitHub-Lauf „Core deterministic verification“ auf `bd9d249` | erfolgreich | [Lauf 44923695452](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44923695452) |
| Preflight und statischer Unity-Vertrag | 54/54, Vertrag bestanden | `943306d3d808b5b6377e8b099ee3be328962c051deaf6fa0d5032b41f82f8927` |
| Unity EditMode, alle 242 Fälle | 238 bestanden, 0 fehlgeschlagen, 4 übersprungen. 214 im Gesamtlauf, die 24 an private Grafik gebundenen Fälle danach im nativen Abnahmemodus | `ff98fe6ee905856b8b5555fe993d109c4cabadf0582bedd09e96e041c5b6440b` |
| Unity PlayMode, alle 189 Fälle, nativer Abnahmemodus | 189 bestanden, 0 fehlgeschlagen, 0 übersprungen. Keine Ausnahme und kein Compilerfehler im Editorprotokoll der Läufe | `e9635d9e902353c4dbddfc68b33a2a76387371be6eccafc4ef05d752cd191b3a` |

Die vier übersprungenen EditMode-Fälle brauchen eine besondere Umgebung: einen freien Bauplatz im Bau-Modus und drei
echte Blender-Aufträge der World Forge. Sie haben mit der Dorfwirtschaft nichts zu tun.

## Messwerte

Unity-Editor (Mono) auf dem Entwicklungsserver, je drei Spieltage echter Dorfticks. Kein Android-Gerät, kein
gerendertes Bild.

| Bewohner | Mittel je Tick | langsamste 100 Ticks, je Tick | Spielstand | Speichern | Laden |
| --- | --- | --- | --- | --- | --- |
| 0 | 8,8 µs | 237 µs | 2.459 Zeichen | 1,3 ms | 1,8 ms |
| 1 | 10,7 µs | 290 µs | 11.080 Zeichen | 1,0 ms | 9,0 ms |
| 3 | 9,7 µs | 300 µs | 27.692 Zeichen | 1,6 ms | 4,5 ms |
| 8 | 13,2 µs | 238 µs | 46.600 Zeichen | 9,7 ms | 6,8 ms |

Ein Spieltick dauert 100.000 µs. Die Wirtschaft kostet mit einer Arbeitskraft rund 2 µs je Tick. Im Core-Lauf mit
.NET gibt das Dorf je Tick und Bewohner etwa 60 Byte zusätzlich aus; der Abschnitt `economy` selbst hat nach zwölf
Spieltagen 526 Zeichen bei einer und 783 Zeichen bei drei Arbeitskräften. Der größere Teil des Spielstands ist das
persönliche Gedächtnis der Bewohner.

Im Spiel gibt es heute eine Arbeitskraft, weil nur die Mühlenquest einen Bewohner bringt.

## Nicht geprüft

- Android-Gerät: Bildrate, Speicher und Ladezeit mit laufender Dorfwirtschaft.
- Der Zeitaufwand des Speicherns in Spielabschnitten, die jede Sekunde speichern, auf einem Telefon.
- Die Werkplätze im öffentlichen Build ohne private Modelle wurden nur über den Ersatzpfad im PlayMode-Test geprüft,
  nicht als eigener Build.

## Grenzen

Wald und Mühle werden nicht bespielt gezeigt. Figuren gehen in eigenem Tempo, Arbeitsbewegungen gibt es nicht.
Werkplätze haben keine Kollision. Ein Spielstand ist Klartext auf dem Gerät der Spielerin: Der Kern erkennt
Wirtschaftsabschnitte, deren Buch nicht aufgeht, aber keine in sich stimmige Fälschung.
