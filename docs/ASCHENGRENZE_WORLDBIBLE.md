# Die Aschengrenze | World Bible v1

**Freigabe:** Der Owner hat am 10.11.2026 die erste Region **Die Aschengrenze** gewaehlt.
**Status:** World-Design-Vertrag und erster spielbarer Unity-6-Projektionsblock; keine neue Core-/Quest-Authority.
**Spiel:** ARE Ashen Crypt (Unity 6000.6.8f1, 100-ms-Tick, offline Singleplayer in diesem Vertical Slice).

## Prämisse

Die Aschengrenze ist der letzte bewohnte Grenzposten an der Aschestrasse. Hinter dem Nordbogen beginnt der versiegelte Weg zur Ashen Crypt. Seit einer alten Katastrophe steigt gelegentlich feine, warme Asche aus Fugen des Gesteins. Ein gebrochener Wachstein soll den Uebergang zur Glutklamm markiert haben. Niemand weiss, ob die Siegel die Menschen vor etwas schuetzen oder etwas vor den Menschen.

**Konflikt:** Die Siedlung braucht Reisende und Vorräte, misstraut aber allem, was aus der Krypta zurueckkehrt. Hilfe, Bedrohung und Raub beeinflussen schon heute die *direkt betroffenen* NPCs. Zeugen, Geruechte, Herrschaft und Fraktionen sind spaetere Erweiterungen gem. Issues #2–#6, nicht bereits spielbare Weltwahrheiten.

## Erste Spielgeschichte: Drei Zeugen, ein Fremder

- `story.aschengrenze.witnesses.v1` ist ein **Narrativbogen**, derzeit **kein persistierbarer Quest-State**.
- **Ilyan, der Pfadleser (`NpcId=0`, cautious adventurer):** kennt Risse im Nordweg, meidet unnoetige Kaempfe. Sein Misstrauen ist persoenlich.
- **Mara, die Versorgerin (`NpcId=1`, peaceful provider):** haelt das Lager mit knappem Proviant zusammen. Eine tatsaechlich akzeptierte Hilfe veraendert Ressourcen und ihre Beziehung im bestehenden Core.
- **Dorn, die Wache (`NpcId=2`, brave guard):** sichert den Zugang. Direkte Uebergriffe wirken auf sein persoenliches Gedaechtnis; er erhaelt noch keine telepathischen Meldungen ueber andere NPCs.
- Die drei erhalten **nur Anzeigenamen** in Unity. Die vorhandenen Core-IDs, Persoenlichkeiten, `NpcMemory` und `SocialLedger` bleiben kanonisch.

## Geometrische Anker (Unity-Meter, X/Z)

| Ort / stabile Platzierungskennung | Anker | Vorhandene Autoritaet / Wirkung |
| --- | --- | --- |
| `region.aschengrenze.v1` | Kapitel 1 | Bekannte Krypta-Kapitelprojektion; kein neues Gebiet im Core |
| `poi.aschengrenze.market` | (-6.8, -10.5) | Steinplatz/Staende, nur Grafik |
| `poi.aschengrenze.supply` | (-12.5, -8.7) | Versorgungshaus; Core-Camp bleibt bei (-4.5, -6.0) |
| `poi.aschengrenze.watch` | (-14.5, -1.8) | Wachturm und Signalfeuer, nur Grafik |
| `poi.aschengrenze.approach` | (-2.5, -7.7) | Begehbare optische Strasse, **keine Collider-Authority** |
| `poi.aschengrenze.north` | (0, 24) | **Bestehender** `ArenaSimulation.PassageXmm/Zmm` mit vorhandenem Freischalt-/Beutegate |

Die neuen Strukturen werden durch `ArenaRuntime.ChapterShape()` erzeugt, im `_chapterScenery` registriert und beim Kapitelwechsel entfernt. Keine Unity-Kollision entscheidet NPC-Interaktion, Kampf, Wegfreigabe oder Save. Alle Positionen sind Projektionsdaten und duerfen die kanonische Physik nicht stillschweigend aendern.

## Spielbare Abfolge dieses Slices

1. Einstieg in Kapitel 1: sichtbarer Aschengrenze-Siedlungsplatz und Hinweis auf Lager/Wache/Nordpass.
2. Spieler kann sich wie bisher per WASD/Touch bewegen und den versetzten Markt/Versorger/Wachturm erkennen.
3. Bei persoenlicher NPC-Naehe erscheinen dieselben vier vorhandenen Befehle: Gruessen, Versorgen, Angreifen, Ausrauben. Akzeptierte Taten folgen unveraendert `ArenaSimulation.InteractNpc` und AC5-Save.
4. Bestehende Gegner und Beute bleiben spielbar; nach Sieg und aufgesammelter Beute bleibt nur der vorhandene Nordbogen der echte Gebietswechsel.
5. Kapitel 2 / Glutklamm behaelt sein altes Aussehen und seinen Boss; kein neuer Weltzustand.

