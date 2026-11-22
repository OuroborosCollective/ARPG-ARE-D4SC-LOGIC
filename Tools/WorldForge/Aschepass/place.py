"""Place static chapter details through the game's existing plan. Re-running preserves revisions."""
from pathlib import Path
import json
import sys
import subprocess

# Fixed plan coordinates in metres. Paths, doors and quest interactions stay clear.
PIECES = {
    "krypta": [("RibPillar",-4.6,-11.8,33,.82),("Brazier",4.3,-12.4,17,.76)],
    "aschengrenze": [("StoneOutcrop",3.05,-16.59,142,.46),("BloodcapBank",3.40,-16.99,202,.78)],
    "aschestrasse": [("ShaleLedge",-6.9,-14.75,24,.72),("StoneOutcrop",-6.05,-16.45,108,.48),("AshPine",-10.55,-8.3,74,.88),("AshPine",-12.66,-4.95,157,1.04),("ShaleLedge",-9.2,-7.3,89,.85)],
    "muehle": [("StoneOutcrop",5.38,7.12,119,.67),("StoneOutcrop",5.84,8.06,63,.39),("EmberSedge",4.66,8.42,131,.68),("AshPine",12.8,15.4,204,.87)],
    "speicher": [("GrainBay",8.25,-6.4,277,.82),("StoneOutcrop",4.94,17.10,124,.36)],
    "verborgene-krypta": [("RibPillar",-3.15,18.48,12,.84),("BloodcapBank",-3.5,17.88,231,.73)],
    "rattlernest": [("ShaleLedge",-12.9,-5.2,32,.87),("StoneOutcrop",-10.7,-6.1,149,.58)],
    "aschenzoll": [("StoneOutcrop",-5.7,-12.5,28,.59),("Brazier",-5.25,-11.65,109,.76)],
    "wald": [("BloodcapBank",-5.38,-41.93,88,.90),("StoneOutcrop",-6.0,-41.33,78,.58),("ShaleLedge",-8.45,-39.88,209,.68)],
    "baerenhoehle": [("ShaleLedge",-6.3,8.65,132,.71),("BloodcapBank",-4.3,8.88,266,.68)],
}

def apply(directory):
    for area, pieces in PIECES.items():
        path=directory/(area+".json")
        plan=json.loads(path.read_text())
        pinned="c600181d42da666c4ac0aa6ade4489ef4903e24a"
        root=directory.resolve().parents[3]
        rel=path.resolve().relative_to(root)
        original=json.loads(subprocess.check_output(["git","-C",str(root),"show",f"{pinned}:{rel}"]))
        # Accept the exact base plan or our previous private review. Other edits are retained for the owner.
        saved=[p for p in plan["pieces"] if not p["id"].startswith("b020")]
        if saved!=original["pieces"] or plan["revision"] not in (original["revision"],original["revision"]+1):
            raise ValueError(f"Area has changed independently: {area}")
        authored=[dict(id=f"b{200+i:04}",key=key,x=x,z=z,rot=yaw,scale=scale)
                  for i,(key,x,z,yaw,scale) in enumerate(pieces,1)]
        previous_preview={
            "aschengrenze":[dict(id="b0201",key="StoneOutcrop",x=-6.9,z=-9.65,rot=142,scale=.48),
                            dict(id="b0202",key="BloodcapBank",x=-7.18,z=-10.16,rot=202,scale=.78)],
            "speicher":[authored[0],dict(id="b0202",key="Brazier",x=5.5,z=16.55,rot=60,scale=.68)]
        }
        current=[p for p in plan["pieces"] if p["id"].startswith("b020")]
        if current not in ([],authored,previous_preview.get(area,authored[:2])):
            raise ValueError(f"Private placement conflict: {area}")
        if current!=authored:
            plan["pieces"]=saved+authored
            plan["revision"]=original["revision"]+1
            path.write_text(json.dumps(plan,indent=2,ensure_ascii=False)+"\n")

if __name__=="__main__":
    apply(Path(sys.argv[1]))
