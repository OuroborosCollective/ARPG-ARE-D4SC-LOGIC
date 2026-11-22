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
| `smith` | Der Schmied: kräftig, dunkle Lederweste, Lederstulpen, kurzer dunkler Bart | `Smith0` |
| `orsel` | Orsel, die Heilerin: Leinenbluse, salbeigrünes Mieder, helle Schürze, moosgrünes Tuch, ergrauter Zopf | `Orsel0` |
| `guard_a`, `guard_b` | Dorfwache in zwei Farben: Waffenrock mit Stahlplatten, Soldatenhelm, Schulterplatten | `Guard0`, `Guard1` |
| `fighter_plate` | Bergwache in Vollplatte: kantiger Plattenharnisch, Helm mit dunkelrotem Busch | `Fighter0` |
| `fighter_steel` | Gepanzerter Kämpfer: geschwärzter Stahlharnisch, Stahlhelm mit Nackenschutz | `Fighter1` |
| `messenger` | Bote: Reiseumhang mit Kapuze, schlichte Tunika, hohe Stiefel | `Messenger0` |
| `tailor` | Schneiderin: cremefarbene Bluse, pflaumenfarbenes Mieder, langer Rock | `Tailor0` |
| `forester_a`, `forester_b` | Förster in zwei Farben: Weste mit Lederbrust, Kapuze, lange Waldstiefel | `Forester0`, `Forester1` |
| `baker` | Bäckerin: helle Bluse, hellbraunes Mieder, weiße Schürze, blonder Zopf | `Baker0` |
| `doctor` | Arzt: langer dunkler Mantel, Handschuhe, ergrautes Haar | `Doctor0` |

`Villager0–2` stehen für die Bewohner der Dorfwirtschaft. An den Bauplätzen der Dorfhäuser stehen `Smith0` (Schmiede),
`Villager0` (der Bauer, Feldscheune) und `Orsel0` (Krankenstube). `Merchant0` ist für einen Händler-Stand vorbereitet.

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
| `sameShapeAs` | Nur für Farbvarianten: `id` der Grundfigur mit denselben Teilen und Formen. Die Variante bekommt kein eigenes Modell, sondern nutzt das der Grundfigur mit eigener Textur |
| `parts` | Figur in zwei Teilen (Körper und Kopf), damit Köpfe getauscht werden können; siehe unten |

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

### Farbvarianten teilen sich ein Modell

Wachen, Kämpfer, Boten, Schneider, Förster, Bäcker und Arzt sind für spätere Bewohner und Rollen vorbereitet; das Spiel
bindet sie, sobald eine Stelle sie braucht.

Das Modell einer Figur ist ihr größter Teil (13 000 bis 16 000 Eckpunkte, rund 3 MB), die Farbtextur ist klein. Zwei
Figuren mit denselben Teilen und denselben Körper- und Gesichtsformen ergeben beim Backen dasselbe Modell, Eckpunkt für
Eckpunkt. Eine Farbvariante nennt deshalb mit `sameShapeAs` ihre Grundfigur und nutzt deren Modell.

- Die Rezeptprüfung lehnt eine Variante ab, die sich in mehr als Farben, Rolle, Höhe und Namen unterscheidet.
- Beim Erzeugen wird der Bake der Variante mit dem der Grundfigur verglichen. Weichen sie ab, bleibt die bisherige
  Figur unverändert, statt mit falschem Körper zu erscheinen.

### Köpfe tauschen: Figuren in zwei Teilen

Eine Figur mit `"parts": true` wird in zwei Teilen gebaut: **Körper** und **Kopf**, zwei Renderer auf demselben
Skelett mit demselben Material. `FigureComposer.TryPutHead(körper, kopfFigur)` setzt den Kopf einer anderen solchen
Figur auf den Körper: Der Kopf behält sein Modell und seine Farben und hängt an den Knochen des Körpers, sitzt also auf
dessen Hals und folgt dessen Bewegung. Aus 10 Körpern und 10 Köpfen werden so 100 Figuren, ohne ein einziges Modell mehr.

