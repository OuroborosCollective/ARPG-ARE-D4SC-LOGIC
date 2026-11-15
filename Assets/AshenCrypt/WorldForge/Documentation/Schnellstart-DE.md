# World Forge – Schnellstart und Bedienung

## Voraussetzungen

- Unity 6000.6.x, URP 17. Built-in Rendering geht auch, dann ohne automatisches Terrain-Material.
- Eingebaute Module **Terrain** und **Terrain Physics**. Beim Import fragt World Forge danach (*Tools ▸ World Forge ▸ Enable Terrain Modules*). Fehlen die Module, werden die Terrain-Teile des Pakets gar nicht erst kompiliert. Das Projekt bleibt also immer baubar.
- Optional: Paket *AI Navigation* (`com.unity.ai.navigation`) für den Knopf *Bake NavMesh*. Ohne das Paket gibt es nur einen Hinweis.

## Arbeitsablauf

| Schritt | Knopf im World Designer | Was passiert |
|---|---|---|
| Vorschau | *Render Preview* | Höhen-Stack + Texturregeln als Draufsicht (384 px), ohne Szenenänderung |
| Alles | *Generate All* | Höhen → Kacheln → Texturen → Spawns → POIs → (NavMesh) → Core-Map |
| 1 | *Generate Heights Only* | nur Gelände neu, Texturen/Spawns bleiben |
| 2 | *Repaint Textures* | liest das **aktuelle** Terrain (auch handgemalt) und texturiert neu |
| 3 | *Respawn* | Bäume, Gras, Props und POIs neu auf dem aktuellen Terrain |
| 4 | *Export Core Map* | ganzzahlige Karte für den Core schreiben |
| – | *Clear Spawned*, *New Seed*, *Select World* | Aufräumen, neuer Zufallsstand, Szene fokussieren |

Ausgaben: TerrainData, Terrain-Material und Core-Map in `outputFolder` (Standard `Assets/WorldForgeData/...`). In der Szene liegt ein Root mit `ForgedWorld`, darunter `WF_Terrain_xNN_zNN`, `WF_Spawned/<Regel>/Tile_..` und `WF_POIs`.

## Profil (WorldProfile)

- **Raster:** Kacheln X/Z, Kachelgröße (m), max. Höhe (m), Heightmap-Auflösung (2ⁿ+1), Splat- und Detail-Auflösung, **Seed**, Qualitäts-Preset (PC / Android).
- **Höhen-Stack:** wird von oben nach unten ausgewertet, nicht-destruktiv. Mit *+ Add height layer* fügst du Ebenen hinzu, ▲▼ ändert die Reihenfolge, die Checkbox schaltet ab.
  - *Shape:* Noise, Stamp (Mound, Basin, Crater, RingRidge, Plateau, RidgeLine, TextureStamp), ImportRaw
  - *Erosion:* HydraulicErosion (Tropfen pro Höhenprobe, 0,3–1), ThermalErosion (Talus-Winkel)
  - *Gameplay:* PathCarve (Hohlweg, Schultern, `gradeSmoothing` = sanfte Steigung), FlattenPoi, Boundary (Randklippen, Lücken an Wegen)
  - *Utility:* Remap (Kurve/Clamp), Terrace, Smooth
- **Wege:** Punktlisten in Weltmetern (Ursprung = Südwest-Ecke), Breite, Einsenkung, Schulter, Glättung (Catmull-Rom).
- **POIs:** Id, Art, Position, Radius, Einebnungsradius, Spawn-Freiraum, Tags (z. B. `npc:smith, faction:ashborn`).

## Biom (BiomeProfile)

- **Texture Rules:** TerrainLayer + Gewicht + Maske. Eine Maske kombiniert Höhenband, Hangband, Rauschen, Wegnähe/-abstand und Mulde/Kuppe. Gewichte werden pro Pixel normiert; `textureSharpness` macht die Übergänge härter.
- **Spawn Rules:** Art (TerrainTree / Detail / GameObject), Prefabs bzw. Grastextur, Dichte pro 100 m², Mindestabstand, Freiradius für spätere Regeln, Maske, Pflicht-Textur, Skalierung, Ausrichtung am Hang. Für die Core-Map kommen dazu: *coreObstacle*, Radius und *blocksSight*. Große Objekte sollten zuerst kommen, denn sie reservieren Platz.

## Tipps

- Mit „Würfeln“ wird nichts zerschossen. Zufallszahlen werden pro Punkt vorab gezogen, deshalb verschiebt eine geänderte Maske nicht die ganze Welt.
- Android: höchstens 4 Terrain-Layer (sonst zweiter URP-Pass), Preset *Android_ThirdPerson*, Detaildichte 0,55.
- Baum-Prefabs brauchen eine LODGroup oder einen MeshRenderer am Root. Das Beispiel erzeugt dafür Wrapper unter `WorldForgeData/AshenKrypt/Prefabs`.
- Die Python-Referenz (`Reference/`) rechnet dieselbe Welt ohne Unity nach. Sie eignet sich für schnelle Experimente mit Seeds und Regeln, inklusive Vorschaubildern.
