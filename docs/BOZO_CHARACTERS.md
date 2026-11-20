# Einheitliches Figurensystem: BoZo Stylized Modular Characters

Owner-Vorgabe vom 19.11.2026: **Alle NPCs, Händler und menschlichen Figuren** von Ashen Crypt entstehen aus dem
gekauften Asset-Store-Paket „BoZo: Stylized Modular Characters - Fantasy Pack“ (Produkt 361388, Version 2.6.5).
Ein System für alle Figuren statt eines eigenen Pakets pro NPC. Jede Figur wird passend zu ihrer Rolle gestaltet.

Das Paket selbst ist privat (nur auf dem VPS im Live-Workspace) und wird nie über dieses Repository verteilt.
Im Repository liegen nur die Rezepte und das Werkzeug. Untote Gegner (Skelette) sind keine menschlichen Figuren und
bleiben, wie sie sind.

## Figuren

| Rezept | Rolle | Visuals im Spiel |
| --- | --- | --- |
| `ilyan` | Pfadleser: verarmt, vorsichtig; graugrüne Tunika, Schal, umwickelte Hände | `EntryNpc0`, `Npc0` |
| `mara` | Versorgerin: Leinenbluse, Ledermieder, Schürze, ockerfarbenes Schultertuch | `EntryNpc1`, `Npc1` |
| `dorn` | Wache: Eisenharnisch, Kesselhut, roter Gürtel, 2,05 m | `EntryNpc2`, `Npc2` |
| `hunter` | Jäger der Aschestraße und Begleiter: grüne Kapuze, Lederwams, gebundener Bart | `RoadHunter` |
| `hero` | Der Wanderer (Spielfigur): Lederwams, Brustschutz, Schulterplatten, roter Schal | `EntryCombatHero`, `EntryHero`, `Hero` |
| `patient_a`–`patient_c` | Verletzte der alten Mühle (liegend, zugedeckt) | `MillPatient0`–`MillPatient2` |
| `villager_a`–`villager_c` | Dorfbewohner der Aschengrenze (Bauer, Frau mit Zopf, alter Mann) | `Villager0`–`Villager2` |
| `merchant` | Händler: weinroter Umhang mit Goldsaum, lange Robe, Schärpe | `Merchant0` |

`Villager0–2` und `Merchant0` sind für Bewohner und Händler vorbereitet; im Spiel gebunden werden sie, sobald eine
Stelle sie braucht (zum Beispiel der Bau-Modus oder ein Händler-Stand).

## Rezepte

`Assets/Editor/BozoCharacters/figures.json` beschreibt jede Figur:

| Feld | Bedeutung |
| --- | --- |
| `id`, `role` | Kurzname und Rolle im Spiel (Pflicht: die Gestaltung folgt der Rolle) |
| `base` | Vorlage des Pakets, von der die Figur ausgeht (`Fantasy/Hero` männlich, `Fantasy/Ranger` weiblich) |
| `body`, `face` | Körper- und Gesichtsformen 0–100 (z. B. `Muscle`, `Weight`, `Belly`, `Stern`, `EarsElf`) |
| `outfits` | Teile je Platz (`Head`, `Body`, `Eyes`, `EyeBrows`, `Top`, `Bottom`, `Feet`, `Gloves`, `Hat`, `Shoulders`, `Neck`, `HairFront`, `HairBack`, `LowerFace`, `UpperFace`) mit bis zu neun Farben `#rrggbb` |
| `remove` | Plätze der Vorlage, die leer bleiben |
| `height`, `texture`, `motion` | Sichtbare Höhe in Metern, Kantenlänge des Texturatlas (512/1024/2048), Bewegungssatz `m`/`f` |
| `keys` | Spielvisuals unter `Resources/ARE/Visuals/<Key>.prefab` |
| `sources` | Private Quellfiguren für andere Werkzeuge (Held, liegende Patienten), nicht in `Resources` |

`BozoFigureRecipes` prüft die Rezepte (auch ohne installiertes Paket): gültige Schlüssel, Höhen, Farben, keine
doppelten Visuals, und dass jede vom Spiel gebundene Menschenfigur ein Rezept hat. Abgelehnt werden auch Werte, die
keine Zahl sind, und Schlüssel von Visuals, die ein anderes Werkzeug aus einer Quellfigur baut (`EntryCombatHero`,
`EntryHero`, `MillPatient0–2`): Solche Figuren stehen unter `sources`.

### Farben richtig setzen

Jedes Teil hat **neun Farben**. Die Namen, die das Paket dazu anzeigt, sind oft nur ein Platzhalter mit drei Namen
(`Base|Pockets|Straps`), obwohl das Teil sechs oder acht Farben verwendet. Wer nur die benannten Farben setzt, lässt
sichtbare Flächen in den bunten Standardfarben des Pakets (so entstand der erste, falsche Entwurf).

