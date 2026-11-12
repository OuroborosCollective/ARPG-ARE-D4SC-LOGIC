# Alter Speicher und verborgene Krypta

Nach der Entscheidung über Ilyans Versorgung und der Untersuchung der verlassenen Lagerstatt öffnet sich der Nordweg der Mühle zum alten Speicher. Alle drei Versorgungsentscheidungen führen zu dieser Spur. Die frühere Entscheidung bleibt bestehen.

Im Speicher liegen drei örtliche Hinweise: ein Verband im Westen, Schleifspuren im Südosten und ein versiegelter Mechanismus im Norden. Goldene Bodenmarkierungen und das Spurenjournal zeigen die Untersuchungsstellen. Handlungen erfordern tatsächliche Nähe; mehrfaches Antippen erzeugt keine weiteren Folgen.

| Weg | Voraussetzung | Folge |
| --- | --- | --- |
| Kontrolliert öffnen | Alle drei Hinweise untersuchen | Zugang zur Krypta ohne ausgelöste Steinfalle |
| Siegel brechen | Mechanismus untersuchen und Gefahrenhinweis erhalten | Einmalige Steinfalle im vorderen Kryptagang |

Die Steinfalle warnt eine Sekunde lang mit einer roten Bodenfläche. Sie trifft danach die feste markierte Fläche, nicht eine nachgezogene Spielerposition. Wer rechtzeitig ausweicht, bleibt unverletzt. Wer darin stehen bleibt, verliert 320 Lebenspunkte. Laden oder Zurücklaufen setzt die bereits ausgelöste Falle nicht zurück. Beide Wege bleiben begehbar.

Vor einer verschlossenen inneren Tür findet der Spieler frischen Verbandsstoff. Das Journal beschreibt dumpfe Schläge von dahinter. Erst ein persönlicher Bericht bei Ilyan macht ihn zum Mitwisser. Andere Dorfbewohner erhalten dadurch keine erfundenen Erinnerungen. Die Identität des Verursachers, das Schicksal der Vermissten und das Öffnen der inneren Tür bleiben für die Fortsetzung offen.

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
