# ARE Ashen Crypt — Fortschritt und Weltfluss

## Vom Owner freigegebene Richtung
Ein ARPG ohne Levelcap, mit Lernen durch ausgeführte Tätigkeiten und Gebietswechseln innerhalb der Welt. Der Spielfluss bleibt beim Charakter; Siege führen nicht zu einem Level-complete-Menü. Die vorhandene C#-Simulation, 100-ms-Ticks, Loot, Ausrüstung, Mastery und private Account-Assets bleiben die Grundlage.

## Implementierter erster Spielblock
| Stabile Tätigkeit / Slot | Tatsächlicher Auslöser | Fortschritt |
| --- | --- | --- |
| blade / 0 — Klingenführung | Wirksamer normaler Treffer | Eigene BigInteger-XP und unbegrenzter Rang; eigener Schadensbonus |
| impact / 1 — Wucht | Wirksamer schwerer Treffer | Unabhängige BigInteger-XP und Ränge; eigener Schadensbonus |
| warding / 2 — Schutz | Tatsächlich wiederhergestellte HP | Unabhängige BigInteger-XP und Ränge; Heilbonus |
| ruin / 3 — Ruinmagie | Wirksame Treffer der Flächenfähigkeit | Unabhängige BigInteger-XP und Ränge; eigener Schadensbonus |

Pro Treffer zählt nur tatsächlich abgezogene Gesundheit, nicht die überschüssige Schadenszahl. Fehlschläge, Cooldown-Ablehnungen und Heilung bei voller Gesundheit geben keine Übung. 1 XP pro angefangener wirksamer Kleinstaktion, sonst ganzzahlig Schaden/10 bzw. Heilung/10. Die Rangkurve verwendet die bestehende Formel 75*(Rang-1)*Rang; Rang2 ab150XP. Kein Ranglimit und kein saisonaler Reset. Die endlichen Kampfwerte skalieren in diesem ersten Balancingpass begrenzt (+50% additive Basis-Schadenspunkte je eingesetzter Disziplin, +200 Heilung); unbeschränkte Zähler bedeuten keine physikalisch unendlichen Trefferwerte.

Charakter-XP aus Kills und bestehende Mastery bleiben erhalten. Übungsränge ersetzen keine vorhandenen Spielstände. AC4 erweitert AC3 um vier kanonische BigInteger-Zähler, liest AC1/AC2/AC3 und übernimmt deren XP, Ausrüstung, Ränge und Gebietscheckpoint. Alte Saves erhalten keine erfundene Übung, sondern starten die vier neuen Zähler bei0. Speicherschlüssel, Backup und geprüfter Readback bleiben bestehen.

## Waffenkünste im RuneScape-Stil (Owner-Auftrag 17.11.2026)

Jede derzeit droppbare Waffenart hat eine eigene Fähigkeit: **Schwertkunst, Dolchkunst, Axtkunst, Stabkunst, Bogenkunst** und **Schildkunst**.

| Regel | Wert |
| --- | --- |
| Stufen | 1–99, klassische RuneScape-EP-Tabelle (Stufe 2: 83 EP, 10: 1.154, 50: 101.333, 99: 13.034.431), Obergrenze 200.000.000 EP |
| Training | nur wirksame Treffer mit angelegter Waffe dieser Art; 1 EP je volle 10 tatsächlich abgezogene LP, abgerundet, mindestens 1 EP je wirksamem Treffer (wie die Disziplinen; 19 LP geben 1 EP) |
| Alle 5 Stufen | +1 frei verteilbarer Wertepunkt (Macht, Vitalität oder Entschlossenheit) und +2,5 % Schaden mit dieser Waffenart |
| Maximum je Waffenart | 19 Wertepunkte und +47,5 % Schaden auf Stufe 95–99 |
| Ohne Waffe, ohne Ziel, Fehlschlag | keine EP |

Wertepunkte aus Charakterstufen und aus Waffenkünsten werden zusammen angezeigt und verteilt; gespeichert werden die
aus Waffenkünsten gekauften Ränge getrennt, damit ältere Spielstände ihre bestehende Prüfung behalten. Der Schadensbonus
zählt additiv zu den übrigen Schadensanteilen (Basis 20 %, Macht, Waffenaffix, Disziplin) und gilt nur, solange eine
Waffe dieser Art angelegt ist.