So rechnet der Kleidungs-Shader des Pakets: Die ID-Textur wählt eine Dreiergruppe (rot = Farben 1–3, grün = 4–6,
blau = 7–9), die drei Kanäle der Diffuse-Textur gewichten die drei Farben der Gruppe.

*ARE ARPG → Art → BoZo Characters → Write Part Colour Report* (`BozoPartColours.WriteReport`) schreibt für jedes
Teil nach `Library/BozoCharacters/part-colours.txt`, welche Farbe wie viel Fläche färbt und in welcher Körperhöhe sie
liegt, zum Beispiel:

```
Bottom/Bottom_RusticSkirt [Base|Pockets|Straps]
   1: 11.1% height 0.68–1.04 default #C03838
   2: 12.6% height 0.76–1.04 default #455B80
   4: 5.4% height 1.05–1.14 default #585058
   5: 1.2% height 1.08–1.11 default #7F5A4A
   7: 69.2% height 0.41–1.06 default #454950
```

Hier ist Farbe 7 der eigentliche Rock. Vor jedem neuen Rezept den Bericht lesen und alle Farben mit Fläche setzen.
Die Farbliste eines Teils endet an der ersten Lücke; deshalb immer alle Farben bis zur letzten benutzten angeben.

## Bauen

Nur im Workspace mit installiertem Paket (private Datei `Assets/csc.rsp` mit `-define:ARE_BOZO_CHARACTERS`):

1. *ARE ARPG → Art → BoZo Characters → Bake All Figures* (`BozoFigureBaker.BakeAll`, einzelne Figuren mit
   `BozoFigureBaker.Bake(new[] { "mara" })`). Der Editor wechselt in den Play-Modus einer privaten Bühnen-Szene. Das
   Hersteller-System setzt jede Figur zusammen, fügt sie zu **einem Mesh und einem Material** zusammen und speichert
   sie unter `Assets/PrivateArt/BozoCharacters/<id>/`. Dauer: etwa ein bis vier Minuten je Figur.
2. Zurück im Edit-Modus entsteht aus jeder Figur ein **passives Visual** (nur Transform, Mesh, Renderer, Animator –
   genau das, was `PassiveArtVisuals` annimmt): auf die Zielhöhe skaliert, auf die sichtbaren Füße gestellt, mit Idle
   und Gehen aus dem Paket (humanoide Clips, Parameter `MoveMagnitude` wie bisher). Nur dieser zweite Schritt:
   *Rebuild Game Visuals From Baked Figures*. Dabei entstehen vier Dinge, die das Paket nicht spielfertig liefert:
   - **Spiel-Mesh** (`<id>_GameMesh.asset`): Das zusammengefügte Mesh trägt noch rund 80 Blend Shapes (Mimik,
     Körperregler), mehr als die Hälfte seiner Daten. Die im Rezept gesetzten Regler werden in die Punkte
     geschrieben, die Blend Shapes entfallen und die Kopie für Skripte wird nicht mitgeliefert. Gleiches Aussehen, im
     Projekt rund 3 MB statt 14 MB je Figur.
   - **Spiel-Material mit lesbarer Normal-Textur** (`<id>_GameMaterial.mat`, `<id>_GameNormal.png`): Das Paket
     speichert die Normal-Textur in einer Zwei-Kanal-Packung (x im Alpha-, y im Grünkanal) als gewöhnliche Textur.
     Dieses Projekt liest Normal-Texturen als x/y/z; unverändert gelten alle Flächen als vom Licht abgewandt – die
     Figuren stehen „im Dunkeln“ und die Augen wirken glasig. Die Kopie wird als echte Normal-Textur geschrieben und
     importiert, damit jede Plattform sie richtig liest.
   - **Aufgehellte Farben** (`<id>_GameColours.png`): Das Paket multipliziert jede Farbe mit seiner gemalten
     Schattierung, die Figuren kommen dunkler heraus als die Welt. Der Wert `"light"` oben in `figures.json` hebt
     die Helligkeit an (Helligkeit hoch `light`; 1 = unverändert, kleiner = heller, erlaubt 0,4–1; Farbton und
     Sättigung bleiben). Nach einer Änderung genügt der zweite Schritt, kein neues Backen.
   - **Eigener Avatar** (`<id>_GameAvatar.asset`): Die Körperregler skalieren Knochen (Schultern, Brust, Glieder).
     Der Avatar des Pakets beschreibt das unskalierte Skelett; alles, was Unity über den Avatar löst, landet dann
     neben den echten Knochen (die Schwerthand des Helden verfehlte ihren Griff um 4 cm, mit eigenem Avatar 0,3 cm).
   Die Dateien des Herstellers bleiben unverändert daneben liegen.
