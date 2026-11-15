# Ashen Crypt — Projekt- und Verlaufshandoff (aktualisiert)

Stand: 15.11.2026, nach der Fortsetzungssitzung. Grundlage ist der Handoff vom selben Tag. Dieses Dokument ersetzt ihn: Überholte Aussagen sind korrigiert, neue Ergebnisse, Besitzerentscheidungen und offene Punkte sind ergänzt. Es trennt weiterhin vorhandenes Spiel, geprüfte Ergebnisse und geplante Arbeit.

## 1. Sofortüberblick für den nächsten Arbeitsbeginn

- Repository: https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC
- Unity-Pin: **6000.6.8f1**. Ziel: Android ARM64 / IL2CPP, insbesondere Samsung Galaxy Tab A9.
- Kanonische Regeln: **AreArpg.Core**, bestehender **100-ms-Tick**, ganzzahlige/deterministische Simulation. Keine zweite Gameplay-Authority.
- `main` unverändert auf **abc9eb01e8e654eb73e7656b9d5c00ff14a51190** (PR 43).
- **Vier offene, aufeinander aufbauende PRs** (alle `clean`). Merge-Reihenfolge strikt einhalten und die Basis des jeweils nächsten PRs nach dem Merge auf `main` umstellen:
  1. **#44** `integration/worldforge-pilot-20261115` → `main` (Wald-/Höhlenpilot, Wiederaufnahme MOB-001), Kopf `d2c6776`
  2. **#45** `fix/first-village-20261115` → #44 (erstes Dorf: Boden, beruhigende Dorfbewohner, Maras Lager als Nebenaufgabe), Kopf `705a0db`
  3. **#46** `feat/combat-feel-20261115` → #45 (Aktion für jede Fertigkeit, optischer Rückstoß, Umkippen, Level-up-Effekt), Kopf `643433a`
  4. **#47** `feat/village-arrival-20261115` → #46 (feste Ankunftsszene im Dorf), Kopf `8ca66ec`
