import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from game_dev import route
from official_unity import inventory


class GameDevRoutingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.project = self.root / "project"
        (self.project / "ProjectSettings").mkdir(parents=True)
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.8f1\n")
        (self.project / "Assets").mkdir()
        names = ["unity-animation", "game-development", "game-feel", "level-design", "procedural-gen", "rpg"]
        skills = []
        for name in names:
            path = self.root / name / "SKILL.md"
            path.parent.mkdir()
            path.write_text(name)
            ref = path.parent / "reference.md"
            ref.write_text("bundled guidance")
            digest = lambda f: hashlib.sha256(f.read_bytes()).hexdigest()
            skills.append(dict(name=name, path=str(path), sha256=digest(path),
                               files={"SKILL.md": digest(path), "reference.md": digest(ref)}))
        self.catalog = self.root / "catalog.json"
        self.catalog.write_text(json.dumps(dict(schema="ashen.skill-catalog.v1", skills=skills)))

    def test_hunter_animation_loads_the_attached_principles_with_the_animator_skill(self):
        result = route("Jäger Bogenschuss Animation", self.catalog, self.project)
        self.assertEqual("6000.6.8f1", result["unity"])
        self.assertEqual(["unity-animation", "game-development"], [s["name"] for s in result["skills"]])
        self.assertTrue(result["preserve_core_authority"])

    def test_procedural_forest_uses_design_and_generation_without_a_2d_engine(self):
        result = route("Prozeduraler Wald und Dungeon Leveldesign", self.catalog, self.project)
        self.assertEqual(["level-design", "procedural-gen"], [s["name"] for s in result["skills"]])
        self.assertTrue(result["advisory_only"])

    def test_changed_bundled_reference_is_not_reported_as_installed(self):
        (self.root / "unity-animation/reference.md").write_text("changed")
        skill = next(s for s in inventory(self.catalog) if s["name"] == "unity-animation")
        self.assertEqual("CHANGED", skill["status"])

    def test_missing_project_is_not_reported_as_a_verified_unity_project(self):
        with self.assertRaises(ValueError):
            route("animation", self.catalog, self.root / "absent")


if __name__ == "__main__":
    unittest.main()