- **Wo getrennt wird:** Das Paket fügt eine Figur zusammen, indem es die Modelle aller Kleidungsstücke hintereinander
  hängt (`BoZoCharacterOptimizer`). Beim Erzeugen schreibt das Werkzeug diese Abschnitte mit Typ und Länge auf
  (`Library/BozoCharacters/Parts/<id>.txt`) und trennt danach genau dort: Kopf, Augen, Brauen, Haare, Bart, Hut und
  Gesichtsteile zum Kopf, alles andere zum Körper. Ein Kleidungstyp, der keiner Seite zugeordnet ist, wird abgelehnt.
  An den Knochengewichten allein ließ sich das nicht entscheiden: Kapuzen und Helmkragen liegen halb am Kopf.
- **Mitgebrachte Knochen:** Haare, Federbüsche oder Kapuzen haben eigene Knochen unter dem Kopf. Fehlen sie dem
  Körper, kopiert `TryPutHead` sie unter denselben Knochen. Gesucht wird nur im Skelett; das Paket legt daneben leere
  Helfer mit gleichen Namen ab (`MagicaClothSupport/FrontHair`).
- **Geht etwas nicht,** bleibt der eigene Kopf des Körpers, und es wird nichts halb angelegt.
- **Gut zusammen passen** Köpfe und Körper derselben Vorlage (`base`) und derselben Hautfarbe; die Hautfarbe steckt in
  den Farben jedes Teils. Der Tausch selbst prüft das nicht.
- Geprüft wird für jedes Paar gleicher Vorlage: Der Kopf bleibt am Kopfknochen und reicht nicht weiter hinunter als
  auf seiner eigenen Figur (ein langer Bart darf bis zum Gürtel hängen), am Hals entsteht kein Loch (der Kopf reicht so
  weit in den Kragen wie der eigene Kopf des Körpers), und er dreht sich als Ganzes mit dem Kopfknochen.
- Ein überschriebenes Modell entlädt das Werkzeug nach dem Speichern, damit der Editor es neu aus der Datei liest:
  Behielt er das alte im Speicher, zeichnete er nach einer Änderung der Eckpunktzahl gar nichts (Figur ohne Kopf oder
  unsichtbar). Für eine schon betroffene Sitzung: `BozoFigureBaker.ReloadGameMeshes()`.

### Der Figurenvorrat (`pool.json`)

Owner-Vorgabe 2026-11-22: rund 200 fertige Menschen für Bewohner und Rollen, vom Knecht bis zur Bergwache in
Vollplatte. `Assets/Editor/BozoCharacters/pool.json` beschreibt jede **Art** von Mensch einmal (Vorlage, getragene
Teile, Körper- und Gesicht) und dazu mehrere **Aussehen** (Hautton, Haarfarbe, Augenfarbe, Farbpalette der Kleidung).
`BozoFigurePool` macht daraus gewöhnliche Rezepte; `BozoFigureRecipes.Load()` liefert sie zusammen mit `figures.json`
und prüft beide gemeinsam.

- **30 Arten × 6 Aussehen = 180 Figuren**, Ids `folk_<art>_<n>`, Visuals `Folk<Art><n>` (z. B. `FolkBauer0`). Das
  erste Aussehen einer Art ist ihr Modell, die übrigen fünf sind Farbvarianten (`sameShapeAs`). Im Spiel stecken also
  30 Modelle; jede weitere Figur bringt nur ihre Farbtextur mit, die Normal-Textur übernimmt sie vom Modell, wenn sie
  Byte für Byte gleich ist.
- **Gruppen:** `arbeit` (Bauer, Knecht, Holzfäller, Förster, Jäger, Bergmann, die Alten – Männer und Frauen),
  `handwerk` (Bäcker, Schmied, Arzt, Händler, Bote, Schneider, Heilerin …), `wache` (Dorfwache, Torwache, Wächterin),
  `krieger` (Kettensoldat, gepanzerte Kämpfer, Bergwache in Vollplatte).
- **Farben über Rollen:** Jedes Teil nennt für jeden Farbkanal eine Rolle (`cloth`, `leather`, `metal`, `boots`,
  `sole` …); die Palette eines Aussehens gibt jeder Rolle eine Farbe. `hair`, `hair_light`, `hair_dark` kommen aus der
  Haarfarbe, `#rrggbb` steht für eine feste Farbe. Kopf und Körper tragen die Farben des Hauttons.
- **Was nicht getragen wird, fällt weg:** Alle Plätze in `wearable`, die eine Art nicht belegt, werden aus der Vorlage
  entfernt; so bleibt kein Teil der Vorlage (Schal, Schulterplatten, Stacheln im Haar) zurück. Die Vorlage für Frauen
  bekommt menschliche Ohren und kein Augen-Make-up (`bases`).
