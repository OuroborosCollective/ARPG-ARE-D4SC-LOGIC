# World Forge 1.0.1 – Store-Update (Asset Store Paket 430394)

Grundlage: eingereichte Version 1.0.0 (`WorldForge-1.0.0-draft.unitypackage`, SHA-256
`85ff90eb26bd36b93ef19f3edde9eccbd1574baa8e3a98fccf7298c29ef449c3`, Paket 430394 / Version 1558994, Stand
19.11.2026 „pendingReview“). 43 von 45 Einträgen waren GUID- und byte-gleich mit `Assets/AshenCrypt/WorldForge` auf
`main` `488567f`; abweichend waren nur der spätere Review-Fix aus PR #44 (`WFTerrainBuilder.cs`,
`WorldForgeScopedSaveTests.cs`).

Update-Paket: `WorldForge-1.0.1.unitypackage`, 759 210 Bytes, SHA-256
`6aabff4cc0ceca6b22a6ffc8e70c4cb44346a2d467ab4ef2fe1469871d03e34a`, 45 Einträge unter `Assets/WorldForge`, alle GUIDs
erhalten (privat auf dem VPS unter `/var/lib/unityarpg/.local/share/are-arpg/store-drafts-20261119/`, gebaut mit
`build_store_package.py`).

Geändert: `CHANGELOG.md`, `README.md` (1.0.1), `Editor/WFAshenKryptSample.cs`, `Editor/WFTerrainBuilder.cs`,
`Runtime/WFHeightLayers.cs`, `Tests/Editor/WorldForgeEditModeTests.cs`, `Tests/Editor/WorldForgeScopedSaveTests.cs`.

Warum: In 1.0.0 brauchten zwei Tests die Spielszene `Assets/Scenes/Arena.unity` und einer das private
`Assets/Studio/Nature/AshenKrypt`; der RAW-Höhenlayer zeigte standardmäßig auf eine Datei dieses privaten Pakets.
Käuferprojekte haben beides nicht.

Prüfung: World-Forge-Tests im Unity-6000.6.8f1-Spielprojekt 20/20 (mit Natur-Paket läuft auch dessen Prüfung voll);
`unity assets inspect` liest alle 45 Einträge. Ausstehend bis zum Upload: sauberer Import in ein leeres Projekt und
Asset Store Validator.

## Release Notes (für das Publisher-Portal)

**English:** Version 1.0.1: edits (Repaint, Respawn, Clear) now refuse terrain data outside the profile's output folder
instead of leaving changes unsaved. The package tests run in any project (they create their own empty scene and skip
the optional Ashen Krypt nature-pack check when that pack is absent). The RAW heightmap layer starts without a file
path; an empty path skips the layer with a clear warning.

**Deutsch:** Version 1.0.1: Bearbeitungen (Neu bemalen, Neu verteilen, Leeren) verweigern Terrain-Daten außerhalb des
Ausgabeordners, statt Änderungen ungespeichert zu lassen. Die Paket-Tests laufen in jedem Projekt (eigene leere Szene,
die optionale Prüfung des Ashen-Krypt-Natur-Pakets wird übersprungen, wenn es fehlt). Der RAW-Höhenlayer startet ohne
Dateipfad; ein leerer Pfad überspringt den Layer mit klarer Warnung.

Owner-Entscheidung 19.11.2026 (C): Die laufende Prüfung von 1.0.0 bleibt bestehen. Nach der Freigabe legt der Owner im
Publisher-Portal eine neue Version an; dann wird 1.0.1 hochgeladen (vorher sauberer Import + Validator).
