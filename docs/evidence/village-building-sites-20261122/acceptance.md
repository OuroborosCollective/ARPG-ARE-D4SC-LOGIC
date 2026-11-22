# Abnahme: Bauplätze der Dorfhäuser (22.11.2026)

Geprüfter Code: `2b6ed07` (Bauplätze) zusammen mit der regionalen Kunst aus #109. Unity 6000.6.8f1, Editor mit
Software-Renderer (keine GPU).

## Entscheidungen des Owners (22.11.2026)

- Jedes Wirtschaftshaus beginnt als Bauplatz; davor bittet jemand um Material.
- Kosten: Schmiede 10 Holz und 6 Ascheisen, Feldscheune 12 Holz und 4 Ascheisen, Krankenstube 8 Holz und 4 Aschenflieder.
  (Für die Scheune war Stroh vorgeschlagen; Stroh kann der Spieler nicht tragen, deshalb Ascheisen.)
- Es bitten: der Schmied, der Bauer, Orsel. Orsel erscheint erst, wenn ihr Mühlenauftrag erledigt und der Rattler
  besiegt ist; sie verlegt ihre Kranken ins Dorf.
- Nach der letzten Abgabe: Dank, „Komm später wieder“. Das Haus steht erst beim nächsten Betreten des Dorfs.
- Ohne Haus läuft die Arbeit im Freien im bisherigen Tempo, im fertigen Haus in zwei Dritteln der Zeit.

## Was abgenommen wurde

| Kriterium | Nachweis | Ergebnis |
|---|---|---|
| Kosten, Bittende, Standplätze und Reihenfolge wie entschieden | Core `Rules` | bestanden |
| Abgabe nur am Bauplatz, nur Benötigtes, nie mehr als fehlt, nichts aus leerem Gepäck | Core `FromLotToHouse` | bestanden |
| Dank beim letzten Stück; danach wird nichts mehr genommen | Core `FromLotToHouse`, PlayMode `LotsShowWhatWasBroughtAndBecomeHousesWhenThePlayerComesAgain` | bestanden |
| Das Haus erscheint nicht während des Besuchs, auch nicht durch Speichern und Laden; es steht nach der Rückkehr | Core `FromLotToHouse` (echte Reise), PlayMode (Ankunft von Hand ausgelöst) | bestanden |
| Der Bauer fragt erst, wenn die Schmiede steht | Core `FromLotToHouse` | bestanden |
| Orsel kommt nach Mühle und Rattler und bleibt, auch wenn der Bau des Rattlers neu beginnt | Core `OrselComes` | bestanden |
| Pflege im Dorf: niemand läuft zur Mühle, Kräuter und Verbände aus dem Dorfvorrat, Patienten und unterwegs befindliche Kräuter ziehen mit | Core `CareInTheSickRoom` | bestanden |
| Wer das Dorf gegen sich hat, kann nichts abgeben | Core `FromLotToHouse` | bestanden |
| Spielstand: nur erreichbare Zustände werden gelesen (26 unmögliche Token, gefälschte Spielstände mit richtiger Prüfsumme) | Core `WhatASaveMayClaim` | bestanden |
| Arbeit im Haus zwei Drittel, ohne Haus unverändert, andere Arbeit unberührt | Core `QuickerUnderARoof` | bestanden |
| Auf einem Bauplatz steht kein Haus; Stapel zeigen genau das Gelieferte; die Person steht dort und sagt ihre Worte | PlayMode, derselbe Test | bestanden |
| Anzeige ändert den Kern nicht (Welt-Hash und Spielstand gleich) | PlayMode, derselbe Test | bestanden |
| Bauplatz-Modell trägt die Stapel und passt auf den Grund eines Hauses; Fenster passt auf den kleinsten Bildschirm | EditMode `VillageBuildSitePresentationTests` | bestanden |

Vier Review-Punkte zum Umzug von Orsel wurden erst mit einem fehlschlagenden Kern-Test nachgestellt und dann behoben.

Nebenbefund: Das Werkzeug, das die Figuren erzeugt, konnte seit dem 20.11. keine Figur mehr erzeugen, weil es im
Play-Modus Assets neu serialisierte. Es veröffentlicht jetzt im Edit-Modus; die Figuren des Schmieds und Orsels sind
damit entstanden.

## Gesamtläufe

| Lauf | Ergebnis |
|---|---|
| Core-Prüfung (Bauplätze zusammen mit #109) | 22.634 Prüfungen bestanden |
| Preflight, Unity-Vertrag, Director, Fadenwerk-Gateway | 54 bestanden, Vertrag PASS, 59 bestanden, 5 bestanden |
| EditMode, gesamter Bestand | 242 von 246 bestanden, 0 fehlgeschlagen, 4 übersprungen (brauchen eigene Testdaten) |
| PlayMode, gesamter Bestand | 200 von 202 bestanden, 0 fehlgeschlagen, 2 übersprungen (Prüfbilder nur auf Anforderung) |

Die Gesamtläufe liefen vor den letzten beiden Korrekturen an den Kernregeln (Umzug von Orsel). Danach liefen die
Core-Prüfung vollständig und die drei Dorf-Spieltests einzeln erneut: bestanden.

## Nicht geprüft

- Android-Build und Gerät.
- Eine echte Reise aus dem Dorf und zurück im laufenden Spiel (im Kern geprüft, im Spieltest von Hand ausgelöst).