Speicherung: AC21 umhüllt den vollständigen bisherigen Spielstand, sobald eine Waffenkunst Fortschritt hat; ohne
Fortschritt bleibt der Spielstand byte-gleich. Abgelehnt werden u. a. mehr verteilte Punkte als verdient (auch wenn
riesige Ränge beim Addieren überlaufen würden), EP über der Obergrenze, Fortschritt auf Waffenarten ohne Fähigkeit,
nicht kanonische oder negative Zahlen, verschachtelte Hüllen und Änderungen ohne gültige Prüfsumme. Beim Laden gelten
die Waffenränge schon vor jeder Gesundheitsprüfung des inneren Spielstands, damit ein mit Waffen-Vitalität geheilter
Charakter gültig bleibt. Ein Waffen- oder Schild-Stufenaufstieg wird sofort gespeichert, auch ohne Kill. Ein AC21-Stand
um einen Reise-Checkpoint gilt bei der Wiederherstellung weiter als Reise-Checkpoint: Er fällt nie auf ein älteres
Backup ohne Reise zurück.

Menü **FÄHIGKEITEN** (Taste P): nur die sechs Waffenkünste mit Stufe, EP-Fortschritt und Boni.

Menü **WERTE** (Taste U, Owner-Entscheidung 18.11.2026): kompaktes Werteblatt mit kurzen Zeilen, Erklärung erst beim
Antippen eines Werts in einer Detailzeile. Links freie Punkte, Macht, Vitalität und Entschlossenheit mit „+“-Knöpfen,
Leben, Heilung (Schutz) und Bewegung. Rechts „KAMPF“ mit der angelegten Waffenart: DPS (Klinge im Mittel gegen einen
Ghul, mit Krit und beim Bogen mit Fehlschüssen), Klinge, Wucht und Ruin als „normal / kritisch“ gegen einen Ghul, Krit
(z. B. 19 % ×1,5), Treffer, Tempo (Schläge/s), Reichweite, Block und beim Stab der Magiefokus. Das Antippen zeigt z. B.
die Anteile einer Schadensart, den Ritter-Treffer und die Rüstungsregel: Rüstung/(1 + Rüstung) wird vom Treffer
abgezogen, Ghul −9,1 %, Ritter −18 % (Rüstungswerte 1000 und 2200 bp). Alle Zahlen kommen aus denselben Core-Regeln wie
im Kampf (ohne Zufall); das Antippen ändert die Simulation nie.

### Waffenarten und Schild (Owner-Entscheidungen 17.11.2026)

Jede Waffenart hat ihren eigenen Charakter (`WeaponProfiles`, Revision 1). Tempo und Reichweite kommen aus dem
Waffenkatalog, das Profil gewichtet den Grundschaden und legt Krit, Treffsicherheit und Bewegung fest:

| Waffenart | Abklingzeit | Reichweite | Schadensgewicht | Krit | Treffsicherheit | Bewegung |
| --- | --- | --- | --- | --- | --- | --- |
| Dolch | 2 Ticks | 2,3 m | ×0,68 | 35 % | 100 % | 100 % |
| Einhandschwert | 3 Ticks | 3,1 m | ×1,00 | 19 % | 100 % | 98 % |
| Einhandaxt | 4 Ticks | 3,0 m | ×1,06 | 15 % | 100 % | 96 % |
| Zweihandschwert | 4 Ticks | 4,1 m | ×1,06 | 16 % | 100 % | 94 % |
| Zweihandaxt | 5 Ticks | 3,8 m | ×1,33 | 12 % | 100 % | 90 % |
| Bogen (zweihändig) | 6 Ticks | 12 m | ×1,05 | 22 % | 60 % + 1,25 % je Bogenstufe (100 % ab Stufe 32) | 98 % |
| Stab (zweihändig, Magiefokus) | 7 Ticks | 8,5–9 m | ×1,10 | 15 % | 100 % | 97 % |
| ohne Waffe | Fertigkeit | Fertigkeit | Grundwert 95 | 19 % | 100 % | 100 % |

