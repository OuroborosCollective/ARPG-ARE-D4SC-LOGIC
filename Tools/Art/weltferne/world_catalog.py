"""Art direction of the mapped horizons. Bearings run clockwise from north; dimensions in metres are art choices."""
import json
from pathlib import Path

def sight(kind, bearing, distance, scale=1, turn=0):
    return dict(kind=kind, bearing=bearing, distance=distance, scale=scale, turn=turn)

def place(id, name, group, style, bearing, motif, travel, views, planned=False):
    return dict(id=id, name=name, group=group, style=style, bearing=bearing, motif=motif,
                travel=travel, landmarks=[sight(*v) for v in views], planned=planned)

MAIN="Hauptweg"
WEST="Westliche Wälder"
BELOW="Unter der Erde"
FUTURE="Geplante Orte"
catalog=[
place("old_crypt","Die alte Krypta",MAIN,"crypt",0,
      "Ein kalter Lichtspalt über dem nördlichen Rippenbogen zieht den Blick zwischen gestaffelten Pfeilern hindurch.",
      "Nach dem Kampf führt das Nordportal einmalig in den entweihten Vorhof.",
      [("crypt_arch",1,64,1.18),("sealed_arch",105,83,.77),("broken_pier",331,48,.9),("broken_pier",24,55,.8),("cairn",195,61,.8)]),
place("forecourt","Der entweihte Vorhof",MAIN,"ash",9,
      "Über der geborstenen Nordmauer hängt die Glocke frei im Turm. In der Ostmauer kündigen drei glimmende Ofenschlote den Seitenhof an.",
      "Offener Übergang nach Norden zum Glockenhof. Das östliche Seitentor führt in den Brennerhof.",
      [("bell",3,67,1.36),("kiln",90,68,1.18),("broken_pier",333,49,.82),("abbey",30,119,.76),("crypt_crown",184,96,.65),("aqueduct",284,115,.85)]),
place("kiln","Der Brennerhof",MAIN,"kiln",193,
      "Hinter dem Südtor schneidet das offene Glockengeschoss in den Rauch. Die kalten Bögen eines Aquädukts rahmen den Ofenbezirk.",
      "Das Südtor führt in den Vorhof zurück. Ofengang und Scherbenschlupf bleiben Teil des Seitenhofs.",
      [("bell",182,86,.96),("aqueduct",211,86,1.36),("kiln",17,69,1.24),("kiln",242,105,.60),("broken_pier",156,48,.88),("abbey",270,140,.69)]),
place("bellcourt","Der Glockenhof",MAIN,"ash",5,
      "Jenseits der Turmtür liegt das gezackte Dachband der Aschengrenze. Im Rückblick stehen kalte Vorhofbögen vor den Ofenschloten.",
      "Südwärts ist der Vorhof begehbar. Durch die Turmtür geht es einmalig ins Dorf.",
      [("village",4,72,1.17),("bell",331,59,1.03),("aqueduct",190,82,1.13),("kiln",129,106,.74),("abbey",42,124,.87),("broken_pier",30,52,.75)]),
place("village","Die Aschengrenze",MAIN,"village",353,
      "Nördlich sammeln sich schiefe Wegsteine am Pass. Über den südlichen Dächern bleibt Aschenwachts beschädigter Glockenturm erkennbar.",
      "Vom Nordtor zur Aschestraße und zurück. Die Krankenstube, die Schmiede und die Scheune behalten eigene Innenräume.",
      [("road_shrine",351,58,1.12),("mill",13,111,.82),("village",319,66,1.02),("bell",185,104,1.03),("village",68,85,.74),("standing_rock",30,60,.90)]),
place("ash_road","Die Aschestraße",MAIN,"road",1,
      "Das gebrochene Rad der Mühle ist der erste Umriss vor dem nördlichen Kamm. Nach Süden kehren die Dächer und der Schmiedekamin wieder.",
      "Norden zur alten Mühle. Süden zurück in die Aschengrenze.",
      [("mill",3,67,1.46,18),("road_shrine",337,53,.85),("granary",20,120,.69),("village",181,88,1.12),("dead_oak",30,53,.67),("standing_rock",247,84,1)]),
place("mill","Die alte Mühle",MAIN,"mill",355,
      "Über dem ausgewaschenen Tal stehen Speicherstelzen im Norden. Links davon markieren windschiefe Bäume den Weg nach Aschenzoll.",
      "Nordweg zum Speicher, südlich zur Aschestraße. Der westliche Waldpfad zweigt im Nordwesten ab.",
      [("granary",1,84,1.24),("mill",27,70,1.17,-12),("dead_oak",316,57,1.26),("toll",310,118,.63),("road_shrine",183,96,.89),("aqueduct",92,125,.72)]),
place("granary","Der alte Speicher",MAIN,"store",1,
      "Hölzerne Speicherfüße stehen vor der versiegelten Nordflanke. Das tiefe Relief im Stein wiederholt sich unten in der Mühlenkrypta.",
      "Norden zur verborgenen Krypta, Süden zur Mühle. Das geöffnete Siegel im Speicher führt in die Krypta unter der Mühle.",
      [("crypt_crown",2,77,1.24),("sealed_arch",345,63,.68),("granary",35,66,1.02),("mill",184,99,1.07),("standing_rock",310,57,.78),("dead_oak",88,91,.8)]),
place("hidden_crypt","Die verborgene Krypta",MAIN,"crypt",0,
      "Hinter der letzten Reihe gotischer Pfeiler lösen sich die bearbeiteten Gewölbe in unregelmäßige Knochenrippen auf.",
      "Nördlich liegt das Rattlernest, südlich der alte Speicher. Dies ist eine andere Krypta als der Abstieg unter der Mühle.",
      [("nest",3,72,1.31),("crypt_arch",340,57,1.14),("sealed_arch",178,80,1),("broken_pier",42,60,1),("side_rift",271,92,.83)]),
place("nest","Das Rattlernest",MAIN,"nest",186,
      "Gebleichte Rippen stehen übereinander wie die Spanten eines Wracks. Zum südlichen Rückweg wird wieder Mauerwerk erkennbar.",
      "Südwärts zurück in die verborgene Krypta. Das Nest des Tiefscharrers ist der Endpunkt dieses Arms.",
      [("crypt_arch",182,74,1.13),("nest",207,58,1.18),("nest",30,75,1.43,34),("side_rift",103,91,.84),("cairn",150,55,.8)]),
place("west_forest","Der Wald nach Aschenzoll",WEST,"forest",350,
      "Durch das kahle Astwerk hängt im Norden die Waagschale des Zollkrans. Nach Westen verdichtet sich der dunkle Fels der Bärenhöhle.",
      "Südlicher Rückweg zur Mühle, Norden nach Aschenzoll, Westen zur Bärenhöhle.",
      [("toll",0,73,1.34),("bear_den",269,65,1.08),("mill",178,108,1.04),("dead_oak",325,50,1),("dead_oak",25,60,.81),("standing_rock",222,78,.8)]),
place("bear","Die Bärenhöhle",WEST,"bear",182,
      "Wurzeln fassen den einzigen helleren Spalt nach Süden ein. In den Seitenwänden liegen stumpfe, feuchte Basaltsäulen.",
      "Der südliche Höhlenausgang bringt die Figur in denselben Wald zurück.",
      [("bear_den",180,62,1.42),("dead_oak",192,83,1.16),("side_rift",254,86,.8),("standing_rock",151,54,1.04),("standing_rock",82,71,.92)]),
place("freight","Aschenzoll · Der Frachtplatz",WEST,"toll",350,
      "Das lange Tau des Waagekrans hängt vor dem nördlichen Waldsattel. Im Süden ragen die abgestorbenen Leitbäume des Waldwegs auf.",
      "Südlich zurück in den Wald. Der nördliche Waldweg öffnet sich erst nach der Frachtmeldung bei Ilyan.",
      [("toll",343,60,1.28),("road_shrine",7,83,1.12),("dead_oak",18,75,.93),("village",83,118,.64),("dead_oak",185,75,1.25),("standing_rock",304,77,.88)]),
place("forest_road","Waldweg jenseits von Aschenzoll",WEST,"ridge",272,
      "Westlich der Rastlichtung schlitzt eine kalte Mineralader die Steilwand auf. Der östliche Schatzabzweig bleibt eine kleine Einsenkung zwischen Steinen.",
      "Südwärts zum Frachtplatz. Die Seitenhöhle liegt westlich, der Schatzabzweig östlich der Rastlichtung.",
      [("side_rift",270,68,1.28),("dead_oak",242,55,.9),("standing_rock",293,67,1.0),("toll",178,98,.96),("cairn",91,68,.74),("standing_rock",22,95,.68)]),
]
side_data=[
("I · Wurzelsaum","Wurzeln reichen vom Eingang in einen niedrigen Schieferkessel. Kaltes Streiflicht hält die Richtung zum Wald lesbar.","bear_den","side_rift",.96),
("II · Schieferbruch","Splittrige Schichten laufen quer über den Spalt. Ihr heller Bruch zeichnet die nächste Tiefe nach.","side_rift","standing_rock",1.11),
("III · Die blinde Ader","Dünne Mineraladern glimmen zwischen größeren, versetzten Felslamellen. Der Lichtsaum des Eingangs ist verschwunden.","side_rift","nest",1.24),
("IV · Schwarzer Trichter","Eine ausgebrochene Felswanne schließt die Sicht in der Tiefe. Der Rückblick greift dieselbe Ader in kaltem Blau auf.","nest","side_rift",1.30)]
for depth,(subtitle,motif,one,two,k) in enumerate(side_data,1):
    angle=[18,327,54,288][depth-1]
    catalog.append(place("side_"+str(depth),"Seitenhöhle "+subtitle,BELOW,"rift",angle,motif,
        "Vier aufeinanderfolgende Ebenen. Der Rückweg führt über den Höhleneingang auf denselben Waldweg.",
        [(one,angle+4,67,k),(two,angle-23,60,.79),("standing_rock",angle+31,57,1.07),("bear_den" if depth==1 else "cairn",angle+171,82,.78),("side_rift",angle+99,94,.8)]))
