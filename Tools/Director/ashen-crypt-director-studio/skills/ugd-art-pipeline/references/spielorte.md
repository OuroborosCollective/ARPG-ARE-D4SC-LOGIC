# Spielorte und CPU-Bildprüfung

Die zehn festen Spielorte haben eigene Baupläne in `Assets/Resources/ARE/BuildLayouts`. Öffne zuerst den Bauplan und bestimme den Blick vom Weg, vom Questziel oder von der Tür. Verwende die existierenden regionalen Prefabs unter `Assets/Resources/ARE/Borderland`. Der Runtime-Einstieg `BuildPlacedLayer` setzt sie auf den Boden. In der Mühle orientiert sich die Gestaltung am Rad bei (7,53 m, 7 m). In der Aschengrenze soll die Marktüberdachung die interessanten Details vom Weg aus sichtbar lassen. Im Speicher genügt eine beleuchtete Stelle am Siegel. Bei generierten Reisen und Dungeon-Ebenen entsteht die Geometrie aus dem vorhandenen Seed. Dort tragen dieselben regionalen Bauteile die Oberflächen.

Der private Projektstand mit den originalen Store-Abhängigkeiten für diese Kunstrunde liegt unter `~/.local/share/unity-game-director/art-lab/20261120/proof/editor`. Seine Ausgangsrevision ist `c600181d42da666c4ac0aa6ade4489ef4903e24a`. Die Autorenwerkzeuge liegen neben dem Projekt unter `proof/chapter-work`, relativ zur Laborwurzel. `place.py` prüft die Ausgangspläne und erkennt eine fremde Zwischenänderung. `RegionalArtPass.cs` baut das zusätzliche Relief und die Feuerkarten an die vorhandenen LOD-Gruppen. `RegionalJourneyReview.cs` nimmt die tatsächliche Spielkamera und eine Detailaufnahme auf. Der Vergleich blendet nur die neuen Detailobjekte aus. Er kontrolliert Save und Welthash in derselben Szene. Die Bilder und das geprüfte Unity-Paket sind auf dem Guacamole-Desktop im Ordner `Ashen-Crypt-Werkansichten` verfügbar.

Die ARE SoftwareGPU unter `tools/are-softwaregpu` dient dem zweiten Blick auf LOD-Wechsel. `tools/softwaregpu-workbench/scripts/workbench.py inspect` erfasst ihre Quellrevision und Projektflächen. Für eigene GLBs mit je einem indizierten Dreiecksmesh pro Detailstufe, eingebetteter Basiskarte und gebackenen Objekttransformationen verwendet der Director diesen Aufruf im Material-Workbench-Python:

```bash
lab="$HOME/.local/share/unity-game-director/art-lab/20261120"
"$lab/tools/material-env/bin/python" scripts/softwaregpu_review.py \
  --engine "$lab/tools/are-softwaregpu" --models "$lab/proof/release-model" \
  --output "$lab/proof/lod-pruefung" --asset Schieferbruch
```

Der Vergleich rastert die drei Stufen bei gleicher Kameraposition und schreibt pro Bild die Farb- und Tiefenprüfsumme sowie die Überdeckung der Silhouette gegenüber LOD0. Für weitere Modelle `--asset` wiederholen. Die Ausgabe einer Figur ist die ungestellte Meshform. Deformation und Bodenkontakt separat mit dem echten Animator prüfen. Beleuchtung, Feuer, Materialglanz und Partikel in Unity beurteilen.

Der Held trägt bereits einen Fellumhang am Brustknochen, sobald `Woodland.ArmorCrafted` erfüllt ist. Neue Rüstung am vorhandenen Avatar und an den vorhandenen Animationen anpassen. Der bestehende Test `CanonicalKillProjectsAndPersistsVersionedLoot` führt einen Gegnerdrop über Aufnahme, Inventar und Laden des Spielstands. Eine neue Rüstung braucht zusätzlich den Nachweis ihrer eigenen Gegenstandskennung, Ausrüstung und sichtbaren Bindung.
