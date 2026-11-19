# Bau-Modus – Welt bauen wie in einer Aufbausimulation

Der Bau-Modus ist eine Browser-Oberfläche für den Unity-6-Editor von Ashen Crypt. Man sieht das Spielgebiet
isometrisch von schräg oben (wie in einer Städtebau-Simulation), tippt auf eine freie Stelle, wählt ein Haus, ein
Prop, einen Bewohner, Natur oder einen Untergrund, verschiebt und dreht es und baut es mit **✓ Bauen** fest.
Jede Änderung erscheint sofort im offenen Unity-Editor und, wenn das Spiel läuft, direkt im laufenden Spiel.
Programmierkenntnisse, Inspector oder Skalierungsmenüs braucht man dafür nicht.

## Öffnen

| Weg | Adresse |
| --- | --- |
| Geschützt über den VPS (Tablet, Handy, PC) | `https://arelogic.space/fadenwerk/?bau` (dieselbe Owner-Anmeldung wie Fadenwerk) |
| Lokal am Editor-Rechner | `http://127.0.0.1:8095/?bau#t=<Fadenwerk-Zugangsschlüssel>` |

Im Editor startet *Tools → Ashen Crypt → Bau-Modus → Browser-Oberfläche starten* den Server (es ist der vorhandene
Fadenwerk-Server: derselbe Port, derselbe Zugangsschlüssel, derselbe geschützte Proxy). *Vorschau aus der Szene
entfernen* räumt die Editor-Vorschau weg.

## Bedienung

1. **Gebiet wählen** (oben links). Unity baut das Gebiet als Vorschau im Editor auf und zeichnet daraus die Karte.
2. **Auf eine freie Fläche tippen** → Baumenü mit Kategorien: Häuser, Mauern & Wege, Props, Bewohner, Natur & Wald,
   Untergründe. Oder unten in der Leiste eine Kategorie öffnen.
3. Das Teil erscheint als **Vorschau** (grüner Rahmen) – im Browser und gleichzeitig im Unity-Editor bzw. im Spiel.
   **Ziehen** verschiebt, **⟲ ⟳** drehen um 15°, **90°** um eine Vierteldrehung, **− +** ändern die Größe.
   Grün = passt, Orange = überlappt (erlaubt), Rot = außerhalb des Baugebiets.
4. **✓ Bauen** übernimmt das Teil fest. Die Meldung zeigt die Rückmeldung von Unity, z. B.
   „in Unity sichtbar ✓ (12 Teile)“ oder „live im Spiel ✓“.
5. **Gebautes antippen** → verschieben, drehen, kopieren, abreißen. **↶** macht den letzten Schritt rückgängig.
6. **Untergründe** (Rasen, Dorferde, Pflasterweg, Holzbohlen, Wasser …) mit dem Finger als Fläche aufziehen.
   **Wald-Pinsel**: Kreis setzen, Radius und Anzahl wählen, 🎲 für eine andere Anordnung, **Pflanzen**.
7. **Ansicht** dreht die Karte um 90°, **Karte** zeichnet den Hintergrund neu, **Echtbild** lässt Unity alles
   inklusive des Gebauten rendern. **⬇** lädt den Bauplan als Datei herunter.

Tasten: Q/E drehen · Enter bauen · Esc abbrechen · Entf abreißen · Strg+Z rückgängig · Pfeile/Rad/Pinch für Ansicht.

## Wie es funktioniert

```
Browser (Canvas, isometrisch)  ──HTTPS + Owner-Login──▶  Fadenwerk-Proxy (127.0.0.1:8096)
                                                          │ Zugangsschlüssel
                                                          ▼
Unity-Editor: FadenwerkServer (8095) ─▶ BauModusBridge (/api/x/…)
   1. Anfrage prüfen (Katalog, Baugebiet, Revision)  ─▶ 2. Bauplan schreiben: Assets/Resources/ARE/BuildLayouts/<gebiet>.json
   3. live anwenden: Edit-Modus → Vorschau in der Szene (DontSave) · Play-Modus → laufendes Spiel (BuildLayerLive)
   4. Rückmeldung: tatsächlich in Unity erzeugte Teile + Revision  ─▶ Antwort an den Browser
Spiel (auch APK): ArenaRuntime lädt den Bauplan des aktuellen Gebiets nach jedem Szenenaufbau (BuildLayer.Load)
```

- **Karte und Bausteine kommen aus Unity**: Hintergrund und Sprites (8 Drehrichtungen je Baustein) rendert Unity mit
  derselben Kamera (30° Neigung, 2:1), deshalb passen Browser und Editor geometrisch zusammen. Cache:
  `Library/BauModus` (nicht versioniert).
- **Katalog**: `BuildCatalog` in `Assets/Scripts/Runtime/BuildLayout.cs`. Größen werden aus den echten Modellen
  gemessen. Story-Figuren (Held, Mara, Ilyan, Dorn, Patienten, Jäger, Bär, Gegner), Beute und Weltschalen sind
  bewusst nicht platzierbar. Fehlen die privaten Modelle, erscheinen einfache Platzhalter-Formen.
- **Baugebiete**: feste Gebiete (Aschengrenze, Krypta, Aschestraße, Mühle, Speicher, verborgene Krypta,
  Rattlernest, Aschenzoll, Wald, Bärenhöhle). Prozedurale Dungeon-Ebenen und Reisen haben keinen Bauplan.
  Editor-Vorschau mit echter Umgebung gibt es für Aschengrenze und Krypta; die anderen Gebiete zeigen im
  Edit-Modus nur die Bauplatte – im Play-Modus zeigt die Karte das echte Gebiet.
- **Konflikte**: Jede Änderung trägt die Revision des Bauplans. Hat jemand anderes inzwischen gebaut, lehnt Unity ab
  (409) und der Browser lädt den aktuellen Stand.

## Grenzen

- **Gebautes ist Kulisse.** Kollision, Wege, Sichtlinien, NPC-Verhalten und Spielwerte bleiben beim Spiel-Kern
  (`ArenaSimulation`, `EntryWorldLayout`). Man kann durch gebaute Häuser hindurchlaufen; Bewohner sind Statisten
  ohne Dialog oder KI. Die festen Gebäude der Spielregeln zeigt die Karte rot schraffiert.
- In die **APK** kommt ein Bauplan erst mit dem nächsten Build (er liegt als Datei in `Resources`).
- Gebaute Pläne liegen im Live-Workspace; zum Versionieren `Assets/Resources/ARE/BuildLayouts/*.json` in den
  Source-Workspace übernehmen und committen.
- Bestehende PlayMode-Tests laufen mit dem jeweils vorhandenen Bauplan: zusätzliche Kulisse im Dorf erscheint
  auch dort.
