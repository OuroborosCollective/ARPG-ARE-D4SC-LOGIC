# Core-Integration (ARE / Ashen Crypt)

World Forge erzeugt zwei Dinge:

1. **Kulisse in Unity:** Terrain-Kacheln, Splatmaps, Terrain-Bäume, Details, Prefabs, `ForgedPoi`-Marker. Das ist reine Präsentation.
2. **Core-Map** (`WF_<Profil>_CoreMap.bytes`): eine ganzzahlige Karte für die autoritative Simulation. Sie wird bei *Generate All* bzw. *Respawn* geschrieben, wenn *exportCoreMapAfterBuild* aktiv ist, oder per Knopf *Export Core Map*. Die Datei wird am `ForgedWorld`-Root als `coreMap` verknüpft.

## Format (Version 1, little-endian)

`"WFMAP"`, Version (byte), OriginX/Z (int64 mm), Abstand (int32 mm), Knoten X/Z (int32), Höhen (int32 mm je Knoten), begehbare Zellen (Bitfeld), Hindernisse (x, z int64 mm, Radius int32 mm, Flags byte: 1 = blockiert Bewegung, 2 = blockiert Sicht), POIs (Id, Art, x, z, Radius, Tags), am Ende **FNV-1a 64** über alle Bytes davor. `ForgedWorldMap.FromBytes` lehnt falsche Magie, Version, Länge oder Prüfsumme mit `FormatException` ab. Nichts wird stillschweigend repariert.

Begehbar ist eine Zelle, wenn ihr Gefälle ≤ `maxWalkableSlope` ist (Standard 38°). Die Klippen am Rand und steile Kraterwände sind damit automatisch Wände.

## API (`AshenCrypt.WorldForge.Core`, `noEngineReferences`)

```csharp
ForgedWorldMap map = ForgedWorldMap.FromBytes(bytes);
bool ok   = map.CanStand(xMm, zMm, ArenaSimulation.PlayerRadiusMm); // Zellen + Hindernisse
long y    = map.HeightMm(xMm, zMm);                                  // ganzzahlig bilinear
bool see  = map.HasSight(axMm, azMm, bxMm, bzMm);                    // 250-mm-Schritte wie WoodlandLayout
var camp  = map.FindPoi("ashborn_camp");                             // Id, Kind, Xmm, Zmm, RadiusMm, Tags
var homes = map.FindPois(ForgedPoiKind.NpcHome);
ulong h   = map.ContentHash;                                         // für World-Hash / Save-Bindung
```

Ohne Gleitkomma, Uhr oder RNG. Damit sind die Regeln aus `AGENTS.md` für autoritative Logik erfüllt.

## Anschluss an den bestehenden Core (Vorschlag, nicht automatisch ausgeführt)

1. `Assets/Scripts/Core/AreArpg.Core.asmdef` → `"references": ["AshenCrypt.MemoryForge", "AshenCrypt.WorldForge.Core"]`.
2. Eine Zone-Klasse analog zu `WoodlandLayout` hält eine `ForgedWorldMap` und leitet `CanStand/HeightMm/HasSight` daran weiter. Spawns und NPC-Anker kommen aus `map.Pois` (z. B. `SpawnZone`, `NpcHome`, `NpcWorkplace` für Memory-Forge-Tagesabläufe).
3. Die Runtime lädt die Bytes über `ForgedWorld.coreMap.bytes` (oder `Resources`) und übergibt sie dem Core. Der Core liest keine Unity-Objekte.
4. `Tests/CoreVerification/CoreVerification.csproj`: `<Compile Include="../../Assets/AshenCrypt/WorldForge/Core/*.cs" LinkBase="WorldForge" />`. Dann kann der .NET-Lauf die Karte ohne Unity prüfen.
5. Save-/World-Hash: `map.ContentHash` mischen, damit ein Save nicht still auf eine andere Karte geladen wird.

Story- und Gameplay-Entscheidungen (welche Zone, welche Gegner wo) bleiben beim Besitzer. World Forge liefert nur Geometrie und Anker.

## Prüfung

- `Tests/Editor/WorldForgeEditModeTests.cs`, 9 Tests. Golden-Werte für Seed, RNG und Rauschen, Poisson, Determinismus des Höhen-Stacks, Kachelkanten, Core-Map-Roundtrip, Ablehnung beschädigter Daten. Dazu kommt **ein exakter Abgleich der mitgelieferten Referenzkarte mit der Python-Referenz** (48 Höhen, 48 CanStand, 38 Sichtlinien).
- Der Unity-Editor-Lauf dieser Tests steht noch aus (siehe LIES_MICH_ZUERST).