Schaden pro Sekunde im Mittel (mit Krit, gegen einen Ghul) ordnet sich so: Dolch < Bogen (treffsicher) < Einhandschwert
< Einhandaxt < Zweihandschwert < Zweihandaxt. Ein Bogen niedriger Stufe verliert durch Fehlschüsse deutlich Schaden.
Fehlschüsse treffen nur Klinge und Wucht mit dem Bogen, verursachen nichts und trainieren nichts.

**Stab als Magiefokus (Owner-Entscheidung 18.11.2026):** Mit angelegtem Stab bekommt Ruin, der Zauber, +25 %
zusätzlichen Schadensanteil (additiv wie Macht, Disziplin und Waffenkunst). Klinge und Wucht bleiben unverändert; der
Stab verfehlt nie. Das Menü WERTE zeigt ihn als „Fokus: Ruin +25 %“ und in den Anteilen von Ruin.

**Schildkunst:** Der Schild sitzt in der Nebenhand und greift nie an. Jeder erlittene Angriff mit angelegtem Schild
trainiert ihn (1 EP je volle 10 Angriffsschaden, mindestens 1, auch bei Block). Je Stufe +0,25 % Chance, einen Angriff komplett
zu blocken (0 Schaden; Stufe 99: 24,75 %); dazu die bisherige Schadensminderung des Schildes. Alle 5 Stufen +1 freier
Wertepunkt wie bei den Waffen. Mit angelegtem Schild lassen sich nur Einhandwaffen anlegen (eine Zweihandwaffe wird
abgelehnt, bis der Schild abgelegt ist), und der Charakter bewegt sich 12 Prozentpunkte langsamer.

## Begehbare Gebietswechsel
Der Nordbogen steht bei (0,24000) Millimetern. Nach Sieg und Beutesicherung wird die Bodenmarkierung sichtbar. Der Charakter läuft in den Radius von1800mm; der Core prüft den Übertritt. Der Unity-Adapter speichert und liest den neuen Checkpoint zurück, bevor er die Welt wechselt. Gesundheit, Fokus, Cooldowns, Charakterfortschritt und gelernte Disziplinen reisen mit. Eintritt ins Folgegebiet am südlichen Rand bei z=-23000mm. Kurze Überblendung statt Abschlussfenster; keine Next-Level-Schaltfläche und keine N-Taste als Fernteleport.

Dies ist ein Gebietstausch innerhalb des vorhandenen Arena-Systems, kein bereits implementiertes Open-World-Streaming. Der vorhandene Core bietet derzeit zwei Gebietsgrundtypen; unbeschränkte Tiefe ist noch keine unbeschränkte handgefertigte Inhaltsvielfalt.

## Nächste Entwicklungsblöcke
1. Gerenderte Spielansicht und Lauf-/Kampfanimationen der Account-Modelle prüfen; Telegraphen und Begehbarkeit sichtbar abstimmen.
2. Erkundungs-, Sammel- und Handwerkstätigkeiten mit echten Ressourcen-/Rezeptaktionen ergänzen; keine unbenutzten XP-Zähler ohne Gameplay.
3. Inventar- und Skill-Werkzeuge als Adapter zum kanonischen Fortschritt und Speicher integrieren, anschließend Gegenstandsvielfalt und regionale Belohnungen.
4. Regionale Begegnungen, Abkürzungen und optionale Risiken ausbauen. NPCs/Quests erst mit vorhandenen oder ausdrücklich gewählten Lore-Verträgen.

## Entscheidungen mit dem Owner
Die Richtung ist freigegeben; prägende Inhalte werden gemeinsam konkretisiert. Für die nächste Weltgestaltung stehen z.B. eine zusammenhängende düstere Grenzregion mit mehreren begehbaren Biomen oder eine größere Oberwelt mit instanzierten Tiefengebieten zur Wahl. Beide sind Vorschläge, keine schon gebauten Welten. NPC-Kultur, zentrale Konflikte und endgültige Art Direction werden mit dem Owner entschieden. Routineimplementierung und bedarfsgerechte Installation seiner ausgewählten Pakete erfolgen ohne erneute Einzelpaketfrage.