under_data=[
("I · Der Wasserschacht","In einer alten Schachtfassung wiederholt sich das runde Siegel des Speichers. Unter den Tragbögen sammelt sich dunkles Wasser.","sealed_arch","mill_vault"),
("II · Das verlorene Getriebe","Ein unbewegtes Achslager und steinerne Rippen markieren die Tiefe unter der Mühle. Das Gegenlicht fällt nur noch durch schmale Schächte.","mill_vault","crypt_arch"),
("III · Unter dem Grundwasser","Kühle Pfeiler stehen wie ein zweites, versunkenes Gewölbe hinter dem letzten Schacht. Warmes Licht erscheint nur noch als Reflex.","crypt_arch","mill_vault")]
for depth,(subtitle,motif,one,two) in enumerate(under_data,1):
    angle=[348,17,321][depth-1]
    catalog.append(place("under_"+str(depth),"Mühlenkrypta "+subtitle,BELOW,"vault",angle,motif,
        "Drei Ebenen unter der Mühle. Der Zugang liegt am geöffneten Siegel im alten Speicher. Die gebaute Fernkulisse verändert die neu ausgewürfelten Laufwege nicht.",
        [(one,angle-2,67,1.24),(two,angle+24,83,1.03),("broken_pier",angle-27,50,1.1),("sealed_arch",angle+167,92,.79),("side_rift",angle+260,87,.87)]))
