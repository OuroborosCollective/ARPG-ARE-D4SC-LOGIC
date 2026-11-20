"""Read installed Ashen Crypt art techniques and invoke the existing advisory cortex."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

DEFAULT_LAB = Path.home()/".local/share/unity-game-director/art-lab/20261120"
CATALOG = Path(__file__).resolve().parents[1]/"references/art-sources.json"

def read_skill(catalog, lab, key):
    item = catalog["skills"][key]
    source = item["source"]
    checkout = lab/"sources"/source
    commit = subprocess.check_output(["git","-C",str(checkout),"rev-parse","HEAD"], text=True).strip()
    if commit != catalog["sources"][source]["commit"]:
        raise ValueError("Source revision changed: " + source)
    path = (checkout/item["file"]).resolve(strict=True)
    path.relative_to(checkout.resolve())
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != item["sha256"]:
        raise ValueError("Installed skill changed: " + key)
    return {"name":key,"commit":commit,"path":str(path),"sha256":item["sha256"]}, data

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--lab",type=Path,default=DEFAULT_LAB)
    sub=p.add_subparsers(dest="action",required=True)
    sub.add_parser("verify")
    read=sub.add_parser("read")
    read.add_argument("skill")
    route=sub.add_parser("route")
    route.add_argument("lane")
    route.add_argument("--project",type=Path,required=True)
    args=p.parse_args()
    cat=json.loads(CATALOG.read_text())
    if args.action=="read":
        meta,data=read_skill(cat,args.lab,args.skill)
        print(json.dumps(meta));sys.stdout.flush();sys.stdout.buffer.write(data)
    elif args.action=="verify":
        items=[read_skill(cat,args.lab,k)[0] for k in sorted(cat["skills"])]
        print(json.dumps({"count":len(items),"skills":items},sort_keys=True))
    else:
        lane=cat["routes"][args.lane]
        result=subprocess.run([sys.executable,str(args.project/"Tools/Director/route_work.py"),
                               lane["neuralQuery"],"--project",str(args.project)],
                              capture_output=True,text=True,check=True)
        cortex=json.loads(result.stdout)
        neural=cortex.get("neural_cortex")
        if not isinstance(neural,dict) or not neural.get("selected"):
            raise ValueError("Neural Director did not select a department: "+str(neural)[:700])
        pin=subprocess.check_output(["git","-C",str(args.project),"rev-parse","HEAD"],text=True).strip()
        print(json.dumps({"projectRevision":pin,"lane":args.lane,"director":neural,
                          "techniques":[read_skill(cat,args.lab,k)[0] for k in lane["skills"]]},
                         ensure_ascii=False,sort_keys=True))
if __name__=="__main__":
    main()
