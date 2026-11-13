# Wald, Bärenhöhle und ausrüstbare Beute

## Zuständigkeit und Reise

`AreArpg.Core` besitzt weiterhin die einzige Simulation mit 100-ms-Ticks. `CharacterProgression.Woodland` speichert den Run und dauerhafte Entscheidungen; Unity erzeugt nur die sichtbare Landschaft und sammelt Eingaben. Eine ganzzahlige Karte bestimmt Kollision und Sichtlinien. Die kontinuierliche Höhenfunktion wird in 1-m-Meshes projiziert; sie verändert keine Kampfzeit oder Animationseigentümerschaft.

Die festen Anker sind Mühle, Bärenhöhle und Aschenzoll. Acht Lichtungen, Querverbindungen, 16 Gegner, bis zu vier Truhen und Pflanzen variieren mit dem Seed. Höhlenbesuche, Speichern/Laden und Tod behalten den Run. Verlassen zur Mühle oder Handelsstation und erneuter Eintritt erzeugen einen neuen Seed und neue Kampfbeute. Einmalige Questfakten bleiben erhalten. Der bestehende Jäger-/Bärzustand bestimmt den Kontext der Höhlenbeschreibung.

## Speichern und Kompatibilität

- AC15 erweitert den bestehenden Story-/Dungeon-Spielstand um Woodland-Tokenrevision 1. Die Referenz bleibt Generatorrevision 1. Gespeichert werden Runnummer, Seed, Tick, Gegner, Truhen, ausstehende Beutebelege, Spielercheckpoint und Bärenentscheidung. Ungültige Geometrie, widersprüchliche Entscheidungen und doppelte Beuteordinale werden abgelehnt.
- AC16 umschließt den vollständigen vorherigen, weiterhin geprüften Spielstand als kanonisches UTF-8/Base64 und ergänzt Ausrüstungskatalogrevision 1, erste Ausrüstungs-Beuteordinalzahl und zwei ausgerüstete Ordinale. Beide Prüfsummen werden geprüft. Verschachtelte AC16-Hüllen werden abgelehnt.
- Bestehende AC1–AC12/AC14-Felder und Belohnungen bleiben erhalten. AC13 ist weiterhin reserviert. Alte eingesammelte Schmiedefragmente werden nicht rückwirkend zu neuen Waffen. Ab der ausdrücklich gespeicherten Aktivierung erzeugen neue vorhandene Killbelege Waffen.
- Definitionsreihenfolge, Grundwerte, RNG-Salz und Affixregeln von Katalogrevision 1 sind nach Veröffentlichung unveränderlich. Erweiterungen benötigen eine neue Katalogversion samt historischem Lesepfad; alte Gegenstände dürfen ihr Modell beim Laden nicht wechseln.

## Ausrüstung und Affixe

36 überprüfbare Modellkennungen bilden 33 Waffen und drei Schilde ab: Schwerter, Dolche, Äxte, Stäbe und Bögen aus dem privaten Bestand. Jede Definition besitzt Grundwert, Reichweite, Angriffspause und Handbelegung. Magische Beute trägt einen Affix, seltene Beute zwei verschiedene. Kraft erhöht Schaden, Eile verkürzt die tickgebundene Abklingzeit, Wacht mindert eingehenden Schaden, Zehren heilt entsprechend tatsächlich verursachtem Schaden. Die Abklingzeit wird auf ganze Ticks **aufgerundet**; kleine Eileboni halbieren dadurch keinen Zwei-Tick-Angriff. Gemeinsame Schadensminderung aus Schild, Affixen und Fellmantel ist auf 35 % begrenzt.

Das bestehende Inventar bietet Ausrüsten/Ablegen und zeigt die echten Werte. Zweihandwaffen belegen beide Hände; ein Schild bleibt beim Ablegen im Besitz. Ausgerüstete Gegenstände müssen aus vorhandenen, geprüften Beutebelegen stammen. Der sichtbare Held erhält das entsprechende passive Modell am Hand-/Waffensockel. Der Bogen verwendet den vorhandenen Schussclip; Treffer bleiben im Core. Eigene Animationen für jede einzelne Waffenform und physische Geschossflugbahnen sind nicht Bestandteil dieses Blocks.

## Private Art reproduzieren

Unity 6000.6.8f1: `ARE → Art → Build Woodland and Equipment Bindings`. Das Rezept verlangt die bereitgestellten Wald-Prefabs unter `Assets/PrivateArt/OwnerForestReview`, die drei Waffenpakete unter `Assets/PrivateArt/WoodlandWeapons` sowie das vorhandene Wandererschwert und den BowShot-Clip. Die mitgelieferten Stab-Prefabs liefern ihre ursprünglichen Materialzuordnungen; die rohe FBX allein enthält beim Froststab eine leere Materialreferenz. Es erzeugt sieben Wald-Bindings, 36 passive Waffen-/Schild-Prefabs, Bodenmaterial und Schussclip unter dem ignorierten Visual-Root. Es entfernt keine Originalmodelle und fügt keine Vendor-Gameplay-Skripte ein. Neue Waffentexturen erhalten Android ASTC 6×6, Mipmaps und maximal 1024 Pixel.

Lizenzierte FBX-, Textur- und Prefabdateien gehören nicht in öffentliche Quellen oder ein weiterverkauftes Plugin. Die öffentliche Quelle enthält nur den Modellkatalog, Adapter, Tests und das reproduzierbare Rezept. Ohne privates Overlay sind Art-Abnahmen ausdrücklich nicht erfüllt.

## Prüfgrenzen

Core-Prüfungen durchlaufen erzeugte Karten, echte Bewegung/Kämpfe, Beute, Höhlenbesuch, Stationseintritt, Runwechsel, alte Saves, beide Bärenentscheidungen und Ausrüstung. Native PlayMode-Prüfungen verwenden reale private Modelle, Inventarknöpfe und PlayerPrefs-Readback. Vollständige Revisionen und Ergebniszahlen stehen in `Memory.md` und `ProjectState.md`. Ein Editorlauf belegt weder Android-Framerate noch eine erfolgreiche Ausführung auf dem Galaxy Tab A9.
