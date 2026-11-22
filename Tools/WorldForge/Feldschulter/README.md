# Feldschulter der Heilerin

Die drei Lamellen teilen sich einen Materialatlas. Die offene Mohnblüte, der gebundene Stiel, die gerollten Kanten und die Riemen sind Geometrie. Eine weiche Tragekante ist in die Oberflächenkarte eingearbeitet. Die drei Detailstufen belegen je einen Materialslot. Das Modell wird in Metern um das linke Oberarmgelenk aufgebaut. Im fertigen Unity-Modell zeigt rechts nach +X, oben nach +Y und vorne nach +Z.

## Aus den Quellen bauen

Vom Projektstamm lassen sich die Materialkarten mit Python, NumPy und Pillow neu erzeugen. Blender braucht für die Bilder nur Cycles auf der CPU. Das Modell entsteht aus den Profilen und Flächen in `field_pauldron.py`. Die Pfade hinter `--` gehören zum Autorenskript.

```sh
out="$PWD/Assets/Resources/Equipment/Feldschulter"
art="$PWD/Tools/WorldForge/Feldschulter"
review="$PWD/Library/Feldschulter-Review"
python3 "$art/field_atlas.py" "$out"
blender -b -noaudio -t 4 --python-exit-code 11 -P "$art/field_pauldron.py" -- \
  --out "$out" --lookdev "$review" --blend "$review/Feldschulter.blend" \
  --resolution 1100 --samples 24
```

`BaseColor` nutzt sRGB. `MetalSmooth` speichert Metall in Rot und Glätte im Alphakanal. Die Normalmap verwendet Tangentenraum. Das Inventar verwendet die transparente Aufnahme aus demselben Modell.

In Unity erzeugt **Ashen Crypt → Ausrüstung → Feldschulter backen** das URP-Material und das LOD-Prefab. Der Headless-Einstieg ist `AreArpg.Editor.FieldPauldronBaker.Bake`. Wenn der gekaufte modulare Held installiert ist, erzeugt dieser Schritt seine lokale Tragevariante. Beim Ablegen kehrt der Renderer zu seinem ursprünglichen Mesh zurück. Der Kunstexport enthält die selbst erstellten Modelle, Bilder, das Material und das tragbare Prefab.

Der private World-Forge-Eingang nimmt die gepackte `.blend` über `worldforge-blender submit "$review/Feldschulter.blend"` an. `worldforge-blender status JOB-ID` liefert das Ergebnis mit FBX, GLB und glTF. Änderungen an der Optik werden über `FieldHealerKnightDropFitsThroughMotionAndFreshSave` am vorhandenen Charakter geprüft. Setze `ASHEN_SHOULDER_OUTPUT` für die Einzelbilder und den Messbericht. Das Spiel legt Besitz und Tragezustand anhand des aufgesammelten Beutebelegs fest.
