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

Jede derzeit droppbare angreifende Waffenart hat eine eigene Fähigkeit: **Schwertkunst, Dolchkunst, Axtkunst, Stabkunst, Bogenkunst**.

| Regel | Wert |
| --- | --- |
| Stufen | 1–99, klassische RuneScape-EP-Tabelle (Stufe 2: 83 EP, 10: 1.154, 50: 101.333, 99: 13.034.431), Obergrenze 200.000.000 EP |
| Training | nur wirksame Treffer mit angelegter Waffe dieser Art; 1 EP je angefangene 10 tatsächlich abgezogene LP (wie die Disziplinen) |
| Alle 5 Stufen | +1 frei verteilbarer Wertepunkt (Macht, Vitalität oder Entschlossenheit) und +2,5 % Schaden mit dieser Waffenart |
| Maximum je Waffenart | 19 Wertepunkte und +47,5 % Schaden auf Stufe 95–99 |
| Ohne Waffe, ohne Ziel, Fehlschlag | keine EP |

Wertepunkte aus Charakterstufen und aus Waffenkünsten werden zusammen angezeigt und verteilt; gespeichert werden die
aus Waffenkünsten gekauften Ränge getrennt, damit ältere Spielstände ihre bestehende Prüfung behalten. Der Schadensbonus
zählt additiv zu den übrigen Schadensanteilen (Basis 20 %, Macht, Waffenaffix, Disziplin) und gilt nur, solange eine
Waffe dieser Art angelegt ist.

Speicherung: AC21 umhüllt den vollständigen bisherigen Spielstand, sobald eine Waffenkunst Fortschritt hat; ohne
Fortschritt bleibt der Spielstand byte-gleich. Abgelehnt werden u. a. mehr verteilte Punkte als verdient, EP über der
Obergrenze, Fortschritt auf Waffenarten ohne Fähigkeit, nicht kanonische oder negative Zahlen, verschachtelte Hüllen und
Änderungen ohne gültige Prüfsumme.

Menü **FÄHIGKEITEN** (Taste P): Stufe, EP-Fortschritt und Boni je Waffenkunst; freie Punkte mit Herkunft und „+“-Knöpfen;
Wirkung jedes Werts; je Schadensart (Klinge, Wucht, Ruin) Gesamtbonus mit Aufschlüsselung, Kritchance 19 % für ×1,5
und der exakte Treffer gegen Ghul und Ritter mit und ohne Krit (dieselbe Formel wie im Kampf, ohne Zufall); Heilung von Schutz.

Offen: Schilde sitzen in der Nebenhand und greifen nie an; ihre Fähigkeit braucht eine eigene Regel (Owner-Entscheidung).

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
