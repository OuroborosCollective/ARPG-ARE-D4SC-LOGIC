# Kampfgefühl, Beute und Himmel — Systementwurf (15.11.2026)

## Anlass und Anforderungen

Owner-Bericht vom Tablet und Wünsche:
1. **Angriffsanimationen fehlen:** Angriffe an den Startgegnern erzeugten Kampfsound, aber keine Animation.
2. **Trefferphysik:** Treffer sollen spürbar auf Gegner wirken.
3. **Level-up-Animation.**
4. **Partikeleffekte,** auch für legendäre Waffen.
5. **Echte Skybox** mit Tag-Nacht-Wechsel, Wolken und Sonne. Eine Aschenwelt muss nicht Dauernebel bedeuten.
6. **Beute zu großzügig:** Schon die ersten Gegner gaben gelbe und blaue Waffen.

Nicht-funktionale Grenzen:
- Deterministischer Integer-Core mit 100-ms-Tick, Unity projiziert nur.
- Android ARM64 auf einem Mittelklasse-Tablet, Effektbudget mit der Option „reduzierte Effekte“.
- Bestehende Spielstände bleiben gültig, keine neuen Assetkäufe.

## Architektur

```
 Core (deterministisch, 100 ms)               Unity-Darstellung (nur lesend)
 ──────────────────────────────────          ─────────────────────────────────────────
 ActivateSkill → SkillResult              ─▶ Aktion bei jeder ausgeführten Fertigkeit
   Activated, Hits, Kills,                     (Melee / Guard / Cast), Held dreht sich
   Ziel, getroffene Gegner                      zum Ziel, optischer Rückstoß, Trefferstocken
                                                am Gegner, Kamera-Ruck, Umkippen
 TotalExperience → Level                  ─▶ Level-up: Licht, Partikel, Banner, Ton
 LootItem (Katalog-Revision, Stufe)       ─▶ Seltenheitsfarbe und Waffen-Aura
 Weltuhr (persistiert, Ticks)             ─▶ Skybox, Sonne/Mond, Wolken, Licht, Nebel
```

Kein Darstellungscode schreibt Core-Zustand oder verzögert einen Tick. Rückstoß und Umkippen bewegen nur Darstellungsobjekte und federn auf die Core-Position zurück.

## Block 1 — Kampfgefühl (umgesetzt)

- **Ursache des Fehlers:** Schlag und Schwerer Schlag animierten nur bei Treffer. Schutz und Ruinensturm hatten nie eine Heldenaktion, der Held schaute nicht zum Ziel, Gegner reagierten nicht.
- **Neue Posen:** Es gibt im Bestand keine Clips für Zauber, Abwehr oder Treffer. Guard und Cast werden aus der vorhandenen Kampf-Idle-Haltung als Muskel-Versatz erzeugt. Damit sind sie skelettgerecht und ohne Neukauf.
- **Core-Erweiterung:** `SkillResult` meldet jetzt Ziel und getroffene Gegner. Das ist reine Ergebnisinformation.
- **Abwägungen:**
  - Der Rückstoß ist bewusst optisch (Owner-Entscheidung A), damit Balance und Spielstände unverändert bleiben.
  - Das Trefferstocken gilt nur für den Gegner. Der Core löst Treffer beim Auslösen auf, ein eingefrorener Held würde wie Verzögerung wirken.

## Block 2 — Beute v2 (umgesetzt am 23.11.2026, Owner-Entscheidungen A und C)

Abnahme: `docs/evidence/loot-v2-20261123/acceptance.md`. Der Entwurf von damals:


- **Neue Katalog-Revision 2:** Stufen Normal (weiß), Magisch (blau), Selten (gelb) und Legendär (orange).
  - Es gibt eine Drop-Chance, nicht jeder Gegner lässt etwas fallen.
  - Die Gewichte steigen mit Tiefe, Gebiet und Bossen.
  - Legendär gibt es nur von Bossen und aus der tiefen Höhle.
- **Spielstände:** Gespeicherte Revision-1-Belege bleiben ladbar, weil das Laden sie mit Revision 1 nachwürfelt. Neue Drops tragen Revision 2.
- **Abhängigkeiten:** Betroffen sind Kill-Pfad, Journey/Höhle/Wald, Ausrüstungsabbildung, UI-Farben und die Core-Prüfungen, die bisher einen Drop pro Kill annehmen. Diese werden auf den neuen Vertrag umgeschrieben, nicht abgeschwächt.
- **Optik:** Legendäre Waffen bekommen eine Aura mit Partikeln, im Bodenzustand und ausgerüstet.

## Block 3 — Himmel und Tageszeit (Owner-Entscheidung A: nur Optik)

- **Weltuhr:** Eine persistierte Core-Weltuhr in Ticks. Ein Spieltag dauert etwa 24 Minuten, ohne Regelwirkung.
- **Skybox:** prozedural, mit Sonnenstand, Wolkenschicht, Abendrot und Sternennacht.
- **Licht und Nebel:** Sonnenlicht, Umgebungslicht und Nebel werden aus der Tageszeit abgeleitet.
- **Nebel:** Statt dauerhaft dichtem Nebel nur noch Dunst am Horizont. Innenräume und Krypten behalten ihre eigene Stimmung.
- **Fallback:** Bei reduzierten Effekten eine einfache Farbverlaufs-Skybox.

## Später neu bewerten

- Echter (Core-)Rückstoß für schwere Fertigkeiten, falls die Kämpfe taktischer werden sollen.
- Gekaufte oder aufgenommene Clips für Treffer, Tod und Zauber anstelle der erzeugten Posen.
- Spielfolgen der Nacht (Schlaf, Händler, stärkere Gegner) erst nach eigener Owner-Entscheidung.
