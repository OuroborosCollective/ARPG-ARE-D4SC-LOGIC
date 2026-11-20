# Einheitliches Figurensystem (BoZo) – Abnahme 20.11.2026

Basis `main` `488567f`; Branch `feat/bozo-characters-20261119`. Native Prüfung im Live-Workspace B (Unity 6000.6.8f1,
Software-GL, Zielplattform Android) mit privat installiertem Paket 361388 (2.6.5); Python/Core im Source-Workspace A.
Die geprüften Dateien in B sind byteweise gleich mit dem Branchstand in A. B enthält zusätzlich die Stände Bau-Modus,
Fadenwerk 1.1.0 und World Forge (andere Branches), daher die höhere Gesamtzahl der Tests.

| Prüfung | Ergebnis |
| --- | --- |
| Kompilierung im offenen Editor | fehlerfrei |
| EditMode `BozoFigure*` (Rezepte 4, Visuals 5) | 9/9 am Endstand |
| EditMode gesamt | 135/135 (1309 s), vor der letzten Rezept-Härtung; danach nur `BozoFigure*` erneut (9/9) |
| PlayMode gesamt (mit `ARE_ENTRY_EVIDENCE_DIR`) | 171/171 (1591 s), nach der letzten Änderung an Figuren und Laufzeit; spätere Änderungen betreffen nur Editor-Werkzeug und Tests |
| PlayMode `Grip` (Schwertgriff im Lauf und im Stand) | 2/2, Schwellen unverändert |
| Core (`Tests/CoreVerification`) | 21 239 Assertions PASS (kein Core- und kein Laufzeitcode geändert) |
| Preflight (`Tests/Preflight`) + `unity_contract.py preflight` | 40/40, PASS |
| Skripte für den Android-Player kompilieren (`PlayerBuildInterface.CompilePlayerScripts`) | 42 Assemblies, fehlerfrei |
| Rezept-Angriffsprobe (18 fehlerhafte Rezepte) | 17 schon beim Einlesen abgelehnt; ein Schlüssel auf fremde Kunst (`EntryEnemy0`) wird erst beim Schreiben gestoppt (`MayWrite` = nein, geprüft) |

## Figuren am Endstand (im Editor zurückgelesen)

| Visual | Dreiecke | Renderer / Materialien | Controller |
| --- | --- | --- | --- |
| `EntryNpc0`, `Npc0` (Ilyan) | 23 819 | 1 / 1 | BozoLocomotionM |
| `EntryNpc1`, `Npc1` (Mara) | 25 466 | 1 / 1 | BozoLocomotionF |
| `EntryNpc2`, `Npc2` (Dorn) | 28 315 | 1 / 1 | BozoLocomotionM |
| `RoadHunter` | 27 391 | 1 / 1 | BozoLocomotionM |
| `Hero`, `EntryCombatHero`, `EntryHero` | 27 062 | 1 / 1 | BozoLocomotionM bzw. `EntryMelee` |
| `Villager0`–`Villager2` | 22 070 / 25 468 / 23 838 | 1 / 1 | BozoLocomotionM/F |
| `Merchant0` | 28 259 | 1 / 1 | BozoLocomotionM |
| `MillPatient0`–`MillPatient2` | 22 131 / 24 428 / 21 909 | 2 / 2 (Körper, Decke) | – (liegend) |

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:ea1366e624b8ed28c72b51fdd8a62a68a2ab2930b62b81c240acb35d3bcdca8e -->

Die gemeldete Abhängigkeitsanalyse prognostiziert 73 Figurendateien (Quellgröße 42 MB, davon Meshes 29,9 MB,
Texturen 9,2 MB) und fünf Vendor-Dateien (Kleidungs-Shader und vier Idle-/Geh-Clips). Als reproduzierbare Methode
ist die rekursive Ermittlung mit `AssetDatabase.GetDependencies` ausgehend von den Spiel-Visuals vorgesehen;
der ursprüngliche Analyseaufruf ist nicht archiviert und muss unabhängig zurückgelesen werden. Diese Zahlen
belegen keine tatsächlichen Build-Inhalte: Ein APK und ein zugehöriger Build-Report fehlen (siehe „Nicht geprüft“).

<!-- /coderabbit-ai-generated:v1 -->

## Befunde und Korrekturen

- **Figuren „im Dunkeln“ (Owner-Befund 19.11.):** gemessen im selben Dorfbild, Boden unverändert (106, 93, 64).
  Heldenschal (39, 18, 15) → (51, 41, 38), Heldenhose (23, 21, 16) → (42, 37, 27), Maras Rock (62, 56, 43) → (86, 74, 52),
  Ilyans Rücken (41, 39, 29) → (68, 63, 46). Zwei Ursachen: Die Normal-Textur des Pakets liegt in einer
  Zwei-Kanal-Packung vor, die dieses Projekt als x/y/z las (A/B im selben Licht: mittlere Helligkeit 55,8 mit der
  Originaltextur, 73,2 ohne Normal-Textur, 72,9 mit neu geschriebener), und die Farben sind durch die gemalte
  Schattierung des Pakets abgedunkelt (`"light": 0.6` hebt sie an).
