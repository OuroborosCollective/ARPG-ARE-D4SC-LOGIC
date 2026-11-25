# Alter Speicher und verborgene Krypta

Nach der Entscheidung über Ilyans Versorgung und der Untersuchung der verlassenen Lagerstatt öffnet sich der Nordweg der Mühle zum alten Speicher. Alle drei Versorgungsentscheidungen führen zu dieser Spur. Die frühere Entscheidung bleibt bestehen.

Im Speicher liegen drei örtliche Hinweise: ein Verband im Westen, Schleifspuren im Südosten und ein versiegelter Mechanismus im Norden. Goldene Bodenmarkierungen und das Spurenjournal zeigen die Untersuchungsstellen. Handlungen erfordern tatsächliche Nähe; mehrfaches Antippen erzeugt keine weiteren Folgen.

| Weg | Voraussetzung | Folge |
| --- | --- | --- |
| Kontrolliert öffnen | Alle drei Hinweise untersuchen | Zugang zur Krypta ohne ausgelöste Steinfalle |
| Siegel brechen | Mechanismus untersuchen und Gefahrenhinweis erhalten | Einmalige Steinfalle im vorderen Kryptagang |

Die Steinfalle warnt eine Sekunde lang mit einer roten Bodenfläche. Sie trifft danach die feste markierte Fläche, nicht eine nachgezogene Spielerposition. Wer rechtzeitig ausweicht, bleibt unverletzt. Wer darin stehen bleibt, verliert 320 Lebenspunkte. Laden oder Zurücklaufen setzt die bereits ausgelöste Falle nicht zurück. Beide Wege bleiben begehbar.

Vor einer verschlossenen inneren Tür findet der Spieler frischen Verbandsstoff. Das Journal beschreibt dumpfe Schläge von dahinter. Erst ein persönlicher Bericht bei Ilyan macht ihn zum Mitwisser. Andere Dorfbewohner erhalten dadurch keine erfundenen Erinnerungen. Die Identität des Verursachers, das Schicksal der Vermissten und das Öffnen der inneren Tür bleiben für die Fortsetzung offen.

## Aschenwacht II – Erster Blick vom Dorf, späterer Weg durch den Vorhof

**Designentscheidung vom 25. November 2026. Die Umsetzung in Unity und die Bildabnahme stehen aus.**

Beim ersten Austritt aus der Startkrypta führt der Weg ins Dorf zu Ilyan. Vom niedrigen Dorfeingang aus sieht die Heldin schräg hinauf: Zwischen den nahen Hauskanten steigen Dächer und Stützmauern in mehreren Ebenen an. Über ihnen zeichnet sich das Tor des entweihten Vorhofs gegen einen schmalen Streifen Himmel ab. Das Tor gehört zur späteren verborgenen Krypta. Vom Dorf führt kein direkter Durchgang dorthin.

Für den ersten Bauversuch genügt eine Gasse. Der Weg knickt an einer Stützmauer ab, die kurz die Sicht aufs Tor nimmt. Er steigt begehbar auf ein höheres Podest. Dort erscheint dieselbe Torkontur in einer Lücke zwischen zwei Dächern. Beim Zurückgehen blickt man über die Mauer auf den tieferen Dorfeingang. Bodenhöhe, Figur und Sichtschutz müssen dabei zusammenpassen. Die freie Spielkamera und der Weg zu Ilyan bleiben nutzbar.

Erst auf der Route über die alte Mühle und den alten Speicher betritt die Heldin den Vorhof. Hinter dem geöffneten Speichersiegel tritt sie unter freien Himmel und erkennt auf der anderen Seite des Hofs das Tor von ihrem ersten Dorfblick wieder. Beide oben beschriebenen Siegelwege führen durch den Hof. Die Entscheidung am Siegel bestimmt weiterhin, ob im vorderen Kryptagang die Steinfalle auslöst.

**Abnahme im Spiel.** Auf derselben Unity-Revision einen ungeschnittenen Hin- und Rückweg mit der normalen Spielkamera aufnehmen. Am Eingang sollen Gasse und Tor zusammen lesbar sein, in der Kurve soll die Mauer das Tor verdecken, auf dem Podest soll es wieder sichtbar werden. Beim Auf- und Abstieg Schuhkontakt, Durchgänge und Kamera an der Mauer prüfen. Den Vorhof vom Dorf aus auf vorzeitigen Zugang prüfen und später über beide Siegelwege begehen. Erst diese Aufnahmen und Spielproben belegen die Wirkung.

## Zuständigkeiten und Spielstand

- `CryptInvestigation` gehört zur bestehenden `CharacterProgression`. Hinweise, erste Siegelentscheidung, Bericht und Fallenzustand haben genau diesen Eigentümer.
- Bewegungen, Kontaktprüfung, Geometrie und Schaden laufen im bestehenden C#-Core mit 100-ms-Ticks. Unity zeigt Zustände an und stellt Handlungen in die vorhandene Eingabewarteschlange.
- Der neue Spielstand AC11 erweitert AC10 erst beim Betreten des Speichers. Unberührte ältere Spielstände behalten ihr Format. Laden prüft Prüfsumme, kanonische Zahlen und Zustandsabhängigkeiten.
- In Speicher und Krypta speichert der Charakter zusätzlich Position, Lebenspunkte, Fokus, Abklingzeiten und deterministischen Zufallszustand. Ein expliziter Neustart erneuert den Spieler; die einmalige Siegelentscheidung und die verbrauchte Falle bleiben bestehen.
- Hindernisabmessungen stammen aus demselben unveränderlichen Core-Vertrag für sichtbare Projektion und Begehbarkeit. Die verschlossene innere Tür ist eine echte Grenze.

## Prüfungen

Die Core-Suite läuft beide Siegelwege für Offenlegung, Schutz und Ersatzversorgung. Sie prüft Entfernung, Wiederholungen, Warnung, Schaden, Ausweichen, Laden während der Warnphase, Rückwege, Erhalt der Versorgung und persönlichen Bericht. Native Unity-Tests benutzen die reale Handlungswarteschlange, PlayerPrefs, Gebietswechsel, Szenenprojektion und erneutes Laden. Arrangierte Gegner- und NPC-Kontakte in diesen Tests sind ausdrücklich Testaufbau und kein unbeaufsichtigter Gerätedurchlauf.

Kein neuer APK- oder Gerätetest ist Bestandteil dieses Abschnitts. Die tatsächliche Bildrate und Bedienbarkeit auf Android werden beim nächsten größeren Gebietsstand geprüft.

## Fortsetzung

Nach dem frischen Verband kann der innere Riegel explizit geloest werden. Der Folgebogen und sein AC12-Vertrag stehen in [RATTLER_IMMERGRUEN.md](RATTLER_IMMERGRUEN.md). Die urspruengliche Entscheidung am Speichersiegel bleibt erhalten.
