#!/usr/bin/env python3
"""Read-only Director routing for pinned official Unity guidance; never launches services."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def inventory(catalog):
    data = json.loads(Path(catalog).read_text())
    if data.get("schema") != "ashen.official-unity-tooling.v1":
        raise ValueError("Unsupported official Unity tooling catalog")
    result = []
    names = set()
    for item in data["skills"]:
        if item["name"] in names:
            raise ValueError("Duplicate skill: " + item["name"])
        names.add(item["name"])
        path = Path(item["path"])
        status = "MISSING"
        if path.is_file():
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            status = "INSTALLED" if digest == item["sha256"] else "CHANGED"
        result.append(dict(item, status=status))
    return sorted(result, key=lambda item: item["name"])


def route(task, catalog, vcs="git"):
    text = task.casefold()
    selected = {"unity-cli"}
    packages = []
    groups = [
        (("package", "paket", "install", "logcat", "netcode"), ["unity-package-management"]),
        (("android", "tablet", "performance", "profil", "fps", "memory", "speicher"), ["project-auditor-fixes"]),
        (("audio", "sound", "musik"), ["audio-setup-mixers", "optimize-audio"]),
        (("ui", "hud", "menü", "menu", "touch"), ["ui", "ui-ugui", "optimize-text-mesh-pro"]),
        (("render", "shader", "urp", "licht"), ["urp-postprocessing", "validate-urp-render-graph-renderer-feature"]),
        (("asset", "sprite", "atlas"), ["manage-sprite-atlas", "unity-pipeline"]),
        (("physik", "physics", "collision", "kollision"), ["fix-3d-physics-collisions"]),
    ]
    for keywords, names in groups:
        if any((re.search(r"\b" + re.escape(word) + r"\b", text) if len(word) <= 3 else word in text)
               for word in keywords):
            selected.update(names)
    if any(word in text for word in ("android", "tablet", "logcat", "adb")):
        packages.append(dict(id="com.unity.mobile.android-logcat", version="1.4.7",
                             role="EDITOR_DIAGNOSTICS", minimum_unity="2021.3"))
    if any(word in text for word in ("netcode", "multiplayer", "ko-op", "coop")):
        selected.update(("setup-multiplayer-services", "unity-package-management"))
        packages.append(dict(id="com.unity.netcode.gameobjects", version="2.14.0",
                             role="ISOLATED_EVALUATION", minimum_unity="6000.0",
                             gameplay_decision_required=True))
    if vcs == "uvcs":
        selected.add("uvcs-core")
        for word, name in (("review", "uvcs-code-review"), ("merge", "uvcs-merge"),
                           ("switch", "uvcs-switch"), ("ursache", "uvcs-root-cause")):
            if word in text:
                selected.add(name)
    installed = {item["name"]: item for item in inventory(catalog)}
    skills = [installed.get(name, dict(name=name, status="MISSING")) for name in sorted(selected)]
    return dict(schema="ashen.director.official-unity-route.v1", task=task,
                advisory_only=True, vcs=vcs, skills=skills, packages=packages,
                device_evidence="NOT_MEASURED",
                instruction="Read the selected SKILL.md and applicable references before use; "
                            "verify the current CLI, project, package and device state separately.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("inventory", "route"))
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--task", default="continue")
    parser.add_argument("--vcs", choices=("git", "uvcs"), default="git")
    args = parser.parse_args()
    result = inventory(args.catalog) if args.action == "inventory" else route(args.task, args.catalog, args.vcs)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