- **Schwertgriff:** Die Körperregler skalieren Knochen; mit dem Avatar des Pakets verfehlte die Hand den Griff
  (`ArmedRunKeepsTheReviewedGrip` rot mit 4,13 cm bei einer Grenze von 4 cm), mit eigenem Avatar je Figur misst die
  Sonde 0,3 cm und der Test ist grün, ohne die Grenze anzufassen.
- **Rezept-Härtung am 20.11. (zuerst rot, dann grün):** `"height": NaN` und `"light": NaN`/negativ wurden angenommen
  (ein Vergleich mit „keine Zahl“ ist nie wahr) und hätten Figur bzw. Farbtextur zerstört; ein Rezept konnte
  `EntryCombatHero`/`EntryHero` als Schlüssel nennen und damit die Spielfigur samt Waffenhalterung durch eine
  Laufpuppe ersetzen. Beides wird jetzt abgelehnt (`NumbersThatAreNotNumbersAreRejected`,
  `ARecipeCannotClaimAVisualBuiltByAnotherTool`: 2 rot vor der Korrektur, 4/4 danach).
- **Skripte des Pakets im Build:** Der Namensraum des Herstellers wird in `Assembly-CSharp.dll` (135 KB vor dem
  Entfernen ungenutzten Codes) mitkompiliert. Die Teile des Pakets bleiben draußen (`BozoBuildGuard`, Test
  `BuildGuardHidesThePackFromPlayerBuildsAndRestoresIt`).

## Private Belege

Nur auf dem VPS unter `/var/lib/unityarpg/.local/share/are-arpg/bozo-characters-20261119/` (104 Spielbilder aus dem
PlayMode-Lauf im Ordner `evidence/`, Vorher-Bilder in `evidence-dark-normals/`):

```
de1741b3a6fbcd333e7adb383c4a801accf21aac38f9f578c5c268bb32d6c8e4  game-before-after.png
900c01631269131a7188ae19144ad68962f5706045db0a00100f4bc3dc5b93d5  renders/lineup-final.png
c2cfd75c67cff15005bef53f2e673fc198aa9c538effb1e5836c8ea78a0e3895  evidence/act2-supply-square.png
aed1e0ab795d8f27bbe38f70275c33f0df1d98006855d935aef481c860aa2b78  evidence/act2-arrival.png
757da7f6cd1011912d8a02f8bebf64e0ad4c66c4161ded1c5c3d8068e52f3eeb  evidence/arrival-scene-line0.png
abfc81dde5eead6724bb0dcb66a2d96755d6bc5bd0352aa2b9c6b41cee549d64  evidence/hero-armed-run.png
bb4995d8a9e6136ee56dc4b2cf65d4091bf9111213f33d48002396a512c069b0  evidence/hero-strike-detail.png
6dd73243477e38b8221badf2b0cd3faf50af92a5237230faf4f14c2c8dc5458b  evidence/mill-patients-return.png
8242b76893cda70952cc60c35845e0997396811be5244d6e58d8a8d4499ee208  evidence-dark-normals/act2-supply-square.png
db0bf7c5e23ed56bbcc0c771c1604185366107c7176f1f0fe2b4da618528da22  Paketdatei (BoZo 2.6.5, .unitypackage)
```

## Nebenwirkungen und Aufräumen

Nach jedem Play-Modus wurden die Spielstand-Schlüssel des Editors aus der Sicherung zurückgeschrieben. Ein Versuch
an `EntryWeaponGrip.cs` (Laufzeit) wurde verworfen und zurückgesetzt; der Hilfstest dazu ist entfernt. In B bleiben
privat: das Paket, `Assets/csc.rsp`, die gebackenen Figuren unter `Assets/PrivateArt/BozoCharacters/`, die
ersetzten Visuals (Sicherung der vorherigen unter `visuals-before/`), „Parallel Import“ eingeschaltet. Die CPU-Grenze
des Editors steht auf 6,5 Kerne (Owner-Wunsch).

## Nicht geprüft

- Android-APK und echtes Gerät: weder gebaut noch angesehen. Offen sind dort die Kodierung der Normal-Textur, die
  Bildrate mit 22 000–28 000 Dreiecken je Figur und die Größe des Builds.
- `BozoBuildGuard` in einem echten Build (geprüft sind Verstecken und Wiederherstellen direkt, nicht der Ablauf im
  Build selbst).
