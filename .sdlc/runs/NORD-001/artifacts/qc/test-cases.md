---
run_id: "NORD-001"
task_id: "QC-001"
producer: qc
status: reviewed
revision: 1
---

# Reproduzierbare Fälle

| Fall | Voraussetzung und Ausführung | Erwartete Beobachtung |
| --- | --- | --- |
| TC-001 · REQ-001 / AC-001 | Mit gültiger Stationsfigur neben dem Ausgang, am westlichen Bogen und an der getrennten Waldabzweigung den regulären Durchgang versuchen. Beide Beweisentscheidungen bei Ilyan persönlich melden. | Unbeteiligter Standort löst keine Reise aus. Der Bogen öffnet die Glutklamm auf Tiefe 2. Der Waldweg ist weiterhin begehbar und erlaubt die Rückkehr. |
| TC-002 · REQ-002 / AC-002 | Lebenden Rattler, ungemeldeten Sieg, fehlenden Frachtbericht und gefallene Spielfigur prüfen. Ausrüstung und Vorratsbeutel jeweils als letzte Beute belassen und über den echten Aufnahmeaufruf aufheben. | Vorbedingungen blockieren die Passage. Der letzte Vorratsbeutel sperrt sie selbst bei leerer Ausrüstungsliste. Nach dem letzten Aufheben und vollständigem Bericht ist sie frei. |
| TC-003 · REQ-003 / AC-003 | Berichtete Station mit dem ausgelieferten Startprofil aus PlayerPrefs laden, zum Bogen laufen, Kapitel wechseln. Aktive Szene entladen, AshenEntry-Szenenkontext und ArenaRuntime neu erzeugen. | Der Backup-Slot enthält die vorherige Station. Primärslot und erneut serialisierte Figur sind bytegleich. Frachtwahl, Region und Tiefe 2 bleiben erhalten. |
| TC-004 · REQ-003 / AC-004 | Nach einer wirklichen Waldreise sind die Slots an den Kartenkatalog gebunden. Dem separaten Speicherleser beim Nordwechsel gezielt diesen Katalog entziehen. | Erwarteter Readbackfehler, dieselbe laufende Station, Tiefe 1, beide vorigen Slots wortgleich, weitere Überschreibungen gesperrt. |
| TC-005 · REQ-003 / AC-005 | Aus den gleichen vorher eingelesenen AC22-Bytes zwei echte Core-Instanzen mit Seed 71 erzeugen, jeweils einmal durch den Bogen gehen und beide Quellwelten erneut lesen. | Quellhash bleibt intakt. Beide Zielhashes sind gleich und verschieden vom Quellhash. |
| TC-006 · REQ-004 / AC-006 | Durch den Bogen gehen, zum Warden laufen, Primär- und Ausweich-Kameraprofil sowie manuelles Zoom, verdeckenden Stein, reduzierte Effekte und Kapitelwechsel auslösen. Boden, Weg und Säule auf Schieferbindung prüfen. Originale Kameraaufnahmen sichten. | Die Öffnung lässt den Blick durch, der Stein beschränkt ihn. Das aktive Volume übernimmt den Kapitellook. Steg, Fissuren, Säulen, Ritualpodest und Pulsring sind sichtbar. Basalt hat Materialstruktur. Der Engine-Paketbaum entspricht der Basis. |
| TC-007 · REQ-005 / AC-007 | Warden innerhalb des Radius auslösen, sieben Takte beobachten, am achten vergleichen. Einmal stehen bleiben, einmal ausweichen und gesondert die Aggroreichweite verlassen. Warden und Wächter regulär besiegen, letzten Beutel liegen lassen und anschließend einsammeln. | Der Acht-Takt-Impuls unterscheidet Treffer und Ausweichen und läuft außerhalb der Aggroreichweite ab. Echter Angriff verletzt den Warden. Der letzte Beutel und die Position am Ausgang steuern die Freigabe von Tiefe 3, die einen Neustart übersteht. |
| TC-008 · REQ-006 / AC-008 | Git-Ausgangsbaum mit Zielbaum, bestehender Kapitelprojektion, öffentlichen Ressourcen, Bosslogik und vorhandenem PlayerPrefs-Pfad vergleichen. Szene im laufenden Ziel prüfen. | Bereits vorhandene Klammbauteile, Warden und Speicherweg werden weitergenutzt. Nordbogen, Randdetails und Materialbindung haben eine nachweisbare Aufgabe im Zielbereich. |

Ergebnis, Ausführungszeit und Originalbelege zu jedem Fall stehen in `execution-results.md`. Die Zuordnung zu den genehmigten Kriterien steht in `requirement-coverage.md`.