3. *ARE ARPG → Art → Build Private Mill Patients* baut die liegenden Verletzten aus `PatientSource0–2` und entfernt
   übrig gebliebene Meshes früherer Figuren aus `Resources`.
4. *ARE ARPG → Art → Rebuild Hero From Configured Source* baut `EntryCombatHero` und `EntryHero` aus der in
   `Assets/PrivateArt/entry-combat-art.json` eingetragenen Quelle. Für den Helden dieses Systems stehen dort
   `"hero": "Assets/PrivateArt/BozoCharacters/Sources/HeroSource.prefab"`, `"keepHeroMaterials": true` (das Material
   der Figur bleibt) sowie Lage und Drehung der Waffe in der rechten Hand (`weaponPosition`, `weaponEuler`). Der
   vorhandene Controller `EntryMelee` mit allen Zuständen bleibt; Materialkopien eines früheren Helden werden
   entfernt.

Bericht des letzten Laufs: `Library/BozoCharacters/report.txt`.

### Waffe in der Hand

Die Drehung der Waffe hängt von den Knochenachsen der Hand ab und musste für die neue Figur neu bestimmt werden.
Übernommen wurde die Lage relativ zur Handfläche (Richtung Handgelenk → Mittelfinger, Zeige- → kleiner Finger) der
bisherigen, abgenommenen Spielfigur in der Kampf-Grundhaltung: `weaponEuler = (275.48, 223.75, 50.93)`. Der Griff sitzt
jetzt in der Faust statt im Handgelenk: `weaponPosition = (0.0471, -0.0154, -0.0015)`. Nach einem Neuaufbau der Visuals
immer auch den Helden neu bauen (Schritt 4), weil er aus `HeroSource` entsteht.

## Regeln und Grenzen

- Figuren sind reine Darstellung. Bewegung, Kollision, Kampf und Gespräche bleiben beim Core; die Schlüssel der
  Visuals sind unverändert.
- Neue menschliche Figuren: Rezept ergänzen, backen, Schlüssel binden. Keine Figur aus einem anderen Paket.
- *Build Entry Worlds* (`EntryWorldArtBuilder.Build`) lässt Visuals in Ruhe, die zu einer gebackenen Figur gehören
  (`BozoFigureRecipes.Owns`); es ersetzt sie nicht mehr durch das frühere Aussehen.
- Die Teile des Pakets liegen in `Resources`-Ordnern des Herstellers. Damit sie nicht in einen Build gelangen (das
  gesamte Paket wäre sonst in der APK), benennt `BozoBuildGuard` diese Ordner für die Dauer eines Builds um und
  stellt sie danach wieder her – auch nach einem abgebrochenen Build beim nächsten Editor-Start oder Backen.
- Die Quelltexturen des Pakets werden unkomprimiert importiert (schneller, keine doppelte Kompression beim
  Zusammenfügen). In den Build gelangen nur die gebackenen Atlanten der Figuren.
- Nicht installiert wurden die Paket-Abhängigkeiten aus dessen Manifest (Shader Graph 14, TextMeshPro 3.0.7 –
  beides ist im Projekt neuer vorhanden) und das Skript, das die Grafik-Einstellungen des Projekts ändern würde.
- Je Figur 21 000–28 000 Dreiecke, ein Material, vier Texturen (Farbe, Normal, Metall, Glanz).
- `EntryWorldArtBuilder.VisibleBody` misst jetzt mit der Skalierung aller Eltern (vorher wurde eine skalierte Figur
  zu klein gemessen).
- Noch nicht geprüft: Android-Build und echtes Gerät. Die Normal-Textur wird je Plattform anders kodiert; ein Blick
  auf dem Zielgerät steht aus.

## Tests

- `Assets/Tests/EditMode/BozoFigureRecipeTests.cs`: Rezepte gültig, jede Figur mit Rolle, fehlerhafte Rezepte werden
  abgelehnt. Läuft überall.
- `Assets/Tests/EditMode/BozoFigureVisualTests.cs`: Jede gebackene Figur wird vom Spiel als passives Visual
  angenommen, behält ihr eigenes Material, hat ein Mesh ohne Blend Shapes, eine als Normal-Textur importierte
  Normal-Textur mit nach außen zeigenden Normalen, einen Avatar, der die skalierten Knochen kennt, humanoides Rig mit
  Idle/Gehen, steht auf den Füßen und hat die Höhe ihrer Rolle; Held und Verletzte stammen aus den Figuren; die Waffe
  sitzt in der rechten Hand; der Build-Schutz versteckt das Paket und stellt es wieder her. Ohne installiertes Paket
  werden diese Prüfungen als übersprungen gemeldet, nicht als bestanden.
- Die vorhandenen PlayMode-Tests der Spielfigur (`ArmedRunKeepsTheReviewedGrip` und weitere) laufen mit gesetztem
  `ARE_ENTRY_EVIDENCE_DIR` gegen die neue Figur.
