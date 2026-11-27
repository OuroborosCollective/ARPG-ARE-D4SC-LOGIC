# Prefab-Wurzeln in Unity 6 prüfen

In einer Unity-6-Paketkopie kann das Team einen Produktordner unter `Assets` prüfen und geeignete statische Prefabs korrigieren. Erprobt mit 6000.6.8f1. Das Werkzeug wird nur im Editor kompiliert. Es läuft auf ausdrücklichen Menü- oder Batch-Aufruf.

## Paket einbinden

In einem eigenen Testprojekt die benötigte Renderpipeline und die Importer des Produkts installieren. Das Verkaufsarchiv dort importieren. Über den Package Manager „Install package from disk“ die Datei `Tools/StorePrefabReview/package.json` dieser Werkzeugkopie wählen. Den Produktordner auswählen und im Assets-Menü **Store Prefabs → Inspect selected product folder** aufrufen. Der JSON-Bericht enthält Asset-Pfad, GUID, Prefab-Typ, lokale Position, Quaternion und Skalierung für jede gespeicherte Prefab-Wurzel. Das Werkzeug meldet leere Ordner und fehlgeschlagene Importe als Fehler.

## Korrektur in einer Wegwerfkopie

Für die Korrektur eine Datei `StorePrefabReview.copy` neben den Ordnern `Assets` und `ProjectSettings` der Wegwerfkopie anlegen. Die erste Zeile enthält den absoluten Projektpfad ohne abschließenden Schrägstrich. Die zweite Zeile benennt exakt einen Produktordner, etwa `Assets/Aschenwacht`. Vorhandene Projekte mit Spiel- oder Kundendaten eignen sich zur schreibgeschützten Prüfung. In der dafür angelegten Kopie **Store Prefabs → Repair marked package copy** ausführen.

Die Korrektur hängt das bestehende Objekt mitsamt seinem bisherigen Transform als Kind unter eine neutrale Wurzel. Die ursprüngliche Transformationsfolge bleibt einschließlich Spiegelung, nichtuniformer Skalierung und Neigung erhalten. Die Dateipfade und Meta-Dateien bleiben am gleichen Ort. Beim Modell-Prefab wird die äußere Datei ein reguläres Prefab mit einer verschachtelten, weiterhin verbundenen Modellinstanz. Ein Zugriff auf einen Renderer direkt an der neuen Außenwurzel erreicht den Renderer künftig über das Kind.

Die automatische Korrektur unterstützt statische, textserialisierte reguläre Prefabs sowie einfache, auf einem importierten Modell basierende Varianten. Animation, Skripte, dynamische Physik, eine nicht unterstützte Hierarchie oder eingehende Asset-Verweise auf ein zu änderndes Prefab erfordern eine manuelle Migration. Der Durchlauf prüft alle Kandidaten vor der ersten Änderung. Nach jedem Austausch importiert Unity das Prefab neu und vergleicht bestehende lokale Objekt-IDs, Weltmatrizen, Meshes, Materialreferenzen und Collider-Daten. Bei einer fehlgeschlagenen Prüfung stellt der Editor die bereits ausgetauschten Prefab-Dateien wieder her. Das Originalarchiv ist die Rückfallebene bei einem Prozess- oder Rechnerabbruch. Bereits normalisierte Prefabs werden übersprungen.

Der Abhängigkeitscheck erfasst die in diesem Projekt importierten, serialisierten Asset-Verweise. Code mit dynamischem Laden, externe Kundenprojekte und dort gespeicherte Überschreibungen brauchen einen gesonderten Aktualisierungstest.

## Batch und Regression

Die Umgebungsvariable `PRODUCT_ROOT` enthält denselben Unity-Asset-Pfad wie im Menü. Die folgenden Einstiegspunkte eignen sich für Unity mit `-batchmode -nographics -projectPath <Paketkopie> -executeMethod <Methode> -quit -logFile <Log>`.

| Methode | Ergebnis |
| --- | --- |
| `Ouroboros.StorePrefabReview.RootReview.AuditBatch` | JSON-Bericht im Unity-Log |
| `Ouroboros.StorePrefabReview.RootReview.RepairBatch` | Korrektur mit Zahl geänderter Prefabs im Log. Bei verweigerter Korrektur endet der Aufruf mit einem Fehler. |

Für die EditMode-Tests in einem separaten, unmarkierten Testprojekt das Paket in `Packages/manifest.json` unter `testables` eintragen. Mit `-runTests -testPlatform EditMode -assemblyNames Ouroboros.StorePrefabReview.Tests -testResults <Datei.xml>` starten. Dieses Projekt benötigt den Unity Test Framework und das Physics-Modul.

Den Kandidaten anschließend in ein zweites leeres Unity-Projekt importieren, den offiziellen Validator auf den gesamten Produktordner ausführen und die produktbezogenen Readbacks mit dem Ausgangsarchiv vergleichen. Das Editor-Werkzeug gehört nicht zum Verkaufsarchiv.
