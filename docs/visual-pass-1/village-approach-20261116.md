# Ashen Crypt – Village Approach / Erd-Pflaster-Federung (16.11.2026)

## Geltungsbereich
Der Quality-Epic #48 wird um einen einzelnen, rein darstellenden Dorfstraßen-Pass ergänzt. Er setzt den geprüften Dorf-Hero-Pass fort und verändert **keine** Spiellogik, Kollision, Navigation, Story, Save-Version, Fable-Kamera oder HUD.

## Echte Integration
- Versionierter Editor-Einstieg: `AreArpg.EditorTools.AreVillageApproachBuilder.ApplyVillageApproach` in `Assets/Editor/AreVillageApproachBuilder.cs`.
- Versionierter URP-Shader: `Assets/Resources/AREVillageApproachFade.shader`, transparent mit Vertex-Alpha und dokumentiertem Texture-Sampling.
- Er nutzt die **realen importierten** Pflaster-Bounds aus `Old village road` und `Supply court paving`. Keine externen Vorlagen, Primitiv-Attrappen oder Quellen mit neuer Authority.
- Vier beidseitig auslaufende Seitenränder (zwei an der Hauptstraße, zwei am Hof) werden zu **einem Mesh mit einem Renderer** gebündelt. Die vorhandene geprüfte Erd-Albedo bleibt die Quelle. Die beiden Übergangsschichten enthalten keine Collider, Lights, MonoBehaviours oder Gameplay-Komponenten.
- Private, git-ignorierte Ausgaben: `Assets/Resources/ARE/Visuals/Village/VillageApproachFeather.asset`, `.mat` sowie das erweiterte `EntryVillageWorld.prefab`. Lizenzierte Unity-Assets, Materialien und Mesh-Rohdateien bleiben auf dem VPS und sind nicht Teil des öffentlichen Git-Baums.
- Idempotenz: Ein vorhandener `VillageApproachPass1` wird auf plausibles Mesh, Material und rein passive Komponenten geprüft und nicht blind überschrieben.

## Testfall und Kausalität
Der tatsächliche native Unity-6.8f1-RED-Lauf `approach-edit-red-02` meldete zunächst 1/1 fehlgeschlagen, 0 übersprungen: `Missing new earth-to-stone visual transition`. Der erste Infrastrukturversuch `approach-edit-red-01` hatte wegen eines nicht traversierbaren Log-Verzeichnisses Exit 127 **vor** der Testausführung; er ist kein RED-Beweis. Nach der minimalen Berechtigungsreparatur wurde die RED-Prüfung neu ausgeführt.

Danach erzeugte `approach-build-01` das private Prefab erfolgreich. `approach-edit-green-01` (1/1), `approach-play-green-01` (1/1) und die frühere unabhängige Hero-EditMode-Regression `approach-edit-hero-regress-01` (1/1) bestanden alle ohne Fehler oder Skips. Im nativen PlayMode blieben Welt-Hash und gespeicherter Progressionsstring beim Rendern gleich. Der Core wurde am neuen Source-Commit unabhängig mit **19.857** Assertions, die Python-Preflights mit **40/40** ausgeführt.

Vorher/Nachher sind echte Unity-Kamerabilder (1280×720). Im zentralen Straßenbereich `x=[24%,76%], y=[45%,98%]` weichen **6,8849 %** der verglichenen Pixel in wenigstens einem RGB-Kanal um mehr als 16 Werte ab. Dieser Messwert beweist eine Bildänderung, nicht einen vom Spieler abgenommenen optischen Qualitätsgrad.

Vollständige Code-SHA, Core/Test-Log-Digests, XML-SHA-256, private Artefakt-Digests, Bild-SHA-256, ursprüngliche RED-Fehlerursache, Lauf-Status und verbleibende Grenzen stehen in `docs/evidence/village-approach-20261116/checkpoint.json`.

## Offen – nächster Block
Gezielte **Dorf-Lichtinseln** mit vorhandener Tageszeitprojektion, realem Nacht-/Dämmerungsbildvergleich, Zeit-/Raum-Cleanup, begrenztem mobilen Lichtbudget und echter PlayMode-Regression sind **noch nicht implementiert**. Weitere offene Grenzen: zusammenhängende Wald-/Mühlen-/Höhlen-Optik, vollständige Unity-Testregression, neuer Android-APK-Build, physischer Samsung-Tab-A9-Test, GPU-/FPS-Messung und PR-Merge. Der private Eigentümer-DOCX wurde nicht ersetzt oder als aktualisiert ausgegeben; seine editierbare Originalidentität war in dieser Sitzung nicht verfügbar.
