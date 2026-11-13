"""Routing must distinguish installed guidance from live engine/device evidence."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from official_unity import inventory, route


class OfficialUnityRoutingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        names = ["unity-cli", "unity-package-management", "project-auditor-fixes",
                 "optimize-audio", "ui-ugui", "urp-postprocessing", "uvcs-core",
                 "uvcs-code-review", "setup-multiplayer-services"]
        skills = []
        for name in names:
            path = self.root / (name + ".md")
            path.write_text("reviewed skill " + name)
            skills.append(dict(name=name, path=str(path), revision="a" * 40,
                               sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        self.catalog = self.root / "catalog.json"
        self.catalog.write_text(json.dumps(dict(schema="ashen.official-unity-tooling.v1", skills=skills)))

    def test_android_routes_to_installed_diagnostics_without_claiming_device_pass(self):
        result = route("Android Logcat und Tablet Performance", self.catalog, "git")
        self.assertIn("project-auditor-fixes", [s["name"] for s in result["skills"]])
        self.assertEqual("com.unity.mobile.android-logcat", result["packages"][0]["id"])
        self.assertEqual("NOT_MEASURED", result["device_evidence"])

    def test_modified_skill_is_not_reported_as_verified_installation(self):
        (self.root / "unity-cli.md").write_text("changed after installation")
        item = next(s for s in inventory(self.catalog) if s["name"] == "unity-cli")
        self.assertEqual("CHANGED", item["status"])

    def test_missing_skill_is_not_reported_as_installed(self):
        (self.root / "unity-cli.md").unlink()
        item = next(s for s in inventory(self.catalog) if s["name"] == "unity-cli")
        self.assertEqual("MISSING", item["status"])

    def test_git_review_does_not_route_through_uvcs(self):
        self.assertNotIn("uvcs-core", [s["name"] for s in route("review merge", self.catalog, "git")["skills"]])

    def test_uvcs_review_includes_required_core(self):
        names = [s["name"] for s in route("review", self.catalog, "uvcs")["skills"]]
        self.assertIn("uvcs-core", names)
        self.assertIn("uvcs-code-review", names)

    def test_netcode_stays_an_evaluation_until_gameplay_decision(self):
        result = route("Netcode multiplayer", self.catalog, "git")
        item = next(p for p in result["packages"] if p["id"] == "com.unity.netcode.gameobjects")
        self.assertEqual("2.14.0", item["version"])
        self.assertEqual("ISOLATED_EVALUATION", item["role"])
        self.assertTrue(item["gameplay_decision_required"])
        self.assertTrue(result["advisory_only"])


if __name__ == "__main__":
    unittest.main()
