---
run_id: "NORD-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 1
---

# Validation rules

| Validation ID | Related REQ | Input | Valid behavior | Invalid behavior | Claim IDs |
| --- | --- | --- | --- | --- | --- |
| VAL-001 | REQ-001 | Nordbogen und Waldweg getrennt auswählen. | Das jeweils gewählte Ziel öffnet sich. | Eine Interaktion außerhalb der Passage verändert den Ort nicht. | CLAIM-001 |
| VAL-002 | REQ-002 | Siegstatus und liegende Beute. | Der vollständig erfüllte Zustand erlaubt den Eintritt. | Ein fehlender Sieg oder restliche Beute sperrt den Wechsel. | CLAIM-002 |
| VAL-003 | REQ-003 | Primärer Spielstand und Rückleseergebnis des vorhandenen Pfads. | Nach bestätigtem Schreiben und Lesen wird Kapitel 2 aktiv. | Bei fehlender Bestätigung bleibt die alte Welt aktiv. | CLAIM-003 |
| VAL-004 | REQ-004 | Aktives Gebiet sowie sichtbare Geometrie, Kamera und Volume. | Der Durchgang zum Boss bleibt ablesbar. | Ein fehlender Kapitel-Look oder ein unlesbarer Kampf wird als offener visueller Befund geführt. | CLAIM-004 |
| VAL-005 | REQ-005 | Reichweite sowie verbleibende Aufwärmticks des Warden. | Der Warden kündigt an und der Core löst den Impuls nach Ablauf aus. | Während der offenen Ankündigung entsteht kein vorzeitiger Impulsschaden. | CLAIM-005 |
| VAL-006 | REQ-006 | Vorhandener Basalt, Fissuren, Dais, Steg und bisherige Technik. | Geeignete Teile bleiben im regulären Aufbau eingebunden. | Eine doppelte Arena oder Bosslogik gilt als Integrationsfehler. | CLAIM-006 |
