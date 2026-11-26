---
run_id: "NORD-001"
task_id: "BA-001"
producer: ba
status: draft
revision: 1
---

# Acceptance criteria

## AC-001

requirement_id: REQ-001
claim_ids: [CLAIM-001]

Given die Feldheilerin steht in Aschenzoll vor dem freigegebenen Nordbogen
When sie nutzt die normale Passage am Bogen
Then das Spiel lädt die Glutklamm mit Tiefe 2. Die bisherige Waldwegpassage ist weiter ein eigenständiges Ziel

evidence_expected: Gebiets-ID, Tiefenwert und Wegeauswahl im tatsächlichen Interaktionspfad.

## AC-002

requirement_id: REQ-002
claim_ids: [CLAIM-002]

Given ein vorausgesetzter Sieg steht aus oder es liegt noch eine Beute aus dem Kampf am Boden
When die Spielerin versucht den Übergang
Then sie bleibt im Ausgangsgebiet. Nach Sieg und letzter Aufnahme kann sie am Nordbogen weiterreisen

evidence_expected: Negative Gegenproben für Sieg und Restbeute sowie positiver Übergang mit identischer Spielfigur.

## AC-003

requirement_id: REQ-003
claim_ids: [CLAIM-003]

Given der Zugang ist freigegeben und ein gültiger Spielstand vorhanden
When die Spielerin wechselt durch den Bogen und öffnet anschließend das Spiel neu
Then das Spiel übernimmt die Zielwelt erst nach geprüftem Checkpoint. Erneutes Laden ergibt Kapitel 2 und den gesicherten Tiefenwert

evidence_expected: Revisionsgebundener Lauf mit tatsächlichem Save-/Leseweg und anschließendem Neustart oder Neuinitialisieren der Runtime.

## AC-004

requirement_id: REQ-003
claim_ids: [CLAIM-003]

Given der Checkpoint kann nicht verlässlich geschrieben oder zurückgelesen werden
When das Spiel versucht den Übergang
Then die bisherige Welt bleibt aktiv und die freigegebene Tiefe des laufenden Charakters wird nicht vorweggenommen

evidence_expected: Fehlerfall, angezeigte Welt und Vergleich von Ausgangsfortschritt und Hash.

## AC-005

requirement_id: REQ-003
claim_ids: [CLAIM-003]

Given zwei Läufe beginnen mit identischem kanonischen Ausgangsstand, Seed und Befehlen
When beide vollziehen denselben erfolgreichen Wechsel
Then die beiden Zielwelten liefern denselben Zielhash und dieser unterscheidet sich vom Ausgangshash. Der vorherige Weltbeleg bleibt unverändert

evidence_expected: Quell- und Zielhash aus beiden echten Core-Läufen sowie erneutes Auslesen der Ausgangswelt.

## AC-006

requirement_id: REQ-004
claim_ids: [CLAIM-004]

Given Kapitel 2 wurde über den normalen Weg geladen
When die Figur folgt dem Steg zum Ritualdais und nähert sich dem Warden
Then Basalt, Fissuren, Steg und Dais liegen im sichtbaren Spielraum. Kampfkamera und vorhandene URP-Infrastruktur vermitteln den Kapitellook. Die Paketliste enthält keine neue Engine-Abhängigkeit

evidence_expected: Unity-Szenen-/PlayMode-Beleg mit aktiver Kamera und Volume sowie Vergleich der Paketliste. Gerätebildrate nur mit tatsächlicher Messung angeben.

## AC-007

requirement_id: REQ-005
claim_ids: [CLAIM-005]

Given die Figur steht in Kampfreichweite des Ember Warden in Kapitel 2
When der Warden kündigt seinen bestehenden Feuerimpuls an und der Core schreitet in 100-ms-Schritten fort
Then der Impuls folgt nach acht Ticks. Treffer und Ausweichen folgen den vorhandenen Kampfregeln

evidence_expected: Tickfolge, Bosszustand und Treffer-/Ausweichvergleich in realer Simulation.

## AC-008

requirement_id: REQ-006
claim_ids: [CLAIM-006]

Given Ausgangsrevision und vorhandene Szenenbausteine wurden ermittelt
When der ergänzte Übergang und die Glutklamm werden überprüft
Then der Bericht ordnet Szenengeometrie, Kampf und Sicherung jeweils dem bestehenden oder ergänzten Pfad zu. Neu geschaffene Modelle oder Details schließen eine konkrete Lücke

evidence_expected: Revisionsvergleich und Inventar der tatsächlich verwendeten Bausteine.
