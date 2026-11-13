# Bug Assessment: Jäger-Bogenschuss bleibt eine starre Pose

- **Slug**: hunter-bow-animation (autonom aus dem vorhandenen Fehler abgeleitet)
- **Created**: 2026-11-13
- **Source**: Tatsächlicher Unity-PlayMode-Fehler, privat `hunter-red2/tests.xml`.
- **Verdict**: valid
- **Severity**: medium — sichtbare Kampfanimation fehlerhaft; kein belegter Core-/Saveverlust.
- **Status**: reproduziert, noch nicht repariert. Diese Werkzeuganbindung enthält keine Gameplay-Reparatur.

## Report

> The actual bow clip is a still pose: the hunter's arms must move within the attack,
> independently of transition blends. Expected: greater than 0.100000001f. But was: 0.0f.

Native Quelle `9af7842b05bde3157a9461587babbd90a2fe4408`, Unity 6000.6.8f1,
1 Test, 0 bestanden, 1 fehlgeschlagen, 0 übersprungen. XML-SHA256
`2770609741dc8adbeb54de4ad80ab002166dbde74b5859800a377671905d68ad`.
Beim aktuellen Pilot wurden gespeicherte XML und Resultat erneut unabhängig gelesen;
es wurde kein neuer Unity-Lauf vorgetäuscht.

## Symptom und Reproduktion

Der gerettete und rekrutierte Begleiter trifft durch den Core, zeigt aber innerhalb des
tatsächlich gebundenen Bogenclips keine Armbewegung. Erwartet sind Spannen, Lösen und Erholen.

1. Den isolierten Hunter-Teststand oben mit privaten, lizenzierten Visuals öffnen.
2. `ARE_ENTRY_EVIDENCE_DIR` setzen; andernfalls überspringt der Test seine native Abnahme.
3. `AreArpg.PlayMode.Tests.EntryJourneyRuntimeTests.HunterBowShotMovesTheHumanoidArmsAndReturnsWithoutChangingCoreOrSave`
   ausführen: echte Rettung/Rekrutierung, Core-Treffer, Controller-Angriff und Idle-Rückkehr.
4. Der Test prüft 13 Samples des gebundenen Humanoidclips unabhängig von Übergangsblends.
   Die Armbewegung bleibt 0°. Private Dateien, Prefs und Settings blieben erhalten.

## Suspected Code Paths

- `Assets/Editor/WoodlandArtBuilder.cs`: kopiert den ersten regulären Clip aus
  `Vendor/AnimationsStarterPack/Combat/BowShot.fbx` nach `WeaponBowShot.anim`.
- `Assets/Scripts/Runtime/CompanionPresentation.cs`: AttackFlash → Melee;
  EntryStrike-Override nutzt WeaponBowShot, Root Motion bleibt aus.
- `Assets/Scripts/Runtime/EquipmentPresentation.cs`: **auch der Spieler nutzt diesen Clip**.
- `Assets/Editor/EntryCombatArtBuilder.cs`: Idle/Move/Melee und Rückkehrübergang.
- `Assets/Tests/PlayMode/CompanionRuntimeTests.cs` im genannten Hunter-Teststand:
  tatsächlicher RED; der Test ist noch nicht in main integriert.
- `Assets/Scripts/Runtime/HunterEncounterPresentation.cs`: Straßenjäger hat noch keine
  eigene Animator-Bindung; ArrowFlash allein ist keine animierte Schusssequenz.

## Root Cause Hypothesis

Der private ausgewählte Bogenclip ist eine Stillpose (bereits 0,033333 s und 0° gemessen).
Controller-Blends können Bewegung vortäuschen, beheben aber den Clip nicht. Der neue
Hunter-Inari-Meshimport enthält selbst keine Animationen. Keine Änderung des Core-Angriffstakts
ist durch diesen Befund gerechtfertigt.

## Proposed Remediation

Einen tatsächlich bewegten, rechtmäßig verfügbaren Humanoidclip auswählen oder eine eigene
Sequenz erstellen. Den bestehenden Override-Vertrag nutzen und nur das betroffene Artefakt
neu erzeugen, statt den kompletten Waldimport anzustoßen. Der sechs-Tick-Bogentakt bleibt
kanonisch; Animation darf weder Schaden noch Erfahrung oder eine zweite Uhr erzeugen.

**Verbindliche Abnahme nach Reparatur:**

- Ursprünglicher nativer Test: mindestens drei verschiedene Armhaltungen, >0,1° Bewegung,
  tatsächliche Angriff-/Idle-Übergänge und reproduzierbare Samples.
- Spieler-Bogenanimation sowie Ausrüsten und Rückwechsel zu Nahkampf prüfen: gemeinsamer Clip.
- Bestehende Rettungs-/Begleiter-/Ausrüstungsregressionen und reale CoreVerification ausführen.
- Root-/World-/Save-/Prefs-Invarianten erhalten und private Assets unabhängig vergleichen.
- Echte Unity-Ansicht auf lesbares Spannen/Lösen, Griff und Durchdringungen prüfen.
- Separat deklarieren, ob Straßenjäger-Animator und Inari-Modellintegration enthalten sind.

## Risks & Considerations

Numerische Bewegung allein belegt keine gute Animation. Ein Routingtest belegt keinen
behobenen Gameplayfehler. Kein Geräte-/FPS-Nachweis; keine Rohassets im öffentlichen Repo.
Neue Storyentscheidungen sind für diese Reparatur nicht erforderlich.

## Next Step

`$speckit-bug-fix slug=hunter-bow-animation` im geschützten Hunter-Arbeitsstand fortführen.
Dessen unversionierte `docs/PROLOGUE_RELEASE.md` vor einem Merge erhalten.
Erst tatsächliche Änderungen ergeben `fix.md`; erst erneute Ausführung ergibt `test.md`.
Vorher gibt es weder ein `verified` noch einen geschlossenen Animationsfehler.