- Kompilieren ohne installiertes Paket: nur am Quelltext geprüft (alles Paketabhängige steht hinter
  `ARE_BOZO_CHARACTERS`, Tests greifen per Reflection zu), nicht in einem Editor ohne Paket ausgeführt.
- GitHub-CI für diesen Stand (nicht gepusht).
- Das frühere Menschenpaket (Polytope) liegt weiter unter `Resources/ARE/Visuals/Vendor` und würde mitgebaut.


## Integrationsprüfung vom 20. November für PR #96

Der unabhängige Lauf verwendete Unity 6000.6.8f1 mit Software-OpenGL, dem privaten BoZo-Paket und den erzeugten Spiel-Prefabs auf dem VPS. Für die beiden regulären Testsuiten wurde Commit `a169f3af7c0b2c5990c207f43a9192ae1af1a204` nach dem Zusammenführen von #93 und #95 ausgecheckt. Git bestätigte Tree `8d1ffd9dd4ad82df7874dbfdd1ac70c1cb3c90f2`.

| Prüfung | Ergebnis | SHA-256 des Belegs |
| --- | --- | --- |
| EditMode `BozoFigure` | 11/11, keine Skips | `0b67ab50da52095fb1bb21d3068f54ea9958d9e9a9d1f03fdb4ac5c4ed99a7eb` |
| PlayMode Schwertgriff im Lauf, Freigabe beim Schlag | 2/2, keine Skips | `e4e04e1dc08ed4458261c67ef1cb4ceae91c5371dfe5a433434298515874bff5` |
| CoreVerification Release | 21 239 Assertions | `bafba6f747bd3486a576eef3fc8dac473b21978497177819f63d499b5c3fa99e` |
| Preflight im sauberen Git-Checkout | 48/48 | `7b8c91dd98fed078e0f00c5b3e41687e8986abe01ac1abcf7bad5182e68c24da` |

- **Material.** Vorher scheiterte bei `598314bb17d492ee04d2d0a533aa55c3a3f52eca` der echte Bindevorgang für Ilyan. Der aktive Material-Tag war leer, obwohl Subshader 0 den Tag `UniversalPipeline` trägt. Der Adapter prüft nun auch die Subshader und erhält das bereits kompatible Figurenmaterial. RED-XML `9aec7962f10922b81b39e7e580ca93a1142ebbf15e8694b9a6e9eb87b9335e17`.
- **Griff.** Der ursprüngliche PlayMode-Lauf maß maximal 0,56965816 m Abstand am Schwertgriff, erlaubt sind weniger als 0,04 m. Die opt-in Hand-IK der Spielfigur wird nun bereits vor dem ersten Sichtbarkeitsupdate ausgewertet. Die vorhandenen Winkel- und Distanz-Assertions blieben erhalten. RED-XML `314455c504fa994be8feb120044cf520549cb9b657a8236bbfea0332a5d9ef22`.
- **Fehlgeschlagener Rebake.** Eine separate native Gegenprobe auf derselben Codebasis setzte in der isolierten Arbeitskopie absichtlich eine fehlende Vorlage für Ilyan. Der normale Baker meldete den erwarteten Fehler. Beide zuvor geladenen Szenen kehrten in der ursprünglichen Reihenfolge zurück, Szene B blieb aktiv, Ilyans Mesh und Material waren ladbar. Die 25 SHA-256-Einträge der gebackenen Figur einschließlich ihrer Metadaten und des Spiel-Prefabs waren vor und nach dem Versuch bytegleich. Manifest `4d81ecba64935ccdc1311a4724b276aeed9ac80d5acfbbd6c382ff4744c463f8`, Editorlog `13703b84f5eaa62b2c058cb4f997ff60de4200a3063b05490a1409e168320cbd`. Das temporäre Prüfrezept und die Sonde gehören nur zu dieser Gegenprobe.
- **Grenzen.** Die native Prüfung deckt das Verstecken und Wiederherstellen verschachtelter Resources-Ordner ab. APK-Build, echter Build-Callback und Gerätetest sind offen. In einem zusätzlichen Preflight im privaten Importbaum verlangt der bestehende Metadaten-Test den Text `MonoImporter:` auch in fremden Dateien. Die ausgelieferte `BodyShapeModifier.cs.meta` enthält nur Dateiversion und GUID. Der Test meldete dies, Unity importierte und kompilierte das Skript, und der normale Git-Preflight bestand. Die historischen Zahlen im ersten Abschnitt bleiben dem früheren gemischten Workspace zugeordnet.

Die privaten Rohbelege liegen unter `/var/lib/unityarpg/.local/share/are-arpg/pr96-review-598314bb/`. GitHub Core auf dem getesteten Commit war erfolgreich, die separate lizenzierte CI-PlayMode lief dort nicht. [GitHub Core 44703565340](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/44703565340)
