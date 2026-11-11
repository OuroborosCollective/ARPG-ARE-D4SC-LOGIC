# Ashen Crypt: Krypta und Aschengrenze

## Umfang

Die Szene `AshenEntry` beginnt mit der Krypta. Erst ein vom Core freigegebener
Durchgang nach Kampf und vollständiger Beuteaufnahme führt auf den südlichen
Dorfweg. Dort bleiben Ilyan, Mara und Dorn die vorhandenen drei sozialen Akteure.
Der zweite Akt endet für diese Integration beim Vorratskonflikt. Es gibt keinen
automatischen dritten Akt und keine neue Questbelohnung.

Die ältere Szene `Arena` behält ihre bisherige Kapitelabfolge. Die neue Szene
verwendet denselben `ArenaRuntime` und dieselbe `ArenaSimulation`, mit einem
explizit ausgewählten Einstiegsprofil. Es läuft keine zweite Spielsimulation.

## Eigentümer und Speicher

`CharacterProgression.EntryAct` besitzt den dauerhaften Akt (0 = Altprofil,
1 = Krypta, 2 = Dorf). AC8 schreibt die Katalogrevision/den Akt als `1,1` oder
`1,2` neben die vorhandenen Itembelege und sozialen Daten. Andere Revisionen,
Akte und Krypta-Tiefen werden abgelehnt. AC1–AC5 und AC7 bleiben lesbar; deren
Schreibformate ändern sich für Altprofile nicht. AC6 bleibt reserviert.

Unity verwendet für `AshenEntry` die PlayerPrefs-Schlüssel `ARE_ARPG_entry_v1`
und `ARE_ARPG_entry_v1_backup`. Das vorhandene Charakterprofil wird nicht
überschrieben. Der Aktwechsel schreibt und liest den neuen Checkpoint vor dem
Weltwechsel zurück. Ein Checkpoint speichert Fortschritt, nicht die komplette
laufende Welt: Tick, Position, Cooldowns und laufende NPC-Bewegung werden beim
Neustart wie bisher neu aufgebaut. Replay bedeutet identische Ausgangsdaten,
Seeds, Kataloge und geordnete Tick-Eingaben.

## Vorräte und Beobachtung

`TakeCampRation` ist nur im zweiten Einstiegsakt verfügbar. Die Transaktion
prüft Leben, Maras Bewusstsein, denselben 10-Tick-Interaktionscooldown,
2,4 m Interaktionsradius, freie Sicht und positiven Lagerbestand. Genau eine
Ration wandert vom bestehenden Lager zum Spieler. Mara erinnert den direkten
Übergriff einmal. Dorn erhält diese Erinnerung ausschließlich, wenn er bei der
Tat lebt, höchstens 8 m entfernt ist und keine Gebäudegrundfläche seine Sicht
schneidet. Seine bestehende mutige Policy entscheidet die Reaktion.

Dies erweitert die vorhandene aggregierte `NpcMemory`, nicht den unfertigen
AC6-Ereignisspeicher: keine Gerüchte, episodischen Ereignis-IDs oder globale
Reputation. Der ursprüngliche Fehlbestand von zwei Rationen bleibt ungeklärt;
ein späterer beobachteter Spielerdiebstahl beweist nicht diesen früheren Fall.

## Weltdarstellung

`EntryWorldLayout` enthält zehn undurchlässige Dorfgebäude in Millimetern.
Bewegung und Sichtprüfung verwenden dieselben Rechtecke. Der private
`EntryWorldArtBuilder` passt die sichtbaren Gebäude an diese Grundflächen an.
Häuser und Bäume außerhalb des begehbaren Bereichs bilden die Fernkulisse.
Die Krypta verwendet vorhandene Steinbauteile und die vier etablierten
Core-Hindernisse. Prefabs enthalten ausschließlich Darstellungs-Komponenten;
Unity-Physik ist keine Entscheidungsquelle.

Die Eingangsdaten und erzeugten Prefabs bleiben in ignorierten privaten
Assetverzeichnissen. Auch die Zuordnung der konkreten Paketdateien und die
Materialpaletten bleiben in privaten JSON-Katalogen. Die Palette-Konvertierung
erhält die UV-Anordnung und erzeugt lokale URP-Materialien; fehlende Texturen
werden nicht durch eine angeblich fertige einfarbige Figur ersetzt.
Fehlende erforderliche Eingaben lassen den Bau abbrechen.
Der allgemeine Laufzeit-Fallback ist nur für Entwicklung ohne private Assets
bestimmt; er erfüllt keine visuelle Abnahme.

## Nachweise

`EntryJourneyTests` prüft die echte Core-Abfolge, getrennte Altprofile,
deterministische Beute, AC8-Validierung, Save/Load, Replay, Ressourcentransfer
und Wahrnehmungsgrenzen. `EntryJourneyRuntimeTests` führt den vorhandenen Unity-
Adapter einschließlich PlayerPrefs und Szenenprojektion aus. Arrangierte
Gegnerpositionen/HP sind explizite Testfixtures, kein unbeaufsichtigter
Spieler-Durchlauf. Optionale Kameraaufnahmen sind echte Unity-Renderings,
enthalten aber nicht die über OnGUI gezeichnete Bedienoberfläche.

Revisionsgebundene Ausführungsnachweise stehen im privaten Evidenzlog.
Ein bestandener Core-Test allein belegt weder Unity-Grafik noch Android-
Bedienung, Framerate, Speicherbudget oder Release-Reife.
