---
run_id: "NORD-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 1
---

# Edge cases

| Edge ID | Related REQ | Scenario | Expected behavior | Risk | Claim IDs |
| --- | --- | --- | --- | --- | --- |
| EDGE-001 | REQ-001 | Die Spielerin nutzt den Waldweg nach ihrem früheren Frachtbericht. | Die bisherige Waldreise funktioniert weiter. | Bestehende Reise überschrieben. | CLAIM-001 |
| EDGE-002 | REQ-002 | Nach dem Sieg liegt genau ein Drop am Boden. | Erst seine reguläre Aufnahme erfüllt die Beutebedingung. | Verlust des letzten Drops. | CLAIM-002 |
| EDGE-003 | REQ-003 | Neustart unmittelbar nach bestätigter Übernahme. | Der bestätigte Tiefenfortschritt lässt sich aus dem gespeicherten Zustand wiederherstellen. | Tiefenverlust oder gemeldeter Scheinerfolg. | CLAIM-003 |
| EDGE-004 | REQ-004 | Die Spielerin läuft vom Steg auf die Bossplattform. | Figur und bedrohter Bereich bleiben mit der Kampfkamera erkennbar. | Unlesbare Kampfsituation auf Mobilgeräten. | CLAIM-004 |
| EDGE-005 | REQ-005 | Die Spielerin verlässt den Wirkungsbereich vor Ablauf. | Die vorhandene Ausweichregel greift. | Unvermeidbarer oder zu früher Treffer. | CLAIM-005 |
| EDGE-006 | REQ-006 | Ein vorhandenes Modell deckt einen geforderten Szenenteil bereits ab. | Der bestehende Teil erscheint einmal und sein Ursprung wird benannt. | Doppelte Kosten und divergierende Inhalte. | CLAIM-006 |
