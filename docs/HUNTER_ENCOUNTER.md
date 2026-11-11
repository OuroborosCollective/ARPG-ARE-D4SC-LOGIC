# Jaeger und Baer auf der Aschestrasse

Die Begegnung wird durch Annaeherung an den Jaeger einmalig ausgeloest. Der Spieler behaelt die normale Steuerung. Nach einer Reaktionsfrist von 30 Ticks beginnt der Baer seinen Angriff. Ein wirksamer Spielertreffer lenkt ihn fuer 80 Ticks auf den Spieler um und unterbricht einen bereits begonnenen Angriff auf den Jaeger. Weitere Treffer erneuern diese Frist.

Der Jaeger schiesst alle 12 Ticks, solange der Spieler den Baeren bindet und das Ziel in seiner freien Schusslinie liegt. Ein Prankenhieb wird acht Ticks lang an seiner festgelegten Einschlagposition angezeigt. Ausweichen ist deshalb moeglich; Animationen verursachen keinen Schaden. Alle Werte werden im bestehenden 100-ms-Core berechnet.

Ohne Hilfe stirbt der Jaeger an den tatsaechlichen Angriffen. Sein Tod bleibt dauerhaft bestehen. Rettung setzt eigenen wirksamen Schaden, den Tod des Baeren und einen lebenden Jaeger voraus. Kampfbelohnung und der echte Geldbeutel koennen jeweils nur einmal uebergeben werden. Der Leichenraub ist eine eigene ausdrueckliche Aktion; die normale Beuteaufnahme beraubt den Jaeger nicht.

## Speicherung und Beobachtung

AC10 erweitert den bisherigen Story-Spielstand um den Kampfzustand und vier begrenzte, kausal nummerierte Fakten: Rettung, Tod, beobachtetes Nichthelfen und Leichenraub. Unberuehrte AC8-/AC9-Spielstaende behalten ihr Format. Gegenstandsbestaetigungen, Storyentscheidungen und bisherige persoenliche NPC-Erinnerungen bleiben erhalten. Ein Reload auf der Strasse stellt auch Lebenspunkte, Position, Fokus, Abklingzeiten und den Zufallszustand des Spielers wieder her. Ein ausdruecklicher Neustart setzt den Spieler zurueck, aber nicht das Schicksal des Jaegers.

Ein Reisender kann Ereignisse innerhalb seiner Sichtweite beobachten. Eine Anschuldigung wegen Nichthelfens erfordert, dass er den lebenden Spieler ausreichend lange in der wahrnehmbaren Hilfssituation gesehen hat. Tod allein ist kein Schuldbeweis. Nach dem Ausgang laeuft der Reisende zum Dorf. Ein wacher NPC erfaehrt einen Bericht erst bei einer wirklichen Begegnung in Sichtweite. Die Quelle bleibt gespeichert; empfangene Berichte werden nicht erneut verbreitet und zaehlen nicht als selbst erlebte Hilfe oder Gewalt.

Dieser begrenzte Begegnungsvertrag ist kein Abschluss des allgemeinen SocialEvent-/AC6-Backlogs. Er besitzt keine zweite globale Reputationszahl. Die Erweiterung dieses Vertrags auf beliebige Ereignisse muss die bestehenden Fakten und ihre Quellen migrieren.

## Darstellung

`RoadBear` und `RoadHunter` sind optionale passive Prefabs unter `Resources/ARE/Visuals`. Lizenzierte Modelle und Animationen gehoeren zum privaten Asset-Bestand. Der Baerencontroller bietet die Zustaende `Idle`, `Run`, `Attack` und `Death`; Root Motion und Animation Events duerfen keine Autoritaet besitzen. Der native Art-Abnahmetest verlangt den echten SkinnedMeshRenderer und diese vier Animationszustaende. Ein Entwicklungsplatzhalter ist kein bestandener Asset-Nachweis.

Name, persoenliche Hintergrundgeschichte und eine spaetere Wiederbegegnung mit dem Jaeger sind noch offen. Die Begegnung verriegelt weder den Weg zur Muehle noch den Rueckweg ins Dorf. Ein begonnener Kampf laeuft auch in einem anderen Gebiet durch denselben Core weiter; waehrend die Anwendung geschlossen ist, wird keine Wanduhrzeit nachsimuliert.
