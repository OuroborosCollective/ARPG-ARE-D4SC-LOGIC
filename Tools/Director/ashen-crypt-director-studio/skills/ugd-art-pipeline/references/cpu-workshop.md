# CPU-Autorensystem und Übergabe

Der Director liegt auf dem delegierten VPS unter `~unityarpg/.local/share/unity-game-director`. Das Arbeitsprofil dieser Version liegt darunter in `art-lab/20261120`. Unter `sources` stehen Original-Checkouts, unter `tools` die ausführbaren Werkzeuge und unter `proof` die laufgebundenen Ergebnisse. Bundles enthalten die eigenen Adapter. Die Arbeitskopien der Originalanbieter bleiben beim ihnen zugeordneten Commit. Alle Versionswechsel benötigen einen neuen Eintrag im Quellenkatalog und einen neuen Arbeitslauf.

| Aufgabe | Verwendbare Originaltechnik | Test am eigenen Objekt |
| --- | --- | --- |
| Monster | arjun988 Creature Artist, EL4CTEO Sculpting und Character Rigging, ECC Motion State Inspection | Silhouette aus Front, Seite und Dreiviertel, Gelenkachsen, vollständige Gewichte und Bodenabstand jedes Animationsframes |
| Rüstungen, Waffen und Kleidung | Behalte Avatar, Bones, Bindpose und Clips aus dem gekauften modularen Unity-Paket. Verwende Clothing, Hard Surface, Atlas UV Fitting und Motion State Inspection. | Prüfe Schulter, Hüfte, Saum, Hand-/Waffensockel und Stiefel in den vorhandenen Idle-, Lauf- und Kampfposen. MPFB bleibt als separat getestete Option für ausdrücklich verlangte neue Grundkörper verfügbar. |
| Props, Level und Vegetation | calebrisc modelkit, EL4CTEO Environment Kits, arjun988 Set Dressing und Vegetation, Unity Craft Game Floors | Metrische Öffnung, bündige Anschlüsse, Kontakt zur Laufebene, Blickachse und Vergleich aus Spielhöhe |
| Materialien | Mat-Baker mit Unity-URP-Preset und Objekt-UVs, TexUtil CPU-CLI, Material Workbench und MaterialX | Albedo in sRGB, Normal als Normal Map, Metall in Rot und Glätte in Alpha der Unity-Karte. glTF-ORM führt AO, Rauheit und Metall in RGB. Prüfe Kachelung unter Streiflicht. |
| Shader und Funken | Offizieller Unity-Skill für HLSL-Knoten in Shader Graph, LlamAcademy Embers als im Editor importierte Referenz, Unity Particle System | Native Shader-Fehler prüfen, sichtbare Effekte auf der Projektskala und begrenzte Zahl lebender Partikel erfassen. `Reduced Effects` beim Einbau in eine Spielszene berücksichtigen. |
| Feedback und Mobilprofil | RobLe3 Multiview Fit Loop und Quality Refinement, EL4CTEO Mesh Stats/LOD/QA, OpenAI Web 3D Asset Pipeline | Wiederholbare Kameras, nur die richtige LOD gleichzeitig, lesbare Oberfläche in der Entfernung. Die gemessene Editorlaufzeit beschreibt das Tablet separat. |

## Ausführung

Ein normaler Auftrag liest zunächst die vorgeschlagenen Techniken am Host. Dort, als `unityarpg`, kann die vorgefertigte Werkprobe so erzeugt werden. Verwende ein frisches Ausgabeverzeichnis je Entwurf.

```bash
director="$HOME/.local/share/unity-game-director"
lab="$director/art-lab/20261120"
skill="$director/specialists/ugd-art-pipeline"
author="$lab/tools/blender-5.2.4-linux-x64/blender"
out=$(mktemp -d "$lab/proof/work-XXXXXXXX")
python3 "$skill/scripts/art_work.py" verify
"$author" -b -t 3 --factory-startup --python-exit-code 12 -P "$skill/scripts/specimens.py" -- --lab "$lab" --out "$out" --size 1024
"$author" -b -t 3 --factory-startup --python-exit-code 12 -P "$skill/scripts/lookdev.py" -- "$out" "$out/views"
job=$(worldforge-blender submit "$out/Aschenhorcher.glb")
worldforge-blender status "$job"
```

Für MPFB und Mat-Baker zusätzlich `BLENDER_USER_RESOURCES="$lab/profile"`, `BLENDER_USER_EXTENSIONS="$lab/profile/extensions"` und `BLENDER_USER_CONFIG="$lab/profile/config"` setzen. Das gespeicherte Profil aktiviert die Erweiterungen. Mat-Baker im Headless-Betrieb auf dem tatsächlichen UV-Objekt testen. Bei der temporären Bake-Ebene trat im Blender-5.2-Abschluss ein Fehler beim Wiederherstellen der Objektauswahl auf. Den Rückgabestatus und die ausgegebenen Bildkanäle prüfen.

Die getesteten Hilfen liegen unter `tools/tex-build/texutil`, `tools/material-env/bin/material-workbench` und `tools/dcc-packages`. Der DCC-Adapter ist über einen kurzlebigen lokalen Blender-Prozess testbar. Direkte bpy-Aufträge funktionieren ohne offenen Server. Für einen neuen Auftrag liefert `read` das jeweilige Arbeitsverfahren und die vorhandenen Originalbeispiele. Das Video-Kameratracking verlangt einen Clip-Editor und ist für das CPU-Assetprofil entbehrlich.

