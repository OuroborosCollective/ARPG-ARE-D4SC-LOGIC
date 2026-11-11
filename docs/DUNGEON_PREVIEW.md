# Private Dungeon-Vorschau
Der bestehende EntryWorldArtBuilder kann einen versionierten Raumbauplan als privates, passives Prefab erzeugen. Dieses Werkzeug ist ausschließlich für die Gestaltung im Editor bestimmt. ArenaRuntime lädt die Vorschau nicht; neue begehbare Räume, Gegner, Trigger, Truhen und Loot benötigen weiterhin einen eigenen geprüften AreArpg.Core-Vertrag.

## Eingabe und Bedienung
Beispiel: [dungeon-preview-v1.json](examples/dungeon-preview-v1.json).
Im Editor: **ARE ARPG → Art → Preview Dungeon Blueprint...**, JSON auswählen; das erzeugte Prefab wird im Project-Fenster markiert. Es wird nicht in die offene Szene eingefügt.
CLI auf einem freien, isolierten Projekt mit dem vorhandenen Unity-Pin:
```text
Unity -batchmode -quit -projectPath <Projekt> -executeMethod AreArpg.EditorTools.EntryWorldArtBuilder.BuildDungeonPreviewFile -blueprint <Datei>
```
Editor-C#-Schnittstelle: `EntryWorldArtBuilder.BuildDungeonPreview(json)`; Rückgabe ist der tatsächliche Prefab-Pfad. Fehler werden als Exceptions weitergegeben. Der Aufrufer muss echte Prozess-/Testreceipts prüfen; ein Dispatch ist kein Bau-Ergebnis.

Voraussetzung: bereits geprüfte passive `DungeonFloor`, `DungeonWall` und `DungeonArch` unter `Assets/Resources/ARE/Visuals`. Fehlende Assets werden abgelehnt. Es gibt keine Ersatzwürfel. Originale Materialien und Skalierungen bleiben erhalten.

Vorschauversion1 verwendet originale 4-m-Bauteile ohne Skalierung. Abweichende Maße werden abgelehnt. Die spielbare Krypta und ihre bisherigen Einbaumaße werden nicht verändert.

## Schema v1
- `version:1`, `cellMm:4000`; `id` beginnt mit einem Kleinbuchstaben und enthält maximal48 ASCII-Kleinbuchstaben, Ziffern oder Bindestriche.
- `rooms`: eindeutige `id`, linke untere Ecke `x,z` in Rasterzellen, positive `width,length`. Achsenparallel, keine impliziten Eltern oder Reihenfolgenabhängigkeit.
- `doors`: `a,b` sind die angrenzenden Raum-IDs. Leeres `b` bezeichnet eine Außentür. `axis:x` bezeichnet eine waagerechte Wandkante von(x,z) nach(x+1,z), `axis:z` von(x,z) nach(x,z+1).
- Jede Tür ersetzt genau eine vorhandene Wandkante. Interne Türen müssen beide benannten Räume berühren. Gemeinsame Wandkanten werden nur einmal gebaut.
- Maximal16 Räume,64 Türen,256 Bodenmodule; Raumkanten1–16Zellen; Ursprünge zwischen−128 und128Zellen. Überlappungen, doppelte IDs/Öffnungen, unbekannte Räume und unverbundene Raumgruppen werden abgelehnt.
- Dieses Schema enthält bewusst keine Gameplay-Befehle, Persistenzfelder oder ausführbaren Skript-/Methodennamen. Ein LLM darf Eingaben entwerfen, aber keine Prüfung ersetzen.

## Ergebnis, Wiederholung und Schutz
Ergebnis unter `Assets/PrivateArt/DungeonPreviews/<id>.prefab` mit zugehörigem `<id>.receipt.json`; beide sind privat/gitignoriert. Das Prefab enthält ausschließlich Transform-, MeshFilter-, Renderer- und gegebenenfalls LODGroup-Komponenten. Quellen werden vor Instanziierung geprüft.

Raum-/Platzierungsreihenfolge wird ordinal normalisiert, Geometrie in ganzzahligen Millimetern berechnet. Der Receipt bindet den kanonischen Plan, private Quelldateien/Metadaten und das gespeicherte Prefab an Prüfsummen. Ein identischer Plan mit identischen Assets verwendet dasselbe unveränderte Ergebnis. Andere Reihenfolge der Eingabe-Arrays erzeugt keine Duplikate. Ein eigener vorhandener Output wird nur bei gültigem Eigentumsreceipt und unverändertem Dateihash ersetzt; fremde oder manuell veränderte Ergebnisse bleiben erhalten. Ungültige Eingaben werden vor dem Schreiben abgelehnt. Bei Speicher-/Readbackfehlern werden die vorherigen eigenen Dateien restauriert.

Aktive Szenen, EntryCryptWorld, Core-Kollisionen, 100-ms-Tick, XP/Mastery, Loot und AC-Saveformat werden nicht geändert. Ein Editor-Undo für eine aktive Szene ist nicht notwendig, weil die Szene nicht mutiert wird. Das erzeugte Prefab kann separat im Prefab-Modus betrachtet werden.

## Abnahme
Das Beispiel hat3Räume,26Bodenmodule,27Wandmodule und3Bögen, also56Teile.

Native EditMode-Tests prüfen Originalproportionen, passive Komponenten, bekannte Türpositionen, Wiederholung bei umgekehrter Eingabereihenfolge, Schutz der spielbaren Krypta, Fehlerablehnung und Schutz manueller Prefabänderungen. Eine echte Testkameraaufnahme prüft den importierten Inhalt zusätzlich visuell. Core-/PlayMode-Regressionen bleiben getrennte Gates; Android- und Gameplay-Abnahme der neuen Räume wird nicht behauptet.
