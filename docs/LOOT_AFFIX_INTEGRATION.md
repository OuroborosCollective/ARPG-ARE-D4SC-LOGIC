# Loot-/Affix-Slice v1

## Eigentümerschaft und sichtbares Ergebnis

`ArenaSimulation.DealDamage` bestätigt den Kill genau einmal. Der ausdrücklich gebundene `LootCatalog.Version1` erzeugt eine immutable `LootItem`-Instanz. Seed, Tiefe, Tick, Gegner-ID/-Art, persistente BigInteger-Killordinalzahl und Katalogrevision bilden die Identität. Der bestehende GroundLoot-/Pickup-Pfad bleibt alleiniger Owner; Unity zeigt ihn an und speichert über seinen bestehenden PlayerPrefs-Adapter.

Der Katalog enthält acht nach ordinaler ID sortierte Einträge für vier Gegnerarten. Nach Gegnerfilterung gelten Gewichte Magic 75 / Rare 25. Beide Seltenheiten tragen genau einen `weapon.upgrade`-Affix v1 mit einem festen Wert (degenerierter Wertebereich): Ghoul 8, Knight 17, Boss 34 Waffenpunkte. Seltenheit erzeugt in diesem ersten Kompatibilitätsslice keinen zusätzlichen Kampfbonus. Keine neue Ausrüstungs- oder Inventar-UI.

Der Loot-PRNG wird aus der vollständigen Killidentität und dem Kataloghash abgeleitet; er verändert den bestehenden Kampf-PRNG nicht. Rejection Sampling nutzt die oberen 32 Bit des vorhandenen PRNG. Für Schranke n wird unterhalb `2^32 - (2^32 mod n)` akzeptiert. Wolfram-Kernel prüfte die Teilbarkeit für n=3,7,100,10000,2147483647. Keine Wolfram-Runtime im Spiel.

## Save-/Hash-Vertrag

AC1–AC5 werden weiterhin gelesen; Profile ohne neue Itembelege behalten exakt AC5-Payload und Hash. AC6 bleibt der separaten Social-Ereignisarbeit vorbehalten. AC7 ergänzt an Feld 14 eine kanonisch nach Killordinalzahl sortierte Liste gesammelter Upgradebelege, danach die bestehende FNV-Prüfsumme. Ein Beleg speichert Revision, Seed, Tiefe, Tick, Gegneridentität, Killordinalzahl, Definition, Rarity, Affix und Wert. Der Loader regeneriert den Beleg aus dem versionierten Katalog und lehnt unbekannte IDs, manipulierte Werte, Duplikate und nichtkanonische Zahlen ab. FNV ist kein Manipulationsschutz.

**Checkpoint, kein vollständiger Kampfsnapshot:** Bestehende Saves speichern Charakter und Kapitel. HP, Gegnerzustände und ungesammelte Bodendrops werden beim Neustart weiterhin neu begonnen. Der Slice verspricht keine Wiederaufnahme eines laufenden Kampfes. Gesammelte Instanzen/Upgrades bleiben erhalten; erneute Kills erhalten durch LifetimeKills neue IDs. Derselbe Anfangscheckpoint plus dieselben Eingaben liefert denselben Replay. Ein Rollback alter AC5-Binaries kann AC7 nicht lesen; vorhandene Saves nicht überschreiben oder deinstallieren.

Alte Core-Konstruktoren bleiben ausdrücklich im Legacy-Modus. Unity bindet v1 am vorhandenen Awake-Entry-Point; Kapitelwechsel übernehmen denselben Katalog. Neue World-Hashes binden Kataloginhalt, Seed und Itembelege. Legacy-Goldenwerte gegen main ebef21e61246f4b72198c9f385c4858a6c6c3af4: Spawn 778990259245021324, Kill 12708209387939169364, Pickup 4613376337469592607.

## Speicher / Threading / Skalierung

Core läuft synchron auf dem bestehenden Simulationsthread. Immutable Definitionen kopieren/sortieren ihre Eingabe einmal. Keine Unity-Referenzen, Events, Tasks, nativen Allokationen oder Disposal-Pflichten im Loot-Modul. Itembelege wachsen mit gesammelten Gegenständen; Save und Hash sind linear zur Beleganzahl. Kein mobiles Langzeit-Performance-Budget behauptet. Eine spätere Kompaktierung benötigt eigenen versionierten Persistenzvertrag, sie darf IDs nicht stillschweigend verwerfen. Die XP-/Level- und Mastery-BigInteger-Regeln bleiben unverändert.

## Tests / Grenzen

TDD-Core RED e36bcae (fehlende Verträge), GREEN 803 Assertions; 21 Python-Preflights. Unity PlayMode RED auf 1b1b3b15691e1f4c7d5a38fd29df2578db6cef2d: 11/12, fehlende Client-Katalogbindung. GREEN auf 8277ba6d0a476431e7e6a22923ac2eb5012d1571: 12/12, kein Skip; XML SHA256 58cd4ed218d4e9ad64818113b05ed53ab12c0eb2e9d333a8ecf6162fb345d1d6. Derselbe Git-Tree 2024dd4487c7ad11bdbe0f6b470c461f78b1bbdc auf GitHub fe46c29df6febc934134d7c61ff2cc9f5c1f80cf. EditMode/CI siehe finalen Evidenzeintrag. Kein Android-/Tablet-/Grafikqualitätsnachweis aus diesen Tests.

## Folgeintegration

Skills, Combat, Stats und Learn-by-Doing müssen die bestehenden Skill-Slots, FixedMath-/Basispoint-Konventionen und RecordPractice-Regeln erweitern. NPC Memory/Policies gehören weiterhin NpcRelations/SocialLedger; der aktive AC6-Branch darf nicht durch einen zweiten Ereignis- oder Speicherowner ersetzt werden. PRs #10/#11 und die private Fantasy-Kingdom-Grafikarbeit bleiben getrennt.
