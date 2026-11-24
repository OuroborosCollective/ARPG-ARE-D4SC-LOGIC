# Art Forge: Browser-Regressionen

Diese Skripte starten einen neuen Chromium-Prozess mit einem eigenen temporären Browser-Kontext. Sie verwenden die echte `BauModus.html`, fangen aber sämtliche HTTP-Anfragen ab und liefern ausdrücklich synthetische API-Antworten. Die Testdaten gelangen nicht in den Produktpfad. Das Ergebnis ist ein Browser-Verhaltensnachweis, **kein Live-HTTP-, Unity-, Authentifizierungs- oder physischer Gerätenachweis**.

Voraussetzung sind bereits vorhandenes Node.js, Playwright Core und Chromium. Die Skripte installieren oder laden nichts herunter. Playwright-Modul und Browser müssen explizit angegeben werden; die Ausgabe gehört in ein Evidence-Verzeichnis außerhalb des Repositorys. JSON-Berichte enthalten HTML-/Skript-SHA-256, Request-Traces und Ergebnisse. Erzeugte PNGs dienen der internen Prüfung.

## Aufruf

Die folgenden Variablen müssen auf vorhandene Dateien beziehungsweise das gewünschte externe Evidence-Verzeichnis zeigen:

```sh
ASHEN_HTML=/absolute/path/to/Assets/Editor/BauModus/Web/BauModus.html
ASHEN_EVIDENCE=/absolute/path/to/evidence
ASHEN_PLAYWRIGHT_MODULE=/absolute/path/to/playwright-core
ASHEN_CHROMIUM_PATH=/absolute/path/to/chrome-headless-shell

node Tests/Browser/revision-races-scanning.cjs \
  --html "$ASHEN_HTML" --out "$ASHEN_EVIDENCE/races-with-scanning" \
  --playwright "$ASHEN_PLAYWRIGHT_MODULE" --browser "$ASHEN_CHROMIUM_PATH" \
  --phase green

node Tests/Browser/scan-polling.cjs \
  --html "$ASHEN_HTML" --out "$ASHEN_EVIDENCE/polling" \
  --playwright "$ASHEN_PLAYWRIGHT_MODULE" --browser "$ASHEN_CHROMIUM_PATH" \
  --phase green --suite protocol
```

| Skript / Suite | Verhalten |
| --- | --- |
| `revision-races.cjs` | Direkte Antworten: beide Reihenfolgen A→409/B→503; ältere 200 überschreibt B nicht; frühere Reload-Epoche löscht neuen Graph nicht. Vier Fälle bei 1007×601, 601×1007 und 390×844. |
| `revision-races-scanning.cjs` | Dieselben zwölf Fälle mit echten 202-Zwischenschritten im Browser-Testdouble und identischen Ticket-Abfragen. |
| `scan-polling.cjs --suite protocol` | 13 Fälle: State/Graph/Asset, unveränderte Query und Ticket-ID, ungültige Pending-Antworten, 410/400/503, Reload, 401-Anmeldung, Intervallbegrenzung. |
| `scan-polling.cjs --suite poll-limit` | Ein Fall bei 1007×601: Startanfrage plus höchstens 150 Polls, ohne Ersatzscan. |
| `scan-polling.cjs --suite time-limit` | Ein Fall bei 1007×601: tatsächliches 40-Sekunden-Budget bei wiederholten 202-Antworten. |
| `scan-network-timeout.cjs --suite time-limit` | Ein Fall bei 1007×601: ein absichtlich unbeantworteter Poll wird nach dem tatsächlichen Zeitbudget per Fetch-Abbruch beendet. |

Für die letzten drei Aufrufe gelten dieselben vier Pfadargumente; pro Suite ein eigenes `--out` verwenden. Diese Zeitgrenzen werden einmal geprüft, nicht für jede Bildschirmgröße. `--phase red` ändert ausschließlich die Kennzeichnung des Berichts: Assertions bleiben aktiv und ein fehlgeschlagener Lauf liefert Exitcode 1.

## Tatsächlich ausgeführte Referenzläufe

Am 24.11.2026 liefen die Evidence-Kopien mit vorhandenem Playwright Core 1.62.1 und Chromium 151.0.7922.34. Beispiel eines tatsächlich ausgeführten Aufrufs:

```sh
/usr/bin/node /var/lib/unityarpg/.local/share/are-arpg/art-forge-review-20261124/browser-race/scan-polling.cjs \
  --html /var/lib/unityarpg/.local/share/are-arpg/art-forge-review-20261124/browser-race/candidate-polling.html \
  --out /var/lib/unityarpg/.local/share/are-arpg/art-forge-review-20261124/browser-race/poll-green \
  --phase green --suite protocol
```

Die damals ausgeführten Originalkopien enthielten die bereits bekannten lokalen Dependency-Pfade als Defaults. Die dauerhaften Kopien hier verlangen diese Pfade explizit. Ausschließlich diese Argumentprüfung und zwei Default-Ausdrücke wurden geändert; die Prüf- und Assertion-Logik blieb bytegleich. Die Originalbytes und Originalberichte bleiben als Evidence erhalten. Die dauerhaften Kopien wurden zusätzlich auf JavaScript-Syntax und fehlende Pflichtargumente geprüft; die langen Browserläufe wurden dafür nicht wiederholt.

| Stufe | HTML-SHA-256 | Ergebnis |
| --- | --- | --- |
| Revisions-Race RED | `a6ffc33d30e0f2452978ab881bc9f785c412ad8658e1ba1f21c71a5711be503c` | Sechs erwartete Fehler; sechs Kontrollfälle bestanden. |
| Revisions-Race GREEN / Polling RED | `71d8d18effbf4c14c7a346312bde24f67e94ad1e86ce09822faef01da547834d` | Race 12/12 bestanden; Polling zehn erwartete Fehler und drei bestandene Negativkontrollen. |
| Polling und Revisions-Race GREEN | `ffa8c1c6fbb51e51af3f8767af373b2aa6995e8a61d4f8a215787fe9127199af` | 13 Protokollfälle, zwölf Races und drei einzelne Budget-/Netzabbruchfälle bestanden. |

Gemessen wurden 151 Requests bis zur Pollgrenze, 40.007 ms bis zur Zeitgrenze und 40.011 ms bis zum Abbruch des unbeantworteten Polls. Das sind Beobachtungen der isolierten Browserprüfungen und keine Leistungswerte eines Unity-Editors.
