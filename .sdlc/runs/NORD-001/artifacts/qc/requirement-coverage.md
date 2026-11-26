---
run_id: "NORD-001"
task_id: "QC-001"
producer: qc
status: reviewed
revision: 1
---

# Abdeckung der genehmigten Kriterien

| Anforderung | Kriterium | Ziel | Fall | Ergebnis | Befund | Beleg |
| --- | --- | --- | --- | --- | --- | --- |
| REQ-001 | AC-001 | Station und Kapitel 2 | TC-001 | passed | – | Core-Record `EVD-3dbba544d962f987`, Unity-Fälle zu Nordwechsel und beiden Frachtwegen |
| REQ-002 | AC-002 | Reisevoraussetzungen | TC-002 | passed | – | Gleicher Record, getrennte Gegenproben für Sieg, Meldung, Ausrüstung und Vorräte |
| REQ-003 | AC-003 | PlayerPrefs und Kaltstart | TC-003 | passed | DEF-NORD-001 geschlossen | `unity-72510b0c.xml`, erfolgreicher Nordwechsel samt neuer AshenEntry-Laufzeit |
| REQ-003 | AC-004 | Abgewiesener Checkpoint | TC-004 | passed | – | `unity-72510b0c.xml`, `NordwechselReadbackFailureKeepsTheStationWorldActive` |
| REQ-003 | AC-005 | Kanonischer Core-Hash | TC-005 | passed | DEF-NORD-001 geschlossen | `EVD-3dbba544d962f987/stdout.txt`, Zeile `NORDWECHSEL_HASH` sowie vollständige Quellenprüfung |
| REQ-004 | AC-006 | Darstellung und Kamera | TC-006 | passed | DEF-NORD-002 geschlossen | `unity-72510b0c.xml`, beide Kameraprofile, Volume, Bogensicht, drei Basaltfälle und drei Spielkamera-PNG |
| REQ-005 | AC-007 | Ember Warden, Beute und Folgetiefe | TC-007 | passed | – | `EVD-3dbba544d962f987` für Takt und Folgetiefe, Unity für sichtbaren Warnkreis und den Treffer |
| REQ-006 | AC-008 | Bestehender Kapitel- und Speicherpfad | TC-008 | passed | – | Ausgangsblobvergleich `0cf480c..72510b0` und `unity-72510b0c.xml` auf dem Ziel |

Die vollständigen Recordpfade, Originaldateien, Zeiten und Prüfsummen stehen in `execution-results.md`. Alle sechs Anforderungen und acht Kriterien sind abgedeckt. Gerätebildrate und persönliche Spielerabnahme sind separat offen.
