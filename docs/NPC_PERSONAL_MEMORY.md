# Persönliches NPC-Gedächtnis in Ashen Crypt

Eine Spielidentität besitzt genau einen `NpcMind`. Dessen `PersonalCognition` sitzt zwischen den vorhandenen Beobachtungen, dem zugelassenen Rollencontroller und der ausgeführten Aktion. Bei gleichen Beobachtungen, Gewichten und Spielticks ist das Ergebnis reproduzierbar.

| Ebene | Inhalt und Lebensdauer |
| --- | --- |
| Kurzzeit | Bis zu acht persönliche Episoden mit Kategorie, Ort und Sinnesdetail. Nach einem Spieltag ohne Wiederabruf entfallen Ort und Detail. |
| Gegenwart | Vier ganzzahlige Knoten bündeln Nahrung, Erholung, Bedrohung und soziale Nähe. Eine persönliche Matrix mit 40 vorzeichenbehafteten Byte-Gewichten verändert die Priorität zulässiger Aktionen. Eine ausgeführte Aktion erhält ein Lernsignal aus ihrer gemessenen Wirkung. |
| Langzeit | Bis zu 16 wichtige Aussagen mit Subjekt, Beziehung, Objekt und Herkunft. Am dritten Spieltag seit dem Ereignis entfallen auch wiederholt abgerufene Sinnesdetails und die Kategorie. Ein Bericht behält seine Quelle. |

Bei der Hochzeit sieht ein Gast zuerst Blumen und Essen am Hof. Ohne Wiederabruf erinnert er sich später an die Mahlzeit. Ab dem dritten abgelaufenen Spieltag bleibt die Aussage, dass die beiden verheiratet sind. Ein abwesender NPC kann bei einer späteren Begegnung einen Bericht erhalten. Das Spiel fragt das persönliche Wissen über `mind.Cognition.Recall` ab. Die unabhängig geführte Quest- und Ereignishistorie belegt den Ablauf.

## Anschluss für weitere Bedürfnisse und Berufe

| Entscheidung der Wirtschaft | Vorhandener Anschluss |
| --- | --- |
| Eine Figur erscheint | Dieselbe stabile Identität wiederverwenden. Für zusätzliche Bewohner `NpcForgeState.BindResident(stableId, role)` benutzen. Unbekannte Rollen erhalten den eingeschränkten Fallback-Controller. |
| Bedürfnisse und Angebote vergleichen | Aus dem tatsächlichen Weltzustand einen `ObservationFrame` mit den zulässigen Aktionen bilden. Den Rollencontroller und `mind.Cognition` an `UtilityEvaluator.Decide` übergeben. |
| Eine Tätigkeit abschließen | Zuerst die echte Transaktion ausführen. Danach `mind.Cognition.RecordOutcome(action, tick, effect)` mit der gemessenen Wirkung aufrufen. Der Lernpfad prüft Aktionswahl, Tick und Rollenrechte. |
| Ein soziales Ereignis wahrnehmen | Einen stabilen semantischen Kern per `Archive.Append` festhalten. Die konkret anwesenden Personen über `Perceive` informieren. Sinnesdetails gehören in `EpisodeDetails`. Für Hörensagen nach einer Begegnung `Report` verwenden. |
| Verhalten nach einem Neustart fortsetzen | Der existierende Charakterspielstand speichert die persönlichen Gewichte und die noch vorhandenen Details. |

Hunger nutzt diesen Anschluss bereits. Die tatsächlich verbrauchte Lagerration und der gesunkene Hunger liefern das Signal. Für Holzfällen, Schreinern und Liefern braucht die spätere Wirtschaft einmal je Tätigkeit ihren Bestandsübergang, ihre Voraussetzungen und die zugelassene Rollenaktion. Weitere Figuren desselben Berufs können dann dieselbe Regel mit ihrer eigenen Erfahrung auswerten. Das Wirtschaftsdesign der Vorlage ist ein separater Ausbauschritt.
