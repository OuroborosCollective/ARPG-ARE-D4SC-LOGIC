# Weltferne für Ashen Crypt

Die Kunst für die 16 gebauten Kartenorte besteht aus 21 nicht begehbaren 3D-Fernkulissen. Die vier Stufen der Seitenhöhle und die drei Stufen der Mühlenkrypta bekommen jeweils eigene Modelle. Zwei separat exportierbare Studien behandeln den König von Immergrün und den Sockel des Glutwächters. Der Gebietsschlüssel der Unity-Präsentation lässt beide Studien aus.

## Autorieren

Die Ortseinträge und Blickrichtungen stehen in `world_catalog.py`. Winkel zählen von Norden im Uhrzeigersinn. Der Maßstab in den Exporten ist Meter. Abstände und Landmarkenhöhen dienen der Bildgestaltung. Kartenbeziehungen beschreiben Zugänge. Die Laufwege und die Freigabe der Tore bestimmt das bestehende Spiel.

Die Quelldateien verwenden als Achsen Osten, Norden und Höhe. Der Export bindet dieselben Dreiecke, drei Positionswerte, vier Materialklassen und die sRGB-Farbe an das Unity- und das glTF-Koordinatensystem. Für die Ferne ist Licht in die Farben eingerechnet. Die Unity-Darstellung übernimmt die Tönung des vorhandenen Himmels. Vier gekachelte Farbtexturen für Steinmauer, Schiefer, Holz und Ascheboden liefern die Oberflächendetails.

Aus dem Projektverzeichnis mit Python 3, NumPy, SciPy und Pillow neu erzeugen:

```sh
python3 Tools/Art/weltferne/surfaces.py --out Assets/Resources/Weltferne/Detail
python3 Tools/Art/weltferne/build.py --out Assets/Resources/Weltferne --models /tmp/ashen-modelle --studies /tmp/ashen-studien --report /tmp/ashen-meshes.json --contact-index Tools/Art/weltferne/footing-index.json
python3 Tools/Art/weltferne/unity_import.py . --source Assets/Resources/Weltferne/Detail
```

## Bodenkontakt

`grounding.py` bestimmt für jedes Motiv die Geländeoberfläche unter der gesamten Aufstandsfläche. Unter Gebäuden entstehen ebene Bauplätze mit einem Übergang zum vorhandenen Hang. Reicht der Platz am vorgesehenen Radius nicht aus, sucht der Export auf derselben Blickachse einen geeigneten Standort. Jeder Baum und jeder Fels wird anschließend am fertigen Gelände eingesetzt.

`ground_anchors.py` benennt die tragenden Punkte der Einzelmodelle. Beim Export werden die geschriebenen Unity-Dreiecke an Fundamentecken, Stützen und Schwellen erneut geschnitten. Die Prüfung berücksichtigt die Tiefe der modellierten Sockel. Hängende Glocken, Wasserräder und erhöhte Speicherböden werden über ihre Aufhängung oder Stützen geprüft. Die drei früheren Fehlerfälle im Glockenhof, am Frachtplatz und an der Aschestraße laufen auch im normalen Python-Preflight des Projekts.

Der laufende Client lädt eine Kulisse pro Gebiet. Türen, Kollisionen, Beute, Gegner, Zufallszahlengenerator und Spielstand gehören weiterhin dem Core. Die Ortsauswahl arbeitet mit den vorhandenen Zuständen für Vorhof und Glockenhof, Ofenhof, Dungeons und Dorfhäuser. Außerhalb der Ashen-Entry-Spielszene wird der Anschluss nicht gestartet. Frühere Spielstände mit fünf oder sechs Seitenhöhlenebenen verwenden für die zusätzlichen Ebenen die tiefste Schieferkulisse.
