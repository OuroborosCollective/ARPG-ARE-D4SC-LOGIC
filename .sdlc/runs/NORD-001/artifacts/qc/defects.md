---
run_id: "NORD-001"
task_id: "QC-001"
producer: qc
status: reviewed
revision: 1
---

# Befunde und unabhängige Wiederholungen

Alle XML- und Bildnamen beziehen sich auf `docs/evidence/nordwechsel-20261126/`.

## DEF-NORD-001 · Kaltstart mit unterschiedlichen Profilen

| Feld | Wert |
| --- | --- |
| severity / priority | major / high |
| affected_requirements | REQ-003 |
| affected_acceptance_criteria | AC-003, AC-005 |
| target / owner_role | Unity-Kaltstartprüfung / frontend (mobile) |
| Status / retest_result | closed / passed |
| Reparatur | RPR-001 |

Auf Produkt `4300ee52` bestand `NordwechselCommitsChapterTwoBeforeSwitchAndRestoresItInAshenEntry` den ersten Vergleich nach dem Neuaufbau nicht. Von zwölf Fällen bestand dieser eine nicht. Die Teststation war mit Seed 71 und älterem Beutekatalog erzeugt, der echte Einstieg neu mit dem Spielseed und V2. Der Forge-Bestand erhielt drei unterschiedliche Identitäten. Das originale NUnit-XML nennt den erwarteten Fortschrittshash 12850526453339916025 und den gelesenen 17023350171700666581. Die Reproduktion besteht aus Speichern der vorbereiteten Station, Reise und Neuaufbau der ArenaRuntime.

Der reparierte Test lädt die Station vor dem Wechsel bereits über AshenEntry aus PlayerPrefs, reist durch den regulären Bogen und wiederholt den tatsächlichen Start im Ziel. Erwartet werden derselbe Fortschrittshash und die vollständig identischen Fortschrittsbytes. QC prüfte den bestandenen Fall im finalen Original-XML, die Vergleiche im Testcode und die zwei unabhängigen Core-Hashläufe.

| Original | SHA-256 |
| --- | --- |
| `unity-4300ee52.xml` · erster Hashbefund | `8d6c912d150976bb64470915282270725f058a8648429804e2d9e44946b630b9` |
| `unity-4d7fe150.xml` · 13/13 nach erster Reparatur | `515ef826ed9cad0da5d2d316db550ae14fbcc3375ef8d9ec5c3f59525bea5588` |
| `unity-72510b0c.xml` · abschließender Retest | `1d3492733c681f9ff1dcdc36f4c94ab76b380db0f5e3cf72fe1df9434cd07821` |

## DEF-NORD-002 · Fehlende Schieferoberfläche im öffentlichen Kapitel

| Feld | Wert |
| --- | --- |
| severity / priority | major / high |
| affected_requirements | REQ-004 |
| affected_acceptance_criteria | AC-006 |
| target / owner_role | Unity-Kapitelprojektion / frontend (mobile) |
| Status / retest_result | closed / passed |
| Reparatur | RPR-002 |

Auf der öffentlichen Revision `4d7fe150` die Station über den Bogen verlassen und die Kamera entlang des Stegs ausrichten. In der Aufnahme erschienen Steg, Boden und Säulen als fast einfarbige Flächen, obwohl alle 13 damaligen Unity-Fälle bestanden. Die materiellen Oberflächenüberlagerungen waren in dieser Projektkopie nicht verfügbar.

Der Retest auf `72510b0c` prüfte vorhandene regionale Materialien bzw. das ausgelieferte RoughSlate-Material mit Maske für Boden (5,4 m), Weg (1,65 m) und Säule (2,8 m). Alle drei getrennten Fälle bestanden. QC öffnete die beiden Originalaufnahmen und sah unter vergleichbarer Stegperspektive die strukturierte Schieferoberfläche auf Boden und Säulen. Das dritte Bild zeigt den 4-m-Ring auf dem Ritualpodest.

| Original | SHA-256 |
| --- | --- |
| `glutklamm-causeway-4d7fe150.png` · Flächen vor der Reparatur | `33c83497624c39b4aebabd2ea07ae02f074201ae03b4c7be70d58b8c006e7e4a` |
| `glutklamm-causeway-72510b0c.png` · Schiefer im Spiel | `581c3f691108e4b0cca0ba1eb0b4882b9e3767fafeba13f73743cc414df1bd77` |
| `unity-72510b0c.xml` · drei zusätzliche Materialfälle und bestehende Szenenfälle | `1d3492733c681f9ff1dcdc36f4c94ab76b380db0f5e3cf72fe1df9434cd07821` |

Der unabhängige ältere Dorfhallen-Befund aus der Kontrollgruppe gehört zum privaten Modellbestand und liegt außerhalb dieser Lieferung. Der Nordwechsel hat keine offenen Befunde.
