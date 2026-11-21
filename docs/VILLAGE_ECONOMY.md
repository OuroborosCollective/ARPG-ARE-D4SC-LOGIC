# Dorfwirtschaft von Aschengrenze

Die Bewohner von Aschengrenze arbeiten für ihren Lebensunterhalt. Sie fällen Bäume, bauen Bögen, jagen, bestellen
ein Feld, backen, gerben und tragen Rationen in Maras Lager. Jede Ware hat eine Quelle, jeder Bestand ein Buch.
Die Spielerin sieht die Arbeit im Dorf und liest an der Dorftafel, was vorhanden ist und was fehlt.

Grundlage ist das Owner-Konzept „NPC-Wirtschaft und Lebenszyklus“ mit seinen zehn Abnahmefällen. Die
Gestaltungsfragen aus Abschnitt 12 des Konzepts sind nicht entschieden, siehe
[Grenzen](#grenzen-und-offene-entscheidungen).

## Was im Spiel zu sehen ist

Die Dorftafel steht von Anfang an an der Straße. Die Werkplätze erscheinen, sobald der erste zusätzliche Bewohner
im Dorf lebt. Das Dorf gewinnt einen Bewohner je abgeschlossener Hauptquest; heute ist das die Quest an der alten
Mühle.

| Ort | Was dort steht | Was die Bestände zeigen | Arbeiten an diesem Ort |
| --- | --- | --- | --- |
| Holzplatz, östlich der Straße | Hackklotz und Stapelgestell | bis 12 Holz, bis 2 Äxte | – |
| Werkbank, vor dem mittleren Haus im Osten | Werkbank mit Schraubstock | Holzrahmen, Bögen, defekte Axt, Metall, Dornbeeren | Rahmen, Bogen, Axt reparieren, Erz verhütten, Beschläge schmieden, Verband binden |
| Kochstelle, nordwestlich des Lagers | Feuerring, Dreibein mit Topf, Tisch | bis 4 Rationen, bis 4 Eimer Wasser; das Feuer brennt bei der Arbeit | kochen, Brot backen, Herdfeuer schüren |
| Wildgestell, neben der Kochstelle | Stange auf Astgabeln | bis 4 Stück Wild | – |
| Trockengestell, im Südosten | drei Spannrahmen | bis 3 Häute | Häute gerben |
| Brunnen, auf dem Marktplatz | Steinbrunnen mit Winde | – | Wasser holen, trinken |
| Haus im Osten | vorhandenes Haus | – | Feld, Kompost, Dach, Wasser für Kranke; Ruhe und Schlaf |
| Dorftafel, an der Straße | Anschlagbrett | Text der Tafel | – |
| Maras Lager | wie bisher | die vorhandenen Rationskisten | Rationen übergeben |

Ein sichtbares Teil steht für eine Einheit im Kern. Liegen mehr Einheiten im Lager, als ein Werkplatz Plätze hat,
zeigt er seine volle Anzahl; die genaue Zahl steht auf der Tafel. Getreide, Mehl, Stroh, Kompost, Erz, Stein,
Beschläge, Leder und Verbände haben keine eigenen Modelle und stehen nur auf der Tafel.

Die Bewohner gehen zu dem Ort ihrer Arbeit. Wer in den Wald oder zur Mühle aufbricht, geht durch das Nordtor aus
dem Bild und kommt dort wieder herein. Wer schläft, ist im Haus.

### Die Dorftafel

Wer an der Tafel steht, öffnet sie mit der Schaltfläche in der Bildmitte oder mit der Taste `N`. Langer Text lässt
sich mit dem Finger oder dem Mausrad rollen.

- **Vorräte:** genaue Stückzahlen des Dorflagers nach Werkstatt, Küche, Hausgarten, Back- und Pflegestube und Bau,
  dazu Maras Vorrat.
- **Waldsaum und alte Mühle:** was der letzte heimgekehrte Bewohner dort gesehen hat, mit Namen des Boten und Tag.
  Das Dorf zählt seine eigenen Vorräte; vom Umland weiß es nur aus Berichten.
- **Bewohner:** wer im Dorf ist und was die Person tut. Von Abwesenden steht dort nur, dass sie unterwegs sind.
- **Bedarf:** offene Engpässe, zum Beispiel eine defekte Axt ohne Metall oder ein kaltes Herdfeuer.
- **Abgeben:** je eine Schaltfläche für Holz, Dornbeeren, Ascheisen, Heilkräuter, Holzrahmen und Bogen, soweit die
  Spielerin sie bei sich trägt. Ein Tippen übergibt eine Einheit an das Dorflager.

Beim Betreten des Dorfs nennt die Meldungszeile den dringendsten Bedarf der Tafel. Bevor jemand einzieht, zeigt
die Tafel Maras Vorrat und dass Arbeitskräfte fehlen; Material nimmt das Dorf erst mit dem ersten Bewohner an.

## Regeln im Kern

### Bewohner und Bedürfnisse

Bewohner sind die vorhandenen namenlosen Einwohner (`resident.village.N`). Es entstehen keine neuen benannten
Figuren. Jede Person hat Hunger, Durst, Müdigkeit und Gesundheit und kann jede Arbeit ausführen.

Der Kern prüft das Dorf alle zehn Ticks, also einmal je Spielsekunde. Für jede Person gilt diese Reihenfolge:

1. **Gefahr und Gesundheit:** Kämpft ein Dorfbewohner in der Nähe eines Werkplatzes, ruht dort die Arbeit. Wer
   verletzt ist, gibt die Arbeit ab und geht nach Hause. Nachts bleibt niemand im Wald.
2. **Grundversorgung:** Hungrige essen eine Ration aus Maras Lager, Durstige trinken aus dem Wasservorrat des
   Dorfs. Müde ruhen, nachts wird geschlafen. Einer verletzten Person bringt ein Nachbar Wasser ans Haus.
3. **Arbeit:** der nächste Schritt, den das Dorf am dringendsten braucht.
4. **Übergabe:** fertige Rationen werden in Maras Lager getragen.
5. **Gemeinschaft:** Wer nichts zu tun hat, hält sich bei den anderen auf und erzählt, was er selbst erlebt hat.

Zwischen den zulässigen Handlungen wählt das persönliche Gedächtnis der Person. Was zulässig ist, legt allein der
Kern fest. Eine begonnene Arbeit oder ein begonnener Weg wird zu Ende geführt, solange nichts Dringenderes
dazwischenkommt. Hunger verletzt, tötet aber nicht: die Gesundheit sinkt bis 200 von 800 und nicht weiter.

Nachts erholt sich schlechter, wer in einem Haus mit kaltem Herd oder schadhaftem Dach schläft.

### Arbeiten

| Arbeit | Ort | Dauer | Verbraucht | Ergibt |
| --- | --- | --- | --- | --- |
| Baum fällen | Wald | 60 s | einen Baum, eine Axtnutzung | 3 Holz |
| Setzlinge sammeln | Wald | 30 s | Baumsaat | 2 Setzlinge |
| Setzling pflanzen | Wald | 20 s | 1 Setzling | einen Jungbaum, schlagreif nach 3 Spieltagen |
| Dornbeeren sammeln | Wald | 30 s | Beeren am Strauch | 2 Dornbeeren |
| Jagen | Wald | 150 s | ein Tier | 1 Wild |
| Heilkräuter sammeln | Wald | 30 s | Kräuter | 2 Heilkräuter |
| Erz bergen | Wald | 42 s | Erzader | 2 Erz |
| Bruchstein sammeln | Wald | 30 s | Geröll | 2 Bruchstein |
| Holzrahmen bauen | Werkbank | 30 s | 2 Holz | 1 Holzrahmen |
| Bogen bauen | Werkbank | 45 s | 1 Holzrahmen, 1 Dornbeere | 1 Bogen |
| Axt reparieren | Werkbank | 60 s | 1 defekte Axt, 1 Metall, 1 Holz | 1 Axt |
| Erz verhütten | Werkbank | 34 s | 1 Erz, 1 Holz | 1 Metall |
| Beschläge schmieden | Werkbank | 24 s | 1 Metall, 1 Holz, 1 Wasser | 1 Beschlag |
| Verband binden | Werkbank | 14 s | 1 Leder, 1 Stroh, 1 Wasser | 1 Verband |
| Wasser holen | Brunnen | 10 s | Brunnenwasser | 2 Wasser |
| Rationen zubereiten | Kochstelle | 40 s | 1 Wild, 1 Wasser, 1 Holz | 2 Rationen, 1 Haut |
| Brot backen | Kochstelle | 24 s | 1 Mehl, 1 Wasser, 1 Holz | 3 Rationen |
| Herdfeuer schüren | Kochstelle | 9 s | 1 Holz | ein warmes Haus bis zum Morgen |
| Haut gerben | Trockengestell | 32 s | 1 Haut, 1 Wasser | 1 Leder |
| Feld bestellen | Haus | 22 s | 1 Saatgut, 1 Wasser, ein Maß Boden | Getreide, reif nach einem halben Spieltag |
| Getreide ernten | Haus | 26 s | das reife Feld | 4 Getreide, 1 Saatgut, 2 Stroh |
| Kompost ansetzen | Haus | 18 s | 1 Stroh, 1 Wasser | 1 Kompost |
| Kompost aufs Feld bringen | Haus | 14 s | 1 Kompost | ein Maß Boden |
| Dach reparieren | Haus | 40 s | 2 Holz, 1 Bruchstein, 1 Beschlag | ein dichtes Dach |
| Wasser zur Krankenstube bringen | Haus | 9 s | 1 Wasser | der Durst einer verletzten Person ist gestillt |
| Getreide mahlen | Mühle | 28 s | 2 Getreide | 2 Mehl |
| Rationen zu Mara bringen | Lager | 6 s | – | bis zu 4 Rationen wechseln ins Lager |
| Heilkräuter zu Orsel bringen | Mühle | 6 s | – | bis zu 4 Kräuter wechseln auf Orsels Regal |

Der Weg in den Wald dauert 30 Sekunden je Richtung, der zur Mühle 40. Eine Person trägt sechs Waren, Werkzeug
zählt nicht mit. Eine Axt hält zwölf Bäume und ist dann defekt. Eine von 24 Jagden endet mit einer Verletzung.
Verletzte arbeiten nicht. Ist Orsels Lehre freigeschaltet, pflegt sie Verletzte an der Mühle; Heilkräuter und ein
mitgebrachter Verband beschleunigen die Heilung. Wer hungert, wird durch Pflege nicht gesund und geht nach Hause.

Feld, Mühle, Schmiede, Gerberei, Dach und Herd arbeiten erst, wenn Orsels Lehre freigeschaltet ist. Die Rezepte für
Rahmen und Bogen sind dieselben wie im Handwerk der Spielerin.

### Nichts entsteht ohne Quelle

- **Startbestand** beim Einzug des ersten Bewohners: 4 Holz, 2 Äxte, 2 Metall, 2 Wasser, 2 Saatgut, 4 Setzlinge.
- **Wald:** 24 schlagreife Bäume. Sechs Samenbäume werden nie gefällt. Nachwuchs kommt nur aus gepflanzten
  Setzlingen und braucht drei Spieltage.
- **Wild:** höchstens 8 Tiere. Der Bestand wächst nur, solange mindestens zwei Tiere leben.
- **Dornbeeren, Baumsaat, Heilkräuter, Brunnen:** wachsen bis zu ihrer Obergrenze in festen Abständen nach.
- **Erz und Bruchstein:** je 12 Einheiten am Waldweg. Sie wachsen nicht nach.
- **Boden:** zwei Maß. Jede Aussaat verbraucht eines, Kompost bringt eines zurück.
- **Dach:** nutzt sich alle zwei Spieltage um eine Stufe ab. **Herd:** brennt bis zum Morgen aus.

Für jede Ware gilt jederzeit: Anfang + erzeugt + empfangen − abgegeben − verbraucht = vorhanden. „Vorhanden“
zählt das Lager, die Hände der Bewohner und zurückgelegte Zutaten. Der Kern prüft diese Rechnung bei jedem Laden.

### Lager und Übergabe

Maras Lager fasst zehn Rationen. Solange die Auffüllaufgabe der Spielerin offen ist, lassen die Bewohner den
letzten Platz frei. Eine volle Kammer nimmt nichts an; die Ration bleibt im Dorflager.

Zutaten werden beim Beginn einer Arbeit zurückgelegt. Bricht die Person ab, gehen sie zurück. Rationen für Mara
nimmt genau eine Person auf; bis zur Übergabe gehören sie weiter dem Dorf.

### Wissen

- Eine Lieferung merkt sich die liefernde Person, Mara nur, wenn sie dabei an ihrem Lager steht. Orsels Pflege
  merken sich Orsel und die gepflegte Person, die Wasserhilfe am Haus die beiden Beteiligten.
- Eine Abgabe der Spielerin an der Tafel sehen nur, wer in Sichtweite steht.
- Dorfbewohner, die beieinanderstehen, erzählen einander, was sie selbst erlebt haben. Der Zuhörer weiß es dann
  als Bericht mit Quelle, nicht als Augenzeuge. Ilyan, Mara und Dorn sprechen die Spielerin darauf an.
- Über Wald und Mühle weiß das Dorf nur, was ein Heimkehrer berichtet. Die Planung richtet sich nach dem letzten
  Bericht; von zwei Berichten desselben Augenblicks gilt der spätere.

Die Einträge liegen im vorhandenen NPC-Gedächtnis, siehe [NPC_PERSONAL_MEMORY.md](NPC_PERSONAL_MEMORY.md). Was eine
Arbeit einbringt, lernt die Person für die Entscheidung, mit der sie die Arbeit begonnen hat. Das bloße Ankommen an
einem entfernten Arbeitsort ist noch kein Ergebnis; ein Heimweg oder der Weg zum Essen schon.

### Zeit und Spielstand

Die Wirtschaft läuft mit den Spielticks, auch wenn die Spielerin unterwegs ist. Bei geschlossenem Spiel, im Menü
und solange die Spielerin besiegt am Boden liegt, vergeht für das Dorf keine Zeit; es gibt keine Aufholrechnung.

Der Zustand liegt im Abschnitt `economy` des Charakterspielstands, derzeit in Regelstand 2. Ein Spielstand aus
Regelstand 1 wird beim ersten Dorftakt überführt. Ein Spielstand aus der Zeit vor der Wirtschaft lädt unverändert;
die Wirtschaft beginnt dann mit dem Startbestand und ohne erfundene Vergangenheit. Ein Spielstand, dessen
Wirtschaftsabschnitt nicht aufgeht, wird als Ganzes abgelehnt.

## Darstellung

Unity zeigt den Zustand des Kerns und ändert ihn nicht. Bestände, Feuer und Figuren werden in jedem Bild neu aus
dem Kern gelesen. Eine Übergabe an der Tafel ist die einzige Eingabe; der Kern prüft sie selbst, einschließlich
des Abstands zur Tafel.

Vereinfachungen:

- Wald und Mühle werden nicht gezeigt. Eine Person ist bis zum Nordtor zu sehen und wieder ab dort.
- Figuren gehen in eigenem Tempo um Häuser und Werkplätze herum. Eine Figur kann noch unterwegs sein, während
  der Kern die Arbeit schon zählt.
- Feld, Dach und Krankenstube haben keinen eigenen Platz; diese Arbeiten sind am Haus zu sehen.
- Werkplätze haben wie alle Kulissen keine Kollision.
- Ohne die privaten Modelle erscheinen Klötze mit einem Würfel je Wareneinheit.

## Technik

| Teil | Datei |
| --- | --- |
| Regeln, Zustand, Ablauf | `Assets/Scripts/Core/VillageEconomyRules.cs`, `VillageEconomyState.cs`, `VillageEconomy.cs` |
| Feld, Mühle, Schmiede, Gerberei, Dach, Herd | `Assets/Scripts/Core/VillageEconomyProduction.cs` |
| Durst, Wasserhilfe, Berichte, Dorfgespräch | `Assets/Scripts/Core/VillageEconomyCommunity.cs` |
| Arbeitscontroller der Bewohner | `Assets/Scripts/Core/VillageWorkController.cs` |
| Orte und Tafeltext | `Assets/Scripts/Core/VillageEconomyLayout.cs`, `VillageEconomyBoard.cs` |
| Darstellung | `Assets/Scripts/Runtime/VillageEconomyPresentation.cs` |
| Modelle | `Tools/Art/blender/village_work_props.py`, `Assets/Editor/VillageWork/VillageWorkArt.cs` |
| Tests | `Tests/CoreVerification/VillageEconomy*Tests.cs`, `VillageProductionTests.cs`, `Assets/Tests/EditMode/VillageEconomy*Tests.cs`, `Assets/Tests/PlayMode/VillageEconomyRuntimeTests.cs` |

Die Werkplatzmodelle sind private Kunst und liegen nicht im Repository. So entstehen sie neu:

```text
blender --background --python Tools/Art/blender/village_work_props.py -- --output Assets/PrivateArt/VillageWork
Unity: ARE ARPG → Art → Village Work → Build Workplaces
```

Liegen die Modelle vor, baut ein normaler Spiel-Build fehlende Werkplätze von selbst. Jedes Modell besteht aus
`Base` (dem Möbel), `Stock_<Ware>_<Nummer>` (je einer Wareneinheit) und bei der Kochstelle `Fire`. Die Warennamen
sind die des Kerns (`VillageGood`).

## Grenzen und offene Entscheidungen

Nicht enthalten: Tausch, Löhne und Außenhandel, Alterung, Nachwuchs und Tod, eigene Orte und Modelle für Feld,
Schmiede und Krankenstube, Arbeit des vorhandenen Jägers im Dorf. Leder und Beschläge haben über Verband und Dach
hinaus noch keinen Abnehmer.

Der Owner entscheidet laut Konzept, Abschnitt 12:

1. welche weiteren Personen, Tierarten, Werkstätten und Orte dazukommen,
2. wie stark Alterung, Krankheit, Nachwuchs und Verlust wirken,
3. wie Aschengrenze mit Tausch, Löhnen, Reserven und Außenhandel umgeht,
4. welcher Teil als nächster sichtbar und spielbar wird.

Messwerte und Nachweise stehen in der [Abnahme](evidence/npc-economy-20261121/acceptance.md).
