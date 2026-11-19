# Bau-Modus – Baupläne

Eine Datei pro Baugebiet (`aschengrenze.json`, `krypta.json`, …), geschrieben vom Bau-Modus
(`https://arelogic.space/fadenwerk/?bau`, Unity-Menü *Tools → Ashen Crypt → Bau-Modus*).
Das Spiel lädt sie zur Laufzeit über `BuildLayer.Load` und stellt die Teile als passive Kulisse dar.

```json
{ "schema": 1, "area": "aschengrenze", "revision": 3,
  "pieces": [ { "id": "b0001", "key": "VillageHome2", "x": 3.5, "z": -7.25, "rot": 90, "scale": 1, "w": 0, "d": 0 } ] }
```

- `key`: Baustein aus `BuildCatalog` (Modell unter `Resources/ARE/Visuals/` oder Material unter `WorldSurfaces/`).
- `x`, `z`: Meter in Spielkoordinaten, `rot`: Drehung um die Hochachse in Grad, `scale`: 0,3–3.
- `w`, `d`: nur bei Untergründen (Flächengröße in Metern).

Kulisse heißt: keine Kollision, keine Wege, keine Spielwerte. Diese Regeln bleiben beim Spiel-Kern.
Fehlen die privaten Modelle (öffentlicher Build), erscheinen einfache Platzhalter-Formen.