- Aktueller lokaler Stand im nativen Projekt: Branch `feat/village-arrival-20261115` (= #47).
- Die Arbeitsauthority ist wieder aufgenommen: **MOB-001 = ready**, Blocker über `node .sdlc/runtime.cjs transition … --resolve-blocker` gelöst, nicht von Hand.
- Die zuletzt **ausgelieferte APK** ist weiterhin `Ashen-Crypt-2f296b2-20261115.apk` (Quelle `2f296b2`). **Keine** der Änderungen aus #44–#47 ist in einer APK enthalten. Für einen Tablet-Test ist ein neuer Build nötig.
- Höchstens **ein aktiver Unity-Haupteditor**, gemeinsames Unity-CPU-Budget **1 Kern** (`cpu.max = 100000 100000`). Der alte Intake-Editor (`are-arpg-asset-intake-unity6-20261109`) ist pausiert (Status T) und darf nicht aufgeweckt werden.
- Die erste geplante Veröffentlichung bleibt die **Prolog-Demo bis Aschenzoll**.

## 2. Bisheriger Entwicklungsverlauf

### Bestand vor dieser Sitzung (unverändert gültig)

Der Prolog verbindet folgende Abschnitte:
- Einstieg und Dorf
- Jägerrettung
- alte Mühle
- dreistöckiger Dungeon mit Rattler
- prozeduraler Verbindungswald
- Bärenfamilienhöhle
- Aschenzoll

Bereits Teil des Spiels sind Gegner, Truhen, Beute, Erfahrung, Ausrüstung und klassenloses Learning by Doing. Ebenso vorhanden sind:
- **Jäger und Begleiter:** dauerhafte Rettung, eigene Erfahrung, Tote bleiben tot, die Bogenanimation ist repariert.
- **NPC Memory Forge:** Utility bleibt Standard, keine neuronale Laufzeit-Lernschleife.
- **Aschenzoll:** leerer Tiertransport, zwei getrennte Beweiszweige.
- **Fadenwerk:** Browser-/Ilyan-Anbindung (PR 43).
- **World-Forge-Pilot:** 128 × 128 m, 16.641 Höhenknoten, gebackene Karten, vierstöckige Seitenhöhle.

Abgrenzungen und Nachweise dazu stehen im ursprünglichen Handoff und in `docs/evidence/worldforge-pilot-20261115/`.

### Neu in der Fortsetzungssitzung (15.11.2026)

**Pilot-Integration (#44)**
- Volle native Regression des Checkpoints: EditMode 85/85, PlayMode 132/132, 0 Skips.
- Der bisher nie nativ gelaufene **Missing-Map-Fall** ist bestanden.
- Befund: Beim Beenden des Editors nach PlayMode schreibt Unity eine URP-Änderung (`disabledShaderPasses: MOTIONVECTORS`) in ein lizenziertes Vendor-Material (FantasyKingdom Foliage `Atlas_Mat.mat`). Kein Projektcode tut das. Die Datei wurde byteexakt aus dem Backup wiederhergestellt; der neue Testrunner prüft private Inhalte per Hash und stellt solche Änderungen automatisch wieder her.

**Erstes Dorf (#45), Owner-Bericht vom Tablet**
- **Schwebende Kulisse:** Häuser, Bäume und Sträucher schwebten 0,32 m, Dorfbewohner 0,46 m.
  - Ursache 1: Der Dorfboden lag bei −0,32 m.
  - Ursache 2: Inaktive Outfit-Varianten bestimmten die Fußhöhe.
  - Jetzt: Boden auf 0, Pflaster nach mittlerer Steinoberfläche 2 cm darüber, Figuren auf sichtbaren Füßen.
  - Ein eng begrenzter Editor-Befehl `EntryWorldArtBuilder.GroundVillageArt` hat die 6 betroffenen privaten Prefabs angepasst, ohne Weltneubau.
- **Ilyan floh und die Hauptquest war gesperrt:**
  - Ursache: Ein einziger Fehltipper auf „Angreifen“ oder „Rauben“ setzte dauerhafte Feindseligkeit 2500 (Fluchtschwelle 2000).
  - Neu: Dorfbewohner beruhigen sich über gespeicherte Ereignisse alle 60 s, Grüßen (höchstens alle 30 s) und Helfen versöhnen schneller. Die Tat bleibt erinnert.
  - Alte Sozialzähler werden nie gesenkt, weil der Save-Validator sie an die Tatanzahl bindet. Stattdessen gibt es eine gemeinsame `NpcAgent.Mood`.
- **Rationen ohne Fortschritt:**
  - Das Auffüllen von Maras Lager ist eine einmalige Nebenaufgabe mit Dank und Questlog-Haken.
  - Bei vollem Lager ist der Button gesperrt.
  - „Entwenden“ ist abgesetzt und braucht ein zweites Tippen.
  - Review-Fix: Stehlen und Zurückgeben zählt nicht.

**Kampfgefühl (#46)**
- **Befund:** Schutz und Ruinensturm hatten nie eine Heldenaktion, der Held schaute nicht zum Ziel, Gegner reagierten nicht und verschwanden beim Tod sofort.
- **Fertigkeiten:** Jede ausgeführte Fertigkeit zeigt jetzt eine Aktion. Guard- und Cast-Pose sind aus der Kampf-Idle erzeugt, weil im Bestand keine Clips dafür existieren. Der Held dreht sich zum Ziel.
- **Treffer:** optischer Rückstoß (0,34 m, federt exakt auf die Core-Position zurück), Trefferstocken nur am Gegner, Kamera-Ruck.
- **Tod:** Besiegte Gegner kippen um und sinken ein.
- **Effekte:** Level-up mit Licht, Partikeln und Banner; Schockwelle beim Ruinensturm, Schimmer bei Schutz.
- **Core:** `SkillResult` meldet nur zusätzlich Ziel und getroffene Gegner.

**Dorf-Ankunftsszene (#47)**
- Beim ersten Betreten läuft eine feste, nicht überspringbare Szene: Kinobalken, Untertitel, Kamera auf den Sprecher. Mara meldet den Fehlbestand, Dorn will strafen, Ilyan bestreitet den Diebstahl.
- Die Simulation pausiert; die Entscheidungs-Buttons sind bis zum Ende verborgen.
- Danach werden beide Wege erklärt: Mara sofort Ersatz geben, oder herausfinden, wer es war und warum.
- Der Core vermerkt die gesehene Szene einmal im gespeicherten Story-Archiv, ohne NPC-Erinnerungen zu ändern. Fortgeschrittene Spielstände bekommen sie nie nachträglich.
- Szenentext: `docs/specs/village-arrival-20261115/owner-decisions.md`.

**Zurückgestellt**
- Größenanpassung des Helden: lokaler Branch `feat/hero-taller-20261115`, WIP, **nicht** angewendet. Der Owner hat die Messungen vom 12.11. als veraltet verworfen.
- Aktuelle Spielmessung: Held 1,82 m, Ilyan und Mara 1,80 m, Dorn 1,94 m.
- Nebenbefund: Bei Dorn rendern mehrere Rüstungssets gleichzeitig übereinander.

## 3. Verbindliche Handlung, Gameplay- und Präsentationsentscheidungen

Die Entscheidungen 1–10 aus dem ursprünglichen Handoff gelten unverändert (Spion, Immergrün bei Torschluss, Meisteralchemist, Kräuter/Alchemie, 3–6-Ebenen-Höhlen, Run-/Story-Grenzen, erst Gebiete, dann Feinschliff).

Neu festgelegt am 15.11.2026, jeweils Option A und **nicht erneut abfragen**:

11. **Dorfbewohner nach Übergriffen:** Sie fliehen zunächst und beruhigen sich ohne weitere Gewalt; Grüßen und Helfen versöhnen schneller. Die Tat bleibt erinnert, die Hauptquest bleibt erreichbar.
12. **Maras Lager:** Das Auffüllen ist eine eigene Nebenaufgabe mit sichtbarem Abschluss. Bei vollem Lager ist der Button gesperrt; Diebstahl ist abgesetzt und braucht eine Bestätigung.
13. **Treffer-Physik:** nur optischer Rückstoß, keine Core-Regel- oder Balanceänderung.
14. **Beute v2:**
    - Nicht jeder Gegner droppt.
    - Stufen Weiß, Blau, Gelb und Orange, mit Tiefe, Gebiet und Bossen häufiger.
    - Legendär nur von Bossen bzw. aus der tiefen Höhle, mit Waffen-Aura.
    - Vorhandene Items bleiben gültig.
15. **Tag/Nacht:** nur Optik.
    - Echte Skybox mit Sonne, Wolken, Abendrot und Sternen; ein Spieltag dauert etwa 24 Minuten und wird gespeichert.
    - Statt Dauernebel nur noch Dunst; eine Aschenwelt muss nicht Dauernebel bedeuten.
16. **Dorf-Intro:** feste, nicht überspringbare Ankunftsszene statt sofortiger Entscheidungen. KI-Laufzeitgespräche (Convai) sind verworfen: Konto, Internet und Kosten wären nötig, und es widerspricht der Regel „keine LLM-Aufrufe im Spieltick“.
17. **Ilyan:** ein verarmter ehemaliger Pfadleser, der seit dem Ausbleiben der Händler im Dorf bettelt.

Die Entscheidungsprotokolle stehen in `docs/specs/first-village-fixes-20261115/`, `docs/specs/combat-feel-sky-20261115/` und `docs/specs/village-arrival-20261115/`.

Weiterhin offen: Namen und Herkunft des Spions, Hintermänner, Schicksal der Vermissten, königliche Verantwortung. Bei neuen folgenreichen Lore-/Gameplay-Fragen den Besitzer mit drei konkreten Vorschlägen einbeziehen.

## 4. Revisionsgebundene Nachweise und Grenzen

| Bereich | Tatsächlicher Beleg |
|---|---|
| Pilot-Checkpoint | `9741e1d`: EditMode 85/85, PlayMode 132/132, 0 Skips, inkl. Missing-Map-Fall. `docs/evidence/pilot-integration-20261115/native-regression.json` |
| Erstes Dorf | RED/GREEN Bodentest. Nach Review-Fixes: EditMode 85/85, PlayMode 135/135, 0 Skips. Core 19.682. Zwei Review-Regressionen waren gegen den alten Code rot. `docs/evidence/first-village-fixes-20261115/acceptance.json` |
| Kampfgefühl | RED (Guard/Cast ohne Aktion), dann GREEN. EditMode 85/85, PlayMode 137/137, 0 Skips. `docs/evidence/combat-feel-20261115/acceptance.json` |
| Ankunftsszene | Eigener PlayMode-Test. EditMode 85/85, PlayMode 138/138, 0 Skips. Core 19.690. `docs/evidence/village-arrival-20261115/acceptance.json` |
| Core-Verifikation | 19.690 deterministische Assertions auf #47 |
| Statische Preflights | 40/40 und Unity/Android-Vertrag auf jedem Kopf |
| Reales Tablet | **ungeprüft**: Keine der neuen Änderungen lief auf einem Gerät, es gibt keine neue APK. |
| Ehrlichkeitshinweise | Die verschärften Quest-Abnahmeprüfungen in #45 sind nach der Diagnose entstanden (kein formales RED vorher). Die erzeugten Guard-/Cast-Posen sind keine professionellen Clips. Für die Szene gibt es keine Sprachausgabe (nur Text). |

Alle nativen Läufe liefen im Batchmodus (`-force-glcore`, Android-Buildtarget) im Ein-Kern-Budget. Owner-Prefs und ProjectSettings wurden nach jedem Lauf wiederhergestellt; private Inhalte wurden per Hash gegen das Checkpoint-Manifest geprüft.

## 5. Lokale Arbeitsbereiche

Die `/workspace/...`-Pfade des ursprünglichen Handoffs stammen aus einer anderen Ausführungsumgebung und **existieren auf dem VPS nicht**. Auf dem VPS gilt:

| Pfad | Zweck |
|---|---|
| `/var/lib/unityarpg/workspaces/are-arpg-forges-20261114` | Natives Spielprojekt mit privaten Assets und Library. Branches: `integration/worldforge-pilot-20261115`, `fix/first-village-20261115`, `feat/combat-feel-20261115`, `feat/village-arrival-20261115` (aktiv), `feat/hero-taller-20261115` (WIP, pausiert). Nicht eingecheckte Autorenszenen/WorldForgeData der Vorsitzung bleiben unangetastet. |
| `/var/lib/unityarpg/workspaces/are-arpg-hero-scale-20261115` | Losgelöster Scratch-Worktree der pausierten Größenanpassung; Inhalt ist im WIP-Branch gesichert. |
| `/var/lib/unityarpg/workspaces/are-arpg-mobile-ui-20261112` | Älterer UI-/Kamera-Worktree vom 12.11. Der Inhalt ist über PR 28 in `main`; dort liegen noch uncommittete Kamera-Reste. Nicht verwerfen, aber nicht mehr als Arbeitsstand nutzen. |

## 6. VPS-Verzeichnisplan (Ergänzungen)

Alle Pfade aus dem ursprünglichen Handoff gelten weiter. Neu:

| Absoluter VPS-Pfad | Inhalt / Verwendung |
|---|---|
| `/var/lib/unityarpg/.local/share/are-arpg/pilot-integration-20261115/` | Alle nativen Läufe dieser Sitzung (je Lauf `before.json`, `result.json`, `tests.xml`, `unity.log`, `renders/`). |
| `…/pilot-integration-20261115/run.py` | Nativer Testrunner. Als root ausführen: `python3 run.py <label> <EditMode\|PlayMode\|Execute> [Filter\|Methode]`, optional `INTENDED=<kommagetrennte beabsichtigte private Änderungen>`. Er erzwingt das Ein-Kern-Budget, prüft auf aktive Editoren, stellt Prefs und ProjectSettings wieder her, vergleicht private Inhalte per Hash und stellt unbeabsichtigte Änderungen aus `project-and-saves.tar` wieder her. Volle EditMode etwa 80 s, volle PlayMode etwa 300 s. |
| `…/pilot-integration-20261115/grounding-prefabs-before/` | Originale der 6 Dorf-/Figuren-Prefabs vor `GroundVillageArt` (mit SHA256SUMS). |
| `…/pilot-integration-20261115/ability-poses-before/` | Original-`EntryMelee.controller` vor den Guard-/Cast-Posen. |
| `…/pilot-integration-20261115/actor-prefabs-before/` | Sicherung für die pausierte Größenanpassung. |
| `…/pilot-integration-20261115/audit-1/` | Abgebrochener Project-Auditor-Versuch; die Paketdateien wurden aus den Sicherungen dort byteexakt wiederhergestellt. |
| Privates Overlay `Assets/Resources/ARE/Visuals/` | Geändert: `EntryVillageWorld`, `VillageBackdrop`, `EntryHero`, `EntryNpc0-2` (Boden/Füße); neu `EntryGuard.anim`, `EntryCast.anim`; erweitert `EntryMelee.controller`. Das Overlay ist nicht in Git und wird für jede APK lokal gebraucht. |
| `/var/lib/unityarpg/.local/share/are-arpg/handoff-20261115-update/` | Dieses Dokument. |

**VPS-Wartung am 14./15.11.**
- Etwa 28 GB freigegeben: 3 alte Aurion-DB-Backups, 3 alte Aurion-Releases, Aurion-Temp-Klone, das Docker-Image `browserless/chrome`.
- Swap von 4 auf 8 GB erweitert (`/swapfile2`, in `/etc/fstab`).
- Unity und Ashen Crypt wurden nicht angefasst.
- Aurion-Zombie-Prozess-Fix: Echoes_of_Aurion **PR #1365** (`init: true`), offen.

## 7. Maßgebliche Dateien im Repository (Ergänzungen)

- `Assets/Scripts/Core/NpcForgeCalm.cs`: Beruhigung und Versöhnung, `NpcAgent.Mood`, Nebenaufgabe Maras Lager (Netto-Regel).
- `Assets/Scripts/Core/NpcForgeArrival.cs`: Szenenvertrag `AshenCrypt.Scene.ArrivalAschengrenze.v1`, `ArrivalScenePending`, `CompleteArrivalScene`.
- `Assets/Scripts/Runtime/CombatFeelPresentation.cs`: Aktionen, Rückstoß, Trefferstocken, Umkippen, Level-up, Zauber-Effekte.
- `Assets/Scripts/Runtime/ArrivalScenePresentation.cs`: Szene, Pause, Kino-Kamera (`ArenaCameraRig.SetCinematic/ClearCinematic`).
- `Assets/Editor/EntryWorldArtBuilder.cs`: `GroundVillageArt`, Pflaster nach Steinoberfläche, Figuren auf sichtbaren Füßen.
- `Assets/Editor/EntryCombatArtBuilder.cs`: `AddAbilityPoses` (Guard/Cast).
- Tests:
  - PlayMode: `VillageGroundingTests`, `VillageQuestFlowTests`, `CombatFeelTests`, `ArrivalSceneTests`.
  - Core: `NpcCalmTests`, `ArrivalSceneTests`.
  - Der Helfer `TravelStory()` modelliert einen Spieler, der die Ankunftsszene gesehen hat.
- `docs/specs/combat-feel-sky-20261115/system-design.md`: Systementwurf für Kampfgefühl, Beute v2 und Himmel/Tageszeit.

## 8. Konkrete nächste Schritte

1. **PRs mergen:** #44 → #45 → #46 → #47, jeweils exakter Kopf, nach jedem Merge die Basis des nächsten auf `main` umstellen und `main` zurücklesen. Beim Merge keine Memory-Commits nachschieben (der Eintrag ist jeweils im PR).
2. **Block Himmel/Tageszeit (Entscheidung 15):** persistierte Core-Weltuhr in Ticks ohne Regelwirkung, prozedurale Skybox (Sonnenstand, Wolken, Abendrot, Sterne), Licht/Ambient/Nebel aus der Tageszeit, nur Dunst statt Dauernebel. Krypten und Innenräume behalten ihre Stimmung; bei reduzierten Effekten eine Farbverlaufs-Skybox.
3. **Block Beute v2 (Entscheidung 14):**
   - Katalog-Revision 2 mit Drop-Chance und vier Stufen.
   - `LootItem.TryRead` muss Revision-1-Belege weiter nachwürfeln und Revision 2 zusätzlich unterstützen.
   - Core-Prüfungen, die einen Drop pro Kill annehmen, auf den neuen Vertrag umschreiben, nicht abschwächen.
   - Seltenheitsfarben und Aura für legendäre Waffen.
4. **Neue APK:** Der Owner hat einem VPS-Build zugestimmt (etwa 30 min im Ein-Kern-Budget). Er ist noch nicht gebaut. Weg: `ArpgBuildTools.BuildAndroidApk` mit Prüfung von Signatur, ARM64/IL2CPP und 16-KiB-Ausrichtung sowie revisionsbenannter Auslieferung, ohne bestehende Routen zu überschreiben.
5. **GitHub-gehosteter APK-Build:** Das Repo ist **öffentlich**.
   - Offen ist eine Owner-Entscheidung, wie die lizenzierten Assets geschützt in den Build kommen. Vorschlag: privates Build-Repo mit hashgebundenem Download vom VPS.
   - Der Owner muss Unity-Login (Unity-6-Kontolizenz) und Signierschlüssel selbst als Secrets setzen.
   - Das CI-Image `unityci/editor:ubuntu-6000.6.8f1-android-3` existiert.
6. **Fadenwerk-Erweiterung:** Die Ankunftsszene ist im Browser-Editor noch nicht pflegbar. Der bestehende Vertrag hat feste Feldzahlen und einen Hash; eine Szenenerweiterung braucht einen eigenen Gateway- und Editor-Schritt.
7. **Offene Darstellungsfunde:** Dorn rendert mehrere Rüstungssets gleichzeitig; Heldengröße (WIP pausiert, nur auf Owner-Wunsch fortsetzen); Sprachausgabe für die Szene (eigene NPC-Voicepacks wie „NPC Merchants“ und „Wary NPC Guards“ sind im Besitz, aber nicht importiert).
8. **Project Auditor:** braucht das Paket `com.unity.project-auditor-rules`. Der Owner hat das Hinzufügen abgebrochen; nur nach seinem Okay erneut versuchen und danach die Paketdateien wieder exakt zurücksetzen.
9. **Weiterhin aus dem ursprünglichen Plan:** Reise in spielbaren Abschnitten bis Immergrün, zwei Folgegebiete, Berufe (Kräuter/Alchemie), Inari-Jäger und weitere Gegner, danach Balancing und Prolog-Release-Gates. Tablet-Abnahme bleibt optional und blockiert nichts.

Bei Bugs zuerst ein echter fehlschlagender Verhaltenstest, danach Fix und benachbarte Regression. Nach jedem abgeschlossenen Integrationsblock genau ein kurzer Memory-Eintrag im selben PR. Die frühere Ausnahme „Memory.md unverändert lassen“ ist überholt.

## 9. Verkaufsmodule — nur zur Vollständigkeit

Unverändert gegenüber dem ursprünglichen Handoff: Memory Forge ist pendingReview, Fadenwerk und World Forge sind Draft. „Alles erledigt“ ist eine Besitzerrückmeldung, kein Portal-Readback.

## 10. Zugangsdaten und Sicherheit

- Ein vom Owner im Chat übergebener GitHub-Token wurde für Push und PRs dieser Sitzung **nur im Arbeitsspeicher** einzelner Befehle verwendet und nirgends gespeichert (keine Git-Config, kein Credential-Store, keine Dateien).
- Der Token, frühere Tokens und Kennwörter aus dem Chat sollten **rotiert** werden.
- Die claude.ai-GitHub-Verbindung hatte keine Schreibrechte (403); Push lief nur mit dem Owner-Token.
- Keine Kennwörter, Tokens oder Session-Argumente in Ausgaben, Logs oder Handoffs kopieren.

## 11. Startprompt für eine neue Sitzung

> Setze Ashen Crypt anhand dieses aktualisierten Handoffs fort. Lies zuerst AGENTS.md, Memory.md, die ASHEN-001-Authority (MOB-001 ist ready), die Besitzerentscheidungen unter `docs/specs/*-20261115/` und die neuesten Evidence-Dateien. Prüfe den tatsächlichen Git-, PR-, VPS- und Editorzustand, bevor du etwas startest; die PRs #44–#47 bauen aufeinander auf. Nutze für native Läufe den Runner unter `…/pilot-integration-20261115/run.py`. Erhalte den 100-ms-Core, historische Saves, private Assets, Fable-HUD und Kamera. Höchstens ein aktiver Unity-Editor im Ein-Kern-Budget. Nächste Blöcke: Himmel/Tageszeit, Beute v2, neue APK. Neue folgenreiche Lore-/Gameplayfragen mit drei Vorschlägen an den Besitzer. Keine uncommitteten Arbeiten verwerfen, keinen Store-Draft ungefragt veröffentlichen, keine Zugangsdaten speichern.
