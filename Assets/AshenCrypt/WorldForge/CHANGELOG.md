# Changelog

## 1.0.1 – 2026-11-19
- Fix: Edits (Repaint, Respawn, Clear) refuse terrain data that lies outside the profile's output folder instead of
  leaving those changes unsaved (for example after the output folder was changed).
- Fix for Asset Store installs: the package tests no longer need a scene of the host project (they create their own
  empty scene) and skip the optional Ashen Krypt nature-pack check when that pack is not installed.
- The RAW heightmap layer starts without a file path (previously a path of the private nature pack); the sample world
  sets its own path. An empty path skips the layer with a clear warning.

## 1.0.0 – 2026-11-14
- Erste Version: Höhen-Stack (Noise, Stempel, RAW-Import, Erosion, Wege, POI-Einebnung, Randklippen), Texturregeln, Spawner (Terrain-Bäume, Details, Prefabs), POIs, World Designer, Core-Map-Export (`ForgedWorldMap`), Ashen-Krypt-Beispielwelt, 9 EditMode-Tests.
- Ersetzt den Blender-Weltgenerator v004 und den nicht kompilierenden `AshenKryptWorldBuilder.cs`.
- Status: Python-Referenz geprüft; Unity-Editor-Kompilierung und Android-Messung stehen aus.
