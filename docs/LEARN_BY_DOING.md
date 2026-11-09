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
