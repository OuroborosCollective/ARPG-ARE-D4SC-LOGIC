# Aschenzoll: leerer Tiertransport und Beweisentscheidung

Der Besitzer hat den leeren Tiertransport mit Beweisentscheidung gewählt. Es
werden weder lebende Gefangene noch ein Auftraggeber oder das Schicksal der
verschwundenen Verletzten erfunden.

## Spielbarer Ablauf

Der vorhandene Waldweg führt zum festen Frachtplatz. Der Spieler untersucht
Aushang, leeren Käfig und Frachtbeleg vor Ort, in beliebiger Reihenfolge. Sendung
17 verbindet die alte Mühle mit der Rückführung über Aschenzoll zum Nordwechsel;
die Käfigspuren beweisen noch keine Identität des früheren Insassen.

Nach allen drei Untersuchungen stehen am Pult zwei dauerhafte Möglichkeiten:

- **Original sichern:** Der Notriegel hält die Plattform fest. Der Spieler muss
  den Originalbeleg anschließend ausdrücklich aufnehmen. Bloßes Lesen zählt
  nicht als Besitz. Die Weiterfahrt dieses Transports ist danach ausgeschlossen.
- **Weitergabe verfolgen:** Der leere Käfig fährt in 100 bestehenden
  Simulationsticks zur oberen Rampe. Der Beleg bleibt bei der Fracht. Erst eine
  Untersuchung am angekommenen Käfig bestätigt die Weitergabe; Ankunft allein
  reicht nicht als Beweis.

Die Bewegung pausiert außerhalb des Frachtplatzes. Laden mitten in der Fahrt
setzt denselben Zustand fort. Der seitliche Fußweg und Rückweg bleiben offen.
Der Schienenstrang liegt außerhalb der unveränderten Bewegungsgrenze, sodass
auch ältere Stations-Checkpoints gültig bleiben.

Der Spieler berichtet Ilyan persönlich im Dorf. Nur dieser Kontakt erfährt den
Bericht; Mara erhält kein unerklärliches Wissen. Die gewählte Beweisart bleibt
im Journal nach dem Bericht sichtbar. Der Nordwechsel ist als nächste Spur
benannt, aber noch kein betretbares Gebiet. Eine Wächterkonfrontation und weitere
Stationstätigkeiten sind noch nicht implementiert. Dieser Ermittlungsabschnitt
vergibt keine neue Erfahrung oder erfundene Beute.

## Eigentümerschaft und Speicher

| Bereich | Dateien und Aufgabe |
| --- | --- |
| Core / Produktion | `Assets/Scripts/Core/AschenzollFreight.cs`: Voraussetzungen, Entscheidung, Fahrt, Kontakt und Journalziel |
| Persistenz | `FreightState` und AC19: vollständiger bestehender Save als geprüfter innerer Vertrag; Clues, Wahl, Fahrtticks, Belegbesitz, Prüfung und Bericht |
| Effekt | Bestehender `HungerAction`-Pfad und individuelle Forge-Ereignisse; keine zweite Uhr oder Quest-Authority |
| Unity-Projektion | `AschenzollPresentation.cs`, Stationszweig in `WoodlandPresentation.cs`: Käfig, Schienen, Beleg, Riegel, Schilder und Landschaft lesen den Core |
| Test | `AschenzollTests.cs` und `AschenzollRuntimeTests.cs`: beide Zweige, harte Voraussetzungen, Wiederholung, Migration, Save/Load und persönlicher Bericht |

AC19 wird nur bei tatsächlichem Frachtfortschritt geschrieben. Alte AC1–AC18
bleiben lesbar. Prüfsumme, kanonische Zahlen, UTF-8/Base64, erlaubte
Zustandskombinationen und exakte Reserialisierung werden geprüft. Die bisherigen
Dungeon-, Wald-, Equipment-, Gedächtnis- und Begleiterverträge bleiben enthalten.

## Nachweise und Grenzen

Auf `c10f5b1d54910362e2b6bf5b5c26d27d668fc0ed` bestehen die beiden neuen nativen
Szenarien, anschließend die vollständigen 122 PlayMode- und 48 EditMode-Tests
unter Unity 6000.6.8f1. Private Dateien blieben unverändert; Besitzer-Prefs und
Projekteinstellungen wurden wiederhergestellt. Die Tests gehen durch den echten
Wald-/Kampf-/Reiseadapter und die tatsächlichen Interaktions- und Speicherpfade.
Der abschließende Dorfkontakt nutzt ein ausdrücklich arrangiertes Reisefixture,
keinen behaupteten unbeaufsichtigten vollständigen Spieler-Durchlauf.

Die Folgekorrektur erhält das abgeschlossene Journalziel. Die visuellen
Folgeänderungen verkleinern die Schilder, ergänzen Waldhintergrund und Boden und
erhalten Kamera und Menüs. Aktueller Core: 8096 Assertions, 40 Preflights. Der
native Readback auf `bcb4296feb7bf76a2106b9af6c0146b3b21e6704` besteht alle
121 Spiel-PlayModefälle; ein getrenntes Sample-Rundungsproblem wurde anschließend
auf `0377c9ffd7139114673206dabb32072849923061` mit 2/2 gezielten PlayModefällen
repariert. Dort bestehen auch 48/48 EditModefälle (40 Spiel, 8 Sample).
Nach der Wald-Materialkorrektur bestehen auf
`35ed66d98dbb083e1d9788751a72ad530251e351` erneut beide Stationszweige, die
Waldreise und der Material-Lebensdauertest: 4/4, keine Skips. CI43433696048
bestätigt 8096 Core-Assertions, 109 Sample-Assertions und 40 Preflights.
[Getrennte Resultate und vollständige XML-Prüfsummen](evidence/aschenzoll-freight-20261113/native-readback.json).

Die beiden nativen Schlussansichten wurden geprüft: kleine lesbare Schilder,
Waldhintergrund und texturierter Boden. Kein neuer Android- oder physischer
Gerätetest für diesen Storyabschnitt wird behauptet.
