# World Forge: erster nachweisbarer Spieleinsatz

Status: geplanter Integrationsblock, noch kein Laufzeitnachweis. Besitzerauftrag:
erst am Spiel einsetzen, Nutzen belegen, danach Welt/Spielstand sichern und die
eigenständigen Werkzeuge für den Verkauf vorbereiten. Fadenwerks offene native
Spielprüfung geht diesem Block voraus.

## Spielbarer Umfang

Der erste Einsatz gestaltet einen Wald-Verbindungsabschnitt auf der Reise von
Aschenzoll zum nächsten Handelsposten außerhalb des Königreichs. Der Ort erhält
vorerst einen beschreibenden Arbeitsnamen; neue Königsnamen, Täter und Schicksale
der Vermissten werden dafür nicht erfunden. Die bestehende Prologwelt bleibt
erhalten. Vorhandene Bäume, Sträucher, rote Pilze, Gegner und Ausrüstung reichen.

Ein begrenztes WorldProfile erzeugt Gelände, Wege und platzierte Vegetation.
Zunächst eine 128-m-Kachel mit 129 Höhenpunkten pro Achse; dies ist ein zu
prüfender Arbeitswert, kein Performanceversprechen. Zwei unterschiedliche Seeds
und die Wiederholung eines Seeds prüfen Variation und Reproduzierbarkeit des
Autorenwerkzeugs. Das vorhandene Werkzeug erzeugt Gelände im Editor. Dieser
Export ist allein noch kein Generator für wechselnde Gebiete zur Spielzeit.

Die Spielanbindung übernimmt die exportierte unveränderliche Ganzzahlkarte samt
Inhaltshash. Der bestehende Core besitzt Bewegung, Sicht, Begegnungen, Beute,
Fortschritt und Speicherzustand; TerrainCollider oder ein zweiter MonoBehaviour-
Controller dürfen diese Regeln nicht ersetzen. Neue Karten werden ausdrücklich
gebunden. Bestehende AC1–AC19-Spielstände behalten ihre bisherige Geometrie und
Geschichte. Ein fehlender oder falscher Kartenhash wird erkannt und nicht durch
die neueste Karte ersetzt.

Der Abschnitt erhält einen sicheren Einstieg, verbundene Ausgänge, mehrere
Kampfgruppen, eine optionale Schatzabzweigung und einen Höhleneingang. Die erste
optionale Höhle verwendet vier Ebenen innerhalb der bestätigten Spanne 3–6.
Die feste Bärenfamilienhöhle und der dreistöckige Rattler-Storydungeon bleiben
eigene Orte. Tiefer liegende Ebenen müssen messbar schwerere Begegnungen und
wertvollere Beute aus dem bestehenden Katalog besitzen. Der Grundriss darf keine
unerreichbaren Gegner, Truhen oder Auf-/Abgänge erzeugen.

Die endgültige Verbindungswelt muss auch die bestätigte Laufzeitregel erfüllen:
äußerer Wiedereintritt beginnt einen neuen Seed/Run mit erneuerten Wegen und
Begegnungen; Ebenenwechsel und Save/Load setzen denselben Run fort. Ein einzelner
gebackener Terrainexport erfüllt diese Anforderung nicht. Seine Einbindung und
die anschließende Core-Generierung müssen getrennt belegt werden.

## Überprüfbare Abnahme

1. Profil, Seed, Werkzeugrevision und erzeugte Karten-/Assethashes protokollieren.
   Gleicher Seed erzeugt dieselben kanonischen Kartendaten; anderer Seed verändert
   das beabsichtigte Gelände. Export und Import bewahren den Inhaltshash.
2. Wege, Spielerradius, Neigungen, Spawnabstände und erreichbare Ausgänge mit
   echten Karten prüfen. Unity-Füße, Gegner und Props liegen auf derselben
   Höhenfläche, die der Core abfragt; keine sichtbaren Nahtkanten oder schwebenden
   Figuren. Ausgewachsene Bäume bleiben mindestens doppelt so hoch wie die Figur.
3. Im tatsächlichen Unity-Spiel hineinlaufen, kämpfen, Beute aufnehmen, einen
   Höhlenabstieg nutzen, speichern und laden. Run, Position, HP, Inventar,
   Begleiterzustand und bereits geöffnete Truhen müssen erhalten bleiben. Kein
   zweiter XP-/Beutebeleg durch Laden; keine erneuerte Storyentscheidung.
4. Nutzen als konkrete Automatisierung ausweisen: erzeugte Geländefläche,
   Höhenpunkte, verbundene Wege, regelkonform platzierte Props und erforderliche
   manuelle Korrekturen. Aufbauzeit tatsächlich messen. Eine Zeitersparnis in
   Prozent nur mit vergleichbarer manueller Baseline angeben.
5. Core-, EditMode-, PlayMode-, Android-Build- und Gerätetests getrennt erfassen.
   Der VPS arbeitet mit Software-Rendering; daraus folgt keine Tablet-FPS-Aussage.
   Höchstens ein aktiver Unity-Editor, gemeinsamer CPU-Deckel, begrenzte Prüfläufe.
6. Nach bestandener Spieleinsatz-Abnahme den zugehörigen Quellstand, Profile,
   generierten Karten, Szenen, eigenes Authoring und den aktuellen Spielstand
   sichern. Wiederherstellung in einer isolierten Kopie mit denselben Hashes und
   demselben gespeicherten Spielzustand nachweisen. Ein Git-Commit allein sichert
   weder private Originalassets noch den Spielstand.

## Spätere eigenständige Pakete

Erst nach dem obigen Nachweis: World Forge USD 35 und Fadenwerk USD 6, jeweils
einmalig. Sauberer Import in ein fremdes Unity-6-Projekt, eigene Beispieldaten,
Anleitung, Lizenzprüfung, Screenshots aus echten Funktionen und professionelles
Cover gehören zur Paketprüfung. Private Drittanbieter-Spielassets werden nicht
mitverkauft. Beschreibung und Bilder nennen nur geprüfte Funktionen; vollständige
Gaia-Pro-Parität und eine fertige allgemeine Fadenwerk-Gameplay-Authority sind
derzeit nicht belegt. Die Store-Einreichung ist ein späterer eigener Block.