Blender 5.2.4 ist das Autorenwerkzeug. Reiche sein selbstenthaltendes GLB in den bestehenden Blender-4.5.16-Worker ein. Nach Status `ready` die SHA-256 aus `receipt.json` gegen Quelle und Ausgaben nachrechnen. In der isolierten Unity-Arbeitskopie importiert die vorhandene API das FBX. Die optionale Editor-Prüfung in `assets/AshenArtIntakeProbe.cs` erwartet die vier Zeilen `Assetname Job-ID` in `ASHEN_ART_JOBS`, die vier Karten albedo, normal, metalSmooth, emission und den originalen Embers-Graph im Reviewordner `Assets/PrivateArt/ArtTechniqueReview`. Sie schreibt das Ergebnis an `ASHEN_ART_RECEIPT`. Der Prüflauf lässt die Spielkapitel unberührt.

## Asche, Glut, Feuer und Lava

Der Umgebungsdurchlauf verwendet den vorhandenen Schneeasche-Shader samt Originalmaske des Spiels, den vorhandenen Flammenshader und das Atmosphärenmaterial für aufsteigende Funken. Die eigene schmale Lavaader verbindet die CPU-gebackene Krustenmaske mit dem URP-Shader in `assets/CrustedLava.shader`. In einer realen Spielszene kommt die visuelle Stärke von der vorhandenen Effekteinstellung. Der prüfbare Umgebungsausschnitt bleibt eine private Autorenszene.

`scripts/environment_maps.py` erzeugt eine kachelbare Basaltoberfläche und eine Fugenmaske im Alphakanal, eine Tangentennormale und eine weiche Funkenkarte. Verwende die Python-Umgebung von Material Workbench. Der Editor-Prüflauf `assets/AshenEnvironmentProof.cs` setzt auf dem gespeicherten Ossuar-Prefab auf. Kopiere die drei Karten und den Lava-Shader in denselben Reviewordner, den C#-Prüfer unter `Assets/Editor`, setze `ASHEN_ENV_CAPTURE` auf einen privaten Bildordner und rufe `AshenCrypt.ArtReview.AshenEnvironmentProof.Run` im Editor auf. Er erfasst dieselbe Projektkamera mit allen Effekten, abgedunkelt und mit isoliertem Lavasaum. Die Projektprobe prüft die sichtbare Pixeländerung und speichert die Szene wiederverwendbar im Reviewordner.

Material Maker 1.8 liegt separat unter `tools/material-maker`. CharMorph lässt sich auf dem Blender-4.5-Autorenprofil mit seiner eigenen Datenbank laden. Der Standardweg zur Anpassung eines NPCs ist das vorhandene Unity-Prefab.

## Weitere Quellen aus dem Auftrag

Material Maker und MaterialPilot sind separate Materialeditor-Anbindungen. CharMorph ist eine alternative Figurenbasis mit eigener Datenbank. Deren lokaler Teststatus steht im Hostbeleg. InnoGames Mobile Toolkit, Flying Embers und Unity-Open-MCP dienen dem Vergleich von Effekten und Werkzeugen. Flying Embers setzt auf VFX Graph. Die saubere mobile Arbeitsprobe verwendet den bestehenden URP-Pfad. sandraschi, djeada, bpy-dev und ahujasid bieten alternative Blender-Brücken. Für diese Installation reicht der gewählte DCC-Adapter. AmbientCG und Poly Haven sind optionale Fremdquellen für konkrete Materialien oder HDRIs. Der Prüfstand verwendet selbst erzeugte Oberflächen.

Die verlinkten neuronalen Bild-zu-Mesh- und Auto-Rig-Verfahren (TRELLIS.2, TripoSR, Stable Fast 3D, SPAR3D, InstantMesh, Hunyuan3D, UniRig und SkinTokens) haben hier keinen nachgewiesenen CPU-Produktionspfad. Meshy, Tripo-API und Rodin benötigen externe Verarbeitung. Die drei Forschungsarbeiten liefern Kriterien für Geometriequalität, Deformierbarkeit und Teilkontrolle. Keiner dieser Einträge ist als ausgeführtes lokales Werkzeug registriert.

## Einen bestehenden Abschnitt selbst prüfen

Öffne eine isolierte Arbeitskopie mit dem nativen Editor. Der rein lesende Batch-Einstieg `AshenCrypt.ArtReview.AshenSceneReview.Run` erhält `ASHEN_REVIEW_SCENE` (eine echte .unity-Datei unter Assets) und `ASHEN_SCENE_RECEIPT` (den Ausgabepfad). Die Editor-Datei liegt unter `assets/AshenSceneReview.cs`. Das Ergebnis nennt konkrete Hierarchiepfade für defekte Shader, fehlende Partikelmaterialien und unvollständige Hautbindungen und ordnet ihnen einen Technikweg zu. Bei einer rein zur Laufzeit aufgebauten Szene danach die aktive Spielkamera aus dem vorhandenen Director verwenden. Die gespeicherte Szene beantwortet nur Fragen über ihre enthaltenen Objekte. Bei technisch fehlerfreier Szene vergleiche zwei gleiche Kameras mit aktivierten und reduzierten Effekten; entscheide visuell über die Lesbarkeit.
