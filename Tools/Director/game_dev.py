"""Read-only routing of community guidance under Ashen Crypt's existing Director."""
import argparse
import json
import re
from pathlib import Path

from official_unity import inventory


def route(task, catalog, project):
    project = Path(project)
    version_file = project / "ProjectSettings/ProjectVersion.txt"
    if not (project / "Assets").is_dir() or not version_file.is_file():
        raise ValueError("A real Unity project path is required")
    match = re.search(r"^m_EditorVersion:\s*(\S+)", version_file.read_text(), re.MULTILINE)
    if not match:
        raise ValueError("Unity project version is missing")
    text = task.casefold()
    selected = []
    groups = [
        (("animation", "animator", "bogenschuss", "retarget", "rigging"),
         ("unity-animation", "game-development")),
        (("feedback", "treffergefühl", "hit-stop", "game feel"), ("game-feel", "game-development")),
        (("level", "wald", "forest", "dungeon", "blockout"), ("level-design",)),
        (("prozedural", "procedural", "generator", "seed"), ("procedural-gen",)),
        (("equipment", "ausrüstung", "inventar", "loot", "xp", "rüstung"), ("rpg",)),
        (("utility", "behavior tree", "verhaltensbaum"), ("ai-behavior-trees-utility-ai",)),
        (("npc", "gegner", "wahrnehmung", "enemy ai"), ("game-ai",)),
        (("save", "load", "spielstand", "migration"), ("save-systems",)),
        (("dialog", "quest"), ("dialogue-systems",)),
        (("ui", "hud", "menü", "touch"), ("game-ui-ux",)),
        (("kamera", "camera"), ("camera-systems",)),
        (("performance", "profil", "fps", "framezeit", "stutter"), ("performance-optimization",)),
        (("apk", "build size", "buildgröße"), ("unity-build-size",)),
        (("build", "il2cpp"), ("unity-build-pipeline",)),
        (("audio", "sound", "musik"), ("audio-design",)),
        (("art", "asset", "textur", "material"), ("create-game-assets",)),
    ]
    for keywords, names in groups:
        if any((re.search(r"\b" + re.escape(k) + r"\b", text) if len(k) <= 3 else k in text)
               for k in keywords):
            selected.extend(name for name in names if name not in selected)
    installed = {s["name"]: s for s in inventory(catalog)}
    return dict(schema="ashen.director.gamedev-route.v1", task=task,
                unity=match.group(1), source_baseline="6000.3.28f1",
                verify_apis_against_project_pin=True, advisory_only=True,
                preserve_core_authority=True,
                skills=[installed.get(n, dict(name=n, status="MISSING")) for n in selected[:3]],
                additional_matches=selected[3:],
                instruction="Read selected guidance before use. Preserve AreArpg.Core, its 100-ms "
                            "ticks, saves and existing controls; animation is a projection. "
                            "Use official Unity tools for live execution and verify it separately.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--task", required=True)
    args = parser.parse_args()
    print(json.dumps(route(args.task, args.catalog, args.project), ensure_ascii=False, indent=2))
