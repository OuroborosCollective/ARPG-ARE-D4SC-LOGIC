# World Forge — Quick start / Schnellstart

Status: instructions for the current development draft.

1. Import `WorldForge-1.0.0-draft.unitypackage` into a backed-up Unity 6 project. Files appear under `Assets/WorldForge`.
2. Use Unity’s package/module configuration to ensure Terrain and Terrain Physics are available. URP 17.6.0 was used for the current draft. AI Navigation is optional.
3. Open **Tools > World Forge > World Designer** and create or assign a `WorldProfile`.
4. Start small: one tile, a modest heightmap resolution and a known seed. Add noise or a mound layer to the height stack. Use **Render Preview** before modifying a scene.
5. Add paths and POIs at positions appropriate for the profile’s world dimensions. Configure flattening and clear space around key locations.
6. Assign a `BiomeProfile`. Add your own TerrainLayers for texture rules and your own tree/detail/prop assets for spawn rules. Generation without these assets does not supply a finished forest art set.
7. Choose a dedicated output folder and use **Generate All**. Inspect the terrain, path slopes, generated objects and POI positions.
8. Use **Repaint Textures** or **Respawn** for those stages; **Generate Heights Only** changes the terrain shape. Save the intended scene and generated assets.
9. Export the core map. Load its bytes with `AshenCrypt.WorldForge.Core.ForgedWorldMap.FromBytes(bytes)`. Connect `CanStand`, `HeightMm`, `HasSight` and POI queries to your own gameplay code.
10. Keep the profile, seed and exported map together with your world’s revision. Bind your save to the map hash if your game must reject a different world layout.

## Existing documentation
See `Documentation/Schnellstart-DE.md` and `Documentation/Integration-DE.md`. Their Ashen Crypt integration example is project-specific and does not mean that the game is included.

## Draft limitations
The Ashen Krypt sample helper references private game assets that are not included. Use your own profile and art until the standalone sample is supplied. One Editor regression references a game scene unavailable in a clean import. Module setup and licensing/documentation need final store review. These are known release gates; the current package is held as a draft.

## Deutsch
Profil erstellen, kleinen Seed-Test erzeugen, Wege und wichtige Orte anlegen und eigene Texturen/Bäume/Props zuweisen. Die ganzzahlige Core-Map ist eine Integrationsschnittstelle; Kampf, Loot, Quests und Höhlen gehören nicht automatisch zum Werkzeug. Das Verkaufsmotiv ist eine Illustration, kein mitgeliefertes Landschaftsmodell.
