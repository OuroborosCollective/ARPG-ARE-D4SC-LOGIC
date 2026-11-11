# Ashen Crypt: Krypta und Aschengrenze

## Umfang

Die Szene `AshenEntry` beginnt mit der Krypta. Erst ein vom Core freigegebener
Durchgang nach Kampf und vollständiger Beuteaufnahme führt auf den südlichen
Dorfweg. Dort bleiben Ilyan, Mara und Dorn die vorhandenen drei sozialen Akteure.
Der zweite Akt endet für diese Integration beim Vorratskonflikt. Es gibt keinen
automatischen dritten Akt und keine neue Questbelohnung.

Die ältere Szene `Arena` behält ihre bisherige Kapitelabfolge. Die neue Szene
verwendet denselben `ArenaRuntime` und dieselbe `ArenaSimulation`, mit einem
explizit ausgewählten Einstiegsprofil. Es läuft keine zweite Spielsimulation.

## Eigentümer und Speicher

`CharacterProgression.EntryAct` besitzt den dauerhaften Akt (0 = Altprofil,
1 = Krypta, 2 = Dorf). AC8 schreibt die Katalogrevision/den Akt als `1,1` oder
`1,2` neben die vorhandenen Itembelege und sozialen Daten. Andere Revisionen,
Akte und Krypta-Tiefen werden abgelehnt. AC1–AC5 und AC7 bleiben lesbar; deren
Schreibformate ändern sich für Altprofile nicht. AC6 bleibt reserviert.

Unity verwendet für `AshenEntry` die PlayerPrefs-Schlüssel `ARE_ARPG_entry_v1`
und `ARE_ARPG_entry_v1_backup`. Das vorhandene Charakterprofil wird nicht
überschrieben. Der Aktwechsel schreibt und liest den neuen Checkpoint vor dem
Weltwechsel zurück. Ein Checkpoint speichert Fortschritt, nicht die komplette
laufende Welt: Tick, Position, Cooldowns und laufende NPC-Bewegung werden beim
Neustart wie bisher neu aufgebaut. Replay bedeutet identische Ausgangsdaten,
Seeds, Kataloge und geordnete Tick-Eingaben.

## Vorräte und Beobachtung

`TakeCampRation` ist nur im zweiten Einstiegsakt verfügbar. Die Transaktion
prüft Leben, Maras Bewusstsein, denselben 10-Tick-Interaktionscooldown,
2,4 m Interaktionsradius, freie Sicht und positiven Lagerbestand. Genau eine
Ration wandert vom bestehenden Lager zum Spieler. Mara erinnert den direkten
Übergriff einmal. Dorn erhält diese Erinnerung ausschließlich, wenn er bei der
Tat lebt, höchstens 8 m entfernt ist und keine Gebäudegrundfläche seine Sicht
schneidet. Seine bestehende mutige Policy entscheidet die Reaktion.

Dies erweitert die vorhandene aggregierte `NpcMemory`, nicht den unfertigen
AC6-Ereignisspeicher: keine Gerüchte, episodischen Ereignis-IDs oder globale
Reputation. Der ursprüngliche Fehlbestand von zwei Rationen bleibt ungeklärt;
ein späterer beobachteter Spielerdiebstahl beweist nicht diesen früheren Fall.

## Weltdarstellung

`EntryWorldLayout` enthält zehn undurchlässige Dorfgebäude in Millimetern.
Bewegung und Sichtprüfung verwenden dieselben Rechtecke. Der private
`EntryWorldArtBuilder` passt die sichtbaren Gebäude an diese Grundflächen an.
Häuser und Bäume außerhalb des begehbaren Bereichs bilden die Fernkulisse.
Die Krypta verwendet vorhandene Steinbauteile und die vier etablierten
Core-Hindernisse. Prefabs enthalten ausschließlich Darstellungs-Komponenten;
Unity-Physik ist keine Entscheidungsquelle.

Die Eingangsdaten und erzeugten Prefabs bleiben in ignorierten privaten
Assetverzeichnissen. Auch die Zuordnung der konkreten Paketdateien und die
Materialpaletten bleiben in privaten JSON-Katalogen. Die Palette-Konvertierung
erhält die UV-Anordnung und erzeugt lokale URP-Materialien; fehlende Texturen
werden nicht durch eine angeblich fertige einfarbige Figur ersetzt.
Fehlende erforderliche Eingaben lassen den Bau abbrechen.
Der allgemeine Laufzeit-Fallback ist nur für Entwicklung ohne private Assets
bestimmt; er erfüllt keine visuelle Abnahme.

Ein optionaler privater Katalog `Assets/PrivateArt/entry-combat-art.json`
konfiguriert modulare Krypta-Meshes und ein passives humanoides Spieler-Prefab.
Animationen werden über Dateipfad **und** exakten Clipnamen ausgewählt, weil
eine FBX-Datei zusätzlich eine T-Pose enthalten kann. Der Builder erzeugt
eigene URP-Materialkopien und einen Idle/Move/Melee-Controller; die Originale
bleiben erhalten. Kopierte Clips enthalten keine AnimationEvents, Root Motion
bleibt ausgeschaltet. Die Waffe folgt einem konfigurierten Socket der rechten
Hand; ihre Haltung ist Teil der privaten visuellen Abnahme.
Alle drei Quellclips werden vor Änderung des vorhandenen Controllers validiert.
Materialkopien verwenden Asset-GUID und lokale Datei-ID, damit eingebettete
Materialien ihre eigenen Texturen behalten. Beim Abflachen von Dungeon-Meshes
werden untere LOD-Stufen ausgeschlossen; gemeinsam mit LOD0 verwendete Renderer
bleiben erhalten. Native Editor-Fixtures prüfen diese Authoring-Grenzen getrennt
von den PlayMode-Prüfungen mit den tatsächlichen privaten Charakterassets.