## Grenzen und Folgearbeit

**Jetzt implementiert:** Sichtbare Landmarken und lokale Figurennamen; bestehendes Gameplay bleibt vorhanden. **Nicht implementiert:** Dialogbaum, Questabschluss/Belohnungen, AC6-SocialEvents, Zeugenschaft/Geruechte, Fraktionen, Handel, KI-Autoritaet oder dynamischer Besitzwechsel. Die ersten fuenf sozialen Ausbauschritte folgen den offenen Issues #2–#6, beginnend mit kanonischen Events/AC6. Keine Doppel-Authority.

**Art-Upgrade:** Die grobe geometrische Struktur ist ein vermessbarer Szenenanker, kein behauptetes Final-Art. Nach privaten, lizenzierten Import- und Android-Budgetchecks kommen die eigenen Unity-Store-Siedlungs-/Natur-/Marktpakete zum Einsatz; keine urheberrechtlich gesperrten Assets im oeffentlichen GitHub. Individuelle Material-/LOD-/Animations-Abnahme am echten GameView und Spaetergeraet steht aus.

**Abnahmeschritte:** Unity-PlayMode `AschengrenzeArrivalTests` gegen den echten `ArenaRuntime`; vorhandene Core-Tests und Preflights; dann gerenderte Ansicht, neuer Android-Build und physischer Touch-/Save-Readback, bevor das Feature als releasefaehig gilt.

## Spielblock `story.aschengrenze.supplies.v1` — Die gestohlenen Vorräte

**Ausgangspunkt / bekannte Fakten:** Maras Lager enthält auf **neu angelegten** AC5-Social-Profilen acht von zehn Rationen. Zwei fehlen; der Titel ist eine Anschuldigung, keine bewiesene Tat. Frühere, gültige AC5-Spielstände behalten ihren tatsächlich gespeicherten Lagerbestand (0–10). Eine alte Lagerfüllung wird weder überschrieben noch um zwei Rationen beraubt. Als neue Figur kann Ilyan nachweislich hungrig sein. Nur die alten sozialen Kerneigentümer bestimmen Ressourcen und Beziehungen.

**Spielerentscheidungen (bereits spielbar in der vorhandenen Unity-6-Runtime):**

- `supplies.help_ilyan`: Ilyan aufsuchen; per bestehendem `NpcAction.Help` Proviant verbrauchen, seinen tatsächlichen Bedarf reduzieren und seine direkte Dankbarkeit in `NpcMemory`/AC5 fortschreiben. Mara erhält keine telepathische Reputation.
- `supplies.donate_mara`: Mara persönlich aufsuchen und per **G / RATION AN MARA** (Touch-Button oder Tastatur G) genau eine wirklich vorhandene Spieler-Ration in das bestehende Lager geben. Die Aktion prüft Region 1, Lebenszustand, Entfernung, 10-Tick-Interaktionscooldown, eigenen Proviant und Lagergrenze 10; nur bei tatsächlicher Übertragung schreibt derselbe `SocialLedger` einen einzigen direkten Help-Gedächtniseintrag für Mara. Die Änderung wird durch bestehenden PlayerPrefs-Primär-/Backup-/Readback-Pfad gespeichert.
- `supplies.accuse_by_force`: Den vermeintlich Verdächtigen Ilyan mit der bestehenden Attacke bedrohen; echter Schaden und negative, persistente eigene Erinnerung. Das Lager erhält **keine** Ration, und kein anderes NPC-Wissen wird erfunden.

**Spielbarer Abschluss dieses Teilabschnitts:** Den Lagerfehlbestand durch bis zu zwei echte Rationen auf null bringen, oder sich anders entscheiden und die Folgen der vorhandenen Interaktionen erfahren. Das ist **kein** behaupteter abgeschlossener investigativer Questbaum, keine juristische Schuldprüfung und keine einmalige einzigartige Questbelohnung. Weitere Geheimnisse, Zeugenketten, Dialog-/Untersuchungsentscheidung und Fraktionen bleiben auf die bestehenden Issues #2–#6 gebunden: kanonische SocialEvents/AC6 zuerst, danach tatsächliche Wahrnehmung, NPC-Policies und Wirkungen. Nichts davon wird als bereits im Spiel vorhanden ausgegeben.

**Visuelle Flächen:** Ein leeres Lagerregal (`Aschengrenze_Missing_Rations`) im vorhandenen Dorf; ausschließlich kollisionsfreie Unity-Renderprojektion, keine eigene Interaktions- oder Physikautorität. Im UI ein tatsächlicher AC5-Lagerstand mit Fehlbestand, dazu die Spieleraktion. Eindeutige Save-/Replay-Kriterien stehen in `SupplyTheftTests`, der echte Szene-Smoketest in `SupplyDisputeArrivalTests`.
