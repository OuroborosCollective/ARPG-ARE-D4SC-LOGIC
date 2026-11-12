# MaulwurfRattler und Immergruen

## Kanon: Vorgabe des Schoepfers

`creature.maulwurfrattler` ist ein uraltes, maulwurfartiges Rattenwesen aus den tiefen **Aschenglutfällen** (`region.aschenglutfaelle`). Es ist blind, orientiert sich an Geruch und Geraeuschen und haelt sich normalerweise fern menschlicher Siedlungen. Ein freilebendes Tier ist ausserhalb seiner Heimat selten.

Ein Jungtier laesst sich durch regelmaessige Mahlzeiten auf einen bestimmten Geruch konditionieren. Beim ausgewachsenen Tier ist dieser Sinn so stark entwickelt, dass es selbst winzigen Spuren seines erlernten Futtergeruchs bis ans Ende der Welt nachsetzen kann. Denkbar sind regionale Gerueche oder der Geruch einer bestimmten Menschengruppe. **Welcher Geruch dieses Tier anzieht, ist noch nicht festgelegt.** Die Spielmechanik dieser Begegnung bildet nur lokale Wahrnehmung ab; weltweite Duftausbreitung und Verfolgung sind keine bereits implementierten Systeme.

Das **schwebende Koenigreich Immergruen** (`realm.immergruen`) liegt als gruene Oase mit Wiesen und traumhaft wirkendem Schloss hoch auf einem Berg ueber den Aschenglutfällen. Aus der Ferne scheinen Berg und Schloss zu schweben; es ist kein bestaetigtes fliegendes Land. Immergruen ist das aktivste und begehrteste Handelsreich dieser Welt: Jeder darf handeln, aber nur Wohlhabende oder vom Koenig Erlaubte duerfen dort wohnen. Nach Geschaeftsschluss werden auch hungernde alte Frauen und Kinder in die schmutzigen Aschenumlande hinausgeworfen und ihrem Schicksal ueberlassen.

Der Rattler in der verborgenen Krypta traegt **am Hals ein Brandmal, das exakt dem Wappen des Koenigs von Immergruen entspricht** (`immergruen.royal.brand`). Das verbindet das Tier mit Immergruen. Sein Auftauchen bei den Verletzten und die Moeglichkeit der Abrichtung begruenden den Verdacht auf gezielten Einsatz. **Abrichter, Auftraggeber, Motiv, ausgewaehlte Opfer und das Schicksal aller Vermissten bleiben offen.** Insbesondere ist das Wappen kein automatischer Beweis eines persoenlichen Befehls des Koenigs.

Die konkrete heraldische Zeichnung ist noch keine Owner-Entscheidung. Ein grafischer Prototyp darf als solcher erkennbar sein; Markenidentitaet und Untersuchungstext sind bereits eindeutig.

## Spielbarer Abschnitt

Nach der frischen Spur in der Vorkrypta kann der Spieler den inneren Riegel loesen. Der Norddurchgang fuehrt in das Rattlernest (Akt 7); der Suedweg bleibt zum Rueckzug offen. Der Rattler ist dieselbe Core-Kampfentitaet fuer Faehigkeiten, Schaden, Tod und Belohnung.

Normale tatsaechliche Schritte sind bis 14 m hoerbar. Schleichen reduziert die Bewegung von 620 auf 240 mm pro 100-ms-Tick und vermeidet diesen Schrittimpuls. Innerhalb von 2,8 m nimmt das Tier den Spieler trotzdem ueber Geruch wahr. Erfolgreiche Angriffe verraten die Position ebenfalls. Ohne neue Wahrnehmung verfolgt es sechs Sekunden lang die letzte Position, danach kehrt es zur Futterspur zurueck. Es besitzt keine Sichtabfrage und verfolgt nicht die aktuelle versteckte Spielerposition.

Ein Hieb kuendigt sich zehn Ticks lang an. Das Ziel wird beim Beginn eingefroren; wer den roten 2,6-m-Bereich verlaesst, entgeht den 280 Schadenspunkten. Vom Mittelpunkt braucht normales Gehen fuenf Ticks zum Verlassen des Kreises; Schleichen elf. Vor einem Hieb also in den schnellen Gang wechseln. 5200 Lebenspunkte, vorhandene Spielerfaehigkeiten und keine zweite Physik-/Animationsautoritaet. Die lokalen Zahlen sind balancierbare Kampfparameter, keine Reichweitenbegrenzung des beschriebenen weltweiten konditionierten Instinkts.

Nach dem Sieg laesst sich das Halsbrandmal aus der Naehe untersuchen. Erst ein persoenliches Gespraech informiert Ilyan. Keine anderen NPCs lernen die Enthuellung telepathisch. Der Spielbogen oeffnet die Spur nach Immergruen; das Koenigreich selbst ist noch kein begehbares Gebiet.

## Eigentum und Persistenz

`CharacterProgression.Rattler` ist Teil desselben Charaktersaves. AC12 wird erst beim Oeffnen des inneren Riegels aktiv. Unbenutzte AC11- und aeltere Profile bleiben bytegleich. AC12 bindet HP, Position, Angriffswarnung, Zielgedaechtnis, Belohnungsbeleg, Schleichen, Brandmal und Bericht. Der bestehende Krypta-Checkpoint traegt Spieler-HP, Fokus, Cooldowns und PRNG auch fuer Akt 7. Reise und Wiederladen vergeben keine zweite Tötungsbelohnung. Tod/Neustart setzt den Spielercheckpoint zurueck, nicht die Weltfakten.

**Core:** RattlerEncounter, ArpgSimulation, CombatRules. **Persistenz:** CharacterProgression, CryptInvestigation, HungerJourney. **Runtime-Projektion:** RattlerPresentation und bestehende Arena-/Journaladapter. **Effekt:** private passive `CryptRattler`-Grafik aus dem vorhandenen Asset **Creep Horror Creature**; keine Vendor-Skripte im Wahrheitspfad und keine Rohdateien im oeffentlichen Repository. **Tests:** Core-Verhaltenspruefungen und echte Unity-Adapter-/Artpruefungen; arrangierte Testzustände sind keine unbeaufsichtigten Spieler-Durchlaeufe.