Nur ein vom vorhandenen Core bestätigter Treffer von Strike oder Heavy Strike
löst die kosmetische Schlaganimation aus. Zurückgewiesene Aktivierungen,
Guard und Ruin lösen sie nicht aus. Beide Nahkampfaktionen verwenden zunächst
denselben ausgewählten Clip. Animationen entscheiden weder Schaden noch
Trefferzeitpunkt, Position, Cooldown oder Loot und ergänzen kein Save-Feld.
Ohne optionalen Katalog bleibt die vorherige Darstellung verfügbar.

`stabilizeWeaponGrip` aktiviert optional eine kosmetische rechte Handkorrektur.
Der Builder nimmt Griff und Ellbogen aus dem ausgewählten Bereitschaftsclip
und speichert sie als Transform-Marker am Oberkörper. Die privaten Prefabs
bleiben passiv; erst der eigene Runtime-Adapter hängt `EntryWeaponGrip` an das
validierte humanoide Rig. Ziele unter dem zu lösenden Arm werden abgelehnt.
Die Laufanimation blendet den IK-Einfluss ein; Idle und Melee besitzen Gewicht
null. Die Abweichung zwischen Avatar-Handknochen und Unitys IK-Palmenachsen wird
vor dem ersten eigenen IK-Schreibzugriff am tatsächlichen Animator kalibriert.
Bei weiterlaufender Bewegung führt der Schlag direkt zurück in Move; sonst
kehrt er in Idle zurück. Ein zusätzlicher Idle-Zwischenschritt entfällt.
Der native Animator löst die Handhaltung, während der Core weiterhin
alle Spielentscheidungen besitzt. Fehlende Marker oder alte Controller
aktivieren keinen Adapter. Der IK-Helper-Paketcode wird nicht öffentlich
kopiert und nicht als ungeprüftes Vendor-Verhalten in Prefabs zugelassen.

API-Grundlage: Unity-6 `Animator.SetIKPosition` / `OnAnimatorIK` sowie
`AnimatorControllerLayer.iKPass`. Die tatsächliche Wirkung muss am privaten
Rig geprüft werden; statische Konfiguration allein genügt nicht.

## Nachweise

`EntryJourneyTests` prüft die echte Core-Abfolge, getrennte Altprofile,
deterministische Beute, AC8-Validierung, Save/Load, Replay, Ressourcentransfer
und Wahrnehmungsgrenzen. `EntryJourneyRuntimeTests` führt den vorhandenen Unity-
Adapter einschließlich PlayerPrefs und Szenenprojektion aus. Arrangierte
Gegnerpositionen/HP sind explizite Testfixtures, kein unbeaufsichtigter
Spieler-Durchlauf. Optionale Kameraaufnahmen sind echte Unity-Renderings,
enthalten aber nicht die über OnGUI gezeichnete Bedienoberfläche.
Zusätzliche Detailaufnahmen versetzen ausschließlich die Testkamera zur
Beurteilung von Materialien und Waffenhaltung; sie zeigen keine neue
Spielkamera. Die private Nahkampf-Prüfung muss mit dem konfigurierten Overlay
laufen; ein Skip im öffentlichen Projekt ohne diese Assets ist kein Nachweis.

Revisionsgebundene Ausführungsnachweise stehen im privaten Evidenzlog.
Ein bestandener Core-Test allein belegt weder Unity-Grafik noch Android-
Bedienung, Framerate, Speicherbudget oder Release-Reife.

## Kamera und manueller Zoom

Der Einstieg startet mit 11,5 m Kameraabstand statt 16 m; das Altprofil bleibt bei 22 m.
`ArenaCameraRig` folgt ausschließlich dem projizierten Helden in `LateUpdate`.
Zwei auf freier Weltfläche begonnene Finger steuern den Abstand im Bereich 7–22 m;
Finger-IDs statt Touch-Arrayreihenfolge bestimmen die Geste. Kontakte auf MOVE,
Fähigkeiten und sichtbaren HUD-Panels können nicht nachträglich in eine Zoomgeste
wechseln. Fehlende/abgebrochene Finger setzen die Geste ohne Distanzsprung neu an.
Alternativ stehen Plus/Minus/Reset sowie am Desktop das Mausrad zur Verfügung.
Gebietswechsel behalten die gewählte Entfernung; Reset stellt den Profilstandard
wieder her. Fokusverlust/Pause verwerfen Fingerbindung, nicht den Zoom.
Der Abstand ist eine sitzungslokale Darstellungspräferenz und kein Save-Feld.
Es gibt keine neue Physikabfrage, Kamera-Autorität oder automatische Storykamera.
Native Tests prüfen reale Kamera-Projektion sowie unveränderte Core-Hashes und
Save-Strings während Zoom; Eingabesamples bleiben ausdrücklich Testeingaben.
Ein weiterer Gerätetest wird auf Owner-Wunsch erst mit mehreren fertig
ausgearbeiteten Spielabschnitten und Storyfortschritt gebündelt.
