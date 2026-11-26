"""Extract the actual village and crypt verdicts from Unity PlayMode artifacts."""
import hashlib
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET

areas = {"DressedCryptFacesItsWallsInwardKeepsTheLaneFreeAndStaysPassive": "Crypt",
         "VillageArtAndVillagersStandOnTheVisibleGround": "Village",
         "PrivateVillageNpcFallbackFeetTouchTheGround": "Village"}
cases = []
for path in sorted(Path("artifacts").rglob("*.xml")):
    data = path.read_bytes()
    root = ET.fromstring(data)
    for test in root.iter("test-case"):
        if test.get("methodname") in areas and (test.get("classname") or "").endswith(".EntryJourneyRuntimeTests"):
            cases.append(dict(scene=areas[test.get("methodname")], test=test.get("methodname"), result=test.get("result"),
                              message=test.findtext("failure/message"), xml=str(path),
                              sha256=hashlib.sha256(data).hexdigest()))
report = dict(name="ashen.scene.reviewed", sourceSha=os.environ["SOURCE_SHA"],
              run=os.environ["GITHUB_RUN_ID"], attempt=os.environ["GITHUB_RUN_ATTEMPT"], cases=cases)
Path("scene-readback.json").write_text(json.dumps(report, indent=2))
if {c["test"] for c in cases} != set(areas) or any(c["result"] != "Passed" for c in cases):
    raise SystemExit("Scene verification incomplete or failed. Inspect scene-readback.json")