catalog += [
place("king","Der König von Immergrün",FUTURE,"future",354,
      "Lebende Wurzeln krümmen sich über einem zerbrochenen Kronenring. Das letzte Grün bleibt auf die Steinritzen beschränkt.",
      "Isolierte Entwurfsstudie. Ort und Zugang des Endkampfs bleiben offen.",
      [("king",353,61,1.36),("dead_oak",332,50,1.03),("abbey",24,110,.85),("standing_rock",55,82,.86)],True),
place("guardian","Der Sockel des Glutwächters",FUTURE,"vault",8,
      "Ein abgesenkter Ring hält das Glutlicht in seiner leeren Mulde. Darüber hängen kalte Rippen im Dunkel.",
      "Isolierte Entwurfsstudie zum Sockel und zum Edelstein aus der Königskrone. Es wird kein Standort und kein Reiseweg festgelegt.",
      [("guardian",8,60,1.48),("crypt_arch",10,92,1.25),("broken_pier",338,54,1.11),("standing_rock",40,64,.90)],True)]
# Noninteractive scenery forms the middle distance between the main landmark and
# the far ranges. Keep return-route vistas framed from their own local gate.
dressing = {
    "old_crypt":[("ossuary_wall",339,52,.94,-8),("ossuary_wall",22,65,.82,22)],
    "forecourt":[("court_wall",348,54,1.16,-16),("court_wall",20,63,.98,19),("court_wall",68,64,.69,-23)],
    "kiln":[("court_wall",166,61,.86,-28),("freight_margin",205,59,.77,24),("court_wall",30,68,.93,20)],
    "bellcourt":[("court_wall",347,53,.97,-24),("court_wall",24,67,1.13,20)],
    "village":[("freight_margin",338,62,.76,-12),("forest_margin",16,61,.85,24),("court_wall",174,67,.77,-12)],
    "ash_road":[("mill_bank",346,58,1.14,-12),("forest_margin",22,59,1.09,22),("forest_margin",171,62,.87,-17)],
    "mill":[("mill_bank",343,52,1.15,-15),("freight_margin",13,67,.89,14),("forest_margin",318,61,.97,21)],
    "granary":[("freight_margin",341,56,.97,-13),("court_wall",20,62,1.0,18)],
    "hidden_crypt":[("ossuary_wall",343,54,1.14,-20),("mineral_wall",24,63,.88,20)],
    "nest":[("mineral_wall",162,54,1.02,-16),("ossuary_wall",205,66,.79,13)],
    "west_forest":[("forest_margin",331,55,1.15,-17),("forest_margin",11,63,1.08,26),("forest_margin",253,59,1.0,-23)],
    "bear":[("mineral_wall",162,52,.96,14),("forest_margin",194,74,.64,-18)],
    "freight":[("freight_margin",335,52,1.11,-21),("forest_margin",9,65,1.1,30)],
    "forest_road":[("forest_margin",251,54,1.15,-14),("mineral_wall",289,64,.93,27),("forest_margin",106,67,1.04,25)],
    "king":[("forest_margin",336,55,1.07,-15),("court_wall",15,67,1.0,24)],
    "guardian":[("ossuary_wall",346,55,1.1,-22),("cistern_wall",26,64,.94,16)]
}
for record in catalog:
    key=record['id']; a=record['bearing']
    ornaments=dressing.get(key)
    if ornaments is None:
        if key.startswith('side_'):
            ornaments=[("mineral_wall",a-15,55,1.04,-17),("mineral_wall",a+23,69,.87,27)]
        else:
            ornaments=[("cistern_wall",a-15,54,1.08,-17),("ossuary_wall",a+22,64,.98,22)]
    record['dressing']=[sight(*v) for v in ornaments]

if __name__=="__main__":
    Path(__file__).with_name("catalog.json").write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+"\n")