- **Die Prüfung lehnt ab,** was nicht zu bauen ist: unbekannte Teile, Paletten, Haut-, Haar- oder Augenfarben, zwei
  gleiche Aussehen, eine Art ohne Oberteil, Hose oder Schuhe, ein Teil ohne Farbrollen.
- **Nach dem Backen** schreibt das Werkzeug `Assets/Resources/ARE/Visuals/FolkIndex.txt`: je gebackener Pool-Figur
  Visual, Art, Gruppe, Bewegungssatz (m/f) und Hautton. Die Datei liegt bei den privaten Visuals und wird wie sie nicht
  eingecheckt.

### Bewohner tragen Figuren aus dem Vorrat

`FolkFigures` liest `FolkIndex` und gibt jedem Bewohner des Dorfs eine Figur der Gruppe `arbeit`, festgelegt durch
seine laufende Nummer (`Ordinal`) – jedes Mal dieselbe, auf jedem Gerät, ohne Spielstand oder Core zu berühren:

- Die Figuren der Gruppe stehen in einer festen, gemischten Reihenfolge. In der ersten Runde bekommt jeder Bewohner
  eine eigene Figur.
- Ab der zweiten Runde kommen dieselben Körper wieder, aber mit dem Kopf der nächsten Figur gleichen Bewegungssatzes
  und gleichen Hauttons (`FigureComposer`). Aus den 72 Arbeiter-Figuren (je Bewegungssatz und Hautton 14 Männer oder
  10 Frauen) werden so 888 verschiedene Menschen.
- Ohne gebackenen Vorrat (andere Workspaces, CI) tragen Bewohner wie bisher eine der drei Dorf-Figuren.
- Die Datei wird streng gelesen: Ein Visual muss mit `Folk` beginnen und darf nur Buchstaben und Ziffern enthalten,
  fehlerhafte Zeilen werden übersprungen.

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
- `Assets/Tests/EditMode/BozoFigurePoolTests.cs`: Der Vorrat ergibt je Art ein Modell mit Farbvarianten, alle in zwei
  Teilen; jedes getragene Teil bekommt seine Farben, alles Nichtgetragene fällt weg; Wachen, Kämpfer, Boten,
  Schneider und Förster sind dabei; fehlerhafte Vorräte werden abgelehnt; `FolkIndex` nennt nur Gebackenes. Läuft überall.
- `Assets/Tests/EditMode/FolkFiguresTests.cs`: Der Index wird streng gelesen (keine Pfade, keine fremden Visuals);
  die ersten Bewohner tragen je eine eigene Figur, spätere dieselben Körper mit Köpfen gleichen Bewegungssatzes und
  gleicher Haut. Läuft überall.
- `Assets/Tests/EditMode/FigureComposerTests.cs`: Kopftausch für jedes Paar von Modellen gleicher Vorlage (Varianten
  zeichnen die Modelle ihres Modells).
- `ResidentsWearFiguresOfThePool` (PlayMode): Bewohner tragen Pool-Figuren, in der ersten Runde jeder eine eigene; der
  Kopftausch einer späteren Runde funktioniert im laufenden Spiel. Ohne gebackenen Vorrat übersprungen.
- `Assets/Tests/EditMode/BozoFigureVisualTests.cs`: Jede gebackene Figur wird vom Spiel als passives Visual
  angenommen, behält ihr eigenes Material, hat ein Mesh ohne Blend Shapes, eine als Normal-Textur importierte
  Normal-Textur mit nach außen zeigenden Normalen, einen Avatar, der die skalierten Knochen kennt, humanoides Rig mit
  Idle/Gehen, steht auf den Füßen und hat die Höhe ihrer Rolle; Held und Verletzte stammen aus den Figuren; die Waffe
  sitzt in der rechten Hand; der Build-Schutz versteckt das Paket und stellt es wieder her. Ohne installiertes Paket
  werden diese Prüfungen als übersprungen gemeldet, nicht als bestanden.
- Die vorhandenen PlayMode-Tests der Spielfigur (`ArmedRunKeepsTheReviewedGrip` und weitere) laufen mit gesetztem
  `ARE_ENTRY_EVIDENCE_DIR` gegen die neue Figur.
